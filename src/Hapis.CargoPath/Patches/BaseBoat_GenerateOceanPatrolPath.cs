using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Moose.Hapis.CargoPath.Patches;

[HarmonyPatch(typeof(BaseBoat), nameof(BaseBoat.GenerateOceanPatrolPath), typeof(float), typeof(float))]
public class BaseBoatGenerateOceanPatrolPath
{
	[HarmonyPrefix]
	private static bool Prefix(ref List<Vector3> __result)
	{
		if (!HapisCargoPath.Instance.ReadMapData() || HapisCargoPath.Instance.SerializedPathList == null)
		{
			return true;
		}
		
		HapisCargoPath.Instance.CargoPath = HapisCargoPath.Instance.SerializedPathList.vectorData.Select(vectorDatum => new Vector3(vectorDatum.x, vectorDatum.y, vectorDatum.z)).ToList();
		__result = HapisCargoPath.Instance.CargoPath;
		
		return false;
	}

	[HarmonyPostfix]
	private static void Postfix(ref List<Vector3> __result)
	{
		if (HapisCargoPath.Instance.CargoPath.Count != 0)
		{
			Debug.LogWarning("[Hapis.CargoPath] Loaded Custom Path " + __result.Count + " Nodes.");
			__result = HapisCargoPath.Instance.CargoPath;
		}
		else
		{
			HapisCargoPath.Instance.CargoPath = __result;
		}
	}
}
