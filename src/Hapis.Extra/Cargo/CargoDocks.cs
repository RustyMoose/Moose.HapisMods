using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.Extra;

internal static class CargoDocks
{
	public static bool Active()
	{
		if (!Settings.Loaded)
		{
			Settings.Load();
		}

		var s = Settings.Current;
		if (!s.CargoDocking)
		{
			return false;
		}

		if (string.IsNullOrEmpty(s.CargoDockMap))
		{
			return true;
		}

		var names = new[] { World.Name, World.MapFileName, ConVar.Server.levelurl };
		return names.Any(n => !string.IsNullOrEmpty(n) && n.IndexOf(s.CargoDockMap, StringComparison.OrdinalIgnoreCase) >= 0);
	}

	public static List<Vector3> Stops()
	{
		var s = Settings.Current;
		return new List<Vector3> { new(s.CargoDockX, 0f, s.CargoDockZ) };
	}

	public static void Report()
	{
		var s = Settings.Current;
		if (!s.CargoDocking)
		{
			Log.Info("Cargo docking: off");
			return;
		}

		Log.Info("Cargo docking: at " + s.CargoDockX + ", " + s.CargoDockZ
			+ (string.IsNullOrEmpty(s.CargoDockMap) ? " on any map" : " on maps named '" + s.CargoDockMap + "'")
			+ (Active() ? string.Empty : " - not this map, ships sail normally")
			+ ", event timer paused while docked: " + (s.CargoPauseWhileDocked ? "yes" : "no"));
	}

	public static void OnShipSpawned(CargoShip ship)
	{
		if (ship == null || !Active())
		{
			return;
		}

		ship.gameObject.AddComponent<CargoStop>().Begin(ship, Stops());
	}
}

[HarmonyPatch(typeof(CargoShip), nameof(CargoShip.Spawn))]
internal static class CargoSpawnPatch
{
	[HarmonyPostfix]
	private static void Postfix(CargoShip __instance)
	{
		try
		{
			CargoDocks.OnShipSpawned(__instance);
		}
		catch (Exception e)
		{
			Log.Once("cargo spawn", e);
		}
	}
}

[HarmonyPatch(typeof(CargoShip), nameof(CargoShip.UpdateMovement))]
internal static class CargoSteerPatch
{
	[HarmonyPrefix]
	private static bool Prefix(CargoShip __instance)
	{
		try
		{
			return !CargoStop.Steer(__instance);
		}
		catch (Exception e)
		{
			Log.Once("cargo steer", e);
			return true;
		}
	}
}
