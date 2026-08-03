using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(CH47Helicopter), nameof(CH47Helicopter.CreateMapMarker))]
internal class Ch47HelicopterCreateMapMarker
{
	[HarmonyPrefix]
	private static bool Prefix(CH47Helicopter __instance)
	{
		try
		{
			return Config.EnableCh47MapMarker || HapisDeployables.Instance.CreateMapMarkerHook(__instance);
		}
		catch
		{
			return true;
		} 
	}
}
