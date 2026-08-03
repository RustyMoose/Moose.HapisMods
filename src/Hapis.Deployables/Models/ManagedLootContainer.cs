namespace Moose.Hapis.Deployables.Models;

public class ManagedLootContainer
{
    public string gameobjectName = string.Empty;

    public int respawnTimeMin = 1;

    public int respawnTimeMax = 1;

    public ManagedLootContainer(string name, int timeMin, int timeMax)
    {
        gameobjectName = name;
        respawnTimeMin = timeMin;
        respawnTimeMax = timeMax;
    }
}
