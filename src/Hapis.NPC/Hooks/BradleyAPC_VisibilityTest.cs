using HarmonyLib;

namespace Moose.Hapis.NPC.Hooks;

[HarmonyPatch(typeof(BradleyAPC), nameof(BradleyAPC.VisibilityTest), typeof(BaseEntity))]
internal class BradleyApcVisibilityTest
{
	[HarmonyPrefix]
	private static bool Prefix(BaseEntity ent, BradleyAPC __instance)
	{
		try
		{
			return HapisNPC.Instance.APCHook(ent, __instance);
		}
		catch
		{
			return true;
		}
	}
}
