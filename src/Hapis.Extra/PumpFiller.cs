using System;
using System.Collections.Generic;
using System.Reflection;
using Moose.HapisExtra;
using UnityEngine;

namespace Moose.Hapis.Extra;

/// <summary>
/// Crude oil pumps (WaterCatcher / crudeoilproducer).
/// <list type="number">
/// <item>On server start: raise each pump to PumpStartAmount (never lowers it).</item>
/// <item>PumpTrickleDelayMinutes later: add crude slowly until PumpMaxAmount
/// (rate = (Max - Start) / PumpTrickleHours, so 100 -> 300 takes 2 hours).</item>
/// </list>
/// </summary>
public static class PumpFiller
{
	private static readonly HashSet<string> Warned = [];
	private static readonly List<BaseNetworkable> Pumps = [];
	private static readonly Dictionary<BaseNetworkable, float> Carry = new();
	private static float _nextRescan = 0f;
	private static float _trickleStart = -1f;
	private static float _lastTrickle = -1f;
	private static float _nextStatusLog = 0f;
	private static int _lastLoggedCount = -1;
	private static bool _ioLogged;
	private static bool _startFillDone;

	private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

	private static void Warn(string key, string msg)
	{
		if (Warned.Add(key))
		{
			HapisExtraMod.Log("PumpFill WARNING: " + msg);
		}
	}

	// true if this entity's prefab path matches the pump keywords (used by the take guard too)
	public static bool IsPump(BaseNetworkable? n)
	{
		if (n == null)
		{
			return false;
		}
		var path = n.PrefabName;
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}

		var words = HapisExtraMod.Config.PumpPrefabKeywords.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		if (words.Length == 0)
		{
			return false;
		}

