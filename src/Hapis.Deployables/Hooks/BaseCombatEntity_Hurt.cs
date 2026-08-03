using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(BaseCombatEntity), nameof(BaseCombatEntity.Hurt), typeof(HitInfo))]
internal class BaseCombatEntityHurt
{
	[HarmonyPrefix]
	private static bool Prefix(HitInfo info, BaseCombatEntity __instance)
	{
		try
		{
			return HapisDeployables.Instance.ProtectedHook(__instance, info);
		}
		catch
		{
			return true;
		}
	}
}