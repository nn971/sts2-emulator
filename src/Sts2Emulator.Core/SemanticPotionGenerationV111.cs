namespace Sts2Emulator.Core;

/// <summary>Ordered source pool for one random combat-potion model choice.</summary>
public sealed record SemanticPotionGenerationEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    string[] CandidatePotionIds, string SupportFingerprint,
    string ProbabilityStatus = "UNVERIFIED") : ISemanticChanceEventV111
{
    private string[]? _candidatePotionIds = CloneOptional(CandidatePotionIds);

    public string[] CandidatePotionIds
    {
        get => CloneOptional(_candidatePotionIds)!;
        init => _candidatePotionIds = CloneOptional(value);
    }

    public SemanticPotionGenerationEventV111 Fork() => this with
    {
        CandidatePotionIds = _candidatePotionIds!
    };

    public void Validate()
    {
        var candidatePotionIds = _candidatePotionIds;
        if (SchemaId != SemanticPotionGenerationChoiceV111.SchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            SiteId != SemanticPotionGenerationChoiceV111.SiteId ||
            (StreamId != SemanticPotionGenerationChoiceV111.StreamId &&
             StreamId != SemanticPotionGenerationChoiceV111.NativeStreamId) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_potion_model" ||
            candidatePotionIds is not { Length: > 0 } ||
            candidatePotionIds.Any(string.IsNullOrWhiteSpace) ||
            candidatePotionIds.Distinct(StringComparer.Ordinal).Count() !=
                candidatePotionIds.Length ||
            SupportFingerprint != CanonicalJson.Sha256(candidatePotionIds) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic potion-generation event.");
    }

    private static string[]? CloneOptional(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}

public sealed record SemanticPotionGenerationDecisionV111(
    SemanticPotionGenerationEventV111 Event, int SelectedIndex,
    string SelectedPotionId)
{
    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidatePotionIds.Length ||
            Event.CandidatePotionIds[SelectedIndex] != SelectedPotionId)
            throw new InvalidDataException(
                "Potion-generation outcome is outside its ordered source pool.");
    }

    public SemanticPotionGenerationDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };
}

/// <summary>Native combat potion model result paired with its exact ticket.</summary>
public sealed record SemanticPotionGenerationNativeAuditV111(
    string SchemaId, SemanticPotionGenerationDecisionV111 Outcome,
    RandomIndexDecisionV111 ProviderDecision)
{
    public const string CurrentSchemaId =
        "sts2-v111-native-potion-generation-audit-v1";

    private static string BoundaryFingerprintFor(
        RandomIndexDecisionV111 providerDecision) => CanonicalJson.Sha256(new
    {
        schemaId = CurrentSchemaId,
        request = providerDecision.Request,
        nativeCursorBefore = providerDecision.NativeCursorBefore
    });

    public SemanticPotionGenerationNativeAuditV111 Fork() => this with
    {
        Outcome = Outcome.Fork(),
        ProviderDecision = ProviderDecision with
        {
            Request = ProviderDecision.Request with
            {
                OrderedCandidates = ProviderDecision.Request.OrderedCandidates
            }
        }
    };

    public static SemanticPotionGenerationNativeAuditV111 Create(
        RandomIndexDecisionV111 providerDecision,
        IReadOnlyList<string> orderedCandidatePotionIds)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        ArgumentNullException.ThrowIfNull(orderedCandidatePotionIds);
        providerDecision.Validate();
        var request = providerDecision.Request;
        var candidates = orderedCandidatePotionIds.ToArray();
        if (providerDecision.ProviderMode != "native-v111" ||
            request.SiteId != SemanticPotionGenerationChoiceV111.SiteId ||
            request.StreamId != SemanticPotionGenerationChoiceV111.NativeStreamId ||
            request.OperationKind != "choose_index" ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)))
            throw new InvalidDataException(
                "Native potion-generation audit has an unsupported provider request.");

        var semanticEvent = SemanticPotionGenerationChoiceV111.CreateEvent(
            BoundaryFingerprintFor(providerDecision), request.OwnerId,
            request.Ordinal, candidates, request.StreamId);
        var outcome = SemanticPotionGenerationChoiceV111.Select(
            semanticEvent, semanticEvent.EventId,
            candidates[providerDecision.SelectedIndex]);
        var audit = new SemanticPotionGenerationNativeAuditV111(
            CurrentSchemaId, outcome, providerDecision with
            {
                Request = request with
                {
                    OrderedCandidates = request.OrderedCandidates
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
        var request = ProviderDecision.Request;
        var candidateIds = Outcome.Event.CandidatePotionIds;
        var expectedEvent = SemanticPotionGenerationChoiceV111.CreateEvent(
            BoundaryFingerprintFor(ProviderDecision), request.OwnerId,
            request.Ordinal, candidateIds, request.StreamId);
        if (SchemaId != CurrentSchemaId ||
            ProviderDecision.ProviderMode != "native-v111" ||
            request.SiteId != SemanticPotionGenerationChoiceV111.SiteId ||
            request.StreamId != SemanticPotionGenerationChoiceV111.NativeStreamId ||
            request.OperationKind != "choose_index" ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidateIds.Length)) ||
            CanonicalJson.Sha256(Outcome.Event) !=
                CanonicalJson.Sha256(expectedEvent) ||
            Outcome.SelectedIndex != ProviderDecision.SelectedIndex ||
            Outcome.SelectedPotionId !=
                candidateIds[ProviderDecision.SelectedIndex])
            throw new InvalidDataException(
                "Native potion-generation audit differs from its provider request or result.");
    }
}

