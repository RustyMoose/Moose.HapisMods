using System.Collections.Generic;
using Facepunch;
using Moose.Hapis.NPC.Enums;
using Moose.Hapis.NPC.Models;
using Rust;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Moose.Hapis.NPC.Behaviours;

public class NpcSpawner : MonoBehaviour
{
	public SerializedNpcSpawner serializedNPCSpawner;

	public BaseEntity BOT;

	public BaseEntity target;

	public bool Respawning = true;

	public float Delay;

	public float RoamDistance = 30f;

	public NpcType npctype;

	public string prefab;

	public bool cooldown;

	public Vector3 OldPos;

	public bool stationary;

	private static readonly Dictionary<NpcType, string> TypeToPrefabMap = new()
	{
		{ NpcType.Scientist, "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roamtethered.prefab" },
		{ NpcType.Peacekeeper, "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_peacekeeper.prefab" },
		{ NpcType.HeavyScientist, "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab" },
		{ NpcType.JunkpileScientist, "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_junkpile_pistol.prefab" },
		{ NpcType.Bandit, "assets/rust.ai/agents/npcplayer/humannpc/banditguard/npc_bandit_guard.prefab" },
		{ NpcType.Murderer, "assets/prefabs/npc/scarecrow/scarecrow.prefab" },
		{ NpcType.Scarecrow, "assets/prefabs/npc/scarecrow/scarecrow.prefab" }
	};

	public void Initialize(SerializedNpcSpawner serializedNpcSpawner)
	{
		serializedNPCSpawner = serializedNpcSpawner;
		npctype = (NpcType)serializedNPCSpawner.NpcType;
		prefab = TypeToPrefabMap[npctype];
		OldPos = serializedNPCSpawner.Position;
	}

