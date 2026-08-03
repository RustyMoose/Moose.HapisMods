using UnityEngine;

namespace Moose.Hapis.Deployables.Behaviours;

public class LootSpawnerHandler : MonoBehaviour
{
	public class LootRespawnManager : MonoBehaviour
	{
		public LootSpawnerHandler handler;

		private void OnDestroy()
		{
			if (!Rust.Application.isQuitting && handler != null)
			{
				handler.BeginRespawnTimer();
			}
			else if (handler == null)
			{
				Debug.LogError("[Hapis.Deployables] LootSpawnerHandler is null! This means one of your Loot Containers will no longer Respawn: " + ToString());
			}
		}
	}

	public int respawnRateMin = 1;

	public int respawnRateMax = 1;

	public string gameObjectName;

	private string[] randomCrateList;

	private int randomCrateIndex = -1;

	public Vector3 position;

	public Quaternion rotation;

	public void InitializeFirstEntity()
	{
		var array = FindObjectsOfType<LootRespawnManager>();
		for (var i = 0; i < array.Length; i++)
		{
			if (array[i].handler == this)
			{
				Debug.LogError("[Hapis.Deployables] LootSpawnerHandler for " + gameObjectName + " tried to initialize first entity but managed entity is Already spawned!");
				return;
			}
		}
		
		RespawnLootbox();
	}

	public void BeginRespawnTimer()
	{
		var num = Mathf.Clamp(UnityEngine.Random.Range(respawnRateMin, respawnRateMax) * 60f, 60f, 2.1474836E+09f);
		FindObjectsOfType<LootSpawnerHandler>();
		if (Config.LogManagedSpawns)
		{
			Debug.LogWarning($"[Hapis.Deployables] {gameObjectName} destroyed! Respawning in {num}s");
		}
		InvokeHandler.Invoke(this, RespawnLootbox, num);
	}

	public void RespawnLootbox()
	{
		var strPrefab = gameObjectName;
		
		if (randomCrateIndex > -1)
		{
			var num = UnityEngine.Random.Range(0f, 1f);
			if (num < 0.2f)
			{
				strPrefab = randomCrateList[2];
			}
			else if (num < 0.5f)
			{
				strPrefab = randomCrateList[1];
			}
			else if (num < 0.8f)
			{
				strPrefab = randomCrateList[0];
			}
		}
		var baseEntity = GameManager.server.CreateEntity(strPrefab, position, rotation, startActive: false);
		if ((bool)baseEntity)
		{
			baseEntity.enableSaving = false;
			baseEntity.gameObject.AwakeFromInstantiate();
			baseEntity.Spawn();
			baseEntity.gameObject.AddComponent<LootRespawnManager>().handler = this;
			if (Config.LogManagedSpawns)
			{
				Debug.LogWarning("[Hapis.Deployables] Spawned " + baseEntity + "@" + baseEntity.transform.position);
			}
		}
	}

	private void OnDestroy()
	{
		Debug.LogError("[Hapis.Deployables] LootSpawnerHandler has been destroyed! This isn't supposed to happen!");
	}
}