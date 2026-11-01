using System;
using HarmonyLib;
using Moose.Hapis.Deployables.Doors;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.OpenConnection))]
internal class ServerMgr_OpenConnection_DoorFix
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			DoorFix.WakeAll();
		}
		catch (Exception e)
		{
			HapisLog.Once("door fix", e);
		}
	}
}
