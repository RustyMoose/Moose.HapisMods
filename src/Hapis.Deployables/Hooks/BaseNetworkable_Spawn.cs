using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(BaseNetworkable), nameof(BaseNetworkable.Spawn))]
internal class BaseNetworkableSpawn
{
	[HarmonyPostfix]
	private static void Postfix(BaseNetworkable __instance)
	{
		HapisDeployables.Instance.EntitySpawned(__instance);
	}
}
