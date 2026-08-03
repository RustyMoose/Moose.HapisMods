using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(BaseCombatEntity), nameof(BaseCombatEntity.CanCompletePickup), typeof(BasePlayer))]
internal class BaseCombatEntityCanPickup
{
	[HarmonyPrefix]
	private static bool Prefix(BasePlayer player, BaseCombatEntity __instance)
	{
		try
		{
			return HapisIO.Instance.CanPickupHook(__instance);
		}
		catch
		{
			return true;
		}
	}
}
