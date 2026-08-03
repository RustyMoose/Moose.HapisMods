using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(BaseCombatEntity), nameof(BaseCombatEntity.Die), typeof(HitInfo))]
internal class BaseCombatEntityDie
{
	[HarmonyPrefix]
	private static bool Prefix(HitInfo info, BaseCombatEntity __instance)
	{
		try
		{
			return HapisIO.Instance.OnEntityDeath(info, __instance);
		}
		catch
		{
			return true;
		}
	}
}
