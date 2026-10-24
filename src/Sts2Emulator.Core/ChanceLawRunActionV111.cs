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
        if (continuation.PendingRequest.SiteId ==
            "reward.card_transform.replacement.index_draw")
            return QueryControlledRelicDeckTransform(
                run, continuation, eventId);
        return QueryRandomIndex(eventId, continuation.PendingRequest);
    }

    private static ChanceLawV111 QueryControlledRelicDeckTransform(
        RunState run,
        PrototypeControlledRunActionContinuationV111 continuation,
        string eventId)
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
        var request = continuation.PendingRequest;
        var outcomes = law.Outcomes;
        if (request.SiteId != law.SiteId ||
            request.StreamId != "reward" ||
            request.OperationKind != law.OperationKind ||
            request.OwnerId != law.OwnerId ||
            request.Ordinal != law.Ordinal ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, outcomes.Length)) ||
            continuation.PendingCandidateLabels.Length != outcomes.Length ||
            !outcomes.Select(outcome => outcome.OutcomeId)
                .SequenceEqual(continuation.PendingCandidateLabels.Select(
                    label => "card:" + label), StringComparer.Ordinal))
            throw new InvalidDataException(
                "Controlled relic deck transform law differs from its saved support.");

        return law with { EventId = eventId };
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
