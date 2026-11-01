using System;
using System.Threading;
using HarmonyLib;
using Moose.Hapis.RustEdit2;

namespace Moose.Hapis.Deployables.Hooks;

[HarmonyPatch(typeof(World), nameof(World.Spawn), typeof(float), typeof(Action<string>), typeof(CancellationToken))]
internal class World_Spawn
{
	private static bool _added;

	[HarmonyPrefix]
	private static void Prefix()
	{
		if (_added)
		{
			return;
		}

		_added = true;

		try
		{
			if (!Re2Map.Usable)
			{
				return;
			}

			var prefabs = Re2Data.Prefabs();
			World.Serialization.world.prefabs.AddRange(prefabs);
			HapisLog.Info("RustEdit 2: Added " + prefabs.Count + " Map Entities To The World Spawn");
		}
		catch (Exception e)
		{
			HapisLog.Once("RustEdit 2 world spawn", e);
		}
	}
}
