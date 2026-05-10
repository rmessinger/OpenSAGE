using System.Collections.Generic;

namespace OpenSage.Tools.ReplaySketch.Model;

public sealed class PlayerSlotConfig
{
    public string Name { get; set; }

    /// <summary>
    /// Index into the game's PlayerTemplates list.
    /// USA = 2, GLA = 4 (Generals defaults).
    /// </summary>
    public int FactionIndex { get; set; }

    public sbyte Color { get; set; }

    /// <summary>1-based start position on the map (matches Player_N_Start waypoint naming).</summary>
    public int StartPosition { get; set; }

    /// <summary>1-based team number.</summary>
    public int Team { get; set; }

    public List<ActionEntry> Actions { get; set; } = new();

    // -----------------------------------------------------------------
    // Faction factories
    // -----------------------------------------------------------------

    /// <summary>
    /// Creates a USA slot pre-populated with 3 default actions for Alpine Assault.
    /// </summary>
    public static PlayerSlotConfig CreateUSA() => new()
    {
        Name = "Player1",
        FactionIndex = 2,
        Color = 0,
        StartPosition = 2,
        Team = 1,
        Actions =
        [
            new ActionEntry(
                "Build Barracks",
                ActionType.BuildBarracks,
                new LandmarkRelativePosition(LandmarkType.OwnBase, 2.0f, new FixedAngle(45f)),
                new FixedTiming(300)),

            new ActionEntry(
                "Recruit Ranger",
                ActionType.RecruitBasicUnit,
                null,
                new FixedTiming(600)),

            new ActionEntry(
                "Attack Enemy Base",
                ActionType.AttackEnemyBase,
                new LandmarkRelativePosition(LandmarkType.EnemyBase, 1.0f, new RandomAngle(150f, 210f)),
                new FixedTiming(900)),
        ],
    };

    /// <summary>
    /// Creates a GLA slot pre-populated with 3 default actions for Alpine Assault.
    /// </summary>
    public static PlayerSlotConfig CreateGLA() => new()
    {
        Name = "Player2",
        FactionIndex = 4,
        Color = 1,
        StartPosition = 1,
        Actions =
        [
            new ActionEntry(
                "Build Barracks",
                ActionType.BuildBarracks,
                new LandmarkRelativePosition(LandmarkType.OwnBase, 2.0f, new FixedAngle(225f)),
                new FixedTiming(300)),

            new ActionEntry(
                "Recruit Rebel",
                ActionType.RecruitBasicUnit,
                null,
                new FixedTiming(600)),

            new ActionEntry(
                "Attack Enemy Base",
                ActionType.AttackEnemyBase,
                new LandmarkRelativePosition(LandmarkType.EnemyBase, 1.0f, new RandomAngle(-30f, 30f)),
                new FixedTiming(900)),
        ],
    };
}
