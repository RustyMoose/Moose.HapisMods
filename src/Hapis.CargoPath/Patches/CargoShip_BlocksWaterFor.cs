using HarmonyLib;

namespace Moose.Hapis.CargoPath.Patches;

[HarmonyPatch(typeof(CargoShip), nameof(CargoShip.BlocksWaterFor))]
internal class CargoShipBlocksWaterFor
{
	[HarmonyPrefix]
	private static bool Prefix(ref bool __result, CargoShip __instance)
	{
		try
		{
			__result = false;
			return false;
		}
		catch
		{
			//
		}
		
		return true;
	}
}
