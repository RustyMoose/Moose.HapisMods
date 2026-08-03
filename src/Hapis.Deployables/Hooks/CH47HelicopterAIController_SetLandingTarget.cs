using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(CH47HelicopterAIController), "SetLandingTarget")]
internal class CH47HelicopterAIController_SetLandingTarget
{
    [HarmonyPostfix]
    private static void Postfix(CH47HelicopterAIController __instance, Vector3 target)
    {
        try
        {
            if (Config.EnableCh47Reinforcements)
            {
                HapisDeployables.Instance.SetLandingTargetHook(__instance, target);
            }
        }
        catch
        {
        }
    }
}
