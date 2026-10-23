namespace Sts2Emulator.Core;

/// <summary>Ordered replacement pool for one selected combat-card transform.</summary>
public sealed record SemanticCardTransformEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    long SourceCardInstanceId, string OriginalCardId,
    string[] CandidateCardIds, string SupportFingerprint,
    string ProbabilityStatus = "UNVERIFIED") : ISemanticChanceEventV111
{
    private string[]? _candidateCardIds = CloneOptional(CandidateCardIds);

    public string[] CandidateCardIds
    {
        get => CloneOptional(_candidateCardIds)!;
        init => _candidateCardIds = CloneOptional(value);
    }

    public const string EventSchemaId = SemanticCardTransformChoiceV111.SchemaId;

    public SemanticCardTransformEventV111 Fork() => this with
    {
        CandidateCardIds = _candidateCardIds!
    };

    public void Validate()
    {
        var candidateCardIds = _candidateCardIds;
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            SiteId != SemanticCardTransformChoiceV111.SiteId ||
            (StreamId != SemanticCardTransformChoiceV111.StreamId &&
             StreamId != SemanticCardTransformChoiceV111.NativeStreamId) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_card_model" ||
            SourceCardInstanceId <= 0 || string.IsNullOrWhiteSpace(OriginalCardId) ||
            candidateCardIds is not { Length: > 0 } ||
            candidateCardIds.Any(string.IsNullOrWhiteSpace) ||
            candidateCardIds.Distinct(StringComparer.Ordinal).Count() !=
                candidateCardIds.Length ||
            SupportFingerprint != CanonicalJson.Sha256(candidateCardIds) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic card-transform event.");
    }

    private static string[]? CloneOptional(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}

public sealed record SemanticCardTransformDecisionV111(
    SemanticCardTransformEventV111 Event, int SelectedIndex,
    string SelectedCardId)
{
    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateCardIds.Length ||
            Event.CandidateCardIds[SelectedIndex] != SelectedCardId)
            throw new InvalidDataException(
                "Card-transform outcome is outside its ordered source pool.");
    }

    public SemanticCardTransformDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };
}

