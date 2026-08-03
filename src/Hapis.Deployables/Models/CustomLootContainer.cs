using UnityEngine;

namespace Moose.Hapis.Deployables.Models;

public class CustomLootContainer
{
	public string prefabName;

	public Vector3 position;

	public Quaternion rotation;

	public LootContainer lootContainer;

	public LootableContainerData lootableContainerData;

	public CustomLootContainer(LootContainer lootContainer0, LootableContainerData lootableContainerData0)
	{
		lootContainer = lootContainer0;
		prefabName = lootContainer.PrefabName;
		position = lootContainer.transform.position;
		rotation = lootContainer.transform.rotation;
		lootableContainerData = lootableContainerData0;
	}
}
