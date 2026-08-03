using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using ConVar;
using Moose.Hapis.IO.Behaviours;
using Moose.Hapis.IO.Models;
using ProtoBuf;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Moose.Hapis.IO;

public class HapisIO
{
	public static HapisIO Instance = null!;

	private readonly List<BaseEntity> _destroyOnUnload = [];

	private readonly Dictionary<Vector3, PrefabData> _elevators = new();

	private readonly List<Vector3> _protectedEntities = [];

	private readonly List<IOEntity> _processed = [];

	private readonly List<Dictionary<Vector3, Vector3>> _wires = [];

	private SerializedIOData? _serializedIoData;

	private bool _mapDataLoaded;

	private static readonly IReadOnlyCollection<Type> ComponentTypes = [
		typeof(GroundWatch),
		typeof(DestroyOnGroundMissing),
		typeof(DeployableDecay)
	];

	public void InitializeHook()
	{
		Debug.Log("[Hapis.IO] Checking for IO Prefabs");

		if (_protectedEntities.Count == 0)
		{
			return;
		}
		
		Debug.Log("[Hapis.IO] Making IO Connections");
		
		InitializeIO();
	}

	public void OnServerSaving()
	{
		foreach (var item in _destroyOnUnload.Where(item => BaseEntity.saveList.Contains(item)))
		{
			BaseEntity.saveList.Remove(item);
		}
	}
	
	public void OnSaveLoaded()
	{
		CheckCustomElevators();
	}

	public void OnServerShuttingDown()
	{
		Debug.Log("[Hapis.IO] Cleaning up elevators...");
		
		foreach (var item in _destroyOnUnload)
		{
			if (item.IsDestroyed)
			{
				continue;
			}
			
			item.transform.position = new Vector3(0f, -5f, 0f);
			item.transform.hasChanged = true;
			_protectedEntities.Remove(item.transform.position);
			item.Kill();
		}
	}

	public bool OnChatMessageHandler(BasePlayer player, string message, Chat.ChatChannel targetChannel)
	{
		if (player == null || targetChannel != 0)
		{
			return true;
		}
		if (!player.IsAdmin)
		{
			return true;
		}
		
		switch (message.ToLower())
		{
			case "/showiowires":
			{
				player.ChatMessage("[Hapis.IO] Showing IO Wires");
				ShowWires(player);
				return false;
			}
			case "/reloaddoors":
			{
				player.ChatMessage("[Hapis.IO] Reloading Doors");
				ReloadDoors(player);
				return false;
			}
			default:
				return true;
		}
	}

	public bool OnEntityAttackedHandler(BaseCombatEntity baseCombatEntity, HitInfo? hitInfo = null)
	{
		if (baseCombatEntity == null)
		{
			return true;
		}
		
		return !_protectedEntities.Contains(baseCombatEntity.transform.position);
	}

	public bool CanPlayerOpenIoEntityContainer(BasePlayer player, string panelToOpen, ContainerIOEntity container)
	{
		if (player == null || container == null || player.IsAdmin)
		{
			return true;
		}
		
		return !_protectedEntities.Contains(container.transform.position);
	}

	public bool CanPlayerOpenStorageContainer(BasePlayer player, string panelToOpen, StorageContainer container)
	{
		if (player == null || container == null || (player.IsAdmin && player.IsGod() && player.IsFlying))
		{
			return true;
		}
		
		return !_protectedEntities.Contains(container.transform.position);
	}

	public bool CanModifyEntityHook(BasePlayer player, IOEntity ent)
	{
		if (!(player == null) && !(ent == null) && (!player.IsAdmin || !player.IsGod() || !player.IsFlying))
		{
			return !_protectedEntities.Contains(ent.transform.position);
		}
		return true;
	}

	public bool CanPickupHook(BaseCombatEntity baseCombatEntity)
	{
		if (baseCombatEntity == null)
		{
			return true;
		}
		
		return !_protectedEntities.Contains(baseCombatEntity.transform.position);
	}

	public bool OnEntityHurt(BaseCombatEntity baseCombatEntity)
	{
		if (baseCombatEntity == null)
		{
			return true;
		}
		
		return !_protectedEntities.Contains(baseCombatEntity.transform.position);
	}

