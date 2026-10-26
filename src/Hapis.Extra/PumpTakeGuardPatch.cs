using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Moose.Hapis.Extra;

/// <summary>
/// Players can OPEN a crude pump and see the oil, but cannot take it out.
/// Every player move, split, drop and swap asks PlayerInventory.CanMoveItemsFrom / CanSwapItemsFrom first,
/// so refusing there blocks all of them without touching item moves made by the game itself.
/// The hose and this mod change the amount directly, so they are not affected.
/// </summary>
[HarmonyPatch]
public static class PumpTakeGuardPatch
{
	private static bool Prepare()
	{
		return HapisExtraConstants.BlockPumpTaking;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(PlayerInventory), nameof(PlayerInventory.CanMoveItemsFrom));
		yield return AccessTools.Method(typeof(PlayerInventory), nameof(PlayerInventory.CanSwapItemsFrom));
	}

	private static bool Prefix(BaseEntity entity, ref PlayerInventory.CanMoveFromResponse __result)
	{
		if (entity is not WaterCatcher catcher || !LiquidSources.IsPump(catcher))
		{
			return true;
		}

		__result = new PlayerInventory.CanMoveFromResponse(false, null);   // taking oil out of a pump by hand: refused
		return false;
	}
}
