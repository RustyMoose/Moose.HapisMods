using HarmonyLib;

namespace Moose.Hapis.NPC.Hooks;

[HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.OpenConnection))]
internal class ServerMgrOpenConnection
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			HapisNPC.Instance.InitializeHook();
		}
		catch
		{
			// ignored
		}
	}
}
