using System.Numerics;
using OpenSage.Tools.ReplaySketch.Maui.ViewModels;
using OpenSage.Tools.ReplaySketch.Model;
using OpenSage.Tools.ReplaySketch.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace OpenSage.Tools.ReplaySketch.Maui.Views;

public partial class MapPreviewView : ContentView
{
    // Slot colours (ARGB)
    private static readonly SKColor[] SlotColors =
    [
        new SKColor(0xFF, 0x44, 0x44, 0xFF), // slot 0 – red
        new SKColor(0x44, 0x44, 0xFF, 0xFF), // slot 1 – blue
        new SKColor(0x44, 0xFF, 0x44, 0xFF), // slot 2 – green
        new SKColor(0x44, 0xFF, 0xFF, 0xFF), // slot 3 – yellow
    ];

    private static readonly SKColor AnchorColor = SKColors.White;
    private static readonly SKColor CircleColor = new SKColor(0xFF, 0xFF, 0xFF, 0x66);
    private static readonly SKColor LineColor = new SKColor(0xFF, 0xFF, 0xFF, 0xCC);
    private static readonly SKColor ArcColor = new SKColor(0xFF, 0xFF, 0xFF, 0x66);

    private MainViewModel? _vm;

    public MapPreviewView()
    {
        InitializeComponent();
    }

    public void BindViewModel(MainViewModel vm)
    {
        _vm = vm;
        _vm.PropertyChanged += (_, _) => Canvas.InvalidateSurface();
        Invalidate();
    }

