using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using ConVar;
using Facepunch;
using Moose.Hapis.Deployables.Behaviours;
using Moose.Hapis.Deployables.Models;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Moose.Hapis.Deployables;

public class HapisDeployables
{
	public static HapisDeployables Instance;

	private bool _loaded;

	private SerializedVehicleData? _serializedVehicleData;

	private SerializedVendingContainerData? _serializedVendingContainerData;

	private SerializedLootableContainerData? _serializedLootableContainerData;

	private readonly List<CustomVendingMachines> _customVendingMachine = [];

	private readonly List<CustomLootContainer> _customLootContainer = [];

	private readonly List<LootSpawnerHandler> _lootSpawnerHandlers = [];

	private readonly List<SpawnerHandler> _spawners = [];

	private readonly List<PrefabData> _animalsSpawners = [];

	public readonly List<BaseEntity> CoalingTower = [];

	private readonly List<PrefabData> _spawnPoints = [];

	private readonly List<TakenV> _takenVehicles = [];

	private readonly List<PrefabData> _lootProfiles = [];

	private Coroutine? _prefabSpawnerThread;

	public void InitializeHook()
	{
		try
		{
			var num = 0;
			var list = _spawners.SelectMany(spawner => spawner.ManagedPrefabIDs()).ToList();
			var array = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
			
			foreach (var baseEntity in array)
			{
				if (!list.Contains(baseEntity.prefabID))
				{
					continue;
				}
				
				foreach (var spawner2 in _spawners)
				{
					if (spawner2.ManagedPrefabIDs().Contains(baseEntity.prefabID) && spawner2.DistanceFromSpawn(baseEntity.transform.position) < 0.5f)
					{
						baseEntity.Kill();
						num++;
					}
				}
			}
			
			Debug.LogWarning($"[Hapis.Deployables] Killed {num} existing keycard spawner managed entities");
		}
		catch
		{
			Debug.LogError("[Hapis.Deployables] Failed to Kill existing keycard spawner managed entities");
		}
		
		try
		{
			var num2 = 0;
			var array = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
			
			foreach (var entity in array)
			{
				if (_lootSpawnerHandlers.Find(handler => Vector3.Distance(handler.position, entity.transform.position) < 1f && handler.gameObjectName == entity.gameObject.name) != null)
				{
					entity.Kill();
					num2++;
				}
			}
			Debug.LogWarning($"[Hapis.Deployables] Killed {num2} existing loot spawner managed entities.");
		}
		catch
		{
			//
		}
		
		try
		{
			if (_spawnPoints.Count != 0)
			{
				Debug.LogWarning("[Hapis.Deployables] Creating " + _spawnPoints.Count + " Managed Prefabs.");
				_prefabSpawnerThread ??= SingletonComponent<ServerMgr>.Instance.StartCoroutine(SpawnManagerPrefabs());
			}
		}
		catch
		{
			//
		}
		
		try
		{
			Debug.LogWarning("[Hapis.Deployables] Creating " + _spawners.Count + " Keycard Spawners.");
			foreach (var spawner3 in _spawners)
			{
				spawner3.CancelInvoke("SetSpawn");
				spawner3.InitializeFirstEntity();
			}
		}
		catch
		{
			Debug.LogError("[Hapis.Deployables] Failed To Create Spawners!");
		}
		try
		{
			if (_customVendingMachine.Count != 0)
			{
				for (var num3 = _customVendingMachine.Count - 1; num3 >= 0; num3--)
				{
					var vendingMachine = _customVendingMachine[num3];
					StockVending(vendingMachine);
				}
				
				Debug.LogWarning("[Hapis.Deployables] Stocked " + _customVendingMachine.Count + " Custom Vending Machines.");
			}
		}
		catch
		{
			Debug.LogError("[Hapis.Deployables] Failed To Stock Vending Machines!");
		}
		try
		{
			Debug.LogWarning("[Hapis.Deployables] Creating " + _lootSpawnerHandlers.Count + " Loot Spawner Managers.");
			var array2 = UnityEngine.Object.FindObjectsOfType<LootSpawnerHandler>();
			foreach (var obj6 in array2)
			{
				obj6.CancelInvoke("RespawnLootbox");
				obj6.InitializeFirstEntity();
			}
		}
		catch
		{
			Debug.LogError("[Hapis.Deployables] Failed To Create Managed Loot Spawners!");
		}
		try
		{
			if (_customLootContainer.Count != 0)
			{
				Debug.LogWarning("[Hapis.Deployables] Filling " + _customLootContainer.Count + " Custom LootableContainers.");
			}
		}
		catch
		{
			Debug.LogError("[Hapis.Deployables] Failed To Fill Loot Containers!");
		}
		try
		{
			if (_animalsSpawners.Count != 0)
			{
				foreach (var animalsSpawner in _animalsSpawners)
				{
					try
					{
						var baseEntity2 = GameManager.server.CreateEntity(StringPool.Get(animalsSpawner.id), animalsSpawner.position, animalsSpawner.rotation, startActive: false);
						if ((bool)baseEntity2)
						{
							baseEntity2.enableSaving = false;
							baseEntity2.gameObject.AwakeFromInstantiate();
							baseEntity2.Spawn();
							var animalSpawner = baseEntity2.gameObject.AddComponent<AnimalSpawner>();
							animalSpawner.PD = animalsSpawner;
							animalSpawner.Lifetime = UnityEngine.Time.time + Config.AnimalsMinRespawnDelay;
							if (Config.LogManagedSpawns)
							{
								Debug.LogWarning("[Hapis.Deployables] Spawned " + baseEntity2 + " @ " + baseEntity2.transform.position);
							}
						}
					}
					catch
					{
						Debug.LogError("[Hapis.Deployables] Failed To Set Up " + StringPool.Get(animalsSpawner.id) + " Animal Spawner!");
					}
				}
				
				Debug.LogWarning("[Hapis.Deployables] Setting up " + _animalsSpawners.Count + " Animal Spawners.");
			}
		}
		catch
		{
			Debug.LogError("[Hapis.Deployables] Failed To Set Up Animal Spawners!");
		}
	}

