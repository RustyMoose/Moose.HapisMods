using HarmonyLib;

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(WireTool), nameof(WireTool.CanModifyEntity), typeof(BasePlayer), typeof(IOEntity))]
internal class WireToolCanPlayerUseWires
{
	[HarmonyPrefix]
	private static bool Prefix(BasePlayer player, IOEntity ent)
	{
		try
		{
			return HapisIO.Instance.CanModifyEntityHook(player, ent);
		}
		catch
		{
			return true;
		}
	}
}
