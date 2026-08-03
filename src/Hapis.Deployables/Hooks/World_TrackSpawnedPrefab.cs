using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(World), nameof(World.TrackSpawnedPrefab), typeof(string), typeof(GameObject))]
internal class World_TrackSpawnedPrefab
{
    [HarmonyPrefix]
    private static void Prefix(string category, GameObject instance)
    {
        try
        {
            HapisDeployables.Instance.WorldSpawnHook(instance);
        }
        catch
        {
            //
        }
    }
}