	public static bool KillHook(BaseNetworkable baseNetworkable)
	{
		if (baseNetworkable == null)
		{
			return true;
		}

		return baseNetworkable switch
		{
			SprayCanSpray_Decal decal => !decal.HasFlag(BaseEntity.Flags.Placeholder),
			Signage signage => !signage.HasFlag(BaseEntity.Flags.Placeholder),
			_ => true
		};
	}

	public bool SayAsHandler(BasePlayer player, string message, Chat.ChatChannel _)
	{
		if (!player.IsAdmin)
		{
			return true;
		}
		
		switch (message.ToLower())
		{
		case "/showanimalspawns":
		case "//showanimalspawns":
			foreach (var animalsSpawner in _animalsSpawners)
			{
				var vector2 = new Vector3(animalsSpawner.position.x, animalsSpawner.position.y, animalsSpawner.position.z);
				var source2 = StringPool.Get(animalsSpawner.id).Split(['/']);
				player.SendConsoleCommand("ddraw.sphere", 10f, Color.white, vector2, 1f);
				player.SendConsoleCommand("ddraw.text", 10f, Color.white, vector2, "<size=25>" + source2.Last() + "</size>");
			}
			player.ChatMessage("Showing Animal Spawn Points.");
			return false;
		case "/showlootspawns":
		case "//showlootspawns":
			foreach (var item in _customLootContainer)
			{
				player.SendConsoleCommand("ddraw.sphere", 10f, Color.white, item.position, 1f);
				player.SendConsoleCommand("ddraw.text", 10f, Color.white, item.position, "<size=25>" + item.lootableContainerData.filename + "</size>");
			}
			player.ChatMessage("Showing Custom Loot Spawn Points.");
			return false;
		case "/showvendingspawns":
		case "//showvendingspawns":
			foreach (var item2 in _customVendingMachine)
			{
				player.SendConsoleCommand("ddraw.sphere", 10f, Color.white, item2.position, 1f);
				player.SendConsoleCommand("ddraw.text", 10f, Color.white, item2.position, "<size=25>" + item2.vendingContainerData.filename + "</size>");
			}
			player.ChatMessage("Showing Custom Vending Spawn Points.");
			return false;
		case "/showentityspawns":
		case "//showentityspawns":
			foreach (var spawnPoint in _spawnPoints)
			{
				var vector = new Vector3(spawnPoint.position.x, spawnPoint.position.y, spawnPoint.position.z);
				var source = StringPool.Get(spawnPoint.id).Split(['/']);
				player.SendConsoleCommand("ddraw.sphere", 10f, Color.white, vector, 1f);
				player.SendConsoleCommand("ddraw.text", 10f, Color.white, vector, "<size=25>" + source.Last() + "</size>");
			}
			player.ChatMessage("Showing Managed Entity Spawn Points.");
			return false;
		default:
			return true;
		}
	}

