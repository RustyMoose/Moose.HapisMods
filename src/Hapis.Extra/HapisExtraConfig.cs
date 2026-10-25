namespace Moose.Hapis.Extra;

/// <summary>
/// Mod configuration, saved to HarmonyMods\HapisExtra.xml.
/// </summary>
public class HapisExtraConfig
{
	public int ConfigVersion = 0;            // files without the current version are reset automatically

	public float DropX = -482.519f;
	public float DropY = 41.435f;
	public float DropZ = -918.106f;

	public float CruiseClearance = 80f;      // metres above highest ground on the way in/out
	public float DropClearance = 40f;        // metres above DropY while dropping
	public float OrbitRadius = 60f;
	public float OrbitSeconds = 25f;
	public float EgressTimeoutSeconds = 180f;

	public bool Invulnerable = true;
	public float AutoSpawnMinutes = 360f;        // time between Chinooks (6 hours), 0 = never
	public float FirstSpawnDelaySeconds = 7200f; // first Chinook after the map loads (2 hours)

	public string Prefab = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";

	// console command run once on every server start (leave empty to disable)
	public string StartupCommand = "powergrid.simulatepowerplantfuses 18";

	// crude oil pumps
	public bool KeepPumpsFull = true;                        // false = the mod leaves the pumps alone
	public string PumpPrefabKeywords = "crudeoilproducer";   // ALL (comma separated) must appear in the prefab path
	public string PumpItemShortname = "";                    // empty = use the pump's own default liquid (crude oil)
	public int PumpStartAmount = 100;                        // on every server start, pumps are raised to at least this
	public int PumpMaxAmount = 300;                          // the slow refill stops here
	public float PumpTrickleDelayMinutes = 30f;              // slow refill begins this long after the start fill
	public float PumpTrickleHours = 2f;                      // hours to go from PumpStartAmount to PumpMaxAmount
	public bool BlockPumpTaking = true;                      // players can open a pump but cannot take the oil out by hand

	// water treatment pipe access points: filled to FULL on start, then to full again every X hours
	public bool KeepWaterFull = true;
	public string WaterPrefabKeywords = "watertreatment,pipe";   // ALL must appear in the prefab path (ignores _ - and spaces)
	public string WaterItemShortname = "";                       // empty = the source's own default liquid, else "water"
	public float WaterRefillHours = 2f;                          // 0 = only once at start

	// diagnostics (log only, changes nothing)
	public bool LogPumpIO = false;          // pump IO slots, once
	public bool LogLiquidPrefabs = true;    // lists every liquid container prefab on the map, once
}
