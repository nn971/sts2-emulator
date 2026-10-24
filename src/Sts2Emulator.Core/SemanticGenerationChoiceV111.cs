namespace Sts2Emulator.Core;

/// <summary>An ordered generated-card model pool at its source selection boundary.</summary>
public sealed record SemanticGeneratedCardEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    string[] CandidateCardIds, string SupportFingerprint,
    string ProbabilityStatus = "UNVERIFIED")
    : ISemanticChanceEventV111
{
    private string[]? _candidateCardIds = CloneOptional(CandidateCardIds);

    public string[] CandidateCardIds
    {
        get => CloneOptional(_candidateCardIds)!;
        init => _candidateCardIds = CloneOptional(value);
    }

    public SemanticGeneratedCardEventV111 Fork() => this with
    {
        CandidateCardIds = _candidateCardIds!
    };

    public void Validate()
    {
        var candidateCardIds = _candidateCardIds;
        bool supportedCombatSite = SiteId is
            StochasticSiteRegistryV111.CombatGeneratedCardSelectSiteId or
            StochasticSiteRegistryV111.CombatRandomDrawAttackCardModelSiteId;
        bool validStream = supportedCombatSite
            ? StreamId is "prototype.combat" or
                "run.combat_card_generation"
            : SiteId == StochasticSiteRegistryV111.NativeRewardCardModelSiteId &&
                StreamId == NativePlayerRngV111.RewardsStreamId;
        if (SchemaId != SemanticGenerationChoiceV111.SchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            (!supportedCombatSite && SiteId !=
                StochasticSiteRegistryV111.NativeRewardCardModelSiteId) ||
            !validStream ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_card_model" ||
            candidateCardIds is not { Length: > 0 } ||
            candidateCardIds.Any(string.IsNullOrWhiteSpace) ||
            candidateCardIds.Distinct(StringComparer.Ordinal).Count() !=
                candidateCardIds.Length ||
            SupportFingerprint != CanonicalJson.Sha256(candidateCardIds) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic generated-card event.");
    }

    private static string[]? CloneOptional(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}

/// <summary>Typed result for one without-replacement generated-card pick.</summary>
public sealed record SemanticGeneratedCardDecisionV111(
    SemanticGeneratedCardEventV111 Event, int SelectedIndex,
    string SelectedCardId)
{
    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateCardIds.Length ||
            Event.CandidateCardIds[SelectedIndex] != SelectedCardId)
            throw new InvalidDataException(
                "Generated-card outcome is outside its ordered source pool.");
    }

    public SemanticGeneratedCardDecisionV111 Fork() => this with
    {
        Event = Event with
        {
            CandidateCardIds = (string[])Event.CandidateCardIds.Clone()
        }
    };
}

