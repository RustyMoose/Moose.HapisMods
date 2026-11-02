using System;
using HarmonyLib;
using Moose.Hapis.Deployables.MapSettings;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.OpenConnection))]
internal class ServerMgr_OpenConnection_Re2Settings
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			Re2Settings.ApplyRadiation();
		}
		catch (Exception e)
		{
			HapisLog.Once("RustEdit 2 radiation", e);
		}
	}
}