	public void BuildPrefabList()
	{
		var num = 0;
		
		if (_serializedVehicleData == null)
		{
			var array = GetSerializedData("SerializedVehicleData");
			if (array != null)
			{
				_serializedVehicleData = DeserializePlugin<SerializedVehicleData>(array);
			}
		}
		if (_serializedVehicleData != null)
		{
			_spawnPoints.AddRange(_serializedVehicleData.vehicles);
			num = _serializedVehicleData.vehicles.Count;
		}
		for (var i = 0; i < World.Serialization.world.prefabs.Count; i++)
		{
			var text = StringPool.Get(World.Serialization.world.prefabs[i].id);
			if (Config.ManagedPrefabs.Contains(text))
			{
				_spawnPoints.Add(World.Serialization.world.prefabs[i]);
				if (text.Contains("vehicles"))
				{
					num++;
				}
			}
		}
		Debug.LogWarning("[Hapis.Deployables] Managed Vehicles: " + num);
		Debug.LogWarning("[Hapis.Deployables] Managed Prefabs: " + _spawnPoints.Count);
		try
		{
			if (_serializedVendingContainerData == null)
			{
				var array2 = GetSerializedData("SerializedVendingContainerData");
				if (array2 != null)
				{
					_serializedVendingContainerData = DeserializePlugin<SerializedVendingContainerData>(array2);
				}
			}
			Debug.LogWarning("[Hapis.Deployables] Managed Vending Machines: " + _serializedVendingContainerData.entities.Count);
		}
		catch
		{
			Debug.LogError("[Hapis.Deployables] No Custom vending profiles found");
		}
		try
		{
			if (_serializedLootableContainerData == null)
			{
				var array3 = GetSerializedData("SerializedLootableContainerData");
				if (array3 != null)
				{
					_serializedLootableContainerData = DeserializePlugin<SerializedLootableContainerData>(array3);
				}
			}
			Debug.LogWarning("[Hapis.Deployables] Managed Loot Profiles: " + _serializedLootableContainerData.entities.Count);
		}
		catch
		{
			Debug.LogError("[Hapis.Deployables] No Custom loot profiles found");
		}
	}

	private static string GetContainerInfo(string cat)
	{
		if (string.IsNullOrEmpty(cat))
		{
			return string.Empty;
		}
		
		var array = cat.Split([':']);
		return array.Length <= 2 ? string.Empty : array[3];
	}

