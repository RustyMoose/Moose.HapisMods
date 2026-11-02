using HarmonyLib;
using Moose.Hapis.Deployables.MapSettings;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(BaseCombatEntity), "CanCompletePickup", typeof(BasePlayer))]
internal class BaseCombatEntity_CanCompletePickup_Re2Settings
{
	[HarmonyPrefix]
	private static bool Prefix(BasePlayer __0, BaseCombatEntity __instance, ref bool __result)
	{
		try
		{
			if (__instance == null || Re2Settings.AdminBypass(__0))
			{
				return true;
			}

			if (!Re2Settings.IsCustomSign(__instance) && !Re2Settings.IsIndestructible(__instance))
			{
				return true;
			}

			__result = false;
			return false;
		}
		catch
		{
			return true;
		}
	}
}
