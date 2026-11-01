using System.Collections.Generic;
using UnityEngine;

namespace Moose.Hapis.Extra;

internal static class Water
{
	private const int FallbackFull = 1000;

	private static readonly List<LiquidContainer> Found = new();
	private static float _nextFill = -1f;

	public static void Tick()
	{
		var s = Settings.Current;
		if (!s.KeepWaterFull)
		{
			return;
		}

		var now = Time.realtimeSinceStartup;
		if (_nextFill >= 0f && now < _nextFill)
		{
			return;
		}

		_nextFill = s.WaterRefillHours > 0f ? now + s.WaterRefillHours * 3600f : float.MaxValue;

		Found.RemoveAll(w => w == null || w.IsDestroyed);
		if (Found.Count == 0)
		{
			LiquidSources.FindWater(Found);
		}

		if (Found.Count == 0)
		{
			Log.Info("No water sources found for '" + s.WaterPrefabKeywords + "'");
			return;
		}

		var filled = 0;
		foreach (var source in Found)
		{
			var shortname = string.IsNullOrEmpty(s.WaterItemShortname) ? LiquidSources.LiquidOf(source, "water") : s.WaterItemShortname;
			if (LiquidSources.SetAmount(source, Full(source), shortname))
			{
				filled++;
			}
		}

		Log.Info("Filled " + filled + " of " + Found.Count + " water sources"
			+ (s.WaterRefillHours > 0f ? ", next fill in " + s.WaterRefillHours + " h" : string.Empty));
	}

	private static int Full(LiquidContainer source)
	{
		var container = source.inventory;
		if (container.maxStackSize > 0)
		{
			return container.maxStackSize;
		}

		return container.itemList.Count > 0 ? container.itemList[0].MaxStackable() : FallbackFull;
	}
}