/// <summary>Pairs a Native generated-card model result with the exact
/// primitive selection ticket and its source operation.</summary>
public sealed record SemanticGeneratedCardNativeAuditV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SourceOperationKind, long? SourceCardInstanceId,
    SemanticGeneratedCardDecisionV111 Outcome,
    RandomIndexDecisionV111 ProviderDecision)
{
    public const string CurrentSchemaId =
        "sts2-v111-semantic-generated-card-native-audit-v1";
    internal const string NativeCombatRewardOperation =
        "NativeCombatRewardCardOffer";

    internal static bool IsSupportedAuditSite(string siteId) =>
        siteId is StochasticSiteRegistryV111.CombatGeneratedCardSelectSiteId or
            StochasticSiteRegistryV111.CombatRandomDrawAttackCardModelSiteId or
            StochasticSiteRegistryV111.NativeRewardCardModelSiteId;

    public SemanticGeneratedCardNativeAuditV111 Fork() => this with
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

    public static SemanticGeneratedCardNativeAuditV111 Create(
        RandomIndexDecisionV111 providerDecision,
        IReadOnlyList<string> candidateCardIds,
        PrototypeCombatEffectKind sourceOperationKind,
        long? sourceCardInstanceId) => CreateCore(
            providerDecision, candidateCardIds,
            sourceOperationKind.ToString(), sourceCardInstanceId);

    internal static SemanticGeneratedCardNativeAuditV111 CreateNativeReward(
        RandomIndexDecisionV111 providerDecision,
        IReadOnlyList<string> candidateCardIds) => CreateCore(
            providerDecision, candidateCardIds,
            NativeCombatRewardOperation, null);

    private static SemanticGeneratedCardNativeAuditV111 CreateCore(
        RandomIndexDecisionV111 providerDecision,
        IReadOnlyList<string> candidateCardIds,
        string operation, long? sourceCardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        ArgumentNullException.ThrowIfNull(candidateCardIds);
        providerDecision.Validate();
        var request = providerDecision.Request;
        var candidates = candidateCardIds.ToArray();
        if (providerDecision.ProviderMode != "native-v111" ||
            !IsSupportedAuditSite(request.SiteId) ||
            !OwnerIdMatches(operation, sourceCardInstanceId,
                request, candidates) ||
            request.OperationKind != "choose_index" ||
            request.SupportCount != candidates.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)) ||
            candidates.Length == 0 ||
            candidates.Any(string.IsNullOrWhiteSpace) ||
            candidates.Distinct(StringComparer.Ordinal).Count() !=
                candidates.Length ||
            providerDecision.SelectedIndex < 0 ||
            providerDecision.SelectedIndex >= candidates.Length)
            throw new InvalidDataException(
                "Native generated-card audit has an unsupported request or model support.");

        var boundaryFingerprint = BoundaryFingerprintFor(
            request, operation, sourceCardInstanceId);
        var semanticEvent = operation == NativeCombatRewardOperation
            ? SemanticGenerationChoiceV111.CreateNativeRewardEvent(
                boundaryFingerprint, request, candidates)
            : SemanticGenerationChoiceV111.CreateEvent(
                boundaryFingerprint, request.SiteId, request.OwnerId,
                request.Ordinal, candidates, request.StreamId);
        var semanticOutcome = SemanticGenerationChoiceV111.Select(
            semanticEvent, semanticEvent.EventId,
            candidates[providerDecision.SelectedIndex]);
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            semanticOutcome,
            providerDecision
        });
        var audit = new SemanticGeneratedCardNativeAuditV111(
            CurrentSchemaId, eventId, boundaryFingerprint, operation,
            sourceCardInstanceId, semanticOutcome, providerDecision with
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
        var expectedBoundaryFingerprint = BoundaryFingerprintFor(
            request, SourceOperationKind, SourceCardInstanceId);
        var ownerMatches = OwnerIdMatches(
            SourceOperationKind, SourceCardInstanceId, request,
            semanticEvent.CandidateCardIds);
        var expectedEvent = SourceOperationKind ==
            NativeCombatRewardOperation
                ? SemanticGenerationChoiceV111.CreateNativeRewardEvent(
                    expectedBoundaryFingerprint, request,
                    semanticEvent.CandidateCardIds)
                : SemanticGenerationChoiceV111.CreateEvent(
                    expectedBoundaryFingerprint, request.SiteId,
                    request.OwnerId, request.Ordinal,
                    semanticEvent.CandidateCardIds, request.StreamId);
        if (SchemaId != CurrentSchemaId ||
            ProviderDecision.ProviderMode != "native-v111" ||
            !IsSupportedAuditSite(request.SiteId) ||
            request.OperationKind != "choose_index" ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0,
                    semanticEvent.CandidateCardIds.Length)) ||
            request.SupportCount != semanticEvent.CandidateCardIds.Length ||
            !ownerMatches ||
            semanticEvent.SiteId != request.SiteId ||
            semanticEvent.StreamId != request.StreamId ||
            semanticEvent.OwnerId != request.OwnerId ||
            semanticEvent.Ordinal != request.Ordinal ||
            string.IsNullOrWhiteSpace(SourceOperationKind) ||
            ProviderDecision.SelectedIndex < 0 ||
            ProviderDecision.SelectedIndex >=
                semanticEvent.CandidateCardIds.Length ||
            Outcome.SelectedIndex != ProviderDecision.SelectedIndex ||
            Outcome.SelectedCardId != semanticEvent.CandidateCardIds[
                ProviderDecision.SelectedIndex] ||
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
                "Invalid Native generated-card model audit outcome.");
    }

    private static string BoundaryFingerprintFor(
        RandomIndexRequestV111 request, string sourceOperationKind,
        long? sourceCardInstanceId) => CanonicalJson.Sha256(new
        {
            schemaId = CurrentSchemaId,
            request,
            sourceOperationKind,
            sourceCardInstanceId
        });

    private static string OwnerIdFor(string sourceOperationKind,
        long? sourceCardInstanceId, IReadOnlyList<string> candidates) =>
        (sourceCardInstanceId is { } sourceCardId
            ? "card_instance:" + sourceCardId
            : "effect:" + sourceOperationKind) + ":pool:" +
        CanonicalJson.Sha256(candidates);

    private static bool OwnerIdMatches(string sourceOperationKind,
        long? sourceCardInstanceId, RandomIndexRequestV111 request,
        IReadOnlyList<string> candidates)
    {
        if (sourceOperationKind != NativeCombatRewardOperation)
            return request.SiteId !=
                    StochasticSiteRegistryV111.NativeRewardCardModelSiteId &&
                request.OwnerId == OwnerIdFor(
                    sourceOperationKind, sourceCardInstanceId, candidates);

        if (sourceCardInstanceId is not null || request.SiteId !=
                StochasticSiteRegistryV111.NativeRewardCardModelSiteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId)
            return false;

        var suffix = ":pool:" + CanonicalJson.Sha256(candidates);
        if (!request.OwnerId.EndsWith(suffix, StringComparison.Ordinal))
            return false;
        var prefix = request.OwnerId[..^suffix.Length].Split(':');
        return prefix.Length == 8 && prefix[0] == "act" &&
            int.TryParse(prefix[1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var act) && act >= 1 && prefix[2] == "room" &&
            (prefix[3] is "Combat" or "Elite" or "Boss") &&
            prefix[4] == "pick" &&
            int.TryParse(prefix[5],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var pick) && pick >= 0 && prefix[6] == "rarity" &&
            (prefix[7] is "Common" or "Uncommon" or "Rare");
    }
}

