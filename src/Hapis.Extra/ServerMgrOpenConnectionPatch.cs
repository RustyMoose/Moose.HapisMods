using HarmonyLib;

namespace Moose.Hapis.Extra;

/// <summary>
/// Signals that the map and save have finished loading, so the pump and water fills can start.
/// </summary>
[HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.OpenConnection))]
public static class ServerMgrOpenConnectionPatch
{
	private static void Postfix()
	{
		HapisExtraScheduler.MarkServerReady();
	}
}
