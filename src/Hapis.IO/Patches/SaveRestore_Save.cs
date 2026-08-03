using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(SaveRestore), nameof(SaveRestore.Save), typeof(string), typeof(bool))]
internal class SaveRestoreSave
{
	[HarmonyPrefix]
	private static void Prefix()
	{
		HapisIO.Instance.OnServerSaving();
	}
}
