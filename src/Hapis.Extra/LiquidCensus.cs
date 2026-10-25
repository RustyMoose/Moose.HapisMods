using System;
using System.Collections.Generic;

namespace Moose.Hapis.Extra;

/// <summary>
/// One-time list of every liquid container prefab on the map
/// (LiquidContainer / WaterCatcher and anything built on them).
/// Log only. Use it to find the real prefab path of the water pipe access points.
/// </summary>
public static class LiquidCensus
{
	private static bool _done;

	private static bool IsLiquid(Type t)
	{
		for (; t != null; t = t.BaseType)
		{
			if (t.Name == "LiquidContainer" || t.Name == "WaterCatcher")
			{
				return true;
			}
		}
		return false;
	}

	public static void RunOnce()
	{
		if (_done)
		{
			return;
		}
		_done = true;

		try
		{
			var counts = new Dictionary<string, int>();
			var info = new Dictionary<string, string>();
			var order = new List<string>();

			var all = EntityList.Get();
			for (var i = 0; i < all.Count; i++)
			{
				var n = all[i];
				if (n.IsDestroyed)
				{
					continue;
				}
				if (!IsLiquid(n.GetType()))
				{
					continue;
				}

				var path = n.PrefabName;
				if (string.IsNullOrEmpty(path))
				{
					path = "(no prefab name)";
				}

				if (!counts.ContainsKey(path))
				{
					counts[path] = 0;
					info[path] = n.GetType().Name + " first at " + n.transform.position;
					order.Add(path);
				}
				counts[path]++;
			}

			HapisExtraMod.Log("LiquidCensus: " + order.Count + " liquid prefab type(s) on the map:");
			var shown = 0;
			for (var i = 0; i < order.Count; i++)
			{
				if (shown >= 80)
				{
					HapisExtraMod.Log("LiquidCensus: (more not shown)");
					break;
				}
				var p = order[i];
				HapisExtraMod.Log("LiquidCensus: " + p + "  x" + counts[p] + "  [" + info[p] + "]");
				shown++;
			}
			HapisExtraMod.Log("LiquidCensus: done.");
		}
		catch (Exception e)
		{
			HapisExtraMod.Log("LiquidCensus failed: " + e.Message);
		}
	}
}
