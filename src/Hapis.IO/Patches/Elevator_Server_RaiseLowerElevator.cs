using HarmonyLib;
using Moose.Hapis.IO.Apartments;

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(Elevator), nameof(Elevator.Server_RaiseLowerElevator))]
internal class Elevator_Server_RaiseLowerElevator
{
	[HarmonyPrefix]
	private static void Prefix(Elevator __instance, ref int __0, ref bool __1)
	{
		if (__instance is not ElevatorStatic || !CustomApartments.Present)
		{
			return;
		}

		if (__0 == int.MinValue)
		{
			__0 = -1;
			__1 = true;
		}
		else if (__0 == int.MaxValue)
		{
			__0 = 1;
			__1 = true;
		}
	}
}