public static class SemanticGenerationChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-generation-choice-v1";

    internal static RandomIndexRequestV111 CreateRequest(
        string siteId, string ownerId, int ordinal, int supportCount,
        string streamId = "prototype.combat")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (siteId is not ("combat.generated_card.select" or
            "combat.random_draw_attack.card_model"))
            throw new NotSupportedException(
                "UNSUPPORTED_RANDOM_SITE site=" + siteId +
                " operation=generated_card_model.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (supportCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (streamId != "prototype.combat" &&
            !((siteId is "combat.generated_card.select" or
                  "combat.random_draw_attack.card_model") &&
              streamId == "run.combat_card_generation"))
            throw new NotSupportedException(
                "UNSUPPORTED_RANDOM_SITE site=" + siteId +
                " operation=generated_card_model_stream.");

        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray(),
            OperationKind: "choose_index");
        request.Validate();
        return request;
    }

    internal static SemanticGeneratedCardEventV111 CreateEvent(
        string boundaryFingerprint, string siteId, string ownerId,
        int ordinal, string[] candidateCardIds,
        string streamId = "prototype.combat")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(candidateCardIds);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var candidates = (string[])candidateCardIds.Clone();
        var request = CreateRequest(
            siteId, ownerId, ordinal, candidates.Length, streamId);
        return CreateEventCore(boundaryFingerprint, request, candidates);
    }

    internal static SemanticGeneratedCardEventV111 CreateNativeRewardEvent(
        string boundaryFingerprint, RandomIndexRequestV111 request,
        string[] candidateCardIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateCardIds);
        request.ValidateForNativeProvider();
        var candidates = (string[])candidateCardIds.Clone();
        if (request.SiteId !=
                StochasticSiteRegistryV111.NativeRewardCardModelSiteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            request.OperationKind != "choose_index" ||
            request.SupportCount != candidates.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)) ||
            candidates.Length == 0 ||
            candidates.Any(string.IsNullOrWhiteSpace) ||
            candidates.Distinct(StringComparer.Ordinal).Count() !=
                candidates.Length)
            throw new InvalidDataException(
                "Native reward-card model event has an unsupported request or pool.");
        return CreateEventCore(boundaryFingerprint, request, candidates);
    }

    private static SemanticGeneratedCardEventV111 CreateEventCore(
        string boundaryFingerprint, RandomIndexRequestV111 request,
        string[] candidates)
    {
        var supportFingerprint = CanonicalJson.Sha256(candidates);
        // Stream attribution is a projection of this source boundary. Keep
        // it out of EventId so existing prototype-labeled decisions replay.
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SiteId = request.SiteId,
            OwnerId = request.OwnerId,
            Ordinal = request.Ordinal,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticGeneratedCardEventV111(
            SchemaId, eventId, request.SiteId, request.StreamId,
            request.OwnerId, request.Ordinal, "choose_card_model",
            candidates, supportFingerprint);
        semanticEvent.Validate();
        return semanticEvent;
    }

    internal static SemanticGeneratedCardEventV111 DescribePending(
        RunState run, string boundaryFingerprint, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var choice = run.World?.Combat?.PendingChoice;
        var generation = choice?.ControlledGeneration ??
            throw new InvalidDataException(
                "Generated-card request has no controlled generation choice.");
        ValidateBoundary(run);
        var combat = run.World?.Combat;
        var streamId = combat?.Stochastic.NativeRng is not null
            ? "run.combat_card_generation" : "prototype.combat";
        var ownerId = choice!.SourceCardInstanceId is { } sourceCardId
            ? "card_instance:" + sourceCardId
            : "effect:" + generation.Operation.Kind;
        return CreateEvent(
            boundaryFingerprint, "combat.generated_card.select", ownerId,
            ordinal, generation.CandidateCardIds, streamId);
    }

    internal static void ValidateBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var generation = choice?.ControlledGeneration;
        if (run.Phase != RunPhase.Combat ||
            combat is not { PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is null &&
             !combat.IsPlayerTurn) ||
            choice is not { ChoiceId: "controlled_generated_card",
                OuterChoiceContinuation: not null } ||
            generation is null ||
            !PrototypeGameEngine.ValidateControlledGenerationSupport(generation))
            throw new InvalidDataException(
                "Semantic generated-card pool is not source-reachable.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Generated-card continuation lost its provider-owned deck.",
            "Generated-card continuation changed the hidden draw pile.");
        PrototypeStateInvariants.Validate(run);
    }

    public static SemanticGeneratedCardDecisionV111 Select(
        SemanticGeneratedCardEventV111 pending, string eventId,
        string cardId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Generated-card event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateCardIds, cardId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card model is outside the pending generated-card support.");
        var decision = new SemanticGeneratedCardDecisionV111(
            pending.Fork(), index, cardId);
        decision.Validate();
        return decision;
    }

    internal static void ValidateDecision(
        SemanticGeneratedCardEventV111? pending,
        SemanticGeneratedCardDecisionV111 decision, string selectedCardId)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedCardId);
        pending?.Validate();
        decision.Validate();
        bool legacyPrototypeStream = pending is not null &&
            (pending.SiteId is "combat.generated_card.select" or
                "combat.random_draw_attack.card_model") &&
            pending.StreamId == "run.combat_card_generation" &&
            decision.Event.StreamId == "prototype.combat" &&
            CanonicalJson.Sha256(pending with
            {
                StreamId = decision.Event.StreamId
            }) == CanonicalJson.Sha256(decision.Event);
        if (pending is null || selectedCardId != decision.SelectedCardId ||
            (CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event) &&
             !legacyPrototypeStream))
            throw new InvalidDataException(
                "Generated-card decision no longer matches the pending event.");
    }

    /// <summary>
    /// Revalidates the source pool and resumes either ordinary card
    /// generation or the card-model stage of draw-pile insertion.
    /// </summary>
    internal static RunState Resume(
        RunState checkpoint, SemanticGeneratedCardDecisionV111 decision)
        => ResumeWithBoundary(checkpoint, decision).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeWithBoundary(
            RunState checkpoint, SemanticGeneratedCardDecisionV111 decision,
            bool deferAutomaticHandDrawContinuation = false)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();

        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Generated-card choice lost its source combat.");
        var choice = combat.PendingChoice;
        var insertion = choice?.ControlledDrawInsertion;
        bool drawInsertionModelChoice = decision.Event.SiteId ==
            "combat.random_draw_attack.card_model";
        bool crossbowTurnStartContinuation = !drawInsertionModelChoice &&
            choice is not null &&
            PrototypeGameEngine.IsCrossbowTurnStartGenerationContinuation(
                combat, choice);
        bool crossbowTurnStartHandoff = crossbowTurnStartContinuation &&
            combat.Turn >= 2;
        bool randomDrawAttackModelHandoff = drawInsertionModelChoice &&
            choice is
            {
                ChoiceId: "controlled_draw_insertion",
                ControlledDrawInsertion:
                {
                    Stage: "card_model",
                    Operation.Kind:
                        PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile
                }
            };
        bool automaticHandDrawGenerationHandoff =
            deferAutomaticHandDrawContinuation &&
            !drawInsertionModelChoice &&
            choice is not null &&
            PrototypeGameEngine
                .IsAutomaticHandDrawGeneratedCardContinuation(
                    combat, choice);
        bool generatedCardModelHandoff = !drawInsertionModelChoice &&
            choice is
            {
                ChoiceId: "controlled_generated_card",
                ControlledGeneration: not null
            };
        bool nativeGenerationStreamHandoff =
            combat.Stochastic.NativeRng?.RunStreams is not null &&
            (generatedCardModelHandoff || randomDrawAttackModelHandoff);
        if (decision.Event.StreamId == "run.combat_card_generation" &&
            !nativeGenerationStreamHandoff)
            throw new InvalidDataException(
                "Native card-generation stream attribution is outside its supported source continuation.");
        if (drawInsertionModelChoice)
        {
            if (choice is not { ChoiceId: "controlled_draw_insertion" } ||
                insertion is not { Stage: "card_model" } insertionChoice ||
                !insertionChoice.CandidateCardIds.SequenceEqual(
                    decision.Event.CandidateCardIds))
                throw new InvalidDataException(
                    "Generated-card decision no longer matches its insertion pool.");
            SemanticDrawInsertionChoiceV111.ValidateBoundary(checkpoint);
        }
        else
        {
            if (decision.Event.SiteId != "combat.generated_card.select" ||
                choice is not { ChoiceId: "controlled_generated_card" } ||
                choice.ControlledGeneration is not { } generation ||
                !generation.CandidateCardIds.SequenceEqual(
                    decision.Event.CandidateCardIds))
                throw new InvalidDataException(
                    "Generated-card decision no longer matches its source pool.");
            ValidateBoundary(checkpoint);
        }

        var resumed = ControlledChanceGuardV111.ExecuteGuarded(() =>
        {
            if (drawInsertionModelChoice)
                return PrototypeGameEngine
                    .ResumeControlledDrawInsertionByCardModelWithBoundary(
                        checkpoint.Fork(), decision.SelectedCardId);
            else if (crossbowTurnStartHandoff)
                return PrototypeGameEngine
                    .ResumeControlledGeneratedCardForSemanticHandoffWithBoundary(
                        checkpoint.Fork(), decision.SelectedCardId);
            else if (automaticHandDrawGenerationHandoff)
                return PrototypeGameEngine
                    .ResumeControlledGeneratedCardForSemanticHandDrawWithBoundary(
                        checkpoint.Fork(), decision.SelectedCardId);
            return PrototypeGameEngine
                .ResumeControlledGeneratedCardWithBoundary(
                    checkpoint.Fork(), decision.SelectedCardId);
        }, allowEngineOwnedPrototypeReward: true);
        if (resumed.Run.Phase == RunPhase.Combat)
        {
            if (!automaticHandDrawGenerationHandoff)
                SemanticPilotCombatEngineV111
                    .ValidateSemanticCombatBoundary(resumed.Run);
            var nextCombat = resumed.Run.World?.Combat ??
                throw new InvalidDataException(
                    "Generated-card continuation lost its resulting combat.");
            SemanticDrawPileAdapterV111.RequireUnchangedDrawPile(
                combat, nextCombat,
                "UNSUPPORTED_SEMANTIC_DECK site=combat.generated_card " +
                "operation=unmodeled_draw_pile_mutation");
        }
        else if (!PrototypeGameEngine
                     .IsAtCombatActionResultBoundary(resumed.Run))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.generated_card " +
                "operation=unsupported_post_generation_phase");
        return resumed;
    }
}