	public void SpawnNPC()
	{
		Vector3 position = serializedNPCSpawner.Position;
		stationary = !HapisNPC.IsOnGround(ref position, out var areaMask);
		
		ScientistNPC val = null;
		ScarecrowNPC val2 = null;
		
		if (HapisNPC.Instance.Config.UseRustEditNPCLogic || npctype == NpcType.Scarecrow || npctype == NpcType.Murderer)
		{
			var val3 = GameManager.server.CreateEntity(prefab, position, Quaternion.identity, false);
			if (val3 != null)
			{
				val3.enableSaving = false;
				val3.gameObject.AwakeFromInstantiate();
				val3.EnableSaving(false);
				val3.Spawn();
				Respawning = false;
				BOT = val3;
				val = val3.GetComponent<ScientistNPC>();
				val2 = val3.GetComponent<ScarecrowNPC>();
				try
				{
					var component = val3.GetComponent<NavMeshAgent>();
					if (component != null)
					{
						component.areaMask = areaMask;
						component.agentTypeID = areaMask != 25 && areaMask != 8 ? (-1372625422) : 0;
						if (stationary)
						{
							component.isStopped = true;
							component.enabled = false;
						}
					}
				}
				catch
				{
				}
			}
		}
		else
		{
			prefab = StringPool.Get(3763080634u);
			var val4 = Facepunch.Instantiate.GameObject(GameManager.server.FindPrefab(prefab));
			val4.SetActive(false);
			val4.name = prefab;
			val4.transform.position = position;
			val = val4.GetComponent<ScientistNPC>();
			SceneManager.MoveGameObjectToScene(val4, Server.EntityScene);
			if (val != null)
			{
				val.userID = (ulong)Random.Range(0, 10000000);
				val.UserIDString = val.userID.ToString();
				val.displayName = "[NPC]" + RandomUsernames.Get(val.userID);
				val.startHealth = 120f;
				val4.SetActive(true);
				val.EnableSaving(false);
				val.Spawn();
				Respawning = false;
				BOT = val;
				try
				{
					var component2 = val.GetComponent<NavMeshAgent>();
					if (component2 != null)
					{
						component2.areaMask = areaMask;
						component2.agentTypeID = ((areaMask != 25 && areaMask != 8) ? (-1372625422) : 0);
						if (stationary)
						{
							component2.isStopped = true;
							component2.enabled = false;
						}
					}
				}
				catch
				{
					// ignored
				}
			}
		}
		BasePlayer val5 = null;
		if (val != null)
		{
			val5 = val;
		}
		else if (val2 != null)
		{
			val5 = val2;
		}
		switch (npctype)
		{
		case NpcType.Scientist:
			((BaseCombatEntity)val).InitializeHealth(HapisNPC.Instance.Config.ScientistNPCHealth, HapisNPC.Instance.Config.ScientistNPCHealth);
			if (val != null)
			{
				RoamDistance = HapisNPC.Instance.Config.ScientistNPCRoamRange;
				val.aimConeScale = HapisNPC.Instance.Config.ScientistNPCAimMultiplyer;
				val.damageScale = HapisNPC.Instance.Config.ScientistNPCDamageScale;
				val.Brain.CheckLOS = HapisNPC.Instance.Config.ScientistNPCCheckLOS;
				val.Brain.IgnoreSafeZonePlayers = HapisNPC.Instance.Config.ScientistNPCIgnoreSafeZonePlayers;
				val.Brain.SenseRange = HapisNPC.Instance.Config.ScientistNPCSenseRange;
				val.Brain.MemoryDuration = HapisNPC.Instance.Config.ScientistNPCMemoryDuration;
				val.Brain.TargetLostRange = HapisNPC.Instance.Config.ScientistNPCTargetLostRange;
				val.Brain.AttackRangeMultiplier = HapisNPC.Instance.Config.ScientistNPCAttackRangeMultiplier;
				if (!HapisNPC.Instance.Config.UseRustEditNPCLogic)
				{
					val.inventory.Strip();
					HapisNPC.Instance.DressNPC(val, npctype);
				}
			}
			break;
		case NpcType.Peacekeeper:
			((BaseCombatEntity)val5).InitializeHealth(HapisNPC.Instance.Config.PeacekeeperNPCHealth, HapisNPC.Instance.Config.PeacekeeperNPCHealth);
			if (val != null)
			{
				RoamDistance = HapisNPC.Instance.Config.PeacekeeperNPCRoamRange;
				val.aimConeScale = HapisNPC.Instance.Config.PeacekeeperNPCAimMultiplyer;
				val.damageScale = HapisNPC.Instance.Config.PeacekeeperNPCDamageScale;
				val.Brain.CheckLOS = HapisNPC.Instance.Config.PeacekeeperNPCCheckLOS;
				val.Brain.IgnoreSafeZonePlayers = HapisNPC.Instance.Config.PeacekeeperNPCIgnoreSafeZonePlayers;
				val.Brain.SenseRange = HapisNPC.Instance.Config.PeacekeeperNPCSenseRange;
				val.Brain.MemoryDuration = HapisNPC.Instance.Config.PeacekeeperNPCMemoryDuration;
				val.Brain.TargetLostRange = HapisNPC.Instance.Config.PeacekeeperNPCTargetLostRange;
				val.Brain.AttackRangeMultiplier = HapisNPC.Instance.Config.PeacekeeperNPCAttackRangeMultiplier;
				if (!HapisNPC.Instance.Config.UseRustEditNPCLogic)
				{
					val.inventory.Strip();
					HapisNPC.Instance.DressNPC(val, npctype);
				}
			}
			break;
		case NpcType.HeavyScientist:
			((BaseCombatEntity)val5).InitializeHealth(HapisNPC.Instance.Config.HeavyScientistNPCHealth, HapisNPC.Instance.Config.HeavyScientistNPCHealth);
			if (val != null)
			{
				RoamDistance = HapisNPC.Instance.Config.HeavyScientistNPCRoamRange;
				val.aimConeScale = HapisNPC.Instance.Config.HeavyScientistNPCAimMultiplyer;
				val.damageScale = HapisNPC.Instance.Config.HeavyScientistNPCDamageScale;
				val.Brain.CheckLOS = HapisNPC.Instance.Config.HeavyScientistNPCCheckLOS;
				val.Brain.IgnoreSafeZonePlayers = HapisNPC.Instance.Config.HeavyScientistNPCIgnoreSafeZonePlayers;
				val.Brain.SenseRange = HapisNPC.Instance.Config.HeavyScientistNPCSenseRange;
				val.Brain.MemoryDuration = HapisNPC.Instance.Config.HeavyScientistNPCMemoryDuration;
				val.Brain.TargetLostRange = HapisNPC.Instance.Config.HeavyScientistNPCTargetLostRange;
				val.Brain.AttackRangeMultiplier = HapisNPC.Instance.Config.HeavyScientistNPCAttackRangeMultiplier;
				if (!HapisNPC.Instance.Config.UseRustEditNPCLogic)
				{
					val.inventory.Strip();
					HapisNPC.Instance.DressNPC(val, npctype);
				}
			}
			break;
		case NpcType.JunkpileScientist:
			((BaseCombatEntity)val5).InitializeHealth(HapisNPC.Instance.Config.JunkpileScientistNPCHealth, HapisNPC.Instance.Config.JunkpileScientistNPCHealth);
			if (val != null)
			{
				RoamDistance = HapisNPC.Instance.Config.JunkpileScientistNPCRoamRange;
				val.aimConeScale = HapisNPC.Instance.Config.JunkpileScientistNPCAimMultiplyer;
				val.damageScale = HapisNPC.Instance.Config.JunkpileScientistNPCDamageScale;
				val.Brain.CheckLOS = HapisNPC.Instance.Config.JunkpileScientistNPCCheckLOS;
				val.Brain.IgnoreSafeZonePlayers = HapisNPC.Instance.Config.JunkpileScientistNPCIgnoreSafeZonePlayers;
				val.Brain.SenseRange = HapisNPC.Instance.Config.JunkpileScientistNPCSenseRange;
				val.Brain.MemoryDuration = HapisNPC.Instance.Config.JunkpileScientistNPCMemoryDuration;
				val.Brain.TargetLostRange = HapisNPC.Instance.Config.JunkpileScientistNPCTargetLostRange;
				val.Brain.AttackRangeMultiplier = HapisNPC.Instance.Config.JunkpileScientistNPCAttackRangeMultiplier;
				if (!HapisNPC.Instance.Config.UseRustEditNPCLogic)
				{
					val.inventory.Strip();
					HapisNPC.Instance.DressNPC(val, npctype);
				}
			}
			break;
		case NpcType.Bandit:
			((BaseCombatEntity)val5).InitializeHealth(HapisNPC.Instance.Config.BanditNPCHealth, HapisNPC.Instance.Config.BanditNPCHealth);
			if (val != null)
			{
				RoamDistance = HapisNPC.Instance.Config.BanditNPCRoamRange;
				val.aimConeScale = HapisNPC.Instance.Config.BanditNPCAimMultiplyer;
				val.damageScale = HapisNPC.Instance.Config.BanditNPCDamageScale;
				val.Brain.CheckLOS = HapisNPC.Instance.Config.BanditNPCCheckLOS;
				val.Brain.IgnoreSafeZonePlayers = HapisNPC.Instance.Config.BanditNPCIgnoreSafeZonePlayers;
				val.Brain.SenseRange = HapisNPC.Instance.Config.BanditNPCSenseRange;
				val.Brain.MemoryDuration = HapisNPC.Instance.Config.BanditNPCMemoryDuration;
				val.Brain.TargetLostRange = HapisNPC.Instance.Config.BanditNPCTargetLostRange;
				val.Brain.AttackRangeMultiplier = HapisNPC.Instance.Config.BanditNPCAttackRangeMultiplier;
				if (!HapisNPC.Instance.Config.UseRustEditNPCLogic)
				{
					val.inventory.Strip();
					HapisNPC.Instance.DressNPC(val, npctype);
				}
			}
			break;
		case NpcType.Murderer:
			((BaseCombatEntity)val5).InitializeHealth(HapisNPC.Instance.Config.MurdererNPCHealth, HapisNPC.Instance.Config.MurdererNPCHealth);
			if (val2 != null)
			{
				RoamDistance = HapisNPC.Instance.Config.MurdererNPCRoamRange;
				val2.damageScale = HapisNPC.Instance.Config.MurdererNPCDamageScale;
				val2.Brain.CheckLOS = HapisNPC.Instance.Config.MurdererNPCCheckLOS;
				val2.Brain.IgnoreSafeZonePlayers = HapisNPC.Instance.Config.MurdererNPCIgnoreSafeZonePlayers;
				val2.Brain.SenseRange = HapisNPC.Instance.Config.MurdererNPCSenseRange;
				val2.Brain.MemoryDuration = HapisNPC.Instance.Config.MurdererNPCMemoryDuration;
				val2.Brain.TargetLostRange = HapisNPC.Instance.Config.MurdererNPCTargetLostRange;
				val2.Brain.AttackRangeMultiplier = HapisNPC.Instance.Config.MurdererNPCAttackRangeMultiplier;
				val2.inventory.Strip();
				HapisNPC.Instance.DressNPC(val2, npctype);
			}
			break;
		case NpcType.Scarecrow:
			((BaseCombatEntity)val5).InitializeHealth(HapisNPC.Instance.Config.ScarecrowNPCHealth, HapisNPC.Instance.Config.ScarecrowNPCHealth);
			if (val2 != null)
			{
				RoamDistance = HapisNPC.Instance.Config.ScarecrowNPCRoamRange;
				val2.damageScale = HapisNPC.Instance.Config.ScarecrowNPCDamageScale;
				val2.Brain.CheckLOS = HapisNPC.Instance.Config.ScarecrowNPCCheckLOS;
				val2.Brain.IgnoreSafeZonePlayers = HapisNPC.Instance.Config.ScarecrowNPCIgnoreSafeZonePlayers;
				val2.Brain.SenseRange = HapisNPC.Instance.Config.ScarecrowNPCSenseRange;
				val2.Brain.MemoryDuration = HapisNPC.Instance.Config.ScarecrowNPCMemoryDuration;
				val2.Brain.TargetLostRange = HapisNPC.Instance.Config.ScarecrowNPCTargetLostRange;
				val2.Brain.AttackRangeMultiplier = HapisNPC.Instance.Config.ScarecrowNPCAttackRangeMultiplier;
				val2.inventory.Strip();
				HapisNPC.Instance.DressNPC(val2, npctype);
			}
			break;
		}
	}
}
