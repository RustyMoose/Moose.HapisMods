using System;
using System.Collections.Generic;
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
	private static readonly List<WaterCatcher> Pumps = [];
	private static readonly Dictionary<WaterCatcher, float> Carry = new();
	private static float _nextRescan;
	private static float _trickleStart = -1f;
	private static float _lastTrickle = -1f;
	private static float _nextStatusLog;
	private static int _lastLoggedCount = -1;
	private static bool _startFillDone;

	private static void Warn(string key, string msg)
	{
		if (Warned.Add(key))
		{
			HapisExtraMod.Log("PumpFill WARNING: " + msg);
		}
	}

	private static void Rescan()
	{
		LiquidSources.CollectPumps(Pumps);

		if (Pumps.Count != _lastLoggedCount)
		{
			_lastLoggedCount = Pumps.Count;
			HapisExtraMod.Log("PumpFill: found " + Pumps.Count + " matching pump(s) for '" + HapisExtraConstants.PumpPrefabKeywords + "'.");
			for (var i = 0; i < Pumps.Count; i++)
			{
				HapisExtraMod.Log("PumpFill:   pump " + i + " at " + Pumps[i].transform.position);
			}
		}
	}

	private static string LiquidShortname(WaterCatcher pump)
	{
		var def = pump.defaultLiquid;
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
					if (GetAmount(pump) >= HapisExtraConstants.PumpStartAmount)
					{
						continue;
					}
					if (SetAmount(pump, HapisExtraConstants.PumpStartAmount))
					{
						raised++;
					}
				}

				_trickleStart = now + Mathf.Max(HapisExtraConstants.PumpTrickleDelayMinutes, 0f) * 60f;
				_lastTrickle = _trickleStart;
				_nextStatusLog = _trickleStart;
				HapisExtraMod.Log("PumpFill: start fill done (" + raised + " of " + Pumps.Count + " pump(s) raised to " + HapisExtraConstants.PumpStartAmount + "). Slow refill begins in " + HapisExtraConstants.PumpTrickleDelayMinutes + " minute(s).");
				return;
			}

			// step 2: slow refill
			if (now < _trickleStart)
			{
				return;
			}

			const float ratePerSecond = (HapisExtraConstants.PumpMaxAmount - HapisExtraConstants.PumpStartAmount) / (HapisExtraConstants.PumpTrickleHours * 3600f);
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
				if (current >= HapisExtraConstants.PumpMaxAmount)
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
					SetAmount(pump, Mathf.Min(HapisExtraConstants.PumpMaxAmount, current + whole));
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
				HapisExtraMod.Log("PumpFill: pump amounts now: " + line + " (max " + HapisExtraConstants.PumpMaxAmount + ").");
			}
		}
		catch (Exception e)
		{
			Warn("run", "failed: " + e.Message);
		}
	}

	private static int GetAmount(WaterCatcher pump)
	{
		var c = pump.inventory;
		if (c == null || c.itemList == null || c.itemList.Count == 0)
		{
			return 0;
		}
		return c.itemList[0].amount;
	}

	private static bool SetAmount(WaterCatcher pump, int amount)
	{
		return SetAmount(pump, amount, null);
	}

	// sets the container's liquid to exactly this amount (creates the item if it is empty)
	public static bool SetAmount(WaterCatcher pump, int amount, string? shortnameOverride)
	{
		var c = pump.inventory;
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
		var item = ItemManager.CreateByName(shortname);
		if (item == null)
		{
			Warn("noitem-" + shortname, "item '" + shortname + "' not found. Set the ItemShortname in the XML.");
			return false;
		}

		item.amount = amount;
		if (!item.MoveToContainer(c))
		{
			item.Remove();
			Warn("nomove-" + pump.GetType().Name, "could not put " + shortname + " into the container at " + pump.transform.position + " (container refused).");
			return false;
		}

		return true;
	}
}