	public bool OnEntityDeath(HitInfo hitInfo, BaseCombatEntity baseCombatEntity)
	{
		if (baseCombatEntity == null)
		{
			return true;
		}
		
		return !_protectedEntities.Contains(baseCombatEntity.transform.position);
	}

	public bool OnPrefabSpawning(string category, Prefab? prefab, Vector3 position, Quaternion rotation, Vector3 scale)
	{
		if (!_mapDataLoaded)
		{
			ReadMapData();
		}
		
		if (prefab == null || !prefab.Object)
		{
			return true;
		}
		
		if (prefab.ID == 3978222077u)
		{
			var val = new PrefabData();
			val.position = position;
			val.id = 3978222077u;
			val.rotation = rotation;
			val.scale = scale;
			val.category = category;
			_elevators.Add(position, val);
			return false;
		}
		
		if (prefab.Object.HasComponent<SpawnGroup>())
		{
			var component = prefab.Object.GetComponent<SpawnGroup>();
			if (!component.WantsTimedSpawn())
			{
				SetupSpawner(component);
			}
		}
		
		if (_protectedEntities.Contains(position))
		{
			var component2 = prefab.Object.GetComponent<BaseEntity>();
			if (component2 != null)
			{
				SetupEntity(component2);
			}
		}
		
		return true;
	}

	public bool OnAdminKill(BaseNetworkable baseNetworkable)
	{
		if (baseNetworkable == null)
		{
			return true;
		}
		
		return !_protectedEntities.Contains(baseNetworkable.transform.position);
	}

