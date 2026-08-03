using ProtoBuf;

namespace Moose.Hapis.IO.Models;

public class SerializedConnectionData
{
	public string fullPath;

	public VectorData position;

	public bool input;

	public int connectedTo;

	public int type;
}
