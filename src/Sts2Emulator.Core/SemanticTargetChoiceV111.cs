namespace Sts2Emulator.Core;

/// <summary>A live random-enemy support at the source effect boundary.</summary>
public sealed record SemanticTargetEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    int[] CandidateEnemyInstanceIds, string SupportFingerprint,
    string ProbabilityStatus = "UNVERIFIED", string TargetKind = "effect_random_enemy")
    : ISemanticChanceEventV111
{
    private int[]? _candidateEnemyInstanceIds =
        CloneOptional(CandidateEnemyInstanceIds);

    public int[] CandidateEnemyInstanceIds
    {
        get => CloneOptional(_candidateEnemyInstanceIds)!;
        init => _candidateEnemyInstanceIds = CloneOptional(value);
    }

    public SemanticTargetEventV111 Fork() => this with
    {
        CandidateEnemyInstanceIds = _candidateEnemyInstanceIds!
    };

    public void Validate()
    {
        var candidateEnemyInstanceIds = _candidateEnemyInstanceIds;
        var expectedSiteId =
            SemanticTargetChoiceV111.SiteIdForTargetKind(TargetKind);
        if (SchemaId != SemanticTargetChoiceV111.SchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            expectedSiteId is null || SiteId != expectedSiteId ||
            StreamId != "run.combat_targets" ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_enemy_instance" ||
            candidateEnemyInstanceIds is not { Length: > 0 } ||
            candidateEnemyInstanceIds.Distinct().Count() !=
                candidateEnemyInstanceIds.Length ||
            SupportFingerprint != CanonicalJson.Sha256(
                candidateEnemyInstanceIds) ||
            ProbabilityStatus != "UNVERIFIED" ||
            TargetKind is not ("effect_random_enemy" or "sly_auto_play" or
                "autoplay_random_enemy"))
            throw new InvalidDataException("Invalid semantic target event.");
    }

    private static int[]? CloneOptional(int[]? values) => values is null
        ? null
        : (int[])values.Clone();
}

/// <summary>Typed result for one random target selection.</summary>
public sealed record SemanticTargetDecisionV111(
    SemanticTargetEventV111 Event, int SelectedIndex,
    int SelectedEnemyInstanceId)
{
    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateEnemyInstanceIds.Length ||
            Event.CandidateEnemyInstanceIds[SelectedIndex] !=
                SelectedEnemyInstanceId)
            throw new InvalidDataException(
                "Invalid semantic target event or outcome.");
    }

    public SemanticTargetDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };
}

/// <summary>Native provider result paired with its ordered enemy support.</summary>
public sealed record SemanticTargetNativeAuditV111(
    string SchemaId, SemanticTargetDecisionV111 Outcome,
    RandomIndexDecisionV111 ProviderDecision)
{
    public const string CurrentSchemaId =
        "sts2-v111-native-random-target-audit-v1";

    private static string BoundaryFingerprintFor(
        RandomIndexDecisionV111 providerDecision) => CanonicalJson.Sha256(new
    {
        schemaId = CurrentSchemaId,
        request = providerDecision.Request,
        nativeCursorBefore = providerDecision.NativeCursorBefore
    });

    public SemanticTargetNativeAuditV111 Fork() => this with
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

    public static SemanticTargetNativeAuditV111 Create(
        RandomIndexDecisionV111 providerDecision)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        providerDecision.Validate();
        var request = providerDecision.Request;
        var targetKind = SemanticTargetChoiceV111.TargetKindForSiteId(
            request.SiteId);
        if (targetKind is null ||
            request.StreamId != "run.combat_targets")
            throw new InvalidDataException(
                "Native random-target audit has an unsupported provider request.");

        var selectedEnemyInstanceId = NativeSemanticOutcomeV111
            .SelectByChooseIndex(providerDecision, request.OrderedCandidates);

        var semanticEvent = SemanticTargetChoiceV111.CreateEvent(
            BoundaryFingerprintFor(providerDecision), targetKind, request.OwnerId,
            request.Ordinal, request.OrderedCandidates);
        var outcome = SemanticTargetChoiceV111.Select(
            semanticEvent, semanticEvent.EventId,
            selectedEnemyInstanceId);
        var audit = new SemanticTargetNativeAuditV111(
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
        var targetKind = SemanticTargetChoiceV111.TargetKindForSiteId(
            request.SiteId);
        var expectedEvent = targetKind is null
            ? null
            : SemanticTargetChoiceV111.CreateEvent(
                BoundaryFingerprintFor(ProviderDecision), targetKind,
                request.OwnerId, request.Ordinal,
                request.OrderedCandidates);
        if (SchemaId != CurrentSchemaId ||
            ProviderDecision.ProviderMode != "native-v111" ||
            targetKind is null || request.StreamId != "run.combat_targets" ||
            request.OperationKind != "choose_index" ||
            expectedEvent is null ||
            CanonicalJson.Sha256(Outcome.Event) !=
                CanonicalJson.Sha256(expectedEvent) ||
            Outcome.SelectedIndex != ProviderDecision.SelectedIndex ||
            Outcome.SelectedEnemyInstanceId !=
                request.OrderedCandidates[ProviderDecision.SelectedIndex])
            throw new InvalidDataException(
                "Native random-target audit differs from its provider request or result.");
    }
}

