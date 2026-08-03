using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(StorageContainer), nameof(StorageContainer.PlayerOpenLoot), typeof(BasePlayer), typeof(string), typeof(bool))]
internal class StorageContainerPlayerOpenLoot
{
	[HarmonyPrefix]
	private static bool Prefix(BasePlayer player, string panelToOpen, bool doPositionChecks, StorageContainer __instance)
	{
		try
		{
			return HapisIO.Instance.CanPlayerOpenStorageContainer(player, panelToOpen, __instance);
		}
		catch
		{
			return true;
		}
	}
}
