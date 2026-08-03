using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.OpenConnection))]
internal class ServerMgrOpenConnection
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			HapisDeployables.Instance.InitializeHook();
		}
		catch
		{
			//
		}
	}
}
