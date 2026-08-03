using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(SaveRestore), nameof(SaveRestore.Load))]
internal class SaveRestore_Load
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			HapisIO.Instance.OnSaveLoaded();
		}
		catch
		{
			Debug.LogWarning("[Hapis.IO] Load Hook Error");
		}
	}
}
