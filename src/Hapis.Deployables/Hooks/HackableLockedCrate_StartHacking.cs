using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(HackableLockedCrate), nameof(HackableLockedCrate.StartHacking))]
internal class HackableLockedCrate_StartHacking
{
    [HarmonyPrefix]
    private static void Prefix(HackableLockedCrate __instance)
    {
        try
        {
            if (Config.EnableReinforcementAlarm)
            {
                HapisDeployables.Instance.StartHackingHook(__instance);
            }
        }
        catch
        {
            //
        }
    }
}
