using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using ConVar;
using Moose.Hapis.NPC.Enums;
using Moose.Hapis.NPC.Models;
using Rust;
using UnityEngine;
using UnityEngine.AI;

namespace Moose.Hapis.NPC;

public class HapisNPC
{
	public static HapisNPC Instance = null!;

	public Config Config = new();

	public SerializedNPCSpawner? SerializedNpcSpawner;

	private SerializedNPCData? _serializedNpcData;

	private readonly List<Behaviours.NpcSpawner> _npcSpawnerList = [];

	private Coroutine? _npcThread;

	private readonly MethodInfo? _startAttackCooldown = typeof(NPCPlayer).GetMethod("StartAttackCooldown", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	public void InitializeHook()
	{
		if (_serializedNpcData == null)
		{
			var array = GetSerializedData("SerializedNPCData");
			if (array != null)
			{
				_serializedNpcData = DeserializePlugin<SerializedNPCData>(array);
			}
		}
		
		if (_serializedNpcData != null)
		{
			Debug.LogWarning("[Hapis.NPCSpawner] Loaded " + _serializedNpcData.npcSpawners.Count + " NPC Spawners.");
			foreach (var npcSpawner in _serializedNpcData.npcSpawners)
			{
				var nPCSpawner = new GameObject("NPCSpawner").AddComponent<Behaviours.NpcSpawner>();
				nPCSpawner.Initialize(npcSpawner);
				_npcSpawnerList.Add(nPCSpawner);
			}
		}
		
		_npcThread ??= SingletonComponent<ServerMgr>.Instance.StartCoroutine(NPCAIFunction());
	}

	public bool AttackedHook(HitInfo info, BasePlayer __instance)
	{
		if (!Config.UseRustEditNPCLogic && info.InitiatorPlayer != null)
		{
			foreach (var npcSpawner in _npcSpawnerList)
			{
				if (!(npcSpawner.BOT.ToPlayer() == __instance))
				{
					continue;
				}
				
				var initiatorPlayer = info.InitiatorPlayer;
				if (initiatorPlayer == null)
				{
					return true;
				}
				if (BasePlayer.activePlayerList.Contains(initiatorPlayer))
				{
					var val = (ScarecrowNPC)((__instance is ScarecrowNPC) ? __instance : null);
					if (val != null)
					{
						val.Brain.Navigator.SetDestination(initiatorPlayer.transform.position, (BaseNavigator.NavigationSpeed)3);
						return true;
					}
					var npc = (ScientistNPC)(__instance is ScientistNPC ? __instance : null);
					if (npc != null && npc.Brain != null)
					{
						npc.Brain.Senses.Memory.Targets.Clear();
						npc.Brain.Senses.Memory.Players.Clear();
						npc.Brain.Senses.Memory.Threats.Clear();
						npc.Brain.Senses.Memory.Targets.Add(initiatorPlayer);
						npc.Brain.Senses.Memory.Players.Add(initiatorPlayer);
						npc.Brain.Senses.Memory.Threats.Add(initiatorPlayer);
						if (npc.Brain.AttackRangeMultiplier != 10f)
						{
							var oldTLR = npc.Brain.TargetLostRange;
							var oldARM = npc.Brain.AttackRangeMultiplier;
							var oldAIM = npc.aimConeScale;
							npc.aimConeScale = 0.5f;
							npc.Brain.TargetLostRange = 800f;
							npc.Brain.AttackRangeMultiplier = 10f;
							npcSpawner.cooldown = true;
							npc.Brain.Events.Memory.Entity.Set(initiatorPlayer, 0);
							npc.Invoke((Action)delegate
							{
								if (npcSpawner != null)
								{
									npcSpawner.cooldown = false;
								}
								if (npc != null)
								{
									npc.aimConeScale = oldAIM;
									npc.Brain.TargetLostRange = oldTLR;
									npc.Brain.AttackRangeMultiplier = oldARM;
									npc.Brain.Senses.Memory.Targets.Clear();
									npc.Brain.Senses.Memory.Players.Clear();
									npc.Brain.Senses.Memory.Threats.Clear();
								}
							}, 8f);
						}
					}
				}
				return true;
			}
		}
		return true;
	}

	public bool AutoTurretHook(BaseCombatEntity targ, AutoTurret __instance)
	{
		if (__instance.OwnerID == 0L && Config.NPCAutoTurretsIgnoreNPCs)
		{
			foreach (var nPCs in _npcSpawnerList)
			{
				if (!(nPCs == null) && !(nPCs.BOT == null) && nPCs.BOT == targ)
				{
					return false;
				}
			}
		}
		return true;
	}

	public bool APCHook(BaseEntity ent, BradleyAPC __instance)
	{
		if (ent is NPCPlayer && Config.APCIgnoreNPCs)
		{
			foreach (var nPCs in _npcSpawnerList)
			{
				if (!(nPCs == null) && !(nPCs.BOT == null) && nPCs.BOT == ent)
				{
					return false;
				}
			}
		}
		return true;
	}

	public bool AnyPlayersNearby(Vector3 position, float maxDist)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		var list = Facepunch.Pool.Get<List<BasePlayer>>();
		Vis.Entities(position, maxDist, list, 131072);
		var result = false;
		foreach (var item in list)
		{
			if (!item.IsNpc && !item.IsConnected && !item.IsSleeping() && item.IsAlive())
			{
				result = true;
				break;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref list);
		return result;
	}

	private IEnumerator NPCAIFunction()
	{
		yield return CoroutineEx.waitForSeconds(5f);
		var checks = 0;
		var _instruction = FPS.limit > 30 ? CoroutineEx.waitForSeconds(0.01f) : CoroutineEx.waitForSeconds(0.001f);
		
		Debug.LogWarning("[Hapis.NPCSpawner] AI Thread Running on " + _npcSpawnerList.Count + " NPCs");
		
		while (_npcThread != null)
		{
			foreach (var npcs in _npcSpawnerList)
			{
				var num = checks + 1;
				checks = num;
				if (num >= 50)
				{
					checks = 0;
					yield return _instruction;
				}
				try
				{
					if (npcs == null)
					{
						continue;
					}
					if (npcs.Respawning)
					{
						if (npcs.Delay <= UnityEngine.Time.time && !AnyPlayersNearby(npcs.serializedNPCSpawner.position, Config.SpawnBlockingDistance))
						{
							npcs.SpawnNPC();
						}
						continue;
					}
					if ((npcs.BOT == null || npcs.BOT.IsDestroyed) && !npcs.Respawning)
					{
						npcs.Respawning = true;
						npcs.Delay = UnityEngine.Time.time + UnityEngine.Random.Range(npcs.serializedNPCSpawner.respawnMin, npcs.serializedNPCSpawner.respawnMax);
						continue;
					}
					if (Config.UseRustEditNPCLogic || npcs.npctype == NpcType.Scarecrow || npcs.npctype == NpcType.Murderer)
					{
						if (npcs.BOT is not ScarecrowNPC scarecrowNpc)
						{
							continue;
						}
						
						if (npcs.target == null)
						{
							try
							{
								npcs.target = scarecrowNpc.Brain.Senses.GetNearestTarget(scarecrowNpc.Brain.SenseRange);
							}
							catch
							{
								npcs.target = null;
							}
						}
						if (npcs.target != null)
						{
							if (Vector3.Distance((npcs.target).transform.position, scarecrowNpc.transform.position) >= scarecrowNpc.Brain.SenseRange)
							{
								scarecrowNpc.Brain.Events.Memory.Entity.Set(scarecrowNpc, 0);
								scarecrowNpc.Brain.Navigator.SetDestination(npcs.serializedNPCSpawner.position, (BaseNavigator.NavigationSpeed)2);
								npcs.target = null;
								continue;
							}
							scarecrowNpc.Brain.Events.Memory.Entity.Set(npcs.target, 0);
							if (scarecrowNpc.IsTargetInRange(npcs.target, out var num2))
							{
								var obj2 = scarecrowNpc != null ? scarecrowNpc.GetHeldEntity() : null;
								var val2 = (BaseMelee)((obj2 is BaseMelee) ? obj2 : null);
								if (!(val2 == null) && !(val2).HasAttackCooldown() && Vector3.Distance(npcs.target.transform.position, ((Component)scarecrowNpc).transform.position) <= val2.maxDistance && scarecrowNpc.MeleeAttack())
								{
									(npcs.target.ToPlayer()).Hurt(val2.TotalDamage() * scarecrowNpc.damageScale, (DamageType)10, scarecrowNpc);
									_startAttackCooldown.Invoke(val2, new object[1] { val2.attackSpacing });
								}
							}
						}
						else
						{
							RandomMovement(npcs);
							npcs.target = null;
						}
						continue;
					}

					if (npcs.BOT is not ScientistNPC val3)
					{
						continue;
					}
					
					try
					{
						if (val3.GetGun().primaryMagazine.contents < 1)
						{
							val3.AttemptReload();
						}
					}
					catch
					{
					}
					if (npcs.target == null)
					{
						try
						{
							npcs.target = val3.Brain.Senses.GetNearestTarget(val3.Brain.SenseRange);
						}
						catch
						{
							npcs.target = null;
						}
					}
					if (npcs.target != null)
					{
						if (Vector3.Distance(npcs.target.transform.position, npcs.BOT.transform.position) > val3.Brain.SenseRange)
						{
							if (!npcs.cooldown)
							{
								npcs.target = null;
								val3.Brain.Events.Memory.Entity.Set(val3, 0);
							}
							if (!val3.Brain.Navigator.Moving)
							{
								val3.Brain.Navigator.SetDestination(npcs.serializedNPCSpawner.position, (BaseNavigator.NavigationSpeed)3);
							}
						}
						else
						{
							if (!npcs.cooldown)
							{
								(val3).Brain.Events.Memory.Entity.Set(npcs.target, 0);
							}
							if (!(val3).Brain.Navigator.Moving)
							{
								(val3).Brain.Navigator.SetDestination((npcs.target).transform.position, (BaseNavigator.NavigationSpeed)2);
							}
						}
					}
					else
					{
						RandomMovement(npcs);
						npcs.target = null;
					}
				}
				catch
				{
					// ignored
				}
			}
			yield return CoroutineEx.waitForSeconds(Config.AITickRate);
		}
		
		Debug.LogWarning("Stopped AI Thread");
	}

	public void RandomMovement(Behaviours.NpcSpawner npcs)
	{
		BaseNavigator val = null;
		if (npcs.BOT is ScientistNPC)
		{
			var bOT = npcs.BOT;
			val = ((HumanNPC)(bOT is ScientistNPC ? bOT : null)).Brain.Navigator;
		}
		if (npcs.BOT is ScarecrowNPC)
		{
			BaseEntity bOT2 = npcs.BOT;
			val = ((ScarecrowNPC)((bOT2 is ScarecrowNPC) ? bOT2 : null)).Brain.Navigator;
		}
		if (!(val == null) && !val.Moving && !npcs.stationary)
		{
			Vector3 val2 = npcs.serializedNPCSpawner.position;
			var num = npcs.RoamDistance / 2f;
			Vector2 val3 = Vector3.zero;
			val3 = UnityEngine.Random.insideUnitCircle * num;
			val2 += new Vector3(val3.x, 0f, val3.y);
			val.SetDestination(val2, (BaseNavigator.NavigationSpeed)1, 0f, 0f);
		}
	}

	public void DressNPC(BasePlayer npc, NpcType npctype)
	{
		if (npc == null)
		{
			return;
		}
		switch (npctype)
		{
		case NpcType.Scientist:
			CreateCloths(npc, "hazmatsuit_scientist_arctic");
			switch ((int)UnityEngine.Random.Range(0f, 2f))
			{
			case 0:
				CreateGun(npc, "rifle.lr300", "weapon.mod.flashlight");
				break;
			case 1:
				CreateGun(npc, "rifle.ak", "weapon.mod.flashlight");
				break;
			case 2:
				CreateGun(npc, "pistol.m92", "weapon.mod.flashlight");
				break;
			}
			break;
		case NpcType.Peacekeeper:
			CreateCloths(npc, "hazmatsuit_scientist_peacekeeper");
			CreateGun(npc, "rifle.m39", "weapon.mod.flashlight");
			break;
		case NpcType.HeavyScientist:
			CreateCloths(npc, "scientistsuit_heavy");
			switch ((int)UnityEngine.Random.Range(0f, 2f))
			{
			case 0:
				CreateGun(npc, "lmg.m249", "weapon.mod.flashlight");
				break;
			case 1:
				CreateGun(npc, "shotgun.spas12", "weapon.mod.flashlight");
				break;
			case 2:
				CreateGun(npc, "rifle.lr300", "weapon.mod.flashlight");
				break;
			}
			break;
		case NpcType.JunkpileScientist:
			CreateCloths(npc, "hazmatsuit_scientist");
			CreateGun(npc, "pistol.m92", "weapon.mod.flashlight");
			break;
		case NpcType.Bandit:
			CreateCloths(npc, "attire.banditguard", 0uL);
			CreateGun(npc, "rifle.semiauto", "weapon.mod.flashlight");
			break;
		case NpcType.Murderer:
			CreateCloths(npc, "scarecrow.suit");
			CreateCloths(npc, "jacket");
			CreateGun(npc, "chainsaw");
			break;
		case NpcType.Scarecrow:
			CreateCloths(npc, "halloween.mummysuit");
			switch ((int)UnityEngine.Random.Range(0f, 2f))
			{
			case 0:
				CreateGun(npc, "pitchfork");
				break;
			case 1:
				CreateGun(npc, "sickle");
				break;
			case 2:
				CreateGun(npc, "bone.clubsickle");
				break;
			}
			break;
		}
	}

	public void CreateCloths(BasePlayer npc, string itemName, ulong skin = 0uL)
	{
		if (!(npc == null))
		{
			var val = ItemManager.CreateByName(itemName, 1, skin);
			if (val != null && !val.MoveToContainer(npc.inventory.containerWear, -1, false))
			{
				val.Remove();
			}
		}
	}

	public void CreateGun(BasePlayer npc, string itemName, string attachment = "", ulong skinid = 0uL)
	{
		var val = ItemManager.CreateByName(itemName, 1, skinid);
		if (val == null)
		{
			return;
		}
		if (!val.MoveToContainer(npc.inventory.containerBelt, -1, false))
		{
			val.Remove();
			return;
		}
		var heldEntity = val.GetHeldEntity();
		if (heldEntity != null && heldEntity is BaseProjectile && attachment != "")
		{
			var val2 = ItemManager.CreateByName(attachment);
			if (val2 != null && val.contents != null && !val2.MoveToContainer(val.contents))
			{
				val.contents.itemList.Add(val2);
			}
		}
		
		DoFixes(npc, val);
	}

	public void DoFixes(BasePlayer npc, Item item)
	{
		npc.Invoke((Action)delegate
		{
			if (npc != null && item != null)
			{
				npc.UpdateActiveItem(item.uid);
			}
		}, 2f);
		
		npc.Invoke((Action)delegate
		{
			if (npc != null)
			{
				var heldEntity = npc.GetHeldEntity();
				var val = (Chainsaw)(heldEntity is Chainsaw ? heldEntity : null);
				if (val != null)
				{
					val.SetFlagLocal((BaseEntity.Flags)2, true);
					val.SendNetworkUpdateImmediate();
				}
			}
		}, 4f);
	}

	public static bool IsOnGround(ref Vector3 position, out int areaMask)
	{
		if (NavMesh.SamplePosition(position, out var navMeshHit, 8f, -1))
		{
			areaMask = navMeshHit.mask;
			position = navMeshHit.position;
			
			return true;
		}
		
		areaMask = -1;
		
		return false;
	}

	private static byte[]? GetSerializedData(string dateName)
	{
		foreach (var map in World.Serialization.world.maps)
		{
			try
			{
				if (Encoding.ASCII.GetString(map.data).Contains(dateName))
				{
					return map.data;
				}
			}
			catch
			{
				//
			}
		}
		return null;
	}

	private static T? DeserializePlugin<T>(byte[] bytes)
	{
		try
		{
			using var stream = new MemoryStream(bytes);
			return (T)new XmlSerializer(typeof(T)).Deserialize(stream);
		}
		catch (Exception ex)
		{
			Debug.Log(ex.Message + " " + ex.StackTrace);
			return default;
		}
	}
}
