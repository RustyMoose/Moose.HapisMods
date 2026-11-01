using System;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.NPC.Hooks;

[HarmonyPatch(typeof(HumanNPC), nameof(HumanNPC.CanSeeTarget), typeof(BaseEntity), typeof(Vector3))]
internal class HumanNPC_CanSeeTarget
{
	private const int Deployed = 8;
	private const int Transparent = 24;
	private const int ExtraBlockers = (1 << Deployed) | (1 << Transparent);

	[HarmonyPostfix]
	private static void Postfix(HumanNPC __instance, BaseEntity __0, Vector3 __1, ref bool __result)
	{
		if (!__result || __0 is not BasePlayer target || HapisNPC.Instance == null || !HapisNPC.Instance.OwnsNpc(__instance))
		{
			return;
		}

		try
		{
			var eyes = __instance.eyes != null ? __instance.eyes.position + __1 : __instance.transform.position + Vector3.up * 1.6f + __1;
			var head = target.eyes != null ? target.eyes.position : target.transform.position + Vector3.up * 1.5f;
			var chest = target.transform.position + Vector3.up * 1f;

			if (Physics.Linecast(eyes, head, ExtraBlockers, QueryTriggerInteraction.Ignore)
				&& Physics.Linecast(eyes, chest, ExtraBlockers, QueryTriggerInteraction.Ignore))
			{
				__result = false;
			}
		}
		catch (Exception e)
		{
			HapisLog.Once("NPC sight", e);
		}
	}
}
