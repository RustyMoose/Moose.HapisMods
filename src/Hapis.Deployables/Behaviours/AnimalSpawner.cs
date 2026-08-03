using ProtoBuf;
using UnityEngine;

namespace Moose.Hapis.Deployables.Behaviours;

public class AnimalSpawner : MonoBehaviour
{
    public PrefabData PD;

    public float Lifetime;

    public void Spawn(BaseEntity baseEntity)
    {
        if (!baseEntity)
        {
            return;
        }
        if (!HapisDeployables.AnyPlayersNearby(PD.position, Config.PlayerDistanceBlockAnimals) && Lifetime <= Time.time)
        {
            baseEntity.enableSaving = false;
            baseEntity.gameObject.AwakeFromInstantiate();
            baseEntity.Spawn();
            var animalSpawner = baseEntity.gameObject.AddComponent<AnimalSpawner>();
            animalSpawner.PD = PD;
            animalSpawner.Lifetime = Time.time + Config.AnimalsMinRespawnDelay;
        }
        else
        {
            baseEntity.Invoke(delegate
            {
                Spawn(baseEntity);
            }, 30f);
        }
    }

    private void OnDestroy()
    {
        if (Rust.Application.isQuitting)
        {
            return;
        }
        var baseEntity = GameManager.server.CreateEntity(StringPool.Get(PD.id), PD.position, PD.rotation, startActive: false);
        if ((bool)baseEntity)
        {
            Spawn(baseEntity);
            if (Config.LogManagedSpawns)
            {
                Debug.LogWarning("[Hapis.Deployables] Spawned " + baseEntity + "@" + baseEntity.transform.position);
            }
        }
    }
}
