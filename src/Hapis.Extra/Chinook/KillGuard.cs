using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Moose.Hapis.Extra;

[HarmonyPatch]
internal static class KillGuardPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return new[] { typeof(BaseNetworkable), typeof(BaseEntity), typeof(BaseCombatEntity) }
			.SelectMany(t => AccessTools.GetDeclaredMethods(t))
			.Where(m => m.Name == "Kill" && !m.IsAbstract);
	}

	[HarmonyPrefix]
	private static bool Prefix(BaseNetworkable __instance)
	{
		if (Chinook.Managed.Count == 0 || !Chinook.Managed.Contains(__instance))
		{
			return true;
		}

		return __instance is BaseCombatEntity combat && combat.IsDead();
	}
}
