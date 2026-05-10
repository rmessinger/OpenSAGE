using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using OpenSage.Tools.ReplaySketch.Model;
using OpenSage.Tools.ReplaySketch.Services;

namespace OpenSage.Tools.ReplaySketch.UI;

/// <summary>
/// Renders a 2-D top-down map preview with:
/// - Player-start anchor dots (always visible, labelled P1/P2…).
/// - Per-action position markers colour-coded by player slot.
/// - For <see cref="LandmarkRelativePosition"/>: a dashed radius circle around the
///   anchor and either a line (fixed angle) or an arc sector (random angle range).
/// - Only the selected slot's markers are interactive (drag to reposition).
/// </summary>
internal sealed class MapPreviewPanel
{
    // Size of the preview region in screen pixels
    private const float PreviewSize = 400f;

    // Slot colours (ABGR for ImGui)
    private static readonly uint[] SlotColors =
    [
        0xFFFF4444u, // slot 0 – blue-ish (ABGR: blue)
        0xFF4444FFu, // slot 1 – red-ish
        0xFF44FF44u, // slot 2 – green
        0xFF44FFFFu, // slot 3 – yellow
    ];

    private static readonly uint AnchorColor = 0xFFFFFFFFu; // white
    private static readonly uint CircleColor = 0x66FFFFFFu; // translucent white
    private static readonly uint LineColor = 0xCCFFFFFFu; // semi-transparent white
    private static readonly uint ArcColor = 0x66FFFFFFu;