	private void InitializeIO()
	{
		foreach (var elevator in _elevators)
		{
			try
			{
				RecreateElevatorBase("assets/prefabs/deployable/elevator/elevator.prefab", elevator.Value.position, elevator.Value.rotation);
			}
			catch
			{
				Debug.LogError("Fault With Corrupted Elevators");
			}
		}

		if (_serializedIoData is null)
		{
			return;
		}
		
		foreach (var entity in _serializedIoData.entities)
		{
			try
			{
				var baseEntity = FindEntity(entity.position, entity.fullPath);
				
				if (baseEntity == null)
				{
					continue;
				}
				
				if (entity.timerLength == 0f)
				{
					entity.timerLength += 0.25f;
				}
				
				try
				{
					switch (baseEntity)
					{
						case Elevator elevator:
							try
							{
								CreateElevator(elevator, entity);
							}
							catch
							{
								Debug.LogError("Elevator Fault");
							}
							continue;
						case CardReader cardReader:
						{
							if (cardReader != null)
							{
								entity.accessLevel++;
								var cardReaderMonitor = cardReader.gameObject.GetComponent<CardReaderMonitor>() ?? 
														cardReader.gameObject.AddComponent<CardReaderMonitor>();
								if (cardReaderMonitor != null)
								{
									cardReaderMonitor.TimerLength = entity.timerLength;
								}
								
								cardReader.accessLevel = entity.accessLevel;
								cardReader.accessDuration = entity.timerLength;
								cardReader.SetFlag(BaseEntity.Flags.Reserved1, entity.accessLevel == 1);
								cardReader.SetFlag(BaseEntity.Flags.Reserved2, entity.accessLevel == 2);
								cardReader.SetFlag(BaseEntity.Flags.Reserved3, entity.accessLevel == 3);
							}

							break;
						}
						case PressButton button:
							button.pressDuration = entity.timerLength;
							break;
						case RFBroadcaster rfBroadcaster:
						{
							rfBroadcaster.frequency = entity.frequency;
							rfBroadcaster.MarkDirty();
							rfBroadcaster.SendNetworkUpdate();
							break;
						}
						case CCTV_RC cctvRc:
							cctvRc.isStatic = true;
							cctvRc.UpdateIdentifier(entity.rcIdentifier, clientSend: true);
							break;
						case Telephone telephone:
							telephone.Controller.PhoneName = entity.phoneName;
							break;
						case TimerSwitch timerSwitch:
							timerSwitch.timerLength = entity.timerLength;
							break;
						case SamSite site:
							site.staticRespawn = true;
							break;
						case ElectricalBranch electricalBranch:
							electricalBranch.branchAmount = entity.branchAmount;
							break;
						case PowerCounter powerCounter:
							powerCounter.SetFlag(BaseEntity.Flags.Reserved2, entity.counterPassthrough);
							powerCounter.SetCounterNumber(0);
							powerCounter.targetCounterNumber = entity.targetCounterNumber;
							break;
						case WheelSwitch wheelSwitch:
							var connection = wheelSwitch.gameObject.GetComponent<IOEntityToWheelSwitchConnection>() ?? 
											 wheelSwitch.gameObject.AddComponent<IOEntityToWheelSwitchConnection>(); 
							connection.SetTarget(wheelSwitch);
							break;
						case DoorManipulator manipulator:
							manipulator.SetTargetDoor(manipulator.FindDoor());
							break;
					}
				}
				catch
				{
					Debug.LogError("Failed To Set Setting " + baseEntity);
				}
				
				var ioEntity = baseEntity as IOEntity;
				if (ioEntity == null)
				{
					continue;
				}
				
				Array.Resize(ref ioEntity.outputs, entity.outputs.Length);
				
				for (var i = 0; i < ioEntity.outputs.Length; i++)
				{
					try
					{
						ioEntity.outputs[i] = new IOEntity.IOSlot
						{
							connectedTo = new IOEntity.IORef()
						};
						
						if (entity.outputs[i] == null)
						{
							continue;
						}
						
						var iOEntity2 = FindEntity(entity.outputs[i].position, entity.outputs[i].fullPath) as IOEntity;
						if (iOEntity2 is Elevator elevator1 && ioEntity is not ElectricGenerator)
						{
							var connectedTo = entity.outputs[i].connectedTo;
							var inputSlot = 0;
							if (connectedTo % 2 == 0)
							{
								connectedTo /= 2;
							}
							else
							{
								connectedTo = (connectedTo + 1) / 2;
								inputSlot = 1;
							}
							var list = new List<Elevator>();
							Vis.Entities(GetWorldSpaceFloorPosition(elevator1, connectedTo - 1), 1f, list);
							foreach (var elevator in list)
							{
								RunWire(ioEntity, i, elevator, inputSlot);
							}
						}
						else if (iOEntity2 != null)
						{
							RunWire(ioEntity, i, iOEntity2, entity.outputs[i].connectedTo);
						}
					}
					catch
					{
						//
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogError(ex.ToString());
			}
		}
		
		foreach (var item in _processed)
		{
			try
			{
				item.MarkDirtyForceUpdateOutputs();
				item.SendNetworkUpdate();
			}
			catch
			{
				//
			}
		}
		
		Debug.Log("[Hapis.IO] Ran " + _wires.Count + " IO Connections");
	}

	public void RunWire(IOEntity outputEntity, int outputSlot, IOEntity inputEntity, int inputSlot)
	{
		if (inputEntity.inputs[inputSlot] == null || inputEntity.inputs.Length < inputSlot - 1 || outputEntity.outputs[outputSlot] == null || outputEntity.outputs.Length < outputSlot - 1)
		{
			Debug.LogError("Failed To Run Wire No Socket: " + outputEntity + ":" + outputSlot + " to " + inputEntity + ":" + inputSlot);
			return;
		}
		
		if (inputEntity is WheelSwitch wheelSwitch)
		{
			var wheelSwitchConnection = outputEntity.gameObject.GetComponent<IOEntityToWheelSwitchConnection>() ??
										outputEntity.gameObject.AddComponent<IOEntityToWheelSwitchConnection>();
			wheelSwitchConnection.SetTarget(wheelSwitch);
		}
		
		inputEntity.inputs[inputSlot].connectedTo.Set(outputEntity);
		inputEntity.inputs[inputSlot].connectedToSlot = outputSlot;
		inputEntity.inputs[inputSlot].connectedTo.Init();
		
		outputEntity.outputs[outputSlot].connectedTo.Set(inputEntity);
		outputEntity.outputs[outputSlot].connectedToSlot = inputSlot;
		outputEntity.outputs[outputSlot].connectedTo.Init();
		
		if (!_processed.Contains(outputEntity))
		{
			_processed.Add(outputEntity);
		}
		
		if (!_processed.Contains(inputEntity))
		{
			_processed.Add(inputEntity);
		}
		
		LogWires(outputEntity, inputEntity);
	}

	private void LogWires(IOEntity outputEntity, IOEntity inputEntity)
	{
		var position = outputEntity.transform.position;
		var position2 = inputEntity.transform.position;
		
		if (outputEntity is CardReader or TimerSwitch or PowerCounter or ElectricSwitch or SmartSwitch or PressButton or ORSwitch or XORSwitch or ANDSwitch or DoorManipulator or FuseBox)
		{
			position.y += 0.8f;
		}
		
		if (inputEntity is CardReader or TimerSwitch or PowerCounter or ElectricSwitch or SmartSwitch or PressButton or ORSwitch or XORSwitch or ANDSwitch or DoorManipulator or FuseBox)
		{
			position2.y += 0.8f;
		}
		
		_wires.Add(new Dictionary<Vector3, Vector3> { { position, position2 } });
	}

	private static BaseEntity? FindEntity(Vector3 pos, string? path = "")
	{
		if (path == null)
		{
			return null;
		}

		foreach (var entity in BaseNetworkable.serverEntities)
		{
			if (entity == null || entity.IsDestroyed || (entity.PrefabName != path && path != "") || !(Vector3.Distance(pos, entity.transform.position) < 0.01f))
			{
				continue;
			}
			
			if (entity is BaseEntity baseEntity && baseEntity != null)
			{
				return baseEntity;
			}
		}
		
		return null;
	}

	public BaseEntity RecreateElevatorBase(string path, Vector3 pos, Quaternion rot)
	{
		var baseEntity = GameManager.server.CreateEntity(path, pos, rot);
		_protectedEntities.Add(baseEntity.transform.position);
		baseEntity.Spawn();
		SetupEntity(baseEntity);
		_destroyOnUnload.Add(baseEntity);
		baseEntity.SendNetworkUpdateImmediate();
		return baseEntity;
	}

	private void CreateElevator(Elevator baseElevator, SerializedIOEntity serializedEntity)
	{
		_processed.Add(baseElevator);
		
		var elevator = baseElevator;
		
		for (var i = 1; i < serializedEntity.floors; i++)
		{
			var newElevator = GameManager.server.CreateEntity(baseElevator.PrefabName, baseElevator.transform.position + baseElevator.transform.up * (3f * i), baseElevator.transform.rotation) as Elevator;
			
			newElevator!.pickup.enabled = false;
			newElevator.EnableSaving(wants: false);
			newElevator.Spawn();
			
			SetupEntity(newElevator);
			
			_protectedEntities.Add(newElevator.transform.position);
			_processed.Add(newElevator);
			_destroyOnUnload.Add(newElevator);
			
			newElevator.RefreshEntityLinks();
			newElevator.OnDeployed(null, null, null);
			newElevator.SetFlag(BaseEntity.Flags.Reserved2, true, false, false);
			
			elevator = newElevator;
		}
		
		elevator.SetFlag(BaseEntity.Flags.Reserved1, true, false, false);
		
		CreateLift(elevator);
	}


	private void CreateLift(Elevator elevator)
	{
		var elevatorLift = elevator.liftEntity.Get(elevator.isServer);
		
		if (elevatorLift == null)
		{
			elevatorLift = FindLift(elevator);
			
			if (elevatorLift == null)
			{
				elevatorLift = GameManager.server.CreateEntity(elevator.LiftEntityPrefab.resourcePath, GetWorldSpaceFloorPosition(elevator, elevator.Floor), elevator.LiftRoot.rotation) as ElevatorLift;
				
				if (elevatorLift == null)
				{
					return;
				}
				
				elevatorLift.SetParent(elevator, worldPositionStays: true);
				elevatorLift.enableSaving = false;
				elevatorLift.Spawn();
				
				_protectedEntities.Add(elevatorLift.transform.position);
				_destroyOnUnload.Add(elevatorLift);
				
				elevator.liftEntity.Set(elevatorLift);
				
				return;
			}
		}
		
		if (elevatorLift != null)
		{
			elevatorLift.pickup.enabled = false;
			elevatorLift.enableSaving = false;
			elevatorLift.SendNetworkUpdate();
			_destroyOnUnload.Add(elevatorLift);
			_protectedEntities.Add(elevatorLift.transform.position);
		}
	}

	private static Vector3 GetWorldSpaceFloorPosition(Elevator elevator, int floor)
	{
		var num = elevator.Floor - floor;
		var val = elevator.transform.up * (num * 3f);
		val.y -= 1f;
		return elevator.transform.position - val;
	}

	private static ElevatorLift? FindLift(Elevator elevator)
	{
		return elevator.children.OfType<ElevatorLift>().FirstOrDefault();
	}
	
	public void SetupEntity(BaseEntity baseEntity)
	{
		if (baseEntity is LiquidContainer)
		{
			return;
		}
		
		if (baseEntity is ReactiveTarget reactiveTarget)
		{
			reactiveTarget.ResetTarget();
		}
		
		var flags = baseEntity.flags;
		baseEntity.enableSaving = baseEntity is not Elevator;
		baseEntity.ResetState();
		baseEntity.flags = flags;
		RustEditIOEntity(baseEntity);
		
		if (baseEntity is not IOEntity ioEntity)
		{
			return;
		}
		
		if (ioEntity == null)
		{
			return;
		}
			
		ioEntity.ResetIOState();
		ioEntity.OnCircuitChanged(forceUpdate: true);
		ResetIOEntity(ioEntity);
		ioEntity.SendIONetworkUpdate();
	}

	private static void SetupSpawner(SpawnGroup spawnerEntity)
	{
		if (spawnerEntity == null || spawnerEntity.WantsTimedSpawn())
		{
			return;
		}
		
		var respawnDelayMin = 1200f;
		var respawnDelayMax = 1500f;
		
		if (spawnerEntity.name.Contains("food") || spawnerEntity.name.Contains("barrel") || spawnerEntity.name.Contains("basic"))
		{
			respawnDelayMin = 900f;
			respawnDelayMax = 1200f;
		}
		
		if (spawnerEntity.name.Contains("tech") || spawnerEntity.name.Contains("fuel") || spawnerEntity.name.Contains("oil"))
		{
			respawnDelayMin = 1500f;
			respawnDelayMax = 1800f;
		}
		
		if (spawnerEntity.name.Contains("elite") || spawnerEntity.name.Contains("ammo"))
		{
			respawnDelayMin = 2400f;
			respawnDelayMax = 2400f;
		}
		
		spawnerEntity.respawnDelayMin = respawnDelayMin;
		spawnerEntity.respawnDelayMax = respawnDelayMax;
	}

	private static T? DeserializeMapData<T>(byte[] serializedMapData, out bool mapDataFound)
	{
		try
		{
			using var stream = new MemoryStream(serializedMapData);
			var result = (T)new XmlSerializer(typeof(T)).Deserialize(stream);
			
			mapDataFound = true;
			
			return result;
		}
		catch (Exception ex)
		{
			Debug.Log(ex.Message + " " + ex.StackTrace);
			
			mapDataFound = false;
			
			return default;
		}
	}

	private static byte[]? GetSerializedIoData()
	{
		foreach (var map in World.Serialization.world.maps)
		{
			if (Encoding.ASCII.GetString(map.data).Contains("SerializedIOData"))
			{
				return map.data;
			}
		}
		
		return null;
	}

	private void ReadMapData()
	{
		if (_mapDataLoaded)
		{
			Debug.LogWarning("[Hapis.IO] Map Data Already Loaded");
			return;
		}
		
		Debug.Log("[Hapis.IO] Reading Map Data");
		
		_protectedEntities.Clear();
		
		if (_serializedIoData != null)
		{
			return;
		}
		
		var array = GetSerializedIoData();
		if (array == null)
		{
			return;
		}
		
		_serializedIoData = DeserializeMapData<SerializedIOData>(array, out var mapDataFound);
		
		if (mapDataFound && _serializedIoData != null)
		{
			foreach (var entity in _serializedIoData.entities)
			{
				_protectedEntities.Add(new Vector3(entity.position.x, entity.position.y, entity.position.z));
			}
		}
		
		Debug.Log("[Hapis.IO] IO data Found:" + mapDataFound);
		
		_mapDataLoaded = true;
	}

	private void CheckCustomElevators()
	{
		var list = new List<BaseEntity>();
		
		foreach (var elevator in _elevators)
		{
			var key = elevator.Key;
			
			for (var i = 0; i < 100; i++)
			{
				Vis.Entities(key, 3f, list);
				
				key.y = i * 3f;
				key.y -= 1f;
				
				foreach (var item in list.Where(item => !item.IsDestroyed && item is Elevator or ElevatorLift))
				{
					Debug.LogWarning($"[Hapis.IO] Destroying custom elevator {item} at {item.transform.position} that were not properly destroyed during server shutdown. Restart required to fix.");
					item.Kill();
				}
				
				list.Clear();
			}
		}
	}

	public void RustEditIOEntity(BaseEntity ioEntity)
	{
		if (!_protectedEntities.Contains(ioEntity.transform.position))
		{
			return;
		}

		foreach (var type in ComponentTypes)
		{
			var component = ioEntity.GetComponent(type);
			if (component != null)
			{
				Object.Destroy(component);
			}
		}
	}

	private static void ResetIOEntity(IOEntity ioEntity)
	{
		if (ioEntity == null)
		{
			return;
		}
		
		foreach (var input in ioEntity.inputs)
		{
			input.Clear();
		}
		
		foreach (var output in ioEntity.outputs)
		{
			output.Clear();
		}
	}

	private void ReloadDoors(BasePlayer player)
	{
		var doorsReloadedCount = 0;
		var doorPositionMap = BaseNetworkable.serverEntities
											 // ReSharper disable once RedundantEnumerableCastCall
											 .OfType<BaseNetworkable>()
											 .Where(serverEntity => serverEntity.PrefabName.ToLower().Contains("hinged"))
											 .ToDictionary(serverEntity => serverEntity as BaseEntity, serverEntity => serverEntity.transform.position);

		player.ChatMessage("[Hapis.IO] Found " + doorPositionMap.Count + " doors on server!");
		
		foreach (var prefabData in World.Serialization.world.prefabs)
		{
			var prefabName = StringPool.Get(prefabData.id);
			if (!prefabName.ToLower().Contains("hinged") || doorPositionMap.ContainsValue(prefabData.position))
			{
				continue;
			}
			
			var list = new List<Door>();
			Vis.Entities(prefabData.position, 1.5f, list);
			if (list.Count != 0)
			{
				continue;
			}
			
			try
			{
				var newDoor = GameManager.server.CreateEntity(prefabName, prefabData.position, Quaternion.Euler(prefabData.rotation)) as StabilityEntity;
				if (newDoor == null)
				{
					continue;
				}
				
				newDoor.Spawn();
				
				SetupEntity(newDoor);
				
				newDoor.grounded = true;
				newDoor.pickup.enabled = false;
				
				doorsReloadedCount++;
				
				newDoor.Invoke(() => 
				{
					var doorManipulators = Facepunch.Pool.Get<List<DoorManipulator>>();
					
					Vis.Entities(prefabData.position, 1.5f, doorManipulators);
					
					if (doorManipulators.Count != 0)
					{
						doorManipulators[0].SetTargetDoor(newDoor as Door);
					}
					
					Facepunch.Pool.FreeUnmanaged(ref doorManipulators);
				}, 5f);
			}
			catch
			{
				//
			}
		}
		
		player.ChatMessage("[Hapis.IO] Reloaded " + doorsReloadedCount + " missing doors");
	}

	private void ShowWires(BasePlayer player)
	{
		if (!player.IsAdmin)
		{
			return;
		}

		foreach (var item in _wires.SelectMany(wire => wire.Where(item => Vector3.Distance(item.Key, player.transform.position) < 100f || Vector3.Distance(item.Value, player.transform.position) < 100f)))
		{
			player.SendConsoleCommand(
				"ddraw.line", 
				10f, 
				Color.green, 
				item.Key + new Vector3(0f, 0.5f, 0f), 
				item.Value + new Vector3(0f, 0.5f, 0f));
		}
	}
}
