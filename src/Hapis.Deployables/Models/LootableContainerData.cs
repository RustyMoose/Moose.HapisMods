using System.Collections.Generic;

namespace Moose.Hapis.Deployables.Models;

public class LootableContainerData
{
	public string filename = string.Empty;

	public List<LootableItemData> items = [];

	public int respawnRateMin = 1;

	public int respawnRateMax = 1;

	public int refreshRateMin = 1;

	public int refreshRateMax = 1;

	public int spawnAmountMin = 1;

	public int spawnAmountMax = 1;
}
