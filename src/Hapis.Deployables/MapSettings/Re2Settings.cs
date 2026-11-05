using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Moose.Hapis.RustEdit2;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Moose.Hapis.Deployables.MapSettings;

internal static class Re2Settings
{
	private const float Grid = 20f;

	private static readonly HashSet<(uint, int, int, int)> IndestructibleSpots = new();
	private static readonly Dictionary<(uint, int, int, int), JObject> SignSpots = new();
	private static readonly HashSet<uint> WatchedPrefabs = new();
	private static readonly HashSet<BaseCombatEntity> Indestructible = new();
	private static readonly HashSet<BaseEntity> CustomSigns = new();
	private static readonly FieldInfo? TierField = AccessTools.Field(typeof(TriggerRadiation), "radiationTier");

	private static bool _built;
	private static bool _any;

	private static (uint, int, int, int) Key(uint prefabId, Vector3 position)
	{
		return (prefabId, Mathf.RoundToInt(position.x * Grid), Mathf.RoundToInt(position.y * Grid), Mathf.RoundToInt(position.z * Grid));
	}

	private static void Build()
	{
		if (_built)
		{
			return;
		}

		if (World.Serialization?.world?.maps == null || World.Serialization.world.maps.Count == 0)
		{
			return;
		}

		_built = true;
		if (!Re2Map.Usable)
		{
			return;
		}

		var signs = 0;
		foreach (var entity in Re2Map.Entities)
		{
			if (entity.Type != "Prefab" || entity.PrefabId == 0)
			{
				continue;
			}

			var key = Key(entity.PrefabId, entity.Position);
			if (entity.Part("BaseCombatEntity")?["Indestructible"] is JValue { Type: JTokenType.Boolean } flag && (bool)flag)
			{
				IndestructibleSpots.Add(key);
				WatchedPrefabs.Add(entity.PrefabId);
			}

			if (entity.Part("Signage") is { } sign && HasImages(sign))
			{
				SignSpots[key] = sign;
				WatchedPrefabs.Add(entity.PrefabId);
				signs++;
			}
		}

		_any = WatchedPrefabs.Count > 0;
		HapisLog.Info("RustEdit 2 Settings: " + IndestructibleSpots.Count + " Indestructible Entities, " + signs + " Signs With Images");
	}

	public static void OnEntitySpawned(BaseNetworkable networkable)
	{
		Build();
		if (!_any || networkable == null || !WatchedPrefabs.Contains(networkable.prefabID))
		{
			return;
		}

		var key = Key(networkable.prefabID, networkable.transform.position);
		if (networkable is BaseCombatEntity combat && IndestructibleSpots.Contains(key))
		{
			Indestructible.Add(combat);
		}

		if (networkable is ISignage signage && networkable is BaseEntity { OwnerID: 0uL } && SignSpots.TryGetValue(key, out var data))
		{
			ApplySign(signage, data);
			var sign = (BaseEntity)networkable;
			LockSign(sign);
			CustomSigns.Add(sign);
		}
	}

	public static bool IsCustomSign(BaseEntity entity)
	{
		if (CustomSigns.Count == 0 || !CustomSigns.Contains(entity))
		{
			return false;
		}

		if (entity.IsDestroyed)
		{
			CustomSigns.Remove(entity);
			return false;
		}

		return true;
	}

	public static bool IsIndestructible(BaseCombatEntity entity)
	{
		if (Indestructible.Count == 0 || !Indestructible.Contains(entity))
		{
			return false;
		}

		if (entity.IsDestroyed)
		{
			Indestructible.Remove(entity);
			return false;
		}

		return true;
	}

	public static bool AdminBypass(BasePlayer? player)
	{
		return player != null && player.IsAdmin && player.IsGod() && player.IsFlying;
	}

	private static void LockSign(BaseEntity? entity)
	{
		if (entity == null || entity.HasFlag(BaseEntity.Flags.Locked))
		{
			return;
		}

		entity.OwnerID = 0uL;
		using var flags = entity.StartSetFlags(BaseEntity.FlagsUpdateMode.SendNetworkUpdateImmediate);
		flags.Set(BaseEntity.Flags.Locked, true);
	}

