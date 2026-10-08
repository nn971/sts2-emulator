namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetMapActions(RunState state)
    {
        var world = RequireWorld(state);
        return world.Map.AvailableNodes()
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
        var node = world.Map.AvailableNodes().FirstOrDefault(
            option => StringComparer.Ordinal.Equals(option.NodeId, payload.NodeId))
            ?? throw new InvalidOperationException($"Unknown map node '{payload.NodeId}'.");

        world = world with
        {
            Floor = node.Floor,
            ActiveRoom = node.RoomType,
            Map = world.Map with { CurrentNodeId = node.NodeId },
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null
        };
        state = state with { World = world };

        return node.RoomType switch
        {
            PrototypeRoomType.Combat => StartCombat(state, PrototypeRoomType.Combat),
            PrototypeRoomType.Elite => StartCombat(state, PrototypeRoomType.Elite),
            PrototypeRoomType.Boss => StartCombat(state, PrototypeRoomType.Boss),
            PrototypeRoomType.Event => StartEvent(state),
            PrototypeRoomType.Shop => StartShop(state),
            PrototypeRoomType.Rest => state with { Phase = RunPhase.Rest },
            _ => throw new ArgumentOutOfRangeException()
        };
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
        var eligible = PrototypeContent.Events.Values
            .Where(evt =>
                world.Act >= evt.MinAct
                && world.Act <= evt.MaxAct
                && evt.Weight > 0
                && (!evt.OncePerRun
                    || !world.EventIds.Contains(evt.Id, StringComparer.Ordinal)))
            .ToArray();

        if (eligible.Length == 0)
        {
            throw new InvalidOperationException(
                $"No prototype event is eligible in act {world.Act}.");
        }

        var previousEvent = world.EventIds.LastOrDefault();
        if (previousEvent is not null && eligible.Length > 1)
        {
            var withoutImmediateRepeat = eligible
                .Where(evt =>
                    !StringComparer.Ordinal.Equals(evt.Id, previousEvent))
                .ToArray();
            if (withoutImmediateRepeat.Length > 0)
            {
                eligible = withoutImmediateRepeat;
            }
        }

        var totalWeight = eligible.Sum(evt => evt.Weight);
        var roll = PrototypeRng.NextInt(state.Rng, "event", totalWeight);
        var selected = eligible[^1];
        foreach (var evt in eligible)
        {
            if (roll < evt.Weight)
            {
                selected = evt;
                break;
            }

            roll -= evt.Weight;
        }

        world = world with
        {
            Event = new EventState(selected.Id),
            EventHistory = world.EventIds.Append(selected.Id).ToArray()
        };

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
            return potionReplacement.CandidateSlots
                .Select(slot =>
                    GameAction.Create(
                        "replace_event_potion",
                        new ReplaceEventPotionPayload(
                            slot)))
                .ToArray();
        }

        if (eventState.PendingDeckChoice is { } pending)
        {
            return pending.CandidateCardInstanceIds
                .Select(cardInstanceId =>
                    GameAction.Create(
                        "choose_event_deck_card",
                        new ChooseEventDeckCardPayload(
                            cardInstanceId)))
                .ToArray();
        }

        var definition =
            PrototypeContent.Event(
                eventState.EventId);

        return definition.Choices
            .Where(choice =>
                CanTakeEventChoice(
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

        if (eventState.PendingPotionReplacement is { } potionReplacement)
        {
            RequireKind(
                action,
                "replace_event_potion");
            var payload =
                action.ReadPayload<
                    ReplaceEventPotionPayload>();
            if (!potionReplacement.CandidateSlots
                    .Contains(payload.Slot))
            {
                throw new InvalidOperationException(
                    $"Potion slot {payload.Slot} is not eligible for the event replacement.");
            }

            player = player with
            {
                PotionSlots = ReplacePotionSlot(
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
                World = world with
                {
                    Event = eventState
                }
            };
            return AdvanceEventContinuations(state);
        }

        if (eventState.PendingDeckChoice is { } pending)
        {
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
                        CandidateCardInstanceIds =
                            remainingCandidates
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

        if (!CanTakeEventChoice(
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
                    player = player with
                    {
                        Hp = Math.Max(
                            0,
                            player.Hp - effect.Amount)
                    };
                    break;

                case PrototypeRunEffectKind.GainGold:
                    player = player with
                    {
                        Gold =
                            player.Gold + effect.Amount
                    };
                    break;

                case PrototypeRunEffectKind.LoseGold:
                    player = player with
                    {
                        Gold =
                            player.Gold - effect.Amount
                    };
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
                        effect.CardId);
                    break;

                case PrototypeRunEffectKind.GainRelic:
                {
                    var relicId = effect.RelicId
                        ?? throw new InvalidOperationException(
                            "Gain-relic event effect is missing a relic ID.");
                    var relicDefinition =
                        PrototypeContent.Relic(
                            relicId);
                    if (player.Relics.Any(relic =>
                            StringComparer.Ordinal.Equals(
                                relic.RelicId,
                                relicId)))
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
                        rng: state.Rng);

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
                {
                    var potionId = effect.PotionId
                        ?? throw new InvalidOperationException(
                            "Gain-potion event effect is missing a potion ID.");
                    _ = PrototypeContent.Potion(
                        potionId);
                    if (!CanAcquirePotion(player))
                    {
                        throw new InvalidOperationException(
                            "Player cannot acquire potions.");
                    }

                    var emptySlot = Array.IndexOf(
                        player.PotionSlots,
                        null);
                    if (emptySlot >= 0)
                    {
                        var slots = (PotionInstance?[])
                            player.PotionSlots.Clone();
                        slots[emptySlot] = new PotionInstance(
                            potionId,
                            PrototypeJson.EmptyObject());
                        player = player with { PotionSlots = slots };
                    }
                    else
                    {
                        queuedPotionIds.Add(potionId);
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
            World = world with
            {
                NextCardInstanceId = nextId,
                Event = eventState with
                {
                    ChosenChoiceId = choice.Id,
                    PendingDeckChoice = null,
                    PendingPotionReplacement = null,
                    QueuedPotionIds = queuedPotionIds.ToArray(),
                    QueuedDeckChoices = queuedDeckChoices.ToArray()
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

    private static bool CanTakeEventChoice(
        PlayerState player,
        PrototypeEventChoiceDefinition choice,
        RunWorldState world)
    {
        if (!MatchesRouteCondition(
                world,
                choice.RouteCondition))
        {
            return false;
        }

        var goldCost = choice.Effects
            .Where(effect =>
                effect.Kind
                    == PrototypeRunEffectKind.LoseGold)
            .Sum(effect =>
                Math.Max(0, effect.Amount));
        if (goldCost > player.Gold)
        {
            return false;
        }

        var hpCost = choice.Effects
            .Where(effect =>
                effect.Kind
                    == PrototypeRunEffectKind.LoseHp)
            .Sum(effect =>
                Math.Max(0, effect.Amount));
        if (hpCost >= player.Hp)
        {
            return false;
        }

        var newlyGrantedRelics =
            new HashSet<string>(StringComparer.Ordinal);
        var canAcquirePotions = CanAcquirePotion(player);
        foreach (var effect in choice.Effects)
        {
            if (effect.Kind
                == PrototypeRunEffectKind.GainPotion)
            {
                if (effect.PotionId is null)
                {
                    throw new InvalidOperationException(
                        "Gain-potion event effect is missing a potion ID.");
                }

                _ = PrototypeContent.Potion(
                    effect.PotionId);
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
                if (player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(
                            relic.RelicId,
                            effect.RelicId))
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

        return player.Deck.Any(card =>
            deckChoice.Kind switch
            {
                PrototypePersistentDeckChoiceKind.Remove =>
                    !PrototypeContent.Card(
                        card.CardId).Eternal,
                PrototypePersistentDeckChoiceKind.Upgrade =>
                    card.UpgradeLevel == 0,
                PrototypePersistentDeckChoiceKind.Transform =>
                    !PrototypeContent.Card(
                        card.CardId).Eternal,
                _ => false
            });
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
                SourceRelicId: relic.Id);
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
                spec.UpgradeTransformedCards);
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
                TransformPersistentDeckCard(
                    player,
                    selected.InstanceId,
                    pending.UpgradeTransformedCards,
                    rng),
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
        var cardIds = new[]
            {
                (Type: PrototypeCardType.Attack, Count: 2),
                (Type: PrototypeCardType.Skill, Count: 2),
                (Type: PrototypeCardType.Power, Count: 1)
            }
            .SelectMany(group =>
                PickShopCards(
                    world.Act,
                    group.Count,
                    state.Rng,
                    group.Type))
            .ToArray();

        var offers = cardIds
            .Select((cardId, index) =>
            {
                var basePrice =
                    ShopCardPrice(cardId, state.Rng);
                return new ShopOffer(
                    index + 1,
                    cardId,
                    basePrice,
                    false,
                    BasePrice: basePrice);
            })
            .ToArray();

        var potionIds = PickDistinct(
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
        var relicIds = PickDistinct(
            availableRelics,
            3,
            state.Rng,
            "shop");

        var potionOffers = potionIds
            .Select((potionId, index) =>
            {
                var basePrice =
                    40 + PrototypeRng.NextInt(
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
                var basePrice =
                    100 + PrototypeRng.NextInt(
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
        var removalBasePrice =
            75 + ((world.Act - 1) * 15);

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
        world = world with { Shop = shop };

        return state with
        {
            Player = player,
            World = world,
            Phase = RunPhase.Shop
        };
    }

    private static IReadOnlyList<GameAction> GetShopActions(RunState state)
    {
        var world = RequireWorld(state);
        var shop = world.Shop
            ?? throw new InvalidOperationException("Shop phase has no shop state.");
        if (shop.PendingDeckChoice is { } pending)
        {
            return pending.CandidateCardInstanceIds
                .Select(cardInstanceId => GameAction.Create(
                    "choose_shop_relic_deck_card",
                    new ChooseDeckCardPayload(cardInstanceId)))
                .ToArray();
        }

        var actions = new List<GameAction>();

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
                    .Where(card => !PrototypeContent.Card(card.CardId).Eternal)
                    .Select(card => GameAction.Create(
                        "remove_card",
                        new RemoveCardPayload(card.InstanceId))));
        }

        actions.Add(GameAction.Empty("leave_shop"));
        return actions;
    }

    private static RunState StepShop(RunState state, GameAction action)
    {
        var world = RequireWorld(state);
        var shop = world.Shop
            ?? throw new InvalidOperationException("Shop phase has no shop state.");

        if (shop.PendingDeckChoice is { } pending)
        {
            RequireKind(action, "choose_shop_relic_deck_card");
            var payload = action.ReadPayload<ChooseDeckCardPayload>();
            var resolved = ResolveRelicDeckChoice(
                state.Player,
                pending,
                payload.CardInstanceId,
                state.Rng);
            return state with
            {
                Player = resolved.Player,
                World = world with
                {
                    Shop = shop with
                    {
                        PendingDeckChoice = resolved.Remaining
                    }
                }
            };
        }

        if (StringComparer.Ordinal.Equals(action.Kind, "leave_shop"))
        {
            return CompleteRoomToMap(state);
        }

        var player = state.Player;
        var nextId = world.NextCardInstanceId;

        if (StringComparer.Ordinal.Equals(action.Kind, "remove_card"))
        {
            if (shop.RemovalUsed)
            {
                throw new InvalidOperationException("Shop card removal was already used.");
            }

            if (player.Gold < shop.RemovalPrice)
            {
                throw new InvalidOperationException("Shop card removal is unaffordable.");
            }

            var payload = action.ReadPayload<RemoveCardPayload>();
            var cardToRemove = player.Deck.FirstOrDefault(
                card => card.InstanceId == payload.CardInstanceId)
                ?? throw new InvalidOperationException(
                    $"Card instance {payload.CardInstanceId} is missing.");

            if (PrototypeContent.Card(cardToRemove.CardId).Eternal)
            {
                throw new InvalidOperationException(
                    $"Eternal card {payload.CardInstanceId} cannot be removed from the deck.");
            }

            player = player with
            {
                Gold = player.Gold - shop.RemovalPrice,
                Deck = player.Deck
                    .Where(card => card.InstanceId != payload.CardInstanceId)
                    .ToArray()
            };
            shop = shop with { RemovalUsed = true };
        }
        else if (StringComparer.Ordinal.Equals(action.Kind, "buy_card"))
        {
            var payload = action.ReadPayload<BuyOfferPayload>();
            var offer = shop.CardOffers.FirstOrDefault(item => item.OfferId == payload.OfferId)
                ?? throw new InvalidOperationException($"Unknown card offer {payload.OfferId}.");
            EnsurePurchasable(player, offer);
            player = AppendCard(player with { Gold = player.Gold - offer.Price }, nextId++, offer.ItemId);
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

            EnsurePurchasable(player, offer);
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
                Gold = player.Gold - offer.Price,
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

            EnsurePurchasable(player, offer);
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
                    player.Gold - offer.Price,
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

            EnsurePurchasable(player, offer);
            player = player with
            {
                Gold = player.Gold - offer.Price,
                Relics = player.Relics.Append(
                    new RelicInstance(offer.ItemId, PrototypeJson.EmptyObject())).ToArray()
            };
            player = ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: offer.ItemId,
                rng: state.Rng);
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

        world = world with
        {
            Shop = shop,
            NextCardInstanceId = nextId
        };

        return state with
        {
            Player = player,
            World = world
        };
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

    private static void EnsurePurchasable(PlayerState player, ShopOffer offer)
    {
        if (offer.Sold)
        {
            throw new InvalidOperationException($"Offer {offer.OfferId} is already sold.");
        }

        if (player.Gold < offer.Price)
        {
            throw new InvalidOperationException($"Offer {offer.OfferId} is unaffordable.");
        }
    }

    private static IReadOnlyList<GameAction> GetRestActions(RunState state)
    {
        var actions = new List<GameAction>();
        if (CanRestHeal(state.Player))
        {
            actions.Add(GameAction.Empty("rest_heal"));
        }

        if (PrototypeContent.Rules.RestTrainMaxHp > 0)
        {
            actions.Add(GameAction.Empty("rest_train"));
        }

        if (CanRestUpgrade(state.Player))
        {
            actions.AddRange(
                state.Player.Deck
                    .Where(card => card.UpgradeLevel == 0)
                    .Select(card => GameAction.Create(
                        "rest_upgrade",
                        new UpgradeCardPayload(card.InstanceId))));
        }

        return actions;
    }

    private static RunState StepRest(RunState state, GameAction action)
    {
        if (StringComparer.Ordinal.Equals(action.Kind, "rest_heal"))
        {
            if (!CanRestHeal(state.Player))
            {
                throw new InvalidOperationException(
                    "A relic prevents resting to heal.");
            }

            var amount = Math.Max(
                1,
                (state.Player.MaxHp * PrototypeContent.Rules.RestHealPercent) / 100);
            state = state with
            {
                Player = state.Player with
                {
                    Hp = Math.Min(state.Player.MaxHp, state.Player.Hp + amount)
                }
            };
            return CompleteRoomToMap(state);
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

        RequireKind(action, "rest_upgrade");
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

                if (card.UpgradeLevel != 0)
                {
                    throw new InvalidOperationException("Prototype cards may currently be upgraded once.");
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

    private static IReadOnlyList<GameAction> GetRewardActions(RunState state)
    {
        var reward = RequireWorld(state).Reward
            ?? throw new InvalidOperationException("Reward phase has no reward state.");

        if (reward.PendingDeckChoice is { } deckChoice)
        {
            return deckChoice.CandidateCardInstanceIds
                .Select(cardInstanceId =>
                    GameAction.Create(
                        "choose_relic_deck_card",
                        new ChooseDeckCardPayload(
                            cardInstanceId)))
                .ToArray();
        }

        if (!reward.CardResolved)
        {
            var actions = reward.CardOptions
                .Select((_, index) => GameAction.Create(
                    "take_reward_card",
                    new ChooseCardPayload(index)))
                .ToList();
            actions.Add(GameAction.Empty("skip_reward_card"));
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

        return [GameAction.Empty("leave_reward")];
    }

    private static RunState StepReward(RunState state, GameAction action)
    {
        var world = RequireWorld(state);
        var reward = world.Reward
            ?? throw new InvalidOperationException("Reward phase has no reward state.");
        var player = state.Player;
        var nextId = world.NextCardInstanceId;

        if (reward.PendingDeckChoice is { } deckChoice)
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
        else if (!reward.CardResolved)
        {
            if (StringComparer.Ordinal.Equals(action.Kind, "take_reward_card"))
            {
                var payload = action.ReadPayload<ChooseCardPayload>();
                if (payload.Index < 0 || payload.Index >= reward.CardOptions.Length)
                {
                    throw new InvalidOperationException($"Reward card index {payload.Index} is invalid.");
                }

                player = AppendCard(player, nextId++, reward.CardOptions[payload.Index]);
            }
            else
            {
                RequireKind(action, "skip_reward_card");
            }

            reward = reward with { CardResolved = true };
        }
        else if (reward.ExtraCardRewardsResolved
                 < (reward.ExtraCardOptions?.Length ?? 0))
        {
            var extraCardOptions =
                reward.ExtraCardOptions
                ?? throw new InvalidOperationException(
                    "Extra card reward state is missing.");
            var currentOptions =
                extraCardOptions[
                    reward.ExtraCardRewardsResolved];

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

                player = AppendCard(
                    player,
                    nextId++,
                    currentOptions[payload.Index]);
            }
            else
            {
                RequireKind(action, "skip_reward_card");
            }

            reward = reward with
            {
                ExtraCardRewardsResolved =
                    reward.ExtraCardRewardsResolved + 1
            };
        }
        else if (!reward.PotionResolved)
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
        else if (!reward.RelicResolved)
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
                rng: state.Rng);
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
        else
        {
            RequireKind(action, "leave_reward");
            state = state with
            {
                Player = player,
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
            World = world
        };
    }

    private static RunState EnterReward(RunState state)
    {
        var world = RequireWorld(state);
        var combat = world.Combat
            ?? throw new InvalidOperationException(
                "Combat reward has no combat state.");
        var room = world.ActiveRoom
            ?? throw new InvalidOperationException("Combat reward has no active room.");

        var gold = room switch
        {
            PrototypeRoomType.Elite => 40 + (world.Act * 5),
            PrototypeRoomType.Boss => 60 + (world.Act * 5),
            _ => 20 + (world.Act * 5)
        };

        var cardChoiceCount =
            RewardCardChoiceCount(state.Player);
        var cardOptions = PickRewardCards(
            world.Act,
            cardChoiceCount,
            state.Rng);
        var extraCardRewardGroups =
            Math.Max(
                0,
                combat.ExtraCardRewardsEarned)
            + (room == PrototypeRoomType.Combat
                ? ExtraNormalCombatCardRewardGroups(
                    state.Player)
                : 0);
        var extraCardOptions = Enumerable.Range(
                0,
                extraCardRewardGroups)
            .Select(_ => PickRewardCards(
                world.Act,
                cardChoiceCount,
                state.Rng))
            .ToArray();

        var potion = CanAcquirePotion(state.Player)
            && PrototypeRng.NextBool(
                state.Rng,
                "reward",
                1,
                2)
            ? PrototypeContent.PotionPool[
                PrototypeRng.NextInt(
                    state.Rng,
                    "reward",
                    PrototypeContent.PotionPool.Length)]
            : null;

        var relic = room == PrototypeRoomType.Elite
            ? PrototypeContent.RelicPool[
                PrototypeRng.NextInt(
                    state.Rng,
                    "reward",
                    PrototypeContent.RelicPool.Length)]
            : null;
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
            EndsAct: room == PrototypeRoomType.Boss,
            ExtraCardOptions: extraCardOptions,
            RelicOptions: relicOptions);

        world = world with
        {
            Combat = null,
            Reward = reward
        };

        return state with
        {
            Player = state.Player with { Gold = state.Player.Gold + gold },
            World = world,
            Phase = RunPhase.Reward
        };
    }

    private static RunState CompleteReward(RunState state)
    {
        var world = RequireWorld(state);
        var reward = world.Reward
            ?? throw new InvalidOperationException("Reward phase has no reward state.");

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

    private static RunState CompleteRoomToMap(RunState state)
    {
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
        RngBundle rng)
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

    private static PrototypePendingDeckChoiceState?
        CreateRelicDeckChoice(
            PlayerState player,
            string relicId)
    {
        var spec =
            PrototypeContent.Relic(relicId)
                .AcquisitionDeckChoice;
        if (spec is null || spec.Selections <= 0)
        {
            return null;
        }

        var candidates = player.Deck
            .Where(card =>
                !PrototypeContent.Card(card.CardId)
                    .Eternal)
            .Select(card => card.InstanceId)
            .ToArray();
        var selections =
            Math.Min(spec.Selections, candidates.Length);
        return selections <= 0
            ? null
            : new PrototypePendingDeckChoiceState(
                relicId,
                spec.Kind,
                selections,
                candidates,
                spec.UpgradeTransformedCards);
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
