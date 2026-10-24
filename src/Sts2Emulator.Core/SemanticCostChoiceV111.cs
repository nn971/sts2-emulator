namespace Sts2Emulator.Core;

/// <summary>One opt-in source-timed random card-cost event.</summary>
public sealed record SemanticCostEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, RandomIndexRequestV111 Request,
    int[] CandidateCosts, string ProbabilityStatus = "UNVERIFIED",
    long? TargetCardInstanceId = null, string? TargetCardModelId = null)
    : ISemanticChanceEventV111
{
    private int[]? _candidateCosts = CloneOptional(CandidateCosts);

    public int[] CandidateCosts
    {
        get => CloneOptional(_candidateCosts)!;
        init => _candidateCosts = CloneOptional(value);
    }

    public const string EventSchemaId = SemanticCostChoiceV111.SchemaId;
    public string SiteId => Request.SiteId;
    public string OperationKind => Request.OperationKind;
    public string StreamId => Request.StreamId;
    public string OwnerId => Request.OwnerId;
    public int Ordinal => Request.Ordinal;

    public SemanticCostEventV111 Fork() => this with
    {
        Request = Request with
        {
            OrderedCandidates = (int[])Request.OrderedCandidates.Clone()
        },
        CandidateCosts = _candidateCosts!
    };

    public void Validate()
    {
        Request.Validate();
        var candidateCosts = _candidateCosts;
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            Request.OperationKind != "bounded_integer" ||
            candidateCosts is null || candidateCosts.Length == 0 ||
            candidateCosts.Distinct().Count() != candidateCosts.Length ||
            !candidateCosts.SequenceEqual(
                Enumerable.Range(0, candidateCosts.Length)) ||
            Request.SupportCount != candidateCosts.Length ||
            ProbabilityStatus != "UNVERIFIED" ||
            (TargetCardInstanceId is null) != (TargetCardModelId is null) ||
            (TargetCardInstanceId is not null &&
             (TargetCardInstanceId <= 0 ||
              string.IsNullOrWhiteSpace(TargetCardModelId) ||
              !((Request.SiteId ==
                     StochasticSiteRegistryV111.CombatRandomHandCardEnergyCostSiteId &&
                 (Request.StreamId ==
                      SemanticCostChoiceV111.LegacyHandCostStreamId ||
                  Request.StreamId ==
                      SemanticCostChoiceV111.NativeHandCostStreamId)) ||
                ((Request.SiteId is
                     StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId or
                     StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId) &&
                 Request.StreamId is "semantic.combat" or
                     SemanticCostChoiceV111.NativeHandCostStreamId)))))
            throw new InvalidDataException("Invalid semantic cost event.");
        var identityRequest = SemanticCostChoiceV111
            .RequestForEventIdentity(Request);
        var expectedSupportFingerprint = TargetCardInstanceId is null
            ? CanonicalJson.Sha256(new
                { request = identityRequest, costs = candidateCosts })
            : CanonicalJson.Sha256(new
            {
                request = identityRequest,
                costs = candidateCosts,
                targetCardInstanceId = TargetCardInstanceId,
                targetCardModelId = TargetCardModelId
            });
        if (SupportFingerprint != expectedSupportFingerprint)
            throw new InvalidDataException(
                "Semantic cost support fingerprint is inconsistent.");
    }

    private static int[]? CloneOptional(int[]? values) => values is null
        ? null
        : (int[])values.Clone();
}

public sealed record SemanticCostDecisionV111(
    SemanticCostEventV111 Event, int SelectedIndex, int SelectedCost)
{
    public SemanticCostDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 || SelectedIndex >= Event.CandidateCosts.Length ||
            Event.CandidateCosts[SelectedIndex] != SelectedCost)
            throw new InvalidDataException(
                "Semantic cost decision is outside its event support.");
    }
}

