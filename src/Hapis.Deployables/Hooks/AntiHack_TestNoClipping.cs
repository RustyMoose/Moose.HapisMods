using HarmonyLib;
using Moose.Hapis.Deployables.Doors;
using UnityEngine;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(AntiHack), nameof(AntiHack.TestNoClipping))]
internal class AntiHack_TestNoClipping
{
	[HarmonyPrefix]
	private static void Prefix(Vector3 __1, Vector3 __2)
	{
		DoorAntiHack.BeginSingle(__1, __2);
	}

	[HarmonyFinalizer]
	private static void Finalizer()
	{
		DoorAntiHack.EndSingle();
	}
}
