using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(BaseNetworkable), nameof(BaseNetworkable.Kill), typeof(BaseNetworkable.DestroyMode), typeof(bool))]
internal class BaseNetworkable_Kill
{
    [HarmonyPrefix]
    static bool Prefix(BaseNetworkable __instance)
    {
        try
        {
            return HapisDeployables.KillHook(__instance);
        }
        catch
        {
            return true;
        }
    }
}