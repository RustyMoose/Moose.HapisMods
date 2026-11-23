using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Moose.Hapis.NPC.Behaviours;
using Moose.Hapis.RustEdit2;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Moose.Hapis.NPC.Events;

[HarmonyPatch(typeof(ServerMgr), nameof(ServerMgr.OpenConnection))]
internal static class CH47EventStartPatch
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		try
		{
			CH47Events.Start();
			OilRigNavmesh.Report();
		}
		catch (Exception e)
		{
			HapisLog.Once("CH47 events", e);
		}
	}
}

internal sealed class CH47Zone
{
	public Vector3 Spot;
	public int Scientists;
	public float Distance;
	public readonly List<(uint Prefab, Vector3 Spot)> Crates = new();
	public readonly List<(uint Prefab, Vector3 Spot)> Alarms = new();
	public readonly List<BaseEntity> AlarmEntities = new();
	public readonly List<IOEntity> Sirens = new();
	public bool SirensSearched;
	public float NextSearch;
	public bool SirensOn;
	public float SirenUntil;
	public CH47HelicopterAIController? Heli;
	public HackableLockedCrate? Crate;
	public bool Watching;
}

internal static class CH47Events
{
	private const string HeliPrefab = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";
	private const string SirenPrefab = "assets/prefabs/io/electric/other/alarmsound.prefab";
	private const float CrateReach = 3f;
	private const float AlarmReach = 2f;
	private const float SirenReach = 80f;
	private const int SirenPower = 100;
	private const float SirenSeconds = 60f;
	private const float RustEventReach = 150f;
	private const float CheckSeconds = 2f;
	private const float SearchSeconds = 30f;
	private const int DefaultSeats = 10;

	private static readonly List<CH47Zone> Zones = new();
	private static uint? _sirenId;
	private static bool _started;
	private static bool _rustEventSkipped;

	public static void Start()
	{
		if (_started)
		{
			return;
		}

		_started = true;
		if (!Re2Map.Usable)
		{
			return;
		}

		var crates = 0;
		var alarms = 0;
		foreach (var entity in Re2Map.Entities)
		{
			if (entity.Type != "CH47LandingZone")
			{
				continue;
			}

			var zone = new CH47Zone
			{
				Spot = entity.Position,
				Scientists = Mathf.Clamp((int?)entity.Raw["Scientists"] ?? 4, 1, 11),
				Distance = Mathf.Max(100f, (float?)entity.Raw["FlyInDistance"] ?? 500f)
			};

			Link(entity.Raw["Crates"], zone.Crates);
			Link(entity.Raw["Alarms"], zone.Alarms);

			var marker = new GameObject("Hapis.NPC.CH47LandingZone");
			marker.transform.position = zone.Spot;
			marker.AddComponent<CH47LandingZone>();

			Zones.Add(zone);
			crates += zone.Crates.Count;
			alarms += zone.Alarms.Count;
		}

		if (Zones.Count > 0)
		{
			HapisLog.Info("CH47 Events: " + Zones.Count + " landing zones, " + crates + " linked crates, " + alarms + " linked alarms");
		}
	}

	private static void Link(JToken? ids, List<(uint Prefab, Vector3 Spot)> into)
	{
		if (ids is not JArray list)
		{
			return;
		}

		foreach (var id in list)
		{
			var target = Re2Map.Find((uint?)id ?? 0);
			if (target != null)
			{
				into.Add((target.PrefabId, target.Position));
			}
		}
	}

	public static bool IsOurs(CH47HelicopterAIController heli)
	{
		foreach (var zone in Zones)
		{
			if (zone.Heli == heli)
			{
				return true;
			}
		}

		return false;
	}

	public static bool TakesOverRustEvent(Vector3 spot)
	{
		foreach (var zone in Zones)
		{
			if ((zone.Spot - spot).sqrMagnitude <= RustEventReach * RustEventReach)
			{
				if (!_rustEventSkipped)
				{
					_rustEventSkipped = true;
					HapisLog.Info("CH47 Events: Rust's own oil rig CH47 skipped at " + spot + " - the RustEdit landing zone sends it instead");
				}

				return true;
			}
		}

		return false;
	}

	public static void OnHackStarted(HackableLockedCrate crate)
	{
		if (Zones.Count == 0 || crate == null)
		{
			return;
		}

		var spot = crate.transform.position;
		foreach (var zone in Zones)
		{
			if (!Near(zone.Crates, crate.prefabID, spot, CrateReach))
			{
				continue;
			}

			zone.Crate = crate;
			zone.SirenUntil = Time.realtimeSinceStartup + SirenSeconds;
			SetAlarms(zone, true);
			Call(zone);
			if (!zone.Watching)
			{
				zone.Watching = true;
				SingletonComponent<ServerMgr>.Instance.StartCoroutine(Watch(zone));
			}

			return;
		}
	}

	private static bool Near(List<(uint Prefab, Vector3 Spot)> links, uint prefab, Vector3 spot, float reach)
	{
		foreach (var link in links)
		{
			if (link.Prefab == prefab && (link.Spot - spot).sqrMagnitude <= reach * reach)
			{
				return true;
			}
		}

		return false;
	}

