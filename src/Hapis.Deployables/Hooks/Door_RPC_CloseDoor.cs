using System;
using HarmonyLib;
using Moose.Hapis.Deployables.Doors;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(Door), "RPC_CloseDoor")]
internal class Door_RPC_CloseDoor
{
	[HarmonyPrefix]
	private static bool Prefix(Door __instance, out bool __state)
	{
		__state = __instance != null && __instance.IsOpen();
		try
		{
			return DoorFix.MayUse(__instance!);
		}
		catch (Exception e)
		{
			HapisLog.Once("door fix", e);
			return true;
		}
	}

	[HarmonyPostfix]
	private static void Postfix(Door __instance, bool __state)
	{
		try
		{
			DoorFix.Moved(__instance, __state);
		}
		catch (Exception e)
		{
			HapisLog.Once("door fix", e);
		}
	}
}
