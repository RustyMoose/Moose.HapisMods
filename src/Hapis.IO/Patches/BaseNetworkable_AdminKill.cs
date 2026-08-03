using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(BaseNetworkable), nameof(BaseNetworkable.AdminKill))]
internal class BaseNetworkable_AdminKill
{
	[HarmonyPrefix]
	private static bool Prefix(BaseNetworkable __instance)
	{
		try
		{
			return HapisIO.Instance.OnAdminKill(__instance);
		}
		catch
		{
			return true;
		}
	}
}
