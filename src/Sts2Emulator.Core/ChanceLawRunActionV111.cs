namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public static ChanceLawV111 QueryControlledRunAction(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.Stochastic.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        var pending = ControlledRunActionPendingV111.Describe(run);
        var controlledRunStartRequest =
            ControlledStochasticRequestV111.FromIndex(pending.Request);
        var configuredOvergrowthBossStart =
            continuation.ActionId == "start_run"
            && ControlledRunActionEngineV111
                .IsConfiguredNativeReferenceNatureOvergrowthRunStartSource(
                    run)
            && ControlledRunActionEngineV111
                .IsConfiguredNativeOvergrowthBossRequest(
                    controlledRunStartRequest);
        var configuredUnderdocksBossStart =
            continuation.ActionId == "start_run"
            && ControlledRunActionEngineV111
                .IsConfiguredNativeReferenceNatureUnderdocksRunStartSource(
                    run)
            && ControlledRunActionEngineV111
                .IsConfiguredNativeUnderdocksBossRequest(
                    controlledRunStartRequest);
        if (configuredOvergrowthBossStart || configuredUnderdocksBossStart)
        {
            var bossPool = configuredOvergrowthBossStart
                ? PrototypeContent.OvergrowthBossEncounterPool
                : PrototypeNativeUnderdocks.NativeBossEncounterIds;
            var law = configuredOvergrowthBossStart
                ? QueryOvergrowthBossEncounter(pending.Request.Ordinal)
                : QueryUnderdocksBossEncounter(pending.Request.Ordinal);
            if (law.SiteId != pending.Request.SiteId
                || law.OperationKind != pending.Request.OperationKind
                || law.OwnerId != pending.Request.OwnerId
                || law.Ordinal != pending.Request.Ordinal
                || law.Outcomes.Length != pending.Request.SupportCount
                || !law.Outcomes.Select(outcome => outcome.OutcomeId)
                    .SequenceEqual(bossPool.Select(id => "encounter:" + id),
                        StringComparer.Ordinal)
                || !(configuredOvergrowthBossStart
                    ? ControlledRunActionTraceV111
                        .MatchesOvergrowthBossCandidateLabels(
                            pending.CandidateLabels)
                    : ControlledRunActionTraceV111
                        .MatchesUnderdocksBossCandidateLabels(
                            pending.CandidateLabels)))
                throw new InvalidDataException(
                    "Act 1 boss Chance Law differs from its controlled run-start support.");
            return law with { EventId = pending.ChanceEventId };
        }
        if (continuation.ActionId == "choose_map_node" &&
            pending.Request.SiteId == "event.unknown_room.odds.ticket")
            return QueryControlledRunActionUnknownRoomOdds(
                run, continuation, pending.ChanceEventId);
        if (continuation.ActionId == "choose_map_node" &&
            pending.Request.SiteId == "event.selection.weighted_ticket")
            return QueryControlledRunActionEventSelection(
                run, continuation, pending.ChanceEventId);
        if (continuation.ActionId == "choose_map_node" &&
            pending.Request.SiteId ==
                "combat.encounter.weighted_selection.ticket")
            return QueryControlledRunActionWeightedEncounterSelection(
                run, continuation, pending.ChanceEventId);
        if (continuation.ActionId == "choose_map_node" &&
            continuation.MapNodeId is { } mapNodeId &&
            PrototypeGameEngine.DescribeActOneEncounterSelection(
                run,
                GameAction.Create("choose_map_node",
                    new ChooseMapNodePayload(mapNodeId)),
                pending.Request.SiteId) is { } encounterSupport)
            return QueryActOneEncounterSelection(
                run,
                GameAction.Create("choose_map_node",
                    new ChooseMapNodePayload(mapNodeId)),
                pending.Request,
                pending.CandidateLabels,
                encounterSupport) with { EventId = pending.ChanceEventId };
        if (QueryControlledRunActionSemanticLaw(
                run, continuation, pending.ChanceEventId) is { } semanticLaw)
            return semanticLaw;
        return QueryRandomIndex(pending.ChanceEventId, pending.Request);
    }

    private static ChanceLawV111 QueryControlledRunActionEventSelection(
        RunState run,
        PrototypeControlledRunActionContinuationV111 continuation,
        string eventId)
    {
        var request = continuation.PendingRequest;
        var directEventNode = continuation.MapNodeId is { } directEventNodeId &&
            run.World is { } directEventWorld &&
            PrototypeNativeWingedBoots.AvailableNodes(
                    directEventWorld, run.Player)
                .Any(node => node.NodeId == directEventNodeId &&
                    node.RoomType == PrototypeRoomType.Event);
        var unknownNodeResolvedToEvent =
            ControlledRunActionEngineV111
                .IsPendingLegacyUnknownEventSelection(continuation, run);
        if (run.Phase != RunPhase.MapChoice ||
            run.Stochastic.NativeRunRng is not null ||
            run.World is null ||
            !directEventNode && !unknownNodeResolvedToEvent)
            throw new InvalidDataException(
                "Controlled event-selection law is outside its legacy Event-node boundary.");

        if (run.World is { } entryWorld &&
            PrototypeGameEngine.ShouldForceWarHistorianRepy(
                entryWorld, run.Player))
            throw new InvalidDataException(
                "Controlled event-selection law is outside the forced Repy room boundary.");
        var entryState = PrototypeGameEngine.ProjectMapNodeEntryContext(
            run,
            continuation.MapNodeId ?? throw new InvalidDataException(
                "Controlled event-selection request is missing its map node."),
            unknownNodeResolvedToEvent ? PrototypeRoomType.Event : null);
        var support = PrototypeEventSelectionSupportV111.Describe(entryState);
        if (request.SiteId != "event.selection.weighted_ticket" ||
            request.StreamId != "event" ||
            request.OperationKind != "bounded_integer" ||
            request.OwnerId != support.RequestOwnerId ||
            request.BoundedCount != support.TotalTicketCount ||
            request.OrderedCandidates.Length != 0)
            throw new InvalidDataException(
                "Controlled event-selection request differs from its eligible weighted support.");

        if (support.TotalTicketCount > ChanceLawV111.MaximumExplicitOutcomeCount)
            return QueryRandomIndex(eventId, request);

        var outcomeIds = support.TicketOutcomeIds();
        if (!support.MatchesTicketOutcomeLabels(
                continuation.PendingCandidateLabels,
                ControlledRunActionEngineV111.MaximumInlineOutcomeActions))
            throw new InvalidDataException(
                "Controlled event-selection labels differ from its ordered weighted tickets.");

        var outcomes = outcomeIds.Select(outcomeId =>
            new ChanceLawOutcomeV111(outcomeId, 1L)).ToArray();
        var law = CreateLegacyCategoricalLaw(
            request.SiteId, request.StreamId, request.OwnerId,
            request.Ordinal,
            "legacy-event-selection-weighted-ticket-identities-v1",
            new
            {
                request,
                support.Act,
                support.Floor,
                support.UsesProvisionalUniformWeights,
                orderedEvents = support.Candidates,
                orderedTicketOutcomeIds = outcomeIds
            },
            "Equal mass per legacy bounded ticket, with each outcome bound " +
            "to its source-ordered event and local ticket position. The " +
            "emulator eligibility, event weights, and event-to-ticket mapping " +
            "are captured; Native event-pool parity and legacy modulo " +
            "calibration remain unverified.",
            outcomes);
        return law with { EventId = eventId };
    }

    private static ChanceLawV111 QueryControlledRunActionUnknownRoomOdds(
        RunState run,
        PrototypeControlledRunActionContinuationV111 continuation,
        string eventId)
    {
        var request = continuation.PendingRequest;
        var world = run.World;
        var nodeIsAvailableUnknown = world is not null &&
            continuation.MapNodeId is { } nodeId &&
            PrototypeNativeWingedBoots.AvailableNodes(world, run.Player)
                .Any(node => node.NodeId == nodeId &&
                    node.RoomType == PrototypeRoomType.Unknown);
        if (run.Phase != RunPhase.MapChoice ||
            run.Configuration is not null ||
            run.Stochastic.NativeRunRng is not null ||
            world is null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            !nodeIsAvailableUnknown ||
            continuation.Decisions.Length != 0)
            throw new InvalidDataException(
                "Controlled Unknown-room law is outside its legacy regional map boundary.");

        var support = PrototypeUnknownRoomOddsSupportV111.Describe(run);
        if (request.SiteId != "event.unknown_room.odds.ticket" ||
            request.StreamId != "event" ||
            request.OperationKind != "bounded_integer" ||
            request.OwnerId != support.RequestOwnerId ||
            request.BoundedCount != support.TotalTicketCount ||
            request.OrderedCandidates.Length != 0 ||
            !support.MatchesTicketOutcomeLabels(
                continuation.PendingCandidateLabels,
                ControlledRunActionEngineV111.MaximumInlineOutcomeActions))
            throw new InvalidDataException(
                "Controlled Unknown-room request differs from its ordered ticket-to-room support.");

        var outcomes = support.TicketOutcomeIds().Select(outcomeId =>
            new ChanceLawOutcomeV111(outcomeId, 1L)).ToArray();
        var law = CreateLegacyCategoricalLaw(
            request.SiteId, request.StreamId, request.OwnerId,
            request.Ordinal,
            "legacy-unknown-room-ticket-identities-v1",
            new
            {
                request,
                support.Act,
                support.Floor,
                support.EventOnly,
                odds = world.UnknownRoomOdds ??
                    new PrototypeUnknownRoomOddsState(),
                orderedTicketOutcomeIds = support.TicketOutcomeIds()
            },
            "Equal mass per legacy bounded ticket, with each outcome bound " +
            "to the emulator's room result and raw ticket position. The " +
            "ticket-to-room mapping is captured; the source NextFloat grid, " +
            "legacy modulo calibration, and Native stream parity remain " +
            "unverified.",
            outcomes);
        return law with { EventId = eventId };
    }

    private static ChanceLawV111
        QueryControlledRunActionWeightedEncounterSelection(
            RunState run,
            PrototypeControlledRunActionContinuationV111 continuation,
            string eventId)
    {
        var request = continuation.PendingRequest;
        var world = run.World;
        var selectedNode = world is not null &&
            continuation.MapNodeId is { } nodeId
            ? PrototypeNativeWingedBoots.AvailableNodes(world, run.Player)
                .SingleOrDefault(node => node.NodeId == nodeId)
            : null;
        if (run.Phase != RunPhase.MapChoice ||
            run.Configuration is not null ||
            run.Stochastic.NativeRunRng is not null ||
            world is null ||
            world.ActOneRegion is not null ||
            world.ActOneEncounterPool is not null ||
            PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            selectedNode?.RoomType is not (PrototypeRoomType.Combat or
                PrototypeRoomType.Elite or PrototypeRoomType.Boss) ||
            continuation.Decisions.Length != 0)
            throw new InvalidDataException(
                "Controlled weighted-encounter law is outside the legacy prototype combat-entry boundary.");

        var encounterEntryState = PrototypeGameEngine
            .ProjectMapNodeEntryContext(run,
                continuation.MapNodeId ?? throw new InvalidDataException(
                    "Controlled weighted-encounter request is missing its map node."));
        var support = PrototypeWeightedEncounterSupportV111.Describe(
            encounterEntryState.World!, selectedNode.RoomType);
        if (request.SiteId !=
                "combat.encounter.weighted_selection.ticket" ||
            request.StreamId != "combat" ||
            request.OperationKind != "bounded_integer" ||
            request.OwnerId != support.RequestOwnerId ||
            request.BoundedCount != support.TotalTicketCount ||
            request.OrderedCandidates.Length != 0 ||
            !support.MatchesTicketOutcomeLabels(
                continuation.PendingCandidateLabels,
                ControlledRunActionEngineV111.MaximumInlineOutcomeActions))
            throw new InvalidDataException(
                "Controlled weighted-encounter request differs from its current ordered support.");

        if (support.TotalTicketCount > ChanceLawV111.MaximumExplicitOutcomeCount)
            return QueryRandomIndex(eventId, request);

        var outcomeIds = support.TicketOutcomeIds();
        var outcomes = outcomeIds.Select(outcomeId =>
            new ChanceLawOutcomeV111(outcomeId, 1L)).ToArray();
        var law = CreateLegacyCategoricalLaw(
            request.SiteId, request.StreamId, request.OwnerId,
            request.Ordinal,
            "legacy-weighted-encounter-ticket-identities-v1",
            new
            {
                request,
                support.Act,
                support.Floor,
                support.RoomType,
                orderedEncounterIds = support.EncounterIds,
                orderedTicketWeights = support.TicketWeights,
                orderedTicketOutcomeIds = outcomeIds
            },
            "Equal mass per legacy bounded ticket, with each outcome bound " +
            "to its emulator encounter and local ticket position. The " +
            "current encounter eligibility, no-immediate-repeat filter, " +
            "weights, and ticket mapping are captured; Native pool parity " +
            "and legacy modulo calibration remain unverified.",
            outcomes);
        return law with { EventId = eventId };
    }

    internal static ChanceLawV111 QueryActOneEncounterSelection(
        RunState source,
        GameAction action,
        RandomIndexRequestV111 request,
        string[]? candidateLabels = null,
        ActOneEncounterSelectionSupportV111? knownSupport = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var support = knownSupport ??
            PrototypeGameEngine.DescribeActOneEncounterSelection(
                source, action, request.SiteId);
        var expectedCandidates = support is null
            ? []
            : Enumerable.Range(0, support.EncounterIds.Length).ToArray();
        var numericLabels = candidateLabels is not null && support is not null &&
            candidateLabels.SequenceEqual(
                Enumerable.Range(0, support.EncounterIds.Length).Select(index =>
                    index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                StringComparer.Ordinal);
        if (support is null || request.StreamId != "combat" ||
            request.SiteId != support.SiteId ||
            request.OwnerId != support.OwnerId ||
            request.OperationKind != "choose_index" ||
            request.OrderedCandidates.Length != support.EncounterIds.Length ||
            !request.OrderedCandidates.SequenceEqual(expectedCandidates) ||
            candidateLabels is not null &&
                !numericLabels &&
                !candidateLabels.SequenceEqual(
                    support.EncounterIds, StringComparer.Ordinal))
            throw new InvalidDataException(
                "Act 1 encounter-pool Chance Law differs from its source selection support.");

        var conditioning = new
        {
            request.SiteId,
            request.StreamId,
            request.OwnerId,
            request.Ordinal,
            support.EncounterIds
        };
        var outcomes = support.EncounterIds.Select(encounterId =>
            new ChanceLawOutcomeV111("encounter:" + encounterId, 1L))
            .ToArray();
        return CreateLegacyCategoricalLaw(
            request.SiteId,
            request.StreamId,
            request.OwnerId,
            request.Ordinal,
            "act-one-encounter-pool-identity-v1",
            conditioning,
            "Equal mass per ordered encounter-pool index, with outcomes " +
            "resolved to source encounter identities; seed-to-index parity " +
            "is unverified.",
            outcomes,
            operationKind: request.OperationKind);
    }

    private static ChanceLawV111? QueryControlledRunActionSemanticLaw(
        RunState run,
        PrototypeControlledRunActionContinuationV111 continuation,
        string eventId)
    {
        var request = continuation.PendingRequest;
        ChanceLawV111? law = request.SiteId switch
        {
            "reward.card_transform.replacement.index_draw" =>
                QueryControlledRelicDeckTransform(run, continuation),
            "relic.acquisition.random_rare_card.model"
                when request.StreamId == "reward" =>
                    QueryLegacyRelicAcquisitionRareCardModel(
                        request.Ordinal),
            "relic.acquisition.random_potion.model"
                when request.StreamId == "event" =>
                    QueryLegacyRelicAcquisitionPotionModel(
                        ControlledPotionSlotIndex(request), request.Ordinal),
            "event.potion.fill_empty_slots.model"
                when request.StreamId == "event" =>
                    QueryLegacyEventFillPotionSlot(
                        ControlledPotionSlotIndex(request),
                        canAcquirePotion: true, slotIsEmpty: true,
                        ordinal: request.Ordinal),
            "run.potion.fill_empty_slots.model"
                when request.StreamId == "reward" =>
                    QueryLegacyRunPotionFillSlotModel(
                        ControlledPotionSlotIndex(request),
                        canAcquirePotion: true, slotIsEmpty: true,
                        ordinal: request.Ordinal),
            "reward.card_model.index_draw"
                when request.StreamId == "reward" &&
                    continuation.ActionId ==
                        PrototypeNativeRestSiteActionsV111.Heal =>
                    QueryControlledLegacyRestHealCardModel(
                        run, continuation),
            "reward.card_rarity.ticket"
                when request.StreamId == "reward" &&
                    continuation.ActionId ==
                        PrototypeNativeRestSiteActionsV111.Heal =>
                    QueryControlledLegacyRestHealRarityTicket(
                        run, continuation),
            "map.legacy.edge_extra_chance.ticket"
                when request.StreamId == "map" &&
                    continuation.ActionId == "continue_act" =>
                    QueryLegacyMapEdgeExtraChanceTicketIndices(request),
            "treasure.relic_model.index_draw"
                when request.StreamId == "reward" =>
                    QueryControlledLegacyTreasureRelicModel(
                        run, continuation),
            _ => null
        };
        if (law is null)
            return null;

        var outcomePrefix = request.SiteId switch
        {
            "relic.acquisition.random_rare_card.model" or
                "reward.card_transform.replacement.index_draw" or
                "reward.card_model.index_draw" => "card:",
            "treasure.relic_model.index_draw" => "relic:",
            _ => "potion:"
        };
        var expectedStream = request.SiteId switch
        {
            "map.legacy.edge_extra_chance.ticket" => "map",
            "relic.acquisition.random_rare_card.model" or
                "reward.card_transform.replacement.index_draw" or
                "reward.card_model.index_draw" or
                "reward.card_rarity.ticket" or
                "treasure.relic_model.index_draw" or
                "run.potion.fill_empty_slots.model" => "reward",
            _ => "event"
        };
        var outcomes = law.Outcomes;
        var numericSupportLabels = request.SiteId ==
            "reward.card_rarity.ticket" || request.SiteId ==
                "map.legacy.edge_extra_chance.ticket";
        var supportsOmittedLabels = numericSupportLabels ||
            request.SiteId == "reward.card_model.index_draw";
        var omittedLabels = supportsOmittedLabels &&
            continuation.PendingCandidateLabels.Length == 0 &&
            outcomes.Length >
                ControlledRunActionEngineV111.MaximumInlineOutcomeActions;
        var legacyNumericTreasureLabels = request.SiteId ==
                "treasure.relic_model.index_draw" &&
            continuation.PendingCandidateLabels.SequenceEqual(
                Enumerable.Range(0, outcomes.Length).Select(index =>
                    index.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                StringComparer.Ordinal);
        var labelsMatch = numericSupportLabels
            ? omittedLabels ||
                continuation.PendingCandidateLabels.SequenceEqual(
                    Enumerable.Range(0, outcomes.Length).Select(index =>
                        index.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)),
                    StringComparer.Ordinal)
            : omittedLabels ||
                legacyNumericTreasureLabels ||
                continuation.PendingCandidateLabels.Length == outcomes.Length &&
                    outcomes.Select(outcome => outcome.OutcomeId)
                        .SequenceEqual(continuation.PendingCandidateLabels.Select(
                            label => outcomePrefix + label), StringComparer.Ordinal);
        var boundedTicketSemanticSupport = request.SiteId is
            "reward.card_rarity.ticket" or
            "map.legacy.edge_extra_chance.ticket";
        var orderedSupportMatches = boundedTicketSemanticSupport
            ? request.OperationKind == "bounded_integer" &&
                request.BoundedCount == outcomes.Length &&
                request.OrderedCandidates.Length == 0
            : request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, outcomes.Length));
        if (request.SiteId != law.SiteId ||
            request.StreamId != expectedStream ||
            request.OperationKind != law.OperationKind ||
            request.OwnerId != law.OwnerId ||
            request.Ordinal != law.Ordinal ||
            !orderedSupportMatches ||
            !labelsMatch)
            throw new InvalidDataException(
                "Controlled run-action law differs from its saved semantic support.");

        return law with { EventId = eventId };
    }

    private static ChanceLawV111 QueryControlledRelicDeckTransform(
        RunState run,
        PrototypeControlledRunActionContinuationV111 continuation)
    {
        var action = continuation.RunAction ??
            throw new InvalidDataException(
                "Controlled relic deck transform has no saved action.");
        if (!ControlledRunActionEngineV111
                .IsRelicDeckTransformActionKind(action.Kind) ||
            action.Kind != continuation.ActionId ||
            !ControlledRunActionEngineV111.IsRandomRelicDeckTransformAction(
                run, action))
            throw new InvalidDataException(
                "Controlled relic deck transform differs from its saved action.");

        var cardInstanceId = action.ReadPayload<ChooseDeckCardPayload>()
            .CardInstanceId;
        var card = run.Player.Deck.Single(candidate =>
            candidate.InstanceId == cardInstanceId);
        var law = QueryLegacyRewardCardTransform(
            cardInstanceId, card.CardId,
            continuation.PendingRequest.Ordinal);
        return law;
    }

    private static ChanceLawV111 QueryControlledLegacyTreasureRelicModel(
        RunState run,
        PrototypeControlledRunActionContinuationV111 continuation)
    {
        if (continuation.ActionId != "choose_map_node" ||
            continuation.MapNodeId is null ||
            run.Phase != RunPhase.MapChoice || run.World is not { } world)
            throw new InvalidDataException(
                "Controlled legacy Treasure law is outside its map-node boundary.");

        var ownedRelicIds = run.Player.Relics
            .Select(relic => relic.RelicId).ToArray();
        return QueryLegacyTreasureRelicModel(
            world.Act, world.Floor, ownedRelicIds,
            continuation.PendingRequest.Ordinal);
    }

    private static ChanceLawV111 QueryControlledLegacyRestHealCardModel(
        RunState run,
        PrototypeControlledRunActionContinuationV111 continuation)
    {
        var action = continuation.RunAction ??
            throw new InvalidDataException(
                "Controlled rest-heal card query has no saved action.");
        if (!ControlledRunActionEngineV111.IsLegacyRestHealRewardAction(
                run, action) || run.World is not { } world)
            throw new InvalidDataException(
                "Controlled rest-heal card query is outside its legacy reward boundary.");

        var request = continuation.PendingRequest;
        var groupOwnerId = $"act:{world.Act}:reward_group:external";
        var groupMarker = groupOwnerId + ":card:";
        const string rarityMarker = ":rarity:";
        const string poolMarker = ":pool:";
        if (!request.OwnerId.StartsWith(groupMarker,
                StringComparison.Ordinal))
            throw new InvalidDataException(
                "Controlled rest-heal card request has an invalid reward-group owner.");
        var suffix = request.OwnerId[groupMarker.Length..];
        var rarityOffset = suffix.IndexOf(rarityMarker,
            StringComparison.Ordinal);
        var poolSearchOffset = rarityOffset + rarityMarker.Length;
        var poolOffset = rarityOffset < 0 ||
            poolSearchOffset > suffix.Length ? -1 : suffix.IndexOf(
                poolMarker, poolSearchOffset, StringComparison.Ordinal);
        if (rarityOffset < 0 || poolOffset < 0 ||
            !int.TryParse(suffix[..rarityOffset],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var priorPickCount) || priorPickCount < 0 ||
            !Enum.TryParse<PrototypeCardRarity>(
                suffix[(rarityOffset + rarityMarker.Length)..poolOffset],
                ignoreCase: true, out var rarity) ||
            !Enum.IsDefined(rarity))
            throw new InvalidDataException(
                "Controlled rest-heal card request has an invalid rarity owner.");

        var priorCardIds = ControlledRestHealPriorCardIds(
            continuation, priorPickCount);

        return QueryLegacyRewardCardModel(
            world.Act, groupOwnerId, rarity, priorCardIds,
            request.Ordinal);
    }

    private static ChanceLawV111 QueryControlledLegacyRestHealRarityTicket(
        RunState run,
        PrototypeControlledRunActionContinuationV111 continuation)
    {
        var action = continuation.RunAction ??
            throw new InvalidDataException(
                "Controlled rest-heal rarity query has no saved action.");
        if (!ControlledRunActionEngineV111.IsLegacyRestHealRewardAction(
                run, action) || run.World is not { } world)
            throw new InvalidDataException(
                "Controlled rest-heal rarity query is outside its legacy reward boundary.");

        var request = continuation.PendingRequest;
        var groupOwnerId = $"act:{world.Act}:reward_group:external";
        var groupMarker = groupOwnerId + ":card:";
        const string weightsMarker = ":weights:";
        if (!request.OwnerId.StartsWith(groupMarker,
                StringComparison.Ordinal))
            throw new InvalidDataException(
                "Controlled rest-heal rarity request has an invalid reward-group owner.");
        var suffix = request.OwnerId[groupMarker.Length..];
        var weightsOffset = suffix.IndexOf(weightsMarker,
            StringComparison.Ordinal);
        if (weightsOffset < 0 ||
            !int.TryParse(suffix[..weightsOffset],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var priorPickCount) || priorPickCount < 0)
            throw new InvalidDataException(
                "Controlled rest-heal rarity request has an invalid pick count.");

        var priorCardIds = ControlledRestHealPriorCardIds(
            continuation, priorPickCount);
        return QueryLegacyRewardCardRarityTicketIndices(
            world.Act, groupOwnerId, priorCardIds,
            request.Ordinal);
    }

    private static string[] ControlledRestHealPriorCardIds(
        PrototypeControlledRunActionContinuationV111 continuation,
        int priorPickCount)
    {
        var priorCardIds = new List<string>(priorPickCount);
        for (var index = continuation.Decisions.Length - 1;
             index >= 0 && priorCardIds.Count < priorPickCount; index--)
        {
            var decision = continuation.Decisions[index];
            if (decision.Request.SiteId != "reward.card_model.index_draw")
                continue;
            var labels = continuation.DecisionCandidateLabels[index];
            if ((uint)decision.SelectedIndex >= (uint)labels.Length)
                throw new InvalidDataException(
                    "Controlled rest-heal prefix has an invalid selected card index.");
            priorCardIds.Add(labels[decision.SelectedIndex]);
        }
        if (priorCardIds.Count != priorPickCount)
            throw new InvalidDataException(
                "Controlled rest-heal request is missing its prior card picks.");
        priorCardIds.Reverse();
        return priorCardIds.ToArray();
    }

    internal static int ControlledPotionSlotIndex(
        RandomIndexRequestV111 request)
    {
        const string ownerPrefix = "player:potion_slot:";
        var poolFingerprint = CanonicalJson.Sha256(
            PrototypeContent.PotionPool);
        var ownerSuffix = ":pool:" + poolFingerprint;
        if (!request.OwnerId.StartsWith(ownerPrefix,
                StringComparison.Ordinal) ||
            !request.OwnerId.EndsWith(ownerSuffix,
                StringComparison.Ordinal))
            throw new InvalidDataException(
                "Controlled potion-slot request has an invalid owner.");
        var slotText = request.OwnerId[ownerPrefix.Length..
            ^ownerSuffix.Length];
        if (!int.TryParse(slotText,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var slotIndex) || slotIndex < 0)
            throw new InvalidDataException(
                "Controlled potion-slot request has an invalid slot index.");
        return slotIndex;
    }

    /// <summary>Fingerprint the pending controlled action and request without
    /// hashing the enclosing RunState (which contains private provider state).</summary>
    public static string ControlledRunActionBoundaryFingerprint(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return ControlledRunActionPendingV111.Describe(run)
            .BoundaryFingerprint;
    }
}
