using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Moose.Hapis.Extra;

[HarmonyPatch]
internal static class PumpTakePatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return new[] { "CanMoveItemsFrom", "CanSwapItemsFrom" }
			.Select(name => AccessTools.Method(typeof(PlayerInventory), name))
			.Where(m => m != null);
	}

	[HarmonyPrefix]
	private static bool Prefix(BaseEntity entity, ref PlayerInventory.CanMoveFromResponse __result)
	{
		if (!Settings.Current.BlockPumpTaking || !LiquidSources.IsPump(entity))
		{
			return true;
		}

		__result = default;
		return false;
	}
}

[HarmonyPatch]
internal static class PumpAdaptorPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return new[] { "ServerInit", "OnParentChanging" }
			.Select(name => AccessTools.Method(typeof(IndustrialStorageAdaptor), name))
			.Where(m => m != null);
	}

	[HarmonyPostfix]
	private static void Postfix(IndustrialStorageAdaptor __instance)
	{
		if (Settings.Current.BlockPumpTaking)
		{
			__instance.Invoke(() => Check(__instance), 0f);
		}
	}

	private static void Check(IndustrialStorageAdaptor adaptor)
	{
		try
		{
			if (adaptor == null || adaptor.IsDestroyed || !LiquidSources.IsPump(adaptor.GetParentEntity()))
			{
				return;
			}

			Log.Info("Removed a storage adaptor on a crude pump (owner " + adaptor.OwnerID + ")");
			adaptor.Kill();
		}
		catch (Exception e)
		{
			Log.Once("pump adaptor", e);
		}
	}
}