    public void Draw(ReplayScenario scenario, MapMetadataService? map, int selectedSlotIndex)
    {
        ImGui.BeginChild("MapPreview", new Vector2(PreviewSize + 16, PreviewSize + 16), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();

        // Draw background
        drawList.AddRectFilled(origin, origin + new Vector2(PreviewSize, PreviewSize), 0xFF2A2A2Au);
        drawList.AddRect(origin, origin + new Vector2(PreviewSize, PreviewSize), 0xFF555555u);

        if (map == null)
        {
            ImGui.SetCursorScreenPos(origin + new Vector2(8, 8));
            ImGui.TextDisabled("No map loaded");
            ImGui.EndChild();
            return;
        }

        var extentMin = map.ExtentMin;
        var extentMax = map.ExtentMax;

        // Convert a world XY position into preview-panel pixel position
        Vector2 WorldToPreview(Vector2 world)
        {
            var nx = (world.X - extentMin.X) / (extentMax.X - extentMin.X);
            var ny = 1f - (world.Y - extentMin.Y) / (extentMax.Y - extentMin.Y); // flip Y
            return origin + new Vector2(nx * PreviewSize, ny * PreviewSize);
        }

        // Convert a world-space radius to preview pixels
        float WorldRadiusToPixels(float worldRadius)
        {
            var mapWidthWorld = extentMax.X - extentMin.X;
            return worldRadius / mapWidthWorld * PreviewSize;
        }

        // Draw player-start anchor dots and action markers for each slot
        for (var slotIdx = 0; slotIdx < scenario.Players.Count; slotIdx++)
        {
            var player = scenario.Players[slotIdx];
            var color = slotIdx < SlotColors.Length ? SlotColors[slotIdx] : AnchorColor;
            var isSelected = slotIdx == selectedSlotIndex;

            // Player start anchor
            var startWorld = map.PlayerStart(slotIdx);
            var startScreen = WorldToPreview(new Vector2(startWorld.X, startWorld.Y));
            drawList.AddCircleFilled(startScreen, isSelected ? 7f : 5f, color);
            drawList.AddText(startScreen + new Vector2(6, -6), color, $"P{slotIdx + 1}");

            // Action markers
            for (var actionIdx = 0; actionIdx < player.Actions.Count; actionIdx++)
            {
                DrawActionMarker(drawList, player.Actions[actionIdx], actionIdx, slotIdx,
                    map, WorldToPreview, WorldRadiusToPixels, color, isSelected,
                    scenario.BaseRadiusWorldUnits);
            }
        }

        // Reserve the preview area so ImGui knows we used that space
        ImGui.Dummy(new Vector2(PreviewSize, PreviewSize));

        ImGui.EndChild();
    }

    private static void DrawActionMarker(
        ImDrawListPtr drawList,
        ActionEntry action,
        int actionIdx,
        int slotIdx,
        MapMetadataService map,
        Func<Vector2, Vector2> worldToPreview,
        Func<float, float> worldRadiusToPixels,
        uint color,
        bool isSelected,
        float baseRadius)
    {
        // Short label shown next to each position marker so multiple actions on the
        // same anchor (e.g. two FixedAngle entries that share OwnBase) can be told apart.
        var label = $"{actionIdx + 1}";

        if (action.Position == null)
            return;

        switch (action.Position)
        {
            case NormalizedPosition np:
                {
                    var extentMin = map.ExtentMin;
                    var extentMax = map.ExtentMax;
                    var wx = extentMin.X + np.NormX * (extentMax.X - extentMin.X);
                    var wy = extentMin.Y + np.NormY * (extentMax.Y - extentMin.Y);
                    var screen = worldToPreview(new Vector2(wx, wy));
                    drawList.AddCircleFilled(screen, isSelected ? 5f : 3f, color);
                    if (isSelected)
                        drawList.AddCircle(screen, 7f, 0xFFFFFFFFu);
                    drawList.AddText(screen + new Vector2(6, -6), color, label);
                    break;
                }

            case LandmarkRelativePosition lrp:
                {
                    var anchor = GetLandmarkWorld(lrp.Landmark, slotIdx, 1 - slotIdx, map);
                    var anchorScreen = worldToPreview(new Vector2(anchor.X, anchor.Y));

                    var distPixels = worldRadiusToPixels(lrp.DistanceInBaseWidths * baseRadius);

                    // Radius circle — draw it once per unique (anchor, dist) combination.
                    // We always draw it; overlapping circles at the same position are harmless.
                    drawList.AddCircle(anchorScreen, distPixels, CircleColor, 64, 1f);

                    switch (lrp.Angle)
                    {
                        case FixedAngle fa:
                            {
                                var rad = fa.Degrees * MathF.PI / 180f;
                                var tip = anchorScreen + new Vector2(
                                    MathF.Cos(rad) * distPixels,
                                    -MathF.Sin(rad) * distPixels); // flip Y for screen space
                                drawList.AddLine(anchorScreen, tip, LineColor, 1.5f);
                                drawList.AddCircleFilled(tip, isSelected ? 5f : 3f, color);
                                if (isSelected)
                                    drawList.AddCircle(tip, 7f, 0xFFFFFFFFu);
                                // Label next to the tip so each action's line is identifiable
                                drawList.AddText(tip + new Vector2(6, -6), color, label);
                                break;
                            }

                        case RandomAngle ra:
                            {
                                // Draw arc sector between min and max angles
                                DrawArcSector(drawList, anchorScreen, distPixels,
                                    ra.MinDegrees, ra.MaxDegrees, ArcColor, color);
                                // Label at the mid-angle tip
                                var midRad = (ra.MinDegrees + ra.MaxDegrees) * 0.5f * MathF.PI / 180f;
                                var midTip = anchorScreen + new Vector2(
                                    MathF.Cos(midRad) * distPixels,
                                    -MathF.Sin(midRad) * distPixels);
                                drawList.AddText(midTip + new Vector2(4, -6), color, label);
                                break;
                            }
                    }
                    break;
                }
        }
    }

    private static Vector3 GetLandmarkWorld(LandmarkType landmark, int ownerIdx, int enemyIdx, MapMetadataService map)
        => landmark switch
        {
            LandmarkType.OwnBase => map.PlayerStart(ownerIdx),
            LandmarkType.EnemyBase => map.PlayerStart(enemyIdx),
            LandmarkType.MapCenter => map.MapCenter,
            _ => map.MapCenter,
        };

    private static void DrawArcSector(
        ImDrawListPtr drawList,
        Vector2 center,
        float radius,
        float minDeg,
        float maxDeg,
        uint fillColor,
        uint edgeColor)
    {
        const int segments = 24;
        var minRad = minDeg * MathF.PI / 180f;
        var maxRad = maxDeg * MathF.PI / 180f;
        var step = (maxRad - minRad) / segments;

        // Fan triangles
        for (var i = 0; i < segments; i++)
        {
            var a0 = minRad + i * step;
            var a1 = a0 + step;
            var p0 = center + new Vector2(MathF.Cos(a0) * radius, -MathF.Sin(a0) * radius);
            var p1 = center + new Vector2(MathF.Cos(a1) * radius, -MathF.Sin(a1) * radius);
            drawList.AddTriangleFilled(center, p0, p1, fillColor);
        }

        // Arc outline
        var minTip = center + new Vector2(MathF.Cos(minRad) * radius, -MathF.Sin(minRad) * radius);
        var maxTip = center + new Vector2(MathF.Cos(maxRad) * radius, -MathF.Sin(maxRad) * radius);
        drawList.AddLine(center, minTip, edgeColor, 1.2f);
        drawList.AddLine(center, maxTip, edgeColor, 1.2f);
        drawList.AddCircle(minTip, 3f, edgeColor);
        drawList.AddCircle(maxTip, 3f, edgeColor);
    }
}
