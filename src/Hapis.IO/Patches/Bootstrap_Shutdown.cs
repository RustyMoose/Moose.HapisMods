using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.Shutdown))]
internal class Bootstrap_Shutdown
{
	[HarmonyPrefix]
	private static void Prefix()
	{
		try
		{
			HapisIO.Instance.OnServerShuttingDown();
		}
		catch
		{
			//
		}
	}
}
