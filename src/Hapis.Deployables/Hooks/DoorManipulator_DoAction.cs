using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(DoorManipulator), nameof(DoorManipulator.DoAction))]
internal class DoorManipulator_DoAction
{
    [HarmonyPrefix]
    static bool Prefix(DoorManipulator __instance)
    {
        foreach (var CoalMech in HapisDeployables.Instance.CoalingTower)
        {
            if (Vector3.Distance(CoalMech.transform.position, __instance.transform.position) <= 30)
            {
                CoalMech.SetFlag(BaseEntity.Flags.Reserved8, __instance.IsPowered());
                
                return false;
            }
        }
        
        return true;
    }
}