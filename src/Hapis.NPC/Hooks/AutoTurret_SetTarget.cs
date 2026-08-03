using HarmonyLib;

namespace Moose.Hapis.NPC.Hooks;

[HarmonyPatch(typeof(AutoTurret), nameof(AutoTurret.SetTarget), typeof(BaseCombatEntity))]
internal class AutoTurretSetTarget
{
	[HarmonyPrefix]
	private static bool Prefix(BaseCombatEntity targ, AutoTurret __instance)
	{
		try
		{
			return HapisNPC.Instance.AutoTurretHook(targ, __instance);
		}
		catch
		{
			return true;
		}
	}
}
