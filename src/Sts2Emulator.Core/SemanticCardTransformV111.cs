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

/// <summary>Pairs a Native card-transform replacement with its exact
/// primitive selection ticket.</summary>
public sealed record SemanticCardTransformNativeAuditV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SourceOperationKind, SemanticCardTransformDecisionV111 Outcome,
    RandomIndexDecisionV111 ProviderDecision)
{
    public const string CurrentSchemaId =
        "sts2-v111-semantic-card-transform-native-audit-v1";

    public SemanticCardTransformNativeAuditV111 Fork() => this with
    {
        Outcome = Outcome.Fork(),
        ProviderDecision = ProviderDecision with
        {
            Request = ProviderDecision.Request with
            {
                OrderedCandidates = (int[])ProviderDecision.Request
                    .OrderedCandidates.Clone()
            }
        }
    };

    public static SemanticCardTransformNativeAuditV111 Create(
        RandomIndexDecisionV111 providerDecision,
        IReadOnlyList<string> candidateCardIds,
        PrototypeCombatEffectKind sourceOperationKind,
        long sourceCardInstanceId, string originalCardId)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        ArgumentNullException.ThrowIfNull(candidateCardIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalCardId);
        providerDecision.Validate();
        var request = providerDecision.Request;
        var candidates = candidateCardIds.ToArray();
        var operation = sourceOperationKind.ToString();
        var supportFingerprint = CanonicalJson.Sha256(candidates);
        var expectedProviderOwner = "card_instance:" +
            sourceCardInstanceId + ":pool:" + supportFingerprint;
        if (request.SiteId != SemanticCardTransformChoiceV111.SiteId ||
            request.StreamId != SemanticCardTransformChoiceV111.NativeStreamId ||
            request.OwnerId != expectedProviderOwner ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)) ||
            sourceCardInstanceId <= 0 || candidates.Length == 0 ||
            candidates.Any(string.IsNullOrWhiteSpace) ||
            candidates.Distinct(StringComparer.Ordinal).Count() !=
                candidates.Length)
            throw new InvalidDataException(
                "Native card-transform audit has an unsupported request or replacement pool.");

        var selectedCardId = NativeSemanticOutcomeV111
            .SelectByChooseIndex(providerDecision, candidates);

        var boundaryFingerprint = BoundaryFingerprintFor(
            request, operation, sourceCardInstanceId, originalCardId);
        var semanticEvent = SemanticCardTransformChoiceV111.CreateEvent(
            boundaryFingerprint, request.Ordinal, sourceCardInstanceId,
            originalCardId, candidates, request.StreamId);
        var semanticOutcome = SemanticCardTransformChoiceV111.Select(
            semanticEvent, semanticEvent.EventId,
            selectedCardId);
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            semanticOutcome,
            providerDecision
        });
        var audit = new SemanticCardTransformNativeAuditV111(
            CurrentSchemaId, eventId, boundaryFingerprint, operation,
            semanticOutcome, providerDecision with
            {
                Request = request with
                {
                    OrderedCandidates = (int[])request.OrderedCandidates
                        .Clone()
                }
            });
        audit.Validate();
        return audit;
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Outcome);
        ArgumentNullException.ThrowIfNull(ProviderDecision);
        Outcome.Validate();
        ProviderDecision.Validate();
        var semanticEvent = Outcome.Event;
        var request = ProviderDecision.Request;
        var candidates = semanticEvent.CandidateCardIds;
        var expectedBoundaryFingerprint = BoundaryFingerprintFor(
            request, SourceOperationKind,
            semanticEvent.SourceCardInstanceId,
            semanticEvent.OriginalCardId);
        var expectedProviderOwner = "card_instance:" +
            semanticEvent.SourceCardInstanceId + ":pool:" +
            CanonicalJson.Sha256(candidates);
        var expectedEvent = SemanticCardTransformChoiceV111.CreateEvent(
            expectedBoundaryFingerprint, request.Ordinal,
            semanticEvent.SourceCardInstanceId,
            semanticEvent.OriginalCardId, candidates, request.StreamId);
        if (SchemaId != CurrentSchemaId ||
            ProviderDecision.ProviderMode != "native-v111" ||
            request.SiteId != SemanticCardTransformChoiceV111.SiteId ||
            request.StreamId != SemanticCardTransformChoiceV111.NativeStreamId ||
            request.OwnerId != expectedProviderOwner ||
            request.OperationKind != "choose_index" ||
            request.SupportCount != candidates.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)) ||
            string.IsNullOrWhiteSpace(SourceOperationKind) ||
            ProviderDecision.SelectedIndex < 0 ||
            ProviderDecision.SelectedIndex >= candidates.Length ||
            Outcome.SelectedIndex != ProviderDecision.SelectedIndex ||
            Outcome.SelectedCardId != candidates[
                ProviderDecision.SelectedIndex] ||
            semanticEvent.Ordinal != request.Ordinal ||
            semanticEvent.StreamId != request.StreamId ||
            CanonicalJson.Sha256(Outcome.Event) !=
                CanonicalJson.Sha256(expectedEvent) ||
            BoundaryFingerprint != expectedBoundaryFingerprint ||
            EventId != CanonicalJson.Sha256(new
            {
                schemaId = SchemaId,
                semanticOutcome = Outcome,
                providerDecision = ProviderDecision
            }))
            throw new InvalidDataException(
                "Invalid Native card-transform audit outcome.");
    }

    private static string BoundaryFingerprintFor(
        RandomIndexRequestV111 request, string sourceOperationKind,
        long sourceCardInstanceId, string originalCardId) =>
        CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            request,
            sourceOperationKind,
            sourceCardInstanceId,
            originalCardId
        });
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
        => ResumeWithBoundary(checkpoint, decision).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeWithBoundary(
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
        bool automaticHandDrawChild =
            PrototypeGameEngine
                .IsAutomaticHandDrawHookCardTransformContinuation(
                    combat, combat.PendingChoice!);
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

        var resumed = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine
                .ResumeControlledCardTransformWithBoundary(
                    checkpoint.Fork(), decision.SelectedCardId));
        if (resumed.Run.Phase == RunPhase.Combat)
        {
            if (automaticHandDrawChild)
                SemanticCardSelectionEngineV111
                    .ValidateAutomaticHandDrawHookResult(resumed.Run);
            else
                SemanticPilotCombatEngineV111
                    .ValidateSemanticCombatBoundary(resumed.Run);
        }
        else if (!PrototypeGameEngine
                     .IsAtCombatRewardOrTerminalBoundary(resumed.Run))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_TRANSFORM " +
                "site=combat.card_transform " +
                "operation=unsupported_post_transform_phase");
        return resumed;
    }
}
