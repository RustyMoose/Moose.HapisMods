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
	private static readonly List<WaterCatcher> Sources = [];
	private static float _nextRefill = -1f;
	private static float _nextRescan;
	private static int _lastLoggedCount = -1;

	private static void Warn(string key, string msg)
	{
		if (Warned.Add(key))
		{
			HapisExtraMod.Log("WaterFill WARNING: " + msg);
		}
	}

	private static void Rescan()
	{
		LiquidSources.CollectWaterSources(Sources);

		if (Sources.Count != _lastLoggedCount)
		{
			_lastLoggedCount = Sources.Count;
			HapisExtraMod.Log("WaterFill: found " + Sources.Count + " matching water source(s) for '" + HapisExtraConstants.WaterPrefabKeywords + "'.");
			for (var i = 0; i < Sources.Count; i++)
			{
				HapisExtraMod.Log("WaterFill:   " + Sources[i].GetType().Name + " at " + Sources[i].transform.position + "  " + Sources[i].PrefabName);
			}
		}
	}

	private static string LiquidShortname(WaterCatcher src)
	{
		var configured = HapisExtraConstants.WaterItemShortname;
		if (!string.IsNullOrEmpty(configured))
		{
			return configured;
		}

		var def = src.defaultLiquid;
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

				var c = src.inventory;
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

			_nextRefill = now + HapisExtraConstants.WaterRefillHours * 3600f;
			HapisExtraMod.Log("WaterFill: filled " + filled + " of " + Sources.Count + " source(s) to full. Next fill in " + HapisExtraConstants.WaterRefillHours + " hour(s).");
		}
		catch (Exception e)
		{
			Warn("run", "failed: " + e.Message);
		}
	}
}
