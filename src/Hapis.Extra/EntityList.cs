using System;
using System.Collections.Generic;

namespace Moose.Hapis.Extra;

/// <summary>
/// Every server entity, with a fallback if Rust's list is empty.
/// </summary>
public static class EntityList
{
	private static bool _loggedFallback;

	public static List<BaseNetworkable> Get()
	{
		var list = new List<BaseNetworkable>();

		try
		{
			if (BaseNetworkable.serverEntities != null)
			{
				foreach (var n in BaseNetworkable.serverEntities)
				{
					if (n != null)
					{
						list.Add(n);
					}
				}
			}
		}
		catch (Exception e)
		{
			HapisExtraMod.Log("EntityList: serverEntities failed: " + e.Message);
		}

		if (list.Count == 0)
		{
			try
			{
				var all = UnityEngine.Object.FindObjectsOfType<BaseNetworkable>();
				for (var i = 0; i < all.Length; i++)
				{
					if (all[i] != null)
					{
						list.Add(all[i]);
					}
				}
				if (list.Count > 0 && !_loggedFallback)
				{
					_loggedFallback = true;
					HapisExtraMod.Log("EntityList: serverEntities was empty, using the Unity object list instead (" + list.Count + " found).");
				}
			}
			catch (Exception e)
			{
				HapisExtraMod.Log("EntityList: Unity fallback failed: " + e.Message);
			}
		}

		return list;
	}
}
