using System;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.NPC.Hooks;

[HarmonyPatch(typeof(HumanNPC), nameof(HumanNPC.CanSeeTarget), typeof(BaseEntity), typeof(Vector3))]
internal class HumanNPC_CanSeeTarget
{
	private const int Deployed = 8;
	private const int Transparent = 24;
	private const int Terrain = 23;
	private const int ExtraBlockers = (1 << Deployed) | (1 << Transparent);
	private const int Ground = 1 << Terrain;
	private const float Underground = 1f;

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

			if (Blocked(eyes, head, chest, ExtraBlockers) || ((BelowGround(eyes) || BelowGround(head)) && Blocked(eyes, head, chest, Ground)))
			{
				__result = false;
			}
		}
		catch (Exception e)
		{
			HapisLog.Once("NPC sight", e);
		}
	}

	private static bool BelowGround(Vector3 spot)
	{
		return TerrainMeta.HeightMap != null && spot.y < TerrainMeta.HeightMap.GetHeight(spot) - Underground;
	}

	private static bool Blocked(Vector3 eyes, Vector3 head, Vector3 chest, int layers)
	{
		return Physics.Linecast(eyes, head, layers, QueryTriggerInteraction.Ignore)
			&& Physics.Linecast(eyes, chest, layers, QueryTriggerInteraction.Ignore);
	}
}
