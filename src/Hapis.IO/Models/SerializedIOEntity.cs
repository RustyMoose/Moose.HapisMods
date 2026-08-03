using ProtoBuf;

namespace Moose.Hapis.IO.Models;

public class SerializedIOEntity
{
	public string fullPath;

	public VectorData position;

	public SerializedConnectionData[] inputs;

	public SerializedConnectionData[] outputs;

	public int accessLevel;

	public int doorEffect;

	public float timerLength;

	public int frequency;

	public bool unlimitedAmmo;

	public bool peaceKeeper;

	public string autoTurretWeapon;

	public int branchAmount;

	public int targetCounterNumber;

	public string rcIdentifier;

	public bool counterPassthrough;

	public int floors = 1;

	public string phoneName;
}
