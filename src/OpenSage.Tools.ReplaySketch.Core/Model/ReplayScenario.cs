using System.Collections.Generic;

namespace OpenSage.Tools.ReplaySketch.Model;

public sealed class ReplayScenario
{
    /// <summary>
    /// Map file path as stored in the replay metadata,
    /// e.g. <c>"maps/alpine assault/alpine assault.map"</c>.
    /// </summary>
    public string MapPath { get; set; } = string.Empty;

    /// <summary>
    /// World-unit radius used as "1 base width" for landmark-relative positions.
    /// Roughly the footprint radius of a Command Center (~120 world units).
    /// </summary>
    public float BaseRadiusWorldUnits { get; set; } = 120f;

    public List<PlayerSlotConfig> Players { get; set; } = new();

    // -----------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------

    /// <summary>
    /// Returns a ready-to-edit Alpine Assault scenario: USA (slot 0) vs GLA (slot 1),
    /// each pre-populated with 3 actions.
    /// </summary>
    public static ReplayScenario CreateAlpineAssaultUSAvGLA() => new()
    {
        MapPath = "maps/alpine assault/alpine assault.map",
        BaseRadiusWorldUnits = 120f,
        Players =
        [
            PlayerSlotConfig.CreateUSA(),
            PlayerSlotConfig.CreateGLA(),
        ],
    };
}
