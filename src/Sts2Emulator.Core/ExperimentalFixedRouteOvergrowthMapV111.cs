namespace Sts2Emulator.Core;

/// <summary>
/// Explicit, opt-in fixed Act 1 training map for delayed-credit experiments.
/// Derives its room order from the existing Golden Path implementation,
/// removes its second ordinary combat, and retains the normal experimental
/// Overgrowth boss floor. This is a controlled experimental map, not a
/// native Overgrowth generation profile or the Golden Compass relic.
/// </summary>
public static class ExperimentalFixedRouteOvergrowthMapV111
{
    public const string ProfileId = "experimental-act1-golden-path-minus-combat-v1";
    public const int RemovedGoldenPathFloor = 3;

    public static MapState Generate()
    {
        var original = PrototypeGoldenPathMapV111.Generate(act: 1);
        var roomTypes = original.Nodes
            .OrderBy(node => node.Floor)
            .Where(node => node.Floor != RemovedGoldenPathFloor)
            .Select(node => node.RoomType)
            .ToArray();

        if (roomTypes.Length != PrototypeNativeOvergrowthMap.BossFloor
            || roomTypes[0] != PrototypeRoomType.Combat
            || roomTypes[^1] != PrototypeRoomType.Boss)
            throw new InvalidOperationException(
                "Golden Path derivative has the wrong Act 1 length or endpoints.");

        var nodes = roomTypes.Select((room, index) =>
            new MapNodeState(
                $"1:{index + 1}:3",
                1,
                index + 1,
                room,
                index == roomTypes.Length - 1
                    ? []
                    : [$"1:{index + 2}:3"]))
            .ToArray();

        return new MapState(
            Nodes: nodes,
            EntryNodeIds: [nodes[0].NodeId],
            GenerationProfileId: ProfileId);
    }

    /// <summary>
    /// Overwrite only the map of an already-initialized native-structure
    /// Overgrowth opening. Neow, RNG state, player state and the remainder of
    /// the run are left unchanged.
    /// </summary>
    public static RunState OverrideOpening(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var world = state.World
            ?? throw new InvalidOperationException("A run world is required.");
        if (world.Act != 1 || world.Floor != 0
            || !world.NativeOvergrowthOpening
            || world.Map.CurrentNodeId is not null
            || state.Phase != RunPhase.Event)
            throw new InvalidOperationException(
                "Fixed route override requires an unentered Act 1 native-structure opening.");

        return state with
        {
            World = world with { Map = Generate() }
        };
    }

    public static RunState Create(string seed, int ascension = 0) =>
        OverrideOpening(PrototypeNativeOvergrowthRunFactory.Create(seed, ascension));
}
