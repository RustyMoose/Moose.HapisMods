using System.Reflection;

namespace Moose.Hapis.NPC;

public class Config
{
	public string ConfigVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

	public bool UseRustEditNPCLogic;

	public float AITickRate = 2f;

	public float SpawnBlockingDistance = 30f;

	public bool APCIgnoreNPCs = true;

	public bool NPCAutoTurretsIgnoreNPCs = true;

	public float ScientistNPCHealth = 120f;

	public float ScientistNPCAimMultiplyer = 0.8f;

	public float ScientistNPCRoamRange = 50f;

	public bool ScientistNPCIgnoreSafeZonePlayers = true;

	public bool ScientistNPCCheckLOS = true;

	public float ScientistNPCDamageScale = 0.4f;

	public float ScientistNPCSenseRange = 40f;

	public float ScientistNPCMemoryDuration = 1f;

	public float ScientistNPCTargetLostRange = 80f;

	public float ScientistNPCAttackRangeMultiplier = 1f;

	public float PeacekeeperNPCHealth = 120f;

	public float PeacekeeperNPCAimMultiplyer = 0.8f;

	public float PeacekeeperNPCRoamRange = 30f;

	public bool PeacekeeperNPCIgnoreSafeZonePlayers = true;

	public bool PeacekeeperNPCCheckLOS = true;

	public float PeacekeeperNPCDamageScale = 0.4f;

	public float PeacekeeperNPCSenseRange = 30f;

	public float PeacekeeperNPCMemoryDuration = 1f;

	public float PeacekeeperNPCTargetLostRange = 80f;

	public float PeacekeeperNPCAttackRangeMultiplier = 1f;

	public float HeavyScientistNPCHealth = 190f;

	public float HeavyScientistNPCAimMultiplyer = 0.8f;

	public float HeavyScientistNPCRoamRange = 50f;

	public bool HeavyScientistNPCIgnoreSafeZonePlayers = true;

	public bool HeavyScientistNPCCheckLOS = true;

	public float HeavyScientistNPCDamageScale = 0.6f;

	public float HeavyScientistNPCSenseRange = 30f;

	public float HeavyScientistNPCMemoryDuration = 1f;

	public float HeavyScientistNPCTargetLostRange = 80f;

	public float HeavyScientistNPCAttackRangeMultiplier = 1f;

	public float JunkpileScientistNPCHealth = 120f;

	public float JunkpileScientistNPCAimMultiplyer = 0.8f;

	public float JunkpileScientistNPCRoamRange = 50f;

	public bool JunkpileScientistNPCIgnoreSafeZonePlayers = true;

	public bool JunkpileScientistNPCCheckLOS = true;

	public float JunkpileScientistNPCDamageScale = 0.4f;

	public float JunkpileScientistNPCSenseRange = 20f;

	public float JunkpileScientistNPCMemoryDuration = 1f;

	public float JunkpileScientistNPCTargetLostRange = 80f;

	public float JunkpileScientistNPCAttackRangeMultiplier = 1f;

	public float BanditNPCHealth = 120f;

	public float BanditNPCAimMultiplyer = 0.8f;

	public float BanditNPCRoamRange = 50f;

	public bool BanditNPCIgnoreSafeZonePlayers = true;

	public bool BanditNPCCheckLOS = true;

	public float BanditNPCDamageScale = 0.4f;

	public float BanditNPCSenseRange = 40f;

	public float BanditNPCMemoryDuration = 1f;

	public float BanditNPCTargetLostRange = 80f;

	public float BanditNPCAttackRangeMultiplier = 1f;

	public float MurdererNPCHealth = 100f;

	public float MurdererNPCRoamRange = 30f;

	public bool MurdererNPCIgnoreSafeZonePlayers = true;

	public bool MurdererNPCCheckLOS = true;

	public float MurdererNPCDamageScale = 0.4f;

	public float MurdererNPCSenseRange = 20f;

	public float MurdererNPCMemoryDuration = 1f;

	public float MurdererNPCTargetLostRange = 40f;

	public float MurdererNPCAttackRangeMultiplier = 1f;

	public float ScarecrowNPCHealth = 60f;

	public float ScarecrowNPCRoamRange = 30f;

	public bool ScarecrowNPCIgnoreSafeZonePlayers = true;

	public bool ScarecrowNPCCheckLOS = true;

	public float ScarecrowNPCDamageScale = 0.4f;

	public float ScarecrowNPCSenseRange = 20f;

	public float ScarecrowNPCMemoryDuration = 1f;

	public float ScarecrowNPCTargetLostRange = 40f;

	public float ScarecrowNPCAttackRangeMultiplier = 1f;
}
