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
                ? ResolveUnknownMapRoom(ref world, state.Rng, state.Player,
                    eventOnly: true)
                : forceRepy ? PrototypeRoomType.Event
                : ResolveUnknownMapRoom(ref world, state.Rng, state.Player)
            : node.RoomType;

        world = world with
        {
            Floor = node.Floor,
            ActiveRoom = room,
            Map = world.Map with { CurrentNodeId = node.NodeId },
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null,
            RestSiteOptionsUsed = null
        };
        state = state with
        {
            World = world,
            Player = CompleteSpoilsMapQuestAtNode(
                ApplyRelicRoomEntryEffects(
                    ApplyPersistentCardRoomEntry(playerAfterTravel, node),
                    room,
                    state.Rng,
                    world,
                    state.NativeRunRng),
                node)
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
                width += PrototypeRng.NextInt(
                    rng,
                    "map",
                    floorRule.MaxNodes
                        - floorRule.MinNodes
                        + 1);
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
                        PrototypeRng.NextInt(
                            rng,
                            "map",
                            balanced.Length)];
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
                        PrototypeRng.NextInt(
                            rng,
                            "map",
                            candidates.Length)];
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
                    || PrototypeRng.NextInt(
                        rng,
                        "map",
                        100) >= 35)
                {
                    continue;
                }

                AddSecondTargetForSource(
                    targets,
                    sourceIndex,
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
                PrototypeRng.NextInt(
                    rng,
                    "map",
                    candidates.Length)];
        AddSecondTargetForSource(
            targets,
            sourceIndex,
            next,
            incoming,
            rng);
    }

    private static void AddRandomConvergence(
        List<string>[] targets,
        MapNodeState[] current,
        MapNodeState[] next,
        int[] incoming,
        RngBundle rng)
    {
        var nextOrder =
            Enumerable.Range(
                    0,
                    next.Length)
                .OrderBy(_ =>
                    PrototypeRng.NextInt(
                        rng,
                        "map",
                        int.MaxValue))
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
                    PrototypeRng.NextInt(
                        rng,
                        "map",
                        sources.Length)];
            targets[sourceIndex].Add(
                next[nextIndex].NodeId);
            incoming[nextIndex]++;
            return;
        }
    }

    private static void AddSecondTargetForSource(
        List<string>[] targets,
        int sourceIndex,
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
                PrototypeRng.NextInt(
                    rng,
                    "map",
                    balanced.Length)];
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
                    PrototypeRng.NextInt(
                        rng,
                        "map",
                        candidateIndices.Length)];
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
                    PrototypeRng.NextInt(
                        rng,
                        "map",
                        candidates.Length)];
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

    private static RunState StartEvent(RunState state)
    {
        var world = RequireWorld(state);
        var nativeOvergrowth = world.Act == 1
            && world.Map.GenerationProfileId
                == PrototypeNativeOvergrowthMap.GenerationProfileId;
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
                && (evt.Id != PrototypeNativeSlipperyBridge.EventId
                    || PrototypeNativeSlipperyBridge.IsEligible(
                        state.Player, world))
                && (evt.Id != PrototypeNativeRanwidTheElder.EventId
                    || PrototypeNativeRanwidTheElder.IsEligible(
                        state.Player))
                && world.Act >= evt.MinAct
                && world.Act <= evt.MaxAct
                && (evt.Id != PrototypeNativeFakeMerchant.EventId
                    || PrototypeNativeFakeMerchant.IsEligible(state.Player))
                && (evt.Id != PrototypeNativeFutureOfPotions.EventId
                    || laterRoute
                    && PrototypeNativeFutureOfPotions.IsEligible(state.Player))
                && (evt.Id != PrototypeNativeRelicTrader.EventId
                    || laterRoute
                    && PrototypeNativeRelicTrader.IsEligible(state.Player))
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
        var roll = PrototypeRng.NextInt(state.Rng, "event", totalWeight);
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
                NativeEventGold:
                    selected.Id switch
                    {
                        PrototypeNativeDenseVegetation.EventId =>
                            61 + PrototypeRng.NextInt(state.Rng, "event", 39),
                        "proto.native.event.whispering_hollow" =>
                            26 + PrototypeRng.NextInt(state.Rng, "event", 19),
                        "proto.native.event.sunken_statue" =>
                            101 + PrototypeRng.NextInt(state.Rng, "event", 21),
                        "proto.native.event.luminous_choir" =>
                            100 + PrototypeRng.NextInt(state.Rng, "event", 50),
                        // Source rolls two independent -15..15 floats:
                        // 150 + solo roll, 50 + join roll, truncated to
                        // integer gold by PlayerCmd.GainGold. The
                        // prototype currently samples integer bins.
                        "proto.native.event.jungle_maze_adventure" =>
                            135 + PrototypeRng.NextInt(state.Rng, "event", 30),
                        // Native Sunken Treasury rolls small then large
                        // independently; both rolls are persisted so the
                        // eventual choice never changes the RNG outcome.
                        "proto.native.underdocks.sunken_treasury" =>
                            52 + PrototypeRng.NextInt(state.Rng, "event", 16),
                        PrototypeNativeHiveEvents.LostWispId =>
                            45 + PrototypeRng.NextInt(state.Rng, "event", 31),
                        "proto.native.underdocks.sunken_statue" =>
                            101 + PrototypeRng.NextInt(state.Rng, "event", 21),
                        // Punch Off computes an entry-only 91..98 gold
                        // variable even though neither option pays it.
                        // Keep this draw to preserve the event RNG stream.
                        PrototypeNativePunchOff.EventId =>
                            91 + PrototypeRng.NextInt(state.Rng, "event", 8),
                        PrototypeNativeSharedEvents.ThisOrThatId =>
                            41 + PrototypeRng.NextInt(state.Rng, "event", 28),
                        PrototypeNativeCrystalSphere.EventId =>
                            51 + PrototypeRng.NextInt(state.Rng, "event", 49),
                        _ => 0
                    },
                NativeEventSecondaryGold:
                    selected.Id == "proto.native.event.jungle_maze_adventure"
                        ? 35 + PrototypeRng.NextInt(state.Rng, "event", 30)
                        : selected.Id == "proto.native.underdocks.sunken_treasury"
                            ? 303 + PrototypeRng.NextInt(
                                state.Rng, "event", 61)
                            : 0,
                NativeEventPotionSlot:
                    selected.Id == PrototypeNativeStoneOfAllTime.EventId
                        ? PrototypeNativeStoneOfAllTime.RollPotionSlot(
                            state.Player, state.Rng)
                        : null),
            EventHistory = world.EventIds.Append(selected.Id).ToArray()
        };

        if (selected.Id == PrototypeNativeSlipperyBridge.EventId)
        {
            world = world with
            {
                Event = PrototypeNativeSlipperyBridge.Initialize(
                    world.Event!, state.Player, state.Rng)
            };
        }

        if (selected.Id == PrototypeNativeWongos.EventId)
        {
            var initialized = PrototypeNativeWongos.Initialize(
                world,
                state.Player,
                world.Event!,
                state.Rng);
            world = initialized.World with { Event = initialized.Event };
        }

        if (selected.Id == PrototypeNativeRanwidTheElder.EventId)
        {
            world = world with
            {
                Event = PrototypeNativeRanwidTheElder.RollOffers(
                    world.Event!, state.Player, state.Rng)
            };
        }

        if (selected.Id == PrototypeNativeFakeMerchant.EventId)
        {
            world = world with
            {
                Event = PrototypeNativeFakeMerchant.RollInventory(
                    world.Event!, state.Rng)
            };
        }

        if (selected.Id == PrototypeNativeEndlessConveyor.EventId)
        {
            world = world with
            {
                Event = PrototypeNativeEndlessConveyor.RollNextDish(
                    world.Event!, state.Player, state.Rng)
            };
        }

        if (selected.Id == PrototypeNativeFutureOfPotions.EventId)
        {
            world = world with
            {
                Event = PrototypeNativeFutureOfPotions.RollOffers(
                    world.Event!, state.Player, state.Rng)
            };
        }

        if (selected.Id == PrototypeNativeRelicTrader.EventId)
        {
            var rolled = PrototypeNativeRelicTrader.RollOffers(
                world, world.Event!, state.Player, state.Rng);
            world = rolled.World with { Event = rolled.Event };
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

        if (eventState.EventId
            == PrototypeNativeSlipperyBridge.EventId)
        {
            if (PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                    eventState.EventId)
                && eventState.NativePageIndex
                    == PrototypeNativeEventCompletionPages
                        .TerminalChoicePageIndex(eventState.EventId))
                return [GameAction.Empty("continue_event")];
            return GetSlipperyBridgeActions(eventState);
        }

        if (eventState.EventId
            == PrototypeNativeRanwidTheElder.EventId)
        {
            if (PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                    eventState.EventId)
                && eventState.NativePageIndex
                    == PrototypeNativeEventCompletionPages
                        .TerminalChoicePageIndex(eventState.EventId))
                return [GameAction.Empty("continue_event")];
            return GetNativeRanwidActions(eventState);
        }

        if (eventState.EventId
            == PrototypeNativeRoomFullOfCheese.EventId)
        {
            return GetRoomFullOfCheeseActions(eventState);
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

        if (eventState.EventId == PrototypeNativeDollRoom.EventId)
            return GetNativeDollRoomActions(eventState);

        if (eventState.EventId == PrototypeNativeWongos.EventId)
            return GetNativeWongosActions(eventState, state.Player);

        if (eventState.EventId == PrototypeNativeCrystalSphere.EventId)
            return GetNativeCrystalSphereActions(eventState);

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

        if (eventState.EventId == PrototypeNativeRelicTrader.EventId)
        {
            return (eventState.NativeRelicTraderOffers
                    ?? Array.Empty<PrototypeRelicTraderOffer>())
                .Select((_, index) => GameAction.Create(
                    "trade_event_relic",
                    new TradeEventRelicPayload(index)))
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

    private static RunState StepEvent(
        RunState state,
        GameAction action)
    {
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Event phase has no event state.");
        var player = state.Player;
        var nextId = world.NextCardInstanceId;
        var nativeRunRng = state.NativeRunRng?.Fork();

        if (eventState.EventId == PrototypeNativeStoneOfAllTime.EventId
            && action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId == "lift")
        {
            return StepNativeStoneOfAllTimeLift(state);
        }

        if (eventState.EventId == PrototypeNativeFakeMerchant.EventId)
            return StepNativeFakeMerchant(state, action);

        if (eventState.EventId
            == PrototypeNativeSlipperyBridge.EventId)
            return StepSlipperyBridge(state, action);

        if (eventState.EventId
            == PrototypeNativeRanwidTheElder.EventId)
            return StepNativeRanwidTheElder(state, action);

        if (eventState.EventId
            == PrototypeNativeRoomFullOfCheese.EventId)
            return StepRoomFullOfCheese(state, action);

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

        if (eventState.EventId == PrototypeNativeDollRoom.EventId)
            return StepNativeDollRoom(state, action);

        if (eventState.EventId == PrototypeNativeWongos.EventId)
            return StepNativeWongos(state, action);

        if (eventState.EventId == PrototypeNativeCrystalSphere.EventId)
            return StepNativeCrystalSphere(state, action);

        if (eventState.EventId == PrototypeNativeFutureOfPotions.EventId)
            return StepNativeFutureOfPotions(state, action);

        if (eventState.EventId == PrototypeNativeArchitectEnding.EventId)
            return StepNativeArchitectEnding(state, action);

        if (eventState.EventId == PrototypeNativeRelicTrader.EventId)
            return StepNativeRelicTrader(state, action);

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
            "proto.native.underdocks.doors_of_light_and_dark"
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

        RequireKind(action, "event_choice");
        var choicePayload =
            action.ReadPayload<EventChoicePayload>();
        var definition =
            PrototypeContent.Event(
                eventState.EventId);
        var choice = definition.Choices.FirstOrDefault(
            item => StringComparer.Ordinal.Equals(
                item.Id,
                choicePayload.ChoiceId))
            ?? throw new InvalidOperationException(
                $"Unknown event choice '{choicePayload.ChoiceId}'.");

        if ((eventState.OfferedChoiceIds is not null
                && !eventState.OfferedChoiceIds.Contains(
                    choice.Id, StringComparer.Ordinal))
            || !CanTakeEventChoice(
                player,
                choice,
                world))
        {
            throw new InvalidOperationException(
                $"Event choice '{choice.Id}' cannot currently be taken.");
        }

        var queuedPotionIds = new List<string>();
        var queuedDeckChoices =
            new List<PrototypePendingEventDeckChoiceState>();
        RewardState? deferredReward = null;
        var deferredHpLoss = 0;
        var deferredGoldLoss = 0;
        string? deferredCardId = null;

        foreach (var effect in choice.Effects)
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
                    player = ApplyPlayerHpLossAndDeathPrevention(
                        player, effect.Amount);
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
                    player = ApplyGoldGain(
                        player,
                        ResolveEventGoldAmount(
                            eventState, choice.Id, effect),
                        state.Rng,
                        world,
                        nativeRunRng: nativeRunRng);
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
                            PrototypeRng.NextInt(
                                state.Rng, "event", candidates.Length)];
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
                        state.Rng,
                        allocateCardId: () => nextId++);
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
                        eventState.EventId, ref world);
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

                    var cardId = rares[
                        PrototypeRng.NextInt(
                            state.Rng, "reward", rares.Length)];
                    player = AppendCard(
                        player, nextId++, cardId, state.Rng,
                        allocateCardId: () => nextId++);
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
                            world, player, state.Rng, merchant: false);
                        world = draw.World;
                        relicId = draw.Id
                            ?? throw new InvalidOperationException(
                                "No source-eligible event relics remain in the bag.");
                    }
                    else
                    {
                        relicId = effect.Kind
                            == PrototypeRunEffectKind.GainRandomRelic
                            ? PickUnownedEventRelic(player, state.Rng)
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
                            state.Rng,
                            allocateCardId: () => nextId++);
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
                            eventState.EventId, ref world);
                    }
                    if (relicId == NativeDarvEventV111.CallingBellId)
                    {
                        if (deferredReward is not null)
                            throw new InvalidOperationException(
                                "Calling Bell acquisition already has a custom reward.");
                        deferredReward = CreateCallingBellRelicReward(
                            player, eventState.EventId, state.Rng, ref world);
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
                                    PrototypeRng.NextInt(
                                        state.Rng, "event",
                                        PrototypeContent.PotionPool.Length)]
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
                    DeferredCardId = deferredCardId
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
        RngBundle rng)
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
        return available[PrototypeRng.NextInt(
            rng, "event", available.Length)];
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
        if (world.Event?.EventId == PrototypeNativeSharedEvents.SymbioteId
            && choice.Id == "approach_locked")
            return false;

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

            var potionId =
                PrototypeContent.PotionPool[
                    PrototypeRng.NextInt(
                        rng,
                        "event",
                        PrototypeContent.PotionPool.Length)];
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
        // MerchantInventory chooses one of the five character-card
        // slots before populating the inventory.
        var saleIndex = nativeMerchant
            ? PrototypeRng.NextInt(state.Rng, "shop", 5)
            : -1;
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
                        world.CardRarityOffsetBasisPoints, state.Rng)
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
                    ? NativeMerchantCardPrice(cardId, state.Rng)
                    : ShopCardPrice(cardId, state.Rng);
                if (onSale)
                {
                    // MerchantCardEntry.SetOnSale calls CalcCost
                    // again, then halves the newly rolled price.
                    basePrice =
                        NativeMerchantCardPrice(cardId, state.Rng) / 2;
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
                    PrototypeCardRarity.Uncommon, state.Rng),
                PrototypeColorlessCards.PickMerchantCard(
                    PrototypeCardRarity.Rare, state.Rng)
            };
            offers = offers.Concat(colorless.Select((cardId, index) =>
            {
                var price = NativeMerchantCardPrice(cardId, state.Rng);
                return new ShopOffer(
                    6 + index,
                    cardId,
                    price,
                    false,
                    BasePrice: price);
            })).ToArray();
        }

        var potionIds = nativeMerchant
            ? PrototypeNativePotionShop.PickWeightedDistinct(
                state.Rng, "shop", 3)
            : PickDistinct(
                PrototypeContent.PotionPool,
                3,
                state.Rng,
                "shop");
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
                var tier = index == 2
                    ? PrototypeRelicRarity.Shop
                    : PrototypeNativeRelicGrabBag.RollRarity(state.Rng);
                var draw = PrototypeNativeRelicGrabBag.Draw(
                    world, state.Player, state.Rng,
                    merchant: true, forcedRarity: tier);
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
                availableRelics, 3, state.Rng, "shop");
        }

        var potionOffers = potionIds
            .Select((potionId, index) =>
            {
                var basePrice = nativeMerchant
                    ? PrototypeNativePotionShop.MerchantPrice(
                        potionId, state.Rng)
                    : 40 + PrototypeRng.NextInt(
                        state.Rng,
                        "shop",
                        21);
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
                    ? PrototypeNativeRelicGrabBag.MerchantPrice(
                        relicId, state.Rng)
                    : 100 + PrototypeRng.NextInt(
                        state.Rng,
                        "shop",
                        41);
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
            Phase = RunPhase.Shop
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
        var nextCardInstanceId = world.NextCardInstanceId;

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
                    state.Rng,
                    copyCardInstanceId:
                        pending.Kind == PrototypePersistentDeckChoiceKind.Copy
                            ? nextCardInstanceId
                            : null);
                resolvedPlayer = resolved.Player;
                remaining = resolved.Remaining;
                if (pending.Kind == PrototypePersistentDeckChoiceKind.Copy)
                    nextCardInstanceId++;
            }

            if (remaining is null
                && pending.CardIdAfterChoices is { } afterChoicesCardId)
            {
                resolvedPlayer = AppendCard(
                    resolvedPlayer, nextCardInstanceId++, afterChoicesCardId,
                    state.Rng,
                    allocateCardId: () => nextCardInstanceId++);
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
            player = ApplyGoldGain(player, 100, state.Rng, world,
                nativeRunRng: nativeRunRng) with
            {
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
                state.Rng,
                allocateCardId: () => nextId++);
            shop = shop with
            {
                CardOffers = shop.CardOffers
                    .Select(item => item.OfferId == offer.OfferId ? item with { Sold = true } : item)
                    .ToArray()
            };
            if (HasTheCourier(player))
            {
                var replacement = RestockTheCourierCardOffer(
                    state, world, shop, offer);
                shop = shop with
                {
                    CardOffers = shop.CardOffers
                        .Select(item => item.OfferId == offer.OfferId
                            ? replacement : item)
                        .ToArray()
                };
            }
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
            if (HasTheCourier(player))
            {
                shop = UpdatePotionOffer(
                    shop,
                    RestockTheCourierPotionOffer(state, offer));
            }
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
            if (HasTheCourier(player))
            {
                shop = UpdatePotionOffer(
                    shop,
                    RestockTheCourierPotionOffer(state, offer));
            }
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
            if (HasTheCourier(player))
            {
                var restocked = RestockTheCourierRelicOffer(
                    state, world, player, offer);
                world = restocked.World;
                if (restocked.Offer is { } replacement)
                {
                    shop = UpdateRelicOffer(shop, replacement);
                }
            }
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
        shop = RepriceShop(shop, player);

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
        var used = RequireWorld(state).RestSiteOptionsUsed
            ?? Array.Empty<string>();
        bool Available(string option) =>
            !HasMiniatureTent(state.Player)
            || !used.Contains(option, StringComparer.Ordinal);

        if (Available(PrototypeNativeRestSiteActionsV111.Hatch)
            && CanHatchByrdonisEgg(state.Player))
        {
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Hatch));
        }
        if (Available(PrototypeNativeRestSiteActionsV111.Kindle)
            && state.Player.Relics.Any(relic =>
                relic.RelicId == NativeAncientRewardsV111.PumpkinCandleId))
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Kindle));
        if (Available(PrototypeNativeRestSiteActionsV111.Clone)
            && state.Player.ActiveRelics.Any(relic =>
                relic.RelicId == NativePaelRewardsV111.GrowthId))
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Clone));
        if (Available(PrototypeNativeRestSiteActionsV111.Heal)
            && CanRestHeal(state.Player))
        {
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Heal));
        }
        if (Available(PrototypeNativeRestSiteActionsV111.Dig)
            && PrototypeNativeRestSiteRelics.CanDig(state.Player))
        {
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Dig));
        }
        if (Available(PrototypeNativeRestSiteActionsV111.Lift)
            && PrototypeNativeRestSiteRelics.CanLift(state.Player))
        {
            actions.Add(GameAction.Empty(
                PrototypeNativeRestSiteActionsV111.Lift));
        }

        var cookableCards = Available(PrototypeNativeRestSiteActionsV111.Cook)
            && CanRestCook(state.Player)
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

        if (Available("rest_train")
            && PrototypeContent.Rules.RestTrainMaxHp > 0)
        {
            actions.Add(GameAction.Empty("rest_train"));
        }

        if (Available(PrototypeNativeRestSiteActionsV111.Smith)
            && CanRestUpgrade(state.Player))
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

    private static bool HasMiniatureTent(PlayerState player) =>
        player.ActiveRelics.Any(relic =>
            relic.RelicId == PrototypeNativeSharedRelics.MiniatureTentId);

    private static RunState CompleteRestSiteOption(RunState state) =>
        ContinueRestSite(state);

    private static RunState ContinueRestSite(RunState state)
    {
        if (!HasMiniatureTent(state.Player))
            return CompleteRoomToMap(state);

        state = state with { Phase = RunPhase.Rest };
        return GetRestActions(state).Count == 0
            ? CompleteRoomToMap(state)
            : state;
    }

    private static RunState StepRest(RunState state, GameAction action)
    {
        var option = action.Kind;
        var used = RequireWorld(state).RestSiteOptionsUsed
            ?? Array.Empty<string>();
        if (HasMiniatureTent(state.Player)
            && used.Contains(option, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Rest-site option '{option}' was already used.");
        }
        state = state with
        {
            World = RequireWorld(state) with
            {
                RestSiteOptionsUsed = used
                    .Append(option)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            }
        };

        if (action.Kind == PrototypeNativeRestSiteActionsV111.Clone)
            return ClonePaelRestCards(state);
        if (StringComparer.Ordinal.Equals(action.Kind,
                PrototypeNativeRestSiteActionsV111.Dig))
            return BeginRestDigReward(state);
        if (StringComparer.Ordinal.Equals(action.Kind,
                PrototypeNativeRestSiteActionsV111.Lift))
        {
            if (!PrototypeNativeRestSiteRelics.CanLift(state.Player))
            {
                throw new InvalidOperationException(
                    "Lifting requires Girya with an unused lift.");
            }

            return CompleteRestSiteOption(state with
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
            return CompleteRestSiteOption(state with
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

            return CompleteRestSiteOption(state with
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
            return CompleteRestSiteOption(state with { Player = player });
        }

        if (StringComparer.Ordinal.Equals(action.Kind,
                PrototypeNativeRestSiteActionsV111.Heal))
        {
            if (!CanRestHeal(state.Player))
            {
                throw new InvalidOperationException(
                    "A relic prevents resting to heal.");
            }

            var amount = RestSiteHealAmount(state.Player);
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
            return CompleteRestSiteOption(state);
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
        return CompleteRestSiteOption(state);
    }

    private static RunState BeginRestDigReward(RunState state)
    {
        if (!PrototypeNativeRestSiteRelics.CanDig(state.Player))
        {
            throw new InvalidOperationException("Digging requires Shovel.");
        }

        var world = RequireWorld(state);
        string? relicId;
        if (UsesNativeRegionalSystems(world))
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                world, state.Player, state.Rng, merchant: false);
            world = draw.World;
            relicId = draw.Id;
        }
        else
        {
            relicId = PickUnownedEventRelic(state.Player, state.Rng);
        }

        if (relicId is null)
        {
            throw new InvalidOperationException(
                "No source-eligible relic remains for Shovel to dig up.");
        }

        return state with
        {
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

        if (PrototypeNativeRoomFullOfCheese.IsCheeseSelectionReward(reward))
        {
            var options = !reward.CardResolved
                ? reward.CardOptions
                : reward.FirstPendingExtraCardGroupIndex is var cheeseGroup
                    && cheeseGroup >= 0
                    ? reward.ExtraCardOptions![cheeseGroup]
                    : Array.Empty<string>();
            if (options.Length == 0)
            {
                throw new InvalidOperationException(
                    "Room Full of Cheese has no remaining card selections.");
            }

            return options.Select((_, index) => GameAction.Create(
                "take_reward_card", new ChooseCardPayload(index))).ToArray();
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
        if (action.Kind is "sacrifice_reward_card" or "sacrifice_reward_card_group")
            return SacrificePaelCardReward(state, action);
        if (action.Kind == "reroll_reward_card")
            return RerollRewardCardGroup(state,
                action.ReadPayload<RerollRewardCardPayload>().GroupIndex);
        var world = RequireWorld(state);
        var reward = world.Reward
            ?? throw new InvalidOperationException("Reward phase has no reward state.");
        if (PrototypeNativeRoomFullOfCheese.IsCheeseSelectionReward(reward)
            && action.Kind != "take_reward_card")
        {
            throw new InvalidOperationException(
                "Room Full of Cheese requires choosing two cards.");
        }
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
            player = ApplyGoldGain(player,
                reward.ExtraGoldOptions![index], state.Rng, world,
                nativeRunRng: nativeRunRng);
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

            player = ApplyGoldGain(player, goldOffer, state.Rng, world,
                nativeRunRng: nativeRunRng);
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
                    state.Rng,
                    copyCardInstanceId:
                        deckChoice.Kind
                            == PrototypePersistentDeckChoiceKind.Copy
                            ? nextId
                            : null);
                player = resolved.Player;
                reward = reward with
                {
                    PendingDeckChoice = resolved.Remaining
                };
                if (deckChoice.Kind
                    == PrototypePersistentDeckChoiceKind.Copy)
                    nextId++;
            }

            if (reward.PendingDeckChoice is null
                && deckChoice.CardIdAfterChoices is { } afterChoicesCardId)
            {
                player = AppendCard(
                    player, nextId++, afterChoicesCardId, state.Rng,
                    allocateCardId: () => nextId++);
            }

            if (reward.PendingDeckChoice is null
                && deckChoice.HpLossAfterChoices > 0)
            {
                player = ApplyPlayerHpLossAndDeathPrevention(
                    player, deckChoice.HpLossAfterChoices);
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
                player = AppendCard(
                    player, nextId++, cardId, state.Rng,
                    allocateCardId: () => nextId++);
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
                    () => nextId++,
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
                    () => nextId++,
                    reward.CurrentCardOptionEnchantments[payload.Index]);
                if (PrototypeNativeRoomFullOfCheese.IsCheeseSelectionReward(reward))
                {
                    reward = PrototypeNativeRoomFullOfCheese
                        .RemoveChosenCardOffer(reward, payload.Index);
                }
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
                    () => nextId++,
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

        state = state with
        {
            Player = player,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
            World = world
        };

        if (PrototypeNativeRoomFullOfCheese.IsCheeseSelectionReward(reward)
            && reward.CardResolved
            && reward.FirstPendingExtraCardGroupIndex < 0)
        {
            return CompleteReward(state);
        }

        return state;
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
                forced: forced);
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
                room, state.Ascension, state.Rng)
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
        var endsAct = room == PrototypeRoomType.Boss
            && !(world.Act == 3
                && world.LaterActEncounterBag?.SecondBossEncounterId is not null
                && world.Floor == PrototypeNativeLaterActMap.RoomRows(3) + 1);
        var amethystGold = PrototypeNativeAmethystAubergine.GoldBonus(
            state.Player, room, world.Act, endsAct);
        if (!nativeOvergrowth && amethystGold > 0)
        {
            gold = checked(gold + amethystGold);
        }
        var extraGoldRewards = nativeOvergrowth
            ? PrototypeNativeAmethystAubergine.ExtraRewardOptions(
                state.Player, recoveredGold, room, world.Act, endsAct)
            : Array.Empty<int>();

        // PotionReward.Populate precedes CardReward.Populate.
        string? potion = nativeOvergrowth
            && nativePotionOffered
            && CanAcquirePotion(state.Player)
                ? PrototypeNativePotionShop.PickWeighted(
                    state.Rng, "reward")
                : null;

        var cardChoiceCount =
            RewardCardChoiceCount(state.Player);
        var prayerWheelGroups = room == PrototypeRoomType.Combat
            ? ExtraNormalCombatCardRewardGroups(state.Player)
            : 0;
        var whiteStarGroups =
            PrototypeNativeWhiteStar.AdditionalRareCardRewardGroups(
                state.Player, room);
        var extraCardRewardGroups =
            Math.Max(
                0,
                combat.ExtraCardRewardsEarned)
            + prayerWheelGroups
            + whiteStarGroups;
        var normalExtraCardRewardGroups =
            extraCardRewardGroups - whiteStarGroups;
        string[] cardOptions;
        string[][] extraCardOptions;
        bool[]? cardUpgradeFlags = null;
        bool[][]? extraCardUpgradeFlags = null;
        var hasDingyRug = state.Player.ActiveRelics.Any(relic =>
            relic.RelicId == PrototypeNativeSharedRelics.DingyRugId);
        if (nativeOvergrowth)
        {
            var offset = world.CardRarityOffsetBasisPoints;
            var first = PrototypeNativeCardRarityOdds.GenerateEncounterCards(
                cardChoiceCount, state.Ascension, room, offset, state.Rng,
                act: world.Act,
                includeColorlessCardRewards: hasDingyRug);
            cardOptions = first.Cards;
            cardUpgradeFlags = first.UpgradeFlags;
            offset = first.NextOffsetBasisPoints;
            var extras = new List<string[]>();
            var extraUpgrades = new List<bool[]>();
            // Extra combat-earned rewards belong to the initial list.
            // Prayer Wheel adds its CardReward later, from ModifyRewards.
            for (var i = 0; i < Math.Max(0, combat.ExtraCardRewardsEarned); i++)
            {
                var next = PrototypeNativeCardRarityOdds.GenerateEncounterCards(
                    cardChoiceCount, state.Ascension, room, offset, state.Rng,
                    act: world.Act,
                    includeColorlessCardRewards: hasDingyRug);
                extras.Add(next.Cards);
                extraUpgrades.Add(next.UpgradeFlags);
                offset = next.NextOffsetBasisPoints;
            }
            extraCardOptions = extras.ToArray();
            extraCardUpgradeFlags = extraUpgrades.ToArray();
            world = world with { CardRarityOffsetBasisPoints = offset };
        }
        else
        {
            cardOptions = PickRewardCards(
                world.Act, cardChoiceCount, state.Rng, hasDingyRug);
            extraCardOptions = Enumerable.Range(
                    0, normalExtraCardRewardGroups)
                .Select(_ => PickRewardCards(
                    world.Act, cardChoiceCount, state.Rng, hasDingyRug))
                .ToArray();
        }

        if (!nativeOvergrowth)
        {
            potion = CanAcquirePotion(state.Player)
                && PrototypeRng.NextBool(
                    state.Rng, "reward", 1, 2)
                ? PrototypeContent.PotionPool[
                    PrototypeRng.NextInt(
                        state.Rng,
                        "reward",
                        PrototypeContent.PotionPool.Length)]
                : null;
        }

        string? relic = null;
        if (room == PrototypeRoomType.Elite)
        {
            if (UsesNativeRegionalSystems(world))
            {
                var draw = PrototypeNativeRelicGrabBag.Draw(
                    world, state.Player, state.Rng, merchant: false);
                world = draw.World;
                relic = draw.Id;
            }
            else
            {
                relic = PrototypeContent.RelicPool[
                    PrototypeRng.NextInt(
                        state.Rng,
                        "reward",
                        PrototypeContent.RelicPool.Length)];
            }
        }
        if (nativeOvergrowth && prayerWheelGroups > 0)
        {
            // Hook.ModifyRewards happens after the original reward
            // list has been populated. Prayer Wheel appends a separate
            // normal-combat CardReward, so its RNG rolls come after
            // the base reward and (for elites) relic population.
            var extras = extraCardOptions.ToList();
            var upgrades = extraCardUpgradeFlags!.ToList();
            var offset = world.CardRarityOffsetBasisPoints;
            for (var i = 0; i < prayerWheelGroups; i++)
            {
                var next = PrototypeNativeCardRarityOdds.GenerateEncounterCards(
                    cardChoiceCount, state.Ascension,
                    PrototypeRoomType.Combat, offset, state.Rng,
                    act: world.Act,
                    includeColorlessCardRewards: state.Player.ActiveRelics
                        .Any(relic => relic.RelicId
                            == PrototypeNativeSharedRelics.DingyRugId));
                extras.Add(next.Cards);
                upgrades.Add(next.UpgradeFlags);
                offset = next.NextOffsetBasisPoints;
            }
            extraCardOptions = extras.ToArray();
            extraCardUpgradeFlags = upgrades.ToArray();
            world = world with { CardRarityOffsetBasisPoints = offset };
        }
        if (whiteStarGroups > 0)
        {
            var extras = extraCardOptions.ToList();
            var upgrades = extraCardUpgradeFlags?.ToList();
            var offset = world.CardRarityOffsetBasisPoints;
            var rarePool = PrototypeContent.NativeSilentRewardCardPool
                .Where(id => PrototypeContent.Card(id).Rarity
                    == PrototypeCardRarity.Rare)
                .ToArray();
            for (var i = 0; i < whiteStarGroups; i++)
            {
                if (nativeOvergrowth)
                {
                    var next =
                        PrototypeNativeCardRarityOdds.GenerateEncounterCards(
                            cardChoiceCount, state.Ascension,
                            PrototypeRoomType.Boss, offset, state.Rng,
                            act: world.Act);
                    extras.Add(next.Cards);
                    upgrades!.Add(next.UpgradeFlags);
                    offset = next.NextOffsetBasisPoints;
                }
                else
                {
                    extras.Add(PickDistinct(
                        rarePool, cardChoiceCount, state.Rng, "reward"));
                }
            }

            extraCardOptions = extras.ToArray();
            if (nativeOvergrowth)
            {
                extraCardUpgradeFlags = upgrades!.ToArray();
                world = world with
                {
                    CardRarityOffsetBasisPoints = offset
                };
            }
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
            ? extraCardOptions.Select(group =>
                ApplyGlitterToCardRewardOptions(group)).ToArray()
            : null;

        var cardRewardGenerators = Enumerable.Repeat<PrototypeCardRewardGenerator?>(
                nativeOvergrowth
                    ? new PrototypeCardRewardGenerator(
                        PrototypeCardRewardGeneratorKind.Encounter,
                        world.Act, room)
                    : new PrototypeCardRewardGenerator(
                        PrototypeCardRewardGeneratorKind.RewardPool,
                        world.Act, room),
                1 + normalExtraCardRewardGroups)
            .Concat(Enumerable.Repeat<PrototypeCardRewardGenerator?>(
                new PrototypeCardRewardGenerator(
                    PrototypeCardRewardGeneratorKind.RarePool,
                    world.Act),
                whiteStarGroups))
            .ToArray();
        var cardRewardForCandy = new RewardState(
            SourceRoom: room.ToString(),
            CardOptions: cardOptions,
            PotionOption: null,
            RelicOption: null,
            CardResolved: false,
            PotionResolved: true,
            RelicResolved: true,
            EndsAct: false,
            ExtraCardOptions: extraCardOptions,
            CardOptionUpgradeFlags: cardUpgradeFlags,
            ExtraCardOptionUpgradeFlags: extraCardUpgradeFlags,
            CardOptionEnchantments: cardOptionEnchantments,
            ExtraCardOptionEnchantments: extraCardOptionEnchantments,
            CardRewardGenerators: cardRewardGenerators);
        var lastingCandy = ApplyLastingCandyCombatReward(
            silverCrucible.Player, cardRewardForCandy, state.Ascension,
            world.CardRarityOffsetBasisPoints, state.Rng);
        cardOptions = lastingCandy.Reward.CardOptions;
        extraCardOptions = lastingCandy.Reward.ExtraCardOptions
            ?? Array.Empty<string[]>();
        cardUpgradeFlags = lastingCandy.Reward.CardOptionUpgradeFlags;
        extraCardUpgradeFlags =
            lastingCandy.Reward.ExtraCardOptionUpgradeFlags;
        cardOptionEnchantments =
            lastingCandy.Reward.CardOptionEnchantments;
        extraCardOptionEnchantments =
            lastingCandy.Reward.ExtraCardOptionEnchantments;
        var wingCharmResult = ApplyWingCharmToCardRewardOptions(
            state, cardOptions, extraCardOptions,
            cardOptionEnchantments, extraCardOptionEnchantments);
        state = wingCharmResult.State;
        cardOptionEnchantments = wingCharmResult.CardOptionEnchantments;
        extraCardOptionEnchantments =
            wingCharmResult.ExtraCardOptionEnchantments;
        world = world with
        {
            CardRarityOffsetBasisPoints =
                lastingCandy.CardRarityOffsetBasisPoints
        };

        var relicOptions =
            room == PrototypeRoomType.Boss
                ? PickDistinct(
                    PrototypeContent.BossRelicPool
                        .Where(id =>
                            !state.Player.Relics.Any(relic =>
                                StringComparer.Ordinal.Equals(
                                    relic.RelicId,
                                    id)))
                        .ToArray(),
                    3,
                    state.Rng,
                    "reward")
                : null;

        var ticketRelicRewards = new List<string>();
        var rewardPlayer = UpdateWongosTicketCombatEnd(
            lastingCandy.Player,
            ref world,
            state.Rng,
            ticketRelicRewards,
            out _);
        var lavaRock = ApplyLavaRockBossRewards(
            rewardPlayer, room, world.Act,
            relicOptions, state.Rng);
        var extraRelicRewardIds = lavaRock.AdditionalRelicIds.ToList();
        extraRelicRewardIds.AddRange(ticketRelicRewards);
        if (nativeOvergrowth && room == PrototypeRoomType.Elite)
        {
            var blackStarRewards = state.Player.ActiveRelics.Sum(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .ExtraEliteRelicRewardCount);
            for (var index = 0; index < blackStarRewards; index++)
            {
                var draw = PrototypeNativeRelicGrabBag.Draw(
                    world, state.Player, state.Rng, merchant: false);
                world = draw.World;
                if (draw.Id is { } relicId)
                    extraRelicRewardIds.Add(relicId);
            }
        }
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
            EndsAct: endsAct,
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
            ExtraGoldOptions: extraGoldRewards.Length > 0
                ? extraGoldRewards : null,
            ExtraGoldGroupsResolved: extraGoldRewards.Length > 0
                ? new bool[extraGoldRewards.Length] : null,
            ReturnedStolenCards: returnedCards.Length == 0
                ? null : returnedCards,
            CardRewardGenerators: cardRewardGenerators,
            LastingCandyRewardActive:
                lastingCandy.Reward.LastingCandyRewardActive,
            CombatEffectReceipts: combat.EffectReceipts is null
                ? null : (PrototypeCombatEffectReceipt[])combat.EffectReceipts.Clone());
        reward = CaptureLavaLampCombatReward(
            state.Player, combat, reward);
        world = world with
        {
            Combat = null,
            Reward = reward
        };

        var rewardPlayerAfterGold = nativeOvergrowth
            ? lavaRock.Player
            : ApplyGoldGain(lavaRock.Player, gold, state.Rng, world,
                nativeRunRng: state.NativeRunRng);

        return state with
        {
            // Legacy prototype combat rewards retain auto-credit while
            // dispatching the ordinary owner gold-gain hooks.
            Player = rewardPlayerAfterGold,
            World = world,
            Phase = RunPhase.Reward
        };
    }

    private static RunState CompleteReward(RunState state)
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
                    player, nextId++, afterRewardCardId, state.Rng,
                    allocateCardId: () => nextId++);
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
                var curseId = cursePool[PrototypeRng.NextInt(
                    state.Rng, "reward", cursePool.Length)];
                player = AppendCard(
                    player, nextId++, curseId, state.Rng,
                    allocateCardId: () => nextId++);
            }
            else if (reward.AddCardAfterReward is { } additionalCardId)
            {
                player = AppendCard(
                    player, nextId++, additionalCardId, state.Rng,
                    allocateCardId: () => nextId++);
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
            if (StringComparer.Ordinal.Equals(reward.SourceRoom, "Rest"))
                return ContinueRestSite(state);
            return CompleteRoomToMap(state);
        }

        if (world.Act >= PrototypeContent.Rules.Acts)
        {
            if (state.Configuration?.UsesExperimentalSilentThreeActRoute
                == true)
            {
                return PrototypeNativeArchitectEnding
                    .EnterAfterFinalBoss(state);
            }
            return EndRun(state, "victory");
        }

        return state with { Phase = RunPhase.ActTransition };
    }

    private static RunState CompleteRoomToMap(RunState state)
    {
        state = RecordCompletedRoom(state);
        var world = RequireWorld(state) with
        {
            ActiveRoom = null,
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null,
            RestSiteOptionsUsed = null
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

            var roll = PrototypeRng.NextInt(
                rng,
                "shop",
                weights.Sum(item => item.Weight));
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
                PrototypeRng.NextInt(rng, "shop", candidates.Length)];
            selected.Add(selectedId);
            available.Remove(selectedId);
        }

        return selected.ToArray();
    }

    private static int NativeMerchantCardPrice(
        string cardId, RngBundle rng)
    {
        var baseCost = PrototypeContent.Card(cardId).Rarity switch
        {
            PrototypeCardRarity.Common => 50,
            PrototypeCardRarity.Uncommon => 75,
            PrototypeCardRarity.Rare => 150,
            _ => throw new InvalidOperationException(
                "Native shop character cards must have a purchasable rarity.")
        };
        // MerchantCardEntry applies a 15% premium to cards from
        // ColorlessCardPool, before rolling its price modifier.
        if (PrototypeColorlessCards.ImplementedShopPool.Contains(
                cardId, StringComparer.Ordinal))
        {
            baseCost = (int)Math.Round(baseCost * 1.15);
        }
        // Native CalcCost uses Shops.NextFloat(0.95, 1.05)
        // followed by Mathf.RoundToInt. Integer percentage samples
        // approximate that distribution without claiming RNG parity.
        var percent = 95 + PrototypeRng.NextInt(rng, "shop", 11);
        return Math.Max(1,
            (int)Math.Round(baseCost * percent / 100.0));
    }

    private static int ShopCardPrice(string cardId, RngBundle rng)
    {
        var rarity = PrototypeContent.Card(cardId).Rarity;
        var (minimum, spread) = rarity switch
        {
            PrototypeCardRarity.Common => (45, 16),
            PrototypeCardRarity.Uncommon => (65, 21),
            PrototypeCardRarity.Rare => (100, 31),
            _ => throw new InvalidOperationException(
                $"Basic card '{cardId}' cannot be a normal shop offer.")
        };

        return minimum + PrototypeRng.NextInt(rng, "shop", spread);
    }

    private static string[] PickRewardCards(
        int act,
        int count,
        RngBundle rng,
        bool includeColorless = false) =>
        PickRewardCardsFromPool(
            act, count, rng,
            includeColorless
                ? PrototypeContent.RewardCardPool.Concat(
                    PrototypeColorlessCards.ImplementedRewardPool)
                : PrototypeContent.RewardCardPool);

    private static string[] AddDingyRugCardsToRewardPool(
        PlayerState player,
        IEnumerable<string> pool,
        bool cardPoolModificationsAllowed = true)
    {
        var cards = pool.ToArray();
        if (!cardPoolModificationsAllowed
            || !player.ActiveRelics.Any(relic =>
                relic.RelicId == PrototypeNativeSharedRelics.DingyRugId))
        {
            return cards;
        }

        return cards.Concat(
                PrototypeColorlessCards.ImplementedRewardPool)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] PickRewardCardsFromPool(
        int act,
        int count,
        RngBundle rng,
        IEnumerable<string> pool)
    {
        var available = pool.ToList();
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

            var totalWeight = rarityWeights.Sum(item => item.Weight);
            var roll = PrototypeRng.NextInt(rng, "reward", totalWeight);
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
                PrototypeRng.NextInt(rng, "reward", candidates.Length)];

            selected.Add(selectedId);
            available.Remove(selectedId);
        }

        return selected.ToArray();
    }

    private static PrototypeCardEnchantment?[] ApplyGlitterToCardRewardOptions(
        string[] cardIds,
        PrototypeCardEnchantment?[]? existing = null)
    {
        var result = existing is null
            ? new PrototypeCardEnchantment?[cardIds.Length]
            : (PrototypeCardEnchantment?[])existing.Clone();
        for (var index = 0; index < cardIds.Length; index++)
        {
            if (result[index] is not null)
                continue;

            var candidate = new CardInstance(
                0, cardIds[index], 0, PrototypeJson.EmptyObject());
            if (PrototypeGameEngine.CanSelectEventDeckCard(
                candidate,
                PrototypePersistentDeckChoiceKind.Enchant,
                transformToCardId: null,
                enchantmentKind: PrototypeCardEnchantmentKind.Glam,
                basicCardsOnly: false))
            {
                result[index] = new PrototypeCardEnchantment(
                    PrototypeCardEnchantmentKind.Glam);
            }
        }

        return result;
    }

    private static RunState ApplyGlitterToNewLateCardReward(
        RunState previous,
        RunState next)
    {
        if (next.Phase != RunPhase.Reward
            || next.World?.Reward is not { } reward
            || (reward.CardOptions.Length == 0
                && !(reward.ExtraCardOptions?.Any(group =>
                    group.Length > 0) ?? false)))
        {
            return next;
        }

        var isNewEventReward = previous.Phase != RunPhase.Reward
            && reward.CardRewardLateHooksEnabled
            && reward.SourceRoom.StartsWith(
                "Event:", StringComparison.Ordinal);
        var isNewGlassEyeReward = reward.SourceRoom
                == GlassEyeRewardSource
            && reward.CardRewardLateHooksEnabled
            && next.Player.Relics.Count(relic =>
                relic.RelicId == PrototypeNativeOrobasRelics.GlassEyeId)
                > previous.Player.Relics.Count(relic =>
                    relic.RelicId == PrototypeNativeOrobasRelics.GlassEyeId);
        var isNewOrreryReward = reward.SourceRoom == OrreryRewardSource
            && reward.CardRewardLateHooksEnabled
            && next.Player.Relics.Count(relic =>
                relic.RelicId == PrototypeNativeSharedRelics.OrreryId)
                > previous.Player.Relics.Count(relic =>
                    relic.RelicId == PrototypeNativeSharedRelics.OrreryId);
        if ((!isNewEventReward && !isNewGlassEyeReward
                && !isNewOrreryReward)
            || !next.Player.ActiveRelics.Any(relic =>
                relic.RelicId == NativeGloryAncientsV111.GlitterId))
        {
            return next;
        }

        var primary = reward.CardOptions.Length > 0
            ? ApplyGlitterToCardRewardOptions(
                reward.CardOptions, reward.CardOptionEnchantments)
            : reward.CardOptionEnchantments;
        var extras = reward.ExtraCardOptions is { } groups
            ? groups.Select((group, index) =>
                ApplyGlitterToCardRewardOptions(
                    group,
                    reward.ExtraCardOptionEnchantments is { } oldGroups
                        && index < oldGroups.Length
                        ? oldGroups[index]
                        : null)).ToArray()
            : reward.ExtraCardOptionEnchantments;
        var world = RequireWorld(next);

        return next with
        {
            World = world with
            {
                Reward = reward with
                {
                    CardOptionEnchantments = primary,
                    ExtraCardOptionEnchantments = extras
                }
            }
        };
    }

    private static RunState ApplyWingCharmToNewEventCardReward(
        RunState previous,
        RunState next)
    {
        if (next.Phase != RunPhase.Reward
            || next.World?.Reward is not { } reward
            || (reward.CardOptions.Length == 0
                && !(reward.ExtraCardOptions?.Any(group =>
                    group.Length > 0) ?? false)))
        {
            return next;
        }

        var isNewEventReward = previous.Phase != RunPhase.Reward
            && reward.CardRewardLateHooksEnabled
            && reward.SourceRoom.StartsWith(
                "Event:", StringComparison.Ordinal);
        var isNewGlassEyeReward = reward.SourceRoom
                == GlassEyeRewardSource
            && reward.CardRewardLateHooksEnabled
            && next.Player.Relics.Count(relic =>
                relic.RelicId == PrototypeNativeOrobasRelics.GlassEyeId)
                > previous.Player.Relics.Count(relic =>
                    relic.RelicId == PrototypeNativeOrobasRelics.GlassEyeId);
        var isNewOrreryReward = reward.SourceRoom == OrreryRewardSource
            && reward.CardRewardLateHooksEnabled
            && next.Player.Relics.Count(relic =>
                relic.RelicId == PrototypeNativeSharedRelics.OrreryId)
                > previous.Player.Relics.Count(relic =>
                    relic.RelicId == PrototypeNativeSharedRelics.OrreryId);
        if (!isNewEventReward && !isNewGlassEyeReward
            && !isNewOrreryReward)
            return next;

        var result = ApplyWingCharmToCardRewardOptions(
            next,
            reward.CardOptions,
            reward.ExtraCardOptions ?? Array.Empty<string[]>(),
            reward.CardOptionEnchantments,
            reward.ExtraCardOptionEnchantments);
        if (ReferenceEquals(result.State, next))
        {
            return next;
        }

        var world = RequireWorld(result.State);
        return result.State with
        {
            World = world with
            {
                Reward = reward with
                {
                    CardOptionEnchantments =
                        result.CardOptionEnchantments,
                    ExtraCardOptionEnchantments =
                        result.ExtraCardOptionEnchantments
                }
            }
        };
    }

    private static (RunState State,
        PrototypeCardEnchantment?[]? CardOptionEnchantments,
        PrototypeCardEnchantment?[][]? ExtraCardOptionEnchantments)
        ApplyWingCharmToCardRewardOptions(
            RunState state,
            string[] cardOptions,
            string[][] extraCardOptions,
            PrototypeCardEnchantment?[]? cardEnchantments,
            PrototypeCardEnchantment?[][]? extraEnchantments)
    {
        var wingCharmCount = state.Player.ActiveRelics.Count(relic =>
            relic.RelicId == PrototypeNativeSharedRelics.WingCharmId);
        if (wingCharmCount == 0)
        {
            return (state, cardEnchantments, extraEnchantments);
        }

        var cardGroups = new[] { cardOptions }.Concat(extraCardOptions)
            .ToArray();
        var enchantments = new PrototypeCardEnchantment?[
            cardGroups.Length][];
        for (var groupIndex = 0; groupIndex < cardGroups.Length;
             groupIndex++)
        {
            enchantments[groupIndex] = groupIndex == 0
                ? cardEnchantments is null
                    ? new PrototypeCardEnchantment?[cardGroups[groupIndex].Length]
                    : (PrototypeCardEnchantment?[])
                        cardEnchantments.Clone()
                : extraEnchantments is null
                    ? new PrototypeCardEnchantment?[cardGroups[groupIndex].Length]
                    : (PrototypeCardEnchantment?[])
                        extraEnchantments[groupIndex - 1].Clone();

            for (var charm = 0; charm < wingCharmCount; charm++)
            {
                var eligible = Enumerable.Range(
                        0, cardGroups[groupIndex].Length)
                    .Where(optionIndex =>
                        enchantments[groupIndex][optionIndex] is null
                        && PrototypeContent.Card(
                            cardGroups[groupIndex][optionIndex]).Type
                            is PrototypeCardType.Attack
                                or PrototypeCardType.Skill
                                or PrototypeCardType.Power)
                    .ToArray();
                if (eligible.Length == 0)
                    break;

                int selection;
                if (state.NativeRunRng is not null)
                {
                    state = NativeRunRngV111.SampleNextInt(
                        state, "run.niche", eligible.Length,
                        out selection);
                }
                else
                {
                    selection = PrototypeRng.NextInt(
                        state.Rng, "event", eligible.Length);
                }

                enchantments[groupIndex][eligible[selection]] =
                    new PrototypeCardEnchantment(
                        PrototypeCardEnchantmentKind.Swift, 1);
            }
        }

        return (state, enchantments[0],
            enchantments.Skip(1).ToArray());
    }

    internal static PrototypePendingDeckChoiceState?
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

        var transformedCardId =
            pool[PrototypeRng.NextInt(
                rng,
                "reward",
                pool.Length)];
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
        string streamId)
    {
        if (pool.Length == 0)
        {
            return Array.Empty<string>();
        }

        var copy = (string[])pool.Clone();
        PrototypeRng.Shuffle(rng, streamId, copy);
        return copy.Take(Math.Min(count, copy.Length)).ToArray();
    }
}