public static class SemanticCostChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-cost-choice-v1";
    internal const string LegacyHandCostStreamId = "prototype.combat";
    internal const string NativeHandCostStreamId =
        "run.combat_energy_costs";

    // Keep stream attribution out of this event's stable identity so old
    // prototype-labeled decisions can be replayed against Native-backed runs.
    internal static RandomIndexRequestV111 RequestForEventIdentity(
        RandomIndexRequestV111 request) =>
        request.SiteId == StochasticSiteRegistryV111.CombatRandomHandCardEnergyCostSiteId &&
        request.StreamId == NativeHandCostStreamId
            ? request with { StreamId = LegacyHandCostStreamId }
            : request;

    internal static SemanticCostEventV111 CreateEvent(
        string boundaryFingerprint, RandomIndexRequestV111 request,
        long? targetCardInstanceId = null, string? targetCardModelId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentNullException.ThrowIfNull(request);
        if ((targetCardInstanceId is null) != (targetCardModelId is null))
            throw new ArgumentException(
                "Semantic cost target identity must be fully specified.");
        request.Validate();
        if (request.SupportCount < 1 ||
            request.OperationKind != "bounded_integer")
            throw new InvalidDataException(
                "Semantic cost event has invalid bounded support.");

        var costs = Enumerable.Range(0, request.SupportCount).ToArray();
        var identityRequest = RequestForEventIdentity(request);
        var supportFingerprint = targetCardInstanceId is null
            ? CanonicalJson.Sha256(new
                { request = identityRequest, costs })
            : CanonicalJson.Sha256(new
            {
                request = identityRequest,
                costs,
                targetCardInstanceId,
                targetCardModelId
            });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticCostEventV111(
            SchemaId, eventId, boundaryFingerprint, supportFingerprint,
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, costs, TargetCardInstanceId: targetCardInstanceId,
            TargetCardModelId: targetCardModelId);
        semanticEvent.Validate();
        return semanticEvent;
    }

    internal static void ValidateDrawBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var draw = choice?.ControlledSemanticDraw;
        var cost = choice?.ControlledDrawCost;
        var deck = combat?.Stochastic.SemanticDeck;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is not null &&
             (choice is null ||
              !PrototypeGameEngine.IsSupportedAutomaticPipelineSemanticDrawContinuation(
                  combat, choice))) ||
            choice is not { ChoiceId: "controlled_draw_cost",
                ControlledSemanticDraw: not null,
                ControlledDrawCost: not null,
                OuterChoiceContinuation: not null } ||
            !choice.HasValidControlledPayloadShape() ||
            draw is null || cost is null || draw.RemainingCount < 0 ||
            draw.EventDepth < 0 ||
            draw.RemainingCount != cost.RemainingCount ||
            draw.FromHandDraw != cost.FromHandDraw ||
            draw.EventDepth != cost.EventDepth ||
            deck is not { Mode: SemanticDeckStateV111.NatureMode,
                Pending: null } ||
            !PrototypeGameEngine.ValidateControlledDrawCost(
                run.Player, combat, cost))
            throw new InvalidDataException(
                "Card-effect draw cost is not at a source-reachable Nature boundary.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Card-effect draw cost is not at a source-reachable Nature boundary.",
            "Card-effect draw cost diverged from its semantic pile state.");
        PrototypeStateInvariants.Validate(run);
    }

    internal static SemanticCostEventV111? DescribeEffectDrawEvent(
        RunState run, string boundaryFingerprint, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        if (run.World?.Combat?.PendingChoice is not
                { ChoiceId: "controlled_draw_cost",
                  ControlledDrawCost: { } drawCost,
                  ControlledSemanticDraw: not null })
            return null;
        ValidateDrawBoundary(run);
        var combat = run.World!.Combat!;
        var card = combat.Cards.Single(item =>
            item.InstanceId == drawCost.CardInstanceId);
        bool relic = drawCost.Stage == "relic";
        var request = new RandomIndexRequestV111(
            relic ? StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId
                  : StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId,
            "semantic.combat", "card_instance:" + card.InstanceId +
                ":model:" + card.CardId + ":stage:" + drawCost.Stage,
            ordinal, [], OperationKind: "bounded_integer",
            BoundedCount: relic ? drawCost.RelicMaxExclusive : 4);
        return CreateEvent(boundaryFingerprint, request,
            drawCost.CardInstanceId, card.CardId);
    }

    public static SemanticCostDecisionV111 Select(
        SemanticCostEventV111 pending, string eventId, int selectedCost)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Semantic cost event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateCosts, selectedCost);
        if (index < 0)
            throw new InvalidOperationException(
                "Cost value is outside the pending event support.");
        var decision = new SemanticCostDecisionV111(
            pending.Fork(), index, selectedCost);
        decision.Validate();
        return decision;
    }

    internal static void ValidateDecision(
        SemanticCostEventV111? pending,
        SemanticCostDecisionV111 decision, int sourceIndex)
    {
        ArgumentNullException.ThrowIfNull(decision);
        pending?.Validate();
        decision.Validate();
        bool legacyPrototypeStream = pending is not null &&
            pending.Request.SiteId ==
                StochasticSiteRegistryV111
                    .CombatRandomHandCardEnergyCostSiteId &&
            pending.Request.StreamId == NativeHandCostStreamId &&
            decision.Event.Request.StreamId == LegacyHandCostStreamId &&
            CanonicalJson.Sha256(pending with
            {
                Request = pending.Request with
                {
                    StreamId = decision.Event.Request.StreamId
                }
            }) == CanonicalJson.Sha256(decision.Event);
        if (pending is null || sourceIndex != decision.SelectedIndex ||
            (CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event) &&
             !legacyPrototypeStream))
            throw new InvalidOperationException(
                "Semantic cost decision is stale or outside current support.");
    }

    /// <summary>
    /// Resumes a cost inside a semantic card-effect draw, validates the next
    /// supported boundary, and preserves the caller's reward policy.
    /// </summary>
    internal static RunState ResumeEffectDrawCost(
        RunState checkpoint, int selectedCost,
        bool allowEngineOwnedPrototypeReward)
        => ResumeEffectDrawCostWithBoundary(
            checkpoint, selectedCost, allowEngineOwnedPrototypeReward).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeEffectDrawCostWithBoundary(
            RunState checkpoint, int selectedCost,
            bool allowEngineOwnedPrototypeReward)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ValidateDrawBoundary(checkpoint);
        var combat = checkpoint.World!.Combat!;
        var cost = combat.PendingChoice!.ControlledDrawCost!;
        int bound = cost.Stage == "relic" ? cost.RelicMaxExclusive : 4;
        if (selectedCost < 0 || selectedCost >= bound)
            throw new ArgumentOutOfRangeException(nameof(selectedCost));

        var resumed = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine
                .ResumeControlledSemanticEffectDrawCostWithBoundary(
                    checkpoint, selectedCost),
            allowEngineOwnedPrototypeReward);

        ValidateEffectDrawCostResult(resumed);
        return resumed;
    }

    private static void ValidateEffectDrawCostResult(
        PrototypeAutomaticPipelineResumeResultV111 resumed)
    {
        var run = resumed.Run;
        if (run.Phase == RunPhase.Combat)
        {
            SemanticPilotCombatEngineV111
                .ValidateSemanticCombatBoundary(run);
            PrototypeStateInvariants.Validate(run);
        }
        else if (!PrototypeGameEngine.IsAtCombatActionResultBoundary(run))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.card_draw.cost " +
                "operation=unsupported_post_cost_phase");
    }
}

