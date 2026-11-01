using HarmonyLib;
using Moose.Hapis.Deployables.MapSettings;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(BaseCombatEntity), nameof(BaseCombatEntity.Hurt), typeof(HitInfo))]
internal class BaseCombatEntity_Hurt_Re2Settings
{
	[HarmonyPrefix]
	private static bool Prefix(BaseCombatEntity __instance)
	{
		try
		{
			return __instance == null || !Re2Settings.IsIndestructible(__instance);
		}
		catch
		{
			return true;
		}
	}
}
