using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ProtoBuf;

namespace Moose.Hapis;

internal static class MapBlocks
{
	private static readonly HashSet<string> Terrain = new(StringComparer.OrdinalIgnoreCase) { "terrain", "height", "water", "splat", "alpha", "biome", "topology" };
	private static readonly List<MapData> Kept = new();

	public static IReadOnlyList<MapData> All
	{
		get
		{
			var live = World.Serialization?.world?.maps;
			if (live != null && live.Count > 0)
			{
				return live;
			}

			return Kept;
		}
	}

	public static MapData? Find(string name)
	{
		return All.FirstOrDefault(m => m != null && m.name == name);
	}

	public static void Keep()
	{
		var live = World.Serialization?.world?.maps;
		if (live == null || live.Count == 0)
		{
			return;
		}

		Kept.Clear();
		foreach (var block in live)
		{
			if (block?.data != null && !Terrain.Contains(block.name ?? string.Empty))
			{
				Kept.Add(block);
			}
		}

		HapisLog.Shared("Map data: kept " + Kept.Count + " map blocks after Rust cleared the map");
	}
}

[HarmonyPatch(typeof(WorldSerialization), nameof(WorldSerialization.ClearMaps))]
internal static class MapBlocksKeepPatch
{
	[HarmonyPrefix]
	private static void Prefix()
	{
		try
		{
			MapBlocks.Keep();
		}
		catch (Exception e)
		{
			HapisLog.Once("map blocks", e);
		}
	}
}
