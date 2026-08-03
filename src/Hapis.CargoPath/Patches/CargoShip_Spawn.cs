using HarmonyLib;
using Moose.Hapis.CargoPath.Behaviours;

namespace Moose.Hapis.CargoPath.Patches;

[HarmonyPatch(typeof(CargoShip), nameof(CargoShip.Spawn))]
public static class CargoShipSpawn
{
    [HarmonyPostfix]
    private static void Postfix(CargoShip __instance)
    {
        if (__instance == null || HapisCargoPath.Instance.CargoPath.Count == 0)
        {
            return;
        }

        __instance.gameObject.AddComponent<CargoStopController>().SetPath(HapisCargoPath.Instance.CargoPath);
    }
}