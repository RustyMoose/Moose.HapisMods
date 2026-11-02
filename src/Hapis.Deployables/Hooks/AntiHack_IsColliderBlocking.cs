using System;
using HarmonyLib;
using Moose.Hapis.Deployables.Doors;
using UnityEngine;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(AntiHack), "IsColliderBlocking")]
internal class AntiHack_IsColliderBlocking
{
	[HarmonyPostfix]
	private static void Postfix(Collider __0, BasePlayer __1, ref bool __result)
	{
		if (!__result || __0 == null)
		{
			return;
		}

		try
		{
			__result = DoorAntiHack.StillBlocks(__0, __1);
		}
		catch (Exception e)
		{
			__result = true;
			HapisLog.Once("door antihack", e);
		}
	}
}
