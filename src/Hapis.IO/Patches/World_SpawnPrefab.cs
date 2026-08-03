using System;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(World), nameof(World.SpawnPrefab), typeof(string), typeof(Prefab), typeof(Vector3), typeof(Quaternion), typeof(Vector3))]
internal class WorldSpawn
{
	[HarmonyPrefix]
	private static bool Prefix(string category, Prefab prefab, Vector3 position, Quaternion rotation, Vector3 scale)
	{
		try
		{
			return HapisIO.Instance.OnPrefabSpawning(category, prefab, position, rotation, scale);
		}
		catch(Exception e)
		{
			Debug.LogError("[Hapis.IO] World Spawn Hook Error");
			Debug.LogError(e);
			return true;
		}
	}
}
