using System;
using System.Collections.Generic;
using HarmonyLib;
using Moose.Hapis.RustEdit2;
using Rust.Ai.Gen2.Nav;
using UnityEngine;

namespace Moose.Hapis.NPC.Behaviours;

internal static class OilRigNavmesh
{
	private const float RigReach = 120f;
	private const float SpawnerReach = 40f;
	private const float RigRetrySeconds = 5f;

	private static readonly List<(Vector3 Spot, float Reach)> Anchors = new();
	private static bool _mapRead;
	private static bool _rigsFound;
	private static float _nextRigLook;
	private static int _rigs;
	private static int _zones;
	private static int _spawners;
	private static int _kept;

	private static void Find()
	{
		if (!_mapRead)
		{
			_mapRead = true;
			ReadMap();
		}

		if (_rigsFound || Time.realtimeSinceStartup < _nextRigLook)
		{
			return;
		}

		_nextRigLook = Time.realtimeSinceStartup + RigRetrySeconds;
		var monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();
		if (monuments == null || monuments.Length == 0)
		{
			return;
		}

		_rigsFound = true;
		foreach (var monument in monuments)
		{
			if (monument != null && monument.IsOilRig())
			{
				Anchors.Add((monument.transform.position, RigReach));
				_rigs++;
			}
		}
	}

	private static void ReadMap()
	{
		if (Re2Map.Usable)
		{
			foreach (var entity in Re2Map.Entities)
			{
				if (entity.Type == "CH47LandingZone")
				{
					Anchors.Add((entity.Position, RigReach));
					_zones++;
				}
				else if (entity.Type == "NPCSpawner")
				{
					Anchors.Add((entity.Position, SpawnerReach));
					_spawners++;
				}
			}

			return;
		}

		foreach (var spot in HapisNPC.SpawnerSpots())
		{
			Anchors.Add((spot, SpawnerReach));
			_spawners++;
		}
	}

	public static bool NearRig(Bounds tile)
	{
		Find();
		if (Anchors.Count == 0)
		{
			return false;
		}

		foreach (var anchor in Anchors)
		{
			var x = Mathf.Max(Mathf.Abs(anchor.Spot.x - tile.center.x) - tile.extents.x, 0f);
			var z = Mathf.Max(Mathf.Abs(anchor.Spot.z - tile.center.z) - tile.extents.z, 0f);
			if (x * x + z * z <= anchor.Reach * anchor.Reach)
			{
				_kept++;
				return true;
			}
		}

		return false;
	}

	public static void Report()
	{
		Find();
		HapisLog.Info(Anchors.Count == 0 ? "Oil Rigs: none in this map" : "Oil Rigs: navmesh kept at sea around " + _rigs + " oil rigs, " + _zones + " CH47 landing zones and " + _spawners + " NPC spawners (" + _kept + " sea tiles kept)");
	}
}

[HarmonyPatch(typeof(RustNavmesh), nameof(RustNavmesh.IsTileFarFromShore), typeof(int), typeof(int))]
internal static class OilRigNavmeshPatch
{
	[HarmonyPostfix]
	private static void Postfix(RustNavmesh __instance, int __0, int __1, ref bool __result)
	{
		if (!__result)
		{
			return;
		}

		try
		{
			var tile = __instance.rcExpandTileBounds(__instance.rcCalcTileBounds(new Vector2Int(__0, __1)));
			if (OilRigNavmesh.NearRig(tile))
			{
				__result = false;
			}
		}
		catch (Exception e)
		{
			HapisLog.Once("Oil Rigs navmesh", e);
		}
	}
}
