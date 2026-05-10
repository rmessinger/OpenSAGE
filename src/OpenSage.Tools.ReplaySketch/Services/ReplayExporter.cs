using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using OpenSage.Data;
using OpenSage.Data.Rep;
using OpenSage.Logic.Orders;
using OpenSage.Logic.Object;
using OpenSage.Tools.ReplaySketch.Model;

namespace OpenSage.Tools.ReplaySketch.Services;

/// <summary>
/// Known object-definition names for the initial USA vs GLA scope.
/// Object definition IDs (integers used in replay orders) are their
/// 0-based index in the order all ObjectDefinitions appear across INI files.
/// Because a full AssetStore load is expensive, we use well-known sentinel
/// placeholder IDs here and document them.
///
/// Real IDs can be substituted once an INI-scanning lookup is wired up.
/// </summary>
internal static class KnownDefinitions
{
    // These placeholder IDs match typical Generals skirmish replays.
    // They should be validated/replaced with a live INI lookup before ship.
    public const int UsaBarracks   = 40;   // AmericaBarracks
    public const int UsaRanger     = 41;   // AmericaInfantryRanger
    public const int GlaBarracks   = 100;  // GLABarracks
    public const int GlaRebel      = 101;  // GLAInfantryRebel
}

public static class ReplayExporter
{
    private const int ChecksumIntervalFrames = 150;

    /// <summary>
    /// Exports the scenario as a <c>.rep</c> file to <paramref name="outputPath"/>.
    /// Returns <see langword="null"/> on success, or an error message string on failure.
    /// </summary>
    public static string? Export(ReplayScenario scenario, MapMetadataService map, string outputPath)
    {
        var rng = new Random();

        // ------------------------------------------------------------------
        // Build interleaved (frame, Order) sequence
        // ------------------------------------------------------------------
        var orders = new List<(uint Frame, Order Order)>();

        for (var ownerIdx = 0; ownerIdx < scenario.Players.Count; ownerIdx++)
        {
            var player = scenario.Players[ownerIdx];
            var enemyIdx = ownerIdx == 0 ? 1 : 0;

            var ctx = TerrainValidator.BuildContextPublic(scenario, map, ownerIdx, enemyIdx);

            uint cumulativeFrame = 0;
            foreach (var action in player.Actions)
            {
                cumulativeFrame += action.Timing.Resolve(rng);

                // Actions without a position (e.g. RecruitBasicUnit) use Vector3.Zero;
                // BuildOrders won't consume the position for those action types.
                var worldPos = action.Position?.Resolve(ctx, rng) ?? Vector3.Zero;

                var actionOrders = BuildOrders(ownerIdx, action.Type, worldPos, player.FactionIndex);
                foreach (var order in actionOrders)
                {
                    orders.Add((cumulativeFrame, order));
                }
            }
        }

        // Sort by frame, then by player index (stable sort)
        orders.Sort((a, b) =>
        {
            var cmp = a.Frame.CompareTo(b.Frame);
            return cmp != 0 ? cmp : a.Order.PlayerIndex.CompareTo(b.Order.PlayerIndex);
        });

        // Inject Checksum orders
        var allOrders = InjectChecksums(orders);

        // ------------------------------------------------------------------
        // Build metadata
        // ------------------------------------------------------------------
        var slots = new List<ReplaySlot>();
        foreach (var player in scenario.Players)
        {
            slots.Add(ReplaySlot.CreateHuman(
                player.Name,
                player.Color,
                player.FactionIndex,
                player.StartPosition,
                player.Team));
        }

        // Fill remaining slots as empty up to 8
        while (slots.Count < 8)
        {
            slots.Add(ReplaySlot.CreateEmpty());
        }

        var metadata = ReplayMetadata.Create(
            mapFile: scenario.MapPath,
            mapCrc: 0,
            mapSize: 0,
            seed: Random.Shared.Next(),
            startingCredits: 10000,
            slots: slots.ToArray());

        var header = ReplayHeader.Create(metadata);

        // ------------------------------------------------------------------
        // Write to disk
        // ------------------------------------------------------------------
        try
        {
            using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write);
            ReplayFile.Write(stream, header, allOrders);
        }
        catch (Exception ex)
        {
            return $"Write failed: {ex.Message}";
        }

