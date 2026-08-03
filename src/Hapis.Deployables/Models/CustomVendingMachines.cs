using UnityEngine;

namespace Moose.Hapis.Deployables.Models;

public class CustomVendingMachines
{
    public string prefabName;

    public Vector3 position;

    public Quaternion rotation;

    public NPCVendingMachine npcVendingMachine;

    public VendingContainerData vendingContainerData;

    public CustomVendingMachines(NPCVendingMachine npcVendingMachine0, VendingContainerData vendingContainerData0)
    {
        npcVendingMachine = npcVendingMachine0;
        prefabName = npcVendingMachine.PrefabName;
        position = npcVendingMachine.transform.position;
        rotation = npcVendingMachine.transform.rotation;
        vendingContainerData = vendingContainerData0;
    }
}