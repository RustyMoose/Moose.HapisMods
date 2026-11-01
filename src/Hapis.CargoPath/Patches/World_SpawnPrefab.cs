using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Moose.Hapis.CargoPath.Patches;

[HarmonyPatch]
internal class WorldSpawn
{
    private static MethodBase _target;

    private static bool Prepare()
    {
        _target = FindTarget();

        Debug.Log(_target != null
            ? $"[Hapis.CargoPath] World spawn hook patched on {_target.DeclaringType?.Name}.{_target.Name}"
            : "[Hapis.CargoPath] World spawn hook NOT found: prefab spawn handling is disabled");

        // Returning false tells Harmony to skip this patch instead of crashing
        return _target != null;
    }

    private static MethodBase TargetMethod()
    {
        return _target;
    }

    private static MethodBase FindTarget()
    {
        var expected = new[]
        {
            typeof(string), typeof(Prefab), typeof(Vector3), typeof(Quaternion), typeof(Vector3)
        };

        return AccessTools.GetDeclaredMethods(typeof(World))
            .Where(m => m.IsStatic && !m.ContainsGenericParameters)
            .Where(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(expected))
            .OrderByDescending(m => m.Name.IndexOf("SpawnPrefab", StringComparison.OrdinalIgnoreCase) >= 0)
            .ThenByDescending(m => m.Name.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0)
            .FirstOrDefault();
    }

    // __0 = category, __1 = prefab, __2 = position, __3 = rotation, __4 = scale
    [HarmonyPrefix]
    private static bool Prefix(string __0, Prefab __1, Vector3 __2, Quaternion __3, Vector3 __4)
    {
        try
        {
            return HapisCargoPath.Instance.OnWorldSpawnPrefabHook(__0, __1, __2, __3, __4);
        }
        catch
        {
            return true;
        }
    }
}