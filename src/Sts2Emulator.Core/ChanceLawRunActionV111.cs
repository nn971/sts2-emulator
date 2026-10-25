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
        if (QueryControlledRunActionSemanticLaw(
                run, continuation, pending.ChanceEventId) is { } semanticLaw)
            return semanticLaw;
        return QueryRandomIndex(pending.ChanceEventId, pending.Request);
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
            "treasure.relic_model.index_draw" || request.SiteId ==
                "reward.card_rarity.ticket" || request.SiteId ==
                "map.legacy.edge_extra_chance.ticket";
        var supportsOmittedLabels = numericSupportLabels ||
            request.SiteId == "reward.card_model.index_draw";
        var omittedLabels = supportsOmittedLabels &&
            continuation.PendingCandidateLabels.Length == 0 &&
            outcomes.Length >
                ControlledRunActionEngineV111.MaximumInlineOutcomeActions;
        var labelsMatch = numericSupportLabels
            ? omittedLabels ||
                continuation.PendingCandidateLabels.SequenceEqual(
                    Enumerable.Range(0, outcomes.Length).Select(index =>
                        index.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)),
                    StringComparer.Ordinal)
            : omittedLabels ||
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

    private static int ControlledPotionSlotIndex(
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