public static class SemanticCardTransformChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-card-transform-v1";
    public const string SiteId =
        StochasticSiteRegistryV111.CombatCardTransformReplacementSiteId;
    internal const string StreamId = "prototype.combat";
    internal const string NativeStreamId = "run.combat_card_generation";

    internal static RandomIndexRequestV111 CreateRequest(
        string ownerId, int ordinal, int supportCount,
        string streamId = StreamId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (supportCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (streamId is not (StreamId or NativeStreamId))
            throw new NotSupportedException(
                "UNSUPPORTED_RANDOM_SITE site=" + SiteId +
                " operation=card_transform_stream.");

        var request = new RandomIndexRequestV111(
            SiteId, streamId, ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray(),
            OperationKind: "choose_index");
        request.Validate();
        return request;
    }

    internal static SemanticCardTransformEventV111 CreateEvent(
        string boundaryFingerprint, int ordinal, long sourceCardInstanceId,
        string originalCardId, string[] candidateCardIds,
        string streamId = StreamId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalCardId);
        ArgumentNullException.ThrowIfNull(candidateCardIds);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (sourceCardInstanceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceCardInstanceId));

        var candidates = (string[])candidateCardIds.Clone();
        var supportFingerprint = CanonicalJson.Sha256(candidates);
        var ownerId = "card_instance:" + sourceCardInstanceId + ":model:" +
            originalCardId + ":pool:" + supportFingerprint;
        var request = CreateRequest(
            ownerId, ordinal, candidates.Length, streamId);
        // Keep existing decision IDs stable across the stream-label upgrade.
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SiteId = request.SiteId,
            OwnerId = request.OwnerId,
            Ordinal = request.Ordinal,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticCardTransformEventV111(
            SchemaId, eventId, request.SiteId, request.StreamId,
            request.OwnerId, request.Ordinal,
            "choose_card_model", sourceCardInstanceId, originalCardId,
            candidates, supportFingerprint);
        semanticEvent.Validate();
        return semanticEvent;
    }

    internal static SemanticCardTransformEventV111 DescribePending(
        RunState run, string boundaryFingerprint, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        var combat = run.World?.Combat ?? throw new InvalidDataException(
            "Card-transform choice lost its source combat.");
        var transform = combat.PendingChoice?.ControlledCardTransform ??
            throw new InvalidDataException(
                "No controlled card transform is pending.");
        ValidateBoundary(run, transform);
        var sourceId = transform.SelectedCardInstanceIds[
            transform.NextTransformIndex];
        var originalCardId = combat.Cards.Single(card =>
            card.InstanceId == sourceId).CardId;
        return CreateEvent(boundaryFingerprint, ordinal, sourceId,
            originalCardId, transform.CandidateCardIds,
            combat.Stochastic.NativeRng is not null
                ? NativeStreamId : StreamId);
    }

    internal static void ValidateBoundary(
        RunState run, PrototypeControlledCardTransformV111 transform)
    {
        var combat = run.World?.Combat;
        if (run.Phase != RunPhase.Combat || combat is null ||
            combat.PendingChoice is not
                { ChoiceId: "controlled_card_transform" } ||
            !PrototypeGameEngine.ValidateControlledCardTransform(
                combat, transform))
            throw new InvalidDataException(
                "Card-transform pool is not source-reachable.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Card-transform continuation diverged from its semantic draw pile.",
            "Card-transform continuation diverged from its semantic draw pile.",
            allowMissing: true);
    }

    public static SemanticCardTransformDecisionV111 Select(
        SemanticCardTransformEventV111 pending, string eventId,
        string cardId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Card-transform event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateCardIds, cardId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card model is outside the pending transform support.");
        var decision = new SemanticCardTransformDecisionV111(
            pending.Fork(), index, cardId);
        decision.Validate();
        return decision;
    }

    internal static void ValidateDecision(
        SemanticCardTransformEventV111? pending,
        SemanticCardTransformDecisionV111 decision, string cardId)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        decision.Validate();
        bool legacyPrototypeStream = pending is not null &&
            pending.StreamId == NativeStreamId &&
            decision.Event.StreamId == StreamId &&
            CanonicalJson.Sha256(pending with
            {
                StreamId = decision.Event.StreamId
            }) == CanonicalJson.Sha256(decision.Event);
        if (pending is null || pending.EventId != decision.Event.EventId ||
            (CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event) &&
             !legacyPrototypeStream) ||
            decision.SelectedCardId != cardId)
            throw new InvalidDataException(
                "Card-transform decision differs from its live semantic event.");
    }

    /// <summary>Revalidate the live replacement pool and apply one outcome
    /// through the ordinary controlled transform continuation.</summary>
    internal static RunState Resume(
        RunState checkpoint, SemanticCardTransformDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Card-transform choice lost its source combat.");
        var transform = combat.PendingChoice?.ControlledCardTransform ??
            throw new InvalidDataException(
                "Card-transform choice lost its suspended operation.");
        if (decision.Event.StreamId == NativeStreamId &&
            combat.Stochastic.NativeRng is null)
            throw new InvalidDataException(
                "Native card-transform stream attribution has no Native run RNG owner.");
        ValidateBoundary(checkpoint, transform);
        var sourceCardInstanceId = transform.SelectedCardInstanceIds[
            transform.NextTransformIndex];
        var sourceCard = combat.Cards.Single(card =>
            card.InstanceId == sourceCardInstanceId);
        if (decision.Event.SourceCardInstanceId != sourceCardInstanceId ||
            decision.Event.OriginalCardId != sourceCard.CardId ||
            !transform.CandidateCardIds.SequenceEqual(
                decision.Event.CandidateCardIds))
            throw new InvalidDataException(
                "Card-transform decision no longer matches its source pool.");

        RunState resumed;
        using (ControlledChanceGuardV111.Enter())
            resumed = PrototypeGameEngine.ResumeControlledCardTransform(
                checkpoint.Fork(), decision.SelectedCardId);
        if (resumed.Phase == RunPhase.Combat)
            SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(
                resumed);
        else if (!PrototypeGameEngine
                     .IsAtCombatRewardOrTerminalBoundary(resumed))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_TRANSFORM " +
                "site=combat.card_transform " +
                "operation=unsupported_post_transform_phase");
        return resumed;
    }
}