public static class SemanticPotionGenerationChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-potion-generation-v1";
    public const string SiteId =
        StochasticSiteRegistryV111.CombatPotionGenerationRandomModelSiteId;
    internal const string StreamId = "prototype.combat";
    internal const string NativeStreamId = "run.combat_potion_generation";

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
                " operation=potion_generation_stream.");

        var request = new RandomIndexRequestV111(
            SiteId, streamId, ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray(),
            OperationKind: "choose_index");
        request.Validate();
        return request;
    }

    internal static SemanticPotionGenerationEventV111 CreateEvent(
        string boundaryFingerprint, string ownerId, int ordinal,
        string[] candidatePotionIds, string streamId = StreamId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(candidatePotionIds);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var candidates = (string[])candidatePotionIds.Clone();
        var request = CreateRequest(
            ownerId, ordinal, candidates.Length, streamId);
        var supportFingerprint = CanonicalJson.Sha256(candidates);
        // Keep saved event IDs stable across stream-label attribution.
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SiteId = request.SiteId,
            OwnerId = request.OwnerId,
            Ordinal = request.Ordinal,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticPotionGenerationEventV111(
            SchemaId, eventId, request.SiteId, request.StreamId,
            request.OwnerId, request.Ordinal, "choose_potion_model",
            candidates, supportFingerprint);
        semanticEvent.Validate();
        return semanticEvent;
    }

    internal static SemanticPotionGenerationEventV111 DescribePending(
        RunState run, string boundaryFingerprint, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var choice = run.World?.Combat?.PendingChoice;
        var generation = choice?.ControlledPotionGeneration ??
            throw new InvalidDataException(
                "Potion-generation request has no controlled choice.");
        ValidateBoundary(run);
        var combat = run.World?.Combat;
        var streamId = combat?.Stochastic.NativeRng is not null
            ? NativeStreamId : StreamId;
        var ownerId = "effect:" + generation.Operation.Kind + ":source:" +
            (choice!.SourceCardInstanceId?.ToString() ?? "none") + ":pool:" +
            CanonicalJson.Sha256(generation.CandidatePotionIds);
        return CreateEvent(
            boundaryFingerprint, ownerId, ordinal,
            generation.CandidatePotionIds, streamId);
    }

    internal static void ValidateBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var generation = choice?.ControlledPotionGeneration;
        if (run.Phase != RunPhase.Combat ||
            combat is not { PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is null &&
             !combat.IsPlayerTurn) ||
            choice is not { ChoiceId: "controlled_potion_generation",
                OuterChoiceContinuation: not null } ||
            generation is null ||
            !PrototypeGameEngine.ValidateControlledPotionGeneration(
                combat, generation, choice.SourceCardInstanceId))
            throw new InvalidDataException(
                "Potion generation is not source-reachable.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Potion generation lost the provider-owned semantic deck.",
            "Potion generation changed the hidden draw pile.");
        PrototypeStateInvariants.Validate(run);
    }

    public static SemanticPotionGenerationDecisionV111 Select(
        SemanticPotionGenerationEventV111 pending, string eventId,
        string potionId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(potionId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Potion-generation event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidatePotionIds, potionId);
        if (index < 0)
            throw new InvalidOperationException(
                "Potion model is outside the pending source pool.");
        var decision = new SemanticPotionGenerationDecisionV111(
            pending.Fork(), index, potionId);
        decision.Validate();
        return decision;
    }

    internal static void ValidateDecision(
        SemanticPotionGenerationEventV111? pending,
        SemanticPotionGenerationDecisionV111 decision, string selectedPotionId)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPotionId);
        pending?.Validate();
        decision.Validate();
        bool legacyPrototypeStream = pending is not null &&
            pending.StreamId == NativeStreamId &&
            decision.Event.StreamId == StreamId &&
            CanonicalJson.Sha256(pending with
            {
                StreamId = decision.Event.StreamId
            }) == CanonicalJson.Sha256(decision.Event);
        if (pending is null || selectedPotionId != decision.SelectedPotionId ||
            (CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event) &&
             !legacyPrototypeStream))
            throw new InvalidDataException(
                "Potion-generation decision no longer matches the pending event.");
    }

    /// <summary>Resume the source generation effect under the shared engine
    /// continuation, preserving the hidden draw-pile state.</summary>
    internal static RunState Resume(
        RunState checkpoint,
        SemanticPotionGenerationDecisionV111 decision)
        => ResumeWithBoundary(checkpoint, decision).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeWithBoundary(
            RunState checkpoint,
            SemanticPotionGenerationDecisionV111 decision,
            bool deferSemanticHandDrawContinuation = false)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Potion-generation choice lost its source combat.");
        var generation = combat.PendingChoice?.ControlledPotionGeneration;
        if (decision.Event.StreamId == NativeStreamId &&
            combat.Stochastic.NativeRng is null)
            throw new InvalidDataException(
                "Native potion-generation stream attribution has no Native combat RNG owner.");
        if (generation is null ||
            !generation.CandidatePotionIds.SequenceEqual(
                decision.Event.CandidatePotionIds))
            throw new InvalidDataException(
                "Potion-generation decision no longer matches its source pool.");
        bool automaticHandDrawPotionHandoff =
            deferSemanticHandDrawContinuation &&
            PrototypeGameEngine
                .IsAutomaticHandDrawPotionGenerationContinuation(
                    combat, combat.PendingChoice!);
        if (deferSemanticHandDrawContinuation &&
            !automaticHandDrawPotionHandoff)
            throw new InvalidDataException(
                "Potion-generation decision is outside the semantic hand-draw continuation.");
        ValidateBoundary(checkpoint);

        var resumed = ControlledChanceGuardV111.ExecuteGuarded(
            () => deferSemanticHandDrawContinuation
                ? PrototypeGameEngine
                    .ResumeControlledPotionGenerationForSemanticHandDrawWithBoundary(
                        checkpoint.Fork(), decision.SelectedPotionId)
                : PrototypeGameEngine
                    .ResumeControlledPotionGenerationWithBoundary(
                        checkpoint.Fork(), decision.SelectedPotionId));
        if (resumed.Run.Phase != RunPhase.Combat)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_POTION site=combat.potion_generation " +
                "operation=unsupported_post_generation_phase");
        if (!automaticHandDrawPotionHandoff)
            SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(
                resumed.Run);
        var nextCombat = resumed.Run.World?.Combat ??
            throw new InvalidDataException(
                "Potion-generation continuation lost its resulting combat.");
        SemanticDrawPileAdapterV111.RequireUnchangedDrawPile(
            combat, nextCombat,
            "UNSUPPORTED_SEMANTIC_DECK site=combat.potion_generation " +
            "operation=unmodeled_draw_pile_mutation");
        return resumed;
    }
}
