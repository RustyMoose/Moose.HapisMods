using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.NPC.Hooks;

[HarmonyPatch(typeof(BaseNavigator), nameof(BaseNavigator.UpdatePositionAndRotation))]
internal static class BaseNavigatorUpdatePositionAndRotation
{
    private static bool Prefix(BaseNavigator __instance, Vector3 __0)
    {
        try
        {
            return HapisNPC.Instance == null || HapisNPC.Instance.DoorBlockHook(__instance, __0);
        }
        catch
        {
            return true;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(BaseNavigator __instance)
    {
        try
        {
            HapisNPC.Instance?.SnapToFloorHook(__instance);
        }
        catch
        {
        }
    }
}