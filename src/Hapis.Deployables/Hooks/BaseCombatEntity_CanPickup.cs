using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(BaseCombatEntity), nameof(BaseCombatEntity.CanCompletePickup), typeof(BasePlayer))]
internal class BaseCombatEntity_CanPickup
{
	[HarmonyPrefix]
	private static bool Prefix(BasePlayer player, BaseCombatEntity __instance)
	{
		try
		{
			if (player.IsAdmin && player.IsGod() && player.IsFlying)
			{
				return true;
			}
			
			return HapisDeployables.Instance.ProtectedHook(__instance);
		}
		catch
		{
			//
		}
		
		return true;
	}
}
