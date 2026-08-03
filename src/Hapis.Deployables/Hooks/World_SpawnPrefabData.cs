using HarmonyLib;
using ProtoBuf;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(World), nameof(World.SpawnPrefabData), typeof(PrefabData))]
internal class World_SpawnPrefabData
{
    [HarmonyPrefix]
    private static bool Prefix(PrefabData prefab)
    {
        try
        {
            return HapisDeployables.Instance.SpawnHook(prefab);
        }
        catch
        {
            return true;
        }
    }
}
