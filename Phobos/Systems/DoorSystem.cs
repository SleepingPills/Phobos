using System.Linq;
using EFT.Interactive;
using Phobos.Diag;
using UnityEngine;

namespace Phobos.Systems;

public class DoorSystem
{
    public readonly Door[] Doors;

    public DoorSystem()
    {
        var interactables = Object.FindObjectsOfType<WorldInteractiveObject>();
        Doors = interactables.Where(interactable => interactable.Collider != null).OfType<Door>().ToArray();
        Log.Debug($"Found {Doors.Length} doors on the map");
    }
}