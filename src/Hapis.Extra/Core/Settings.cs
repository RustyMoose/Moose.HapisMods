using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Moose.Hapis.Extra;

public class DropSpot
{
	[JsonProperty("Name")]
	public string Name = "Drop";

	[JsonProperty("Position X")]
	public float X;

	[JsonProperty("Position Y")]
	public float Y;

	[JsonProperty("Position Z")]
	public float Z;

	[JsonProperty("First Chinook Hours After Server Start")]
	public float FirstHours = 2f;

	[JsonProperty("Hours Between Chinooks (0 = never)")]
	public float EveryHours = 4f;
}

public class SettingsFile
{
	[JsonProperty("Chinook Drops")]
	public List<DropSpot> Drops = new()
	{
		new DropSpot { Name = "Legacy Offices", X = -482.519f, Y = 41.435f, Z = -918.106f, FirstHours = 2f, EveryHours = 4f },
		new DropSpot { Name = "Milly", X = -833.52f, Y = 68.98f, Z = -237.02f, FirstHours = 3f, EveryHours = 5f },
		new DropSpot { Name = "Sewer", X = 701.30f, Y = 182.46f, Z = 323.69f, FirstHours = 4f, EveryHours = 6f }
	};

	[JsonProperty("Chinook Height Above Ground While Flying")]
	public float CruiseClearance = 80f;

	[JsonProperty("Chinook Height Above Drop Point While Dropping")]
	public float DropClearance = 40f;

	[JsonProperty("Chinook Circle Radius After Drop")]
	public float OrbitRadius = 60f;

	[JsonProperty("Chinook Circle Seconds After Drop")]
	public float OrbitSeconds = 25f;

	[JsonProperty("Chinook Leave Timeout Seconds")]
	public float EgressTimeoutSeconds = 180f;

	[JsonProperty("Chinook Can't Be Destroyed")]
	public bool Invulnerable = true;

	[JsonProperty("Remove Last Crate If Nobody Hacked It")]
	public bool RemoveOldCrate = true;

	[JsonProperty("Chinook Prefab")]
	public string Prefab = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";

	[JsonProperty("Console Command On Server Start (empty = none)")]
	public string StartupCommand = "powergrid.simulatepowerplantfuses 18";

	[JsonProperty("Keep Crude Pumps Filled")]
	public bool KeepPumpsFull = true;

	[JsonProperty("Crude Pump Prefab Keywords (all must match, comma separated)")]
	public string PumpPrefabKeywords = "crudeoilproducer";

	[JsonProperty("Crude Pump Amount On Server Start")]
	public int PumpStartAmount = 100;

	[JsonProperty("Crude Pump Max Amount")]
	public int PumpMaxAmount = 300;

	[JsonProperty("Crude Pump Slow Refill Starts After Minutes")]
	public float PumpTrickleDelayMinutes = 30f;

	[JsonProperty("Crude Pump Hours To Refill From Start To Max")]
	public float PumpTrickleHours = 2f;

	[JsonProperty("Block Taking Oil Out Of Crude Pumps")]
	public bool BlockPumpTaking = true;

	[JsonProperty("Keep Water Sources Full")]
	public bool KeepWaterFull = true;

	[JsonProperty("Water Source Prefab Keywords (all must match, comma separated)")]
	public string WaterPrefabKeywords = "watertreatment,pipe";

	[JsonProperty("Water Item Shortname (empty = the source's own liquid)")]
	public string WaterItemShortname = "";

	[JsonProperty("Water Refill Hours (0 = only on server start)")]
	public float WaterRefillHours = 2f;
}

public static class Settings
{
	private const string Folder = "HarmonyConfig";
	private const string FileName = "HapisExtras.json";

	public static SettingsFile Current = new();

	private static readonly JsonSerializerSettings Json = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };

	public static void Load()
	{
		var path = Path.Combine(Folder, FileName);
		var file = new SettingsFile();

		try
		{
			if (File.Exists(path))
			{
				file = JsonConvert.DeserializeObject<SettingsFile>(File.ReadAllText(path), Json) ?? new SettingsFile();
			}

			Directory.CreateDirectory(Folder);
			File.WriteAllText(path, JsonConvert.SerializeObject(file, Formatting.Indented));
		}
		catch (Exception e)
		{
			Log.Once("settings", e);
		}

		file.Drops ??= new List<DropSpot>();
		file.Drops.RemoveAll(d => d == null);
		foreach (var drop in file.Drops)
		{
			drop.Name = string.IsNullOrWhiteSpace(drop.Name) ? "Drop" : drop.Name.Trim();
		}

		file.Prefab ??= string.Empty;
		file.StartupCommand ??= string.Empty;
		file.PumpPrefabKeywords ??= string.Empty;
		file.WaterPrefabKeywords ??= string.Empty;
		file.WaterItemShortname ??= string.Empty;
		file.PumpStartAmount = Math.Max(0, file.PumpStartAmount);
		file.PumpMaxAmount = Math.Max(file.PumpStartAmount, file.PumpMaxAmount);
		Current = file;
	}
}