    public void Invalidate() => Canvas.InvalidateSurface();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(0x2A, 0x2A, 0x2A));

        var map = _vm?.Map;
        if (map == null)
        {
            NoMapLabel.IsVisible = true;
            return;
        }
        NoMapLabel.IsVisible = false;

        var info = e.Info;
        var previewSize = Math.Min(info.Width, info.Height);
        var offsetX = (info.Width - previewSize) / 2f;
        var offsetY = (info.Height - previewSize) / 2f;

        // Draw border
        using var borderPaint = new SKPaint { Color = new SKColor(0x55, 0x55, 0x55), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRect(offsetX, offsetY, previewSize, previewSize, borderPaint);

        var extentMin = map.ExtentMin;
        var extentMax = map.ExtentMax;

        SKPoint WorldToScreen(Vector2 world)
        {
            var nx = (world.X - extentMin.X) / (extentMax.X - extentMin.X);
            var ny = 1f - (world.Y - extentMin.Y) / (extentMax.Y - extentMin.Y);
            return new SKPoint(offsetX + nx * previewSize, offsetY + ny * previewSize);
        }

        float WorldRadiusToPixels(float worldRadius)
        {
            var mapWidthWorld = extentMax.X - extentMin.X;
            return worldRadius / mapWidthWorld * previewSize;
        }

        var scenario = _vm!.Scenario;
        var selectedSlotIndex = _vm.SelectedSlotIndex;

        for (var slotIdx = 0; slotIdx < scenario.Players.Count; slotIdx++)
        {
            var player = scenario.Players[slotIdx];
            var color = slotIdx < SlotColors.Length ? SlotColors[slotIdx] : AnchorColor;
            var isSelected = slotIdx == selectedSlotIndex;

            // Player start anchor
            var startWorld = map.PlayerStart(slotIdx);
            var startScreen = WorldToScreen(new Vector2(startWorld.X, startWorld.Y));

            using var anchorPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
            canvas.DrawCircle(startScreen, isSelected ? 7f : 5f, anchorPaint);

            using var textPaint = new SKPaint { Color = color };
            using var textFont = new SKFont { Size = 12 };
            canvas.DrawText($"P{slotIdx + 1}", startScreen.X + 6, startScreen.Y - 4, textFont, textPaint);

            // Action markers
            for (var actionIdx = 0; actionIdx < player.Actions.Count; actionIdx++)
            {
                DrawActionMarker(canvas, player.Actions[actionIdx], actionIdx, slotIdx, map,
                    WorldToScreen, WorldRadiusToPixels, color, isSelected,
                    scenario.BaseRadiusWorldUnits);
            }
        }
    }

    private static void DrawActionMarker(
        SKCanvas canvas,
        ActionEntry action,
        int actionIdx,
        int slotIdx,
        MapMetadataService map,
        Func<Vector2, SKPoint> worldToScreen,
        Func<float, float> worldRadiusToPixels,
        SKColor color,
        bool isSelected,
        float baseRadius)
    {
        if (action.Position == null) return;

        var label = $"{actionIdx + 1}";

        using var dotPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
        using var circlePaint = new SKPaint { Color = CircleColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        using var linePaint = new SKPaint { Color = LineColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var arcFillPaint = new SKPaint { Color = ArcColor, Style = SKPaintStyle.Fill };
        using var selPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var textPaint = new SKPaint { Color = color };
        using var textFont = new SKFont { Size = 11 };

        switch (action.Position)
        {
            case NormalizedPosition np:
                {
                    var wx = map.ExtentMin.X + np.NormX * (map.ExtentMax.X - map.ExtentMin.X);
                    var wy = map.ExtentMin.Y + np.NormY * (map.ExtentMax.Y - map.ExtentMin.Y);
                    var screen = worldToScreen(new Vector2(wx, wy));
                    canvas.DrawCircle(screen, isSelected ? 5f : 3f, dotPaint);
                    if (isSelected) canvas.DrawCircle(screen, 7f, selPaint);
                    canvas.DrawText(label, screen.X + 6, screen.Y - 4, textFont, textPaint);
                    break;
                }

            case LandmarkRelativePosition lrp:
                {
                    var anchor = GetLandmarkWorld(lrp.Landmark, slotIdx, 1 - slotIdx, map);
                    var anchorScreen = worldToScreen(new Vector2(anchor.X, anchor.Y));
                    var distPixels = worldRadiusToPixels(lrp.DistanceInBaseWidths * baseRadius);

                    // Distance circle
                    canvas.DrawCircle(anchorScreen, distPixels, circlePaint);

                    switch (lrp.Angle)
                    {
                        case FixedAngle fa:
                            {
                                var rad = fa.Degrees * MathF.PI / 180f;
                                var tip = new SKPoint(
                                    anchorScreen.X + MathF.Cos(rad) * distPixels,
                                    anchorScreen.Y - MathF.Sin(rad) * distPixels);
                                canvas.DrawLine(anchorScreen, tip, linePaint);
                                canvas.DrawCircle(tip, isSelected ? 5f : 3f, dotPaint);
                                if (isSelected) canvas.DrawCircle(tip, 7f, selPaint);
                                canvas.DrawText(label, tip.X + 6, tip.Y - 4, textFont, textPaint);
                                break;
                            }

                        case RandomAngle ra:
                            {
                                DrawArcSector(canvas, anchorScreen, distPixels,
                                    ra.MinDegrees, ra.MaxDegrees, arcFillPaint, dotPaint);
                                var midRad = (ra.MinDegrees + ra.MaxDegrees) * 0.5f * MathF.PI / 180f;
                                var midTip = new SKPoint(
                                    anchorScreen.X + MathF.Cos(midRad) * distPixels,
                                    anchorScreen.Y - MathF.Sin(midRad) * distPixels);
                                canvas.DrawText(label, midTip.X + 4, midTip.Y - 4, textFont, textPaint);
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
        SKCanvas canvas,
        SKPoint center,
        float radius,
        float minDeg,
        float maxDeg,
        SKPaint fillPaint,
        SKPaint edgePaint)
    {
        const int segments = 24;
        var minRad = minDeg * MathF.PI / 180f;
        var maxRad = maxDeg * MathF.PI / 180f;
        var step = (maxRad - minRad) / segments;

        using var path = new SKPath();
        path.MoveTo(center);
        for (var i = 0; i <= segments; i++)
        {
            var a = minRad + i * step;
            path.LineTo(center.X + MathF.Cos(a) * radius, center.Y - MathF.Sin(a) * radius);
        }
        path.Close();
        canvas.DrawPath(path, fillPaint);

        using var outlinePaint = new SKPaint { Color = edgePaint.Color, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f };
        var minTip = new SKPoint(center.X + MathF.Cos(minRad) * radius, center.Y - MathF.Sin(minRad) * radius);
        var maxTip = new SKPoint(center.X + MathF.Cos(maxRad) * radius, center.Y - MathF.Sin(maxRad) * radius);
        canvas.DrawLine(center, minTip, outlinePaint);
        canvas.DrawLine(center, maxTip, outlinePaint);
        canvas.DrawCircle(minTip, 3f, edgePaint);
        canvas.DrawCircle(maxTip, 3f, edgePaint);
    }
}
