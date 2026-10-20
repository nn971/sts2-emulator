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
            StreamId != "prototype.combat" ||
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

public static class SemanticPotionGenerationChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-potion-generation-v1";
    public const string SiteId = "combat.potion_generation.random_model";

    internal static RandomIndexRequestV111 CreateRequest(
        string ownerId, int ordinal, int supportCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (supportCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(supportCount));

        var request = new RandomIndexRequestV111(
            SiteId, "prototype.combat", ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray(),
            OperationKind: "choose_index");
        request.Validate();
        return request;
    }

    internal static SemanticPotionGenerationEventV111 CreateEvent(
        string boundaryFingerprint, string ownerId, int ordinal,
        string[] candidatePotionIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(candidatePotionIds);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var candidates = (string[])candidatePotionIds.Clone();
        var request = CreateRequest(ownerId, ordinal, candidates.Length);
        var supportFingerprint = CanonicalJson.Sha256(candidates);
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
        var ownerId = "effect:" + generation.Operation.Kind + ":source:" +
            (choice!.SourceCardInstanceId?.ToString() ?? "none") + ":pool:" +
            CanonicalJson.Sha256(generation.CandidatePotionIds);
        return CreateEvent(
            boundaryFingerprint, ownerId, ordinal,
            generation.CandidatePotionIds);
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
        if (pending is null || selectedPotionId != decision.SelectedPotionId ||
            CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event))
            throw new InvalidDataException(
                "Potion-generation decision no longer matches the pending event.");
    }

    /// <summary>Resume the source generation effect under the shared engine
    /// continuation, preserving the hidden draw-pile state.</summary>
    internal static RunState Resume(
        RunState checkpoint,
        SemanticPotionGenerationDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Potion-generation choice lost its source combat.");
        var generation = combat.PendingChoice?.ControlledPotionGeneration;
        if (generation is null ||
            !generation.CandidatePotionIds.SequenceEqual(
                decision.Event.CandidatePotionIds))
            throw new InvalidDataException(
                "Potion-generation decision no longer matches its source pool.");
        ValidateBoundary(checkpoint);

        RunState resumed;
        using (ControlledChanceGuardV111.Enter())
            resumed = PrototypeGameEngine.ResumeControlledPotionGeneration(
                checkpoint.Fork(), decision.SelectedIndex);
        if (resumed.Phase != RunPhase.Combat)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_POTION site=combat.potion_generation " +
                "operation=unsupported_post_generation_phase");
        SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(resumed);
        var nextCombat = resumed.World?.Combat ??
            throw new InvalidDataException(
                "Potion-generation continuation lost its resulting combat.");
        SemanticDrawPileAdapterV111.RequireUnchangedDrawPile(
            combat, nextCombat,
            "UNSUPPORTED_SEMANTIC_DECK site=combat.potion_generation " +
            "operation=unmodeled_draw_pile_mutation");
        return resumed;
    }
}