/// <summary>
/// Resumes a controlled random-hand-cost effect and enforces the same
/// mechanics boundary for target selection and cost selection.
/// </summary>
internal static class SemanticRandomHandCostChoiceV111
{
    internal static SemanticCostEventV111 DescribeCostEvent(
        RunState run, string boundaryFingerprint, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var combat = run.World?.Combat ?? throw new InvalidDataException(
            "Random hand-cost event lost its source combat.");
        var randomCost = combat.PendingChoice?.ControlledRandomCost ??
            throw new InvalidDataException(
                "No random hand-cost choice is pending.");
        if (randomCost.Operation.Kind != PrototypeCombatEffectKind
                .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed)
            throw new InvalidDataException(
                "Random hand-cost event requires the cost-randomization operation.");
        ValidateBoundary(run);
        var maximumExclusive = Math.Max(1, randomCost.Operation.Amount);
        var targetCardInstanceId = randomCost.EligibleCardInstanceIds[
            randomCost.NextCardIndex];
        var targetCardModelId = combat.Cards.Single(card =>
            card.InstanceId == targetCardInstanceId).CardId;
        var request = new RandomIndexRequestV111(
            StochasticSiteRegistryV111.CombatRandomHandCardEnergyCostSiteId,
            combat.Stochastic.NativeRng is not null
                ? SemanticCostChoiceV111.NativeHandCostStreamId
                : SemanticCostChoiceV111.LegacyHandCostStreamId,
            "effect:" + randomCost.Operation.Kind + ":hand:" +
                CanonicalJson.Sha256(randomCost.EligibleCardInstanceIds) +
                ":index:" + randomCost.NextCardIndex,
            ordinal, [], OperationKind: "bounded_integer",
            BoundedCount: maximumExclusive);
        return SemanticCostChoiceV111.CreateEvent(
            boundaryFingerprint, request, targetCardInstanceId,
            targetCardModelId);
    }

