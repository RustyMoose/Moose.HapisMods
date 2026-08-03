using ProtoBuf;

namespace Moose.Hapis.NPC.Models;

public class SerializedNpcSpawner
{
	public int NpcType;

	public int RespawnMin;

	public int RespawnMax;

	public VectorData Position;

	public string Category;
}
