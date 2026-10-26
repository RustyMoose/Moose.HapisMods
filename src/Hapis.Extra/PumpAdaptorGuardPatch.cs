using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Moose.Hapis.Extra;

/// <summary>
/// Removes industrial storage adaptors attached to a crude pump, so conveyors cannot pull the oil out.
/// Placement spawns the adaptor before parenting it (and OnParentChanging runs before the parent is set),
/// and a save may load the adaptor before its parent, so the parent is checked one frame later.
/// </summary>
[HarmonyPatch]
public static class PumpAdaptorGuardPatch
{
	private static bool Prepare()
	{
		return HapisExtraConstants.BlockPumpTaking;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(IndustrialStorageAdaptor), nameof(IndustrialStorageAdaptor.ServerInit));
		yield return AccessTools.Method(typeof(IndustrialStorageAdaptor), nameof(IndustrialStorageAdaptor.OnParentChanging));
	}

	private static void Postfix(IndustrialStorageAdaptor __instance)
	{
		__instance.Invoke(() => CheckParent(__instance), 0f);
	}

	private static void CheckParent(IndustrialStorageAdaptor adaptor)
	{
		if (adaptor == null || adaptor.IsDestroyed)
		{
			return;
		}
		if (adaptor.GetParentEntity() is not WaterCatcher catcher || !LiquidSources.IsPump(catcher))
		{
			return;
		}

		HapisExtraMod.Log("Removed a storage adaptor attached to the crude pump at " + catcher.transform.position + " (owner " + adaptor.OwnerID + ").");
		adaptor.Kill();
	}
}
