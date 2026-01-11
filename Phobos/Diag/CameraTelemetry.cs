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

        // We are relabeling from z -> y as we are only interested in 2d coords
        DebugUI.Label(new Vector2(Screen.width / 2f, Screen.height / 4f), $"Position: x: {position.x}, y:{position.z}");
    }
}