using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Moose.Hapis.Deployables.Behaviours;

public class SpawnerHandler : MonoBehaviour
{
	private class SpawnManager : MonoBehaviour
	{
		public SpawnerHandler spawnhandler;

		private void OnDestroy()
		{
			if (!Rust.Application.isQuitting)
			{
				spawnhandler._flag = false;
				spawnhandler.SetSpawn();
			}
		}
	}

	private SpawnGroup spawnGroup;

	private BaseSpawnPoint baseSpawnPoint;

	private bool _flag;

	private void Awake()
	{
		spawnGroup = GetComponent<SpawnGroup>();
		baseSpawnPoint = GetComponentInChildren<BaseSpawnPoint>();
		spawnGroup.Clear();
	}

	public List<uint> ManagedPrefabIDs()
	{
		return spawnGroup.prefabs.Select(prefab => prefab.prefab.resourceID).ToList();
	}

	public float DistanceFromSpawn(Vector3 entityPosition)
	{
		baseSpawnPoint.GetLocation(out var position, out _);
		return Vector3.Distance(entityPosition, position);
	}

	public void InitializeFirstEntity()
	{
		var spawnManagers = FindObjectsOfType<SpawnManager>();
		
		foreach (var spawnManager in spawnManagers)
		{
			if (spawnManager.spawnhandler != this)
			{
				continue;
			}
			
			Debug.LogError("[Hapis.Deployables] SpawnHandler for " + 
						   HapisDeployables.Instance.GetPrefabName(spawnGroup) + 
						   " tried to initialize first entity but managed entity is Already spawned!");
			
			return;
		}
		
		CreateEntity();
	}

	internal void SetSpawn()
	{
		if (!_flag)
		{
			InvokeHandler.Invoke(this, CreateEntity, Config.KeyCardsRespawnSeconds);
		}
	}

	private void CreateEntity()
	{
		baseSpawnPoint.GetLocation(out var position, out var rotation);
		
		var baseEntity = GameManager.server.CreateEntity(HapisDeployables.Instance.GetPrefabName(spawnGroup), position, rotation, startActive: false);
		if (!baseEntity)
		{
			return;
		}
		
		baseEntity.enableSaving = false;
		baseEntity.gameObject.AwakeFromInstantiate();
		baseEntity.Spawn();
		baseEntity.gameObject.AddComponent<SpawnManager>().spawnhandler = this;
		
		_flag = true;
		
		if (Config.LogManagedSpawns)
		{
			Debug.LogWarning("[Hapis.Deployables] Spawned " + baseEntity + "@" + baseEntity.transform.position);
		}
	}
}