		for (var i = 0; i < words.Length; i++)
		{
			var w = words[i].Trim();
			if (w.Length == 0)
			{
				continue;
			}
			if (path.IndexOf(w, StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
		}
		return true;
	}

	private static void Rescan()
	{
		Pumps.Clear();

		var all = EntityList.Get();
		for (var k = 0; k < all.Count; k++)
		{
			var n = all[k];
			if (n.IsDestroyed)
			{
				continue;
			}
			if (IsPump(n))
			{
				Pumps.Add(n);
			}
		}

		if (Pumps.Count != _lastLoggedCount)
		{
			_lastLoggedCount = Pumps.Count;
			HapisExtraMod.Log("PumpFill: found " + Pumps.Count + " matching pump(s) for '" + HapisExtraMod.Config.PumpPrefabKeywords + "'.");
			for (var i = 0; i < Pumps.Count; i++)
			{
				HapisExtraMod.Log("PumpFill:   pump " + i + " at " + Pumps[i].transform.position);
			}
		}

		if (HapisExtraMod.Config.LogPumpIO && !_ioLogged && Pumps.Count > 0)
		{
			_ioLogged = true;
			PumpIODump.Run(Pumps);
		}
	}

	public static object? GetField(object target, string name)
	{
		for (var t = target.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
		{
			var f = t.GetField(name, Fields);
			if (f == null)
			{
				continue;
			}
			try
			{
				return f.GetValue(target);
			}
			catch (Exception)
			{
				return null;
			}
		}
		return null;
	}

	public static ItemContainer? FindContainer(BaseNetworkable e)
	{
		for (var t = e.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
		{
			var fields = t.GetFields(Fields);
			for (var i = 0; i < fields.Length; i++)
			{
				if (fields[i].FieldType != typeof(ItemContainer))
				{
					continue;
				}
				try
				{
					var c = fields[i].GetValue(e) as ItemContainer;
					if (c != null)
					{
						return c;
					}
				}
				catch (Exception)
				{
				}
			}
		}
		return null;
	}

	private static string LiquidShortname(BaseNetworkable pump)
	{
		var configured = HapisExtraMod.Config.PumpItemShortname;
		if (!string.IsNullOrEmpty(configured))
		{
			return configured;
		}

		var def = GetField(pump, "defaultLiquid") as ItemDefinition;
		if (def != null && !string.IsNullOrEmpty(def.shortname))
		{
			return def.shortname;
		}

		return "crude.oil";
	}

	// called every few seconds by the scheduler
	public static void Run()
	{
		try
		{
			var now = Time.realtimeSinceStartup;
			var cfg = HapisExtraMod.Config;

			// no pumps known yet: look again about every 30 seconds
			if (Pumps.Count == 0)
			{
				if (now < _nextRescan)
				{
					return;
				}
				_nextRescan = now + 30f;
				Rescan();
				if (Pumps.Count == 0)
				{
					return;
				}
			}

			// step 1: start fill (once per server start)
			if (!_startFillDone)
			{
				_startFillDone = true;
				var raised = 0;
				for (var i = 0; i < Pumps.Count; i++)
				{
					var pump = Pumps[i];
					if (pump == null || pump.IsDestroyed)
					{
						continue;
					}
					if (GetAmount(pump) >= cfg.PumpStartAmount)
					{
						continue;
					}
					if (SetAmount(pump, cfg.PumpStartAmount))
					{
						raised++;
					}
				}

				_trickleStart = now + Mathf.Max(cfg.PumpTrickleDelayMinutes, 0f) * 60f;
				_lastTrickle = _trickleStart;
				_nextStatusLog = _trickleStart;
				HapisExtraMod.Log("PumpFill: start fill done (" + raised + " of " + Pumps.Count + " pump(s) raised to " + cfg.PumpStartAmount + "). Slow refill begins in " + cfg.PumpTrickleDelayMinutes + " minute(s).");
				return;
			}

			// step 2: slow refill
			if (now < _trickleStart)
			{
				return;
			}
			if (cfg.PumpTrickleHours <= 0f || cfg.PumpMaxAmount <= cfg.PumpStartAmount)
			{
				return;
			}

			var ratePerSecond = (float)(cfg.PumpMaxAmount - cfg.PumpStartAmount) / (cfg.PumpTrickleHours * 3600f);
			var dt = now - _lastTrickle;
			_lastTrickle = now;

			for (var i = 0; i < Pumps.Count; i++)
			{
				var pump = Pumps[i];
				if (pump == null || pump.IsDestroyed)
				{
					continue;
				}

				var current = GetAmount(pump);
				if (current >= cfg.PumpMaxAmount)
				{
					Carry.Remove(pump);
					continue;
				}

				Carry.TryGetValue(pump, out var c);
				c += ratePerSecond * dt;

				var whole = (int)c;
				if (whole >= 1)
				{
					c -= whole;
					SetAmount(pump, Mathf.Min(cfg.PumpMaxAmount, current + whole));
				}
				Carry[pump] = c;
			}

			// status line every 10 minutes
			if (now >= _nextStatusLog)
			{
				_nextStatusLog = now + 600f;
				var line = "";
				for (var i = 0; i < Pumps.Count; i++)
				{
					if (Pumps[i] == null || Pumps[i].IsDestroyed)
					{
						continue;
					}
					line += (line.Length > 0 ? ", " : "") + GetAmount(Pumps[i]);
				}
				HapisExtraMod.Log("PumpFill: pump amounts now: " + line + " (max " + cfg.PumpMaxAmount + ").");
			}
		}
		catch (Exception e)
		{
			Warn("run", "failed: " + e.Message);
		}
	}

	public static int GetAmount(BaseNetworkable pump)
	{
		var c = FindContainer(pump);
		if (c == null || c.itemList == null || c.itemList.Count == 0)
		{
			return 0;
		}
		return c.itemList[0].amount;
	}

	public static bool SetAmount(BaseNetworkable pump, int amount)
	{
		return SetAmount(pump, amount, null);
	}

	// sets the container's liquid to exactly this amount (creates the item if it is empty)
	public static bool SetAmount(BaseNetworkable pump, int amount, string? shortnameOverride)
	{
		var c = FindContainer(pump);
		if (c == null)
		{
			Warn("nocontainer-" + pump.GetType().Name, "no ItemContainer found on " + pump.GetType().Name + " (" + pump.PrefabName + ").");
			return false;
		}

		var existing = (c.itemList != null && c.itemList.Count > 0) ? c.itemList[0] : null;
		if (existing != null)
		{
			existing.amount = amount;
			existing.MarkDirty();
			return true;
		}

		var shortname = !string.IsNullOrEmpty(shortnameOverride) ? shortnameOverride : LiquidShortname(pump);
		var item = ItemManager.CreateByName(shortname, 1);
		if (item == null)
		{
			Warn("noitem-" + shortname, "item '" + shortname + "' not found. Set the ItemShortname in the XML.");
			return false;
		}

		item.amount = amount;
		var moved = item.MoveToContainer(c);

		if (!moved)
		{
			// the inventory is locked for players; unlock for this one move, then lock it again
			Refl.Call(c, "SetLocked", false);
			moved = item.MoveToContainer(c);
			var lockObj = GetField(pump, "lockInventory");
			var wasLocked = lockObj is bool locked && locked;
			if (wasLocked)
			{
				Refl.Call(c, "SetLocked", true);
			}
		}

		if (!moved)
		{
			item.Remove();
			Warn("nomove-" + pump.GetType().Name, "could not put " + shortname + " into the container at " + pump.transform.position + " (container refused).");
			return false;
		}

		return true;
	}
}
