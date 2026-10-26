namespace Moose.Hapis.Extra;

/// <summary>
/// Mod configuration, saved to HarmonyMods\HapisExtra.xml.
/// </summary>
public static class HapisExtraConstants
{
	public const float DropX = -482.519f;
	public const float DropY = 41.435f;
	public const float DropZ = -918.106f;

	public const float CruiseClearance = 80f;      // metres above highest ground on the way in/out
	public const float DropClearance = 40f;        // metres above DropY while dropping
	public const float OrbitRadius = 60f;
	public const float OrbitSeconds = 25f;
	public const float EgressTimeoutSeconds = 180f;

	public const bool Invulnerable = true;
	public const float AutoSpawnMinutes = 360f;        // time between Chinooks (6 hours), 0 = never
	public const float FirstSpawnDelaySeconds = 7200f; // first Chinook after the map loads (2 hours)

	public const string Prefab = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";

	// console command run once on every server start (leave empty to disable)
	public const string StartupCommand = "powergrid.simulatepowerplantfuses 18";

	// crude oil pumps
	public const bool KeepPumpsFull = true;                        // false = the mod leaves the pumps alone
	public const string PumpPrefabKeywords = "crudeoilproducer";   // ALL (comma separated) must appear in the prefab path
	public const int PumpStartAmount = 100;                        // on every server start, pumps are raised to at least this
	public const int PumpMaxAmount = 300;                          // the slow refill stops here
	public const float PumpTrickleDelayMinutes = 30f;              // slow refill begins this long after the start fill
	public const float PumpTrickleHours = 2f;                      // hours to go from PumpStartAmount to PumpMaxAmount
	public const bool BlockPumpTaking = true;                      // players can open a pump but cannot take the oil out by hand

	// water treatment pipe access points: filled to FULL on start, then to full again every X hours
	public const bool KeepWaterFull = true;
	public const string WaterPrefabKeywords = "watertreatment,pipe";   // ALL must appear in the prefab path (ignores _ - and spaces)
	public const string WaterItemShortname = "";                       // empty = the source's own default liquid, else "water"
	public const float WaterRefillHours = 2f;                          // 0 = only once at start
}
