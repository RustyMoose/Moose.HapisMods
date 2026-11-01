using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch]
internal class WireToolCanPlayerUseWires
{
    private static MethodBase _target;

    private static bool Prepare()
    {
        _target = FindTarget();

        UnityEngine.Debug.Log(_target != null
            ? $"[Hapis.IO] Wire hook patched on {_target.DeclaringType?.Name}.{_target.Name}"
            : "[Hapis.IO] Wire hook NOT found: wire protection is disabled");

        // Returning false tells Harmony to skip this patch instead of crashing
        return _target != null;
    }

    private static MethodBase TargetMethod()
    {
        return _target;
    }

    private static MethodBase FindTarget()
    {
        var expected = new[] { typeof(BasePlayer), typeof(IOEntity) };

        return AccessTools.GetDeclaredMethods(typeof(WireTool))
            .Where(m => m.IsStatic
                        && m.ReturnType == typeof(bool)
                        && !m.ContainsGenericParameters)
            .Where(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(expected))
            .OrderByDescending(m => m.Name.IndexOf("CanModify", StringComparison.OrdinalIgnoreCase) >= 0)
            .ThenByDescending(m => m.Name.StartsWith("Can", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    // __0 = player, __1 = ent
    [HarmonyPrefix]
    private static bool Prefix(BasePlayer __0, IOEntity __1)
    {
        try
        {
            return HapisIO.Instance.CanModifyEntityHook(__0, __1);
        }
        catch
        {
            return true;
        }
    }
}