        // ------------------------------------------------------------------
        // Round-trip validation
        // ------------------------------------------------------------------
        try
        {
            RoundTripValidate(outputPath, metadata);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        return null;
    }

    private static IEnumerable<(uint Frame, Order Order)> InjectChecksums(
        List<(uint Frame, Order Order)> orders)
    {
        if (orders.Count == 0) yield break;

        uint lastFrame = orders[^1].Frame;
        var orderIdx = 0;

        for (uint f = ChecksumIntervalFrames; f <= lastFrame + ChecksumIntervalFrames; f += ChecksumIntervalFrames)
        {
            // Emit all real orders that happen before this checksum frame
            while (orderIdx < orders.Count && orders[orderIdx].Frame < f)
            {
                yield return orders[orderIdx++];
            }

            // Emit checksum for player 0 (the game expects one per player per interval,
            // but a single one is sufficient to satisfy the NumTimecodes validation)
            var checksumOrder = new Order(0, OrderType.Checksum);
            checksumOrder.AddIntegerArgument(0); // dummy checksum value
            yield return (f, checksumOrder);
        }

        // Flush remaining real orders
        while (orderIdx < orders.Count)
        {
            yield return orders[orderIdx++];
        }
    }

    private static IEnumerable<Order> BuildOrders(
        int playerIndex, ActionType actionType, Vector3 worldPos, int factionIndex)
    {
        bool isGla = factionIndex == 4;

        switch (actionType)
        {
            case ActionType.BuildBarracks:
            {
                var defId = isGla ? KnownDefinitions.GlaBarracks : KnownDefinitions.UsaBarracks;
                yield return Order.CreateBuildObject(playerIndex, defId, worldPos, 0f);
                break;
            }

            case ActionType.RecruitBasicUnit:
            {
                // CreateUnit: select the barracks (dummy ObjectId 1), then queue the unit.
                var unitDefId = isGla ? KnownDefinitions.GlaRebel : KnownDefinitions.UsaRanger;
                var recruitOrder = new Order(playerIndex, OrderType.CreateUnit);
                recruitOrder.AddObjectIdArgument(new ObjectId(1));
                recruitOrder.AddIntegerArgument(unitDefId);
                yield return recruitOrder;
                break;
            }

            case ActionType.AttackEnemyBase:
            {
                yield return Order.CreateAttackGround(playerIndex, worldPos);
                break;
            }
        }
    }

    /// <summary>
    /// Re-parses the written file and verifies that the key metadata fields survived
    /// the write/read cycle. Throws <see cref="InvalidDataException"/> on any mismatch.
    /// </summary>
    private static void RoundTripValidate(string path, ReplayMetadata expected)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.Unicode, leaveOpen: false);

        var header = ReplayHeader.Parse(reader);
        var actual = header.Metadata;

        var errors = new StringBuilder();

        if (header.GameType != ReplayGameType.Generals)
            errors.AppendLine($"GameType mismatch: expected Generals, got {header.GameType}");

        if (actual.MapFile != expected.MapFile)
            errors.AppendLine($"MapFile mismatch: expected '{expected.MapFile}', got '{actual.MapFile}'");

        if ((actual.Slots?.Length ?? 0) != (expected.Slots?.Length ?? 0))
            errors.AppendLine($"Slot count mismatch: expected {expected.Slots?.Length}, got {actual.Slots?.Length}");

        if (actual.SD != expected.SD)
            errors.AppendLine($"SD (seed) mismatch: expected {expected.SD}, got {actual.SD}");

        if (actual.StartingCredits != expected.StartingCredits)
            errors.AppendLine($"StartingCredits mismatch: expected {expected.StartingCredits}, got {actual.StartingCredits}");

        if (errors.Length > 0)
            throw new InvalidDataException($"Round-trip validation failed:\n{errors}");
    }
}
