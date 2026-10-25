using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moose.Hapis.Extra;

/// <summary>
/// Water treatment pipe access points:
/// filled to FULL on server start, then back to FULL every WaterRefillHours.
/// </summary>
public static class WaterFiller
{
	private static readonly HashSet<string> Warned = [];
	private static readonly List<BaseNetworkable> Sources = [];
	private static float _nextRefill = -1f;
	private static float _nextRescan = 0f;
	private static int _lastLoggedCount = -1;

	private static void Warn(string key, string msg)
	{
		if (Warned.Add(key))
		{
			HapisExtraMod.Log("WaterFill WARNING: " + msg);
		}
	}

	private static string Norm(string s)
	{
		return s.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
	}

	private static void Rescan()
	{
		Sources.Clear();
		var words = HapisExtraMod.Config.WaterPrefabKeywords.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		for (var i = 0; i < words.Length; i++)
		{
			words[i] = Norm(words[i]);
		}

		var all = EntityList.Get();
		for (var k = 0; k < all.Count; k++)
		{
			var n = all[k];
			if (n.IsDestroyed)
			{
				continue;
			}
			var path = n.PrefabName;
			if (string.IsNullOrEmpty(path))
			{
				continue;
			}
			path = Norm(path);

			var allMatch = words.Length > 0;
			for (var i = 0; i < words.Length; i++)
			{
				if (path.IndexOf(words[i], StringComparison.Ordinal) < 0)
				{
					allMatch = false;
					break;
				}
			}
			if (!allMatch)
			{
				continue;
			}

			// only things that really have an inventory (never touches decoration)
			if (PumpFiller.FindContainer(n) == null)
			{
				continue;
			}
			Sources.Add(n);
		}

		if (Sources.Count != _lastLoggedCount)
		{
			_lastLoggedCount = Sources.Count;
			HapisExtraMod.Log("WaterFill: found " + Sources.Count + " matching water source(s) for '" + HapisExtraMod.Config.WaterPrefabKeywords + "'.");
			for (var i = 0; i < Sources.Count; i++)
			{
				HapisExtraMod.Log("WaterFill:   " + Sources[i].GetType().Name + " at " + Sources[i].transform.position + "  " + Sources[i].PrefabName);
			}
		}
	}

	private static string LiquidShortname(BaseNetworkable src)
	{
		var configured = HapisExtraMod.Config.WaterItemShortname;
		if (!string.IsNullOrEmpty(configured))
		{
			return configured;
		}

		var def = PumpFiller.GetField(src, "defaultLiquid") as ItemDefinition;
		if (def != null && !string.IsNullOrEmpty(def.shortname))
		{
			return def.shortname;
		}

		return "water";
	}

	// called every few seconds by the scheduler; only does real work when a fill is due
	public static void Run()
	{
		try
		{
			var now = Time.realtimeSinceStartup;
			var cfg = HapisExtraMod.Config;

			if (Sources.Count == 0)
			{
				if (now < _nextRescan)
				{
					return;
				}
				_nextRescan = now + 30f;
				Rescan();
				if (Sources.Count == 0)
				{
					return;
				}
			}

			if (_nextRefill < 0f)
			{
				_nextRefill = now;   // fill as soon as they are found
			}
			if (now < _nextRefill)
			{
				return;
			}

			Rescan();
			var filled = 0;
			for (var i = 0; i < Sources.Count; i++)
			{
				var src = Sources[i];
				if (src == null || src.IsDestroyed)
				{
					continue;
				}

				var c = PumpFiller.FindContainer(src);
				if (c == null)
				{
					continue;
				}

				var max = c.maxStackSize;
				if (max <= 0 && c.itemList != null && c.itemList.Count > 0)
				{
					max = c.itemList[0].MaxStackable();
				}
				if (max <= 0)
				{
					max = 1000;
				}

				if (PumpFiller.SetAmount(src, max, LiquidShortname(src)))
				{
					filled++;
				}
			}

			if (cfg.WaterRefillHours > 0f)
			{
				_nextRefill = now + cfg.WaterRefillHours * 3600f;
				HapisExtraMod.Log("WaterFill: filled " + filled + " of " + Sources.Count + " source(s) to full. Next fill in " + cfg.WaterRefillHours + " hour(s).");
			}
			else
			{
				_nextRefill = float.MaxValue;
				HapisExtraMod.Log("WaterFill: filled " + filled + " of " + Sources.Count + " source(s) to full. No further fills (WaterRefillHours is 0).");
			}
		}
		catch (Exception e)
		{
			Warn("run", "failed: " + e.Message);
		}
	}
}
