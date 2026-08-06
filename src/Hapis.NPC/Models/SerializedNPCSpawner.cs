using ProtoBuf;

namespace Moose.Hapis.NPC.Models;

public class SerializedNPCSpawner
{
	public int npcType;

	public int respawnMin;

	public int respawnMax;

	public VectorData position;

	public string category;
}
