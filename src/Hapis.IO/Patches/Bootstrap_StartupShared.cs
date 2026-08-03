using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(Bootstrap), nameof(Bootstrap.StartupShared))]
internal class Bootstrap_StartupShared
{
	[HarmonyPrefix]
	private static bool Prefix()
	{
		HapisIO.Instance = new HapisIO();
		
		return true;
	}
}
