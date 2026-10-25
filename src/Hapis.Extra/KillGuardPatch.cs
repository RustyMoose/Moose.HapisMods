using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Moose.Hapis.Extra;

namespace Moose.HapisExtra;

/// <summary>
/// Stops anything except this mod from Kill()-ing our Chinook.
/// Finds every method named "Kill" declared on the base entity classes,
/// whatever its parameters are, so a Rust signature change cannot break it.
/// </summary>
[HarmonyPatch]
public static class KillGuardPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		var types = new[] { typeof(BaseNetworkable), typeof(BaseEntity), typeof(BaseCombatEntity) };
		const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

		var found = new List<MethodBase>();
		for (var i = 0; i < types.Length; i++)
		{
			var methods = types[i].GetMethods(flags);
			for (var j = 0; j < methods.Length; j++)
			{
				if (methods[j].Name == "Kill")
				{
					found.Add(methods[j]);
				}
			}
		}
		return found;
	}

	private static bool Prepare()
	{
		var count = 0;
		foreach (var m in TargetMethods())
		{
			count++;
			HapisExtraMod.Log("Kill guard patched: " + m.DeclaringType.Name + "." + m.Name + " (" + m.GetParameters().Length + " params)");
		}
		if (count == 0)
		{
			HapisExtraMod.Log("Kill guard NOT patched (no Kill method found).");
		}
		return count > 0;
	}

	private static bool Prefix(BaseNetworkable __instance)
	{
		if (HapisExtraMod.Managed.Count == 0)
		{
			return true;
		}
		if (!HapisExtraMod.Managed.Contains(__instance))
		{
			return true;
		}

		// let a genuinely dead entity be removed (only happens if Invulnerable is off)
		var bce = __instance as BaseCombatEntity;
		if (bce != null && bce.IsDead())
		{
			return true;
		}

		return false;   // block outside kills; our own despawn removes it from Managed first
	}
}
