using System;
using HarmonyLib;
using Moose.Hapis.IO.Apartments;

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(ApartmentBuilding), "GrabAllApartments")]
internal class ApartmentBuilding_GrabAllApartments
{
	[HarmonyPostfix]
	private static void Postfix(ApartmentBuilding __instance)
	{
		try
		{
			ApartmentNumbers.LinkMailboxes(__instance);
		}
		catch (Exception e)
		{
			HapisLog.Once("apartment mailboxes", e);
		}
	}
}
