using System;
using HarmonyLib;
using Moose.Hapis.IO.Apartments;
using UnityEngine;

namespace Moose.Hapis.IO.Patches;

[HarmonyPatch(typeof(NPCApartmentSecurity), nameof(NPCApartmentSecurity.OnPaidToll))]
internal class NPCApartmentSecurity_OnPaidToll
{
	[HarmonyPostfix]
	private static void Postfix(BasePlayer __0, Vector3 __1)
	{
		try
		{
			GuardAccess.AfterPaid(__0, __1);
		}
		catch (Exception e)
		{
			HapisLog.Once("guard access", e);
		}
	}
}
