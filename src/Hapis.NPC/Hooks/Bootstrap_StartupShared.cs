using HarmonyLib;

namespace Moose.Hapis.NPC.Hooks;

[HarmonyPatch(typeof(Bootstrap), "StartupShared")]
internal class BootstrapStartupShared
{
	[HarmonyPrefix]
	private static bool Prefix()
	{
		HapisNPC.Instance = new HapisNPC();
		return true;
	}
}
