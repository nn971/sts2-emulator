using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>Single-player GoldenPathActMap and GoldenCompass at the pinned v111 source.</summary>
public static class PrototypeGoldenPathMapV111
{
    public const string ProfileId = "native-golden-path-map-v0.111.0-v1";
    public const int BossFloor = 17;
    private static readonly PrototypeRoomType[] Rooms =
    [
        PrototypeRoomType.Combat, PrototypeRoomType.Unknown, PrototypeRoomType.Combat,
        PrototypeRoomType.Rest, PrototypeRoomType.Combat, PrototypeRoomType.Rest,
        PrototypeRoomType.Unknown, PrototypeRoomType.Treasure, PrototypeRoomType.Unknown,
        PrototypeRoomType.Treasure, PrototypeRoomType.Unknown, PrototypeRoomType.Shop,
        PrototypeRoomType.Elite, PrototypeRoomType.Rest, PrototypeRoomType.Elite,
        PrototypeRoomType.Rest, PrototypeRoomType.Boss
    ];

    public static MapState Generate(int act)
    {
        if (act is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(act));
        var nodes = Rooms.Select((room, index) => new MapNodeState(
            $"{act}:{index + 1}:3", act, index + 1, room,
            index + 1 == Rooms.Length ? [] : [$"{act}:{index + 2}:3"])).ToArray();
        return new(nodes, EntryNodeIds: [nodes[0].NodeId], GenerationProfileId: ProfileId);
    }

    public static int AcquisitionActIndex(RelicInstance relic)
    {
        if (relic.RelicId != NativeAncientRewardsV111.GoldenCompassId
            || relic.PersistentState.ValueKind != JsonValueKind.Object
            || !relic.PersistentState.TryGetProperty("GoldenPathAct", out var value)
            || !value.TryGetInt32(out var act) || act is < 0 or > 2)
            throw new InvalidOperationException("Golden Compass requires a persisted acquisition act index.");
        return act;
    }

    public static bool IsActive(PlayerState player, int act) =>
        player.Relics.Any(relic => relic.RelicId == NativeAncientRewardsV111.GoldenCompassId
            && AcquisitionActIndex(relic) == act - 1);

    internal static RunState ApplyAcquisition(RunState before, RunState after)
    {
        var newIndices = Enumerable.Range(before.Player.Relics.Length,
            Math.Max(0, after.Player.Relics.Length - before.Player.Relics.Length))
            .Where(index => after.Player.Relics[index].RelicId == NativeAncientRewardsV111.GoldenCompassId)
            .ToArray();
        if (newIndices.Length == 0)
            return after;
        var world = after.World ?? throw new InvalidOperationException("Compass acquisition requires a world.");
        var relics = (RelicInstance[])after.Player.Relics.Clone();
        foreach (var index in newIndices)
            relics[index] = relics[index] with
            {
                PersistentState = JsonSerializer.SerializeToElement(new { GoldenPathAct = world.Act - 1 })
            };
        // RunManager.GenerateMap constructs the act's ordinary map before
        // ModifyGeneratedMap replaces it. Retain those act-local random
        // operations for the future provider bridge; persistent run streams
        // are not used by this existing map generator.
        if (world.Act is 2 or 3)
            _ = PrototypeNativeLaterActMap.GenerateNative(after.RunSeed, world.Act, after.Ascension);
        var map = Generate(world.Act);
        if (world.Map.CurrentNodeId is not null)
            map = map with { CurrentNodeId = $"{world.Act}:{world.Floor}:3" };
        return after with { Player = after.Player with { Relics = relics }, World = world with { Map = map } };
    }
}
