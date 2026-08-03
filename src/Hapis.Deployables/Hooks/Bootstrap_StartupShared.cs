using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(Bootstrap), nameof(Bootstrap.StartupShared))]
internal class BootstrapStartupShared
{
	[HarmonyPrefix]
	private static bool Prefix()
	{
		HapisDeployables.Instance = new HapisDeployables();
		return true;
	}
}
