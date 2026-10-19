namespace Sts2Emulator.Core;

/// <summary>One opt-in source-timed random card-cost event.</summary>
public sealed record SemanticCostEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, RandomIndexRequestV111 Request,
    int[] CandidateCosts, string ProbabilityStatus = "UNVERIFIED",
    long? TargetCardInstanceId = null, string? TargetCardModelId = null)
    : ISemanticChanceEventV111
{
    public const string EventSchemaId = "sts2-v111-semantic-cost-choice-v1";
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
        CandidateCosts = (int[])CandidateCosts.Clone()
    };

    public void Validate()
    {
        Request.Validate();
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            Request.OperationKind != "bounded_integer" ||
            CandidateCosts is null || CandidateCosts.Length == 0 ||
            CandidateCosts.Distinct().Count() != CandidateCosts.Length ||
            !CandidateCosts.SequenceEqual(
                Enumerable.Range(0, CandidateCosts.Length)) ||
            Request.SupportCount != CandidateCosts.Length ||
            ProbabilityStatus != "UNVERIFIED" ||
            (TargetCardInstanceId is null) != (TargetCardModelId is null) ||
            (TargetCardInstanceId is not null &&
             (TargetCardInstanceId <= 0 ||
              string.IsNullOrWhiteSpace(TargetCardModelId) ||
              !((Request.SiteId ==
                     "combat.random_hand_card.energy_cost" &&
                 Request.StreamId == "prototype.combat") ||
                ((Request.SiteId is
                     "combat.card_draw.relic_energy_cost" or
                     "combat.card_draw.slither_energy_cost") &&
                 Request.StreamId == "semantic.combat")))))
            throw new InvalidDataException("Invalid semantic cost event.");
        var expectedSupportFingerprint = TargetCardInstanceId is null
            ? CanonicalJson.Sha256(new
                { request = Request, costs = CandidateCosts })
            : CanonicalJson.Sha256(new
            {
                request = Request,
                costs = CandidateCosts,
                targetCardInstanceId = TargetCardInstanceId,
                targetCardModelId = TargetCardModelId
            });
        if (SupportFingerprint != expectedSupportFingerprint)
            throw new InvalidDataException(
                "Semantic cost support fingerprint is inconsistent.");
    }
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
    internal static void ValidateDrawBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var draw = choice?.ControlledSemanticDraw;
        var cost = choice?.ControlledDrawCost;
        var deck = combat?.SemanticDeck;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is not null &&
             (choice is null ||
              !PrototypeGameEngine.IsMayhemSemanticDrawContinuation(
                  combat, choice))) ||
            choice is not { ChoiceId: "controlled_draw_cost",
                ControlledSemanticDraw: not null,
                ControlledDrawCost: not null,
                OuterChoiceContinuation: not null,
                ControlledTarget: null, ControlledGeneration: null,
                ControlledDrawInsertion: null, ControlledPoolShuffle: null,
                ControlledRandomCost: null, ControlledCardShortlist: null,
                ControlledRandomPileCard: null } ||
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
            relic ? "combat.card_draw.relic_energy_cost"
                  : "combat.card_draw.slither_energy_cost",
            "semantic.combat", "card_instance:" + card.InstanceId +
                ":model:" + card.CardId + ":stage:" + drawCost.Stage,
            ordinal, [], OperationKind: "bounded_integer",
            BoundedCount: relic ? drawCost.RelicMaxExclusive : 4);
        request.Validate();
        var costs = Enumerable.Range(0, request.SupportCount).ToArray();
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            request,
            costs,
            targetCardInstanceId = drawCost.CardInstanceId,
            targetCardModelId = card.CardId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticCostEventV111.EventSchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticCostEventV111(
            SemanticCostEventV111.EventSchemaId, eventId,
            boundaryFingerprint, supportFingerprint, request, costs,
            TargetCardInstanceId: drawCost.CardInstanceId,
            TargetCardModelId: card.CardId);
        semanticEvent.Validate();
        return semanticEvent;
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
        decision.Validate();
        if (pending is null || sourceIndex != decision.SelectedIndex ||
            CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event))
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
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ValidateDrawBoundary(checkpoint);
        var combat = checkpoint.World!.Combat!;
        var cost = combat.PendingChoice!.ControlledDrawCost!;
        int bound = cost.Stage == "relic" ? cost.RelicMaxExclusive : 4;
        if (selectedCost < 0 || selectedCost >= bound)
            throw new ArgumentOutOfRangeException(nameof(selectedCost));

        RunState resumed;
        if (allowEngineOwnedPrototypeReward)
        {
            using (ControlledChanceGuardV111.Enter())
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                resumed = PrototypeGameEngine
                    .ResumeControlledSemanticEffectDrawCost(
                        checkpoint, selectedCost);
        }
        else
        {
            using (ControlledChanceGuardV111.Enter())
                resumed = PrototypeGameEngine
                    .ResumeControlledSemanticEffectDrawCost(
                        checkpoint, selectedCost);
        }

        ValidateEffectDrawCostResult(resumed);
        return resumed;
    }

    private static void ValidateEffectDrawCostResult(RunState run)
    {
        if (run.Phase == RunPhase.Combat)
        {
            SemanticPilotCombatEngineV111
                .ValidateSemanticCombatBoundary(run);
            PrototypeStateInvariants.Validate(run);
        }
        else if (!PrototypeGameEngine
                     .IsAtCombatRewardOrTerminalBoundary(run))
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
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        var expectedOperation = siteId switch
        {
            "combat.random_hand_card.select" =>
                PrototypeCombatEffectKind
                    .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed,
            "combat.random_hand_card.energy_cost" =>
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
        ValidateBoundary(checkpoint);

        RunState resumed;
        using (ControlledChanceGuardV111.Enter())
            resumed = PrototypeGameEngine.ResumeControlledRandomCost(
                checkpoint.Fork(), selectedIndex);
        var nextCombat = resumed.World?.Combat;
        if (resumed.Phase != RunPhase.Combat || nextCombat is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=" + siteId +
                " operation=unsupported_post_cost_phase");
        if (nextCombat.PendingChoice?.ControlledRandomCost is not null)
            ValidateBoundary(resumed);
        else
            SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(
                resumed);
        SemanticDrawPileAdapterV111.RequireUnchangedDrawPile(
            priorCombat, nextCombat,
            "UNSUPPORTED_SEMANTIC_DECK site=" + siteId +
            " operation=unmodeled_draw_pile_mutation");
        return resumed;
    }
}
