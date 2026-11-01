using System;
using HarmonyLib;
using Moose.Hapis.Deployables.Doors;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(Door), nameof(Door.OnFlagsChanged))]
internal class Door_OnFlagsChanged
{
	[HarmonyPostfix]
	private static void Postfix(Door __instance, BaseEntity.Flags __0, BaseEntity.Flags __1)
	{
		if (__instance == null || !__instance.isServer)
		{
			return;
		}

		try
		{
			DoorFix.FlagsChanged(__instance, __0, __1);
		}
		catch (Exception e)
		{
			HapisLog.Once("door fix", e);
		}
	}
}
