using System;
using HarmonyLib;
using Moose.Hapis.IO.Apartments;

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(ApartmentRoom), nameof(ApartmentRoom.PostServerLoad))]
internal class ApartmentRoom_PostServerLoad
{
	[HarmonyPrefix]
	private static void Prefix(ApartmentRoom __instance)
	{
		try
		{
			ApartmentNumbers.Ensure(__instance);
		}
		catch (Exception e)
		{
			HapisLog.Once("apartment numbers", e);
		}
	}
}
