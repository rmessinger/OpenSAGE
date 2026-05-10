using System.Collections.ObjectModel;
using OpenSage.Tools.ReplaySketch.Maui.ViewModels;
using OpenSage.Tools.ReplaySketch.Model;

namespace OpenSage.Tools.ReplaySketch.Maui.Views;

public partial class ScenarioView : ContentView
{
    private static readonly string[] MapLabels = ["Alpine Assault"];
    private static readonly string[] MapPaths = ["maps/alpine assault/alpine assault.map"];

    private static readonly string[] FactionLabels =
        ["USA (2)", "China (3)", "GLA (4)", "USA Gen (5)", "Toxin (6)", "Demo (7)",
         "Stealth (8)", "Nuke (9)", "Laser (10)", "Air Force (11)", "Super Weapon (12)", "Tank (13)"];

    private static readonly int[] FactionIndices = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13];

    private MainViewModel? _vm;
    private bool _suppressCallbacks;

    public ScenarioView()
    {
        InitializeComponent();

        MapPicker.ItemsSource = MapLabels;
        FactionPicker.ItemsSource = FactionLabels;
    }

    public void BindViewModel(MainViewModel vm)
    {
        _vm = vm;
        Refresh();
    }

    public void Refresh()
    {
        if (_vm == null) return;
        _suppressCallbacks = true;

        var scenario = _vm.Scenario;

        // Map
        var mapIdx = Array.IndexOf(MapPaths, scenario.MapPath);
        MapPicker.SelectedIndex = mapIdx >= 0 ? mapIdx : 0;

        // Base radius
        BaseRadiusEntry.Text = scenario.BaseRadiusWorldUnits.ToString("F0");

        // Player slots
        var slotItems = new ObservableCollection<string>(
            scenario.Players.Select((p, i) => $"Slot {i}: {p.Name}"));
        SlotsCollectionView.ItemsSource = slotItems;

        RefreshSelectedSlot();
        _suppressCallbacks = false;
    }

    private void RefreshSelectedSlot()
    {
        if (_vm == null) return;
        var player = _vm.SelectedPlayer;
        var slotIdx = _vm.SelectedSlotIndex;

        SlotsCollectionView.SelectedItem =
            SlotsCollectionView.ItemsSource is ObservableCollection<string> items && slotIdx < items.Count
                ? items[slotIdx]
                : null;

        SlotDetailPanel.IsVisible = true;

        PlayerNameEntry.Text = player.Name;

        var factionIdx = Array.IndexOf(FactionIndices, player.FactionIndex);
        FactionPicker.SelectedIndex = factionIdx >= 0 ? factionIdx : 0;

        ColorEntry.Text = player.Color.ToString();
        StartPosEntry.Text = player.StartPosition.ToString();
        TeamEntry.Text = player.Team.ToString();
    }

    // ── Event handlers ────────────────────────────────────────────────────

    private void OnMapPickerChanged(object? sender, EventArgs e)
    {
        if (_suppressCallbacks || _vm == null) return;
        var idx = MapPicker.SelectedIndex;
        if (idx >= 0 && idx < MapPaths.Length && _vm.Scenario.MapPath != MapPaths[idx])
        {
            _vm.Scenario.MapPath = MapPaths[idx];
            _vm.LoadMap();
        }
    }

    private void OnBaseRadiusCompleted(object? sender, EventArgs e) => CommitBaseRadius();
    private void OnBaseRadiusUnfocused(object? sender, FocusEventArgs e) => CommitBaseRadius();
    private void CommitBaseRadius()
    {
        if (_suppressCallbacks || _vm == null) return;
        if (float.TryParse(BaseRadiusEntry.Text, out var v) && v > 0)
            _vm.Scenario.BaseRadiusWorldUnits = v;
    }

    private void OnSlotSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressCallbacks || _vm == null) return;
        if (SlotsCollectionView.ItemsSource is ObservableCollection<string> items)
        {
            var idx = e.CurrentSelection.Count > 0
                ? items.IndexOf((string)e.CurrentSelection[0])
                : -1;
            if (idx >= 0)
            {
                _vm.SelectedSlotIndex = idx;
                _suppressCallbacks = true;
                RefreshSelectedSlot();
                _suppressCallbacks = false;
            }
        }
    }

    private void OnPlayerNameCompleted(object? sender, EventArgs e) => CommitPlayerName();
    private void OnPlayerNameUnfocused(object? sender, FocusEventArgs e) => CommitPlayerName();
    private void CommitPlayerName()
    {
        if (_suppressCallbacks || _vm == null) return;
        _vm.SelectedPlayer.Name = PlayerNameEntry.Text ?? string.Empty;
    }

    private void OnFactionPickerChanged(object? sender, EventArgs e)
    {
        if (_suppressCallbacks || _vm == null) return;
        var idx = FactionPicker.SelectedIndex;
        if (idx >= 0 && idx < FactionIndices.Length)
            _vm.SelectedPlayer.FactionIndex = FactionIndices[idx];
    }

    private void OnColorCompleted(object? sender, EventArgs e) => CommitColor();
    private void OnColorUnfocused(object? sender, FocusEventArgs e) => CommitColor();
    private void CommitColor()
    {
        if (_suppressCallbacks || _vm == null) return;
        if (sbyte.TryParse(ColorEntry.Text, out var v))
            _vm.SelectedPlayer.Color = v;
    }

    private void OnStartPosCompleted(object? sender, EventArgs e) => CommitStartPos();
    private void OnStartPosUnfocused(object? sender, FocusEventArgs e) => CommitStartPos();
    private void CommitStartPos()
    {
        if (_suppressCallbacks || _vm == null) return;
        if (int.TryParse(StartPosEntry.Text, out var v) && v > 0)
            _vm.SelectedPlayer.StartPosition = v;
    }

    private void OnTeamCompleted(object? sender, EventArgs e) => CommitTeam();
    private void OnTeamUnfocused(object? sender, FocusEventArgs e) => CommitTeam();
    private void CommitTeam()
    {
        if (_suppressCallbacks || _vm == null) return;
        if (int.TryParse(TeamEntry.Text, out var v) && v > 0)
            _vm.SelectedPlayer.Team = v;
    }
}