	private static byte[]? GetSerializedData(string data)
	{
		foreach (var map in World.Serialization.world.maps)
		{
			try
			{
				if (Encoding.ASCII.GetString(map.data).Contains(data))
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

	private static int SumbEntity(SpawnGroup.SpawnEntry spawnEntry)
	{
		return spawnEntry.weight;
	}

	public string? GetPrefabName(SpawnGroup spawnGroup)
	{
		float num = spawnGroup.prefabs.Sum((Func<SpawnGroup.SpawnEntry, int>)SumbEntity);
		if (num == 0f)
		{
			return null;
		}
		
		var num2 = UnityEngine.Random.Range(0f, num);
		
		foreach (var prefab in spawnGroup.prefabs)
		{
			if ((num2 -= prefab.weight) <= 0f)
			{
				return prefab.prefab.resourcePath;
			}
		}
		
		return spawnGroup.prefabs[spawnGroup.prefabs.Count - 1].prefab.resourcePath;
	}

	public bool ProtectedHook(BaseCombatEntity baseCombatEntity, HitInfo? info = null)
	{
		if (baseCombatEntity == null)
		{
			return true;
		}
		if (Config.ManagedPrefabs.Contains(baseCombatEntity.PrefabName))
		{
			if (Config.DisableDamageLikeRe)
			{
				return false;
			}
			
			if (info == null)
			{
				if (_spawnPoints.Any(prefabData => baseCombatEntity.transform.position == (Vector3)prefabData.position))
				{
					return false;
				}
				
				return true;
			}
			
			if (info.damageTypes.GetMajorityDamageType() == DamageType.Decay || baseCombatEntity.HasFlag(BaseEntity.Flags.Placeholder))
			{
				return false;
			}
		}
		return true;
	}

	public void WorldSpawnHook(GameObject gameObject)
	{
		if (gameObject == null)
		{
			return;
		}
		
		if (Config.ManagedSpawners.Contains(gameObject.name))
		{
#if DEBUG
			Debug.Log("WorldSpawn Managed Spawner Found: " + gameObject.name);
#endif
			
			foreach (var child in gameObject.transform.GetAllChildren())
			{
				var spawnGroup = child.GetComponent<SpawnGroup>();
				if (spawnGroup == null)
				{
					continue;
				}
#if DEBUG
				Debug.Log("SpawnGroup Found for: " + child.name);
#endif
				
				if (spawnGroup.WantsTimedSpawn())
				{
					spawnGroup.respawnDelayMax = float.PositiveInfinity;
				}
					
				_spawners.Add(spawnGroup.gameObject.AddComponent<SpawnerHandler>());
			}
#if DEBUG
			Debug.Log("Leaving SpawnHandler creator.");
#endif
		}
		
		if (Config.Ch47LandingZoneLocations.Contains(gameObject.name))
		{
			Debug.LogWarning("[Hapis.Deployables] CH47 Landing Zone Detected!: " + gameObject.transform.position);
			gameObject.AddComponent<CH47LandingZone>();
		}
		
		var component2 = gameObject.GetComponent<NPCVendingMachine>();
		if (component2 != null)
		{
			foreach (var pd2 in _lootProfiles)
			{
				if ((Vector3)pd2.position != component2.transform.position)
				{
					continue;
				}
				
				var vendingContainerData = _serializedVendingContainerData?.entities.Find(x => x.filename == GetContainerInfo(pd2.category));
				if (vendingContainerData == null)
				{
					continue;
				}
				
				_customVendingMachine.Add(new CustomVendingMachines(component2, vendingContainerData));
				component2.enableSaving = false;
				
				if (BaseEntity.saveList.Contains(component2))
				{
					BaseEntity.saveList.Remove(component2);
				}
				
				return;
			}
		}
		
		var lootContainer = gameObject.GetComponent<LootContainer>();
		if (lootContainer == null)
		{
			return;
		}
		
		var flag = false;
		var respawnRateMin = 1;
		var respawnRateMax = 1;
		
		foreach (var pd in _lootProfiles)
		{
			if ((Vector3)pd.position == lootContainer.transform.position)
			{
				var lootableContainerData = _serializedLootableContainerData.entities.Find(x => x.filename == GetContainerInfo(pd.category));
				if (lootableContainerData == null)
				{
					_customLootContainer.Add(new CustomLootContainer(lootContainer, null));
				}
				else
				{
					_customLootContainer.Add(new CustomLootContainer(lootContainer, lootableContainerData));
				}
				
				flag = true;
				respawnRateMin = lootableContainerData.respawnRateMin;
				respawnRateMax = lootableContainerData.respawnRateMax;
			}
		}
		foreach (var managedLootContainer in Config.ManagedLootContainers)
		{
			if (string.Equals(gameObject.name, managedLootContainer.gameobjectName))
			{
				var position = gameObject.transform.position;
				var rotation = gameObject.transform.rotation;
				if (!flag)
				{
					respawnRateMin = managedLootContainer.respawnTimeMin;
					respawnRateMax = managedLootContainer.respawnTimeMax;
				}
				var lootSpawnerHandler = new GameObject("CoreCustomLootSpawnerHandler").AddComponent<LootSpawnerHandler>();
				lootSpawnerHandler.respawnRateMin = respawnRateMin;
				lootSpawnerHandler.respawnRateMax = respawnRateMax;
				lootSpawnerHandler.gameObjectName = managedLootContainer.gameobjectName;
				lootSpawnerHandler.position = position;
				lootSpawnerHandler.rotation = rotation;
				_lootSpawnerHandlers.Add(lootSpawnerHandler);
				break;
			}
		}
	}

	public void StartHackingHook(HackableLockedCrate hackableCrate)
	{
		var alarmsound = GameManager.server.CreateEntity("assets/prefabs/io/electric/other/alarmsound.prefab", hackableCrate.transform.position + new Vector3(0f, 2f, 0f)) as IOEntity;
		alarmsound.enableSaving = false;
		alarmsound.Spawn();
		alarmsound.Invoke(delegate
		{
			alarmsound.Kill();
		}, 75f);
		if (Config.EnableReinforcementAlarm)
		{
			alarmsound.UpdateFromInput(100, 0);
		}
	}

	public void SetLandingTargetHook(CH47HelicopterAIController aiController, Vector3 target)
	{
		if (!(aiController != null))
		{
			return;
		}
		aiController.Invoke(delegate
		{
			if (!(aiController == null))
			{
				if (Vector3.Distance(target, aiController.transform.position) < 45f)
				{
					aiController.SetMinHoverHeight(-5f);
				}
				else
				{
					SetLandingTargetHook(aiController, target);
				}
			}
		}, 10f);
	}

	private void ClearContainer(LootContainer lootContainer)
	{
		if (lootContainer == null)
		{
			return;
		}
		
		for (var i = 0; i < 100; i++)
		{
			var inventory = lootContainer.inventory;
			if (inventory is { itemList.Count: > 0 })
			{
				var item = lootContainer.inventory.itemList[0];
				if (item != null)
				{
					item.RemoveFromContainer();
					item.Remove();
				}
				
				continue;
			}
			
			break;
		}
	}

	private void ContainerRespawner(LootContainer lootContainer, LootableContainerData lootableContainerData)
	{
		if (lootContainer != null && !lootContainer.IsDestroyed)
		{
			ClearContainer(lootContainer);
			AddContainerItems(lootContainer, lootableContainerData);
		}
	}

	private void AddContainerItems(LootContainer lootContainer, LootableContainerData lootableContainerData)
	{
		if (lootContainer == null || lootContainer.IsDestroyed)
		{
			return;
		}
		lootContainer.Invoke(delegate
		{
			ContainerRespawner(lootContainer, lootableContainerData);
		}, Mathf.Clamp(UnityEngine.Random.Range(lootableContainerData.refreshRateMin, lootableContainerData.refreshRateMax) * 60, 60, int.MaxValue));
		var num = UnityEngine.Random.Range(lootableContainerData.spawnAmountMin, lootableContainerData.spawnAmountMax);
		lootContainer.inventory.ServerInitialize(null, num);
		lootContainer.inventory.GiveUID();
		var list = new List<LootableItemData>(lootableContainerData.items);
		
		for (var i = 0; i < num; i++)
		{
			var lootableItemData = list.GetRandom();
			if (lootableItemData == null)
			{
				continue;
			}
			
			Item item;
			if (!lootableItemData.blueprint)
			{
				item = ItemManager.CreateByName(lootableItemData.shortname, Mathf.Clamp(UnityEngine.Random.Range(lootableItemData.minimum, lootableItemData.maximum), 1, int.MaxValue));
			}
			else
			{
				item = ItemManager.CreateByName("blueprintbase", Mathf.Clamp(UnityEngine.Random.Range(lootableItemData.minimum, lootableItemData.maximum), 1, int.MaxValue));
				var itemDefinition = ItemManager.itemList.Find(x => x.shortname == lootableItemData.shortname);
				item.blueprintTarget = itemDefinition != null ? itemDefinition.itemid : 0;
			}
			item?.MoveToContainer(lootContainer.inventory);
			list.Remove(lootableItemData);
		}
	}

	private void StockVending(CustomVendingMachines customVendingMachines)
	{
		var npcVendingMachine = customVendingMachines.npcVendingMachine;
		if (npcVendingMachine == null)
		{
			npcVendingMachine = Create<NPCVendingMachine>(customVendingMachines.prefabName, customVendingMachines.position, customVendingMachines.rotation);
			if (npcVendingMachine != null)
			{
				npcVendingMachine.enableSaving = false;
				npcVendingMachine.Spawn();
				customVendingMachines.npcVendingMachine = npcVendingMachine;
			}
		}
		
		var list = new List<VendingItemData>(customVendingMachines.vendingContainerData.items);
		var num = list.Count < 7 ? list.Count : 7;
		var list2 = new List<NPCVendingOrder.Entry>();
		var array = new float[num];
		for (var i = 0; i < num; i++)
		{
			var random = list.GetRandom();
			if (random != null)
			{
				list.Remove(random);
				var item = new NPCVendingOrder.Entry
				{
					sellItem = ItemManager.FindItemDefinition(random.sellItemShortname),
					sellItemAmount = random.sellItemAmount,
					sellItemAsBP = random.sellItemBlueprint,
					currencyItem = ItemManager.FindItemDefinition(random.currencyItemShortname),
					currencyAmount = random.currencyItemAmount,
					currencyAsBP = random.currencyItemBlueprint,
					refillAmount = 1,
					refillDelay = 10f
				};
				array[i] = UnityEngine.Time.realtimeSinceStartup + 10f;
				list2.Add(item);
			}
		}

		if (npcVendingMachine != null)
		{
			npcVendingMachine.vendingOrders = ScriptableObject.CreateInstance<NPCVendingOrder>();
			npcVendingMachine.vendingOrders.orders = list2.ToArray();
			npcVendingMachine.refillTimes = array;
			npcVendingMachine.InstallFromVendingOrders();
		}
	}

	private static T? Create<T>(string prefabName, Vector3 position, Quaternion rotation) where T : Component
	{
		var gameObject = GameManager.server.FindPrefab(prefabName);
		if (gameObject == null)
		{
			return null;
		}
		
		gameObject = Instantiate.GameObject(gameObject, position, rotation);
		if (gameObject == null)
		{
			return null;
		}
		
		gameObject.name = prefabName;
		SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);
		UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());
			
		if (!gameObject.activeSelf)
		{
			gameObject.SetActive(value: true);
		}
			
		return gameObject.GetComponent<T>();
	}

	public void EntitySpawned(BaseNetworkable baseNetworkable)
	{
		if (baseNetworkable == null || baseNetworkable is not LootContainer lootContainer)
		{
			return;
		}

		if (lootContainer == null)
		{
			return;
		}
		if (Config.LockedCrates.Contains(lootContainer.PrefabName))
		{
			lootContainer.InitializeHealth(Config.LockedCratesHealth, Config.LockedCratesHealth);
			lootContainer.SetFlag(BaseEntity.Flags.Locked, b: true);
		}
		
		foreach (var item in _customLootContainer)
		{
			if (item != null && item.position == lootContainer.transform.position)
			{
				ContainerRespawner(lootContainer, item.lootableContainerData);
				break;
			}
		}
	}

	public bool SpawnHook(PrefabData prefab)
	{
		if (!_loaded)
		{
			_loaded = true;
			BuildPrefabList();
		}
		if (Config.RemovePrefabsLocations.Contains(prefab.position))
		{
			return false;
		}
		if (!string.IsNullOrEmpty(GetContainerInfo(prefab.category)))
		{
			_lootProfiles.Add(prefab);
		}
		switch (prefab.id)
		{
		case 273775677u:
			return false;
		case 2047788867u:
			return false;
		case 2138552575u:
			return false;
		case 612480793u:
			return false;
		case 1470785358u:
			return false;
		case 382138391u:
			return false;
		case 1549305686u:
			return false;
		case 2780689094u:
			return false;
		case 3542189355u:
			return false;
		case 541826828u:
			return false;
		case 3185889399u:
			return false;
		case 1888107197u:
			return false;
		case 3078064982u:
			return false;
		case 749308997u:
			_animalsSpawners.Add(prefab);
			return false;
		case 1799741974u:
			_animalsSpawners.Add(prefab);
			return false;
		case 1378621008u:
			_animalsSpawners.Add(prefab);
			return false;
		case 502341109u:
			_animalsSpawners.Add(prefab);
			return false;
		case 2144238755u:
			_animalsSpawners.Add(prefab);
			return false;
		case 152398164u:
			_animalsSpawners.Add(prefab);
			return false;
		case 2628005754u:
		case 3168507223u:
		case 3591916872u:
		case 3919686896u:
		case 708840119u:
			return false;
		default:
			return true;
		}
	}

	public bool CreateMapMarkerHook(CH47Helicopter __instance)
	{
		if (__instance != null && _serializedVehicleData != null)
		{
			foreach (var vehicle in _serializedVehicleData.vehicles)
			{
				if (__instance.transform.position == (Vector3)vehicle.position)
				{
					return false;
				}
			}
		}
		return true;
	}

	private static BaseEntity? ScanEntityList(Vector3 pos, uint id)
	{
		foreach (var serverEntity in BaseNetworkable.serverEntities)
		{
			if (id == 0 && serverEntity.transform.position == pos || serverEntity.transform.position == pos && serverEntity.prefabID == id)
			{
				return serverEntity as BaseEntity;
			}
		}
		
		return null;
	}

	private static BaseEntity? FindEntity(Vector3 pos, float radius, uint id)
	{
		var obj = Facepunch.Pool.Get<List<BaseNetworkable>>();
		Vis.Entities(pos, radius, obj);
		foreach (var item in obj)
		{
			if (item.prefabID == id)
			{
				try
				{
					return item as BaseEntity;
				}
				catch
				{
					//
				}
			}
		}
		
		Facepunch.Pool.FreeUnmanaged(ref obj);
		
		return null;
	}

	private void PairHangerDoor(Door door)
	{
		if (door.HasFlag(BaseEntity.Flags.Reserved11))
		{
			return;
		}
		List<DoorManipulator> list = new List<DoorManipulator>();
		Vis.Entities(door.transform.position, Config.HangerDoorScanRange, list);
		if (list != null && list.Count != 0)
		{
			list[0].SetTargetDoor(door);
			if (Config.HideDoorManipulators)
			{
				list[0]._limitedNetworking = true;
			}
			door.SetFlag(BaseEntity.Flags.Reserved11, b: true);
		}
	}

	private void PairDoor(Door door)
	{
		if (door.HasFlag(BaseEntity.Flags.Reserved11))
		{
			return;
		}
		
		var list = new List<DoorManipulator>();
		Vis.Entities(door.transform.position, 2f, list);
		
		if (list != null && list.Count != 0)
		{
			list[0].SetTargetDoor(door);
			
			if (Config.HideDoorManipulators)
			{
				list[0]._limitedNetworking = true;
			}
			
			door.SetFlag(BaseEntity.Flags.Locked, b: true);
			door.SetFlag(BaseEntity.Flags.Reserved11, b: true);
		}
	}

	private IEnumerator SpawnManagerPrefabs()
	{
		var hangerDoorsDone = false;
		var foundCoalings = false;
		var doneSigns = false;
		
		while (_spawnPoints != null && _spawnPoints.Count != 0)
		{
			foreach (var spawnPoint in _spawnPoints)
			{
				try
				{
					var flag = true;
					var text = StringPool.Get(spawnPoint.id);
					
					BaseEntity baseEntity = null;
					
					if (text == null)
					{
						goto IL_01cb;
					}
					
					if (text.Contains("coaling_tower_mechanism"))
					{
						flag = false;
						if (!foundCoalings)
						{
							baseEntity = ScanEntityList(spawnPoint.position, spawnPoint.id);
							if (baseEntity != null)
							{
								CoalingTower.Add(baseEntity);
							}
						}
					}
					else if (text.Contains("door.hinged.arctic.garage"))
					{
						flag = false;
						if (!hangerDoorsDone)
						{
							baseEntity = ScanEntityList(spawnPoint.position, spawnPoint.id);
							PairHangerDoor(baseEntity as Door);
						}
					}
					else if (text.Contains("sign."))
					{
						flag = false;
						if (!doneSigns)
						{
							baseEntity = ScanEntityList(spawnPoint.position, spawnPoint.id);
						}
					}
					else if (text.Contains("vehicles") || text.Contains("ch47.entity"))
					{
						baseEntity = FindEntity(spawnPoint.position, 8f, spawnPoint.id);
					}
					else
					{
						if (!text.Contains("boogieboard.deployed"))
						{
							goto IL_01cb;
						}
						baseEntity = FindEntity(spawnPoint.position, 4f, spawnPoint.id);
					}
					goto IL_01e4;
					IL_054b:
					if (baseEntity != null)
					{
						if (Config.EnableMapSkins && baseEntity.skinID == 0L && spawnPoint.category.Contains("skinid="))
						{
							try
							{
								var s = spawnPoint.category.Split(new string[1] { "skinid=" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(new char[1] { ':' })[0];
								baseEntity.skinID = ulong.Parse(s);
								baseEntity.SendNetworkUpdateImmediate();
								if (baseEntity is SprayCanSpray_Decal)
								{
									baseEntity.SetFlag(BaseEntity.Flags.Placeholder, b: true);
								}
							}
							catch
							{
							}
						}
						
						if (Config.FillSwimmingPools && baseEntity is PaddlingPool && !baseEntity.HasFlag(BaseEntity.Flags.Reserved4))
						{
							var item = ItemManager.CreateByItemID(-1779180711, 2000);
							if (item != null)
							{
								item.info.itemType = ItemContainer.ContentsType.Liquid;
								if (item.MoveToContainer((baseEntity as PaddlingPool).inventory))
								{
									baseEntity.SetFlag(BaseEntity.Flags.Reserved4, b: true);
									baseEntity.SetFlag(BaseEntity.Flags.Locked, b: true);
								}
							}
						}
						
						if (baseEntity is BaseCombatEntity baseCombatEntity && baseCombatEntity != null)
						{
							if (baseEntity.PrefabName.Contains("vehicles") || baseEntity.PrefabName.Contains("ch47.entity"))
							{
								if (Config.DisableDamageToVehicles)
								{
									baseCombatEntity.SetFlag(BaseEntity.Flags.Placeholder, b: true);
								}
							}
							else
							{
								baseCombatEntity.SetFlag(BaseEntity.Flags.Placeholder, b: true);
							}
							baseCombatEntity.pickup.enabled = false;
							if (baseCombatEntity.Health() < baseCombatEntity.MaxHealth())
							{
								baseCombatEntity.health += 5f;
								if (baseCombatEntity.Health() > baseCombatEntity.MaxHealth())
								{
									baseCombatEntity.health = baseCombatEntity.MaxHealth();
								}
							}
						}
					}
					goto IL_0766;
					IL_01e4:
					if (!(baseEntity == null))
					{
						goto IL_054b;
					}
					try
					{
						if (text.Contains("cassetterecorder"))
						{
							continue;
						}
						if (!AnyPlayersNearby(spawnPoint.position, Config.PlayerDistanceBlock))
						{
							foreach (var item2 in _takenVehicles.ToList())
							{
								if (item2.baseent == null || item2.baseent.IsDestroyed)
								{
									_takenVehicles.Remove(item2);
								}
								else if (item2.spawndata == spawnPoint)
								{
									flag = false;
									break;
								}
							}
							if (!flag)
							{
								continue;
							}
							baseEntity = GameManager.server.CreateEntity(text, spawnPoint.position, Quaternion.Euler(spawnPoint.rotation));
							if (baseEntity != null)
							{
								baseEntity.Spawn();
								if (Config.LogManagedSpawns)
								{
									Debug.LogWarning("[Hapis.Deployables] Spawned " + baseEntity.ToString() + "@" + baseEntity.transform.position.ToString());
								}
								if (baseEntity is StabilityEntity)
								{
									(baseEntity as StabilityEntity).grounded = true;
								}
								UnityEngine.Object.Destroy(baseEntity.GetComponent<GroundWatch>());
								if (baseEntity.PrefabName.Contains("vehicles") || baseEntity.PrefabName.Contains("ch47.entity"))
								{
									Debug.Log("BE VEHICLE was found null anyway " + baseEntity.PrefabName);
									var takenV = new TakenV();
									takenV.baseent = baseEntity;
									takenV.spawndata = spawnPoint;
									_takenVehicles.Add(takenV);
								}
								if (baseEntity is Door)
								{
									var door = baseEntity as Door;
									door.canTakeLock = false;
									door.Invoke(delegate
									{
										if (door != null)
										{
											PairDoor(door);
										}
									}, 5f);
								}
								if (baseEntity.gameObject.GetComponent<NavMeshObstacle>() == null)
								{
									baseEntity.gameObject.AddComponent<NavMeshObstacle>().carving = true;
								}
							}
							goto IL_054b;
						}
						goto IL_054b;
					}
					catch
					{
						goto IL_054b;
					}
					IL_01cb:
					baseEntity = ScanEntityList(spawnPoint.position, spawnPoint.id);
					goto IL_01e4;
				}
				catch
				{
					goto IL_0766;
				}
				IL_0766:
				yield return CoroutineEx.waitForSeconds(0.02f);
			}
			hangerDoorsDone = true;
			foundCoalings = true;
			doneSigns = true;
			yield return CoroutineEx.waitForSeconds(Config.RecheckerSeconds);
		}
	}

	public static bool AnyPlayersNearby(Vector3 position, float maxDist)
	{
		var obj = Facepunch.Pool.Get<List<BasePlayer>>();
		Vis.Entities(position, maxDist, obj, 131072);
		var result = obj.Any(item => !item.IsNpc && !item.IsSleeping() && item.IsAlive());
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
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