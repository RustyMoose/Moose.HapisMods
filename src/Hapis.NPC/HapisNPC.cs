using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

	// NPCs spawned by this mod, so the ground snap only touches our NPCs (never vanilla ones).
	private readonly HashSet<BaseEntity> _groundSnapNpcs = new();

	// Surfaces an NPC can be dropped onto: Default, Deployed, World, Construction and Terrain layers.
	private const int GroundSnapLayerMask = (1 << 0) | (1 << 8) | (1 << 16) | (1 << 21) | (1 << 23);


	// Start the downward check this far above the NPC's feet.
	private const float GroundSnapRayStart = 0.25f;

	// Never drop an NPC further than this, so it can't fall through a floor onto something below.
	private const float GroundSnapMaxDrop = 1f;

	// Per-prefab floor adjustments, read from FloorOffsetsFile at startup. Only lowers NPCs, by at most this many metres
	// (kept below GroundSnapRayStart so the floor check never starts underneath the floor).
	private const float FloorOffsetLimit = 0.2f;

	// Built-in floor adjustment for every floor (metres, negative = lower). Tuned on the official Hapis map:
	// Rust's collision floors sit a few cm above what players see, so NPCs are lowered 4 cm everywhere.
	// HapisNPC/FloorOffsets.txt is optional and only needed to override this for particular prefabs.
	private const float DefaultFloorOffset = -0.04f;


	private const string FloorFolder = "HapisNPC";

	private static readonly string FloorOffsetsFile = Path.Combine(FloorFolder, "FloorOffsets.txt");


	// Prefab name -> metres to move NPCs up (+) or down (-) when they stand on that prefab.
	private readonly Dictionary<string, float> _floorOffsets = new();

	// Which prefab each floor collider belongs to (looked up once per collider).
	private readonly Dictionary<Collider, string> _floorColliderPrefabs = new();



	public void InitializeHook()
	{
		LoadFloorOffsets();

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
						_groundSnapNpcs.Remove(npcs.BOT);
						npcs.Respawning = true;
						npcs.Delay = UnityEngine.Time.time + UnityEngine.Random.Range(npcs.serializedNPCSpawner.respawnMin, npcs.serializedNPCSpawner.respawnMax);
						continue;
					}
					_groundSnapNpcs.Add(npcs.BOT);
					SnapStandingNpc(npcs.BOT);
					ReturnHomeIfLost(npcs);
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
								WakeNavigator(scarecrowNpc.Brain.Navigator);
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
								WakeNavigator(val3.Brain.Navigator);
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
								WakeNavigator(val3.Brain.Navigator);
								(val3).Brain.Navigator.SetDestination((npcs.target).transform.position, (BaseNavigator.NavigationSpeed)2);
							}
							TryThrowGrenade(val3, npcs.target);
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

	// Called after Rust moves an NPC along the navmesh, and while it stands still. On custom prefabs the navmesh can sit above the real floor,
	// so drop our NPCs straight down onto the floor underneath them (only ever down, and at most GroundSnapMaxDrop).
	public void SnapToFloorHook(BaseNavigator navigator)
	{
		// Walking on the navmesh, or stopped/standing still (Rust switches to None when an NPC stops,
		// right before placing it at the navmesh height one last time).
		var navigationType = navigator.CurrentNavigationType;
		if (navigationType != BaseNavigator.NavigationType.NavMesh && navigationType != BaseNavigator.NavigationType.None)
		{
			return;
		}

		BaseEntity entity = navigator.BaseEntity;
		if (entity == null || !_groundSnapNpcs.Contains(entity))
		{
			return;
		}

		SnapEntityToFloor(entity);
	}

	// Puts an NPC's feet on the real floor under it: straight down only (never up), at most GroundSnapMaxDrop,
	// plus any extra lowering for that floor prefab from FloorOffsets.txt.
	// Used by the walking hook above, and by the AI loop for NPCs that are standing still.
	private void SnapEntityToFloor(BaseEntity entity)
	{
		var position = entity.ServerPosition;
		var start = position + Vector3.up * GroundSnapRayStart;
		var distance = GroundSnapRayStart + GroundSnapMaxDrop;
		if (UnityEngine.Physics.Raycast(start, Vector3.down, out var hit, distance, GroundSnapLayerMask, QueryTriggerInteraction.Ignore))
		{
			var prefabName = GetFloorPrefabName(hit.collider);

			// Only ever move NPCs DOWN, never up: if what we hit is above the feet, it isn't the floor.
			if (hit.point.y > position.y + 0.02f)
			{
				return;
			}

			// FloorOffsets.txt can lower NPCs a bit more on a particular prefab (negative numbers only).
			// Prefabs not listed use the "*" line, if there is one.
			if (!_floorOffsets.TryGetValue(prefabName, out var offset))
			{
				_floorOffsets.TryGetValue("*", out offset);
			}
			var targetY = hit.point.y + Mathf.Min(0f, offset);
			if (targetY < position.y - 0.02f)
			{
				entity.ServerPosition = new Vector3(position.x, targetY, position.z);
			}
			return;
		}
	}

	// Surfaces that can hold a door collider (Default, Deployed, World, Construction).
	private const int DoorCheckLayerMask = (1 << 0) | (1 << 8) | (1 << 16) | (1 << 21);

	// Reused buffer for the door check, so it doesn't allocate every movement tick.
	private static readonly RaycastHit[] DoorCheckHits = new RaycastHit[16];

	// Called right BEFORE Rust moves one of our NPCs. The navmesh on custom maps runs straight through doorways,
	// so NPCs walk through closed card room doors. If a closed door that NPCs can't open is in the way,
	// cancel this move and stop the NPC; the AI loop then sends it somewhere else.
	// Returns false to block the move.
	public bool DoorBlockHook(BaseNavigator navigator, Vector3 moveToPosition)
	{
		if (navigator.CurrentNavigationType == BaseNavigator.NavigationType.None)
		{
			return true;
		}

		BaseEntity entity = navigator.BaseEntity;
		if (entity == null || !_groundSnapNpcs.Contains(entity))
		{
			return true;
		}

		// Check the step it's about to take, at chest height, about a body wide, plus a little look-ahead.
		var from = entity.ServerPosition + Vector3.up;
		var to = moveToPosition + Vector3.up;
		var step = to - from;
		var distance = step.magnitude;
		if (distance < 0.001f)
		{
			return true;
		}

		var count = UnityEngine.Physics.SphereCastNonAlloc(from, 0.3f, step / distance, DoorCheckHits, distance + 0.5f, DoorCheckLayerMask, QueryTriggerInteraction.Ignore);
		for (var i = 0; i < count; i++)
		{
			var collider = DoorCheckHits[i].collider;
			if (collider == null)
			{
				continue;
			}

			var door = collider.GetComponentInParent<Door>();
			if (door != null && !door.IsOpen() && !door.IsNpcOpenable)
			{
				navigator.Stop();
				return false;
			}
		}

		return true;
	}

	// An NPC with no target that ends up further than this from its spawn point heads home
	// (its roam range, at least 20 m; roaming only ever picks spots within half the roam range).
	private const float LostMinDistance = 20f;

	// If a homeward NPC hasn't moved this many metres in LostTimeout seconds, it's stuck.
	private const float LostProgressDistance = 3f;
	private const float LostTimeout = 30f;

	// Sends NPCs that have wandered or chased too far back to their spawn point. If one is stuck and can't get
	// home, it's put back at its spawn point, but only while no player is near it or its spawn, so nobody sees it.
	private void ReturnHomeIfLost(Behaviours.NpcSpawner npcs)
	{
		var bot = npcs.BOT;
		var navigator = GetNavigator(bot);
		if (navigator == null || npcs.stationary || npcs.target != null)
		{
			// Fighting (or can't walk): not lost. Start the timer again next time it is.
			npcs.returningHome = false;
			npcs.lostSince = 0f;
			return;
		}

		var now = UnityEngine.Time.time;
		var position = bot.ServerPosition;
		Vector3 home = npcs.serializedNPCSpawner.position;
		var leash = Mathf.Max(npcs.RoamDistance, LostMinDistance);

		if (Vector3.Distance(position, home) <= leash)
		{
			npcs.returningHome = false;
			npcs.lostSince = 0f;
			return;
		}

		// Too far from home.
		if (!npcs.returningHome || npcs.lostSince <= 0f)
		{
			npcs.returningHome = true;
			npcs.lostSince = now;
			npcs.lostCheckPosition = position;
		}

		// Making progress? Then it isn't stuck: restart the stuck timer.
		if (Vector3.Distance(position, npcs.lostCheckPosition) > LostProgressDistance)
		{
			npcs.lostSince = now;
			npcs.lostCheckPosition = position;
		}

		// Stuck for too long: put it back at its spawn point, if nobody is around to see it.
		if (now - npcs.lostSince > LostTimeout)
		{
			if (!AnyConnectedPlayerNear(position, Config.SpawnBlockingDistance) && !AnyConnectedPlayerNear(home, Config.SpawnBlockingDistance))
			{
				PutBackAtSpawn(npcs, navigator, home);
			}
			return;
		}

		// Walk home.
		if (!navigator.Moving)
		{
			WakeNavigator(navigator);
			var destination = navigator.GetNearestNavmeshPosition(home, out var onNavmesh, 4f) ? onNavmesh : home;
			navigator.SetDestination(destination, BaseNavigator.NavigationSpeed.Normal, 0f, 0f);
		}
	}

	// Moves a stuck NPC straight back to its spawn point.
	private void PutBackAtSpawn(Behaviours.NpcSpawner npcs, BaseNavigator navigator, Vector3 home)
	{
		try
		{
			if (navigator.GetNearestNavmeshPosition(home, out var onNavmesh, 4f))
			{
				home = onNavmesh;
			}

			navigator.Stop();
			npcs.BOT.ServerPosition = home;
			if (navigator.Agent != null)
			{
				navigator.Agent.Warp(home);
			}
			SnapEntityToFloor(npcs.BOT);
			npcs.BOT.SendNetworkUpdateImmediate();
		}
		catch
		{
			// ignored
		}

		npcs.returningHome = false;
		npcs.lostSince = 0f;
	}

	// True if a real, connected, living player is within maxDist.
	private static bool AnyConnectedPlayerNear(Vector3 position, float maxDist)
	{
		var list = Facepunch.Pool.Get<List<BasePlayer>>();
		Vis.Entities(position, maxDist, list, 131072);
		var result = false;
		foreach (var player in list)
		{
			if (player != null && !player.IsNpc && player.IsConnected && player.IsAlive())
			{
				result = true;
				break;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref list);
		return result;
	}

	// The navigator (Rust's movement system) of one of our NPCs, or null.
	private static BaseNavigator? GetNavigator(BaseEntity entity)
	{
		if (entity is HumanNPC human && human.Brain != null)
		{
			return human.Brain.Navigator;
		}
		if (entity is ScarecrowNPC scarecrow && scarecrow.Brain != null)
		{
			return scarecrow.Brain.Navigator;
		}
		return null;
	}

	// NPCs standing still (for example at their spawn point) are never moved by Rust, so the walking hook never
	// corrects them. The AI loop calls this every tick to put standing NPCs on the floor too.
	private void SnapStandingNpc(BaseEntity entity)
	{
		var navigator = GetNavigator(entity);
		if (navigator == null || navigator.Moving)
		{
			return;
		}

		var before = entity.ServerPosition.y;
		SnapEntityToFloor(entity);

		// It isn't moving, so tell players' games about the new height straight away.
		if (Mathf.Abs(entity.ServerPosition.y - before) > 0.001f)
		{
			entity.SendNetworkUpdate();
		}
	}


	// Finds the prefab a floor collider belongs to: the nearest parent whose name is a prefab path ("assets/...").
	private string GetFloorPrefabName(Collider collider)
	{
		if (_floorColliderPrefabs.TryGetValue(collider, out var cached))
		{
			return cached;
		}

		string? name = null;
		for (var current = collider.transform; current != null; current = current.parent)
		{
			if (current.name.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
			{
				name = current.name;
				break;
			}
		}
		name ??= collider.transform.root.name;

		_floorColliderPrefabs[collider] = name;
		return name;
	}


	// Reads HapisNPC/FloorOffsets.txt (creating it with instructions if missing) and the list of prefabs already seen.
	private void LoadFloorOffsets()
	{
		_floorOffsets.Clear();

		// Built-in default for all floors, so the mod works with no files at all.
		_floorOffsets["*"] = DefaultFloorOffset;

		try
		{
			// Optional overrides. Nothing is created if the file isn't there.
			if (!File.Exists(FloorOffsetsFile))
			{
				Debug.LogWarning("[Hapis.NPC] Floor offsets: using built-in default (" + DefaultFloorOffset.ToString(CultureInfo.InvariantCulture) + " m on all floors).");
				return;
			}

			var overrides = 0;
			foreach (var raw in File.ReadAllLines(FloorOffsetsFile))
			{
				var line = raw;
				var comment = line.IndexOf('#');
				if (comment >= 0)
				{
					line = line.Substring(0, comment);
				}

				var equals = line.LastIndexOf('=');
				if (equals <= 0)
				{
					continue;
				}

				var name = line.Substring(0, equals).Trim();
				if (name.Length > 0 && float.TryParse(line.Substring(equals + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
				{
					_floorOffsets[name] = Mathf.Clamp(value, -FloorOffsetLimit, 0f);
					overrides++;
				}
			}

			Debug.LogWarning("[Hapis.NPC] Floor offsets: built-in default " + DefaultFloorOffset.ToString(CultureInfo.InvariantCulture) + " m, plus " + overrides + " line(s) from " + Path.GetFullPath(FloorOffsetsFile));
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[Hapis.NPC] Could not read floor offsets file, using built-in default: " + ex.Message);
		}
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
		if (!(val == null) && !val.Moving && !npcs.stationary && !npcs.returningHome)
		{
			WakeNavigator(val);

			var num = npcs.RoamDistance / 2f;
			for (var attempt = 0; attempt < 5; attempt++)
			{
				Vector3 val2 = npcs.serializedNPCSpawner.position;
				Vector2 val3 = UnityEngine.Random.insideUnitCircle * num;
				val2 += new Vector3(val3.x, 0f, val3.y);

				// Put the random spot onto Rust's walking surface, so Rust can actually plan a path there.
				// If that spot is no good (inside a wall, off the walkway...), try another one.
				if (!val.GetNearestNavmeshPosition(val2, out var onNavmesh, 4f))
				{
					continue;
				}
				if (val.SetDestination(onNavmesh, (BaseNavigator.NavigationSpeed)1, 0f, 0f))
				{
					break;
				}
			}
		}
	}

	// Rust switches an NPC's navmesh agent off whenever it stops, and then refuses new destinations
	// until it is switched back on. Switch it back on so our walk / chase / return-home orders are accepted.
	private static void WakeNavigator(BaseNavigator navigator)
	{
		try
		{
			if (navigator.Agent != null && !navigator.Agent.isOnNavMesh)
			{
				navigator.SetNavMeshEnabled(true);
			}
		}
		catch
		{
			// ignored
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
				CreateCloths(npc, "hazmatsuit_scientist");
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
				if (UnityEngine.Random.value < Config.ScientistGrenadeChance)
				{
					CreateGrenade(npc);
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

	// Puts one F1 grenade in the NPC's belt, after the gun (Rust re-equips belt slot 0 after a throw,
	// so the gun must stay in slot 0). Not equipped: the NPC keeps holding its gun until it throws.
	public void CreateGrenade(BasePlayer npc)
	{
		if (npc == null)
		{
			return;
		}

		var grenade = ItemManager.CreateByName("grenade.f1", 1, 0uL);
		if (grenade != null && !grenade.MoveToContainer(npc.inventory.containerBelt, -1, false))
		{
			grenade.Remove();
		}
	}

	// Asks Rust's own NPC throw to throw the NPC's grenade at its target. Rust only throws if the target is
	// 2-20 m away and visible, and the grenade is used up, so each NPC throws at most once per life.
	private static void TryThrowGrenade(ScientistNPC npc, BaseEntity target)
	{
		try
		{
			if (npc == null || target == null || !(target is BasePlayer player) || player.IsNpc || !player.IsAlive())
			{
				return;
			}

			var grenade = npc.FindThrownWeapon();
			if (grenade == null)
			{
				return;
			}

			npc.TryUseThrownWeapon(grenade, target, 0f);
		}
		catch
		{
			// ignored
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