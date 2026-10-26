using System;
using System.Collections.Generic;

namespace Moose.Hapis.Extra;

/// <summary>
/// Finds the crude oil pumps and water treatment pipe access points.
/// Both are <see cref="WaterCatcher"/>s, and Rust keeps every live one in
/// <see cref="WaterCatcher.CollectWorkQueue"/>, so nothing here walks the full entity list.
/// Keyword matches are cached per prefab, so after the first sighting each entity is one dictionary lookup.
/// </summary>
public static class LiquidSources
{
	private static readonly string[] PumpKeywords = HapisExtraConstants.PumpPrefabKeywords.Split([','], StringSplitOptions.RemoveEmptyEntries);
	private static readonly string[] WaterKeywords = NormalizeAll(HapisExtraConstants.WaterPrefabKeywords.Split([','], StringSplitOptions.RemoveEmptyEntries));
	private static readonly Dictionary<uint, bool> PumpPrefabs = new();
	private static readonly Dictionary<uint, bool> WaterPrefabs = new();

	// true if this entity's prefab path matches the pump keywords (used by the take guards too)
	public static bool IsPump(BaseEntity entity)
	{
		if (!PumpPrefabs.TryGetValue(entity.prefabID, out var match))
		{
			match = MatchesPumpKeywords(entity.PrefabName);
			PumpPrefabs[entity.prefabID] = match;
		}
		return match;
	}

	public static bool IsWaterSource(BaseEntity entity)
	{
		if (!WaterPrefabs.TryGetValue(entity.prefabID, out var match))
		{
			match = MatchesWaterKeywords(entity.PrefabName);
			WaterPrefabs[entity.prefabID] = match;
		}
		return match;
	}

	public static void CollectPumps(List<WaterCatcher> pumps)
	{
		pumps.Clear();
		WaterCatcher.CollectWorkQueue.RunOnAll(catcher =>
		{
			if (catcher != null && !catcher.IsDestroyed && IsPump(catcher))
			{
				pumps.Add(catcher);
			}
		});
	}

	public static void CollectWaterSources(List<WaterCatcher> sources)
	{
		sources.Clear();
		WaterCatcher.CollectWorkQueue.RunOnAll(catcher =>
		{
			// only things that really have an inventory (never touches decoration)
			if (catcher != null && !catcher.IsDestroyed && catcher.inventory != null && IsWaterSource(catcher))
			{
				sources.Add(catcher);
			}
		});
	}

	// ALL keywords must appear in the prefab path, ignoring case
	private static bool MatchesPumpKeywords(string path)
	{
		if (string.IsNullOrEmpty(path) || PumpKeywords.Length == 0)
		{
			return false;
		}

		for (var i = 0; i < PumpKeywords.Length; i++)
		{
			var w = PumpKeywords[i].Trim();
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

	// ALL keywords must appear in the prefab path, ignoring case, _ - and spaces
	private static bool MatchesWaterKeywords(string path)
	{
		if (string.IsNullOrEmpty(path) || WaterKeywords.Length == 0)
		{
			return false;
		}

		path = Normalize(path);
		for (var i = 0; i < WaterKeywords.Length; i++)
		{
			if (path.IndexOf(WaterKeywords[i], StringComparison.Ordinal) < 0)
			{
				return false;
			}
		}
		return true;
	}

	private static string Normalize(string s)
	{
		return s.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
	}

	private static string[] NormalizeAll(string[] words)
	{
		for (var i = 0; i < words.Length; i++)
		{
			words[i] = Normalize(words[i]);
		}
		return words;
	}
}
