using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Moose.Hapis.Deployables.MapSettings;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch]
internal class Signage_CanUpdateSign
{
	private static List<MethodBase>? _targets;

	private static bool Prepare()
	{
		_targets ??= new[] { typeof(Signage), typeof(PhotoFrame), typeof(CarvablePumpkin) }
			.Select(t => (MethodBase?)AccessTools.DeclaredMethod(t, "CanUpdateSign", new[] { typeof(BasePlayer) }))
			.Where(m => m != null)
			.Select(m => m!)
			.ToList();

		if (_targets.Count == 0)
		{
			HapisLog.Info("RustEdit 2 Settings: Sign Edit Hook Missing - Custom Signs Can Still Be Drawn On");
		}

		return _targets.Count > 0;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return _targets!;
	}

	[HarmonyPrefix]
	private static bool Prefix(BaseEntity __instance, BasePlayer __0, ref bool __result)
	{
		try
		{
			if (Re2Settings.AdminBypass(__0) || !Re2Settings.IsCustomSign(__instance))
			{
				return true;
			}

			__result = false;
			return false;
		}
		catch (Exception e)
		{
			HapisLog.Once("RustEdit 2 sign edit", e);
			return true;
		}
	}
}
