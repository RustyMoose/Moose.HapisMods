using System;
using HarmonyLib;
using Moose.Hapis.Deployables.Doors;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(AnimatedBuildingBlock), "UpdateAnimationParameters", typeof(bool))]
internal class AnimatedBuildingBlock_UpdateAnimationParameters
{
	[HarmonyPrefix]
	private static void Prefix(AnimatedBuildingBlock __instance)
	{
		try
		{
			DoorFix.BeforeAnimate(__instance);
		}
		catch (Exception e)
		{
			HapisLog.Once("door fix", e);
		}
	}
}
