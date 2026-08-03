using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(ContainerIOEntity), nameof(ContainerIOEntity.PlayerOpenLoot), typeof(BasePlayer), typeof(string), typeof(bool))]
internal class ContainerIOEntity_PlayerOpenLoot
{
	[HarmonyPrefix]
	private static bool Prefix(BasePlayer player, string panelToOpen, bool doPositionChecks, ContainerIOEntity __instance)
	{
		try
		{
			return HapisIO.Instance.CanPlayerOpenIoEntityContainer(player, panelToOpen, __instance);
		}
		catch
		{
			return true;
		}
	}
}
