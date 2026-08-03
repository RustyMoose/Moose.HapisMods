using HarmonyLib;

namespace Moose.Hapis.CargoPath.Patches;

[HarmonyPatch(typeof(Bootstrap), nameof(Bootstrap.StartupShared))]
internal class BootstrapStartupShared
{
	[HarmonyPrefix]
	private static bool Prefix()
	{
		HapisCargoPath.Instance = new HapisCargoPath();
		return true;
	}
}
