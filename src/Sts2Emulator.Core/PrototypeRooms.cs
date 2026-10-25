namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetMapActions(RunState state)
    {
        var world = RequireWorld(state);
        return PrototypeNativeWingedBoots.AvailableNodes(
                world, state.Player)
            .Select(node => GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload(node.NodeId)))
            .ToArray();
    }

    private static RunState StepMap(RunState state, GameAction action)
    {
        RequireKind(action, "choose_map_node");
        var payload = action.ReadPayload<ChooseMapNodePayload>();
        var world = RequireWorld(state);
        var node = PrototypeNativeWingedBoots.AvailableNodes(
                world, state.Player).FirstOrDefault(
            option => StringComparer.Ordinal.Equals(option.NodeId, payload.NodeId))
            ?? throw new InvalidOperationException($"Unknown map node '{payload.NodeId}'.");

        var playerAfterTravel = PrototypeNativeWingedBoots
            .ConsumeIfSkippingEdge(state.Player, world.Map, node.NodeId);

        // Later-act maps execute only under the explicit experimental
        // three-act profile while Ancient assignment and native fidelity
        // remain incomplete. Never fall back to the six-floor prototype.
        var nativeLaterMap = world.Map.GenerationProfileId is
            PrototypeNativeLaterActMap.HiveProfileId
            or PrototypeNativeLaterActMap.GloryProfileId
            or PrototypeGoldenPathMapV111.ProfileId;
        var forceRepy = ShouldForceWarHistorianRepy(world, state.Player);
        var experimentalLaterRoute = nativeLaterMap
            && state.Configuration?.UsesExperimentalSilentThreeActRoute == true;
        if (nativeLaterMap
            && !experimentalLaterRoute
            && !(forceRepy
                && node.RoomType is PrototypeRoomType.Event
                    or PrototypeRoomType.Unknown))
            throw new NotSupportedException(
                "Native Hive/Glory room execution is gated until " +
                "its regional encounter/event/reward pools are integrated.");

        // Native LanternKey converts Glory '?' rooms into Event rooms.
        // Its event replacement also applies to existing Event nodes.
        var room = node.RoomType == PrototypeRoomType.Unknown
            ? PrototypeGoldenPathMapV111.IsActive(state.Player, world.Act)
                ? ResolveUnknownMapRoom(ref world, state.Rng, eventOnly: true)
                : forceRepy ? PrototypeRoomType.Event
                : ResolveUnknownMapRoom(ref world, state.Rng)
            : node.RoomType;

        world = world with
        {
            Floor = node.Floor,
            ActiveRoom = room,
            Map = world.Map with { CurrentNodeId = node.NodeId },
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null
        };
        state = state with
        {
            World = world,
            Player = ApplyRelicRoomEntryGold(
                ApplyPersistentCardRoomEntry(playerAfterTravel, node))
        };

        return room switch
        {
            PrototypeRoomType.Combat => experimentalLaterRoute
                ? StartExperimentalLaterActCombat(state, room)
                : StartCombat(state, room),
            PrototypeRoomType.Elite => experimentalLaterRoute
                ? StartExperimentalLaterActCombat(state, room)
                : StartCombat(state, room),
            PrototypeRoomType.Boss => experimentalLaterRoute
                ? StartExperimentalLaterActCombat(state, room)
                : StartCombat(state, room),
            PrototypeRoomType.Event => forceRepy
                ? StartNativeWarHistorianRepy(state) : StartEvent(state),
            PrototypeRoomType.Shop => StartShop(state),
            PrototypeRoomType.Rest => state with { Phase = RunPhase.Rest },
            PrototypeRoomType.Treasure => StartTreasureRoom(state),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static RunState StartExperimentalLaterActCombat(
        RunState state, PrototypeRoomType room)
    {
        var world = RequireWorld(state);
        if (state.Configuration?.UsesExperimentalSilentThreeActRoute != true
            || world.Act is < 2 or > 3)
            throw new NotSupportedException(
                "Regional combat entry requires the experimental three-act route.");
        var selected = ExperimentalSilentThreeActRouteV111.SelectEncounter(
            world, room, state.Rng);
        return StartCombat(state with
        {
            World = world with { LaterActEncounterBag = selected.Bag }
        }, room, selected.Encounter);
    }

    private static MapState GenerateActMap(
        int act,
        RngBundle rng)
    {
        var rules = PrototypeContent.Rules;
        var layers =
            new List<MapNodeState[]>();

        for (var floor = 1;
             floor <= rules.FloorsPerAct;
             floor++)
        {
            var floorRule =
                GetMapFloorRule(floor);
            if (floorRule.RoomPool.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Prototype map rule for floor {floor} has an empty room pool.");
            }

            if (floorRule.MinNodes <= 0
                || floorRule.MaxNodes
                    < floorRule.MinNodes)
            {
                throw new InvalidOperationException(
                    $"Prototype map rule for floor {floor} has invalid width bounds.");
            }

            var width = floorRule.MinNodes;
            if (floorRule.MaxNodes
                > floorRule.MinNodes)
            {
                width += ResolveLegacyRandomIndex(
                    rng,
                    "map.legacy.floor_width.index_draw",
                    $"act:{act}:floor:{floor}:bounds:{floorRule.MinNodes}-{floorRule.MaxNodes}",
                    Enumerable.Range(
                        0,
                        checked(floorRule.MaxNodes
                            - floorRule.MinNodes + 1)),
                    streamId: "map");
            }

            var nodes = Enumerable.Range(
                    0,
                    width)
                .Select(index =>
                    new MapNodeState(
                        NodeId:
                            $"{act}:{floor}:{index}",
                        Act: act,
                        Floor: floor,
                        RoomType:
                            PrototypeRoomType.Combat,
                        NextNodeIds:
                            Array.Empty<string>()))
                .ToArray();
            layers.Add(nodes);
        }

        for (var layerIndex = 0;
             layerIndex < layers.Count - 1;
             layerIndex++)
        {
            ConnectMapLayers(
                layers[layerIndex],
                layers[layerIndex + 1],
                rng);
        }

        for (var layerIndex = 0;
             layerIndex < layers.Count;
             layerIndex++)
        {
            AssignMapRooms(
                layers,
                layerIndex,
                GetMapFloorRule(
                    layerIndex + 1),
                rng);
        }

        var allNodes = layers
            .SelectMany(layer => layer)
            .ToArray();
        return new MapState(
            Nodes: allNodes,
            CurrentNodeId: null,
            EntryNodeIds: layers[0]
                .Select(node => node.NodeId)
                .ToArray(),
            GenerationProfileId:
                PrototypeContent
                    .MapGenerationProfileId);
    }

    private static PrototypeMapFloorRule
        GetMapFloorRule(int floor) =>
        PrototypeContent.Rules.MapFloorRules
            .SingleOrDefault(rule =>
                floor >= rule.MinFloor
                && floor <= rule.MaxFloor)
        ?? throw new InvalidOperationException(
            $"No prototype map rule covers floor {floor}.");

    private static void ConnectMapLayers(
        MapNodeState[] current,
        MapNodeState[] next,
        RngBundle rng)
    {
        if (current.Length == 0
            || next.Length == 0)
        {
            throw new InvalidOperationException(
                "Prototype map layers cannot be empty.");
        }
        var edgeOwner = LegacyMapEdgeOwner(current, next);

        var targets = Enumerable.Range(
                0,
                current.Length)
            .Select(_ =>
                new List<string>())
            .ToArray();
        var incoming =
            new int[next.Length];

        if (next.Length == 1)
        {
            for (var sourceIndex = 0;
                 sourceIndex < current.Length;
                 sourceIndex++)
            {
                targets[sourceIndex].Add(
                    next[0].NodeId);
                incoming[0]++;
            }
        }
        else
        {
            for (var nextIndex = 0;
                 nextIndex < next.Length;
                 nextIndex++)
            {
                var candidates =
                    Enumerable.Range(
                            0,
                            current.Length)
                        .Where(index =>
                            targets[index].Count < 2)
                        .ToArray();
                var minTargetCount =
                    candidates.Min(index =>
                        targets[index].Count);
                var balanced = candidates
                    .Where(index =>
                        targets[index].Count
                            == minTargetCount)
                    .ToArray();
                var sourceIndex =
                    balanced[
                        ResolveLegacyRandomIndex(
                            rng,
                            "map.legacy.edge_source.index_draw",
                            edgeOwner + ":target:" + next[nextIndex].NodeId
                                + ":sources:" + CanonicalJson.Sha256(
                                    balanced.Select(index =>
                                        current[index].NodeId).ToArray()),
                            Enumerable.Range(0, balanced.Length),
                            streamId: "map")];
                targets[sourceIndex].Add(
                    next[nextIndex].NodeId);
                incoming[nextIndex]++;
            }

            for (var sourceIndex = 0;
                 sourceIndex < current.Length;
                 sourceIndex++)
            {
                if (targets[sourceIndex].Count > 0)
                {
                    continue;
                }

                var minIncoming =
                    incoming.Min();
                var candidates =
                    Enumerable.Range(
                            0,
                            next.Length)
                        .Where(index =>
                            incoming[index]
                                == minIncoming)
                        .ToArray();
                var nextIndex =
                    candidates[
                        ResolveLegacyRandomIndex(
                            rng,
                            "map.legacy.orphan_target.index_draw",
                            edgeOwner + ":source:"
                                + current[sourceIndex].NodeId + ":targets:"
                                + CanonicalJson.Sha256(candidates.Select(index =>
                                    next[index].NodeId).ToArray()),
                            Enumerable.Range(0, candidates.Length),
                            streamId: "map")];
                targets[sourceIndex].Add(
                    next[nextIndex].NodeId);
                incoming[nextIndex]++;
            }

            if (targets.All(items =>
                    items.Count == 1))
            {
                AddRandomSecondTarget(
                    targets,
                    current,
                    next,
                    incoming,
                    rng);
            }

            if (incoming.All(count =>
                    count <= 1))
            {
                AddRandomConvergence(
                    targets,
                    current,
                    next,
                    incoming,
                    rng);
            }

            for (var sourceIndex = 0;
                 sourceIndex < current.Length;
                 sourceIndex++)
            {
                if (targets[sourceIndex].Count != 1
                    || ResolveLegacyBoundedInteger(
                        rng,
                        "map.legacy.edge_extra_chance.ticket",
                        edgeOwner + ":source:"
                            + current[sourceIndex].NodeId,
                        100,
                        streamId: "map") >= 35)
                {
                    continue;
                }

                AddSecondTargetForSource(
                    targets,
                    sourceIndex,
                    current,
                    next,
                    incoming,
                    rng);
            }
        }

        for (var sourceIndex = 0;
             sourceIndex < current.Length;
             sourceIndex++)
        {
            current[sourceIndex] =
                current[sourceIndex] with
                {
                    NextNodeIds =
                        targets[sourceIndex]
                            .OrderBy(
                                id => id,
                                StringComparer.Ordinal)
                            .ToArray()
                };
        }
    }

    private static void AddRandomSecondTarget(
        List<string>[] targets,
        MapNodeState[] current,
        MapNodeState[] next,
        int[] incoming,
        RngBundle rng)
    {
        var candidates =
            Enumerable.Range(
                    0,
                    current.Length)
                .Where(index =>
                    targets[index].Count < 2)
                .ToArray();
        if (candidates.Length == 0)
        {
            return;
        }

        var sourceIndex =
            candidates[
                ResolveLegacyRandomIndex(
                    rng,
                    "map.legacy.second_edge_source.index_draw",
                    LegacyMapEdgeOwner(current, next) + ":sources:"
                        + CanonicalJson.Sha256(candidates.Select(index =>
                            current[index].NodeId).ToArray()),
                    Enumerable.Range(0, candidates.Length),
                    streamId: "map")];
        AddSecondTargetForSource(
            targets,
            sourceIndex,
            current,
            next,
            incoming,
            rng);
    }

    private static string LegacyMapEdgeOwner(
        MapNodeState[] current,
        MapNodeState[] next) =>
        $"act:{current[0].Act}:from_floor:{current[0].Floor}:to_floor:{next[0].Floor}";

    private static void AddRandomConvergence(
        List<string>[] targets,
        MapNodeState[] current,
        MapNodeState[] next,
        int[] incoming,
        RngBundle rng)
    {
        var edgeOwner = LegacyMapEdgeOwner(current, next);
        var nextOrder =
            Enumerable.Range(0, next.Length)
                .OrderBy(nextIndex =>
                    ResolveLegacyBoundedInteger(
                        rng,
                        "map.legacy.convergence.order_key",
                        edgeOwner + ":target:" + next[nextIndex].NodeId,
                        int.MaxValue,
                        streamId: "map"))
                .ToArray();

        foreach (var nextIndex in nextOrder)
        {
            var sources =
                Enumerable.Range(
                        0,
                        current.Length)
                    .Where(sourceIndex =>
                        targets[sourceIndex].Count < 2
                        && !targets[sourceIndex]
                            .Contains(
                                next[nextIndex].NodeId,
                                StringComparer.Ordinal))
                    .ToArray();
            if (sources.Length == 0)
            {
                continue;
            }

            var sourceIndex =
                sources[
                    ResolveLegacyRandomIndex(
                        rng,
                        "map.legacy.convergence.source.index_draw",
                        edgeOwner + ":target:" + next[nextIndex].NodeId
                            + ":sources:" + CanonicalJson.Sha256(
                                sources.Select(sourceIndex =>
                                    current[sourceIndex].NodeId).ToArray()),
                        Enumerable.Range(0, sources.Length),
                        streamId: "map")];
            targets[sourceIndex].Add(
                next[nextIndex].NodeId);
            incoming[nextIndex]++;
            return;
        }
    }

    private static void AddSecondTargetForSource(
        List<string>[] targets,
        int sourceIndex,
        MapNodeState[] current,
        MapNodeState[] next,
        int[] incoming,
        RngBundle rng)
    {
        if (targets[sourceIndex].Count >= 2)
        {
            return;
        }

        var available =
            Enumerable.Range(
                    0,
                    next.Length)
                .Where(index =>
                    !targets[sourceIndex]
                        .Contains(
                            next[index].NodeId,
                            StringComparer.Ordinal))
                .ToArray();
        if (available.Length == 0)
        {
            return;
        }

        var minIncoming =
            available.Min(index =>
                incoming[index]);
        var balanced = available
            .Where(index =>
                incoming[index]
                    == minIncoming)
            .ToArray();
        var nextIndex =
            balanced[
                ResolveLegacyRandomIndex(
                    rng,
                    "map.legacy.second_edge_target.index_draw",
                    LegacyMapEdgeOwner(current, next) + ":source:"
                        + current[sourceIndex].NodeId + ":targets:"
                        + CanonicalJson.Sha256(balanced.Select(index =>
                            next[index].NodeId).ToArray()),
                    Enumerable.Range(0, balanced.Length),
                    streamId: "map")];
        targets[sourceIndex].Add(
            next[nextIndex].NodeId);
        incoming[nextIndex]++;
    }

    private static void AssignMapRooms(
        List<MapNodeState[]> layers,
        int layerIndex,
        PrototypeMapFloorRule floorRule,
        RngBundle rng)
    {
        var nodes = layers[layerIndex];
        if (floorRule.RequiredRooms.Length
            > nodes.Length)
        {
            throw new InvalidOperationException(
                $"Floor {layerIndex + 1} requires more room types than it has nodes.");
        }

        if (floorRule.RequiredRooms.Any(room =>
                !floorRule.RoomPool.Contains(room)))
        {
            throw new InvalidOperationException(
                $"Floor {layerIndex + 1} requires a room outside its room pool.");
        }

        if (!floorRule.AllowDuplicateSpecialRooms
            && floorRule.RequiredRooms
                .Where(room =>
                    room != PrototypeRoomType.Combat)
                .GroupBy(room => room)
                .Any(group =>
                    group.Count() > 1))
        {
            throw new InvalidOperationException(
                $"Floor {layerIndex + 1} requires duplicate special rooms.");
        }

        var assigned =
            new PrototypeRoomType?[nodes.Length];
        var usedSpecialRooms =
            new HashSet<PrototypeRoomType>();

        foreach (var requiredRoom in
                 floorRule.RequiredRooms)
        {
            var candidateIndices =
                Enumerable.Range(
                        0,
                        nodes.Length)
                    .Where(index =>
                        assigned[index] is null
                        && CanAssignMapRoom(
                            layers,
                            layerIndex,
                            index,
                            requiredRoom,
                            floorRule))
                    .ToArray();
            if (candidateIndices.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No valid node can host required room {requiredRoom} on floor {layerIndex + 1}.");
            }

            var index =
                candidateIndices[
                    ResolveLegacyRandomIndex(
                        rng,
                        "map.legacy.required_room.node.index_draw",
                        $"act:{nodes[0].Act}:floor:{nodes[0].Floor}:room:{requiredRoom}:nodes:"
                            + CanonicalJson.Sha256(candidateIndices.Select(index =>
                                nodes[index].NodeId).ToArray()),
                        Enumerable.Range(0, candidateIndices.Length),
                        streamId: "map")];
            assigned[index] =
                requiredRoom;
            if (requiredRoom
                != PrototypeRoomType.Combat)
            {
                usedSpecialRooms.Add(
                    requiredRoom);
            }
        }

        for (var index = 0;
             index < nodes.Length;
             index++)
        {
            if (assigned[index] is not null)
            {
                continue;
            }

            var candidates =
                floorRule.RoomPool
                    .Where(room =>
                        (floorRule
                            .AllowDuplicateSpecialRooms
                         || room
                            == PrototypeRoomType.Combat
                         || !usedSpecialRooms
                            .Contains(room))
                        && CanAssignMapRoom(
                            layers,
                            layerIndex,
                            index,
                            room,
                            floorRule))
                    .ToArray();
            if (candidates.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No room candidate is valid for floor {layerIndex + 1}, node {index}.");
            }

            var room =
                candidates[
                    ResolveLegacyRandomIndex(
                        rng,
                        "map.legacy.room_assignment.index_draw",
                        $"act:{nodes[index].Act}:floor:{nodes[index].Floor}:node:{nodes[index].NodeId}:rooms:"
                            + CanonicalJson.Sha256(candidates),
                        Enumerable.Range(0, candidates.Length),
                        streamId: "map")];
            assigned[index] = room;
            if (room
                != PrototypeRoomType.Combat)
            {
                usedSpecialRooms.Add(room);
            }
        }

        for (var index = 0;
             index < nodes.Length;
             index++)
        {
            nodes[index] =
                nodes[index] with
                {
                    RoomType =
                        assigned[index]!.Value
                };
        }
    }

    private static bool CanAssignMapRoom(
        List<MapNodeState[]> layers,
        int layerIndex,
        int nodeIndex,
        PrototypeRoomType room,
        PrototypeMapFloorRule floorRule)
    {
        if (!floorRule
                .AvoidMatchingSpecialPredecessors
            || room
                == PrototypeRoomType.Combat
            || layerIndex == 0)
        {
            return true;
        }

        var nodeId =
            layers[layerIndex][nodeIndex]
                .NodeId;
        return !layers[layerIndex - 1]
            .Any(predecessor =>
                predecessor.RoomType == room
                && (predecessor.NextNodeIds
                        ?? Array.Empty<string>())
                    .Contains(
                        nodeId,
                        StringComparer.Ordinal));
    }

    internal static int ResolveLegacyEventGold(
        RngBundle rng,
        string eventId,
        string stage,
        int minimum,
        int candidateCount) =>
        checked(minimum + LegacyRandomChoiceV111.ChooseBoundedInteger(
            rng,
            "event.generated_gold.index_draw",
            "event",
            $"event:{eventId}:gold:{stage}",
            candidateCount).SelectedIndex);

    private static RunState ResolveNativeEventGold(RunState state)
    {
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException(
                "Native event gold needs an active event.");
        switch (evt.EventId)
        {
            case PrototypeNativeDenseVegetation.EventId:
            {
                var draw = ResolveNativeEventGoldIndex(
                    state, evt, "DENSE_VEGETATION", "primary", 39);
                return StoreNativeEventGold(
                    draw.State, 61 + draw.SelectedIndex);
            }
            case "proto.native.event.whispering_hollow":
            {
                var draw = ResolveNativeEventGoldIndex(
                    state, evt, "WHISPERING_HOLLOW", "primary", 19);
                return StoreNativeEventGold(
                    draw.State, 26 + draw.SelectedIndex);
            }
            case "proto.native.event.sunken_statue":
            case "proto.native.underdocks.sunken_statue":
            {
                var draw = ResolveNativeEventGoldIndex(
                    state, evt, "SUNKEN_STATUE", "primary", 21);
                return StoreNativeEventGold(
                    draw.State, 101 + draw.SelectedIndex);
            }
            case "proto.native.event.luminous_choir":
            {
                var draw = ResolveNativeEventGoldIndex(
                    state, evt, "LUMINOUS_CHOIR", "primary", 50);
                return StoreNativeEventGold(
                    draw.State, 149 - draw.SelectedIndex);
            }
            case "proto.native.event.jungle_maze_adventure":
            {
                var first = ResolveNativeEventGoldUnitFloat(
                    state, evt, "JUNGLE_MAZE_ADVENTURE", "primary");
                state = first.State;
                evt = RequireWorld(state).Event!;
                var second = ResolveNativeEventGoldUnitFloat(
                    state, evt, "JUNGLE_MAZE_ADVENTURE", "secondary");
                var soloGold = (int)(150f
                    + (first.SelectedValue * 30f - 15f));
                var joinGold = (int)(50f
                    + (second.SelectedValue * 30f - 15f));
                return StoreNativeEventGold(
                    second.State, soloGold, joinGold);
            }
            case "proto.native.underdocks.sunken_treasury":
            {
                var first = ResolveNativeEventGoldIndex(
                    state, evt, "SUNKEN_TREASURY", "primary", 16);
                state = first.State;
                evt = RequireWorld(state).Event!;
                var second = ResolveNativeEventGoldIndex(
                    state, evt, "SUNKEN_TREASURY", "secondary", 61);
                return StoreNativeEventGold(
                    second.State,
                    52 + first.SelectedIndex,
                    303 + second.SelectedIndex);
            }
            case PrototypeNativeHiveEvents.LostWispId:
            {
                var draw = ResolveNativeEventGoldUnitFloat(
                    state, evt, "LOST_WISP", "primary");
                var gold = (int)(60f
                    + (draw.SelectedValue * 30f - 15f));
                return StoreNativeEventGold(draw.State, gold);
            }
            case PrototypeNativePunchOff.EventId:
            {
                var draw = ResolveNativeEventGoldIndex(
                    state, evt, PrototypeNativePunchOff.NativeRngEventId,
                    "primary", 8);
                return StoreNativeEventGold(draw.State, 91 + draw.SelectedIndex);
            }
            case PrototypeNativeSharedEvents.ThisOrThatId:
            {
                var draw = ResolveNativeEventGoldIndex(
                    state, evt,
                    PrototypeNativeSharedEvents.ThisOrThatNativeRngEventId,
                    "primary", 28);
                return StoreNativeEventGold(draw.State, 41 + draw.SelectedIndex);
            }
            default:
                return state;
        }
    }

    private static (RunState State, int SelectedIndex)
        ResolveNativeEventGoldIndex(
            RunState state, EventState evt, string nativeEventEntry,
            string stage, int boundedCount) =>
        NativeEventContextRngV111.ResolveBoundedInteger(
            state,
            evt,
            nativeEventEntry,
            "event.generated_gold.index_draw",
            $"event:{evt.EventId}:gold:{stage}",
            boundedCount);

    private static (RunState State, float SelectedValue)
        ResolveNativeEventGoldUnitFloat(
            RunState state, EventState evt, string nativeEventEntry,
            string stage) =>
        NativeEventContextRngV111.ResolveUnitFloat(
            state,
            evt,
            nativeEventEntry,
            "event.generated_gold.unit_float",
            $"event:{evt.EventId}:gold:{stage}");

    private static RunState StoreNativeEventGold(
        RunState state, int primaryGold, int? secondaryGold = null)
    {
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException(
                "Native event gold needs an active event.");
        return state with
        {
            World = world with
            {
                Event = evt with
                {
                    NativeEventGold = primaryGold,
                    NativeEventSecondaryGold = secondaryGold
                        ?? evt.NativeEventSecondaryGold
                }
            }
        };
    }

    private static RunState StartEvent(RunState state)
    {
        var world = RequireWorld(state);
        var nativeOvergrowth = world.Act == 1
            && PrototypeNativeOvergrowthMap.IsGenerationProfile(
                world.Map.GenerationProfileId);
        var nativeUnderdocks = world.Act == 1
            && world.Map.GenerationProfileId
                == PrototypeNativeUnderdocks.GenerationProfileId;
        var laterRoute = world.Act is 2 or 3
            && state.Configuration?.UsesExperimentalSilentThreeActRoute == true;
        var eligible = PrototypeContent.Events.Values
            .Where(evt =>
                (laterRoute
                    ? ExperimentalSilentThreeActRouteV111.ContainsEvent(
                        evt.Id, world.Act)
                    : nativeUnderdocks
                        ? PrototypeNativeUnderdocksEvents.IsSupported(evt.Id)
                        : nativeOvergrowth
                            ? PrototypeNativeOvergrowthEvents
                                  .IsNativeRegionEvent(evt.Id)
                                || PrototypeNativeSharedEvents.IsSupported(
                                    evt.Id)
                            : !PrototypeNativeOvergrowthEvents
                                  .IsNativeRegionEvent(evt.Id)
                                && !PrototypeNativeUnderdocksEvents
                                    .IsNativeRegionEvent(evt.Id))
                && (!nativeOvergrowth
                    || PrototypeNativeOvergrowthEvents.IsEligible(
                        evt, state.Player))
                && (!nativeUnderdocks
                    || PrototypeNativeUnderdocksEvents.IsEligible(
                        evt, state.Player)
                    && PrototypeNativeUnderdocksEvents.IsFloorEligible(
                        evt, world))
                && (!laterRoute || world.Act != 2
                    || PrototypeNativeHiveEvents.IsEligible(evt, state.Player))
                && (!laterRoute || world.Act != 3
                    || PrototypeNativeGloryEvents.IsEligible(evt, state.Player))
                && world.Act >= evt.MinAct
                && world.Act <= evt.MaxAct
                && (evt.Id != PrototypeNativeFakeMerchant.EventId
                    || PrototypeNativeFakeMerchant.IsEligible(state.Player))
                && (evt.Id != PrototypeNativeFutureOfPotions.EventId
                    || laterRoute
                    && PrototypeNativeFutureOfPotions.IsEligible(state.Player))
                && (laterRoute || evt.Weight > 0)
                && (!evt.OncePerRun
                    || !world.EventIds.Contains(evt.Id, StringComparer.Ordinal)))
            .ToArray();

        if (eligible.Length == 0)
        {
            throw new InvalidOperationException(
                $"No prototype event is eligible in act {world.Act}.");
        }

        if (laterRoute)
        {
            // Without replacement where possible; one-off native events
            // stay excluded by OncePerRun. Pool exhaustion is deliberately
            // provisional until the source event-bag rolls are audited.
            var unseen = eligible.Where(evt =>
                !world.EventIds.Contains(evt.Id, StringComparer.Ordinal))
                .ToArray();
            if (unseen.Length > 0)
                eligible = unseen;
        }
        var previousEvent = world.EventIds.LastOrDefault();
        if (previousEvent is not null && eligible.Length > 1)
        {
            var withoutImmediateRepeat = eligible
                .Where(evt =>
                    !StringComparer.Ordinal.Equals(evt.Id, previousEvent))
                .ToArray();
            if (withoutImmediateRepeat.Length > 0)
                eligible = withoutImmediateRepeat;
        }

        // Gated later-act fixture registrations deliberately have Weight=0
        // so legacy prototype runs cannot sample them. Only this explicit
        // regional route opts in, using uniform provisional weights.
        var totalWeight = eligible.Sum(evt => laterRoute ? 1 : evt.Weight);
        var weightedSupport = eligible.Select(evt => new
        {
            evt.Id,
            Weight = laterRoute ? 1 : evt.Weight
        }).ToArray();
        var roll = ResolveLegacyBoundedInteger(
            state.Rng,
            "event.selection.weighted_ticket",
            $"act:{world.Act}:floor:{world.Floor}:pool:"
                + CanonicalJson.Sha256(weightedSupport),
            totalWeight,
            streamId: "event");
        var selected = eligible[^1];
        foreach (var evt in eligible)
        {
            var weight = laterRoute ? 1 : evt.Weight;
            if (roll < weight)
            {
                selected = evt;
                break;
            }
            roll -= weight;
        }

        world = world with
        {
            Event = new EventState(
                selected.Id,
                NativeEventRng: NativeEventContextRngV111.CreateAtEntry(
                    state,
                    selected.Id switch
                    {
                        PrototypeNativeUnderdocksTrashHeap.EventId =>
                            PrototypeNativeUnderdocksTrashHeap.NativeRngEventId,
                        PrototypeNativeTabletOfTruth.EventId =>
                            PrototypeNativeTabletOfTruth.NativeRngEventId,
                        PrototypeNativeUnderdocksDoors.EventId =>
                            PrototypeNativeUnderdocksDoors.NativeRngEventId,
                        PrototypeNativeFakeMerchant.EventId =>
                            PrototypeNativeFakeMerchant.NativeRngEventId,
                        PrototypeNativeGloryEvents.TinkerTimeId =>
                            PrototypeNativeGloryEvents.TinkerTimeNativeRngEventId,
                        PrototypeNativeStoneOfAllTime.EventId =>
                            PrototypeNativeStoneOfAllTime.NativeRngEventId,
                        PrototypeNativeGloryEvents.BattlewornDummyId =>
                            PrototypeNativeGloryEvents
                                .BattlewornDummyNativeRngEventId,
                        PrototypeNativeGloryEvents.HungryForMushroomsId =>
                            PrototypeNativeGloryEvents
                                .HungryForMushroomsNativeRngEventId,
                        PrototypeNativeGloryEvents.ReflectionsId =>
                            PrototypeNativeGloryEvents
                                .ReflectionsNativeRngEventId,
                        PrototypeNativeGloryEvents.TrialId =>
                            PrototypeNativeGloryEvents
                                .TrialNativeRngEventId,
                        PrototypeNativeFutureOfPotions.EventId =>
                            PrototypeNativeFutureOfPotions.NativeRngEventId,
                        PrototypeNativeHiveEvents.InfestedAutomatonId =>
                            PrototypeNativeHiveEvents
                                .InfestedAutomatonNativeRngEventId,
                        PrototypeNativeEndlessConveyor.EventId =>
                            PrototypeNativeEndlessConveyor.NativeRngEventId,
                        PrototypeNativePunchOff.EventId =>
                            PrototypeNativePunchOff.NativeRngEventId,
                        PrototypeNativeSharedEvents.ThisOrThatId =>
                            PrototypeNativeSharedEvents
                                .ThisOrThatNativeRngEventId,
                        PrototypeNativeHiveEvents.LostWispId =>
                            PrototypeNativeHiveEvents
                                .LostWispNativeRngEventId,
                        PrototypeNativeDenseVegetation.EventId =>
                            "DENSE_VEGETATION",
                        "proto.native.event.whispering_hollow" =>
                            "WHISPERING_HOLLOW",
                        "proto.native.event.sunken_statue" =>
                            "SUNKEN_STATUE",
                        "proto.native.event.luminous_choir" =>
                            "LUMINOUS_CHOIR",
                        "proto.native.event.jungle_maze_adventure" =>
                            "JUNGLE_MAZE_ADVENTURE",
                        "proto.native.underdocks.sunken_treasury" =>
                            "SUNKEN_TREASURY",
                        "proto.native.underdocks.sunken_statue" =>
                            "SUNKEN_STATUE",
                        _ => null
                    }),
                NativeEventGold:
                    selected.Id switch
                    {
                        PrototypeNativeDenseVegetation.EventId =>
                            state.NativeRunRng is null
                                ? ResolveLegacyEventGold(
                                    state.Rng, selected.Id, "primary", 61, 39)
                                : 0,
                        "proto.native.event.whispering_hollow" =>
                            state.NativeRunRng is null
                                ? ResolveLegacyEventGold(
                                    state.Rng, selected.Id, "primary", 26, 19)
                                : 0,
                        "proto.native.event.sunken_statue" =>
                            state.NativeRunRng is null
                                ? ResolveLegacyEventGold(
                                    state.Rng, selected.Id, "primary", 101, 21)
                                : 0,
                        "proto.native.event.luminous_choir" =>
                            state.NativeRunRng is null
                                ? ResolveLegacyEventGold(
                                    state.Rng, selected.Id, "primary", 100, 50)
                                : 0,
                        // Source rolls two independent -15..15 floats:
                        // 150 + solo roll, 50 + join roll, truncated to
                        // integer gold by PlayerCmd.GainGold. Legacy runs
                        // retain the earlier integer-bin approximation;
                        // native runs use the source unit-float boundary.
                        "proto.native.event.jungle_maze_adventure" =>
                            state.NativeRunRng is null
                                ? ResolveLegacyEventGold(
                                    state.Rng, selected.Id, "primary", 135, 30)
                                : 0,
                        // Native Sunken Treasury rolls small then large
                        // independently; both rolls are persisted so the
                        // eventual choice never changes the RNG outcome.
                        "proto.native.underdocks.sunken_treasury" =>
                            state.NativeRunRng is null
                                ? ResolveLegacyEventGold(
                                    state.Rng, selected.Id, "primary", 52, 16)
                                : 0,
                        PrototypeNativeHiveEvents.LostWispId =>
                            state.NativeRunRng is null
                                ? ResolveLegacyEventGold(
                                    state.Rng, selected.Id, "primary", 45, 31)
                                : 0,
                        "proto.native.underdocks.sunken_statue" =>
                            state.NativeRunRng is null
                                ? ResolveLegacyEventGold(
                                    state.Rng, selected.Id, "primary", 101, 21)
                                : 0,
                        // Punch Off computes an entry-only 91..98 gold
                        // variable even though neither option pays it.
                        // Keep this draw to preserve the event RNG stream.
                        PrototypeNativePunchOff.EventId =>
                            state.NativeRunRng is null
                                ? ResolveLegacyEventGold(
                                    state.Rng, selected.Id, "primary", 91, 8)
                                : 0,
                        PrototypeNativeSharedEvents.ThisOrThatId =>
                            state.NativeRunRng is null
                                ? ResolveLegacyEventGold(
                                    state.Rng, selected.Id, "primary", 41, 28)
                                : 0,
                        _ => 0
                    },
                NativeEventSecondaryGold:
                    selected.Id == "proto.native.event.jungle_maze_adventure"
                        && state.NativeRunRng is null
                        ? ResolveLegacyEventGold(
                            state.Rng, selected.Id, "secondary", 35, 30)
                        : selected.Id == "proto.native.underdocks.sunken_treasury"
                            && state.NativeRunRng is null
                            ? ResolveLegacyEventGold(
                                state.Rng, selected.Id, "secondary", 303, 61)
                            : 0,
                NativeEventPotionSlot:
                    selected.Id == PrototypeNativeStoneOfAllTime.EventId
                        && state.NativeRunRng is null
                        ? PrototypeNativeStoneOfAllTime.RollPotionSlot(
                            state.Player, state.Rng)
                        : null),
            EventHistory = world.EventIds.Append(selected.Id).ToArray()
        };

        if (state.NativeRunRng is not null)
        {
            state = state with { World = world };
            world = state.World!;
            state = ResolveNativeEventGold(state);
            world = RequireWorld(state);
        }

        if (selected.Id == PrototypeNativeStoneOfAllTime.EventId
            && state.NativeRunRng is not null)
        {
            state = state with { World = world };
            var slots = PrototypeNativeStoneOfAllTime.OccupiedPotionSlots(
                state.Player);
            if (slots.Length == 0)
                throw new InvalidOperationException(
                    "Stone of All Time requires an owned potion.");
            var draw = NativeEventContextRngV111.ResolveIndex(
                state,
                world.Event!,
                PrototypeNativeStoneOfAllTime.NativeRngEventId,
                "event.stone_of_all_time.potion_slot.index_draw",
                "event:stone_of_all_time:potion_slots:"
                    + CanonicalJson.Sha256(slots),
                slots.Length);
            state = draw.State;
            world = state.World!;
            world = world with
            {
                Event = world.Event! with
                {
                    NativeEventPotionSlot = slots[draw.SelectedIndex]
                }
            };
            state = state with { World = world };
        }

        if (selected.Id == PrototypeNativeFakeMerchant.EventId)
        {
            state = state with { World = world };
            state = PrototypeNativeFakeMerchant.RollInventory(state);
            world = state.World!;
        }

        if (selected.Id == PrototypeNativeEndlessConveyor.EventId)
        {
            state = state with { World = world };
            state = PrototypeNativeEndlessConveyor.RollNextDish(state);
            world = state.World!;
        }

        if (selected.Id == PrototypeNativeFutureOfPotions.EventId)
        {
            state = state with { World = world };
            state = PrototypeNativeFutureOfPotions.RollOffers(state);
            world = state.World!;
        }

        return state with
        {
            World = world,
            Phase = RunPhase.Event
        };
    }

    private static IReadOnlyList<GameAction> GetEventActions(RunState state)
    {
        var eventState = RequireWorld(state).Event
            ?? throw new InvalidOperationException(
                "Event phase has no event state.");

        if (eventState.ControlledChoice is { } controlledChoice)
        {
            var request = controlledChoice.PendingRequest;
            return Enumerable.Range(0, request.SupportCount)
                .Select(index => GameAction.Create(
                    "choose_controlled_event_outcome",
                    new ChooseControlledEventOutcomePayload(index)))
                .ToArray();
        }

        if (eventState.PendingPotionReplacement is { } potionReplacement)
        {
            var actions = potionReplacement.CandidateSlots
                .Select(slot =>
                    GameAction.Create(
                        state.Player.PotionSlots[slot] is null
                            ? "take_event_potion"
                            : "replace_event_potion",
                        new ReplaceEventPotionPayload(
                            slot)))
                .ToList();
            actions.Add(GameAction.Empty("skip_event_potion"));
            return actions;
        }

        if (eventState.PendingDeckChoice is { } pending)
        {
            var actions = pending.CandidateCardInstanceIds
                .Select(cardInstanceId =>
                    GameAction.Create(
                        "choose_event_deck_card",
                        new ChooseEventDeckCardPayload(
                            cardInstanceId)))
                .ToList();
            if ((pending.MinimumSelectionsRemaining
                    ?? pending.RemainingSelections) == 0)
                actions.Add(GameAction.Empty("finish_relic_deck_choice"));
            return actions;
        }

        if (eventState.EventId == PrototypeNativeHiveEvents.WarHistorianRepyId)
            return GetNativeWarHistorianRepyActions(eventState);

        if (eventState.EventId == PrototypeNativeFakeMerchant.EventId)
        {
            var offered = eventState.OfferedChoiceIds
                ?? Array.Empty<string>();
            var fakeMerchantDefinition = PrototypeContent.Event(eventState.EventId);
            var actions = fakeMerchantDefinition.Choices
                .Where(choice => offered.Contains(
                        choice.Id, StringComparer.Ordinal)
                    && CanTakeEventChoice(state.Player, choice,
                        RequireWorld(state)))
                .Select(choice => GameAction.Create("event_choice",
                    new EventChoicePayload(choice.Id)))
                .ToList();
            actions.AddRange(state.Player.PotionSlots
                .Select((potion, slot) => (Potion: potion, Slot: slot))
                .Where(item => item.Potion?.PotionId
                    == PrototypeNativeFakeMerchant.FoulPotionId)
                .Select(item => GameAction.Create(
                    PrototypeNativeFakeMerchant.ThrowPotionAction,
                    new UsePotionPayload(item.Slot, null))));
            return actions;
        }

        if (PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                eventState.EventId)
            && eventState.NativePageIndex ==
                PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                    eventState.EventId))
            return [GameAction.Empty("continue_event")];

        if (eventState.EventId == PrototypeNativeFutureOfPotions.EventId)
        {
            return (eventState.NativeFuturePotionOffers
                    ?? Array.Empty<PrototypeFuturePotionOffer>())
                .Where(offer =>
                    (uint)offer.Slot < (uint)state.Player.PotionSlots.Length
                    && state.Player.PotionSlots[offer.Slot]?.PotionId
                        == offer.PotionId)
                .Select(offer => GameAction.Create(
                    PrototypeNativeFutureOfPotions.TradeAction,
                    new TradeFuturePotionPayload(offer.Slot)))
                .ToArray();
        }

        if (eventState.EventId == PrototypeNativeGloryEvents.RoundTeaPartyId)
            return GetNativeRoundTeaPartyActions(eventState);
        if (eventState.EventId == PrototypeNativeGloryEvents.TeaMasterId)
            return GetNativeTeaMasterActions(state, eventState);
        if (eventState.EventId == PrototypeNativeGloryEvents.TrialId)
            return GetNativeTrialActions(eventState);
        if (eventState.EventId == PrototypeNativeGloryEvents.TinkerTimeId)
            return GetNativeTinkerActions(eventState);

        if (eventState.EventId == PrototypeNativeHiveEvents.LanternKeyEventId
            && eventState.NativePageIndex == 1)
        {
            return [GameAction.Create("event_choice",
                new EventChoicePayload("fight"))];
        }

        if (eventState.EventId == PrototypeNativeHiveEvents.ColossalFlowerId)
        {
            return GetColossalFlowerActions(eventState);
        }

        if (eventState.EventId == PrototypeNativeEndlessConveyor.EventId)
        {
            return GetEndlessConveyorActions(state, eventState);
        }

        if (eventState.EventId == PrototypeNativePunchOff.EventId)
        {
            return GetPunchOffActions(eventState);
        }

        if (eventState.EventId == AbyssalBathsEventId)
        {
            return GetAbyssalBathsActions(eventState);
        }

        if (eventState.EventId == PrototypeNativeUnderdocksTrashHeap.EventId)
        {
            return GetTrashHeapActions();
        }

        if (eventState.EventId == PrototypeNativeDenseVegetation.EventId
            && eventState.NativePageIndex == 1)
        {
            return
            [
                GameAction.Create("event_choice",
                    new EventChoicePayload("fight"))
            ];
        }

        if (eventState.EventId
                == PrototypeNativeTabletOfTruth.EventId
            && eventState.NativePageIndex > 0)
        {
            return GetNativeTabletFollowupActions(eventState);
        }

        var definition =
            PrototypeContent.Event(
                eventState.EventId);

        var choices = eventState.EventId is NativeAncientRewardsV111.TezcataraId
                or NativePaelRewardsV111.PaelId
                or NativeTanxEventV111.EventId
                or NativeVakuuEventV111.EventId
                or NativeNonupeipeEventV111.EventId
                or NativeOrobasEventV111.EventId
                && eventState.OfferedChoiceIds is { } offeredIds
            ? offeredIds.Select(id => definition.Choices.Single(choice => choice.Id == id))
            : definition.Choices.AsEnumerable();
        return choices
            .Where(choice =>
                (eventState.OfferedChoiceIds is null
                    || eventState.OfferedChoiceIds.Contains(
                        choice.Id, StringComparer.Ordinal))
                && CanTakeEventChoice(
                    state.Player,
                    choice,
                    RequireWorld(state)))
            .Select(choice => GameAction.Create(
                "event_choice",
                new EventChoicePayload(choice.Id)))
            .ToArray();
    }

    internal static bool IsControlledEventChoice(
        string eventId, string choiceId)
    {
        if (!PrototypeContent.Events.TryGetValue(eventId, out var definition))
            return false;
        var choice = definition.Choices.FirstOrDefault(item =>
            StringComparer.Ordinal.Equals(item.Id, choiceId));
        if (eventId == PrototypeNativeStoneOfAllTime.EventId
            && (choiceId is "push" or "lift"))
            return choice is not null;
        return choice?.Effects.Any(effect =>
            effect.Kind is
                PrototypeRunEffectKind.GainRandomRareCard or
                PrototypeRunEffectKind.GainRandomRelic or
                PrototypeRunEffectKind.GainRandomPotion or
                PrototypeRunEffectKind.GainRandomUncommonPotion or
                PrototypeRunEffectKind.FillPotionSlots or
                PrototypeRunEffectKind.UpgradeRandomCard or
                PrototypeRunEffectKind.OfferThreeRareCards or
                PrototypeRunEffectKind.OfferThreeCardsAndPotion or
                PrototypeRunEffectKind.OfferFiveClassCards or
                PrototypeRunEffectKind.OfferThreeColorlessCards or
                PrototypeRunEffectKind.OfferTwoColorlessCards or
                PrototypeRunEffectKind.OfferCardBundles or
                PrototypeRunEffectKind.OfferNeowsBonesRelics or
                PrototypeRunEffectKind.OfferRandomRelic or
                PrototypeRunEffectKind.OfferWaxRelics
                or PrototypeRunEffectKind.TransformFirstCardOfId
            || effect.Kind == PrototypeRunEffectKind.GainRelic
                && (effect.RelicId == NativeAncientRewardsV111.ToyBoxId
                    || effect.RelicId == NativeDarvEventV111.CallingBellId
                    || HasControlledRelicAcquisitionRandomEffect(
                        effect.RelicId))) == true;
    }

    private static bool HasControlledRelicAcquisitionRandomEffect(
        string? relicId) =>
        relicId is not null
        && PrototypeContent.Relics.TryGetValue(relicId, out var relic)
        && (relicId is PrototypeNativeOrobasRelics.SandCastleId
                or NativeGloryAncientsV111.BeautifulBraceletId
                or NativeGloryAncientsV111.DistinguishedCapeId
                or NativePaelRewardsV111.LegionId
                or NativeGloryAncientsV111.FurCoatId
            || (relic.RunTriggers
            ?? Array.Empty<PrototypeRelicRunTriggerSpec>())
            .Where(trigger => trigger.EventKind ==
                PrototypeRunEventKind.RelicAcquired)
            .SelectMany(trigger => trigger.Effects)
            .Any(effect => effect.Kind is
                PrototypeRunEffectKind.TransformFirstCardOfId or
                PrototypeRunEffectKind.GainRandomRareCard or
                PrototypeRunEffectKind.GainRandomPotion or
                PrototypeRunEffectKind.FillPotionSlots));

    private static RunState StepEvent(
        RunState state,
        GameAction action)
    {
        var eventState = RequireWorld(state).Event
            ?? throw new InvalidOperationException(
                "Event phase has no event state.");

        if (eventState.ControlledChoice is { } continuation)
            return ResumeControlledEventChoice(state, action, continuation);

        if (ControlledChanceGuardV111.Active
            && action.Kind == "event_choice"
            && IsControlledEventChoice(
                eventState.EventId,
                action.ReadPayload<EventChoicePayload>().ChoiceId))
        {
            var replay = ControlledChanceContinuationExecutorV111.Resume(
                state, [], checkpoint => StepEventCore(checkpoint, action));
            if (replay.PendingChance is { } pendingChance)
                throw UnsupportedControlledEventContinuation(pendingChance);
            return replay.Run ?? throw new InvalidDataException(
                "Controlled event begin returned neither a run nor a pending chance request.");
        }

        return StepEventCore(state, action);
    }

    private static RunState ResumeControlledEventChoice(
        RunState state,
        GameAction action,
        PrototypeControlledEventChoiceContinuationV111 continuation)
    {
        if (action.Kind != "choose_controlled_event_outcome")
            throw new InvalidOperationException(
                "A suspended controlled event choice requires a Nature outcome.");
        continuation.Validate();
        var payload = action.ReadPayload<ChooseControlledEventOutcomePayload>();
        var request = continuation.PendingRequest;
        if (payload.SelectedIndex < 0
            || payload.SelectedIndex >= request.SupportCount)
            throw new InvalidOperationException(
                "Selected event outcome is outside the pending ordered support.");

        var selectedDecision = new RandomIndexDecisionV111(
            request, payload.SelectedIndex, "controlled-v111");
        selectedDecision.Validate();
        var decisions = continuation.Decisions.Append(selectedDecision).ToArray();
        var candidateLabels = continuation.PendingCandidateLabels
            ?? throw new InvalidDataException(
                "Controlled event-choice candidate labels are missing.");
        var decisionCandidateLabels = continuation.DecisionCandidateLabels!
            .Append((string[])candidateLabels.Clone()).ToArray();
        var priorDecisions = continuation.Decisions
            .Take(continuation.EffectDecisionStartIndex).ToArray();
        var priorOrdinals = priorDecisions
            .GroupBy(decision => decision.Request.StreamId,
                StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.Ordinal);
        var resumeFrame = continuation with
        {
            Decisions = decisions,
            DecisionCandidateLabels = decisionCandidateLabels
        };

        var replay = ControlledChanceContinuationExecutorV111.Resume(
            state,
            decisions.Skip(continuation.EffectDecisionStartIndex).ToArray(),
            checkpoint => StepEventCore(
                checkpoint,
                GameAction.Create(
                    "event_choice",
                    new EventChoicePayload(continuation.ChoiceId)),
                resumeFrame),
            priorOrdinals);
        if (replay.PendingChance is { } pendingChance)
            throw UnsupportedControlledEventContinuation(pendingChance);
        var result = replay.Run ?? throw new InvalidDataException(
            "Controlled event replay returned neither a run nor a pending chance request.");
        if (result.World?.Event?.ControlledChoice is not null)
            return result;

        var trace = new ControlledEventChoiceTraceV111(
            continuation.EventId,
            continuation.ChoiceId,
            decisions,
            decisionCandidateLabels);
        trace.Validate();
        return result with
        {
            ControlledEventChoiceHistory =
                (result.ControlledEventChoiceHistory
                    ?? Array.Empty<ControlledEventChoiceTraceV111>())
                .Append(trace).ToArray()
        };
    }

    private static NotSupportedException UnsupportedControlledEventContinuation(
        ControlledChanceRequiredExceptionV111 exception) =>
        new($"Controlled event chance site '{exception.Request.SiteId}' " +
            $"(operation '{exception.Request.OperationKind}') is outside " +
            "the resumable generic event-effect frame.");

    private static RunState StepEventCore(
        RunState state,
        GameAction action,
        PrototypeControlledEventChoiceContinuationV111? resume = null)
    {
        var rootState = state;
        var rootWorld = RequireWorld(state);
        var rootEvent = rootWorld.Event
            ?? throw new InvalidOperationException(
                "Event phase has no event state.");
        var world = resume is null
            ? rootWorld
            : resume.PartialState.World!.Fork() with
            {
                Event = resume.PartialState.World!.Event! with
                {
                    ControlledChoice = resume
                }
            };
        state = resume is null
            ? state
            : resume.PartialState.Fork() with
            {
                DecisionIndex = rootState.DecisionIndex,
                World = world
            };
        var eventState = world.Event!;
        var player = state.Player;
        var nextId = world.NextCardInstanceId;
        var nativeRunRng = state.NativeRunRng?.Fork();

        if (resume is not null
            && eventState.EventId == PrototypeNativeStoneOfAllTime.EventId
            && resume.ChoiceId == "lift")
        {
            var result = StepNativeStoneOfAllTimeLift(state);
            var resultWorld = RequireWorld(result);
            return result with
            {
                World = resultWorld with
                {
                    Event = resultWorld.Event! with { ControlledChoice = null }
                }
            };
        }

        if (resume is not null
            && eventState.EventId == PrototypeNativeStoneOfAllTime.EventId
            && resume.ChoiceId == "push")
        {
            var result = AdvanceEventContinuations(state);
            var resultWorld = RequireWorld(result);
            return result with
            {
                World = resultWorld with
                {
                    Event = resultWorld.Event! with { ControlledChoice = null }
                }
            };
        }

        if (resume is null)
        {
            if (eventState.EventId == PrototypeNativeStoneOfAllTime.EventId
                && action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId == "lift")
            {
                try
                {
                    return StepNativeStoneOfAllTimeLift(state);
                }
                catch (ControlledEventChoiceChanceRequiredException exception)
                {
                    var continuation =
                        new PrototypeControlledEventChoiceContinuationV111(
                            state,
                            eventState.EventId,
                            "lift",
                            0,
                            0,
                            [],
                            [],
                            null,
                            0,
                            0,
                            null,
                            [],
                            exception.Request,
                            exception.CandidateLabels,
                            []);
                    continuation.Validate();
                    return state with
                    {
                        World = world with
                        {
                            Event = eventState with
                            {
                                ControlledChoice = continuation
                            }
                        }
                    };
                }
            }

            if (eventState.EventId == PrototypeNativeFakeMerchant.EventId)
                return StepNativeFakeMerchant(state, action);

            if (PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                    eventState.EventId)
                && eventState.NativePageIndex ==
                    PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                        eventState.EventId))
            {
                RequireKind(action, "continue_event");
                if (NativeAncientRewardsV111.IsOpeningEvent(world)
                    || NativeTanxEventV111.IsOpeningEvent(world)
                    || NativeVakuuEventV111.IsOpeningEvent(world)
                    || NativeNonupeipeEventV111.IsOpeningEvent(world)
                    || StringComparer.Ordinal.Equals(
                        eventState.EventId,
                        PrototypeNativeOvergrowthEvents.NeowEventId)
                        && world.Act == 1
                        && world.Map.GenerationProfileId
                            is PrototypeNativeOvergrowthMap.GenerationProfileId
                                or PrototypeNativeOvergrowthMap.LegacyGenerationProfileId
                            or PrototypeNativeUnderdocks.GenerationProfileId
                        && world.Map.CurrentNodeId is null)
                {
                    // Ancient openings precede the map and do not complete a
                    // map room when their SetEventFinished page is dismissed.
                    return state with
                    {
                        Phase = RunPhase.MapChoice,
                        World = world with { Event = null }
                    };
                }
                return CompleteRoomToMap(state);
            }

            if (eventState.EventId == PrototypeNativeFutureOfPotions.EventId)
                return StepNativeFutureOfPotions(state, action);

            if (eventState.PendingPotionReplacement is { } potionReplacement)
            {
                if (StringComparer.Ordinal.Equals(
                        action.Kind,
                        "skip_event_potion"))
                {
                    state = state with
                    {
                        World = world with
                        {
                            Event = eventState with
                            {
                                PendingPotionReplacement = null
                            }
                        }
                    };
                    return AdvanceEventContinuations(state);
                }

                if (action.Kind != "take_event_potion"
                    && action.Kind != "replace_event_potion")
                {
                    throw new InvalidOperationException(
                        "Unexpected event potion reward action.");
                }
                var payload =
                    action.ReadPayload<
                        ReplaceEventPotionPayload>();
                if (!potionReplacement.CandidateSlots
                        .Contains(payload.Slot))
                {
                    throw new InvalidOperationException(
                        $"Potion slot {payload.Slot} is not eligible for the event reward.");
                }

                var isEmptySlot = player.PotionSlots[payload.Slot] is null;
                if (isEmptySlot != (action.Kind == "take_event_potion"))
                {
                    throw new InvalidOperationException(
                        "Event potion action disagrees with the destination slot.");
                }

                player = player with
                {
                    PotionSlots = isEmptySlot
                        ? FillEventPotionSlot(
                            player.PotionSlots,
                            payload.Slot,
                            potionReplacement.PotionId)
                        : ReplacePotionSlot(
                            player.PotionSlots,
                            payload.Slot,
                            potionReplacement.PotionId)
                };
                eventState = eventState with
                {
                    PendingPotionReplacement = null
                };
                state = state with
                {
                    Player = player,
                    NativeRunRng = nativeRunRng ?? state.NativeRunRng,
                    World = world with
                    {
                        Event = eventState
                    }
                };
                return AdvanceEventContinuations(state);
            }

            if (eventState.PendingDeckChoice is { } pending)
            {
                if (StringComparer.Ordinal.Equals(action.Kind,
                        "finish_relic_deck_choice"))
                {
                    if ((pending.MinimumSelectionsRemaining
                            ?? pending.RemainingSelections) != 0)
                        throw new InvalidOperationException(
                            "This event deck choice still requires a selection.");
                    eventState = eventState with { PendingDeckChoice = null };
                    state = state with
                    {
                        World = world with { Event = eventState }
                    };
                    return AdvanceEventContinuations(state);
                }

                RequireKind(
                    action,
                    "choose_event_deck_card");
                var payload =
                    action.ReadPayload<
                        ChooseEventDeckCardPayload>();
                if (!pending.CandidateCardInstanceIds
                        .Contains(payload.CardInstanceId))
                {
                    throw new InvalidOperationException(
                        $"Card instance {payload.CardInstanceId} is not eligible for the event deck choice.");
                }

                player = ResolveEventDeckChoice(
                    player,
                    pending,
                    payload.CardInstanceId,
                    state.Rng);

                var remainingCandidates =
                    pending.CandidateCardInstanceIds
                        .Where(id =>
                            id != payload.CardInstanceId)
                        .Where(id =>
                            player.Deck.Any(card =>
                                card.InstanceId == id))
                        .ToArray();
                var remainingSelections =
                    pending.RemainingSelections - 1;
                var minimumSelections = Math.Max(
                    0, (pending.MinimumSelectionsRemaining
                        ?? pending.RemainingSelections) - 1);

                if (remainingSelections > 0
                    && remainingCandidates.Length > 0)
                {
                    eventState = eventState with
                    {
                        PendingDeckChoice = pending with
                        {
                            RemainingSelections =
                                Math.Min(
                                    remainingSelections,
                                    remainingCandidates.Length),
                            CandidateCardInstanceIds = remainingCandidates,
                            MinimumSelectionsRemaining = Math.Min(
                                minimumSelections, remainingCandidates.Length)
                        }
                    };

                    return state with
                    {
                        Player = player,
                        World = world with
                        {
                            Event = eventState
                        }
                    };
                }

                eventState = eventState with
                {
                    PendingDeckChoice = null
                };
                state = state with
                {
                    Player = player,
                    World = world with
                    {
                        Event = eventState
                    }
                };
                return AdvanceEventContinuations(state);
            }

            if (eventState.EventId == PrototypeNativeHiveEvents.WarHistorianRepyId)
                return StepNativeWarHistorianRepy(state, action);

            if (eventState.EventId ==
                PrototypeNativeGloryEvents.HungryForMushroomsId)
                return StepNativeHungryForMushrooms(state, action);

            if (eventState.EventId == PrototypeNativeGloryEvents.ReflectionsId)
                return StepNativeGloryReflections(state, action);

            if (eventState.EventId == PrototypeNativeGloryEvents.RoundTeaPartyId)
                return StepNativeRoundTeaParty(state, action);
            if (eventState.EventId == PrototypeNativeGloryEvents.TeaMasterId)
                return StepNativeTeaMaster(state, action);
            if (eventState.EventId == PrototypeNativeGloryEvents.TrialId)
                return StepNativeTrial(state, action);
            if (eventState.EventId == PrototypeNativeGloryEvents.TinkerTimeId)
                return StepNativeTinker(state, action);
            if (eventState.EventId ==
                PrototypeNativeGloryEvents.BattlewornDummyId)
                return StepNativeBattlewornDummy(state, action);

            if (eventState.EventId == PrototypeNativeHiveEvents.LanternKeyEventId)
            {
                return StepNativeLanternKey(state, action);
            }

            if (eventState.EventId == PrototypeNativeHiveEvents.InfestedAutomatonId)
            {
                return StepNativeInfestedAutomaton(state, action);
            }

            if (eventState.EventId == PrototypeNativeHiveEvents.ColossalFlowerId)
            {
                return StepNativeColossalFlower(state, action);
            }

            if (eventState.EventId ==
                PrototypeNativeUnderdocksDoors.EventId
                && action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId == "light")
            {
                return StepNativeDoorsLight(state, action);
            }

            if (eventState.EventId == PrototypeNativeEndlessConveyor.EventId)
            {
                return StepEndlessConveyor(state, action);
            }

            if (eventState.EventId == PrototypeNativePunchOff.EventId)
            {
                return StepPunchOff(state, action);
            }

            if (eventState.EventId == AbyssalBathsEventId)
            {
                return StepAbyssalBaths(state, action);
            }

            if (eventState.EventId == PrototypeNativeUnderdocksTrashHeap.EventId)
            {
                return StepTrashHeap(state, action);
            }

            if (eventState.EventId == PrototypeNativeTabletOfTruth.EventId)
            {
                return StepNativeTabletOfTruth(state, action);
            }

            if (eventState.EventId == PrototypeNativeDenseVegetation.EventId)
            {
                return StepNativeDenseVegetation(state, action);
            }
        }

        if (resume is null)
            RequireKind(action, "event_choice");
        var choicePayload =
            resume is null
                ? action.ReadPayload<EventChoicePayload>()
                : new EventChoicePayload(resume.ChoiceId);
        var definition =
            PrototypeContent.Event(
                eventState.EventId);
        var choice = definition.Choices.FirstOrDefault(
            item => StringComparer.Ordinal.Equals(
                item.Id,
                choicePayload.ChoiceId))
            ?? throw new InvalidOperationException(
                $"Unknown event choice '{choicePayload.ChoiceId}'.");

        if (resume is null
            && ((eventState.OfferedChoiceIds is not null
                    && !eventState.OfferedChoiceIds.Contains(
                        choice.Id, StringComparer.Ordinal))
                || !CanTakeEventChoice(player, choice, world)))
        {
            throw new InvalidOperationException(
                $"Event choice '{choice.Id}' cannot currently be taken.");
        }

        var queuedPotionIds = resume is null
            ? new List<string>()
            : resume.QueuedPotionIds.ToList();
        var queuedDeckChoices = resume is null
            ? new List<PrototypePendingEventDeckChoiceState>()
            : resume.QueuedDeckChoices.Select(choice => choice.Fork()).ToList();
        RewardState? deferredReward = resume?.DeferredReward?.Fork();
        var deferredHpLoss = resume?.DeferredHpLoss ?? 0;
        var deferredGoldLoss = resume?.DeferredGoldLoss ?? 0;
        var deferredCardId = resume?.DeferredCardId;
        var decisions = resume?.Decisions ?? [];
        var decisionCandidateLabels =
            resume?.DecisionCandidateLabels ?? [];

        for (var effectIndex = resume?.NextEffectIndex ?? 0;
             effectIndex < choice.Effects.Length;
             effectIndex++)
        {
            var effect = choice.Effects[effectIndex];
            var effectDecisionStartIndex = resume is not null
                && effectIndex == resume.NextEffectIndex
                    ? resume.EffectDecisionStartIndex
                    : decisions.Length;
            var effectStartState = state.Fork() with
            {
                Player = player.Fork(),
                NativeRunRng = nativeRunRng?.Fork(),
                World = world.Fork() with
                {
                    NextCardInstanceId = nextId,
                    Event = eventState with { ControlledChoice = null }
                }
            };
            var effectStartQueuedPotionIds = queuedPotionIds.ToArray();
            var effectStartQueuedDeckChoices = queuedDeckChoices
                .Select(item => item.Fork()).ToArray();
            var effectStartDeferredReward = deferredReward?.Fork();
            var effectStartDeferredHpLoss = deferredHpLoss;
            var effectStartDeferredGoldLoss = deferredGoldLoss;
            var effectStartDeferredCardId = deferredCardId;
            try
            {
                switch (effect.Kind)
                {
                    case PrototypeRunEffectKind.Heal:
                        player = player with
                        {
                            Hp = Math.Min(
                                player.MaxHp,
                                player.Hp + effect.Amount)
                        };
                        break;

                    case PrototypeRunEffectKind.HealToFull:
                        player = player with
                        {
                            Hp = player.MaxHp
                        };
                        break;

                    case PrototypeRunEffectKind.HealPercentMaxHp:
                    {
                        var amount = Math.Max(
                            0,
                            player.MaxHp * effect.Amount / 100);
                        player = player with
                        {
                            Hp = Math.Min(
                                player.MaxHp,
                                player.Hp + amount)
                        };
                        break;
                    }

                    case PrototypeRunEffectKind.GainMaxHp:
                    {
                        var amount = Math.Max(
                            0,
                            effect.Amount);
                        player = player with
                        {
                            MaxHp = player.MaxHp + amount,
                            Hp = player.Hp + amount
                        };
                        break;
                    }

                    case PrototypeRunEffectKind.LoseMaxHp:
                    {
                        var amount = Math.Max(
                            0,
                            effect.Amount);
                        var maxHp = Math.Max(
                            1,
                            player.MaxHp - amount);
                        player = player with
                        {
                            MaxHp = maxHp,
                            Hp = Math.Min(
                                player.Hp,
                                maxHp)
                        };
                        break;
                    }

                    case PrototypeRunEffectKind.LoseHp:
                        player = player with
                        {
                            Hp = Math.Max(
                                0,
                                player.Hp - effect.Amount)
                        };
                        // Native CreatureCmd.Damage stops the event when
                        // it kills the player. Do not execute subsequent
                        // rewards (e.g. Jungle Maze's gold after damage).
                        if (player.Hp == 0)
                        {
                            return EndRun(state with { Player = player },
                                "defeat");
                        }
                        break;

                    case PrototypeRunEffectKind.LoseHpAfterDeckChoices:
                        deferredHpLoss += Math.Max(0, effect.Amount);
                        break;

                    case PrototypeRunEffectKind.GainGold:
                        player = player with
                        {
                            Gold = GoldAfterGain(player,
                                ResolveEventGoldAmount(
                                    eventState, choice.Id, effect))
                        };
                        break;

                    case PrototypeRunEffectKind.LoseGold:
                    {
                        var price = ResolveEventGoldAmount(
                            eventState, choice.Id, effect);
                        player = player with
                        {
                            Gold = player.Gold - price
                        };
                        break;
                    }

                    case PrototypeRunEffectKind.LoseGoldAfterDeckChoices:
                        deferredGoldLoss += Math.Max(0,
                            ResolveEventGoldAmount(eventState, choice.Id, effect));
                        break;

                    case PrototypeRunEffectKind.LoseAllGold:
                        player = player with { Gold = 0 };
                        break;

                    case PrototypeRunEffectKind.UpgradeRandomCard:
                    {
                        // Native selection filters CardModel.IsUpgradable;
                        // upgraded, eternal and non-upgradable event/status
                        // cards must not dilute random upgrade probabilities.
                        var candidates = player.Deck
                            .Where(card => CanSelectEventDeckCard(
                                card, PrototypePersistentDeckChoiceKind.Upgrade,
                                null, null, false))
                            .Select(card => card.InstanceId)
                            .ToArray();
                        if (candidates.Length > 0)
                        {
                            var selectedId = candidates[
                                ResolveLegacyRandomIndex(
                                    state.Rng,
                                    "event.random_upgrade.card_instance",
                                    "event:" + eventState.EventId + ":choice:"
                                        + choice.Id + ":pool:"
                                        + CanonicalJson.Sha256(candidates),
                                    Enumerable.Range(0, candidates.Length),
                                    streamId: "event",
                                    controlledCandidateLabels: candidates.Select(id =>
                                        player.Deck.Single(card =>
                                            card.InstanceId == id).CardId + "#" +
                                        id.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                        .ToArray())];
                            player = player with
                            {
                                Deck = player.Deck.Select(card =>
                                    card.InstanceId == selectedId
                                        ? card with
                                        {
                                            UpgradeLevel = card.UpgradeLevel + 1
                                        }
                                        : card).ToArray()
                            };
                        }
                        break;
                    }

                    case PrototypeRunEffectKind.UpgradeAllCards:
                        player = player with
                        {
                            Deck = player.Deck.Select(card =>
                                CanSelectEventDeckCard(
                                    card, PrototypePersistentDeckChoiceKind.Upgrade,
                                    null, null, false)
                                    ? card with
                                    {
                                        UpgradeLevel = card.UpgradeLevel + 1
                                    }
                                    : card).ToArray()
                        };
                        break;

                    case PrototypeRunEffectKind.AddCardAfterDeckChoices:
                        if (effect.CardId is null || deferredCardId is not null)
                        {
                            throw new InvalidOperationException(
                                "Deferred event card addition must have one card ID.");
                        }
                        deferredCardId = effect.CardId;
                        break;

                    case PrototypeRunEffectKind.AddCard:
                        if (effect.CardId is null)
                        {
                            throw new InvalidOperationException(
                                "Add-card event effect is missing a card ID.");
                        }

                        player = AppendCard(
                            player,
                            nextId++,
                            effect.CardId,
                            state.Rng);
                        break;

                    case PrototypeRunEffectKind.TransformFirstCardOfId:
                    {
                        var cardId = effect.CardId
                            ?? throw new InvalidOperationException(
                                "Transform-first-card effect requires a card ID.");
                        var first = player.Deck.FirstOrDefault(card =>
                            StringComparer.Ordinal.Equals(card.CardId, cardId));
                        if (first is not null)
                        {
                            player = TransformPersistentDeckCard(
                                player, first.InstanceId, false, state.Rng);
                        }

                        break;
                    }

                    case PrototypeRunEffectKind.OfferThreeRareCards:
                    case PrototypeRunEffectKind.OfferThreeCardsAndPotion:
                    case PrototypeRunEffectKind.OfferFiveClassCards:
                    case PrototypeRunEffectKind.OfferThreeColorlessCards:
                    case PrototypeRunEffectKind.OfferTwoColorlessCards:
                    case PrototypeRunEffectKind.OfferCardBundles:
                    case PrototypeRunEffectKind.OfferNeowsBonesRelics:
                    case PrototypeRunEffectKind.OfferRandomRelic:
                    case PrototypeRunEffectKind.OfferWaxRelics:
                        if (deferredReward is not null)
                        {
                            throw new InvalidOperationException(
                                "Only one custom reward offer per event option is supported.");
                        }
                        deferredReward = CreateDeferredEventReward(
                            state,
                            effect.Kind,
                            ref player,
                            eventState.EventId, ref world,
                            UsesNativeRegionalSystems(world)
                                ? nativeRunRng
                                : null);
                        break;

                    case PrototypeRunEffectKind.GainPotionSlots:
                        if (effect.Amount <= 0)
                        {
                            throw new InvalidOperationException(
                                "Potion slot increase must be positive.");
                        }
                        player = player with
                        {
                            PotionSlots = player.PotionSlots
                                .Concat(new PotionInstance?[effect.Amount])
                                .ToArray()
                        };
                        break;

                    case PrototypeRunEffectKind.GainRandomRareCard:
                    {
                        // Source-shaped Arcane Scroll reward: one Rare from the
                        // character card pool. The prototype reward pool and
                        // stream still differ from native v0.111.0.
                        var rares = PrototypeContent.RewardCardPool
                            .Where(cardId =>
                                PrototypeContent.Card(cardId).Rarity
                                    == PrototypeCardRarity.Rare)
                            .ToArray();
                        if (rares.Length == 0)
                        {
                            throw new InvalidOperationException(
                                "Random Rare-card reward has an empty pool.");
                        }

                        var cardId = rares[ResolveLegacyRandomIndex(
                            state.Rng,
                            "event.reward.random_rare_card.model",
                            "event:" + eventState.EventId + ":choice:"
                                + choice.Id + ":pool:" + CanonicalJson.Sha256(rares),
                            Enumerable.Range(0, rares.Length),
                            streamId: "reward",
                            controlledCandidateLabels: (string[])rares.Clone())];
                        player = AppendCard(
                            player, nextId++, cardId, state.Rng);
                        break;
                    }

                    case PrototypeRunEffectKind.GainRelic:
                    case PrototypeRunEffectKind.GainRandomRelic:
                    {
                        string relicId;
                        if (effect.Kind == PrototypeRunEffectKind.GainRandomRelic
                            && UsesNativeRegionalSystems(world))
                        {
                            var draw = PrototypeNativeRelicGrabBag.Draw(
                                world, player, state.Rng, merchant: false,
                                nativeRunRng: nativeRunRng);
                            world = draw.World;
                            relicId = draw.Id
                                ?? throw new InvalidOperationException(
                                    "No source-eligible event relics remain in the bag.");
                        }
                        else
                        {
                            relicId = effect.Kind
                                == PrototypeRunEffectKind.GainRandomRelic
                                ? PickUnownedEventRelic(
                                    player,
                                    state.Rng,
                                    "event:" + eventState.EventId
                                        + ":choice:" + choice.Id)
                                : effect.RelicId
                                    ?? throw new InvalidOperationException(
                                        "Gain-relic event effect is missing a relic ID.");
                        }
                        var relicDefinition =
                            PrototypeContent.Relic(
                                relicId);
                        // Direct fixed relic event rewards use RelicCmd.Obtain:
                        // a fresh mutable relic is appended even when another
                        // instance of this model already belongs to the player.
                        // Random pulls still use the single-copy grab bag.
                        if (player.Relics.Any(relic =>
                                StringComparer.Ordinal.Equals(
                                    relic.RelicId,
                                    relicId))
                            && (effect.Kind != PrototypeRunEffectKind.GainRelic
                                || !AllowsNativeEventDuplicateRelic(
                                    eventState.EventId, relicId)))
                        {
                            throw new InvalidOperationException(
                                $"Player already owns relic '{relicId}'.");
                        }

                        player = player with
                        {
                            Relics = player.Relics
                                .Append(
                                    new RelicInstance(
                                        relicId,
                                        PrototypeJson.EmptyObject()))
                                .ToArray()
                        };
                        player = ApplyRelicRunEvent(
                            player,
                            PrototypeRunEventKind.RelicAcquired,
                            acquiredRelicId: relicId,
                            rng: state.Rng, allocateCardId: () => nextId++,
                            nativeRunRng: nativeRunRng);

                        if (eventState.EventId == NativeDarvEventV111.EventId
                            && relicId == NativeDarvEventV111.DustyTomeId)
                        {
                            var cardInstanceId = nextId++;
                            player = AppendCard(
                                player, cardInstanceId,
                                NativeDarvEventV111.DustyTomeCardId,
                                state.Rng);
                            var maxUpgradeLevel = PrototypeContent.Card(
                                NativeDarvEventV111.DustyTomeCardId).MaxUpgradeLevel;
                            player = player with
                            {
                                Deck = player.Deck.Select(card =>
                                    card.InstanceId == cardInstanceId
                                        ? card with
                                        {
                                            UpgradeLevel = maxUpgradeLevel
                                        }
                                        : card).ToArray()
                            };
                        }
                        else if (eventState.EventId == NativeDarvEventV111.EventId
                            && relicId == NativeDarvEventV111.PandorasBoxId)
                        {
                            var transformedCardIds = player.Deck
                                .Where(NativeDarvEventV111.IsPandorasBoxTarget)
                                .Select(card => card.InstanceId)
                                .ToArray();
                            foreach (var cardInstanceId in transformedCardIds)
                            {
                                player = TransformPersistentDeckCard(
                                    player, cardInstanceId, false, state.Rng);
                            }
                        }

                        if (relicDefinition.CardIdAfterAcquisitionDeckChoices
                            is { } postChoiceCardId)
                        {
                            if (deferredCardId is not null)
                                throw new InvalidOperationException(
                                    "An event cannot queue multiple deferred card grants.");
                            deferredCardId = postChoiceCardId;
                        }

                        if (eventState.EventId == NativeNonupeipeEventV111.EventId
                            && relicId == NativeGloryAncientsV111.FurCoatId)
                        {
                            player = NativeGloryAncientsV111.MarkFurCoatCombats(
                                player, world.Map, world.Act, state.RunSeed,
                                state.Rng, nativeRunRng is not null);
                        }

                        if (relicId == NativeAncientRewardsV111.ToyBoxId)
                        {
                            if (deferredReward is not null)
                                throw new InvalidOperationException("Toy Box acquisition already has a custom reward.");
                            deferredReward = CreateDeferredEventReward(state,
                                PrototypeRunEffectKind.OfferWaxRelics, ref player,
                                eventState.EventId, ref world, nativeRunRng);
                        }
                        if (relicId == NativeDarvEventV111.CallingBellId)
                        {
                            if (deferredReward is not null)
                                throw new InvalidOperationException(
                                    "Calling Bell acquisition already has a custom reward.");
                            deferredReward = CreateCallingBellRelicReward(
                                player, eventState.EventId, state.Rng, ref world,
                                UsesNativeRegionalSystems(world)
                                    ? nativeRunRng
                                    : null);
                        }

                        var relicChoice =
                            CreateRelicEventDeckChoice(
                                choice.Id,
                                relicDefinition);
                        if (relicChoice is not null)
                        {
                            queuedDeckChoices.Add(relicChoice);
                        }

                        break;
                    }

                    case PrototypeRunEffectKind.GainPotion:
                    case PrototypeRunEffectKind.GainRandomPotion:
                    case PrototypeRunEffectKind.GainRandomUncommonPotion:
                    {
                        var nativeOvergrowth = UsesNativeRegionalSystems(world);
                        var potionId = effect.Kind
                            == PrototypeRunEffectKind.GainRandomUncommonPotion
                            ? PrototypeNativePotionCourier.PickRandomUncommonPotion(
                                state.Rng, "event")
                            : effect.Kind == PrototypeRunEffectKind.GainRandomPotion
                            ? nativeOvergrowth && eventState.EventId
                                == "proto.native.event.wellspring"
                                ? PrototypeNativePotionShop.PickUniform(
                                    state.Rng, "reward")
                                : nativeOvergrowth && eventState.EventId
                                    == "proto.native.event.whispering_hollow"
                                    ? PrototypeNativePotionShop.PickWeighted(
                                        state.Rng, "reward")
                                    : PrototypeContent.PotionPool[
                                        ResolveLegacyRandomIndex(
                                            state.Rng,
                                            "event.reward.random_potion.model",
                                            "event:" + eventState.EventId
                                                + ":choice:" + choice.Id
                                                + ":pool:" + CanonicalJson.Sha256(
                                                    PrototypeContent.PotionPool),
                                            Enumerable.Range(
                                                0, PrototypeContent.PotionPool.Length),
                                            streamId: "event",
                                            controlledCandidateLabels:
                                                (string[])PrototypeContent.PotionPool.Clone())]
                            : effect.PotionId
                                ?? throw new InvalidOperationException(
                                    "Gain-potion event effect is missing a potion ID.");
                        _ = PrototypeContent.Potion(
                            potionId);
                        if (!CanAcquirePotion(player))
                        {
                            throw new InvalidOperationException(
                                "Player cannot acquire potions.");
                        }

                        // Native potion-offer events call RewardsCmd.OfferCustom:
                        // receiving a reward is a player decision even when a
                        // potion slot is empty. Other prototype direct-grant
                        // events retain their historical acquisition behavior.
                        if (PrototypeNativeOvergrowthEvents.UsesNativePotionOffer(eventState.EventId)
                            || PrototypeNativeUnderdocksEvents.UsesNativePotionOffer(eventState.EventId))
                        {
                            queuedPotionIds.Add(potionId);
                        }
                        else
                        {
                            var emptySlot = Array.IndexOf(
                                player.PotionSlots, null);
                            if (emptySlot >= 0)
                            {
                                var slots = (PotionInstance?[])
                                    player.PotionSlots.Clone();
                                slots[emptySlot] = new PotionInstance(
                                    potionId, PrototypeJson.EmptyObject());
                                player = player with { PotionSlots = slots };
                            }
                            else
                            {
                                queuedPotionIds.Add(potionId);
                            }
                        }

                        break;
                    }

                    case PrototypeRunEffectKind.FillPotionSlots:
                        player =
                            FillEmptyPotionSlots(
                                player,
                                state.Rng);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (ControlledEventChoiceChanceRequiredException exception)
            {
                var continuation = new PrototypeControlledEventChoiceContinuationV111(
                    effectStartState,
                    eventState.EventId,
                    choice.Id,
                    effectIndex,
                    effectDecisionStartIndex,
                    effectStartQueuedPotionIds,
                    effectStartQueuedDeckChoices,
                    effectStartDeferredReward,
                    effectStartDeferredHpLoss,
                    effectStartDeferredGoldLoss,
                    effectStartDeferredCardId,
                    decisions,
                    exception.Request,
                    exception.CandidateLabels,
                    decisionCandidateLabels);
                continuation.Validate();
                return rootState with
                {
                    World = rootWorld with
                    {
                        Event = rootEvent with { ControlledChoice = continuation }
                    }
                };
            }
        }

        var explicitDeckChoice =
            CreateEventDeckChoice(
                choice);
        if (explicitDeckChoice is not null)
        {
            queuedDeckChoices.Add(explicitDeckChoice);
        }

        state = state with
        {
            Player = player,
            NativeRunRng = nativeRunRng,
            World = world with
            {
                NextCardInstanceId = nextId,
                Event = eventState with
                {
                    ChosenChoiceId = choice.Id,
                    PendingDeckChoice = null,
                    PendingPotionReplacement = null,
                    QueuedPotionIds = queuedPotionIds.ToArray(),
                    QueuedDeckChoices = queuedDeckChoices.ToArray(),
                    PendingReward = deferredReward,
                    DeferredHpLoss = deferredHpLoss,
                    DeferredGoldLoss = deferredGoldLoss,
                    DeferredCardId = deferredCardId,
                    ControlledChoice = null
                }
            }
        };

        if (player.Hp <= 0)
        {
            return EndRun(
                state,
                "defeat");
        }

        return AdvanceEventContinuations(state);
    }

    private static string PickUnownedEventRelic(
        PlayerState player,
        RngBundle rng,
        string ownerId)
    {
        var available = PrototypeContent.RelicPool
            .Where(id => !player.Relics.Any(relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, id)))
            .ToArray();
        if (available.Length == 0)
        {
            throw new InvalidOperationException(
                "No unowned prototype relics remain for an event reward.");
        }
        return available[ResolveLegacyRandomIndex(
            rng,
            StochasticSiteRegistryV111.UnownedRandomRelicSelectionSiteId,
            ownerId + ":pool:" + CanonicalJson.Sha256(available),
            Enumerable.Range(0, available.Length),
            streamId: "event",
            controlledCandidateLabels: (string[])available.Clone())];
    }

    // Persisted event rolls determine both choice legality and settlement.
    // Definitions supply a base amount for synthetic fixtures.
    private static int ResolveEventGoldAmount(
        EventState? eventState,
        string choiceId,
        PrototypeRunEffectSpec effect)
    {
        if (eventState is null)
        {
            return effect.Amount;
        }

        var rolled = eventState.NativeEventGold;
        if (eventState.EventId == PrototypeNativeSharedEvents.ThisOrThatId
            && choiceId == "plain"
            && effect.Kind == PrototypeRunEffectKind.GainGold
            && rolled is >= 41 and <= 68)
        {
            return rolled;
        }
        if (eventState.EventId
                == "proto.native.underdocks.sunken_treasury"
            && effect.Kind == PrototypeRunEffectKind.GainGold)
        {
            return choiceId switch
            {
                "first_chest" => rolled,
                "second_chest" => eventState.NativeEventSecondaryGold,
                _ => effect.Amount
            };
        }
        if ((eventState.EventId == "proto.native.event.whispering_hollow"
                && choiceId == "gold"
                && effect.Kind == PrototypeRunEffectKind.LoseGold
                && rolled is >= 26 and <= 44)
            || (eventState.EventId == "proto.native.event.luminous_choir"
                && choiceId == "tribute"
                && effect.Kind == PrototypeRunEffectKind.LoseGold
                && rolled is >= 100 and <= 149)
            || ((eventState.EventId == "proto.native.event.sunken_statue"
                    || eventState.EventId
                        == "proto.native.underdocks.sunken_statue")
                && choiceId == "dive"
                && effect.Kind == PrototypeRunEffectKind.GainGold
                && rolled is >= 101 and <= 121)
            || (eventState.EventId == "proto.native.event.jungle_maze_adventure"
                && choiceId == "solo"
                && effect.Kind == PrototypeRunEffectKind.GainGold
                && rolled is >= 135 and <= 164))
        {
            return rolled;
        }

        if (eventState.EventId == PrototypeNativeHiveEvents.LostWispId
            && choiceId == "search"
            && effect.Kind == PrototypeRunEffectKind.GainGold
            && rolled is >= 45 and <= 75)
        {
            return rolled;
        }

        if (eventState.EventId == "proto.native.event.jungle_maze_adventure"
            && choiceId == "join"
            && effect.Kind == PrototypeRunEffectKind.GainGold
            && eventState.NativeEventSecondaryGold is >= 35 and <= 64)
        {
            return eventState.NativeEventSecondaryGold;
        }

        return effect.Amount;
    }

    /// <summary>
    /// Source-pinned direct RelicCmd.Obtain calls that grant a new
    /// instance even if the player owns an identical model already.
    /// This deliberately does not change ordinary relic-bag uniqueness.
    /// </summary>
    private static bool AllowsNativeEventDuplicateRelic(
        string? eventId, string relicId) =>
        ((eventId is "proto.native.event.sunken_statue"
            or "proto.native.underdocks.sunken_statue")
            && relicId == "proto.native.event.sword_of_stone")
        || (eventId == "proto.native.underdocks.drowning_beacon"
            && relicId == "proto.native.underdocks.fresnel_lens")
        || (eventId == PrototypeNativeHiveEvents.LostWispId
            && relicId == PrototypeNativeHiveEvents.LostWispRelicId)
        || (eventId == NativeTanxEventV111.EventId
            && NativeGloryAncientsV111.Relics.Any(relic =>
                relic.Id == relicId))
        || (eventId == NativeNonupeipeEventV111.EventId
            && NativeGloryAncientsV111.Relics.Any(relic =>
                relic.Id == relicId))
        || (eventId == NativeOrobasEventV111.EventId
            && NativeOrobasEventV111.Definition.Choices.Any(choice =>
                choice.Effects.Any(effect => effect.RelicId == relicId)))
        || (eventId == NativeDarvEventV111.EventId
            && NativeDarvEventV111.Definition.Choices.Any(choice =>
                choice.Effects.Any(effect => effect.RelicId == relicId)));

    private static bool CanTakeEventChoice(
        PlayerState player,
        PrototypeEventChoiceDefinition choice,
        RunWorldState world)
    {
        if (world.Event?.EventId == PrototypeNativeSelfHelpBook.EventId
            && !PrototypeNativeSelfHelpBook.IsChoiceAvailable(
                player, choice.Id))
            return false;

        if (world.Event?.EventId == NativeDarvEventV111.EventId
            && choice.Effects.Any(effect =>
                effect.RelicId == NativeDarvEventV111.PandorasBoxId)
            && !NativeDarvEventV111.HasPandorasBoxTarget(player))
            return false;

        if (world.Event?.EventId == NativeOrobasEventV111.EventId
            && !NativeOrobasEventV111.IsChoiceEligible(player, choice.Id))
            return false;
        // Native Grave.IsAllowed gates the event on at least one
        // exhaustible deck card. Its Confront option is additionally
        // displayed as locked when no such card exists.
        if (world.Event?.EventId ==
                PrototypeNativeGloryEvents.GraveOfTheForgottenId
            && choice.Id == "confront"
            && !player.Deck.Any(card => CanSelectEventDeckCard(
                card, PrototypePersistentDeckChoiceKind.Enchant,
                null, PrototypeCardEnchantmentKind.SoulsPower, false)))
        {
            return false;
        }

        if (!MatchesRouteCondition(
                world,
                choice.RouteCondition))
        {
            return false;
        }

        var goldCost = choice.Effects
            .Where(effect =>
                effect.Kind
                    is PrototypeRunEffectKind.LoseGold
                        or PrototypeRunEffectKind.LoseGoldAfterDeckChoices)
            .Sum(effect =>
                Math.Max(0, ResolveEventGoldAmount(
                    world.Event, choice.Id, effect)));
        if (goldCost > player.Gold)
        {
            return false;
        }

        var hpCost = choice.Effects
            .Where(effect =>
                effect.Kind is
                    PrototypeRunEffectKind.LoseHp
                    or PrototypeRunEffectKind.LoseHpAfterDeckChoices)
            .Sum(effect =>
                Math.Max(0, effect.Amount));
        // The pinned solo event UI warns about lethal HP damage;
        // it does not lock these options. Native CreatureCmd.Damage
        // can end the run. Keep the historical safety restriction for
        // unverified prototype-only events.
        if (hpCost >= player.Hp
            && !PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(
                world.Event?.EventId ?? string.Empty)
            && !PrototypeNativeUnderdocksEvents.IsNativeRegionEvent(
                world.Event?.EventId ?? string.Empty)
            && !PrototypeNativeSharedEvents.IsSupported(
                world.Event?.EventId ?? string.Empty)
            && !PrototypeNativeHiveEvents.IsNativeEvent(
                world.Event?.EventId ?? string.Empty))
        {
            return false;
        }

        if (choice.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.GainRandomRelic)
            && !PrototypeContent.RelicPool.Any(id =>
                !player.Relics.Any(relic =>
                    StringComparer.Ordinal.Equals(relic.RelicId, id))))
        {
            return false;
        }

        var newlyGrantedRelics =
            new HashSet<string>(StringComparer.Ordinal);
        var canAcquirePotions = CanAcquirePotion(player);
        foreach (var effect in choice.Effects)
        {
            if (effect.Kind is
                PrototypeRunEffectKind.GainPotion
                or PrototypeRunEffectKind.GainRandomPotion
                or PrototypeRunEffectKind.GainRandomUncommonPotion)
            {
                if (effect.Kind == PrototypeRunEffectKind.GainPotion)
                {
                    if (effect.PotionId is null)
                    {
                        throw new InvalidOperationException(
                            "Gain-potion event effect is missing a potion ID.");
                    }

                    _ = PrototypeContent.Potion(effect.PotionId);
                }
                if (effect.Kind
                        == PrototypeRunEffectKind.GainRandomUncommonPotion
                    && PrototypeNativePotionCourier
                        .ImplementedUncommonPotionPool.Length == 0)
                {
                    return false;
                }
                if (!canAcquirePotions)
                {
                    return false;
                }
            }

            if (effect.Kind
                == PrototypeRunEffectKind.GainRelic)
            {
                if (effect.RelicId is null)
                {
                    throw new InvalidOperationException(
                        "Gain-relic event effect is missing a relic ID.");
                }

                var definition = PrototypeContent.Relic(
                    effect.RelicId);
                if ((player.Relics.Any(relic =>
                         StringComparer.Ordinal.Equals(
                             relic.RelicId, effect.RelicId))
                     && !AllowsNativeEventDuplicateRelic(
                         world.Event?.EventId, effect.RelicId))
                    || !newlyGrantedRelics.Add(effect.RelicId))
                {
                    return false;
                }

                if (definition.PreventPotionAcquisition)
                {
                    canAcquirePotions = false;
                }
            }
        }

        if (choice.DeckChoice is not { } deckChoice)
        {
            return true;
        }

        return PrototypeNativeOvergrowthEvents.AllowsEmptyDeckChoice(
                   world.Event?.EventId ?? string.Empty)
            || PrototypeNativeUnderdocksEvents.AllowsEmptyDeckChoice(
                   world.Event?.EventId ?? string.Empty)
            || PrototypeNativeHiveEvents.AllowsEmptyDeckChoice(
                   world.Event?.EventId ?? string.Empty)
            || player.Deck.Any(card =>
                CanSelectEventDeckCard(
                    card,
                    deckChoice.Kind,
                    deckChoice.TransformToCardId,
                    deckChoice.EnchantmentKind,
                    deckChoice.BasicCardsOnly)
                && (deckChoice.RequiredCardTag is null
                    || PrototypeContent.Card(card.CardId).Tags?.Contains(
                        deckChoice.RequiredCardTag, StringComparer.Ordinal) == true)
                && (deckChoice.RequiredCardType is null
                    || PrototypeContent.Card(card.CardId).Type
                        == deckChoice.RequiredCardType.Value));
    }

    private static PrototypePendingEventDeckChoiceState?
        CreateRelicEventDeckChoice(
            string choiceId,
            PrototypeRelicDefinition relic)
    {
        var spec = relic.AcquisitionDeckChoice;
        return spec is null || spec.Selections <= 0
            ? null
            : new PrototypePendingEventDeckChoiceState(
                choiceId,
                spec.Kind,
                spec.Selections,
                Array.Empty<long>(),
                spec.UpgradeTransformedCards,
                SourceRelicId: relic.Id,
                TransformToCardId: spec.TransformToCardId,
                EnchantmentKind: spec.EnchantmentKind,
                RequiresUpgradable: spec.RequiresUpgradable,
                PreserveUpgradeOnTransformation:
                    spec.PreserveUpgradeOnTransformation,
                PreserveEnchantmentOnTransformation:
                    spec.PreserveEnchantmentOnTransformation,
                MinimumSelectionsRemaining:
                    spec.MinimumSelections ?? spec.Selections);
    }

    private static PrototypePendingEventDeckChoiceState?
        CreateEventDeckChoice(
            PrototypeEventChoiceDefinition choice)
    {
        var spec = choice.DeckChoice;
        return spec is null || spec.Selections <= 0
            ? null
            : new PrototypePendingEventDeckChoiceState(
                choice.Id,
                spec.Kind,
                spec.Selections,
                Array.Empty<long>(),
                spec.UpgradeTransformedCards,
                TransformToCardId: spec.TransformToCardId,
                EnchantmentKind: spec.EnchantmentKind,
                BasicCardsOnly: spec.BasicCardsOnly,
                RequiredCardTag: spec.RequiredCardTag,
                RequiredCardType: spec.RequiredCardType,
                EnchantmentAmount: spec.EnchantmentAmount,
                MinimumSelectionsRemaining: spec.Selections);
    }

    private static PlayerState ResolveEventDeckChoice(
        PlayerState player,
        PrototypePendingEventDeckChoiceState pending,
        long cardInstanceId,
        RngBundle rng)
    {
        var selected = player.Deck.Single(
            card => card.InstanceId
                == cardInstanceId);
        if (pending.RequiresUpgradable
            && selected.UpgradeLevel >= PrototypeContent.Card(selected.CardId).MaxUpgradeLevel)
            throw new InvalidOperationException("Selected card is not upgradable.");
        if (pending.Kind == PrototypePersistentDeckChoiceKind.Remove)
            player = NativePaelRewardsV111.RememberToothRemoval(player, pending.SourceRelicId, selected);

        return pending.Kind switch
        {
            PrototypePersistentDeckChoiceKind.Remove =>
                player with
                {
                    Deck = player.Deck
                        .Where(card =>
                            card.InstanceId
                                != cardInstanceId)
                        .ToArray()
                },
            PrototypePersistentDeckChoiceKind.Upgrade =>
                player with
                {
                    Deck = player.Deck
                        .Select(card =>
                            card.InstanceId
                                == cardInstanceId
                                ? card with
                                {
                                    UpgradeLevel =
                                        card.UpgradeLevel + 1
                                }
                                : card)
                        .ToArray()
                },
            PrototypePersistentDeckChoiceKind.Transform =>
                pending.TransformToCardId is { } resultId
                    ? ApplyFixedEventTransformation(
                        player, selected.InstanceId, resultId,
                        pending.PreserveUpgradeOnTransformation,
                        pending.PreserveEnchantmentOnTransformation)
                    : TransformPersistentDeckCard(
                        player,
                        selected.InstanceId,
                        pending.UpgradeTransformedCards,
                        rng),
            PrototypePersistentDeckChoiceKind.Enchant =>
                ApplyEventEnchantment(
                    player,
                    selected.InstanceId,
                    pending.EnchantmentKind
                        ?? throw new InvalidOperationException(
                            "Event enchant selection is missing its type."),
                    pending.SourceRelicId is { } enchantRelic
                        ? PrototypeContent.Relic(enchantRelic).AcquisitionDeckChoice!.EnchantmentAmount
                        : pending.EnchantmentAmount),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static PlayerState FillEmptyPotionSlots(
        PlayerState player,
        RngBundle rng)
    {
        if (!CanAcquirePotion(player))
        {
            return player;
        }

        var slots =
            (PotionInstance?[])
            player.PotionSlots.Clone();
        for (var index = 0;
             index < slots.Length;
             index++)
        {
            if (slots[index] is not null)
            {
                continue;
            }

            var selectedIndex = ResolveLegacyRandomIndex(
                rng,
                "event.potion.fill_empty_slots.model",
                "player:potion_slot:" + index + ":pool:"
                    + CanonicalJson.Sha256(
                        PrototypeContent.PotionPool),
                Enumerable.Range(
                    0, PrototypeContent.PotionPool.Length),
                streamId: "event",
                controlledCandidateLabels:
                    (string[])PrototypeContent.PotionPool.Clone());
            var potionId = PrototypeContent.PotionPool[selectedIndex];
            slots[index] =
                new PotionInstance(
                    potionId,
                    PrototypeJson.EmptyObject());
        }

        return player with
        {
            PotionSlots = slots
        };
    }

    private static RunState StartShop(RunState state)
    {
        var world = RequireWorld(state);
        var nativeMerchant = UsesNativeRegionalSystems(world);
        var nativeRunRng = state.NativeRunRng?.Fork();
        using var nativeProviderScope = nativeMerchant && nativeRunRng is not null
            ? StochasticProviderScopeV111.Enter(
                new NativeRandomIndexProviderV111(),
                new NativeRandomUnitFloatProviderV111(),
                overrideExplicitProviders: false)
            : null;
        // MerchantInventory chooses one of the five character-card
        // slots before populating the inventory.
        var saleIndex = -1;
        if (nativeMerchant)
        {
            saleIndex = nativeRunRng is { } nativeShopRng
                ? NativePlayerRngV111.ChooseIndex(
                    nativeShopRng,
                    "shop.merchant.sale_slot.index_draw",
                    NativePlayerRngV111.ShopsStreamId,
                    $"merchant:act:{world.Act}:floor:{world.Floor}",
                    5)
                : ResolveLegacyRandomIndex(
                    state.Rng,
                    "shop.merchant.sale_slot.index_draw",
                    $"act:{world.Act}:floor:{world.Floor}",
                    Enumerable.Range(0, 5),
                    streamId: "shop");
        }
        var cardIds = new[]
            {
                (Type: PrototypeCardType.Attack, Count: 2),
                (Type: PrototypeCardType.Skill, Count: 2),
                (Type: PrototypeCardType.Power, Count: 1)
            }
            .SelectMany(group =>
                nativeMerchant
                    ? PrototypeNativeCardRarityOdds.GenerateMerchantCards(
                        group.Count, state.Ascension, group.Type,
                        world.CardRarityOffsetBasisPoints, state.Rng,
                        nativeRunRng)
                    : PickShopCards(
                        world.Act,
                        group.Count,
                        state.Rng,
                        group.Type))
            .ToArray();

        var offers = cardIds
            .Select((cardId, index) =>
            {
                var onSale = index == saleIndex;
                var basePrice = nativeMerchant
                    ? NativeMerchantCardPrice(
                        cardId, state.Rng, nativeRunRng,
                        $"act:{world.Act}:floor:{world.Floor}:offer:{index}:card:{cardId}")
                    : ShopCardPrice(cardId, state.Rng);
                if (onSale)
                {
                    // MerchantCardEntry.SetOnSale calls CalcCost
                    // again, then halves the newly rolled price.
                    basePrice =
                        NativeMerchantCardPrice(
                            cardId, state.Rng, nativeRunRng,
                            $"act:{world.Act}:floor:{world.Floor}:offer:{index}:sale_reprice:{cardId}") / 2;
                }
                return new ShopOffer(
                    index + 1,
                    cardId,
                    basePrice,
                    false,
                    BasePrice: basePrice,
                    OnSale: onSale);
            })
            .ToArray();

        // Native MerchantInventory has two dedicated ColorlessCardPool
        // slots, one Uncommon and one Rare, after the five character
        // slots. Use only implemented single-player cards until more
        // mechanics are added. The legacy six-floor prototype is unchanged.
        if (nativeMerchant)
        {
            var colorless = new[]
            {
                PrototypeColorlessCards.PickMerchantCard(
                    PrototypeCardRarity.Uncommon, state.Rng,
                    nativeRunRng: nativeRunRng),
                PrototypeColorlessCards.PickMerchantCard(
                    PrototypeCardRarity.Rare, state.Rng,
                    nativeRunRng: nativeRunRng)
            };
            offers = offers.Concat(colorless.Select((cardId, index) =>
            {
                var price = NativeMerchantCardPrice(
                    cardId, state.Rng, nativeRunRng,
                    $"act:{world.Act}:floor:{world.Floor}:colorless_offer:{index}:card:{cardId}");
                return new ShopOffer(
                    6 + index,
                    cardId,
                    price,
                    false,
                    BasePrice: price);
            })).ToArray();
        }

        var potionIds = nativeMerchant
            ? nativeRunRng is { } nativePotionRng
                ? PrototypeNativePotionShop.PickWeightedDistinctNativeWithAudit(
                    nativePotionRng,
                    "player.shops",
                    3,
                    $"merchant:act:{world.Act}:floor:{world.Floor}:potions",
                    new NativeRandomIndexProviderV111(),
                    new NativeRandomUnitFloatProviderV111()).PotionIds
                : PrototypeNativePotionShop.PickWeightedDistinct(
                    state.Rng, "shop", 3)
            : PickDistinct(
                PrototypeContent.PotionPool,
                3,
                state.Rng,
                "shop",
                "shop.potion_offer.shuffle_swap_index",
                $"act:{world.Act}:floor:{world.Floor}:potion_offers");
        var availableRelics =
            PrototypeContent.RelicPool
                .Where(id =>
                    !state.Player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(
                            relic.RelicId,
                            id)))
                .ToArray();
        string[] relicIds;
        if (nativeMerchant)
        {
            var selections = new List<string>(3);
            for (var index = 0; index < 3; index++)
            {
                PrototypeRelicRarity? forcedTier = index == 2
                    ? PrototypeRelicRarity.Shop
                    : null;
                if (nativeRunRng is null && forcedTier is null)
                    forcedTier = PrototypeNativeRelicGrabBag.RollRarity(
                        state.Rng, merchant: true,
                        ownerId: $"act:{world.Act}:floor:{world.Floor}:merchant_relic:{index}");
                var draw = PrototypeNativeRelicGrabBag.Draw(
                    world, state.Player, state.Rng,
                    merchant: true,
                    forcedRarity: forcedTier,
                    nativeRunRng: nativeRunRng,
                    merchantOfferOrdinal: index);
                world = draw.World;
                if (draw.Id is not null)
                {
                    selections.Add(draw.Id);
                }
            }
            relicIds = selections.ToArray();
        }
        else
        {
            relicIds = PickDistinct(
                availableRelics,
                3,
                state.Rng,
                "shop",
                "shop.relic_offer.shuffle_swap_index",
                $"act:{world.Act}:floor:{world.Floor}:relic_offers");
        }

        var potionOffers = potionIds
            .Select((potionId, index) =>
            {
                var basePrice = nativeMerchant
                    ? nativeRunRng is { } nativePotionRng
                        ? PrototypeNativePotionShop.MerchantPriceNative(
                            potionId, nativePotionRng,
                            $"act:{world.Act}:floor:{world.Floor}:offer:{index}:potion:{potionId}")
                        : PrototypeNativePotionShop.MerchantPrice(
                            potionId, state.Rng)
                    : ResolveLegacyRandomIndex(
                        state.Rng,
                        "shop.potion_offer.price.index_draw",
                        $"act:{world.Act}:floor:{world.Floor}:offer:{index}:potion:{potionId}",
                        PrototypeLegacyShopPriceV111.PotionOutcomes,
                        streamId: "shop");
                return new ShopOffer(
                    100 + index,
                    potionId,
                    basePrice,
                    false,
                    BasePrice: basePrice);
            })
            .ToArray();
        var relicOffers = relicIds
            .Select((relicId, index) =>
            {
                var basePrice = nativeMerchant
                    ? nativeRunRng is { } nativeRelicRng
                        ? PrototypeNativeRelicGrabBag.MerchantPriceNative(
                            relicId, nativeRelicRng,
                            $"act:{world.Act}:floor:{world.Floor}:offer:{index}:relic:{relicId}")
                        : PrototypeNativeRelicGrabBag.MerchantPrice(
                            relicId, state.Rng)
                    : ResolveLegacyRandomIndex(
                        state.Rng,
                        "shop.relic_offer.price.index_draw",
                        $"act:{world.Act}:floor:{world.Floor}:offer:{index}:relic:{relicId}",
                        PrototypeLegacyShopPriceV111.RelicOutcomes,
                        streamId: "shop");
                return new ShopOffer(
                    200 + index,
                    relicId,
                    basePrice,
                    false,
                    BasePrice: basePrice);
            })
            .ToArray();
        // Native MerchantCardRemovalEntry uses the player's run-wide
        // completed-removal count, independent of act and shop visits.
        // Ascension 6 (Inflation) raises both base and increment.
        var inflated = state.Ascension >= 6;
        var removalBasePrice = checked(
            (inflated ? 100 : 75)
            + (inflated ? 50 : 25) * world.ShopRemovalsUsed);

        var shop = new ShopState(
            offers,
            potionOffers.FirstOrDefault(),
            relicOffers.FirstOrDefault(),
            RemovalPrice: removalBasePrice,
            BaseRemovalPrice: removalBasePrice,
            AdditionalPotionOffers:
                potionOffers.Skip(1).ToArray(),
            AdditionalRelicOffers:
                relicOffers.Skip(1).ToArray());

        var player = ApplyRelicRunEvent(
            state.Player,
            PrototypeRunEventKind.ShopEntered,
            rng: state.Rng);
        shop = RepriceShop(
            shop,
            player);
        if (player.Relics.Any(relic =>
                StringComparer.Ordinal.Equals(
                    relic.RelicId,
                    NativeGloryAncientsV111.LordsParasolId)))
        {
            shop = shop with { LordsParasolPurchaseFlowActive = true };
        }
        world = world with { Shop = shop };

        var started = state with
        {
            Player = player,
            World = world,
            Phase = RunPhase.Shop,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng
        };
        return AdvanceLordsParasolPurchases(started);
    }

    private static IReadOnlyList<GameAction> GetShopActions(RunState state)
    {
        var world = RequireWorld(state);
        var shop = world.Shop
            ?? throw new InvalidOperationException("Shop phase has no shop state.");
        if (shop.PendingDeckChoice is { } pending)
        {
            var pendingActions = pending.CandidateCardInstanceIds
                .Select(cardInstanceId => GameAction.Create(
                    "choose_shop_relic_deck_card",
                    new ChooseDeckCardPayload(cardInstanceId)))
                .ToList();
            if (pending.MinimumSelectionsRemaining == 0)
                pendingActions.Add(GameAction.Empty("finish_relic_deck_choice"));
            return pendingActions;
        }

        if (shop.LordsParasolPendingPotionOfferId is { } parasolPotionId)
        {
            return state.Player.PotionSlots
                .Select((potion, slot) => (Potion: potion, Slot: slot))
                .Where(item => item.Potion is not null)
                .Select(item => GameAction.Create(
                    "replace_shop_potion",
                    new ReplaceShopPotionPayload(
                        parasolPotionId,
                        item.Slot)))
                .ToArray();
        }

        if (shop.LordsParasolRemovalChoicePending)
        {
            return state.Player.Deck
                .Where(card => !PrototypeCardRules.IsEternal(card))
                .Select(card => GameAction.Create(
                    "remove_card",
                    new RemoveCardPayload(card.InstanceId)))
                .ToArray();
        }

        var actions = new List<GameAction>();

        actions.AddRange(state.Player.PotionSlots
            .Select((potion, slot) => (Potion: potion, Slot: slot))
            .Where(item => item.Potion?.PotionId
                == PrototypeNativePotionCourier.FoulPotionId)
            .Select(item => GameAction.Create(
                "throw_foul_potion_at_merchant",
                new UsePotionPayload(item.Slot, null))));

        foreach (var offer in shop.CardOffers.Where(offer => !offer.Sold && offer.Price <= state.Player.Gold))
        {
            actions.Add(GameAction.Create("buy_card", new BuyOfferPayload(offer.OfferId)));
        }

        foreach (var potion in shop.PotionOffers
                     .Where(offer =>
                         !offer.Sold
                         && offer.Price <= state.Player.Gold
                         && CanAcquirePotion(state.Player)))
        {
            var emptySlot =
                Array.IndexOf(
                    state.Player.PotionSlots,
                    null);
            if (emptySlot >= 0)
            {
                actions.Add(
                    GameAction.Create(
                        "buy_potion",
                        new BuyOfferPayload(
                            potion.OfferId)));
            }
            else
            {
                actions.AddRange(
                    state.Player.PotionSlots
                        .Select((item, slot) =>
                            (Potion: item, Slot: slot))
                        .Where(item =>
                            item.Potion is not null)
                        .Select(item =>
                            GameAction.Create(
                                "replace_shop_potion",
                                new ReplaceShopPotionPayload(
                                    potion.OfferId,
                                    item.Slot))));
            }
        }

        foreach (var relic in shop.RelicOffers
                     .Where(offer =>
                         !offer.Sold
                         && offer.Price <= state.Player.Gold))
        {
            actions.Add(
                GameAction.Create(
                    "buy_relic",
                    new BuyOfferPayload(
                        relic.OfferId)));
        }

        if (!shop.RemovalUsed && shop.RemovalPrice <= state.Player.Gold)
        {
            actions.AddRange(
                state.Player.Deck
                    .Where(card => !PrototypeCardRules.IsEternal(card))
                    .Select(card => GameAction.Create(
                        "remove_card",
                        new RemoveCardPayload(card.InstanceId))));
        }

        if (!shop.LordsParasolPurchaseFlowActive)
            actions.Add(GameAction.Empty("leave_shop"));
        return actions;
    }

    private static RunState StepShop(RunState state, GameAction action)
    {
        return AdvanceLordsParasolPurchases(StepShopCore(state, action));
    }

    private static RunState StepShopCore(
        RunState state,
        GameAction action,
        bool ignoreCost = false)
    {
        var world = RequireWorld(state);
        var shop = world.Shop
            ?? throw new InvalidOperationException("Shop phase has no shop state.");
        if (shop.LordsParasolPendingPotionOfferId is { } pendingPotionOfferId
            && StringComparer.Ordinal.Equals(
                action.Kind, "replace_shop_potion"))
        {
            ignoreCost |= action.ReadPayload<ReplaceShopPotionPayload>()
                .OfferId == pendingPotionOfferId;
        }
        var nativeRunRng = state.NativeRunRng?.Fork();

        if (shop.PendingDeckChoice is { } pending)
        {
            PlayerState resolvedPlayer;
            PrototypePendingDeckChoiceState? remaining;
            if (StringComparer.Ordinal.Equals(
                    action.Kind, "finish_relic_deck_choice"))
            {
                if (pending.MinimumSelectionsRemaining != 0)
                    throw new InvalidOperationException(
                        "This relic deck choice still requires a selection.");
                resolvedPlayer = state.Player;
                remaining = null;
            }
            else
            {
                RequireKind(action, "choose_shop_relic_deck_card");
                var payload = action.ReadPayload<ChooseDeckCardPayload>();
                var resolved = ResolveRelicDeckChoice(
                    state.Player,
                    pending,
                    payload.CardInstanceId,
                    state.Rng);
                resolvedPlayer = resolved.Player;
                remaining = resolved.Remaining;
            }

            var nextCardInstanceId = world.NextCardInstanceId;
            if (remaining is null
                && pending.CardIdAfterChoices is { } afterChoicesCardId)
            {
                resolvedPlayer = AppendCard(
                    resolvedPlayer, nextCardInstanceId++, afterChoicesCardId, state.Rng);
            }
            return state with
            {
                Player = resolvedPlayer,
                NativeRunRng = nativeRunRng ?? state.NativeRunRng,
                World = world with
                {
                    Shop = shop with
                    {
                        PendingDeckChoice = remaining
                    },
                    NextCardInstanceId = nextCardInstanceId
                }
            };
        }

        if (StringComparer.Ordinal.Equals(action.Kind, "leave_shop"))
        {
            return CompleteRoomToMap(state);
        }

        var player = state.Player;
        var goldBeforePurchase = player.Gold;
        var nextId = world.NextCardInstanceId;

        if (StringComparer.Ordinal.Equals(
                action.Kind, "throw_foul_potion_at_merchant"))
        {
            var payload = action.ReadPayload<UsePotionPayload>();
            if (payload.Slot < 0
                || payload.Slot >= player.PotionSlots.Length
                || player.PotionSlots[payload.Slot]?.PotionId
                    != PrototypeNativePotionCourier.FoulPotionId)
            {
                throw new InvalidOperationException(
                    "The selected slot does not contain a Foul Potion.");
            }

            var potionSlots = (PotionInstance?[])player.PotionSlots.Clone();
            potionSlots[payload.Slot] = null;
            player = player with
            {
                Gold = GoldAfterGain(player, 100),
                PotionSlots = potionSlots
            };
        }
        else if (StringComparer.Ordinal.Equals(action.Kind, "remove_card"))
        {
            var freeParasolRemoval =
                shop.LordsParasolRemovalChoicePending;
            if (shop.RemovalUsed && !freeParasolRemoval)
            {
                throw new InvalidOperationException("Shop card removal was already used.");
            }

            if (!freeParasolRemoval && !ignoreCost
                && player.Gold < shop.RemovalPrice)
            {
                throw new InvalidOperationException("Shop card removal is unaffordable.");
            }

            var payload = action.ReadPayload<RemoveCardPayload>();
            var cardToRemove = player.Deck.FirstOrDefault(
                card => card.InstanceId == payload.CardInstanceId)
                ?? throw new InvalidOperationException(
                    $"Card instance {payload.CardInstanceId} is missing.");

            if (PrototypeCardRules.IsEternal(cardToRemove))
            {
                throw new InvalidOperationException(
                    $"Eternal card {payload.CardInstanceId} cannot be removed from the deck.");
            }

            player = player with
            {
                Gold = freeParasolRemoval
                    ? player.Gold
                    : player.Gold - (ignoreCost ? 0 : shop.RemovalPrice),
                Deck = player.Deck
                    .Where(card => card.InstanceId != payload.CardInstanceId)
                    .ToArray()
            };
            shop = shop with
            {
                RemovalUsed = true,
                LordsParasolRemovalChoicePending = false
            };
            world = world with
            {
                ShopRemovalsUsed = checked(world.ShopRemovalsUsed + 1)
            };
        }
        else if (StringComparer.Ordinal.Equals(action.Kind, "buy_card"))
        {
            var payload = action.ReadPayload<BuyOfferPayload>();
            var offer = shop.CardOffers.FirstOrDefault(item => item.OfferId == payload.OfferId)
                ?? throw new InvalidOperationException($"Unknown card offer {payload.OfferId}.");
            EnsurePurchasable(player, offer, ignoreCost);
            player = AppendCard(
                player with
                {
                    Gold = player.Gold - (ignoreCost ? 0 : offer.Price)
                },
                nextId++,
                offer.ItemId,
                state.Rng);
            shop = shop with
            {
                CardOffers = shop.CardOffers
                    .Select(item => item.OfferId == offer.OfferId ? item with { Sold = true } : item)
                    .ToArray()
            };
        }
        else if (StringComparer.Ordinal.Equals(action.Kind, "buy_potion"))
        {
            var payload = action.ReadPayload<BuyOfferPayload>();
            var offer = shop.PotionOffers
                .FirstOrDefault(item =>
                    item.OfferId == payload.OfferId)
                ?? throw new InvalidOperationException(
                    $"Unknown potion offer {payload.OfferId}.");

            EnsurePurchasable(player, offer, ignoreCost);
            if (!CanAcquirePotion(player))
            {
                throw new InvalidOperationException(
                    "Player cannot acquire potions.");
            }

            var slot = Array.IndexOf(player.PotionSlots, null);
            if (slot < 0)
            {
                throw new InvalidOperationException("No potion slot is available.");
            }

            var slots = (PotionInstance?[])player.PotionSlots.Clone();
            slots[slot] = new PotionInstance(offer.ItemId, PrototypeJson.EmptyObject());
            player = player with
            {
                Gold = player.Gold - (ignoreCost ? 0 : offer.Price),
                PotionSlots = slots
            };
            shop = UpdatePotionOffer(
                shop,
                offer with { Sold = true });
        }
        else if (StringComparer.Ordinal.Equals(
                     action.Kind,
                     "replace_shop_potion"))
        {
            var payload =
                action.ReadPayload<
                    ReplaceShopPotionPayload>();
            var offer = shop.PotionOffers
                .FirstOrDefault(item =>
                    item.OfferId == payload.OfferId)
                ?? throw new InvalidOperationException(
                    $"Unknown potion offer {payload.OfferId}.");

            EnsurePurchasable(player, offer, ignoreCost);
            if (!CanAcquirePotion(player))
            {
                throw new InvalidOperationException(
                    "Player cannot acquire potions.");
            }

            if (Array.IndexOf(
                    player.PotionSlots,
                    null) >= 0)
            {
                throw new InvalidOperationException(
                    "Potion replacement is only needed when all slots are occupied.");
            }

            player = player with
            {
                Gold =
                    player.Gold - (ignoreCost ? 0 : offer.Price),
                PotionSlots = ReplacePotionSlot(
                    player.PotionSlots,
                    payload.Slot,
                    offer.ItemId)
            };
            shop = UpdatePotionOffer(
                shop,
                offer with { Sold = true });
        }
        else if (StringComparer.Ordinal.Equals(action.Kind, "buy_relic"))
        {
            var payload = action.ReadPayload<BuyOfferPayload>();
            var offer = shop.RelicOffers
                .FirstOrDefault(item =>
                    item.OfferId == payload.OfferId)
                ?? throw new InvalidOperationException(
                    $"Unknown relic offer {payload.OfferId}.");

            EnsurePurchasable(player, offer, ignoreCost);
            player = player with
            {
                Gold = player.Gold - (ignoreCost ? 0 : offer.Price),
                Relics = player.Relics.Append(
                    new RelicInstance(offer.ItemId, PrototypeJson.EmptyObject())).ToArray()
            };
            player = ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: offer.ItemId,
                rng: state.Rng, allocateCardId: () => nextId++,
                nativeRunRng: nativeRunRng);
            shop = UpdateRelicOffer(
                shop,
                offer with { Sold = true });
            shop = RepriceShop(
                shop,
                player);
            shop = shop with
            {
                PendingDeckChoice = CreateRelicDeckChoice(
                    player,
                    offer.ItemId)
            };
        }
        else
        {
            throw new InvalidOperationException($"Unknown shop action '{action.Kind}'.");
        }

        // Native Maw Bank ceases room-entry gold on the FIRST
        // positive-gold merchant purchase, including card removal.
        player = ApplyFirstPositiveShopPurchase(
            player, Math.Max(0, goldBeforePurchase - player.Gold));

        world = world with
        {
            Shop = shop,
            NextCardInstanceId = nextId
        };

        return state with
        {
            Player = player,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
            World = world
        };
    }

    private static RunState AdvanceLordsParasolPurchases(RunState state)
    {
        var world = state.World;
        var shop = world?.Shop;
        if (state.Phase != RunPhase.Shop
            || world is null
            || shop is null
            || !shop.LordsParasolPurchaseFlowActive)
        {
            return state;
        }

        while (true)
        {
            shop = world.Shop!;
            if (shop.PendingDeckChoice is not null)
                return state with { World = world };

            if (shop.LordsParasolPendingPotionOfferId is { } pendingPotionId)
            {
                var pendingPotion = shop.PotionOffers.FirstOrDefault(offer =>
                    offer.OfferId == pendingPotionId);
                if (pendingPotion is null || pendingPotion.Sold)
                {
                    world = world with
                    {
                        Shop = shop with
                        {
                            LordsParasolPendingPotionOfferId = null
                        }
                    };
                    state = state with { World = world };
                    continue;
                }

                if (Array.IndexOf(state.Player.PotionSlots, null) < 0
                    && CanAcquirePotion(state.Player))
                {
                    return state with { World = world };
                }

                world = world with
                {
                    Shop = shop with
                    {
                        LordsParasolPendingPotionOfferId = null
                    }
                };
                state = state with { World = world };
                continue;
            }

            if (shop.LordsParasolRemovalChoicePending)
            {
                if (state.Player.Deck.Any(card =>
                        !PrototypeCardRules.IsEternal(card)))
                    return state with { World = world };

                world = world with
                {
                    Shop = shop with
                    {
                        LordsParasolRemovalChoicePending = false,
                        LordsParasolPurchaseFlowActive = false
                    }
                };
                return state with { World = world };
            }

            var card = shop.CardOffers.FirstOrDefault(offer => !offer.Sold);
            if (card is not null)
            {
                state = StepShopCore(
                    state with { World = world },
                    GameAction.Create(
                        "buy_card",
                        new BuyOfferPayload(card.OfferId)),
                    ignoreCost: true);
                world = state.World!;
                continue;
            }

            var relic = shop.RelicOffers.FirstOrDefault(offer => !offer.Sold);
            if (relic is not null)
            {
                state = StepShopCore(
                    state with { World = world },
                    GameAction.Create(
                        "buy_relic",
                        new BuyOfferPayload(relic.OfferId)),
                    ignoreCost: true);
                world = state.World!;
                continue;
            }

            var potion = shop.LordsParasolPotionOffersSkipped
                ? null
                : shop.PotionOffers.FirstOrDefault(offer => !offer.Sold);
            if (potion is not null)
            {
                if (!CanAcquirePotion(state.Player))
                {
                    world = world with
                    {
                        Shop = shop with
                        {
                            LordsParasolPotionOffersSkipped = true
                        }
                    };
                    state = state with { World = world };
                    continue;
                }

                if (Array.IndexOf(state.Player.PotionSlots, null) < 0)
                {
                    world = world with
                    {
                        Shop = shop with
                        {
                            LordsParasolPendingPotionOfferId = potion.OfferId
                        }
                    };
                    return state with { World = world };
                }

                world = world with { Shop = shop };
                state = StepShopCore(
                    state with { World = world },
                    GameAction.Create(
                        "buy_potion",
                        new BuyOfferPayload(potion.OfferId)),
                    ignoreCost: true);
                world = state.World!;
                continue;
            }

            if (!shop.RemovalUsed
                && state.Player.Deck.Any(card =>
                    !PrototypeCardRules.IsEternal(card)))
            {
                world = world with
                {
                    Shop = shop with
                    {
                        LordsParasolRemovalChoicePending = true
                    }
                };
                return state with { World = world };
            }

            world = world with
            {
                Shop = shop with
                {
                    LordsParasolPurchaseFlowActive = false
                }
            };
            return state with { World = world };
        }
    }

    private static ShopState UpdatePotionOffer(
        ShopState shop,
        ShopOffer updated)
    {
        if (shop.PotionOffer?.OfferId
            == updated.OfferId)
        {
            return shop with
            {
                PotionOffer = updated
            };
        }

        var additional =
            shop.AdditionalPotionOffers
            ?? Array.Empty<ShopOffer>();
        if (!additional.Any(offer =>
                offer.OfferId == updated.OfferId))
        {
            throw new InvalidOperationException(
                $"Unknown potion offer {updated.OfferId}.");
        }

        return shop with
        {
            AdditionalPotionOffers = additional
                .Select(offer =>
                    offer.OfferId == updated.OfferId
                        ? updated
                        : offer)
                .ToArray()
        };
    }

    private static ShopState UpdateRelicOffer(
        ShopState shop,
        ShopOffer updated)
    {
        if (shop.RelicOffer?.OfferId
            == updated.OfferId)
        {
            return shop with
            {
                RelicOffer = updated
            };
        }

        var additional =
            shop.AdditionalRelicOffers
            ?? Array.Empty<ShopOffer>();
        if (!additional.Any(offer =>
                offer.OfferId == updated.OfferId))
        {
            throw new InvalidOperationException(
                $"Unknown relic offer {updated.OfferId}.");
        }

        return shop with
        {
            AdditionalRelicOffers = additional
                .Select(offer =>
                    offer.OfferId == updated.OfferId
                        ? updated
                        : offer)
                .ToArray()
        };
    }

    private static PotionInstance?[] ReplacePotionSlot(
        PotionInstance?[] potionSlots,
        int slot,
        string potionId)
    {
        if (slot < 0 || slot >= potionSlots.Length)
        {
            throw new InvalidOperationException(
                $"Potion slot {slot} is invalid.");
        }

        if (potionSlots[slot] is null)
        {
            throw new InvalidOperationException(
                $"Potion slot {slot} is empty and cannot be replaced.");
        }

        _ = PrototypeContent.Potion(potionId);
        var slots =
            (PotionInstance?[])potionSlots.Clone();
        slots[slot] = new PotionInstance(
            potionId,
            PrototypeJson.EmptyObject());
        return slots;
    }

    private static void EnsurePurchasable(
        PlayerState player,
        ShopOffer offer,
        bool ignoreCost = false)
    {
        if (offer.Sold)
        {
            throw new InvalidOperationException($"Offer {offer.OfferId} is already sold.");
        }

        if (!ignoreCost && player.Gold < offer.Price)
        {
            throw new InvalidOperationException($"Offer {offer.OfferId} is unaffordable.");
        }
    }

    private static PotionInstance?[] FillEventPotionSlot(
        PotionInstance?[] slots, int slot, string potionId)
    {
        if (slot < 0 || slot >= slots.Length || slots[slot] is not null)
        {
            throw new InvalidOperationException(
                "Cannot accept an event potion into an occupied or invalid slot.");
        }

        _ = PrototypeContent.Potion(potionId);
        var copy = (PotionInstance?[])slots.Clone();
        copy[slot] = new PotionInstance(potionId, PrototypeJson.EmptyObject());
        return copy;
    }

    private static IReadOnlyList<GameAction> GetRestActions(RunState state)
    {
        var actions = new List<GameAction>();
        if (CanHatchByrdonisEgg(state.Player))
        {
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Hatch));
        }
        if (state.Player.Relics.Any(relic => relic.RelicId == NativeAncientRewardsV111.PumpkinCandleId))
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Kindle));
        if (state.Player.ActiveRelics.Any(relic => relic.RelicId == NativePaelRewardsV111.GrowthId))
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Clone));
        if (CanRestHeal(state.Player))
        {
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Heal));
        }
        if (PrototypeNativeRestSiteRelics.CanDig(state.Player))
        {
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Dig));
        }
        if (PrototypeNativeRestSiteRelics.CanLift(state.Player))
        {
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Lift));
        }

        var cookableCards = CanRestCook(state.Player)
            ? state.Player.Deck
                .Where(card => !PrototypeCardRules.IsEternal(card))
                .Select(card => card.InstanceId)
                .ToArray()
            : Array.Empty<long>();
        for (var first = 0; first < cookableCards.Length; first++)
        {
            for (var second = first + 1;
                 second < cookableCards.Length;
                 second++)
            {
                actions.Add(GameAction.Create(
                    PrototypeNativeRestSiteActionsV111.Cook,
                    new SelectCardsPayload(
                        [cookableCards[first], cookableCards[second]])));
            }
        }

        if (PrototypeContent.Rules.RestTrainMaxHp > 0)
        {
            actions.Add(GameAction.Empty("rest_train"));
        }

        if (CanRestUpgrade(state.Player))
        {
            actions.AddRange(
                state.Player.Deck
                    .Where(card => card.UpgradeLevel <
                        PrototypeContent.Card(card.CardId).MaxUpgradeLevel)
                    .Select(card => GameAction.Create(
                        "rest_upgrade",
                        new UpgradeCardPayload(card.InstanceId))));
        }

        return actions;
    }

    private static RunState StepRest(RunState state, GameAction action)
    {
        if (action.Kind == PrototypeNativeRestSiteActionsV111.Clone)
            return ClonePaelRestCards(state);
        if (StringComparer.Ordinal.Equals(action.Kind,
                PrototypeNativeRestSiteActionsV111.Dig))
        {
            if (ControlledChanceGuardV111.Active
                && ControlledEventChoiceReplayV111.IsActive
                && !UsesNativeRegionalSystems(RequireWorld(state)))
            {
                try
                {
                    return BeginRestDigReward(state);
                }
                catch (ControlledEventChoiceChanceRequiredException exception)
                {
                    if (!ControlledEventChoiceReplayV111
                            .IsRunActionShovelSite(exception.Request.SiteId)
                        || exception.CandidateLabels is not { Length: > 0 }
                        || exception.CandidateLabels.Length !=
                            exception.Request.SupportCount)
                        throw new NotSupportedException(
                            "Controlled Shovel dig reached an unsupported chance request.");
                    var pendingAction = new
                        PrototypeControlledRunActionContinuationV111(
                            state,
                            PrototypeNativeRestSiteActionsV111.Dig,
                            [],
                            [],
                            exception.Request,
                            exception.CandidateLabels);
                    pendingAction.Validate();
                    return state with
                    {
                        ControlledRunActionChoice = pendingAction
                    };
                }
            }

            return BeginRestDigReward(state);
        }
        if (StringComparer.Ordinal.Equals(action.Kind,
                PrototypeNativeRestSiteActionsV111.Lift))
        {
            if (!PrototypeNativeRestSiteRelics.CanLift(state.Player))
            {
                throw new InvalidOperationException(
                    "Lifting requires Girya with an unused lift.");
            }

            return CompleteRoomToMap(state with
            {
                Player = PrototypeNativeRestSiteRelics.Lift(state.Player)
            });
        }
        if (action.Kind == PrototypeNativeRestSiteActionsV111.Kindle)
        {
            var index = Array.FindIndex(state.Player.Relics,
                relic => relic.RelicId == NativeAncientRewardsV111.PumpkinCandleId);
            if (index < 0)
                throw new InvalidOperationException("Kindle requires Pumpkin Candle.");
            return CompleteRoomToMap(state with
            {
                Player = NativeAncientRewardsV111.Rekindle(state.Player, index)
            });
        }
        if (StringComparer.Ordinal.Equals(action.Kind,
                PrototypeNativeRestSiteActionsV111.Hatch))
        {
            if (!CanHatchByrdonisEgg(state.Player))
            {
                throw new InvalidOperationException(
                    "Hatch requires a Byrdonis Egg and an available pet slot.");
            }

            return CompleteRoomToMap(state with
            {
                Player = HatchByrdonisEgg(state.Player, state.Rng)
            });
        }

        if (StringComparer.Ordinal.Equals(action.Kind,
                PrototypeNativeRestSiteActionsV111.Cook))
        {
            if (!CanRestCook(state.Player))
            {
                throw new InvalidOperationException(
                    "Cooking requires Meat Cleaver and two removable cards.");
            }

            var ids = action.ReadPayload<SelectCardsPayload>()
                .CardInstanceIds;
            if (ids.Length != 2 || ids.Distinct().Count() != 2)
            {
                throw new InvalidOperationException(
                    "Cooking must remove two distinct deck cards.");
            }

            var selected = state.Player.Deck
                .Where(card => ids.Contains(card.InstanceId))
                .ToArray();
            if (selected.Length != 2
                || selected.Any(PrototypeCardRules.IsEternal))
            {
                throw new InvalidOperationException(
                    "Cooking can remove only removable deck cards.");
            }

            var player = state.Player with
            {
                Deck = state.Player.Deck
                    .Where(card => !ids.Contains(card.InstanceId))
                    .ToArray(),
                MaxHp = state.Player.MaxHp + 5,
                Hp = state.Player.Hp + 5
            };
            return CompleteRoomToMap(state with { Player = player });
        }

        if (StringComparer.Ordinal.Equals(action.Kind,
                PrototypeNativeRestSiteActionsV111.Heal))
        {
            if (!CanRestHeal(state.Player))
            {
                throw new InvalidOperationException(
                    "A relic prevents resting to heal.");
            }

            var amount = Math.Max(
                1,
                (state.Player.MaxHp * PrototypeContent.Rules.RestHealPercent) / 100);
            var player = state.Player with
            {
                Hp = Math.Min(state.Player.MaxHp, state.Player.Hp + amount)
            };
            player = ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RestSiteHealed,
                rng: state.Rng);
            state = state with { Player = player };
            return EnterRestHealRelicReward(state);
        }

        if (StringComparer.Ordinal.Equals(action.Kind, "rest_train"))
        {
            var gain = PrototypeContent.Rules.RestTrainMaxHp;
            if (gain <= 0)
            {
                throw new InvalidOperationException("Rest training is disabled by the active ruleset.");
            }

            state = state with
            {
                Player = state.Player with
                {
                    MaxHp = state.Player.MaxHp + gain,
                    Hp = state.Player.Hp + gain
                }
            };
            return CompleteRoomToMap(state);
        }

        RequireKind(action, PrototypeNativeRestSiteActionsV111.Smith);
        if (!CanRestUpgrade(state.Player))
        {
            throw new InvalidOperationException(
                "A relic prevents upgrading cards at rest sites.");
        }

        var payload = action.ReadPayload<UpgradeCardPayload>();
        var found = false;
        var deck = state.Player.Deck
            .Select(card =>
            {
                if (card.InstanceId != payload.CardInstanceId)
                {
                    return card;
                }

                if (card.UpgradeLevel >=
                    PrototypeContent.Card(card.CardId).MaxUpgradeLevel)
                {
                    throw new InvalidOperationException("Card has reached its maximum upgrade level.");
                }

                found = true;
                return card with { UpgradeLevel = 1 };
            })
            .ToArray();

        if (!found)
        {
            throw new InvalidOperationException($"Card instance {payload.CardInstanceId} is missing.");
        }

        state = state with { Player = state.Player with { Deck = deck } };
        return CompleteRoomToMap(state);
    }

    private static RunState ResumeControlledRunAction(
        RunState state,
        GameAction action,
        PrototypeControlledRunActionContinuationV111 continuation)
    {
        if (action.Kind != "choose_controlled_run_action_outcome")
            throw new InvalidOperationException(
                "A suspended run action requires its explicit outcome adapter.");
        continuation.Validate();
        var selectedIndex = action
            .ReadPayload<ChooseControlledEventOutcomePayload>().SelectedIndex;
        if (selectedIndex < 0
            || selectedIndex >= continuation.PendingRequest.SupportCount)
            throw new InvalidOperationException(
                "Selected run-action outcome is outside the pending ordered support.");

        var decision = new RandomIndexDecisionV111(
            continuation.PendingRequest, selectedIndex, "controlled-v111");
        decision.Validate();
        var decisions = continuation.Decisions.Append(decision).ToArray();
        var decisionCandidateLabels =
            continuation.DecisionCandidateLabels.Append(
                (string[])continuation.PendingCandidateLabels.Clone()).ToArray();
        var partialState = continuation.PartialState.Fork() with
        {
            DecisionIndex = state.DecisionIndex
        };
        var selectedMapAction = continuation.ActionId == "choose_map_node"
            ? GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload(
                    continuation.MapNodeId ?? throw new InvalidDataException(
                        "Controlled map-node frame is missing its node ID.")))
            : null;
        var legacyActOneCombatEntry = selectedMapAction is not null
            && ControlledRunActionEngineV111
                .IsLegacyActOneCombatEntryAction(
                    partialState, selectedMapAction);
        var replay = ControlledChanceContinuationExecutorV111.Resume(
            partialState,
            decisions,
            checkpoint => continuation.ActionId switch
            {
                "start_run" => StartRun(
                    checkpoint,
                    GameAction.Empty("start_run")),
                PrototypeNativeRestSiteActionsV111.Dig =>
                    BeginRestDigReward(checkpoint),
                "choose_map_node" => new PrototypeGameEngine().Step(
                    checkpoint with
                    {
                        DecisionIndex = checked(state.DecisionIndex - 1)
                    },
                    selectedMapAction ?? throw new InvalidDataException(
                        "Controlled map-node frame is missing its action."))
                    .State,
                "buy_card" or "buy_potion" or "replace_shop_potion"
                    or "remove_card" or "buy_relic" =>
                    new PrototypeGameEngine().Step(
                        checkpoint with
                        {
                            DecisionIndex = checked(
                                state.DecisionIndex - 1)
                        },
                        continuation.RunAction ?? throw new InvalidDataException(
                            "Controlled Shop-purchase frame is missing its action."))
                    .State,
                _ => throw new NotSupportedException(
                    "Controlled run action has no resume implementation.")
            },
            requestFilter: legacyActOneCombatEntry
                ? ControlledRunActionEngineV111
                    .IsLegacyActOneCombatEntryRequest
                : null);
        if (replay.PendingChance is { } pendingChance)
        {
            var supportedSite = continuation.ActionId switch
            {
                "start_run" => ControlledEventChoiceReplayV111
                    .IsRunActionSite(pendingChance.Request.SiteId),
                "choose_map_node" => ControlledEventChoiceReplayV111
                    .IsRunActionEventEntrySite(pendingChance.Request.SiteId)
                    || ControlledEventChoiceReplayV111
                        .IsRunActionShopSite(pendingChance.Request.SiteId)
                    || ControlledEventChoiceReplayV111
                        .IsRunActionTreasureSite(pendingChance.Request.SiteId)
                    || ControlledRunActionEngineV111
                        .IsLegacyActOneCombatEntrySite(
                            partialState,
                            selectedMapAction ?? throw new InvalidDataException(
                                "Controlled map-node frame is missing its action."),
                            pendingChance.Request.SiteId),
                PrototypeNativeRestSiteActionsV111.Dig =>
                    ControlledEventChoiceReplayV111
                        .IsRunActionShovelSite(pendingChance.Request.SiteId),
                "buy_card" or "buy_potion" or "replace_shop_potion"
                    or "remove_card" or "buy_relic" =>
                    StochasticSiteRegistryV111
                        .IsRunActionShopPurchaseSite(
                            pendingChance.Request.SiteId),
                _ => false
            };
            if (!supportedSite
                || pendingChance.CandidateLabels is null
                || (pendingChance.CandidateLabels.Length !=
                        pendingChance.Request.SupportCount
                    && !(pendingChance.CandidateLabels.Length == 0
                        && pendingChance.Request.SupportCount >
                            ControlledRunActionEngineV111
                                .MaximumInlineOutcomeActions)))
                throw new NotSupportedException(
                    "Controlled run action reached an unsupported chance request.");

            var nextContinuation =
                new PrototypeControlledRunActionContinuationV111(
                    state with { ControlledRunActionChoice = null },
                    continuation.ActionId,
                    decisions,
                    decisionCandidateLabels,
                    pendingChance.Request,
                    pendingChance.CandidateLabels,
                    continuation.MapNodeId,
                    continuation.RunAction);
            nextContinuation.Validate();
            return state with
            {
                ControlledRunActionChoice = nextContinuation
            };
        }

        var result = replay.Run ?? throw new InvalidDataException(
            "Controlled run-action replay returned neither a run nor a pending chance request.");

        if (result.ControlledRunActionChoice is not null)
            throw new NotSupportedException(
                "Controlled run action replay stopped without resolving its continuation.");
        var trace = new ControlledRunActionTraceV111(
            continuation.ActionId,
            decisions,
            decisionCandidateLabels,
            MapNodeId: continuation.MapNodeId,
            ActionFingerprint: continuation.RunAction is null
                ? null
                : PrototypeAiEnvironment.StableActionId(
                    continuation.RunAction));
        trace.Validate();
        return result with
        {
            ControlledRunActionHistory =
                (result.ControlledRunActionHistory
                    ?? Array.Empty<ControlledRunActionTraceV111>())
                .Append(trace).ToArray(),
            ControlledRunActionChoice = null
        };
    }

    private static RunState BeginRestDigReward(RunState state)
    {
        if (!PrototypeNativeRestSiteRelics.CanDig(state.Player))
        {
            throw new InvalidOperationException("Digging requires Shovel.");
        }

        var world = RequireWorld(state);
        var nativeRunRng = state.NativeRunRng?.Fork();
        string? relicId;
        if (UsesNativeRegionalSystems(world))
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                world, state.Player, state.Rng, merchant: false,
                nativeRunRng: nativeRunRng);
            world = draw.World;
            relicId = draw.Id;
        }
        else
        {
            relicId = PickUnownedEventRelic(
                state.Player, state.Rng, "rest:shovel");
        }

        if (relicId is null)
        {
            throw new InvalidOperationException(
                "No source-eligible relic remains for Shovel to dig up.");
        }

        return state with
        {
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
            World = world with
            {
                Reward = new RewardState(
                    SourceRoom: "Rest",
                    CardOptions: [],
                    PotionOption: null,
                    RelicOption: relicId,
                    CardResolved: true,
                    PotionResolved: true,
                    RelicResolved: false,
                    EndsAct: false)
            },
            Phase = RunPhase.Reward
        };
    }

    private static IReadOnlyList<GameAction> GetRewardActions(RunState state)
    {
        var actions = GetRewardActionsCore(state);
        if (!state.Player.ActiveRelics.Any(relic => relic.RelicId == NativePaelRewardsV111.WingId))
            return actions;
        return actions.Concat(actions.Where(action => action.Kind is "skip_reward_card" or "skip_reward_card_group")
            .Select(action => action with { Kind = action.Kind == "skip_reward_card"
                ? "sacrifice_reward_card" : "sacrifice_reward_card_group" })).ToArray();
    }

    private static IReadOnlyList<GameAction> GetRewardActionsCore(RunState state)
    {
        var reward = RequireWorld(state).Reward
            ?? throw new InvalidOperationException("Reward phase has no reward state.");

        // Event/legacy rewards retain their previous ordered interaction.
        // Native encounter rewards expose all unresolved reward types
        // independently; a nested deck selection or bundle choice is
        // mandatory and cannot be bypassed by other reward actions.
        if (!reward.IndependentSelection
            || reward.PendingDeckChoice is not null
            || (!reward.CardResolved && reward.CardBundles is not null))
        {
            return GetOrderedRewardActions(state, reward);
        }

        var actions = new List<GameAction>();
        var firstExtraCard = reward.FirstPendingExtraCardGroupIndex;
        if (!reward.CardResolved || firstExtraCard >= 0)
        {
            // The original action ID remains valid for the primary
            // offer or the earliest remaining extra offer.
            actions.AddRange(
                reward.CurrentCardOptions.Select((_, index) =>
                    GameAction.Create(
                        "take_reward_card",
                        new ChooseCardPayload(index))));
            actions.Add(GameAction.Empty("skip_reward_card"));
            var currentGroup = reward.CardResolved
                ? firstExtraCard + 1 : 0;
            if (CreateDriftwoodRerollAction(
                    state.Player, reward, currentGroup) is { } reroll)
                actions.Add(reroll);
        }

        // Every other extra offer gets an explicit stable group ID.
        // They remain selectable even if the primary offer is pending.
        for (var i = 0; i < (reward.ExtraCardOptions?.Length ?? 0); i++)
        {
            if (reward.IsExtraCardGroupResolved(i)
                || (reward.CardResolved && i == firstExtraCard))
            {
                continue;
            }

            for (var index = 0; index < reward.ExtraCardOptions![i].Length; index++)
            {
                actions.Add(GameAction.Create(
                    "take_reward_card_group",
                    new ChooseRewardCardGroupPayload(i + 1, index)));
            }
            actions.Add(GameAction.Create(
                "skip_reward_card_group",
                new ChooseRewardCardGroupPayload(i + 1, -1)));
            if (CreateDriftwoodRerollAction(
                    state.Player, reward, i + 1) is { } reroll)
                actions.Add(reroll);
        }

        if (!reward.PotionResolved)
        {
            if (CanAcquirePotion(state.Player))
            {
                var freeSlot = Array.IndexOf(
                    state.Player.PotionSlots, null);
                if (freeSlot >= 0)
                {
                    actions.Add(GameAction.Empty("take_reward_potion"));
                }
                else
                {
                    actions.AddRange(
                        state.Player.PotionSlots
                            .Select((item, index) =>
                                (Potion: item, Slot: index))
                            .Where(item => item.Potion is not null)
                            .Select(item => GameAction.Create(
                                "replace_reward_potion",
                                new ReplaceRewardPotionPayload(
                                    item.Slot))));
                }
            }

            actions.Add(GameAction.Empty("skip_reward_potion"));
        }

        for (var i = 0; i < (reward.ExtraPotionOptions?.Length ?? 0); i++)
        {
            if (reward.IsExtraPotionGroupResolved(i))
                continue;
            if (CanAcquirePotion(state.Player))
            {
                var freeSlot = Array.IndexOf(state.Player.PotionSlots, null);
                if (freeSlot >= 0)
                {
                    actions.Add(GameAction.Create(
                        "take_reward_extra_potion",
                        new ChooseExtraPotionRewardPayload(i)));
                }
                else
                {
                    actions.AddRange(state.Player.PotionSlots
                        .Select((potion, slot) => (Potion: potion, Slot: slot))
                        .Where(item => item.Potion is not null)
                        .Select(item => GameAction.Create(
                            "replace_reward_extra_potion",
                            new ChooseExtraPotionRewardPayload(i, item.Slot))));
                }
            }
            actions.Add(GameAction.Create(
                "skip_reward_extra_potion",
                new ChooseExtraPotionRewardPayload(i)));
        }

        if (!reward.RelicResolved)
        {
            var relics = reward.CurrentRelicOptions;
            if (relics.Length == 0)
            {
                throw new InvalidOperationException(
                    "Unresolved relic reward has no options.");
            }
            actions.AddRange(relics.Select((_, index) =>
                GameAction.Create(
                    "take_reward_relic",
                    new ChooseRelicPayload(index))));
            if (reward.IndependentSelection)
                actions.Add(GameAction.Empty("skip_reward_relic"));
        }
        var firstExtraRelic = reward.FirstPendingExtraRelicGroupIndex;
        for (var i = 0; i < (reward.ExtraRelicRewardIds?.Length ?? 0); i++)
        {
            if (reward.IsExtraRelicGroupResolved(i))
            {
                continue;
            }

            if (reward.RelicResolved && i == firstExtraRelic)
            {
                actions.Add(GameAction.Create(
                    "take_reward_relic", new ChooseRelicPayload(0)));
            }
            else
            {
                actions.Add(GameAction.Create(
                    "take_reward_extra_relic", new ChooseRelicPayload(i)));
            }
            if (reward.IsExtraRelicGroupSkippable(i))
            {
                actions.Add(GameAction.Create(
                    "skip_reward_extra_relic", new ChooseRelicPayload(i)));
            }
        }

        if (reward.GoldOption is not null && !reward.GoldResolved)
        {
            actions.Add(GameAction.Empty("take_reward_gold"));
        }
        for (var index = 0; index < (reward.ExtraGoldOptions?.Length ?? 0); index++)
        {
            if (!reward.ExtraGoldGroupsResolved![index])
                actions.Add(GameAction.Create("take_reward_extra_gold", new ChooseGoldRewardPayload(index)));
        }

        actions.Add(GameAction.Empty("leave_reward"));
        return actions;
    }

    private static bool IsCardRewardAction(GameAction action) =>
        action.Kind is "take_reward_card" or "skip_reward_card";

    private static bool IsPotionRewardAction(GameAction action) =>
        action.Kind is "take_reward_potion"
            or "replace_reward_potion"
            or "skip_reward_potion";

    private static IReadOnlyList<GameAction> GetOrderedRewardActions(
        RunState state, RewardState reward)
    {
        if (reward.PendingDeckChoice is { } deckChoice)
        {
            var actions = deckChoice.CandidateCardInstanceIds
                .Select(cardInstanceId =>
                    GameAction.Create(
                        "choose_relic_deck_card",
                        new ChooseDeckCardPayload(
                            cardInstanceId)))
                .ToList();
            if (deckChoice.MinimumSelectionsRemaining == 0)
                actions.Add(GameAction.Empty("finish_relic_deck_choice"));
            return actions;
        }

        if (!reward.CardResolved && reward.CardBundles is { } bundles)
        {
            // Bundle selection is mandatory, like the native
            // FromChooseABundleScreen interaction.
            return bundles.Select((_, index) => GameAction.Create(
                "take_reward_bundle",
                new ChooseBundlePayload(index))).ToArray();
        }

        if (!reward.CardResolved)
        {
            var actions = reward.CardOptions
                .Select((_, index) => GameAction.Create(
                    "take_reward_card",
                    new ChooseCardPayload(index)))
                .ToList();
            if (!PrototypeNativeBrainLeech.IsForcedClassCardReward(
                    RequireWorld(state).Event, reward))
                actions.Add(GameAction.Empty("skip_reward_card"));
            if (CreateDriftwoodRerollAction(
                    state.Player, reward, 0) is { } reroll)
                actions.Add(reroll);
            return actions;
        }

        var extraCardOptions =
            reward.ExtraCardOptions
            ?? Array.Empty<string[]>();
        if (reward.ExtraCardRewardsResolved
            < extraCardOptions.Length)
        {
            var currentOptions =
                extraCardOptions[
                    reward.ExtraCardRewardsResolved];
            var actions = currentOptions
                .Select((_, index) => GameAction.Create(
                    "take_reward_card",
                    new ChooseCardPayload(index)))
                .ToList();
            actions.Add(
                GameAction.Empty("skip_reward_card"));
            if (CreateDriftwoodRerollAction(
                    state.Player, reward,
                    1 + reward.ExtraCardRewardsResolved) is { } reroll)
                actions.Add(reroll);
            return actions;
        }

        if (!reward.PotionResolved)
        {
            var actions = new List<GameAction>();
            if (CanAcquirePotion(state.Player))
            {
                var emptySlot = Array.IndexOf(
                    state.Player.PotionSlots,
                    null);
                if (emptySlot >= 0)
                {
                    actions.Add(
                        GameAction.Empty(
                            "take_reward_potion"));
                }
                else
                {
                    actions.AddRange(
                        state.Player.PotionSlots
                            .Select((item, slot) =>
                                (Potion: item, Slot: slot))
                            .Where(item =>
                                item.Potion is not null)
                            .Select(item =>
                                GameAction.Create(
                                    "replace_reward_potion",
                                    new ReplaceRewardPotionPayload(
                                        item.Slot))));
                }
            }

            actions.Add(GameAction.Empty("skip_reward_potion"));
            return actions;
        }

        if (!reward.RelicResolved)
        {
            var options = reward.CurrentRelicOptions;
            if (options.Length == 0)
            {
                throw new InvalidOperationException(
                    "Unresolved relic reward has no options.");
            }

            return options
                .Select((_, index) => GameAction.Create(
                    "take_reward_relic",
                    new ChooseRelicPayload(index)))
                .ToArray();
        }

        if (reward.ExtraRelicsResolved
            < (reward.ExtraRelicRewardIds?.Length ?? 0))
        {
            return [GameAction.Create(
                "take_reward_relic",
                new ChooseRelicPayload(0))];
        }

        return [GameAction.Empty("leave_reward")];
    }

    private static RunState StepReward(RunState state, GameAction action)
    {
        if (state.ControlledEventRewardChoice is { } controlledRewardChoice)
            return ResumeControlledEventRewardChoice(
                state, action, controlledRewardChoice);
        if (action.Kind is "sacrifice_reward_card" or "sacrifice_reward_card_group")
            return SacrificePaelCardReward(state, action);
        if (action.Kind == "reroll_reward_card")
            return RerollRewardCardGroup(state,
                action.ReadPayload<RerollRewardCardPayload>().GroupIndex);
        var world = RequireWorld(state);
        var reward = world.Reward
            ?? throw new InvalidOperationException("Reward phase has no reward state.");
        if (PrototypeNativeBrainLeech.IsForcedClassCardReward(
                world.Event, reward)
            && !reward.CardResolved
            && action.Kind is "skip_reward_card" or "leave_reward")
        {
            throw new InvalidOperationException(
                "Brain Leech's Share Knowledge reward requires choosing a card.");
        }
        var player = state.Player;
        var nextId = world.NextCardInstanceId;
        var nativeRunRng = state.NativeRunRng?.Fork();

        if (action.Kind == "take_reward_extra_gold" && reward.IndependentSelection
            && reward.PendingDeckChoice is null && (reward.CardResolved || reward.CardBundles is null))
        {
            var index = action.ReadPayload<ChooseGoldRewardPayload>().GroupIndex;
            if (index < 0 || index >= (reward.ExtraGoldOptions?.Length ?? 0)
                || reward.ExtraGoldGroupsResolved![index])
                throw new InvalidOperationException("Extra gold reward group is unavailable.");
            player = player with
            {
                Gold = GoldAfterGain(player, reward.ExtraGoldOptions![index])
            };
            var resolved = (bool[])reward.ExtraGoldGroupsResolved!.Clone();
            resolved[index] = true;
            reward = reward with { ExtraGoldGroupsResolved = resolved };
        }
        else if (reward.IndependentSelection
            && reward.PendingDeckChoice is null
            && (reward.CardResolved || reward.CardBundles is null)
            && action.Kind is "take_reward_extra_potion"
                or "replace_reward_extra_potion"
                or "skip_reward_extra_potion")
        {
            var payload = action.ReadPayload<ChooseExtraPotionRewardPayload>();
            var options = reward.ExtraPotionOptions
                ?? throw new InvalidOperationException(
                    "Extra potion reward groups are missing.");
            if ((uint)payload.GroupIndex >= (uint)options.Length
                || reward.IsExtraPotionGroupResolved(payload.GroupIndex))
                throw new InvalidOperationException(
                    "Extra potion reward group is unavailable.");

            if (action.Kind == "take_reward_extra_potion")
            {
                if (!CanAcquirePotion(player))
                    throw new InvalidOperationException(
                        "Player cannot acquire potions.");
                var slot = Array.IndexOf(player.PotionSlots, null);
                if (slot < 0)
                    throw new InvalidOperationException(
                        "No potion slot is available for the extra reward.");
                var slots = (PotionInstance?[])player.PotionSlots.Clone();
                slots[slot] = new PotionInstance(options[payload.GroupIndex],
                    PrototypeJson.EmptyObject());
                player = player with { PotionSlots = slots };
            }
            else if (action.Kind == "replace_reward_extra_potion")
            {
                if (!CanAcquirePotion(player)
                    || Array.IndexOf(player.PotionSlots, null) >= 0
                    || payload.Slot < 0
                    || payload.Slot >= player.PotionSlots.Length
                    || player.PotionSlots[payload.Slot] is null)
                    throw new InvalidOperationException(
                        "Extra potion replacement slot is unavailable.");
                player = player with
                {
                    PotionSlots = ReplacePotionSlot(player.PotionSlots,
                        payload.Slot, options[payload.GroupIndex])
                };
            }
            else if (payload.Slot != -1)
            {
                throw new InvalidOperationException(
                    "Skipped extra potion reward must not name a slot.");
            }

            reward = reward.ResolveExtraPotionGroup(payload.GroupIndex);
        }
        else if (reward.GoldOption is { } goldOffer
            && reward.PendingDeckChoice is null
            && (reward.CardResolved || reward.CardBundles is null)
            && StringComparer.Ordinal.Equals(
                action.Kind, "take_reward_gold"))
        {
            if (reward.GoldResolved)
            {
                throw new InvalidOperationException(
                    "Combat gold has already been collected.");
            }

            player = player with
            {
                Gold = GoldAfterGain(player, goldOffer)
            };
            reward = reward with { GoldResolved = true };
        }
        else if (reward.IndependentSelection
            && reward.PendingDeckChoice is null
            && (reward.CardResolved || reward.CardBundles is null)
            && StringComparer.Ordinal.Equals(
                action.Kind, "leave_reward"))
        {
            // Native proceed/skip abandons all remaining optional
            // rewards; none of the offered but unclaimed gold is gained.
            state = state with
            {
                Player = player,
                NativeRunRng = nativeRunRng ?? state.NativeRunRng,
                World = world with
                {
                    Reward = reward,
                    NextCardInstanceId = nextId
                }
            };
            return CompleteReward(state);
        }
        else if (reward.PendingDeckChoice is { } deckChoice)
        {
            if (StringComparer.Ordinal.Equals(
                    action.Kind, "finish_relic_deck_choice"))
            {
                if (deckChoice.MinimumSelectionsRemaining != 0)
                    throw new InvalidOperationException(
                        "This relic deck choice still requires a selection.");
                reward = reward with { PendingDeckChoice = null };
            }
            else
            {
                RequireKind(action, "choose_relic_deck_card");
                var payload = action.ReadPayload<ChooseDeckCardPayload>();
                var resolved = ResolveRelicDeckChoice(
                    player,
                    deckChoice,
                    payload.CardInstanceId,
                    state.Rng);
                player = resolved.Player;
                reward = reward with
                {
                    PendingDeckChoice = resolved.Remaining
                };
            }

            if (reward.PendingDeckChoice is null
                && deckChoice.CardIdAfterChoices is { } afterChoicesCardId)
            {
                player = AppendCard(
                    player, nextId++, afterChoicesCardId, state.Rng);
            }

            if (reward.PendingDeckChoice is null
                && deckChoice.HpLossAfterChoices > 0)
            {
                player = player with
                {
                    Hp = Math.Max(
                        0,
                        player.Hp - deckChoice.HpLossAfterChoices)
                };
                if (player.Hp == 0)
                {
                    world = world with
                    {
                        Reward = reward,
                        NextCardInstanceId = nextId
                    };
                    return EndRun(state with
                    {
                        Player = player,
                        NativeRunRng = nativeRunRng ?? state.NativeRunRng,
                        World = world
                    }, "defeat");
                }
            }
        }
        else if (!reward.CardResolved && reward.CardBundles is { } bundles)
        {
            RequireKind(action, "take_reward_bundle");
            var payload = action.ReadPayload<ChooseBundlePayload>();
            if (payload.Index < 0 || payload.Index >= bundles.Length)
            {
                throw new InvalidOperationException(
                    $"Reward bundle index {payload.Index} is invalid.");
            }

            foreach (var cardId in bundles[payload.Index])
            {
                player = AppendCard(player, nextId++, cardId, state.Rng);
            }
            reward = reward with { CardResolved = true };
        }
        else if (reward.IndependentSelection
                 && (reward.CardResolved || reward.CardBundles is null)
                 && (action.Kind is "take_reward_card_group"
                     or "skip_reward_card_group"))
        {
            var payload = action.ReadPayload<ChooseRewardCardGroupPayload>();
            var groupIndex = payload.GroupIndex - 1;
            if (groupIndex < 0
                || groupIndex >= (reward.ExtraCardOptions?.Length ?? 0)
                || reward.IsExtraCardGroupResolved(groupIndex))
            {
                throw new InvalidOperationException(
                    "Extra card reward group is unavailable.");
            }

            var options = reward.ExtraCardOptions![groupIndex];
            if (action.Kind == "take_reward_card_group")
            {
                if (payload.Index < 0 || payload.Index >= options.Length)
                {
                    throw new InvalidOperationException(
                        "Extra card reward option index is invalid.");
                }
                player = AppendNativeCardReward(
                    player, nextId++, options[payload.Index],
                    reward.CardUpgradeFlagsForGroup(groupIndex + 1)[payload.Index],
                    state.Rng,
                    reward.CardEnchantmentsForGroup(groupIndex + 1)[payload.Index]);
            }
            else if (payload.Index != -1)
            {
                throw new InvalidOperationException(
                    "Skipped card reward option index must be -1.");
            }

            reward = reward.ResolveExtraCardGroup(groupIndex);
        }
        else if (reward.IndependentSelection
                 && action.Kind == "take_reward_extra_relic")
        {
            var payload = action.ReadPayload<ChooseRelicPayload>();
            if (payload.Index < 0
                || payload.Index >= (reward.ExtraRelicRewardIds?.Length ?? 0)
                || reward.IsExtraRelicGroupResolved(payload.Index))
            {
                throw new InvalidOperationException(
                    "Extra relic reward group is unavailable.");
            }

            var relicId = reward.ExtraRelicRewardIds![payload.Index];
            if (player.Relics.Any(relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, relicId)))
            {
                throw new InvalidOperationException(
                    "Extra relic reward is already owned.");
            }

            player = player with
            {
                Relics = player.Relics.Append(
                    new RelicInstance(relicId,
                        PrototypeJson.EmptyObject(),
                        IsWax: reward.ExtraRelicWaxFlags?[payload.Index] == true)).ToArray()
            };
            player = ApplyRelicRunEvent(
                player, PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId, rng: state.Rng,
                allocateCardId: () => nextId++, nativeRunRng: nativeRunRng);
            reward = reward.ResolveExtraRelicGroup(payload.Index) with
            {
                PendingDeckChoice = CreateRelicDeckChoice(player, relicId)
            };
        }
        else if (reward.IndependentSelection
                 && action.Kind == "skip_reward_extra_relic")
        {
            var payload = action.ReadPayload<ChooseRelicPayload>();
            if (payload.Index < 0
                || payload.Index >= (reward.ExtraRelicRewardIds?.Length ?? 0)
                || reward.IsExtraRelicGroupResolved(payload.Index))
                throw new InvalidOperationException(
                    "Extra relic reward group is unavailable.");
            if (!reward.IsExtraRelicGroupSkippable(payload.Index))
                throw new InvalidOperationException(
                    "This extra relic reward cannot be skipped.");
            reward = reward.ResolveExtraRelicGroup(payload.Index);
        }
        else if (!reward.CardResolved
                 && (!reward.IndependentSelection
                     || IsCardRewardAction(action)))
        {
            if (StringComparer.Ordinal.Equals(action.Kind, "take_reward_card"))
            {
                var payload = action.ReadPayload<ChooseCardPayload>();
                if (payload.Index < 0 || payload.Index >= reward.CardOptions.Length)
                {
                    throw new InvalidOperationException($"Reward card index {payload.Index} is invalid.");
                }

                player = AppendNativeCardReward(
                    player,
                    nextId++,
                    reward.CardOptions[payload.Index],
                    reward.CurrentCardOptionUpgradeFlags[payload.Index],
                    state.Rng,
                    reward.CurrentCardOptionEnchantments[payload.Index]);
            }
            else
            {
                RequireKind(action, "skip_reward_card");
            }

            reward = reward with { CardResolved = true };
        }
        else if (reward.FirstPendingExtraCardGroupIndex >= 0
                 && (!reward.IndependentSelection
                     || IsCardRewardAction(action)))
        {
            var extraCardOptions =
                reward.ExtraCardOptions
                ?? throw new InvalidOperationException(
                    "Extra card reward state is missing.");
            var extraCardGroupIndex = reward.FirstPendingExtraCardGroupIndex;
            var currentOptions = extraCardOptions[extraCardGroupIndex];

            if (StringComparer.Ordinal.Equals(
                    action.Kind,
                    "take_reward_card"))
            {
                var payload =
                    action.ReadPayload<ChooseCardPayload>();
                if (payload.Index < 0
                    || payload.Index >= currentOptions.Length)
                {
                    throw new InvalidOperationException(
                        $"Extra reward card index {payload.Index} is invalid.");
                }

                player = AppendNativeCardReward(
                    player,
                    nextId++,
                    currentOptions[payload.Index],
                    reward.CurrentCardOptionUpgradeFlags[payload.Index],
                    state.Rng,
                    reward.CurrentCardOptionEnchantments[payload.Index]);
            }
            else
            {
                RequireKind(action, "skip_reward_card");
            }

            reward = reward.ResolveExtraCardGroup(extraCardGroupIndex);
        }
        else if (!reward.PotionResolved
                 && (!reward.IndependentSelection
                     || IsPotionRewardAction(action)))
        {
            if (StringComparer.Ordinal.Equals(
                    action.Kind,
                    "take_reward_potion"))
            {
                if (reward.PotionOption is null)
                {
                    throw new InvalidOperationException(
                        "Reward has no potion.");
                }

                if (!CanAcquirePotion(player))
                {
                    throw new InvalidOperationException(
                        "Player cannot acquire potions.");
                }

                var slot = Array.IndexOf(
                    player.PotionSlots,
                    null);
                if (slot < 0)
                {
                    throw new InvalidOperationException(
                        "No potion slot is available.");
                }

                var slots =
                    (PotionInstance?[])
                    player.PotionSlots.Clone();
                slots[slot] =
                    new PotionInstance(
                        reward.PotionOption,
                        PrototypeJson.EmptyObject());
                player = player with
                {
                    PotionSlots = slots
                };
            }
            else if (StringComparer.Ordinal.Equals(
                         action.Kind,
                         "replace_reward_potion"))
            {
                if (reward.PotionOption is null)
                {
                    throw new InvalidOperationException(
                        "Reward has no potion.");
                }

                if (!CanAcquirePotion(player))
                {
                    throw new InvalidOperationException(
                        "Player cannot acquire potions.");
                }

                if (Array.IndexOf(
                        player.PotionSlots,
                        null) >= 0)
                {
                    throw new InvalidOperationException(
                        "Potion replacement is only needed when all slots are occupied.");
                }

                var payload =
                    action.ReadPayload<
                        ReplaceRewardPotionPayload>();
                player = player with
                {
                    PotionSlots = ReplacePotionSlot(
                        player.PotionSlots,
                        payload.Slot,
                        reward.PotionOption)
                };
            }
            else
            {
                RequireKind(
                    action,
                    "skip_reward_potion");
            }

            reward = reward with
            {
                PotionResolved = true
            };
        }
        else if (!reward.RelicResolved
                 && (!reward.IndependentSelection
                     || action.Kind == "take_reward_relic"))
        {
            RequireKind(action, "take_reward_relic");
            var options = reward.CurrentRelicOptions;
            if (options.Length == 0)
            {
                throw new InvalidOperationException(
                    "Reward has no relic options.");
            }

            var payload =
                action.ReadPayload<ChooseRelicPayload>();
            if (payload.Index < 0
                || payload.Index >= options.Length)
            {
                throw new InvalidOperationException(
                    $"Reward relic index {payload.Index} is invalid.");
            }

            var relicId = options[payload.Index];
            player = player with
            {
                Relics = player.Relics.Append(
                    new RelicInstance(
                        relicId,
                        PrototypeJson.EmptyObject()))
                    .ToArray()
            };
            player = ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId,
                rng: state.Rng, allocateCardId: () => nextId++,
                nativeRunRng: nativeRunRng);
            var acquisitionDeckChoice =
                CreateRelicDeckChoice(
                    player,
                    relicId);
            reward = reward with
            {
                RelicResolved = true,
                PendingDeckChoice =
                    acquisitionDeckChoice
            };
        }
        else if (reward.IndependentSelection
                 && !reward.RelicResolved
                 && action.Kind == "skip_reward_relic")
        {
            reward = reward with { RelicResolved = true };
        }
        else if (reward.FirstPendingExtraRelicGroupIndex >= 0
                 && (!reward.IndependentSelection
                     || action.Kind == "take_reward_relic"))
        {
            RequireKind(action, "take_reward_relic");
            var payload = action.ReadPayload<ChooseRelicPayload>();
            if (payload.Index != 0)
            {
                throw new InvalidOperationException(
                    "A single extra relic reward requires choice index zero.");
            }

            var extraRelicGroupIndex = reward.FirstPendingExtraRelicGroupIndex;
            var relicId = reward.ExtraRelicRewardIds![extraRelicGroupIndex];
            if (player.Relics.Any(relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, relicId)))
            {
                throw new InvalidOperationException(
                    "Extra relic reward is already owned.");
            }

            player = player with
            {
                Relics = player.Relics.Append(
                    new RelicInstance(relicId,
                        PrototypeJson.EmptyObject(),
                        IsWax: reward.ExtraRelicWaxFlags?[extraRelicGroupIndex] == true)).ToArray()
            };
            player = ApplyRelicRunEvent(
                player, PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId, rng: state.Rng,
                allocateCardId: () => nextId++, nativeRunRng: nativeRunRng);
            reward = reward.ResolveExtraRelicGroup(extraRelicGroupIndex) with
            {
                PendingDeckChoice = CreateRelicDeckChoice(player, relicId)
            };
        }
        else
        {
            RequireKind(action, "leave_reward");
            state = state with
            {
                Player = player,
                NativeRunRng = nativeRunRng ?? state.NativeRunRng,
                World = world with
                {
                    Reward = reward,
                    NextCardInstanceId = nextId
                }
            };
            return CompleteReward(state);
        }

        world = world with
        {
            Reward = reward,
            NextCardInstanceId = nextId
        };

        return state with
        {
            Player = player,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
            World = world
        };
    }

    private static RunState ResumeControlledEventRewardChoice(
        RunState state,
        GameAction action,
        PrototypeControlledEventRewardChoiceContinuationV111 continuation)
    {
        if (action.Kind != "choose_controlled_event_outcome")
            throw new InvalidOperationException(
                "A suspended controlled event reward requires a Nature outcome.");
        continuation.Validate();
        var payload = action.ReadPayload<ChooseControlledEventOutcomePayload>();
        var request = continuation.PendingRequest;
        if (payload.SelectedIndex < 0
            || payload.SelectedIndex >= request.SupportCount)
            throw new InvalidOperationException(
                "Selected event reward outcome is outside the pending ordered support.");

        var selectedDecision = new RandomIndexDecisionV111(
            request, payload.SelectedIndex, "controlled-v111");
        selectedDecision.Validate();
        var decisions = continuation.Decisions.Append(selectedDecision).ToArray();
        var decisionCandidateLabels = continuation.DecisionCandidateLabels
            .Append((string[])continuation.PendingCandidateLabels.Clone())
            .ToArray();
        var resumeFrame = continuation with
        {
            Decisions = decisions,
            DecisionCandidateLabels = decisionCandidateLabels
        };
        var partialState = continuation.PartialState.Fork() with
        {
            DecisionIndex = state.DecisionIndex
        };

        var replay = ControlledChanceContinuationExecutorV111.Resume(
            partialState,
            decisions.Skip(continuation.DecisionPrefixLength).ToArray(),
            checkpoint => CompleteReward(checkpoint, resumeFrame),
            continuation.PrefixOrdinalCounts());
        if (replay.PendingChance is { } pendingChance)
            throw pendingChance;
        var result = replay.Run ?? throw new InvalidDataException(
            "Controlled event-reward replay returned neither a run nor a pending chance request.");
        if (result.ControlledEventRewardChoice is not null)
            return result;

        var history = result.ControlledEventChoiceHistory
            ?? throw new InvalidDataException(
                "Completed controlled event reward lost its event-choice history.");
        if (history.Length == 0
            || history[^1].EventId != continuation.EventId
            || history[^1].ChoiceId != continuation.ChoiceId)
            throw new InvalidDataException(
                "Completed controlled event reward no longer matches its event-choice trace.");
        var completedTrace = history[^1] with
        {
            Decisions = decisions,
            CandidateLabels = decisionCandidateLabels
        };
        completedTrace.Validate();
        var nextHistory = (ControlledEventChoiceTraceV111[])history.Clone();
        nextHistory[^1] = completedTrace;
        return result with { ControlledEventChoiceHistory = nextHistory };
    }

    private static RunState EnterReward(RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat reward has no combat state.");
        var room = world.ActiveRoom
            ?? throw new InvalidOperationException("Combat reward has no active room.");

        // SwipePower.BeforeDeath returns the exact stolen DeckVersion and
        // creates a distinct special card reward. This restoration is
        // automatic and must NOT acquire another copy when the reward is
        // inspected/closed. Escaped thieves retain the original card.
        var returnedCards = combat.Enemies
            .Where(enemy => enemy.Hp <= 0 && !enemy.Escaped)
            .SelectMany(enemy => enemy.StolenCards
                ?? Array.Empty<CardInstance>())
            .ToArray();
        if (returnedCards.Length > 0)
        {
            var deck = state.Player.Deck.ToList();
            foreach (var card in returnedCards)
            {
                if (!deck.Any(existing =>
                    existing.InstanceId == card.InstanceId))
                {
                    deck.Add(card);
                }
            }
            state = state with
            {
                Player = state.Player with { Deck = deck.ToArray() }
            };
        }

        var nativeOvergrowth = UsesNativeRegionalSystems(world);
        var nativeRunRng = state.NativeRunRng?.Fork();

        // RewardsSet.GenerateRewardsFor rolls the potion pity check
        // before rewards are populated; GoldReward.Populate runs first,
        // then PotionReward.Populate, then the base CardReward.
        // Forced potion rewards do not consume a pity RNG draw.
        var nativePotionOffered = false;
        if (nativeOvergrowth)
        {
            var forced = state.Player.ActiveRelics.Any(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .ForceCombatPotionReward);
            var (offered, nextOdds) = PrototypeNativePotionRewardOdds.Roll(
                world.PotionRewardOddsThousandths,
                room == PrototypeRoomType.Elite,
                state.Rng,
                forced: forced,
                nativeRunRng: nativeRunRng);
            nativePotionOffered = offered;
            world = world with
            {
                PotionRewardOddsThousandths = nextOdds
            };
        }

        // GoldReward.Populate is the first reward population RNG draw.
        // Its amount is only credited if the player claims the offer.
        var preventsGoldGain = PreventsGoldGain(state.Player);
        var nativeGold = nativeOvergrowth && !preventsGoldGain
            ? PrototypeNativeCombatGoldReward.Roll(
                room, state.Ascension, state.Rng,
                nativeRunRng: nativeRunRng)
            : null;
        var gold = 0;
        if (!preventsGoldGain)
        {
            gold = nativeOvergrowth
                ? nativeGold ?? 0
                : room switch
                {
                    PrototypeRoomType.Elite => 40 + (world.Act * 5),
                    PrototypeRoomType.Boss => 60 + (world.Act * 5),
                    _ => 20 + (world.Act * 5)
                };
        }

        // Source-backed escaped-enemy reward proportions, independently
        // of the existing prototype reward amount approximation.
        // A surviving carrier returns stolen gold only when defeated:
        // escaped carriers neither refund stolen coins nor keep full
        // normal-combat gold rewards.
        foreach (var escaped in combat.Enemies.Where(enemy => enemy.Escaped))
        {
            var definition = PrototypeContent.Enemy(escaped.EnemyId);
            var proportion = escaped.StolenGold > 0
                ? definition.EscapedRewardProportionWithGold
                : definition.EscapedRewardProportionWithoutGold;
            gold = (int)Math.Round(gold * proportion);
        }

        var recoveredGold = preventsGoldGain ? 0
            : combat.Enemies
                .Where(enemy => enemy.Hp <= 0 && !enemy.Escaped
                    && PrototypeContent.Enemy(enemy.EnemyId)
                        .RecoverCarriedGoldOnDeath)
                .Sum(enemy => enemy.StolenGold);

        // PotionReward.Populate precedes CardReward.Populate.
        string? potion = nativeOvergrowth
            && nativePotionOffered
            && CanAcquirePotion(state.Player)
                ? nativeRunRng is { } nativePotionRng
                    ? PrototypeNativePotionShop
                        .PickWeightedDistinctNativeWithAudit(
                            nativePotionRng,
                            NativePlayerRngV111.RewardsStreamId,
                            1,
                            $"act:{world.Act}:floor:{world.Floor}:room:{room}:potion_reward",
                            new NativeRandomIndexProviderV111(),
                            new NativeRandomUnitFloatProviderV111())
                        .PotionIds[0]
                    : PrototypeNativePotionShop.PickWeighted(
                        state.Rng, "reward")
                : null;

        var cardChoiceCount =
            RewardCardChoiceCount(state.Player);
        var prayerWheelGroups = room == PrototypeRoomType.Combat
            ? ExtraNormalCombatCardRewardGroups(state.Player)
            : 0;
        var extraCardRewardGroups =
            Math.Max(
                0,
                combat.ExtraCardRewardsEarned)
            + prayerWheelGroups;
        string[] cardOptions;
        string[][] extraCardOptions;
        bool[]? cardUpgradeFlags = null;
        bool[][]? extraCardUpgradeFlags = null;
        if (nativeOvergrowth)
        {
            var offset = world.CardRarityOffsetBasisPoints;
            var first = PrototypeNativeCardRarityOdds.GenerateEncounterCards(
                cardChoiceCount, state.Ascension, room, offset, state.Rng,
                act: world.Act,
                nativeRunRng: nativeRunRng);
            cardOptions = first.Cards;
            cardUpgradeFlags = first.UpgradeFlags;
            offset = first.NextOffsetBasisPoints;
            // Combat-earned extra rewards follow the base relic reward.
            // Prayer Wheel is appended later by Hook.ModifyRewards.
            extraCardOptions = [];
            extraCardUpgradeFlags = [];
            world = world with { CardRarityOffsetBasisPoints = offset };
        }
        else
        {
            cardOptions = PickRewardCards(
                world.Act,
                cardChoiceCount,
                state.Rng,
                $"act:{world.Act}:floor:{world.Floor}:room:{room}:group:0");
            extraCardOptions = Enumerable.Range(
                    0, extraCardRewardGroups)
                .Select((_, groupIndex) => PickRewardCards(
                    world.Act,
                    cardChoiceCount,
                    state.Rng,
                    $"act:{world.Act}:floor:{world.Floor}:room:{room}:extra_group:{groupIndex}"))
                .ToArray();
        }

        if (!nativeOvergrowth)
        {
            var potionDrop = CanAcquirePotion(state.Player)
                && ResolveLegacyBoundedInteger(
                    state.Rng,
                    "reward.random_potion.drop.ticket",
                    $"act:{world.Act}:floor:{world.Floor}:room:{room}",
                    2,
                    streamId: "reward") == 0;
            potion = potionDrop
                ? PrototypeContent.PotionPool[
                    ResolveLegacyRandomIndex(
                        state.Rng,
                        "reward.random_potion.model",
                        $"act:{world.Act}:floor:{world.Floor}:room:{room}:pool:"
                            + CanonicalJson.Sha256(
                                PrototypeContent.PotionPool),
                        Enumerable.Range(
                            0, PrototypeContent.PotionPool.Length),
                        streamId: "reward")]
                : null;
        }

        string? relic = null;
        if (room == PrototypeRoomType.Elite)
        {
            if (UsesNativeRegionalSystems(world))
            {
                var draw = PrototypeNativeRelicGrabBag.Draw(
                    world, state.Player, state.Rng, merchant: false,
                    nativeRunRng: nativeRunRng);
                world = draw.World;
                relic = draw.Id;
            }
            else
            {
                relic = PrototypeContent.RelicPool[
                    ResolveLegacyRandomIndex(
                        state.Rng,
                        "reward.elite_relic.model",
                        $"act:{world.Act}:floor:{world.Floor}:pool:"
                            + CanonicalJson.Sha256(
                                PrototypeContent.RelicPool),
                        Enumerable.Range(
                            0, PrototypeContent.RelicPool.Length),
                        streamId: "reward")];
            }
        }
        var relicOptions = (string[]?)null;
        if (room == PrototypeRoomType.Boss)
        {
            var bossRelicPool = PrototypeContent.BossRelicPool
                .Where(id => !state.Player.Relics.Any(relic =>
                    StringComparer.Ordinal.Equals(relic.RelicId, id)))
                .ToArray();
            if (nativeRunRng is null)
            {
                relicOptions = PickDistinct(
                    bossRelicPool, 3, state.Rng, "reward",
                    StochasticSiteRegistryV111
                        .RewardBossRelicOfferShuffleSiteId,
                    $"act:{world.Act}:floor:{world.Floor}");
            }
            else
            {
                _ = NativeFisherYatesV111.ShuffleInPlace(
                    bossRelicPool,
                    nativeRunRng,
                    new NativeRandomIndexProviderV111(),
                    NativePlayerRngV111.RewardsStreamId,
                    $"act:{world.Act}:floor:{world.Floor}:boss_relics",
                    StochasticSiteRegistryV111
                        .RewardBossRelicOfferShuffleSiteId);
                relicOptions = bossRelicPool.Take(3).ToArray();
            }
        }
        if (nativeOvergrowth && combat.ExtraCardRewardsEarned > 0)
        {
            var extras = extraCardOptions.ToList();
            var upgrades = extraCardUpgradeFlags!.ToList();
            var offset = world.CardRarityOffsetBasisPoints;
            for (var index = 0;
                 index < combat.ExtraCardRewardsEarned;
                 index++)
            {
                var next = PrototypeNativeCardRarityOdds
                    .GenerateEncounterCards(
                        cardChoiceCount, state.Ascension, room, offset,
                        state.Rng, act: world.Act,
                        nativeRunRng: nativeRunRng);
                extras.Add(next.Cards);
                upgrades.Add(next.UpgradeFlags);
                offset = next.NextOffsetBasisPoints;
            }
            extraCardOptions = extras.ToArray();
            extraCardUpgradeFlags = upgrades.ToArray();
            world = world with { CardRarityOffsetBasisPoints = offset };
        }
        var blackStarRelicRewardIds = new List<string>();
        if (nativeOvergrowth && room == PrototypeRoomType.Elite)
        {
            var blackStarRewards = state.Player.ActiveRelics.Sum(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .ExtraEliteRelicRewardCount);
            for (var index = 0; index < blackStarRewards; index++)
            {
                var draw = PrototypeNativeRelicGrabBag.Draw(
                    world, state.Player, state.Rng, merchant: false,
                    nativeRunRng: nativeRunRng);
                world = draw.World;
                if (draw.Id is { } relicId)
                    blackStarRelicRewardIds.Add(relicId);
            }
        }
        if (nativeOvergrowth && prayerWheelGroups > 0)
        {
            // Hook.ModifyRewards happens after the original reward
            // list has been populated. Prayer Wheel appends a separate
            // normal-combat CardReward, so its RNG rolls follow the
            // base and combat-earned reward population.
            var extras = extraCardOptions.ToList();
            var upgrades = extraCardUpgradeFlags!.ToList();
            var offset = world.CardRarityOffsetBasisPoints;
            for (var i = 0; i < prayerWheelGroups; i++)
            {
                var next = PrototypeNativeCardRarityOdds.GenerateEncounterCards(
                    cardChoiceCount, state.Ascension,
                    PrototypeRoomType.Combat, offset, state.Rng,
                    act: world.Act,
                    nativeRunRng: nativeRunRng);
                extras.Add(next.Cards);
                upgrades.Add(next.UpgradeFlags);
                offset = next.NextOffsetBasisPoints;
            }
            extraCardOptions = extras.ToArray();
            extraCardUpgradeFlags = upgrades.ToArray();
            world = world with { CardRarityOffsetBasisPoints = offset };
        }

        var silkenTress = ConsumeSilkenTressCardReward(
            state.Player, cardOptions.Length > 0);
        var silverCrucible = ApplySilverCrucibleToCardRewardGeneration(
            silkenTress.Player, 1 + extraCardOptions.Length);
        var hasGlitter = state.Player.ActiveRelics.Any(relic =>
            relic.RelicId == NativeGloryAncientsV111.GlitterId);
        var cardOptionEnchantments = hasGlitter
            || silkenTress.EnchantFirstCardReward
            ? ApplyGlitterToCardRewardOptions(cardOptions)
            : null;
        var extraCardOptionEnchantments = hasGlitter
            ? extraCardOptions.Select(ApplyGlitterToCardRewardOptions).ToArray()
            : null;

        var lavaRock = ApplyLavaRockBossRewards(
            silverCrucible.Player, room, world.Act,
            relicOptions, state.Rng, nativeRunRng);
        var extraRelicRewardIds = lavaRock.AdditionalRelicIds
            .Concat(blackStarRelicRewardIds).ToList();
        var extraRelicRewards = extraRelicRewardIds.ToArray();

        var reward = new RewardState(
            SourceRoom: room.ToString(),
            CardOptions: cardOptions,
            PotionOption: potion,
            RelicOption: relic,
            CardResolved: false,
            PotionResolved: potion is null,
            RelicResolved:
                relic is null
                && (relicOptions is null
                    || relicOptions.Length == 0),
            EndsAct: room == PrototypeRoomType.Boss
                && !(world.Act == 3
                    && world.LaterActEncounterBag?.SecondBossEncounterId is not null
                    && world.Floor == PrototypeNativeLaterActMap.RoomRows(3) + 1),
            ExtraCardOptions: extraCardOptions,
            RelicOptions: relicOptions,
            CardOptionsUpgraded: silverCrucible.UpgradeGroups[0],
            ExtraCardOptionsUpgraded:
                silverCrucible.UpgradeGroups.Skip(1).ToArray(),
            ExtraRelicRewardIds: extraRelicRewards,
            CardOptionUpgradeFlags: cardUpgradeFlags,
            ExtraCardOptionUpgradeFlags: extraCardUpgradeFlags,
            CardOptionEnchantments: cardOptionEnchantments,
            ExtraCardOptionEnchantments: extraCardOptionEnchantments,
            GoldOption: nativeOvergrowth ? nativeGold : recoveredGold > 0 ? recoveredGold : null,
            GoldResolved: nativeOvergrowth ? nativeGold is null : recoveredGold == 0,
            IndependentSelection: nativeOvergrowth || recoveredGold > 0,
            ExtraCardGroupsResolved: nativeOvergrowth
                ? new bool[extraCardOptions.Length]
                : null,
            ExtraRelicGroupsResolved: nativeOvergrowth
                ? new bool[extraRelicRewards.Length]
                : null,
            ExtraGoldOptions: nativeOvergrowth && recoveredGold > 0 ? [recoveredGold] : null,
            ExtraGoldGroupsResolved: nativeOvergrowth && recoveredGold > 0 ? [false] : null,
            ReturnedStolenCards: returnedCards.Length == 0
                ? null : returnedCards,
            CardRewardGenerators: Enumerable.Repeat<PrototypeCardRewardGenerator?>(
                nativeOvergrowth
                    ? new PrototypeCardRewardGenerator(
                        PrototypeCardRewardGeneratorKind.Encounter,
                        world.Act, room)
                    : new PrototypeCardRewardGenerator(
                        PrototypeCardRewardGeneratorKind.RewardPool,
                        world.Act),
                1 + extraCardOptions.Length).ToArray());

        world = world with
        {
            Combat = null,
            Reward = reward
        };

        return state with
        {
            Player = lavaRock.Player with
            {
                // Legacy prototype combat rewards retain auto-credit.
                Gold = lavaRock.Player.Gold
                    + (nativeOvergrowth ? 0 : gold)
            },
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
            World = world,
            Phase = RunPhase.Reward
        };
    }

    private static RunState CompleteReward(
        RunState state,
        PrototypeControlledEventRewardChoiceContinuationV111? resume = null)
    {
        var world = RequireWorld(state);
        var reward = world.Reward
            ?? throw new InvalidOperationException("Reward phase has no reward state.");

        if (world.CustomRewardReturn is { } returnState)
        {
            var player = state.Player;
            var nextId = world.NextCardInstanceId;
            if (reward.AddCardAfterReward is { } afterRewardCardId)
            {
                player = AppendCard(
                    player, nextId++, afterRewardCardId, state.Rng);
            }
            return state with
            {
                Player = player,
                Phase = returnState.Phase,
                World = world with
                {
                    NextCardInstanceId = nextId,
                    Reward = returnState.Reward,
                    CustomRewardReturn = returnState.Parent
                }
            };
        }

        if (reward.SourceRoom.StartsWith("Event:", StringComparison.Ordinal))
        {
            if (world.Event is null
                || !StringComparer.Ordinal.Equals(
                    reward.SourceRoom,
                    "Event:" + world.Event.EventId))
            {
                throw new InvalidOperationException(
                    "Custom event reward has no matching active event.");
            }

            if (world.Event.EventId ==
                    PrototypeNativeHiveEvents.LanternKeyEventId
                && world.Event.NativePageIndex == 2)
            {
                // The source event uses shouldResumeAfterCombat=false:
                // taking or skipping the SpecialCardReward exits to the map.
                return CompleteRoomToMap(state with
                {
                    World = world with { Reward = null }
                });
            }

            var player = state.Player;
            var nextId = world.NextCardInstanceId;
            if (reward.AddRandomCurseAfterReward)
            {
                var cursePool = NativeGloryAncientsV111
                    .ModifierGeneratedCurseIds;
                string curseId;
                try
                {
                    using (ControlledEventChoiceReplayV111.DescribeCandidates(
                        cursePool))
                    {
                        curseId = cursePool[ResolveLegacyRandomIndex(
                            state.Rng,
                            "event.reward.random_modifier_curse.index_draw",
                            "event:" + world.Event.EventId + ":pool:"
                                + CanonicalJson.Sha256(cursePool),
                            Enumerable.Range(0, cursePool.Length),
                            streamId: "reward")];
                    }
                }
                catch (ControlledEventChoiceChanceRequiredException exception)
                {
                    if (!ControlledChanceGuardV111.Active
                        || !ControlledEventChoiceReplayV111.IsActive)
                        throw;
                    var continuation = resume is null
                        ? CreateControlledEventRewardContinuation(
                            state, world.Event, exception)
                        : resume with
                        {
                            PartialState = state,
                            PendingRequest = exception.Request,
                            PendingCandidateLabels = exception.CandidateLabels
                                ?? throw new InvalidDataException(
                                    "Controlled event reward omitted its candidate labels.")
                        };
                    continuation.Validate();
                    return state with
                    {
                        ControlledEventRewardChoice = continuation
                    };
                }
                player = AppendCard(player, nextId++, curseId, state.Rng);
            }
            else if (reward.AddCardAfterReward is { } additionalCardId)
            {
                player = AppendCard(
                    player, nextId++, additionalCardId, state.Rng);
            }
            return AdvanceEventContinuations(state with
            {
                Player = player,
                World = world with
                {
                    NextCardInstanceId = nextId,
                    Reward = null
                },
                Phase = RunPhase.Event
            });
        }

        if (reward.EndsAct)
        {
            state = RecordCompletedRoom(state);
            world = RequireWorld(state);
        }

        world = world with
        {
            Reward = null,
            ActiveRoom = reward.EndsAct ? null : world.ActiveRoom
        };
        state = state with { World = world };

        if (!reward.EndsAct)
        {
            return CompleteRoomToMap(state);
        }

        if (world.Act >= PrototypeContent.Rules.Acts)
        {
            return EndRun(state, "victory");
        }

        return state with { Phase = RunPhase.ActTransition };
    }

    private static PrototypeControlledEventRewardChoiceContinuationV111
        CreateControlledEventRewardContinuation(
            RunState state,
            EventState eventState,
            ControlledEventChoiceChanceRequiredException exception)
    {
        var choiceId = eventState.ChosenChoiceId
            ?? throw new InvalidDataException(
                "Controlled event reward has no committed event option.");
        var history = state.ControlledEventChoiceHistory;
        var trace = history is { Length: > 0 } ? history[^1] : null;
        if (trace is null
            || trace.EventId != eventState.EventId
            || trace.ChoiceId != choiceId)
            throw new InvalidDataException(
                "Controlled event reward has no matching committed event-choice trace.");
        return new PrototypeControlledEventRewardChoiceContinuationV111(
            state,
            eventState.EventId,
            choiceId,
            trace.Decisions.Length,
            trace.Decisions.Select(
                PrototypeControlledEventChoiceContinuationV111.CloneDecision)
                .ToArray(),
            trace.CandidateLabels.Select(labels =>
                (string[])labels.Clone()).ToArray(),
            exception.Request,
            exception.CandidateLabels
                ?? throw new InvalidDataException(
                    "Controlled event reward omitted its candidate labels."));
    }

    private static RunState CompleteRoomToMap(RunState state)
    {
        if (RequireWorld(state).ActiveRoom == PrototypeRoomType.Rest)
        {
            state = state with
            {
                Player = PrototypeNativeFakeMerchant
                    .MarkTeaSetsAfterRestSite(state.Player)
            };
        }
        state = RecordCompletedRoom(state);
        var world = RequireWorld(state) with
        {
            ActiveRoom = null,
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null
        };

        if (world.Map.AvailableNodes().Length == 0)
        {
            throw new InvalidOperationException(
                "Completed non-boss room has no reachable next map node.");
        }

        return state with
        {
            World = world,
            Phase = RunPhase.MapChoice
        };
    }

    private static string[] PickShopCards(
        int act,
        int count,
        RngBundle rng,
        PrototypeCardType? requiredType = null)
    {
        var available = PrototypeContent.RewardCardPool
            .Where(cardId =>
                requiredType is null
                || PrototypeContent.Card(cardId).Type
                    == requiredType.Value)
            .ToList();
        var selected = new List<string>(Math.Min(count, available.Count));

        while (selected.Count < count && available.Count > 0)
        {
            var weights = PrototypeContent.ShopRarityWeights(act)
                .Where(item =>
                    item.Weight > 0
                    && available.Any(cardId =>
                        PrototypeContent.Card(cardId).Rarity == item.Rarity))
                .ToArray();
            if (weights.Length == 0)
            {
                break;
            }

            var raritySupport = weights.Select(item => new
            {
                item.Rarity,
                item.Weight
            }).ToArray();
            var roll = ResolveLegacyBoundedInteger(
                rng,
                "shop.card_rarity.ticket",
                $"act:{act}:type:{requiredType}:offer:{selected.Count}:weights:"
                    + CanonicalJson.Sha256(raritySupport),
                weights.Sum(item => item.Weight),
                streamId: "shop");
            var rarity = weights[^1].Rarity;
            foreach (var item in weights)
            {
                if (roll < item.Weight)
                {
                    rarity = item.Rarity;
                    break;
                }

                roll -= item.Weight;
            }

            var candidates = available
                .Where(cardId => PrototypeContent.Card(cardId).Rarity == rarity)
                .ToArray();
            var selectedId = candidates[
                ResolveLegacyRandomIndex(
                    rng,
                    "shop.card_model.index_draw",
                    $"act:{act}:type:{requiredType}:offer:{selected.Count}:rarity:{rarity}:pool:"
                        + CanonicalJson.Sha256(candidates),
                    Enumerable.Range(0, candidates.Length),
                    streamId: "shop")];
            selected.Add(selectedId);
            available.Remove(selectedId);
        }

        return selected.ToArray();
    }

    internal static int NativeMerchantCardBaseCost(string cardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var baseCost = PrototypeContent.Card(cardId).Rarity switch
        {
            PrototypeCardRarity.Common => 50,
            PrototypeCardRarity.Uncommon => 75,
            PrototypeCardRarity.Rare => 150,
            _ => throw new InvalidOperationException(
                "Native shop character cards must have a purchasable rarity.")
        };
        // MerchantCardEntry applies a 15% premium to cards from
        // ColorlessCardPool before the price modifier.
        return PrototypeColorlessCards.ImplementedShopPool.Contains(
                cardId, StringComparer.Ordinal)
            ? (int)Math.Round(baseCost * 1.15)
            : baseCost;
    }

    private static int NativeMerchantCardPrice(
        string cardId, RngBundle rng,
        RngBundle? nativeRunRng = null,
        string ownerId = "")
    {
        var baseCost = NativeMerchantCardBaseCost(cardId);
        if (nativeRunRng is not null)
        {
            var roll = NativePlayerRngV111.NextUnitFloat(
                nativeRunRng,
                "shop.merchant.card_price_modifier.unit_float",
                NativePlayerRngV111.ShopsStreamId,
                string.IsNullOrWhiteSpace(ownerId)
                    ? "card:" + cardId : ownerId);
            return ResolveNativeMerchantCardPrice(cardId, roll);
        }

        // Retained legacy mode approximates source NextFloat with integer
        // percentage bins on the prototype shop stream.
        var percent = ResolveLegacyRandomIndex(
            rng, "shop.merchant.card_price_modifier.index_draw",
            "card:" + cardId, Enumerable.Range(95, 11),
            streamId: "shop");
        return Math.Max(1, (int)Math.Round(
            baseCost * percent / 100.0));
    }

    internal static int ResolveNativeMerchantCardPrice(
        string cardId, float roll)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        if (!float.IsFinite(roll) || roll < 0 || roll > 1)
            throw new ArgumentOutOfRangeException(nameof(roll));
        var baseCost = NativeMerchantCardBaseCost(cardId);
        var modifier = 0.95f + (1.05f - 0.95f) * roll;
        return Math.Max(1,
            (int)MathF.Round(baseCost * modifier,
                MidpointRounding.ToEven));
    }

    private static int ShopCardPrice(string cardId, RngBundle rng)
    {
        return ResolveLegacyRandomIndex(
            rng,
            "shop.card.price.index_draw",
            "card:" + cardId,
            PrototypeLegacyShopPriceV111.CardOutcomes(cardId),
            streamId: "shop");
    }

    private static string[] PickRewardCards(
        int act,
        int count,
        RngBundle rng) =>
        PickRewardCards(
            act,
            count,
            rng,
            $"act:{act}:reward_group:external");

    private static string[] PickRewardCards(
        int act,
        int count,
        RngBundle rng,
        string ownerId)
    {
        var available = PrototypeContent.RewardCardPool.ToList();
        var selected = new List<string>(Math.Min(count, available.Count));

        while (selected.Count < count && available.Count > 0)
        {
            var rarityWeights = PrototypeContent.RewardRarityWeights(act)
                .Where(item =>
                    item.Weight > 0
                    && available.Any(cardId =>
                        PrototypeContent.Card(cardId).Rarity == item.Rarity))
                .ToArray();

            if (rarityWeights.Length == 0)
            {
                break;
            }

            var raritySupport = rarityWeights.Select(item => new
            {
                item.Rarity,
                item.Weight
            }).ToArray();
            var totalWeight = rarityWeights.Sum(item => item.Weight);
            var roll = ResolveLegacyBoundedInteger(
                rng,
                "reward.card_rarity.ticket",
                ownerId + ":card:" + selected.Count + ":weights:"
                    + CanonicalJson.Sha256(raritySupport),
                totalWeight,
                streamId: "reward");
            var rarity = rarityWeights[^1].Rarity;
            foreach (var item in rarityWeights)
            {
                if (roll < item.Weight)
                {
                    rarity = item.Rarity;
                    break;
                }

                roll -= item.Weight;
            }

            var candidates = available
                .Where(cardId => PrototypeContent.Card(cardId).Rarity == rarity)
                .ToArray();
            var selectedId = candidates[
                ResolveLegacyRandomIndex(
                    rng,
                    "reward.card_model.index_draw",
                    ownerId + ":card:" + selected.Count + ":rarity:" + rarity
                        + ":pool:" + CanonicalJson.Sha256(candidates),
                    Enumerable.Range(0, candidates.Length),
                    streamId: "reward",
                    controlledCandidateLabels:
                        (string[])candidates.Clone())];

            selected.Add(selectedId);
            available.Remove(selectedId);
        }

        return selected.ToArray();
    }

    private static PrototypeCardEnchantment?[] ApplyGlitterToCardRewardOptions(
        string[] cardIds)
    {
        return cardIds.Select(cardId =>
        {
            var candidate = new CardInstance(
                0, cardId, 0, PrototypeJson.EmptyObject());
            return PrototypeGameEngine.CanSelectEventDeckCard(
                candidate,
                PrototypePersistentDeckChoiceKind.Enchant,
                transformToCardId: null,
                enchantmentKind: PrototypeCardEnchantmentKind.Glam,
                basicCardsOnly: false)
                ? new PrototypeCardEnchantment(
                    PrototypeCardEnchantmentKind.Glam)
                : null;
        }).ToArray();
    }

    private static PrototypePendingDeckChoiceState?
        CreateRelicDeckChoice(
            PlayerState player,
            string relicId)
    {
        var relic = PrototypeContent.Relic(relicId);
        var spec = relic.AcquisitionDeckChoice;
        var afterChoicesCardId =
            relic.CardIdAfterAcquisitionDeckChoices;
        if ((spec is null || spec.Selections <= 0)
            && afterChoicesCardId is null
            && relic.AcquisitionHpLossAfterDeckChoices <= 0)
        {
            return null;
        }

        var candidates = spec is null || spec.Selections <= 0
            ? Array.Empty<long>()
            : player.Deck
            .Where(card =>
                PrototypeGameEngine.CanSelectRelicDeckCard(card, spec)
                && (!spec.RequiresUpgradable
                    || card.UpgradeLevel < PrototypeContent.Card(card.CardId).MaxUpgradeLevel))
            .OrderBy(card => spec.RequiresUpgradable ? card.CardId : string.Empty, StringComparer.Ordinal)
            .Select(card => card.InstanceId)
            .ToArray();
        var selections = spec is null
            ? 0
            : Math.Min(spec.Selections, candidates.Length);
        return selections <= 0 && afterChoicesCardId is null
            ? null
            : new PrototypePendingDeckChoiceState(
                relicId,
                spec?.Kind ?? PrototypePersistentDeckChoiceKind.Remove,
                selections,
                candidates,
                spec?.UpgradeTransformedCards ?? false)
            {
                MinimumSelectionsRemaining = Math.Min(
                    spec?.MinimumSelections ?? spec?.Selections ?? 0,
                    selections),
                CardIdAfterChoices = afterChoicesCardId,
                HpLossAfterChoices =
                    relic.AcquisitionHpLossAfterDeckChoices
            };
    }

    private static PlayerState TransformPersistentDeckCard(
        PlayerState player,
        long cardInstanceId,
        bool upgradeResult,
        RngBundle rng)
    {
        var original = player.Deck
            .Single(card =>
                card.InstanceId == cardInstanceId);
        if (PrototypeCardRules.IsEternal(original))
            throw new InvalidOperationException("Eternal deck cards cannot be transformed.");
        var pool = PrototypeContent.RewardCardPool
            .Where(cardId =>
                !StringComparer.Ordinal.Equals(
                    cardId,
                    original.CardId))
            .ToArray();
        if (pool.Length == 0)
        {
            throw new InvalidOperationException(
                "No prototype card is available for transformation.");
        }

        var transformedCardId = pool[ResolveLegacyRandomIndex(
            rng,
            "reward.card_transform.replacement.index_draw",
            "card_instance:" + cardInstanceId + ":original:" + original.CardId
                + ":pool:" + CanonicalJson.Sha256(pool),
            Enumerable.Range(0, pool.Length),
            streamId: "reward",
            controlledCandidateLabels: (string[])pool.Clone())];
        return player with
        {
            Deck = player.Deck
                .Select(card =>
                    card.InstanceId != cardInstanceId
                        ? card
                        : new CardInstance(
                            card.InstanceId,
                            transformedCardId,
                            upgradeResult ? 1 : 0,
                            PrototypeJson.EmptyObject()))
                .ToArray()
        };
    }

    private static string[] PickDistinct(
        string[] pool,
        int count,
        RngBundle rng,
        string streamId) =>
        PickDistinct(
            pool,
            count,
            rng,
            streamId,
            "run.random_pool_distinct.shuffle_swap_index",
            streamId + ":take:" + count + ":pool:"
                + CanonicalJson.Sha256(pool));

    private static string[] PickDistinct(
        string[] pool,
        int count,
        RngBundle rng,
        string streamId,
        string siteId,
        string ownerId)
    {
        if (pool.Length == 0)
        {
            return Array.Empty<string>();
        }

        var copy = (string[])pool.Clone();
        _ = LegacyFisherYatesV111.ShuffleInPlace(
            copy,
            rng,
            streamId,
            siteId,
            ownerId + ":pool:" + CanonicalJson.Sha256(pool));
        return copy.Take(Math.Min(count, copy.Length)).ToArray();
    }
}