	private static bool HasImages(JObject sign)
	{
		return sign["Images"] is JArray images && images.Any(i => i != null && i.Type != JTokenType.Null);
	}

	private static byte[]? Bytes(JToken? token)
	{
		if (token == null || token.Type == JTokenType.Null)
		{
			return null;
		}

		if (token.Type == JTokenType.String)
		{
			var text = (string?)token;
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}

			var comma = text!.IndexOf(',');
			if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
			{
				text = text.Substring(comma + 1);
			}

			return Convert.FromBase64String(text);
		}

		if (token is JArray array)
		{
			return array.Select(b => (byte)(int)b).ToArray();
		}

		if (token is JObject obj)
		{
			return Bytes(obj["Data"] ?? obj["data"] ?? obj["Bytes"]);
		}

		return null;
	}

	private static void ApplySign(ISignage signage, JObject data)
	{
		// try
		// {
		// 	var current = signage.GetTextureCRCs();
		// 	if (current != null && current.Any(c => c != 0))
		// 	{
		// 		return;
		// 	}
		//
		// 	var images = (JArray)data["Images"]!;
		// 	var count = Math.Max(1, signage.TextureCount);
		// 	var crcs = new uint[count];
		// 	var stored = 0;
		// 	for (var i = 0; i < count && i < images.Count; i++)
		// 	{
		// 		var bytes = Bytes(images[i]);
		// 		if (bytes == null || bytes.Length == 0)
		// 		{
		// 			continue;
		// 		}
		//
		// 		crcs[i] = FileStorage.server.Store(bytes, signage.FileType, signage.NetworkID, (uint)i);
		// 		stored++;
		// 	}
		//
		// 	if (stored > 0)
		// 	{
		// 		signage.SetTextureCRCs(crcs);
		// 	}
		// }
		// catch (Exception e)
		// {
		// 	HapisLog.Once("RustEdit 2 sign images", e);
		// }
	}

	public static void ApplyRadiation()
	{
		if (!Re2Map.Usable)
		{
			return;
		}

		var wanted = new Dictionary<(int, int, int), JObject>();
		foreach (var prefab in Re2Map.WorldPrefabs)
		{
			if (prefab.Data["TriggerRadiation"] is JObject settings)
			{
				wanted[Spot(prefab.Position)] = settings;
			}
		}

		if (wanted.Count == 0)
		{
			return;
		}

		var applied = 0;
		foreach (var trigger in UnityEngine.Object.FindObjectsOfType<TriggerRadiation>())
		{
			var node = trigger.transform;
			for (var depth = 0; depth < 3 && node != null; depth++, node = node.parent)
			{
				if (!wanted.TryGetValue(Spot(node.position), out var settings))
				{
					continue;
				}

				Apply(trigger, settings);
				applied++;
				break;
			}
		}

		HapisLog.Info("RustEdit 2 Settings: Radiation Set On " + applied + " Of " + wanted.Count + " Radiation Zones");
	}

	private static (int, int, int) Spot(Vector3 position)
	{
		return (Mathf.RoundToInt(position.x * Grid), Mathf.RoundToInt(position.y * Grid), Mathf.RoundToInt(position.z * Grid));
	}

	private static void Apply(TriggerRadiation trigger, JObject settings)
	{
		if (settings["RadiationTier"] is JValue tier && tier.Type == JTokenType.Integer && TierField != null)
		{
			TierField.SetValue(trigger, Enum.ToObject(TierField.FieldType, (int)tier));
		}

		if (settings["RadiationAmountOverride"] is JValue amount && amount.Type is JTokenType.Float or JTokenType.Integer)
		{
			trigger.RadiationAmountOverride = (float)amount;
		}

		if (settings["Falloff"] is JValue falloff && falloff.Type is JTokenType.Float or JTokenType.Integer)
		{
			trigger.falloff = (float)falloff;
		}
	}
}
