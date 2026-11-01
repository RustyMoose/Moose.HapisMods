using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Moose.Hapis.IO.Apartments;

internal static class CustomApartments
{
	private static bool? _present;

	public static bool Present
	{
		get
		{
			_present ??= Check();
			return _present.Value;
		}
	}

	private static bool Check()
	{
		var monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>()
			.Where(m => m != null && m.name.ToLowerInvariant().Contains("apartment"))
			.ToList();

		var custom = 0;
		var vanilla = 0;
		foreach (var room in BaseNetworkable.serverEntities.OfType<ApartmentRoom>())
		{
			if (room == null)
			{
				continue;
			}

			if (Inside(monuments, room.transform.position))
			{
				vanilla++;
			}
			else
			{
				custom++;
			}
		}

		HapisLog.Info("Apartments: " + custom + " Custom, " + vanilla + " In Rust Monuments" + (custom == 0 ? " - Apartment Extras Off" : " - Apartment Extras On"));
		return custom > 0;
	}

	private static bool Inside(List<MonumentInfo> monuments, Vector3 position)
	{
		foreach (var monument in monuments)
		{
			if (monument.IsInBounds(position))
			{
				return true;
			}
		}

		return false;
	}
}