public static class SemanticTargetChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-target-choice-v1";

    internal static string? TargetKindForSiteId(string? siteId) =>
        siteId switch
        {
            StochasticSiteRegistryV111.CombatEffectRandomTargetSiteId =>
                "effect_random_enemy",
            StochasticSiteRegistryV111.CombatSlyAutoplayRandomTargetSiteId =>
                "sly_auto_play",
            StochasticSiteRegistryV111.CombatAutoplayRandomTargetSiteId =>
                "autoplay_random_enemy",
            _ => null
        };

    internal static string? SiteIdForTargetKind(string? targetKind) =>
        targetKind switch
        {
            "effect_random_enemy" =>
                StochasticSiteRegistryV111.CombatEffectRandomTargetSiteId,
            "sly_auto_play" =>
                StochasticSiteRegistryV111.CombatSlyAutoplayRandomTargetSiteId,
            "autoplay_random_enemy" =>
                StochasticSiteRegistryV111.CombatAutoplayRandomTargetSiteId,
            _ => null
        };

    internal static SemanticTargetEventV111 CreateEvent(
        string boundaryFingerprint, string targetKind, string ownerId,
        int ordinal, int[] candidateEnemyInstanceIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(candidateEnemyInstanceIds);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var siteId = SiteIdForTargetKind(targetKind) ??
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_TARGET kind=" + targetKind);
        var candidates = (int[])candidateEnemyInstanceIds.Clone();
        var supportFingerprint = CanonicalJson.Sha256(candidates);
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SiteId = siteId,
            OwnerId = ownerId,
            Ordinal = ordinal,
            SupportFingerprint = supportFingerprint,
            TargetKind = targetKind
        });
        var semanticEvent = new SemanticTargetEventV111(
            SchemaId, eventId, siteId, "run.combat_targets", ownerId,
            ordinal, "choose_enemy_instance", candidates,
            supportFingerprint, TargetKind: targetKind);
        semanticEvent.Validate();
        return semanticEvent;
    }

    internal static void ValidateBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var target = choice?.ControlledTarget;
        var playerActionBoundary = combat is
            { IsPlayerTurn: true, AutomaticPipelineContinuation: null };
        var automaticPipelineBoundary = combat is { } pipelineCombat &&
            PrototypeGameEngine
                .IsAtSupportedAutomaticPipelineTargetBoundary(pipelineCombat);
        if (run.Phase != RunPhase.Combat ||
            (!playerActionBoundary && !automaticPipelineBoundary) ||
            choice is not { ChoiceId: "controlled_random_target",
                OuterChoiceContinuation: not null } ||
            target is null ||
            target.Kind is not ("effect_random_enemy" or "sly_auto_play" or
                "autoplay_random_enemy"))
            throw new InvalidDataException(
                "Semantic target continuation is not at a supported player or automatic pipeline boundary.");
        var liveCombat = combat ?? throw new InvalidDataException(
            "Semantic target continuation is missing its combat state.");
        if (choice is null || !HasValidLiveSupport(liveCombat, choice))
            throw new InvalidDataException(
                "Semantic target support or source disagrees with the live continuation.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            liveCombat,
            "Semantic target continuation changed the hidden draw pile.",
            "Semantic target continuation changed the hidden draw pile.",
            allowMissing: true);
        if (liveCombat.Stochastic.SemanticDeck is null &&
            liveCombat.DrawPile.Length != 0)
            throw new InvalidDataException(
                "A semantic target without a provider deck must have an empty draw pile.");
    }

    /// <summary>
    /// Validates the ordered live enemy support and its source-card identity
    /// against the suspended combat continuation shared by target consumers.
    /// </summary>
    internal static bool HasValidLiveSupport(
        CombatState combat, PendingCombatChoiceState choice)
    {
        var target = choice.ControlledTarget;
        if (choice.ChoiceId != "controlled_random_target" ||
            choice.OuterChoiceContinuation is not { } continuation ||
            choice.SourceCardInstanceId != continuation.SourceCardInstanceId ||
            choice.SourceCardDestination !=
                continuation.SourceCardDestination ||
            target is not { Kind: "effect_random_enemy" or
                "sly_auto_play" or "autoplay_random_enemy" } ||
            target.EligibleEnemyInstanceIds is not { Length: > 0 } support ||
            support.Distinct().Count() != support.Length)
            return false;

        var eligible = combat.Enemies.Where(enemy => enemy.Hp > 0 &&
                (target.Kind != "sly_auto_play" || !enemy.Escaped))
            .Select(enemy => enemy.InstanceId).ToArray();
        if (eligible.Length == 0 || !eligible.SequenceEqual(support))
            return false;

        return target.Kind switch
        {
            "sly_auto_play" =>
                target.SlyCardInstanceId is { } slyCardId &&
                target.AutoPlayCardInstanceId is null &&
                PrototypeGameEngine.IsLiveSlyAutoplaySource(
                    combat, slyCardId),
            "autoplay_random_enemy" =>
                target.AutoPlayCardInstanceId is { } autoPlayCardId &&
                combat.PlayCardIds.Contains(autoPlayCardId) &&
                target.SlyCardInstanceId is null &&
                continuation.Operations is { Length: > 0 } operations &&
                operations[0].Kind ==
                    PrototypeCombatEffectKind.AutoPlayCombatCard &&
                operations[0].CardInstanceId == autoPlayCardId &&
                operations[0].TargetEnemyId is null,
            "effect_random_enemy" =>
                target.SlyCardInstanceId is null &&
                target.AutoPlayCardInstanceId is null &&
                continuation.Operations is { Length: > 0 } effectOperations &&
                effectOperations[0].TargetMode ==
                    PrototypeEffectTarget.ActionTargetEnemy &&
                effectOperations[0].TargetEnemyId is null,
            _ => false
        };
    }

    public static SemanticTargetDecisionV111 Select(
        SemanticTargetEventV111 pending, string eventId,
        int enemyInstanceId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Semantic target event ID is stale or mismatched.");
        var index = Array.IndexOf(
            pending.CandidateEnemyInstanceIds, enemyInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Enemy instance is outside the pending target support.");
        var decision = new SemanticTargetDecisionV111(
            pending, index, enemyInstanceId);
        decision.Validate();
        return decision;
    }

    /// <summary>
    /// Applies one stable-identity target choice through the ordinary
    /// controlled combat continuation and validates either its supported
    /// nested semantic draw or the unchanged hidden draw-pile boundary.
    /// </summary>
    internal static RunState Resume(
        RunState checkpoint, SemanticTargetDecisionV111 decision)
        => ResumeWithBoundary(checkpoint, decision).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeWithBoundary(
            RunState checkpoint, SemanticTargetDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        ValidateBoundary(checkpoint);

        var resumed = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine
                .ResumeControlledRandomTargetWithBoundary(
                    checkpoint.Fork(), decision.SelectedEnemyInstanceId),
            allowEngineOwnedPrototypeReward: true);
        if (resumed.Run.Phase == RunPhase.Combat)
        {
            var priorCombat = checkpoint.World?.Combat ??
                throw new InvalidDataException(
                    "Automatic target lost its source combat.");
            var nextCombat = resumed.Run.World?.Combat ??
                throw new InvalidDataException(
                    "Automatic target continuation lost combat.");
            if (nextCombat.PendingChoice is
                { ChoiceId: "controlled_semantic_draw",
                  ControlledSemanticDraw: not null })
                SemanticDrawChoiceV111.ValidateBoundary(resumed.Run);
            else if (nextCombat.PendingChoice is
                { ChoiceId: "controlled_draw_cost",
                  ControlledSemanticDraw: not null,
                  ControlledDrawCost: not null })
                SemanticCostChoiceV111.ValidateDrawBoundary(resumed.Run);
            else
                SemanticDrawPileAdapterV111.RequireUnchangedDrawPile(
                    priorCombat, nextCombat,
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.random_target " +
                    "operation=unmodeled_draw_pile_mutation");
        }
        else if (!PrototypeGameEngine
                     .IsAtCombatActionResultBoundary(resumed.Run))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.random_target " +
                "operation=unsupported_post_target_phase");
        return resumed;
    }
}