	private static void Call(CH47Zone zone)
	{
		if (zone.Heli != null && !zone.Heli.IsDestroyed)
		{
			return;
		}

		var entity = GameManager.server.CreateEntity(HeliPrefab, Vector3.zero, Quaternion.identity);
		var heli = entity == null ? null : entity.GetComponent<CH47HelicopterAIController>();
		if (heli == null)
		{
			HapisLog.Info("CH47 Events: couldn't create the CH47 for the landing zone at " + zone.Spot);
			return;
		}

		var landing = CH47LandingZone.GetClosest(zone.Spot);
		var target = landing != null ? landing.transform.position : zone.Spot;
		if (landing != null)
		{
			var seats = heli.mountPoints != null && heli.mountPoints.Count > 2 ? heli.mountPoints.Count - 2 : DefaultSeats;
			landing.dropoffScale = (zone.Scientists - 1 + 0.5f) / seats;
		}

		var start = target + Vector3Ex.Direction2D(target, new Vector3(0f, target.y, 0f)) * zone.Distance;
		start.y = target.y;
		heli.transform.position = start;
		heli.SetLandingTarget(target);
		heli.Spawn();
		zone.Heli = heli;
	}

	private static IEnumerator Watch(CH47Zone zone)
	{
		while (zone.Crate != null && !zone.Crate.IsDestroyed && !zone.Crate.IsFullyHacked())
		{
			SetAlarms(zone, true);
			yield return CoroutineEx.waitForSeconds(CheckSeconds);
		}

		zone.Crate = null;
		zone.Watching = false;
		SetAlarms(zone, false);
	}

	private static void SetAlarms(CH47Zone zone, bool on)
	{
		on = on && Time.realtimeSinceStartup < zone.SirenUntil;
		zone.Sirens.RemoveAll(s => s == null || s.IsDestroyed);
		zone.AlarmEntities.RemoveAll(a => a == null || a.IsDestroyed);
		if ((!zone.SirensSearched || zone.AlarmEntities.Count < zone.Alarms.Count) && Time.realtimeSinceStartup >= zone.NextSearch)
		{
			Search(zone);
			zone.SirensOn = !on;
		}

		if (zone.SirensOn != on)
		{
			zone.SirensOn = on;
			foreach (var siren in zone.Sirens)
			{
				siren.UpdateFromInput(on ? SirenPower : 0, 0);
			}
		}

		foreach (var alarm in zone.AlarmEntities)
		{
			if (alarm is DummySwitch dummy)
			{
				if (dummy.IsOn() != on)
				{
					dummy.SetOn(on);
				}
			}
			else if (alarm is IOEntity io)
			{
				io.UpdateFromInput(on ? 100 : 0, 0);
			}
		}
	}

	private static void Search(CH47Zone zone)
	{
		zone.SirensSearched = true;
		zone.NextSearch = Time.realtimeSinceStartup + SearchSeconds;
		zone.Sirens.Clear();
		zone.AlarmEntities.Clear();
		_sirenId ??= StringPool.Get(SirenPrefab);

		var wanted = new HashSet<uint> { _sirenId.Value };
		foreach (var link in zone.Alarms)
		{
			wanted.Add(link.Prefab);
		}

		var found = new bool[zone.Alarms.Count];
		foreach (var networkable in BaseNetworkable.serverEntities)
		{
			if (networkable is not BaseEntity entity || !wanted.Contains(entity.prefabID))
			{
				continue;
			}

			var spot = entity.transform.position;
			if (entity.prefabID == _sirenId && entity is IOEntity siren && siren.OwnerID == 0uL && (spot - zone.Spot).sqrMagnitude <= SirenReach * SirenReach)
			{
				zone.Sirens.Add(siren);
			}

			for (var i = 0; i < zone.Alarms.Count; i++)
			{
				var link = zone.Alarms[i];
				if (!found[i] && link.Prefab == entity.prefabID && (spot - link.Spot).sqrMagnitude <= AlarmReach * AlarmReach)
				{
					found[i] = true;
					zone.AlarmEntities.Add(entity);
					break;
				}
			}
		}
	}
}

[HarmonyPatch(typeof(HackableLockedCrate), nameof(HackableLockedCrate.StartHacking))]
internal static class CH47EventHackPatch
{
	[HarmonyPostfix]
	private static void Postfix(HackableLockedCrate __instance)
	{
		try
		{
			CH47Events.OnHackStarted(__instance);
		}
		catch (Exception e)
		{
			HapisLog.Once("CH47 events hack", e);
		}
	}
}

[HarmonyPatch(typeof(CH47ReinforcementListener), nameof(CH47ReinforcementListener.Call))]
internal static class CH47EventRustListenerPatch
{
	[HarmonyPrefix]
	private static bool Prefix(CH47ReinforcementListener __instance)
	{
		try
		{
			return __instance == null || !CH47Events.TakesOverRustEvent(__instance.transform.position);
		}
		catch (Exception e)
		{
			HapisLog.Once("CH47 events rust listener", e);
			return true;
		}
	}
}