    internal static void ValidateBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var randomCost = choice?.ControlledRandomCost;
        if (run.Phase != RunPhase.Combat ||
            combat is not { PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is null &&
             !combat.IsPlayerTurn) ||
            choice is not { ChoiceId: "controlled_random_hand_cost",
                OuterChoiceContinuation: not null } ||
            randomCost is null ||
            randomCost.Operation.Kind is not (
                PrototypeCombatEffectKind
                    .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed or
                PrototypeCombatEffectKind
                    .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed) ||
            !PrototypeGameEngine.ValidateControlledRandomCost(
                combat, randomCost))
            throw new InvalidDataException(
                "Random hand-cost continuation is not source-reachable.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Random hand-cost continuation lost the provider-owned deck.",
            "Random hand-cost continuation changed the hidden draw pile.");
        PrototypeStateInvariants.Validate(run);
    }

    internal static RunState Resume(
        RunState checkpoint, string siteId, int selectedIndex)
        => ResumeWithBoundary(checkpoint, siteId, selectedIndex).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeWithBoundary(
            RunState checkpoint, string siteId, int selectedIndex)
        => ResumeCore(checkpoint, siteId, savedRun =>
            PrototypeGameEngine.ResumeControlledRandomCostWithBoundary(
                savedRun, selectedIndex));

    internal static RunState ResumeSelectedHandCard(
        RunState checkpoint, long selectedCardInstanceId) =>
        ResumeSelectedHandCardWithBoundary(
            checkpoint, selectedCardInstanceId).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeSelectedHandCardWithBoundary(
            RunState checkpoint, long selectedCardInstanceId) =>
        ResumeCore(checkpoint, StochasticSiteRegistryV111.CombatRandomHandCardSelectSiteId,
            savedRun => PrototypeGameEngine
                .ResumeControlledRandomCostByCardWithBoundary(
                    savedRun, selectedCardInstanceId));

    private static PrototypeAutomaticPipelineResumeResultV111 ResumeCore(
        RunState checkpoint, string siteId,
        Func<RunState, PrototypeAutomaticPipelineResumeResultV111> resume)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(resume);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        var expectedOperation = siteId switch
        {
            StochasticSiteRegistryV111.CombatRandomHandCardSelectSiteId =>
                PrototypeCombatEffectKind
                    .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed,
            StochasticSiteRegistryV111.CombatRandomHandCardEnergyCostSiteId =>
                PrototypeCombatEffectKind
                    .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed,
            _ => throw new ArgumentOutOfRangeException(nameof(siteId))
        };
        var priorCombat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Random hand-cost choice lost its source combat.");
        if (priorCombat.PendingChoice?.ControlledRandomCost is not
                { } pending || pending.Operation.Kind != expectedOperation)
            throw new InvalidDataException(
                "Random hand-cost choice does not match its source operation.");
        bool automaticHandDrawChild = PrototypeGameEngine
            .IsAutomaticHandDrawHookRandomHandCostContinuation(
                priorCombat, priorCombat.PendingChoice!);
        ValidateBoundary(checkpoint);

        var resumed = ControlledChanceGuardV111.ExecuteGuarded(
            () => resume(checkpoint.Fork()));
        var nextCombat = resumed.Run.World?.Combat;
        if (resumed.Run.Phase != RunPhase.Combat || nextCombat is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=" + siteId +
                " operation=unsupported_post_cost_phase");
        if (nextCombat.PendingChoice?.ControlledRandomCost is not null)
            ValidateBoundary(resumed.Run);
        else if (automaticHandDrawChild)
            SemanticCardSelectionEngineV111
                .ValidateAutomaticHandDrawHookResult(resumed.Run);
        else
            SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(
                resumed.Run);
        SemanticDrawPileAdapterV111.RequireUnchangedDrawPile(
            priorCombat, nextCombat,
            "UNSUPPORTED_SEMANTIC_DECK site=" + siteId +
            " operation=unmodeled_draw_pile_mutation");
        return resumed;
    }
}
