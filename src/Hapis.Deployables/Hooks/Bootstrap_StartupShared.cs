using System.Linq;
using System.Reflection;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch]
internal class BootstrapStartupShared
{
    private static MethodBase _target;

    private static bool Prepare()
    {
        _target = FindTarget();

        if (_target != null)
        {
            UnityEngine.Debug.Log(
                $"[Hapis.Deployables] Startup hook patched on {_target.DeclaringType?.Name}.{_target.Name}");
            return true;
        }

        // Nothing to hook: create the instance right now so the mod still works
        UnityEngine.Debug.Log("[Hapis.Deployables] No startup method found, creating instance at load");
        HapisDeployables.Instance = new HapisDeployables();
        return false;
    }

    private static MethodBase TargetMethod()
    {
        return _target;
    }

    private static MethodBase FindTarget()
    {
        // Most likely names first
        var names = new[] { "StartupShared", "StartServer", "StartNexusServer" };

        foreach (var name in names)
        {
            var method = AccessTools.GetDeclaredMethods(typeof(Bootstrap))
                .FirstOrDefault(m => m.Name == name
                                     && !m.IsAbstract
                                     && !m.ContainsGenericParameters);
            if (method != null)
                return method;
        }

        return null;
    }

    [HarmonyPrefix]
    private static bool Prefix()
    {
        HapisDeployables.Instance = new HapisDeployables();
        return true;
    }
}