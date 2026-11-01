using System;
using HarmonyLib;
using Moose.Hapis.Deployables.MapSettings;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(BaseNetworkable), nameof(BaseNetworkable.Spawn))]
internal class BaseNetworkable_Spawn_Re2Settings
{
	[HarmonyPostfix]
	private static void Postfix(BaseNetworkable __instance)
	{
		try
		{
			Re2Settings.OnEntitySpawned(__instance);
		}
		catch (Exception e)
		{
			HapisLog.Once("RustEdit 2 settings", e);
		}
	}
}
