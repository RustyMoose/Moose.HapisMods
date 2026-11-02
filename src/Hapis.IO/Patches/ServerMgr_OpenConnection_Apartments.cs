using System;
using HarmonyLib;
using Moose.Hapis.IO.Apartments;

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.OpenConnection))]
internal class ServerMgr_OpenConnection_Apartments
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			_ = CustomApartments.Present;
		}
		catch (Exception e)
		{
			HapisLog.Once("apartments", e);
		}
	}
}
