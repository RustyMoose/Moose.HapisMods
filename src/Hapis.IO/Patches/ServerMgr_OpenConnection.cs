using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.OpenConnection))]
internal class ServerMgrOpenConnection
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			HapisIO.Instance.InitializeHook();
		}
		catch
		{
			Debug.LogError("[Hapis.IO] Open Connection Hook Error");
		}
	}
}
