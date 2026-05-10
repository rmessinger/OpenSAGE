using System;
using System.Numerics;
using ImGuiNET;
using OpenSage.Tools.ReplaySketch.Model;

namespace OpenSage.Tools.ReplaySketch.UI;

internal sealed class ScenarioPanel
{
    // Known maps (expand as needed)
    private static readonly string[] MapLabels = ["Alpine Assault"];
    private static readonly string[] MapPaths  = ["maps/alpine assault/alpine assault.map"];

    private static readonly string[] FactionLabels =
        ["USA (2)", "China (3)", "GLA (4)", "USA Gen (5)", "Toxin (6)", "Demo (7)",
         "Stealth (8)", "Nuke (9)", "Laser (10)", "Air Force (11)", "Super Weapon (12)", "Tank (13)"];

    private static readonly int[] FactionIndices = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13];

    public void Draw(ReplayScenario scenario, ref int selectedSlotIndex)
    {
        ImGui.BeginChild("ScenarioPanel", new Vector2(230, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);

        // ── Map selection ─────────────────────────────────────────
        ImGui.SeparatorText("Map");

        var mapIdx = Array.IndexOf(MapPaths, scenario.MapPath);
        if (mapIdx < 0) mapIdx = 0;

        if (ImGui.Combo("##map", ref mapIdx, MapLabels, MapLabels.Length))
        {
            scenario.MapPath = MapPaths[mapIdx];
        }

        // ── Map settings ──────────────────────────────────────────
        ImGui.SeparatorText("Map Settings");

        var baseRadius = scenario.BaseRadiusWorldUnits;
        if (ImGui.InputFloat("Base radius (wu)", ref baseRadius, 10f, 50f, "%.0f"))
        {
            scenario.BaseRadiusWorldUnits = Math.Max(1f, baseRadius);
        }
        ImGui.SetItemTooltip("World-unit radius treated as '1 base width'.\nDefault ≈ 120 (Command Center footprint).");

        // ── Player slots ──────────────────────────────────────────
        ImGui.SeparatorText("Player Slots");

        for (var i = 0; i < scenario.Players.Count; i++)
        {
            var player = scenario.Players[i];
            var isSelected = i == selectedSlotIndex;

            // Selectable row header
            var label = $"Slot {i}: {player.Name}##slot{i}";
            if (ImGui.Selectable(label, isSelected, ImGuiSelectableFlags.None, new Vector2(0, 0)))
            {
                selectedSlotIndex = i;
            }

            // Expand fields only for the selected slot
            if (isSelected)
            {
                ImGui.Indent();

                // Name
                var nameBuf = System.Text.Encoding.UTF8.GetBytes(player.Name + "\0\0\0\0\0\0\0\0");
                if (ImGui.InputText($"Name##n{i}", nameBuf, (uint)nameBuf.Length))
                {
                    player.Name = System.Text.Encoding.UTF8.GetString(nameBuf).TrimEnd('\0');
                }

                // Faction
                var factionIdx = Array.IndexOf(FactionIndices, player.FactionIndex);
                if (factionIdx < 0) factionIdx = 0;
                if (ImGui.Combo($"Faction##f{i}", ref factionIdx, FactionLabels, FactionLabels.Length))
                {
                    player.FactionIndex = FactionIndices[factionIdx];
                }

                // Color
                var color = (int)player.Color;
                if (ImGui.InputInt($"Color##c{i}", ref color))
                {
                    player.Color = (sbyte)Math.Clamp(color, -1, 15);
                }

                // Start position
                var startPos = player.StartPosition;
                if (ImGui.InputInt($"Start pos##sp{i}", ref startPos))
                {
                    player.StartPosition = Math.Max(1, startPos);
                }

                // Team
                var team = player.Team;
                if (ImGui.InputInt($"Team##t{i}", ref team))
                {
                    player.Team = Math.Max(1, team);
                }

                ImGui.Unindent();
                ImGui.Spacing();
            }
        }

        ImGui.EndChild();
    }
}
