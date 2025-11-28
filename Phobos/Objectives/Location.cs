using System.Collections.Generic;
using EFT.Interactive;
using UnityEngine;

namespace Phobos.Objectives;

public enum LocationCategory
{
    ContainerLoot,
    LooseLoot,
    Quest,
    Exfil
}

public class Location(string name, LocationCategory category, Vector3 position)
{
    public readonly string Name = name;
    public readonly LocationCategory Category = category;
    public readonly Vector3 Position = position;

    public static List<Location> Collect()
    {
        var pois = new List<Location>();

        Plugin.Log.LogInfo("Collecting quests POIs");

        foreach (var location in Object.FindObjectsOfType<TriggerWithId>())
        {
            if (location.transform == null)
                continue;
            
            pois.Add(new Location(location.name, LocationCategory.Quest, location.transform.position));
            Plugin.Log.LogInfo($"Found quest location: {location} | {location.name}");
        }
        
        foreach (var container in Object.FindObjectsOfType<LootableContainer>())
        {
            if (container.transform == null || !container.enabled || container.Template == null)
                continue;
            
            pois.Add(new Location(container.name, LocationCategory.ContainerLoot, container.transform.position));
            Plugin.Log.LogInfo($"Found lootable container: {container} | {container.name}");
        }
        
        return pois;
    }
}