using System;
using System.Collections.Generic;
using System.Linq;

namespace Moose.Hapis.Extra;

internal static class LiquidSources
{
	private static readonly Dictionary<uint, bool> PumpPrefabs = new();
	private static readonly Dictionary<uint, bool> WaterPrefabs = new();
	private static readonly HashSet<string> Warned = new();
	private static string[]? _pumpWords;
	private static string[]? _waterWords;

	public static bool IsPump(BaseEntity? entity)
	{
		if (entity is not WaterCatcher)
		{
			return false;
		}

		if (!PumpPrefabs.TryGetValue(entity.prefabID, out var match))
		{
			_pumpWords ??= Words(Settings.Current.PumpPrefabKeywords);
			match = Matches(entity.PrefabName, _pumpWords);
			PumpPrefabs[entity.prefabID] = match;
		}

		return match;
	}

	public static bool IsWater(BaseEntity? entity)
	{
		if (entity is not LiquidContainer)
		{
			return false;
		}

		if (!WaterPrefabs.TryGetValue(entity.prefabID, out var match))
		{
			_waterWords ??= Words(Settings.Current.WaterPrefabKeywords);
			match = Matches(entity.PrefabName, _waterWords);
			WaterPrefabs[entity.prefabID] = match;
		}

		return match;
	}

	public static void FindPumps(List<WaterCatcher> into)
	{
		into.Clear();
		WaterCatcher.CollectWorkQueue.RunOnAll(catcher =>
		{
			if (catcher != null && !catcher.IsDestroyed && IsPump(catcher))
			{
				into.Add(catcher);
			}
		});
	}

	public static void FindWater(List<LiquidContainer> into)
	{
		into.Clear();
		foreach (var entity in BaseNetworkable.serverEntities)
		{
			if (entity is LiquidContainer liquid && !liquid.IsDestroyed && liquid.inventory != null && IsWater(liquid))
			{
				into.Add(liquid);
			}
		}
	}

	public static int Amount(LiquidContainer source)
	{
		var items = source.inventory?.itemList;
		return items != null && items.Count > 0 ? items[0].amount : 0;
	}

	public static bool SetAmount(LiquidContainer source, int amount, string shortname)
	{
		var container = source.inventory;
		if (container == null)
		{
			return false;
		}

		if (container.itemList.Count > 0)
		{
			var held = container.itemList[0];
			held.amount = amount;
			held.MarkDirty();
			return true;
		}

		var item = ItemManager.CreateByName(shortname, 1);
		if (item == null)
		{
			Warn("item " + shortname, "No item called '" + shortname + "', check the shortname setting.");
			return false;
		}

		item.amount = amount;
		var moved = item.MoveToContainer(container);
		if (!moved && container.IsLocked())
		{
			container.SetLocked(false, false);
			moved = item.MoveToContainer(container);
			container.SetLocked(true, false);
		}

		if (!moved)
		{
			item.Remove();
			Warn("move " + source.prefabID, "Could not put " + shortname + " into " + source.ShortPrefabName + " at " + source.transform.position);
			return false;
		}

		return true;
	}

	public static string LiquidOf(LiquidContainer source, string fallback)
	{
		var liquid = source.defaultLiquid;
		return liquid != null && !string.IsNullOrEmpty(liquid.shortname) ? liquid.shortname : fallback;
	}

	private static void Warn(string key, string message)
	{
		if (Warned.Add(key))
		{
			Log.Info(message);
		}
	}

	private static string[] Words(string list)
	{
		return list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(Squash)
			.Where(w => w.Length > 0)
			.ToArray();
	}

	private static bool Matches(string path, string[] words)
	{
		if (string.IsNullOrEmpty(path) || words.Length == 0)
		{
			return false;
		}

		var squashed = Squash(path);
		return words.All(w => squashed.IndexOf(w, StringComparison.Ordinal) >= 0);
	}

	private static string Squash(string text)
	{
		return text.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).Trim().ToLowerInvariant();
	}
}
