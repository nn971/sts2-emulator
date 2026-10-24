namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public static ChanceLawV111 QueryControlledRunAction(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.Stochastic.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        continuation.Validate();
        var boundaryFingerprint = ControlledRunActionBoundaryFingerprint(run);
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ControlledRunActionEngineV111.SchemaId,
            boundaryFingerprint
        });
        if (QueryControlledRunActionSemanticLaw(
                run, continuation, eventId) is { } semanticLaw)
            return semanticLaw;
        return QueryRandomIndex(eventId, continuation.PendingRequest);
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
                "reward.card_transform.replacement.index_draw" => "card:",
            "treasure.relic_model.index_draw" => "relic:",
            _ => "potion:"
        };
        var expectedStream = request.SiteId switch
        {
            "relic.acquisition.random_rare_card.model" or
                "reward.card_transform.replacement.index_draw" or
                "treasure.relic_model.index_draw" or
                "run.potion.fill_empty_slots.model" => "reward",
            _ => "event"
        };
        var outcomes = law.Outcomes;
        var numericSupportLabels = request.SiteId ==
            "treasure.relic_model.index_draw";
        var omittedTreasureLabels = numericSupportLabels &&
            continuation.PendingCandidateLabels.Length == 0 &&
            outcomes.Length >
                ControlledRunActionEngineV111.MaximumInlineOutcomeActions;
        var labelsMatch = numericSupportLabels
            ? omittedTreasureLabels ||
                continuation.PendingCandidateLabels.SequenceEqual(
                    Enumerable.Range(0, outcomes.Length).Select(index =>
                        index.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)),
                    StringComparer.Ordinal)
            : continuation.PendingCandidateLabels.Length == outcomes.Length &&
                outcomes.Select(outcome => outcome.OutcomeId)
                    .SequenceEqual(continuation.PendingCandidateLabels.Select(
                        label => outcomePrefix + label), StringComparer.Ordinal);
        if (request.SiteId != law.SiteId ||
            request.StreamId != expectedStream ||
            request.OperationKind != law.OperationKind ||
            request.OwnerId != law.OwnerId ||
            request.Ordinal != law.Ordinal ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, outcomes.Length)) ||
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
        var continuation = run.Stochastic.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        continuation.Validate();
        var boundary = new
        {
            schemaId = ControlledRunActionEngineV111.SchemaId,
            continuation.ActionId,
            continuation.MapNodeId,
            request = continuation.PendingRequest,
            candidateLabels = continuation.PendingCandidateLabels
        };
        return continuation.RunAction is null
            ? CanonicalJson.Sha256(boundary)
            : CanonicalJson.Sha256(new
            {
                boundary,
                actionFingerprint = PrototypeAiEnvironment.StableActionId(
                    continuation.RunAction)
            });
    }
}
