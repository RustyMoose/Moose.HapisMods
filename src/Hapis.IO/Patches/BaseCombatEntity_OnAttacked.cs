using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(BaseCombatEntity), nameof(BaseCombatEntity.OnAttacked), typeof(HitInfo))]
internal class BaseCombatEntity_OnAttacked
{
	[HarmonyPrefix]
	private static bool Prefix(BaseCombatEntity __instance)
	{
		try
		{
			return HapisIO.Instance.OnEntityAttackedHandler(__instance);
		}
		catch
		{
			return true;
		}
	}
}
