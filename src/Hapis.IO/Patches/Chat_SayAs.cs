using System;
using System.Linq;
using System.Reflection;
using ConVar;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch]
internal class Chat_SayAs
{
    private static MethodBase _target;

    private static bool Prepare()
    {
        _target = FindTarget();

        UnityEngine.Debug.Log(_target != null
            ? $"[Hapis.IO] Chat hook patched on {_target.DeclaringType?.Name}.{_target.Name}"
            : "[Hapis.IO] Chat hook NOT found: chat handling is disabled");

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
            typeof(Chat.ChatChannel), typeof(ulong), typeof(string), typeof(string), typeof(BasePlayer)
        };

        return AccessTools.GetDeclaredMethods(typeof(Chat))
            .Where(m => m.IsStatic && !m.ContainsGenericParameters)
            .Where(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(expected))
            .OrderByDescending(m => string.Equals(m.Name, "sayAs", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    // __0 = targetChannel, __3 = message, __4 = player
    [HarmonyPrefix]
    private static bool Prefix(Chat.ChatChannel __0, string __3, BasePlayer __4)
    {
        try
        {
            return HapisIO.Instance.OnChatMessageHandler(__4, __3, __0);
        }
        catch
        {
            return true;
        }
    }
}