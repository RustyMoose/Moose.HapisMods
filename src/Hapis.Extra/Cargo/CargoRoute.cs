using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.Extra;

internal static class CargoRoute
{
	private const float AnchorReach = 60f;
	private const int MaxReplaced = 150;

	private static readonly Vector3[] DockRoute =
	{
		new(-1789.1f, 0f, 677.78f),
		new(-1774.55f, 0f, 663.89f),
		new(-1760f, 0f, 650f),
		new(-1743.25f, 0f, 638.75f),
		new(-1726.5f, 0f, 627.5f),
		new(-1709.75f, 0f, 616.25f),
		new(-1693f, 0f, 605f),
		new(-1681.4f, 0f, 586.2f),
		new(-1669.8f, 0f, 567.4f),
		new(-1658.2f, 0f, 548.6f),
		new(-1646.6f, 0f, 529.8f),
		new(-1635f, 0f, 511f),
		new(-1617f, 0f, 506.5f),
		new(-1599f, 0f, 502f),
		new(-1577.5f, 0f, 495.5f),
		new(-1556f, 0f, 489f),
		new(-1556f, 0f, 467.4f),
		new(-1556f, 0f, 445.8f),
		new(-1556f, 0f, 424.2f),
		new(-1556f, 0f, 402.6f),
		new(-1556f, 0f, 381f),
		new(-1556.5f, 0f, 366.5f),
		new(-1557f, 0f, 352f),
		new(-1580.67f, 0f, 344f),
		new(-1604.33f, 0f, 336f),
		new(-1628f, 0f, 328f),
		new(-1643.67f, 0f, 318.67f),
		new(-1659.33f, 0f, 309.33f),
		new(-1675f, 0f, 300f),
		new(-1688.33f, 0f, 287.33f),
		new(-1701.67f, 0f, 274.67f),
		new(-1715f, 0f, 262f),
		new(-1725f, 0f, 243f),
		new(-1735f, 0f, 224f),
		new(-1745f, 0f, 205f),
		new(-1751.26f, 0f, 182.72f)
	};

	private static bool _told;

	public static void Apply(List<Vector3>? path)
	{
		if (path == null || path.Count < 10 || !CargoDocks.Active())
		{
			return;
		}

		var count = path.Count;
		var first = DockRoute[0];
		var last = DockRoute[DockRoute.Length - 1];
		var from = Nearest(path, first);
		var to = Nearest(path, last);
		if (Flat(path[from] - first).magnitude > AnchorReach || Flat(path[to] - last).magnitude > AnchorReach)
		{
			Tell("Cargo docking: the map's cargo path doesn't pass near the dock, dock route not added");
			return;
		}

		if (Vector3.Dot(Flat(first - path[from]), Flat(DockRoute[1] - first)) > 0f)
		{
			from = (from - 1 + count) % count;
		}

		if (Vector3.Dot(Flat(path[to] - last), Flat(last - DockRoute[DockRoute.Length - 2])) > 0f)
		{
			to = (to + 1) % count;
		}

		var replaced = (from - to + count) % count + 1;
		if (replaced > MaxReplaced || replaced >= count)
		{
			Tell("Cargo docking: the map's cargo path runs the wrong way past the dock, dock route not added");
			return;
		}

		var height = path[from].y;
		var travel = new List<Vector3>(count - replaced + DockRoute.Length);
		for (var k = 0; k < count - replaced; k++)
		{
			travel.Add(path[(to - 1 - k + count * 2) % count]);
		}

		foreach (var point in DockRoute)
		{
			travel.Add(new Vector3(point.x, height, point.z));
		}

		travel.Reverse();
		path.Clear();
		path.AddRange(travel);
		Tell("Cargo docking: dock route added to the cargo path (" + replaced + " nodes swapped for " + DockRoute.Length + ")");
	}

	private static int Nearest(List<Vector3> path, Vector3 spot)
	{
		var best = 0;
		var bestGap = float.MaxValue;
		for (var i = 0; i < path.Count; i++)
		{
			var gap = Flat(path[i] - spot).sqrMagnitude;
			if (gap < bestGap)
			{
				bestGap = gap;
				best = i;
			}
		}

		return best;
	}

	private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

	private static void Tell(string message)
	{
		if (_told)
		{
			return;
		}

		_told = true;
		Log.Info(message);
	}
}

[HarmonyPatch(typeof(BaseBoat), nameof(BaseBoat.GenerateOceanPatrolPath), typeof(float), typeof(float))]
internal static class CargoRoutePatch
{
	[HarmonyPostfix]
	private static void Postfix(List<Vector3> __result)
	{
		try
		{
			CargoRoute.Apply(__result);
		}
		catch (Exception e)
		{
			Log.Once("cargo route", e);
		}
	}
}
