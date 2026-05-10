using System;
using System.Numerics;
using ImGuiNET;
using OpenSage.Tools.ReplaySketch.Model;

namespace OpenSage.Tools.ReplaySketch.UI;

internal sealed class ActionSequencePanel
{
    private static readonly string[] ActionTypeLabels =
        ["Build Barracks", "Recruit Basic Unit", "Attack Enemy Base"];

    private static readonly ActionType[] ActionTypeValues =
        [ActionType.BuildBarracks, ActionType.RecruitBasicUnit, ActionType.AttackEnemyBase];

    private static readonly string[] LandmarkLabels =
        ["Own Base", "Enemy Base", "Map Center"];

    private static readonly LandmarkType[] LandmarkValues =
        [LandmarkType.OwnBase, LandmarkType.EnemyBase, LandmarkType.MapCenter];

    public void Draw(PlayerSlotConfig player)
    {
        ImGui.BeginChild("ActionSequencePanel", new Vector2(0, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);

        ImGui.SeparatorText($"Actions — {player.Name}");

        for (var actionIdx = 0; actionIdx < player.Actions.Count; actionIdx++)
        {
            var action = player.Actions[actionIdx];
            var nodeId = $"action_{actionIdx}";

            var open = ImGui.CollapsingHeader($"{action.Label}##{nodeId}", ImGuiTreeNodeFlags.DefaultOpen);
            if (!open) continue;

            ImGui.Indent();
            ImGui.PushID(nodeId);

            // ── Action type ───────────────────────────────────────
            var typeIdx = Array.IndexOf(ActionTypeValues, action.Type);
            if (typeIdx < 0) typeIdx = 0;
            if (ImGui.Combo("Action##type", ref typeIdx, ActionTypeLabels, ActionTypeLabels.Length))
            {
                var newType = ActionTypeValues[typeIdx];
                if (newType != action.Type)
                {
                    action.Type = newType;
                    // RecruitBasicUnit has no map position; clear it.
                    // All other types need a position; restore a default if currently null.
                    if (newType == ActionType.RecruitBasicUnit)
                    {
                        action.Position = null;
                    }
                    else if (action.Position == null)
                    {
                        action.Position = new LandmarkRelativePosition(
                            LandmarkType.OwnBase, 2.0f, new FixedAngle(45f));
                    }
                }
            }

            // ── Label ─────────────────────────────────────────────
            var labelBuf = System.Text.Encoding.UTF8.GetBytes(action.Label + "\0\0\0\0\0\0\0\0");
            if (ImGui.InputText("Label##lbl", labelBuf, (uint)labelBuf.Length))
            {
                action.Label = System.Text.Encoding.UTF8.GetString(labelBuf).TrimEnd('\0');
            }

            ImGui.Spacing();

            // ── Position mode ────────────────────────────────────
            if (action.Position != null)
            {
                ImGui.SeparatorText("Position");

                var isNormalized = action.Position is NormalizedPosition;
                var modeNorm = isNormalized;
                var modeLandmark = !isNormalized;

                if (ImGui.RadioButton("Map Normalized##pm", modeNorm) && !modeNorm)
                {
                    action.Position = new NormalizedPosition(0.5f, 0.5f);
                }
                ImGui.SameLine();
                if (ImGui.RadioButton("Landmark Relative##pm", modeLandmark) && !modeLandmark)
                {
                    action.Position = new LandmarkRelativePosition(
                        LandmarkType.OwnBase, 2.0f, new FixedAngle(45f));
                }

                ImGui.Spacing();

                if (action.Position is NormalizedPosition np)
                {
                    var normX = np.NormX;
                    var normY = np.NormY;
                    var changed = false;
                    if (ImGui.DragFloat("X (0–1)##nx", ref normX, 0.005f, 0f, 1f, "%.3f")) changed = true;
                    if (ImGui.DragFloat("Y (0–1)##ny", ref normY, 0.005f, 0f, 1f, "%.3f")) changed = true;
                    if (changed) action.Position = new NormalizedPosition(normX, normY);
                }
                else if (action.Position is LandmarkRelativePosition lrp)
                {
                    // Landmark combo
                    var lmIdx = Array.IndexOf(LandmarkValues, lrp.Landmark);
                    if (lmIdx < 0) lmIdx = 0;
                    LandmarkType newLandmark = lrp.Landmark;
                    if (ImGui.Combo("Landmark##lm", ref lmIdx, LandmarkLabels, LandmarkLabels.Length))
                    {
                        newLandmark = LandmarkValues[lmIdx];
                    }

                    // Distance
                    var dist = lrp.DistanceInBaseWidths;
                    if (ImGui.DragFloat("Distance (base widths)##dist", ref dist, 0.1f, 0.1f, 20f, "%.2f"))
                    {
                        dist = MathF.Max(0.1f, dist);
                    }

                    // Angle sub-section
                    ImGui.Spacing();
                    ImGui.TextDisabled("Angle");

                    var isFixed = lrp.Angle is FixedAngle;
                    AngleConfig newAngle = lrp.Angle;

                    if (ImGui.RadioButton("Fixed##ang", isFixed) && !isFixed)
                    {
                        newAngle = new FixedAngle(0f);
                    }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("Random range##ang", !isFixed) && isFixed)
                    {
                        newAngle = new RandomAngle(0f, 360f);
                    }

                    if (lrp.Angle is FixedAngle fa)
                    {
                        var deg = fa.Degrees;
                        if (ImGui.DragFloat("Degrees##fdeg", ref deg, 1f, -360f, 360f, "%.1f°"))
                        {
                            newAngle = new FixedAngle(deg);
                        }
                    }
                    else if (lrp.Angle is RandomAngle ra)
                    {
                        var minDeg = ra.MinDegrees;
                        var maxDeg = ra.MaxDegrees;
                        var changed = false;
                        if (ImGui.DragFloat("Min°##rmin", ref minDeg, 1f, -360f, 360f, "%.1f°")) changed = true;
                        if (ImGui.DragFloat("Max°##rmax", ref maxDeg, 1f, -360f, 360f, "%.1f°")) changed = true;
                        if (changed)
                        {
                            if (minDeg > maxDeg) maxDeg = minDeg;
                            newAngle = new RandomAngle(minDeg, maxDeg);
                        }
                    }

                    // Commit any changes
                    if (newLandmark != lrp.Landmark || dist != lrp.DistanceInBaseWidths || newAngle != lrp.Angle)
                    {
                        action.Position = new LandmarkRelativePosition(newLandmark, dist, newAngle);
                    }
                }

            } // end Position block

            ImGui.Spacing();

            // ── Timing ────────────────────────────────────────────
            ImGui.SeparatorText("Timing");

            var isFixedTiming = action.Timing is FixedTiming;

            if (ImGui.RadioButton("Fixed##tim", isFixedTiming) && !isFixedTiming)
            {
                action.Timing = new FixedTiming(300);
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("Random range##tim", !isFixedTiming) && isFixedTiming)
            {
                action.Timing = new RandomTiming(150, 450);
            }

            if (action.Timing is FixedTiming ft)
            {
                var frames = (int)ft.Frames;
                if (ImGui.InputInt("Gap (frames)##ftf", ref frames))
                {
                    action.Timing = new FixedTiming((uint)Math.Max(1, frames));
                }
                ImGui.SetItemTooltip("Frames after the previous action before this one fires.\n30 frames ≈ 1 second at normal game speed.");
            }
            else if (action.Timing is RandomTiming rt)
            {
                var minF = (int)rt.MinFrames;
                var maxF = (int)rt.MaxFrames;
                var changed = false;
                if (ImGui.InputInt("Min gap (frames)##rtmin", ref minF)) changed = true;
                if (ImGui.InputInt("Max gap (frames)##rtmax", ref maxF)) changed = true;
                if (changed)
                {
                    minF = Math.Max(1, minF);
                    maxF = Math.Max(minF, maxF);
                    action.Timing = new RandomTiming((uint)minF, (uint)maxF);
                }
            }

            ImGui.PopID();
            ImGui.Unindent();
            ImGui.Spacing();
        }

        ImGui.EndChild();
    }
}
