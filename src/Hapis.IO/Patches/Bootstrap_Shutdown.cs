using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch]
internal class Bootstrap_Shutdown
{
    private static bool _registered;
    private static bool _done;
    private static MethodBase _target;

    private static bool Prepare()
    {
        if (!_registered)
        {
            _registered = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => RunShutdown();
        }

        _target = FindTarget();

        UnityEngine.Debug.Log(_target != null
            ? $"[Hapis.IO] Shutdown hook patched on {_target.DeclaringType?.Name}.{_target.Name}"
            : "[Hapis.IO] No shutdown method found, using process-exit fallback");

        // Returning false tells Harmony to skip this patch instead of crashing
        return _target != null;
    }

    private static MethodBase TargetMethod()
    {
        return _target;
    }

    private static MethodBase FindTarget()
    {
        var types = new[] { typeof(ServerMgr), typeof(Bootstrap) };
        var names = new[] { "Shutdown", "OnApplicationQuit" };

        foreach (var type in types)
        {
            foreach (var name in names)
            {
                var method = AccessTools.GetDeclaredMethods(type)
                    .FirstOrDefault(m => m.Name == name
                                         && !m.IsAbstract
                                         && !m.ContainsGenericParameters
                                         && m.GetParameters().Length == 0);
                if (method != null)
                    return method;
            }
        }

        return null;
    }

    [HarmonyPrefix]
    private static void Prefix()
    {
        RunShutdown();
    }

    private static void RunShutdown()
    {
        if (_done) return;
        _done = true;

        try
        {
            HapisIO.Instance.OnServerShuttingDown();
        }
        catch
        {
            //
        }
    }
}