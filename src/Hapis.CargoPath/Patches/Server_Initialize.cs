using CompanionServer;
using HarmonyLib;

namespace Moose.Hapis.CargoPath.Patches;

[HarmonyPatch(typeof(Server), nameof(Server.Initialize))]
internal class ServerInitialize
{
	[HarmonyPrefix]
	private static bool Prefix()
	{
		try
		{
			return HapisCargoPath.Instance.InitializeHook();
		}
		catch
		{
			return true;
		}
	}
}
