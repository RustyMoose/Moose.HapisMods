using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ProtoBuf;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch]
internal class World_SpawnPrefabData
{
    private static MethodBase _target;

    private static bool Prepare()
    {
        _target = FindTarget();

        if (_target != null)
        {
            UnityEngine.Debug.Log(
                $"[Hapis.Deployables] SpawnPrefabData hook patched on {_target.DeclaringType?.Name}.{_target.Name}");
            return true;
        }

        UnityEngine.Debug.Log("[Hapis.Deployables] SpawnPrefabData hook NOT found: prefab data spawn handling is disabled");

        // Diagnostic: list every World method that touches PrefabData
        foreach (var m in AccessTools.GetDeclaredMethods(typeof(World))
                     .Where(x => x.GetParameters().Any(p => p.ParameterType == typeof(PrefabData))
                                 || x.ReturnType == typeof(PrefabData)))
        {
            UnityEngine.Debug.Log(
                $"[Hapis.Deployables] World candidate: {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
        }

        // Returning false tells Harmony to skip this patch instead of crashing
        return false;
    }

    private static MethodBase TargetMethod()
    {
        return _target;
    }

    private static MethodBase FindTarget()
    {
        var expected = new[] { typeof(PrefabData) };

        return AccessTools.GetDeclaredMethods(typeof(World))
            .Where(m => m.IsStatic && !m.ContainsGenericParameters)
            .Where(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(expected))
            .OrderByDescending(m => m.Name.IndexOf("SpawnPrefabData", StringComparison.OrdinalIgnoreCase) >= 0)
            .ThenByDescending(m => m.Name.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0)
            .FirstOrDefault();
    }

    // __0 = prefab
    [HarmonyPrefix]
    private static bool Prefix(PrefabData __0)
    {
        try
        {
            return HapisDeployables.Instance.SpawnHook(__0);
        }
        catch
        {
            return true;
        }
    }
}