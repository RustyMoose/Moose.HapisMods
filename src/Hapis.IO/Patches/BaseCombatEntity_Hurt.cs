using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(BaseCombatEntity), nameof(BaseCombatEntity.Hurt), typeof(HitInfo))]
internal class BaseCombatEntityHurt
{
	[HarmonyPrefix]
	private static bool Prefix(BaseCombatEntity __instance)
	{
		try
		{
			return HapisIO.Instance.OnEntityHurt(__instance);
		}
		catch
		{
			return true;
		}
	}
}
