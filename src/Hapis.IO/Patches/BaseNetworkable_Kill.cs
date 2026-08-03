using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(BaseNetworkable), nameof(BaseNetworkable.Kill), typeof(BaseNetworkable.DestroyMode), typeof(bool))]
internal class BaseNetworkable_Kill
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
