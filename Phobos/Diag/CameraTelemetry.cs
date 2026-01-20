using Comfort.Common;
using Phobos.Orchestration;
using UnityEngine;

namespace Phobos.Diag;

public class CameraTelemetry : MonoBehaviour
{
    public void OnGUI()
    {
        var camera = CameraClass.Instance?.Camera;

        if (camera == null)
            return;

        var position = camera.transform.position;
        var locationSystem = Singleton<PhobosManager>.Instance.LocationSystem;
        
        var coords = locationSystem.WorldToCell(position);
        var advection = locationSystem.AdvectionField[coords.x, coords.y];
        var convergence = locationSystem.ConvergenceField[coords.x, coords.y];

        // We are relabeling from z -> y as we are only interested in 2d coords
        DebugUI.Label(new Vector2(Screen.width / 2f, Screen.height / 4f), $"Pos: {new Vector2(position.x, position.z)} Cell {coords} Adv: {advection} Cnv: {convergence}");
    }
}