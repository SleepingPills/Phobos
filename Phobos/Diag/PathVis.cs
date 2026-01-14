using UnityEngine;

namespace Phobos.Diag;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
public class PathVis
{
    private readonly LineRenderer _lineRenderer;

    public PathVis()
    {
        _lineRenderer = new GameObject().GetOrAddComponent<LineRenderer>();
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    public void Set(Vector3[] data, float thickness = 0.05f, bool knobs = false)
    {
        Set(data, Color.red, Color.green, thickness, knobs: knobs);
    }

    public void Set(Vector3[] data, Color color, float thickness = 0.05f, bool knobs = false)
    {
        Set(data, color, color, thickness, knobs);
    }

    public void Set(Vector3[] data, Color startColor, Color endColor, float thickness = 0.05f, bool knobs = false)
    {
        if (data == null || data.Length == 0)
        {
            _lineRenderer.positionCount = 0;
            return;
        }

        _lineRenderer.startColor = startColor;
        _lineRenderer.endColor = endColor;
        _lineRenderer.startWidth = thickness;
        _lineRenderer.endWidth = thickness;

        _lineRenderer.positionCount = data.Length;
        _lineRenderer.SetPositions(data);

        if (knobs)
        {
            for (var i = 0; i < data.Length; i++)
            {
                var point = data[i];
                DebugGizmos.Sphere(point, 0.35f, color: Color.blue, expiretime: 0f);
            }
        }
    }

    public static void Show(Vector3[] data, float thickness = 0.05f, bool knobs = false)
    {
        var vis = new PathVis();
        vis.Set(data, Color.red, Color.green, thickness, knobs: knobs);
    }

    public static void Show(Vector3[] data, Color color, float thickness = 0.05f, bool knobs = false)
    {
        var vis = new PathVis();
        vis.Set(data, color, thickness, knobs: knobs);
    }

    public void Clear()
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.positionCount = 0;
        }
    }
}