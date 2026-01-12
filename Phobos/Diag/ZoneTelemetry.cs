using System.Collections.Generic;
using Comfort.Common;
using Phobos.Entities;
using Phobos.Orchestration;
using Phobos.Systems;
using UnityEngine;

namespace Phobos.Diag;

public class ZoneTelemetry : MonoBehaviour
{
    // Colors for congestion levels
    private readonly Color _invalidCellColor = new(0.0f, 0.0f, 0.0f, 0.8f);
    private readonly Color _emptyCellColor = new(0.15f, 0.15f, 0.15f, 0.8f);
    private readonly Color _lowCongestionColor = new(0.2f, 0.6f, 0.2f, 0.7f);
    private readonly Color _mediumCongestionColor = new(0.8f, 0.8f, 0.2f, 0.7f);
    private readonly Color _highCongestionColor = new(0.8f, 0.2f, 0.2f, 0.7f);
    private readonly Color _cellCoordTextColor = new(0.4f, 0.4f, 0.4f, 1.0f);
    private readonly Color _agentColor = Color.cyan;
    private readonly Color _agentLeaderColor = Color.magenta;
    private readonly Color _cameraColor = Color.white;
    private readonly Color _zoneColor = Color.blue;
    private readonly Color _gridLineColor = new(0.5f, 0.5f, 0.5f, 0.8f);

    private const int LowCongestionThreshold = 2;
    private const int MediumCongestionThreshold = 4;

    private const float GridDisplaySize = 800f;
    private const float AgentDotRadius = 2f;
    private const float ZoneDotRadius = 4f;

    public void OnGUI()
    {
        var phobos = Singleton<PhobosManager>.Instance;

        if (phobos == null)
            return;

        RenderGrid(
            phobos.AgentData.Entities.Values,
            phobos.LocationSystem.Cells,
            phobos.LocationSystem.AdvectionField,
            phobos.LocationSystem.Zones,
            phobos.LocationSystem.GridSize,
            phobos.LocationSystem.WorldMin,
            phobos.LocationSystem.WorldMax
        );
    }

    private void RenderGrid(
        List<Agent> agents, Cell[,] cells, Vector2[,] advectionField, List<LocationSystem.Zone> zones,
        Vector2Int gridSize, Vector2 worldMin, Vector2 worldMax
    )
    {
        if (cells == null || gridSize.x == 0 || gridSize.y == 0)
            return;

        // Calculate display dimensions to keep cells square
        var maxDimension = Mathf.Max(gridSize.x, gridSize.y);
        var cellSize = GridDisplaySize / maxDimension;

        var displayWidth = cellSize * gridSize.x;
        var displayHeight = cellSize * gridSize.y;

        var gridRect = new Rect(
            (Screen.width - displayWidth) * 0.25f,
            (Screen.height - displayHeight) * 0.5f,
            displayWidth,
            displayHeight
        );

        // Draw cells
        for (var x = 0; x < gridSize.x; x++)
        {
            for (var y = 0; y < gridSize.y; y++)
            {
                var cellRect = new Rect(
                    gridRect.x + x * cellSize,
                    gridRect.y + (gridSize.y - 1 - y) * cellSize, // Flip Y
                    cellSize,
                    cellSize
                );

                var cell = cells[x, y];
                var color = cell.HasLocations ? GetCongestionColor(cell.Congestion) : _invalidCellColor;
                DrawFilledRect(cellRect, color);

                // Draw cell coordinates
                DrawCellCoordinates(cellRect, x, y, cellSize);
                
                // Advection
                // Have to flip the Y axis coordinate
                var advectionVector = advectionField[x, y] * new Vector2(1f, -1f);

                if (!(advectionVector.magnitude > 0.01f)) continue;

                var cellCenter = new Vector2(cellRect.x + cellRect.width / 2, cellRect.y + cellRect.height / 2);
                DrawLine(cellCenter, cellCenter + cellSize * advectionVector / 2, 1f);
            }
        }

        for (var i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            var zoneScreenCoords = new Vector2(
                gridRect.x + zone.Coords.x * cellSize + cellSize / 2,
                gridRect.y + (gridSize.y - 1 - zone.Coords.y) * cellSize + cellSize / 2
            );
            
            DrawDot(zoneScreenCoords, ZoneDotRadius, _zoneColor);
        }

        // Draw grid lines
        DrawGridLines(gridRect, gridSize, cellSize);

        // Draw agents
        DrawAgents(agents, worldMin, worldMax, gridRect, displayWidth, displayHeight);
        
        // Draw players
        DrawCamera(worldMin, worldMax, gridRect, displayWidth, displayHeight);

        // Draw border
        DrawRectOutline(gridRect, Color.white, 2f);

        // Draw legend (position it below the centered grid)
        var legendY = gridRect.y + displayHeight + 20;
        DrawLegend(new Rect((Screen.width - 300) / 2f, legendY, 300, 160));
    }

