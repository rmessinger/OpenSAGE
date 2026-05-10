using System;
using System.Numerics;
using ImGuiNET;
using OpenSage.Tools.ReplaySketch.Model;
using OpenSage.Tools.ReplaySketch.Services;
using Veldrid;
using Veldrid.Sdl2;

namespace OpenSage.Tools.ReplaySketch.UI;

internal sealed class MainForm : DisposableBase
{
    private readonly GraphicsDevice _gd;
    private readonly ImGuiRenderer _imguiRenderer;

    private readonly ReplayScenario _scenario;
    private MapMetadataService? _map;
    private string? _mapLoadError;

    private int _selectedSlotIndex;

    private readonly GeneralsInstallationService _installationService = new();
    private readonly LauncherLocatorService _launcherLocator = new();
    private readonly ScenarioPanel _scenarioPanel = new();
    private readonly MapPreviewPanel _previewPanel = new();
    private readonly ActionSequencePanel _actionPanel = new();
    private readonly ExportPanel _exportPanel = new();

    private bool _mapDirty;

    public MainForm(GraphicsDevice gd, ImGuiRenderer imguiRenderer)
    {
        _gd = gd;
        _imguiRenderer = imguiRenderer;
        _scenario = ReplayScenario.CreateAlpineAssaultUSAvGLA();
        _selectedSlotIndex = 0;
        _mapDirty = true; // trigger initial map load
    }

    public void Draw(Sdl2Window window)
    {
        // Try to load/reload the map when the path changes
        if (_mapDirty)
        {
            TryLoadMap();
            _mapDirty = false;
        }

        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always, Vector2.Zero);
        ImGui.SetNextWindowSize(new Vector2(window.Width, window.Height), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);

        var windowOpen = false;
        ImGui.Begin("OpenSAGE Replay Sketch", ref windowOpen,
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBringToFrontOnFocus);

        DrawMenuBar();

        // Remember previous map path to detect changes
        var mapPathBefore = _scenario.MapPath;

        // ── Layout: left | centre | right ────────────────────────────────
        // The export strip is drawn below the three-column region.
        var availableHeight = ImGui.GetContentRegionAvail().Y;
        const float exportStripHeight = 130f;
        var columnsHeight = availableHeight - exportStripHeight - ImGui.GetStyle().ItemSpacing.Y;

        ImGui.BeginChild("##cols", new Vector2(0, columnsHeight), ImGuiChildFlags.None, ImGuiWindowFlags.None);
        {
            // ── Left column: Scenario / slot selection ────────────────────
            _scenarioPanel.Draw(_scenario, ref _selectedSlotIndex);
            ImGui.SameLine();

            // ── Centre column: Map preview ────────────────────────────────
            _previewPanel.Draw(_scenario, _map, _selectedSlotIndex);
            ImGui.SameLine();

            // ── Right column: Action sequence (filtered to selected slot) ──
            var player = _scenario.Players[_selectedSlotIndex];
            _actionPanel.Draw(player);
        }
        ImGui.EndChild();

        // ── Bottom strip: validate & export ──────────────────────────────
        _exportPanel.Draw(_scenario, _map, _launcherLocator);

        ImGui.End();
        ImGui.PopStyleVar();

        // Detect map path change to trigger reload
        if (_scenario.MapPath != mapPathBefore)
        {
            _mapDirty = true;
        }
    }

    private void DrawMenuBar()
    {
        if (!ImGui.BeginMenuBar()) return;

        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("New (Alpine Assault USA vs GLA)"))
            {
                _scenario.Players.Clear();
                var fresh = ReplayScenario.CreateAlpineAssaultUSAvGLA();
                _scenario.MapPath = fresh.MapPath;
                _scenario.BaseRadiusWorldUnits = fresh.BaseRadiusWorldUnits;
                foreach (var p in fresh.Players) _scenario.Players.Add(p);
                _selectedSlotIndex = 0;
                _mapDirty = true;
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Exit", "Alt+F4"))
            {
                // Requesting window close is handled by the OS; nothing extra needed here.
                Environment.Exit(0);
            }

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Help"))
        {
            if (ImGui.MenuItem("About"))
            {
                // Could open a modal — keep simple for now
            }
            ImGui.EndMenu();
        }

        if (_mapLoadError != null)
        {
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 350);
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"Map: {_mapLoadError}");
        }
        else if (_map != null)
        {
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 250);
            ImGui.TextDisabled($"Map loaded  ({_map.ExtentMax.X - _map.ExtentMin.X:F0} × {_map.ExtentMax.Y - _map.ExtentMin.Y:F0} wu)");
        }

        ImGui.EndMenuBar();
    }

    private void TryLoadMap()
    {
        _map = null;
        _mapLoadError = null;

        if (!_installationService.TryGetInstallation(out var installation) || installation is null)
        {
            _mapLoadError = "Generals not found";
            return;
        }

        try
        {
            _map = MapMetadataService.Load(installation, _scenario.MapPath);
        }
        catch (Exception ex)
        {
            _mapLoadError = ex.Message;
        }
    }
}
