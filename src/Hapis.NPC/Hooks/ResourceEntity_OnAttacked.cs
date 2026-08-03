using HarmonyLib;

namespace Moose.Hapis.NPC.Hooks;

[HarmonyPatch(typeof(BasePlayer), nameof(BasePlayer.OnAttacked), typeof(HitInfo))]
internal class ResourceEntityOnAttacked
{
	[HarmonyPrefix]
	private static bool Prefix(HitInfo info, BasePlayer __instance)
	{
		try
		{
			return HapisNPC.Instance.AttackedHook(info, __instance);
		}
		catch
		{
			// ignored
		}

		return true;
	}
}
