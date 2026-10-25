using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Moose.Hapis.Extra;

/// <summary>
/// Players can OPEN a crude pump and see the oil, but cannot take it out.
/// Blocks Item.MoveToContainer / SplitItem / Drop / DropAndTossUpwards when the item sits
/// inside a pump (so right-click, shift-click, dragging, splitting and dropping all fail).
/// The hose and this mod change the amount directly, so they are not affected.
/// </summary>
[HarmonyPatch]
public static class PumpTakeGuardPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

		var found = new List<MethodBase>();
		var methods = typeof(Item).GetMethods(flags);
		for (var i = 0; i < methods.Length; i++)
		{
			var n = methods[i].Name;
			if (n == "MoveToContainer" || n == "SplitItem" || n == "Drop" || n == "DropAndTossUpwards")
			{
				found.Add(methods[i]);
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
			HapisExtraMod.Log("Pump take guard patched: " + m.DeclaringType.Name + "." + m.Name + " (" + m.GetParameters().Length + " params)");
		}
		if (count == 0)
		{
			HapisExtraMod.Log("Pump take guard NOT patched (no Item methods found).");
		}
		return count > 0;
	}

	private static bool Prefix(Item __instance, object[] __args)
	{
		try
		{
			if (__instance == null)
			{
				return true;
			}

			var parent = __instance.parent;
			if (parent == null)
			{
				return true;   // new items (e.g. created by this mod) are not inside a pump yet
			}

			var owner = PumpFiller.GetField(parent, "entityOwner") as BaseEntity;
			if (owner == null || !PumpFiller.IsPump(owner))
			{
				return true;
			}

			// moving inside the same pump container is harmless
			if (__args != null)
			{
				for (var i = 0; i < __args.Length; i++)
				{
					if (__args[i] is ItemContainer container && container == parent)
					{
						return true;
					}
				}
			}

			return false;   // taking oil out of a pump by hand: refused
		}
		catch (Exception)
		{
		}

		return true;
	}
}
