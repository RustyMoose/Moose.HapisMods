using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Moose.Hapis.Extra;

/// <summary>
/// Mod entry point.
/// </summary>
public class HapisExtraMod : IHarmonyModHooks
{
	private const int CurrentConfigVersion = 11;

	public static readonly HashSet<BaseNetworkable> Managed = [];
	public static BaseCombatEntity? Active;

	private static GameObject? _schedulerObject;

	public static void Log(string msg)
	{
		Debug.Log("[Hapis.Extra] " + msg);
	}

	private static string ModFolder => Path.Combine(Directory.GetCurrentDirectory(), "HarmonyMods");

	private static string ConfigPath => Path.Combine(ModFolder, "HapisExtra.xml");
	
	public void OnLoaded(OnHarmonyModLoadedArgs args)
	{
		_schedulerObject = new GameObject("HapisExtraScheduler");
		UnityEngine.Object.DontDestroyOnLoad(_schedulerObject);
		_schedulerObject.AddComponent<HapisExtraScheduler>();
		Log("Loaded. Drop point " + HapisExtraConstants.DropX + ", " + HapisExtraConstants.DropY + ", " + HapisExtraConstants.DropZ);
		Log("Auto spawn: first in " + HapisExtraConstants.FirstSpawnDelaySeconds + "s, then every " + HapisExtraConstants.AutoSpawnMinutes + " min.");
		Log("Startup command: " + (string.IsNullOrEmpty(HapisExtraConstants.StartupCommand) ? "(none)" : HapisExtraConstants.StartupCommand));
		Log("Pumps: " + (HapisExtraConstants.KeepPumpsFull
			                 ? "on (" + HapisExtraConstants.PumpPrefabKeywords + ", start at " + HapisExtraConstants.PumpStartAmount + ", after " + HapisExtraConstants.PumpTrickleDelayMinutes + " min refill slowly to " + HapisExtraConstants.PumpMaxAmount + " over " + HapisExtraConstants.PumpTrickleHours + " h)"
			                 : "off") + ", hand-taking blocked: " + HapisExtraConstants.BlockPumpTaking);
		Log("Water sources: " + (HapisExtraConstants.KeepWaterFull
			                         ? "on (" + HapisExtraConstants.WaterPrefabKeywords + ", full on start, then full every " + HapisExtraConstants.WaterRefillHours + " h)"
			                         : "off"));
		Log("Pump IO log: " + (HapisExtraConstants.LogPumpIO ? "on" : "off") + ", liquid prefab list: " + (HapisExtraConstants.LogLiquidPrefabs ? "on" : "off"));
	}

	public void OnUnloaded(OnHarmonyModUnloadedArgs args)
	{
		if (_schedulerObject != null)
		{
			UnityEngine.Object.Destroy(_schedulerObject);
		}
		Despawn(Active);
		Managed.Clear();
		Log("Unloaded.");
	}

	public static bool IsActiveAlive()
	{
		return Active != null && !Active.IsDestroyed;
	}

	public static void Despawn(BaseCombatEntity? ent)
	{
		if (ent == null)
		{
			return;
		}
		Managed.Remove(ent);
		if (!ent.IsDestroyed)
		{
			ent.Kill();
		}
		if (Active == ent)
		{
			Active = null;
		}
	}

	public static bool SpawnChinook(out string message)
	{
		message = "";
		if (IsActiveAlive())
		{
			message = "A Chinook is already active.";
			return false;
		}
		if (TerrainMeta.HeightMap == null || World.Size == 0)
		{
			message = "World not ready yet.";
			return false;
		}

		var dropPos = new Vector3(HapisExtraConstants.DropX, HapisExtraConstants.DropY, HapisExtraConstants.DropZ);
		var half = (float)World.Size * 0.5f;

		// pick a map-edge start point that is a decent distance from the drop
		var start = Vector3.zero;
		for (var i = 0; i < 10; i++)
		{
			var a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
			var r = half - 60f;
			start = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
			if ((start - new Vector3(dropPos.x, 0f, dropPos.z)).magnitude > 400f)
			{
				break;
			}
		}
		start.y = ChinookRoute.PathGround(start, dropPos) + HapisExtraConstants.CruiseClearance;

		var look = dropPos - start;
		look.y = 0f;
		var rot = Quaternion.LookRotation(look.normalized);

		var ent = GameManager.server.CreateEntity(HapisExtraConstants.Prefab, start, rot, true);
		if (ent == null)
		{
			message = "CreateEntity returned null. Check Prefab in config.";
			return false;
		}

		var ch47Controller = ent as CH47HelicopterAIController;
		if (ch47Controller == null)
		{
			ent.Kill();
			message = "Spawned entity is not a CH47HelicopterAIController.";
			return false;
		}

		Managed.Add(ch47Controller);   // add BEFORE Spawn so the kill guard is active from the start
		ent.Spawn();
		Active = ch47Controller;

		// switch off the vanilla AI brain so it cannot steer or despawn the heli
		var ch47Brain = ent.GetComponent<CH47AIBrain>();
		if (ch47Brain != null)
		{
			ch47Brain.SetEnabled(false);
			ch47Brain.enabled = false;
		}
		else
		{
			Log("CH47AIBrain component not found (nothing to disable).");
		}

		var route = ent.gameObject.AddComponent<ChinookRoute>();
		route.Init(ch47Controller, dropPos);

		message = "Chinook spawned at " + start;
		Log(message);
		return true;
	}
}
