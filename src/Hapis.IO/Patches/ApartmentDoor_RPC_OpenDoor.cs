using System;
using HarmonyLib;
using Moose.Hapis.IO.Apartments;

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(Door), "RPC_OpenDoor")]
internal class ApartmentDoor_RPC_OpenDoor
{
	[HarmonyPrefix]
	private static bool Prefix(Door __instance, BaseEntity.RPCMessage __0)
	{
		if (__instance is not ApartmentDoor door)
		{
			return true;
		}

		try
		{
			return !StandingBreakIn.TryStart(door, __0.player);
		}
		catch (Exception e)
		{
			HapisLog.Once("standing break in", e);
			return true;
		}
	}
}