    private void DrawAgents(List<Agent> agents, Vector2 worldMin, Vector2 worldMax, Rect gridRect, float displayWidth, float displayHeight)
    {
        var worldWidth = worldMax.x - worldMin.x;
        var worldHeight = worldMax.y - worldMin.y;

        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var pos = agent.Bot.Position;

            // Convert world position to grid display position
            var normX = (pos.x - worldMin.x) / worldWidth;
            var normY = (pos.z - worldMin.y) / worldHeight;

            var screenX = gridRect.x + normX * displayWidth;
            var screenY = gridRect.y + (1f - normY) * displayHeight; // Flip Y

            var color = agent.IsLeader ? _agentLeaderColor : _agentColor;

            DrawDot(new Vector2(screenX, screenY), AgentDotRadius, color);
        }
    }
    
    private void DrawCamera(Vector2 worldMin, Vector2 worldMax, Rect gridRect, float displayWidth, float displayHeight)
    {
        var camera = CameraClass.Instance?.Camera;
        
        if (camera == null) return;
        
        var pos = camera.transform.position;
        
        var worldWidth = worldMax.x - worldMin.x;
        var worldHeight = worldMax.y - worldMin.y;

        // Convert world position to grid display position
        var normX = (pos.x - worldMin.x) / worldWidth;
        var normY = (pos.z - worldMin.y) / worldHeight;

        var screenX = gridRect.x + normX * displayWidth;
        var screenY = gridRect.y + (1f - normY) * displayHeight; // Flip Y

        DrawDot(new Vector2(screenX, screenY), AgentDotRadius, _cameraColor);
    }

    private Color GetCongestionColor(int congestion)
    {
        return congestion switch
        {
            0 => _emptyCellColor,
            < LowCongestionThreshold => _lowCongestionColor,
            < MediumCongestionThreshold => _mediumCongestionColor,
            _ => _highCongestionColor
        };
    }

    private void DrawGridLines(Rect gridRect, Vector2Int gridSize, float cellSize)
    {
        var oldColor = GUI.color;
        GUI.color = _gridLineColor;

        // Vertical lines
        for (var x = 0; x <= gridSize.x; x++)
        {
            var lineX = gridRect.x + x * cellSize;
            DrawLine(
                new Vector2(lineX, gridRect.y),
                new Vector2(lineX, gridRect.y + gridRect.height),
                1f
            );
        }

        // Horizontal lines
        for (var y = 0; y <= gridSize.y; y++)
        {
            var lineY = gridRect.y + y * cellSize;
            DrawLine(
                new Vector2(gridRect.x, lineY),
                new Vector2(gridRect.x + gridRect.width, lineY),
                1f
            );
        }

        GUI.color = oldColor;
    }

    private void DrawLegend(Rect rect)
    {
        GUI.Box(rect, "Grid Congestion");

        const float lineHeight = 25f;
        var y = rect.y + 25;

        DrawLegendItem(new Rect(rect.x + 10, y, 20, 20), _zoneColor, "Zone Spot");
        y += lineHeight;
        DrawLegendItem(new Rect(rect.x + 10, y, 20, 20), _emptyCellColor, "Empty");
        y += lineHeight;
        DrawLegendItem(new Rect(rect.x + 10, y, 20, 20), _lowCongestionColor, $"Low (1-{LowCongestionThreshold - 1})");
        y += lineHeight;
        DrawLegendItem(new Rect(rect.x + 10, y, 20, 20), _mediumCongestionColor, $"Med ({LowCongestionThreshold}-{MediumCongestionThreshold - 1})");
        y += lineHeight;
        DrawLegendItem(new Rect(rect.x + 10, y, 20, 20), _highCongestionColor, $"High ({MediumCongestionThreshold}+)");
        y += lineHeight;
        DrawLegendItem(new Rect(rect.x + 10, y, 20, 20), _agentColor, "Agents");
        y += lineHeight;
        DrawLegendItem(new Rect(rect.x + 10, y, 20, 20), _agentLeaderColor, "Squad Leaders");
        y += lineHeight;
        DrawLegendItem(new Rect(rect.x + 10, y, 20, 20), _cameraColor, "Camera");
    }

    private static void DrawLegendItem(Rect colorRect, Color color, string label)
    {
        DrawFilledRect(colorRect, color);
        GUI.Label(new Rect(colorRect.x + colorRect.width + 5, colorRect.y, 200, colorRect.height), label);
    }
    
    private void DrawCellCoordinates(Rect cellRect, int x, int y, float cellSize)
    {
        // Only draw if cell is large enough to be readable
        if (cellSize < 15f) return;

        var oldColor = GUI.color;
        GUI.color = _cellCoordTextColor;

        var coordText = $"{x},{y}";
        var textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(8, (int)(cellSize / 4f)),
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = _cellCoordTextColor }
        };

        GUI.Label(
            new Rect(cellRect.x + 1, cellRect.y + 1, cellRect.width, cellRect.height),
            coordText,
            textStyle
        );

        GUI.color = oldColor;
    }

    private static void DrawFilledRect(Rect rect, Color color)
    {
        var oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private static void DrawRectOutline(Rect rect, Color color, float thickness)
    {
        var oldColor = GUI.color;
        GUI.color = color;

        // Top
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        // Bottom
        GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
        // Left
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        // Right
        GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);

        GUI.color = oldColor;
    }

    private static void DrawLine(Vector2 start, Vector2 end, float thickness)
    {
        DebugUI.DrawLine(start, end, thickness);
    }

    private static void DrawDot(Vector2 center, float radius, Color color)
    {
        var oldColor = GUI.color;
        GUI.color = color;

        var diameter = radius * 2;
        GUI.DrawTexture(
            new Rect(center.x - radius, center.y - radius, diameter, diameter),
            Texture2D.whiteTexture
        );

        GUI.color = oldColor;
    }
}