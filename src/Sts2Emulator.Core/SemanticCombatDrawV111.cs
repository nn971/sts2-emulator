using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// A provenance-replayable, narrowly scoped semantic draw segment. The source
/// is a supplied concrete combat snapshot whose unrevealed draw-pile
/// instances are available, but whose exact order has not been committed by
/// the semantic Nature provider. No legacy swap tickets are created.
/// </summary>
public sealed record SemanticCombatDrawV111(
    RunState Origin, RunState Run, int RequestedCount, int RemainingCount,
    bool FromHandDraw, bool AutomaticHandDraw = false)
{
    public const string SchemaId = "sts2-v111-semantic-real-combat-draw-v1";

    /// <summary>
    /// Atomic mechanics/provider envelope for one semantic draw segment.
    /// Existing continuation properties remain flattened snapshot fields.
    /// </summary>
    [JsonIgnore]
    public SimulationStateV111<SemanticCombatDrawMechanicsStateV111,
        SemanticCombatDrawStochasticStateV111> Simulation { get; init; } = new(
            new SemanticCombatDrawMechanicsStateV111(
                Origin, Run, RequestedCount, RemainingCount, FromHandDraw,
                AutomaticHandDraw),
            new SemanticCombatDrawStochasticStateV111());

    [JsonIgnore]
    public SemanticCombatDrawMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public SemanticCombatDrawStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState Origin
    {
        get => Mechanics.Origin;
        init => Mechanics = Mechanics with { Origin = value };
    }

    public RunState Run
    {
        get => Mechanics.Run;
        init => Mechanics = Mechanics.SetRun(value);
    }

    public int RequestedCount
    {
        get => Mechanics.ProjectRequestedCount();
        init => Mechanics = Mechanics.SetRequestedCount(value);
    }

    public int RemainingCount
    {
        get => Mechanics.ProjectRemainingCount();
        init => Mechanics = Mechanics.SetRemainingCount(value);
    }

    public bool FromHandDraw
    {
        get => Mechanics.FromHandDraw;
        init => Mechanics = Mechanics with { FromHandDraw = value };
    }

    public bool AutomaticHandDraw
    {
        get => Mechanics.AutomaticHandDraw;
        init => Mechanics = Mechanics.SetAutomaticHandDraw(value);
    }

    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public SemanticDeckEventV111? Pending =>
        Run.World?.Combat?.Stochastic.SemanticDeck is
            { Pending: { Purpose: "draw" } } deck
            ? SemanticDeckEngineV111.Inspect(deck) : null;
    [JsonIgnore] public SemanticRandomPileCardEventV111?
        RandomPileCardPending =>
            SemanticRandomPileCardEngineV111.DescribePending(Run);
    [JsonIgnore] public SemanticDrawInsertionEventV111?
        DrawInsertionPending =>
            SemanticCombatDrawEngineV111.DescribePendingDrawInsertion(this);
    [JsonIgnore] public SemanticGeneratedCardEventV111?
        GeneratedCardPending =>
            SemanticCombatDrawEngineV111.DescribePendingGeneratedCard(this);
    [JsonIgnore] public SemanticPotionGenerationEventV111?
        PotionGenerationPending =>
            SemanticCombatDrawEngineV111
                .DescribePendingPotionGeneration(this);
    [JsonIgnore] public SemanticPoolShuffleEventV111?
        PoolShufflePending =>
            SemanticCombatDrawEngineV111
                .DescribePendingPoolShuffle(this);
    [JsonIgnore] public SemanticCardSelectionEventV111?
        CardSelectionPending =>
            SemanticCombatDrawEngineV111
                .DescribePendingCardSelection(this);
    [JsonIgnore] public SemanticCardTransformEventV111?
        CardTransformPending =>
            SemanticCombatDrawEngineV111
                .DescribePendingCardTransform(this);
    [JsonIgnore] public SemanticCardShortlistEventV111?
        CardShortlistPending =>
            SemanticCombatDrawEngineV111
                .DescribePendingCardShortlist(this);
    [JsonIgnore] public SemanticCostEventV111? RandomHandCostPending =>
        SemanticCombatDrawEngineV111
            .DescribePendingRandomHandCost(this);
    [JsonIgnore] public SemanticHandCostSelectionEventV111?
        HandCostSelectionPending =>
            SemanticCombatDrawEngineV111
                .DescribePendingHandCostSelection(this);
    [JsonIgnore] public SemanticCardSelectionEventV111?
        StratagemSelectionPending =>
            SemanticCombatDrawEngineV111
                .DescribePendingStratagemSelection(this);
    [JsonIgnore] public SemanticDrawHookChoiceEventV111?
        DrawHookChoicePending =>
            SemanticDrawHookChoiceEngineV111.DescribePending(Run);
    [JsonIgnore] public SemanticTargetEventV111? TargetPending =>
        AutomaticHandDraw &&
        Run.World?.Combat?.PendingChoice is
            { AutomaticSemanticDrawTargetChoice: true,
              ControlledTarget: not null }
            ? SemanticPilotCombatEngineV111.DescribePendingTargetEvent(
                Run, Fingerprint, TargetTranscript.Length)
            : null;
    /// <summary>Cost chance follows the *already committed* card instance.
    /// Bound reflects the current source Snecko/Slither stage.</summary>
    [JsonIgnore] public RandomIndexRequestV111? CostPending
    {
        get
        {
            var cost = Run.World?.Combat?.PendingChoice?.ControlledDrawCost;
            if (cost is null) return null;
            var card = Run.World!.Combat!.Cards.Single(x =>
                x.InstanceId == cost.CardInstanceId);
            var relic = cost.Stage == "relic";
            return new RandomIndexRequestV111(
                relic ? StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId
                      : StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId,
                "semantic.combat", "card_instance:" + card.InstanceId +
                    ":model:" + card.CardId + ":stage:" + cost.Stage,
                Stochastic.CostTranscript.Length, [],
                OperationKind: "bounded_integer",
                BoundedCount: relic ? cost.RelicMaxExclusive : 4);
        }
    }
    [JsonIgnore] public string? CostEventId => CostPending is { } request
        ? CanonicalJson.Sha256(new
        {
            schema = SchemaId,
            checkpoint = Fingerprint,
            request
        }) : null;
    [JsonIgnore] public bool AtBoundary =>
        Pending is null && RandomPileCardPending is null &&
        DrawInsertionPending is null && GeneratedCardPending is null &&
        PotionGenerationPending is null &&
        PoolShufflePending is null &&
        CardSelectionPending is null &&
        CardTransformPending is null &&
        CardShortlistPending is null &&
        RandomHandCostPending is null &&
        HandCostSelectionPending is null &&
        StratagemSelectionPending is null &&
        DrawHookChoicePending is null && TargetPending is null &&
        CostPending is null &&
        RemainingCount == 0;
    // The provider's transcript survives across turns. Only decisions
    // newly made during THIS draw segment are replayed by this envelope.
    [JsonIgnore] public SemanticDeckChoiceV111[] Transcript =>
        (Run.World?.Combat?.Stochastic.SemanticDeck?.Transcript ?? [])
            .Skip(Origin.World?.Combat?.Stochastic.SemanticDeck?.Transcript?.Length ?? 0)
            .ToArray();
    public SemanticRandomPileCardDecisionV111[] RandomPileCardTranscript
    {
        get => Stochastic.RandomPileCardTranscript;
        init => Stochastic = Stochastic with
        {
            RandomPileCardTranscript = value
        };
    }
    public SemanticDrawInsertionDecisionV111[] DrawInsertionTranscript
    {
        get => Stochastic.DrawInsertionTranscript;
        init => Stochastic = Stochastic with
        {
            DrawInsertionTranscript = value
        };
    }
    public SemanticGeneratedCardDecisionV111[] GeneratedCardTranscript
    {
        get => Stochastic.GeneratedCardTranscript;
        init => Stochastic = Stochastic with
        {
            GeneratedCardTranscript = value
        };
    }
    public SemanticPotionGenerationDecisionV111[] PotionGenerationTranscript
    {
        get => Stochastic.PotionGenerationTranscript;
        init => Stochastic = Stochastic with
        {
            PotionGenerationTranscript = value
        };
    }
    public SemanticPoolShuffleDecisionV111[] PoolShuffleTranscript
    {
        get => Stochastic.PoolShuffleTranscript;
        init => Stochastic = Stochastic with
        {
            PoolShuffleTranscript = value
        };
    }
    public SemanticCardSelectionDecisionV111[] CardSelectionTranscript
    {
        get => Stochastic.CardSelectionTranscript;
        init => Stochastic = Stochastic with
        {
            CardSelectionTranscript = value
        };
    }
    public SemanticCardTransformDecisionV111[] CardTransformTranscript
    {
        get => Stochastic.CardTransformTranscript;
        init => Stochastic = Stochastic with
        {
            CardTransformTranscript = value
        };
    }
    public SemanticCardShortlistDecisionV111[] CardShortlistTranscript
    {
        get => Stochastic.CardShortlistTranscript;
        init => Stochastic = Stochastic with
        {
            CardShortlistTranscript = value
        };
    }
    public SemanticCostDecisionV111[] RandomHandCostTranscript
    {
        get => Stochastic.RandomHandCostTranscript;
        init => Stochastic = Stochastic with
        {
            RandomHandCostTranscript = value
        };
    }
    public SemanticHandCostSelectionDecisionV111[]
        HandCostSelectionTranscript
    {
        get => Stochastic.HandCostSelectionTranscript;
        init => Stochastic = Stochastic with
        {
            HandCostSelectionTranscript = value
        };
    }
    public SemanticCardSelectionDecisionV111[] StratagemSelectionTranscript
    {
        get => Stochastic.StratagemSelectionTranscript;
        init => Stochastic = Stochastic with
        {
            StratagemSelectionTranscript = value
        };
    }
    public SemanticDrawHookChoiceDecisionV111[] DrawHookChoiceTranscript
    {
        get => Stochastic.DrawHookChoiceTranscript;
        init => Stochastic = Stochastic with
        {
            DrawHookChoiceTranscript = value
        };
    }
    public SemanticTargetDecisionV111[] TargetTranscript
    {
        get => Stochastic.TargetTranscript;
        init => Stochastic = Stochastic with
        {
            TargetTranscript = value
        };
    }
    public RandomIndexDecisionV111[] CostTranscript
    {
        get => Stochastic.CostTranscript;
        init => Stochastic = Stochastic with
        {
            CostTranscript = value
        };
    }
    public SemanticCombatDrawBoundaryChoiceV111[] BoundaryTranscript
    {
        get => Stochastic.BoundaryTranscript;
        init => Stochastic = Stochastic with
        {
            BoundaryTranscript = value
        };
    }
    public int BoundaryTranscriptVersion
    {
        get => Stochastic.BoundaryTranscriptVersion;
        init => Stochastic = Stochastic with
        {
            BoundaryTranscriptVersion = value
        };
    }

    public void Validate()
    {
        this.Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.SemanticCombatDraw);
        this.Stochastic.Validate();
        this.Stochastic.ValidateBoundaryTranscript(this.Transcript.Length);
        var randomPileCardPending = this.RandomPileCardPending;
        var drawInsertionPending = this.DrawInsertionPending;
        var generatedCardPending = this.GeneratedCardPending;
        var potionGenerationPending = this.PotionGenerationPending;
        var poolShufflePending = this.PoolShufflePending;
        var cardSelectionPending = this.CardSelectionPending;
        var cardTransformPending = this.CardTransformPending;
        var cardShortlistPending = this.CardShortlistPending;
        var randomHandCostPending = this.RandomHandCostPending;
        var handCostSelectionPending = this.HandCostSelectionPending;
        var completedHookCursor =
            this.Run.World?.Combat?.AutomaticPipelineContinuation;
        var drawHookChoicePending = this.DrawHookChoicePending;
        PendingCombatChoiceState? automaticBeforeHandDrawPoolShuffleChoice =
            this.AutomaticHandDraw &&
            this.Run.World?.Combat is { } poolShuffleCombat &&
            poolShuffleCombat.PendingChoice is { } poolShuffleChoice &&
            PrototypeGameEngine
                .IsAutomaticPipelineSemanticPoolShuffleContinuation(
                    poolShuffleCombat, poolShuffleChoice)
                ? poolShuffleChoice : null;
        bool automaticBeforeHandDrawPoolShuffle =
            automaticBeforeHandDrawPoolShuffleChoice is not null;
        bool automaticBeforeHandDrawGeneratedSelection =
            this.AutomaticHandDraw &&
            this.Run.World?.Combat is { } generatedSelectionCombat &&
            generatedSelectionCombat.PendingChoice is
                { } generatedSelectionChoice &&
            PrototypeGameEngine
                .IsAutomaticSemanticBeforeHandDrawGeneratedSelection(
                    generatedSelectionCombat, generatedSelectionChoice);
        bool completedAutomaticHandDraw = this.AutomaticHandDraw &&
            this.RequestedCount == 0 && this.RemainingCount == 0 &&
            completedHookCursor is { } completedCursor &&
            completedCursor.Mechanics.IsImmediatelyAfterStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) &&
            !completedCursor.Stochastic.HasSavedHandDrawProgress() &&
            this.Run.World?.Combat is
                { PendingChoice: null,
                  PendingKnowledgeDemonChoice: null,
                  SemanticDeck: { Pending: null } };
        bool automaticBeforeHandDrawRandomTarget =
            this.AutomaticHandDraw && this.RequestedCount == 0 &&
            this.RemainingCount == 0 &&
            this.Run.World?.Combat is { } targetCombat &&
            targetCombat.PendingChoice?.ControlledTarget is not null &&
            targetCombat.AutomaticPipelineContinuation is
                { } targetCursor &&
            targetCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                targetCursor.Mechanics);
        bool automaticBeforeHandDrawEffectDrawPending =
            this.AutomaticHandDraw &&
            this.Run.World?.Combat is { } beforeHandDrawEffectCombat &&
            beforeHandDrawEffectCombat.PendingChoice is
                { } beforeHandDrawEffectChoice &&
            PrototypeGameEngine
                .IsAutomaticBeforeHandDrawHookSemanticDrawContinuation(
                    beforeHandDrawEffectCombat, beforeHandDrawEffectChoice);
        if (this.RequestedCount < 0 ||
            this.RemainingCount < 0 ||
            this.RemainingCount > this.RequestedCount ||
            (this.Mechanics.HasAutomaticPipelineDrawProgress &&
             (this.Mechanics.RequestedCount != 0 ||
              this.Mechanics.RemainingCount != 0)) ||
            (this.RequestedCount == 0 && randomPileCardPending is null &&
             drawInsertionPending is null &&
             generatedCardPending is null &&
             potionGenerationPending is null &&
             poolShufflePending is null &&
             cardSelectionPending is null &&
             cardTransformPending is null &&
             cardShortlistPending is null &&
             randomHandCostPending is null &&
             handCostSelectionPending is null &&
             drawHookChoicePending?.BeforeHandDraw != true &&
             !automaticBeforeHandDrawEffectDrawPending &&
             !automaticBeforeHandDrawPoolShuffle &&
             !automaticBeforeHandDrawGeneratedSelection &&
             !automaticBeforeHandDrawRandomTarget &&
             !completedAutomaticHandDraw) ||
            this.Origin.Phase != RunPhase.Combat ||
            this.Origin.World?.Combat is not { } originCombat ||
            (!this.AutomaticHandDraw && originCombat.Stochastic.SemanticDeck is not null) ||
            (this.AutomaticHandDraw &&
             originCombat.Stochastic.SemanticDeck is { } prior &&
             (this.Run.World?.Combat?.Stochastic.SemanticDeck?
                 .Transcript?.Length ?? 0) <
                 (prior.Transcript?.Length ?? 0)) ||
            originCombat.PendingChoice is not null ||
            (!this.AutomaticHandDraw &&
             originCombat.AutomaticPipelineContinuation is not null) ||
            (this.AutomaticHandDraw &&
             (originCombat.AutomaticPipelineContinuation is not { } initial ||
              !initial.Mechanics.IsAtStep(
                  PrototypeAutomaticStepKind.DrawPlayerHand) ||
              initial.Stochastic.TryGetSemanticHandDrawProjection(
                  initial.Mechanics, out _, out _))) ||
            this.Run.Phase != RunPhase.Combat ||
            this.Run.World?.Combat is not { } combat ||
            combat.Stochastic.SemanticDeck is not { } deck ||
            (!this.AutomaticHandDraw &&
             combat.AutomaticPipelineContinuation is not null) ||
            (!this.AutomaticHandDraw &&
             randomPileCardPending is not null) ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new InvalidDataException("Invalid semantic combat draw stage.");
        if (this.AutomaticHandDraw && originCombat.Stochastic.SemanticDeck is not null)
            SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
                originCombat,
                "Invalid semantic combat draw stage.",
                "Invalid semantic combat draw stage.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Invalid semantic combat draw stage.",
            "Semantic provider state and physical draw-pile membership differ.",
            allowPending: true);
        var cost = combat.PendingChoice?.ControlledDrawCost;
        var controlledSemanticDraw =
            combat.PendingChoice?.ControlledSemanticDraw;
        bool standaloneSemanticDrawPending = controlledSemanticDraw?
            .StandaloneSemanticDrawSegment == true ||
            combat.PendingChoice?.StandaloneSemanticDrawHookChoice == true;
        var stratagemSelectionPending = this.StratagemSelectionPending;
        var pipelineCursor = combat.AutomaticPipelineContinuation;
        bool hasPipelineSemanticDrawProgress =
            TryGetAutomaticSemanticHandDrawProgress(
                combat, out int pipelineRequested,
                out int pipelineRemaining);
        bool automaticHandDrawChildInsertionPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } insertionChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookDrawInsertionContinuation(
                    combat, insertionChoice) &&
            hasPipelineSemanticDrawProgress &&
            pipelineRequested == this.RequestedCount &&
            pipelineRemaining == this.RemainingCount;
        bool automaticBeforeHandDrawChildInsertionPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } beforeHandDrawInsertionChoice &&
            PrototypeGameEngine
                .IsAutomaticBeforeHandDrawHookDrawInsertionContinuation(
                    combat, beforeHandDrawInsertionChoice);
        bool automaticHandDrawGeneratedCardPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } activeGeneratedCardChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawGeneratedCardContinuation(
                    combat, activeGeneratedCardChoice) &&
            hasPipelineSemanticDrawProgress &&
            pipelineRequested == this.RequestedCount &&
            pipelineRemaining == this.RemainingCount;
        bool automaticBeforeHandDrawGeneratedCardPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } beforeHandDrawGeneratedCardChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawGeneratedCardContinuation(
                    combat, beforeHandDrawGeneratedCardChoice) &&
            pipelineCursor is not null &&
            pipelineCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                pipelineCursor.Mechanics);
        bool automaticHandDrawPotionGenerationPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } activePotionGenerationChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawPotionGenerationContinuation(
                    combat, activePotionGenerationChoice) &&
            hasPipelineSemanticDrawProgress &&
            pipelineRequested == this.RequestedCount &&
            pipelineRemaining == this.RemainingCount;
        bool automaticBeforeHandDrawPotionGenerationPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } beforeHandDrawPotionGenerationChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawPotionGenerationContinuation(
                    combat, beforeHandDrawPotionGenerationChoice) &&
            pipelineCursor is not null &&
            pipelineCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                pipelineCursor.Mechanics);
        bool automaticHandDrawPoolShufflePending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } activePoolShuffleChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawPoolShuffleContinuation(
                    combat, activePoolShuffleChoice) &&
            hasPipelineSemanticDrawProgress &&
            pipelineRequested == this.RequestedCount &&
            pipelineRemaining == this.RemainingCount;
        bool automaticBeforeHandDrawPoolShufflePending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } beforeHandDrawPoolShuffleChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawPoolShuffleContinuation(
                    combat, beforeHandDrawPoolShuffleChoice) &&
            pipelineCursor is not null &&
            pipelineCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                pipelineCursor.Mechanics);
        bool automaticHandDrawCardSelectionPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } activeCardSelectionChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookCardSelectionContinuation(
                    combat, activeCardSelectionChoice) &&
            hasPipelineSemanticDrawProgress &&
            pipelineRequested == this.RequestedCount &&
            pipelineRemaining == this.RemainingCount;
        bool automaticBeforeHandDrawCardSelectionPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } beforeHandDrawCardSelectionChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookCardSelectionContinuation(
                    combat, beforeHandDrawCardSelectionChoice) &&
            pipelineCursor is not null &&
            pipelineCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                pipelineCursor.Mechanics);
        bool automaticHandDrawCardTransformPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } activeCardTransformChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookCardTransformContinuation(
                    combat, activeCardTransformChoice) &&
            hasPipelineSemanticDrawProgress &&
            pipelineRequested == this.RequestedCount &&
            pipelineRemaining == this.RemainingCount;
        bool automaticBeforeHandDrawCardTransformPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } beforeHandDrawCardTransformChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookCardTransformContinuation(
                    combat, beforeHandDrawCardTransformChoice) &&
            pipelineCursor is not null &&
            pipelineCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                pipelineCursor.Mechanics);
        bool automaticHandDrawCardShortlistPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } activeCardShortlistChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookCardShortlistContinuation(
                    combat, activeCardShortlistChoice) &&
            hasPipelineSemanticDrawProgress &&
            pipelineRequested == this.RequestedCount &&
            pipelineRemaining == this.RemainingCount;
        bool automaticBeforeHandDrawCardShortlistPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } beforeHandDrawCardShortlistChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookCardShortlistContinuation(
                    combat, beforeHandDrawCardShortlistChoice) &&
            pipelineCursor is not null &&
            pipelineCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                pipelineCursor.Mechanics);
        bool automaticHandDrawRandomHandCostPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } activeRandomHandCostChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookRandomHandCostContinuation(
                    combat, activeRandomHandCostChoice) &&
            hasPipelineSemanticDrawProgress &&
            pipelineRequested == this.RequestedCount &&
            pipelineRemaining == this.RemainingCount;
        bool automaticBeforeHandDrawRandomHandCostPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } beforeHandDrawRandomHandCostChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookRandomHandCostContinuation(
                    combat, beforeHandDrawRandomHandCostChoice) &&
            pipelineCursor is not null &&
            pipelineCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                pipelineCursor.Mechanics);
        if (automaticHandDrawChildInsertionPending ||
            automaticBeforeHandDrawChildInsertionPending)
            SemanticDrawInsertionChoiceV111.ValidateBoundary(this.Run);
        if (automaticHandDrawGeneratedCardPending ||
            automaticBeforeHandDrawGeneratedCardPending)
            SemanticGenerationChoiceV111.ValidateBoundary(this.Run);
        if (automaticHandDrawPotionGenerationPending ||
            automaticBeforeHandDrawPotionGenerationPending)
            SemanticPotionGenerationChoiceV111.ValidateBoundary(this.Run);
        if (automaticHandDrawPoolShufflePending ||
            automaticBeforeHandDrawPoolShufflePending)
            SemanticPoolShuffleChoiceV111.ValidateBoundary(this.Run);
        if (automaticHandDrawCardSelectionPending ||
            automaticBeforeHandDrawCardSelectionPending)
            SemanticCardSelectionEngineV111.ValidatePendingBoundary(this.Run);
        if (automaticHandDrawCardTransformPending ||
            automaticBeforeHandDrawCardTransformPending)
            SemanticCardTransformChoiceV111.ValidateBoundary(
                this.Run, combat.PendingChoice!.ControlledCardTransform!);
        if (automaticHandDrawCardShortlistPending ||
            automaticBeforeHandDrawCardShortlistPending)
            SemanticCardShortlistChoiceV111.ValidateBoundary(this.Run);
        if (automaticHandDrawRandomHandCostPending ||
            automaticBeforeHandDrawRandomHandCostPending)
            SemanticRandomHandCostChoiceV111.ValidateBoundary(this.Run);
        bool automaticHandDrawEffectDrawPending =
            this.AutomaticHandDraw && combat.PendingChoice is
                { } effectDrawChoice &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookSemanticDrawContinuation(
                    combat, effectDrawChoice) &&
            hasPipelineSemanticDrawProgress &&
            pipelineRequested == this.RequestedCount &&
            pipelineRemaining == this.RemainingCount;
        if (automaticHandDrawEffectDrawPending ||
            automaticBeforeHandDrawEffectDrawPending)
        {
            if (combat.PendingChoice?.ChoiceId ==
                "controlled_draw_cost")
                SemanticCostChoiceV111.ValidateDrawBoundary(this.Run);
            else
                SemanticDrawChoiceV111.ValidateBoundary(this.Run);
        }
        bool semanticDrawChoicePending = standaloneSemanticDrawPending ||
            automaticHandDrawEffectDrawPending ||
            automaticBeforeHandDrawEffectDrawPending;
        var targetPending = this.TargetPending;
        bool automaticSemanticHandDrawRandomTarget =
            targetPending is not null;
        bool semanticBeforeHandDrawProgress = pipelineCursor is not null &&
            pipelineCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                pipelineCursor.Mechanics);
        if (this.AutomaticHandDraw)
        {
            bool awaiting = deck.Pending is not null || cost is not null ||
                automaticHandDrawEffectDrawPending ||
                automaticBeforeHandDrawEffectDrawPending ||
                randomPileCardPending is not null ||
                drawInsertionPending is not null ||
                generatedCardPending is not null ||
                potionGenerationPending is not null ||
                poolShufflePending is not null ||
                cardSelectionPending is not null ||
                cardTransformPending is not null ||
                cardShortlistPending is not null ||
                randomHandCostPending is not null ||
                handCostSelectionPending is not null ||
                stratagemSelectionPending is not null ||
                drawHookChoicePending is not null ||
                automaticBeforeHandDrawPoolShuffle ||
                automaticBeforeHandDrawGeneratedSelection ||
                automaticBeforeHandDrawRandomTarget ||
                automaticSemanticHandDrawRandomTarget;
            if (randomPileCardPending is not null)
            {
                bool beforeHandDrawChoice = pipelineCursor is not null &&
                    pipelineCursor.Mechanics.IsAtStep(
                        PrototypeAutomaticStepKind.DrawPlayerHand) &&
                    semanticBeforeHandDrawProgress &&
                    this.RequestedCount == 0 && this.RemainingCount == 0;
                bool activeHandDrawChoice = hasPipelineSemanticDrawProgress &&
                    pipelineRequested == this.RequestedCount &&
                    pipelineRemaining == this.RemainingCount;
                if ((!beforeHandDrawChoice && !activeHandDrawChoice) ||
                    combat.PendingChoice is not
                        { ChoiceId: "controlled_random_pile_card",
                          ControlledRandomPileCard: not null } ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic automatic hand-draw pile-card cursor is inconsistent.");
            }
            else if (drawHookChoicePending?.BeforeHandDraw == true)
            {
                if (pipelineCursor is null ||
                    !pipelineCursor.Mechanics.IsAtStep(
                        PrototypeAutomaticStepKind.DrawPlayerHand) ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    combat.PendingChoice?.AutomaticSemanticDrawHookChoice != true ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw action cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawEffectDrawPending)
            {
                if (pipelineCursor is null ||
                    !pipelineCursor.Mechanics.IsAtStep(
                        PrototypeAutomaticStepKind.DrawPlayerHand) ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw effect-draw cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawPoolShuffle &&
                     automaticBeforeHandDrawPoolShuffleChoice is
                         { } beforeHandDrawShuffleChoice)
            {
                if (pipelineCursor is null ||
                    !pipelineCursor.Mechanics.IsAtStep(
                        PrototypeAutomaticStepKind.DrawPlayerHand) ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    !beforeHandDrawShuffleChoice.AutomaticSemanticDrawHookChoice ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw pool-shuffle cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawGeneratedSelection)
            {
                if (pipelineCursor is null ||
                    !pipelineCursor.Mechanics.IsAtStep(
                        PrototypeAutomaticStepKind.DrawPlayerHand) ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw generated-selection cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawChildInsertionPending)
            {
                if (pipelineCursor is null ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw insertion cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawGeneratedCardPending)
            {
                if (pipelineCursor is null ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw generated-card cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawPotionGenerationPending)
            {
                if (pipelineCursor is null ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw potion-generation cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawPoolShufflePending)
            {
                if (pipelineCursor is null ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw pool-shuffle cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawCardSelectionPending)
            {
                if (pipelineCursor is null ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw card-selection cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawCardTransformPending)
            {
                if (pipelineCursor is null ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw card-transform cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawCardShortlistPending)
            {
                if (pipelineCursor is null ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw card-shortlist cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawRandomHandCostPending)
            {
                if (pipelineCursor is null ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw random-hand-cost cursor is inconsistent.");
            }
            else if (automaticHandDrawGeneratedCardPending)
            {
                if (pipelineCursor is null ||
                    !hasPipelineSemanticDrawProgress ||
                    pipelineRequested != this.RequestedCount ||
                    pipelineRemaining != this.RemainingCount ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Automatic hand-draw generated-card cursor is inconsistent.");
            }
            else if (automaticHandDrawPotionGenerationPending)
            {
                if (pipelineCursor is null ||
                    !hasPipelineSemanticDrawProgress ||
                    pipelineRequested != this.RequestedCount ||
                    pipelineRemaining != this.RemainingCount ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Automatic hand-draw potion-generation cursor is inconsistent.");
            }
            else if (automaticHandDrawPoolShufflePending)
            {
                if (pipelineCursor is null ||
                    !hasPipelineSemanticDrawProgress ||
                    pipelineRequested != this.RequestedCount ||
                    pipelineRemaining != this.RemainingCount ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Automatic hand-draw pool-shuffle cursor is inconsistent.");
            }
            else if (automaticHandDrawCardSelectionPending)
            {
                if (pipelineCursor is null ||
                    !hasPipelineSemanticDrawProgress ||
                    pipelineRequested != this.RequestedCount ||
                    pipelineRemaining != this.RemainingCount ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Automatic hand-draw card-selection cursor is inconsistent.");
            }
            else if (automaticHandDrawCardTransformPending)
            {
                if (pipelineCursor is null ||
                    !hasPipelineSemanticDrawProgress ||
                    pipelineRequested != this.RequestedCount ||
                    pipelineRemaining != this.RemainingCount ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Automatic hand-draw card-transform cursor is inconsistent.");
            }
            else if (automaticHandDrawCardShortlistPending)
            {
                if (pipelineCursor is null ||
                    !hasPipelineSemanticDrawProgress ||
                    pipelineRequested != this.RequestedCount ||
                    pipelineRemaining != this.RemainingCount ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Automatic hand-draw card-shortlist cursor is inconsistent.");
            }
            else if (automaticHandDrawRandomHandCostPending)
            {
                if (pipelineCursor is null ||
                    !hasPipelineSemanticDrawProgress ||
                    pipelineRequested != this.RequestedCount ||
                    pipelineRemaining != this.RemainingCount ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Automatic hand-draw random-hand-cost cursor is inconsistent.");
            }
            else if (automaticBeforeHandDrawRandomTarget)
            {
                if (pipelineCursor is null ||
                    !pipelineCursor.Mechanics.IsAtStep(
                        PrototypeAutomaticStepKind.DrawPlayerHand) ||
                    !semanticBeforeHandDrawProgress ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw target cursor is inconsistent.");
                SemanticTargetChoiceV111.ValidateBoundary(this.Run);
            }
            else if (automaticSemanticHandDrawRandomTarget)
            {
                if (pipelineCursor is null ||
                    !hasPipelineSemanticDrawProgress ||
                    pipelineRequested != this.RequestedCount ||
                    pipelineRemaining != this.RemainingCount ||
                    deck.Pending is not null || cost is not null ||
                    combat.PendingChoice?.AutomaticSemanticDrawTargetChoice != true)
                    throw new InvalidDataException(
                        "Automatic hand-draw target cursor is inconsistent.");
                SemanticTargetChoiceV111.ValidateBoundary(this.Run);
            }
            else if (pipelineCursor is null ||
                semanticBeforeHandDrawProgress ||
                (awaiting &&
                 (!hasPipelineSemanticDrawProgress ||
                  pipelineRemaining != this.RemainingCount ||
                  pipelineRequested != this.RequestedCount)) ||
                (!awaiting &&
                 (this.RemainingCount != 0 ||
                  !pipelineCursor.Mechanics.IsImmediatelyAfterStep(
                      PrototypeAutomaticStepKind.DrawPlayerHand) ||
                  pipelineCursor.Stochastic.HasSavedHandDrawProgress())))
                throw new InvalidDataException(
                    "Semantic automatic hand-draw cursor is inconsistent.");
        }
        if ((combat.PendingChoice is not null && cost is null &&
             randomPileCardPending is null &&
             drawInsertionPending is null &&
             generatedCardPending is null &&
             potionGenerationPending is null &&
             poolShufflePending is null &&
             cardSelectionPending is null &&
             cardTransformPending is null &&
             cardShortlistPending is null &&
             randomHandCostPending is null &&
             handCostSelectionPending is null &&
             !semanticDrawChoicePending &&
             stratagemSelectionPending is null &&
             drawHookChoicePending is null &&
             !automaticBeforeHandDrawPoolShuffle &&
             !automaticBeforeHandDrawGeneratedSelection &&
             !automaticBeforeHandDrawRandomTarget &&
             !automaticSemanticHandDrawRandomTarget) ||
            (cost is not null &&
             (deck.Pending is not null ||
              ((automaticHandDrawEffectDrawPending ||
                automaticBeforeHandDrawEffectDrawPending)
                  ? cost.RemainingCount != controlledSemanticDraw!.RemainingCount ||
                    cost.FromHandDraw != controlledSemanticDraw.FromHandDraw ||
                    cost.EventDepth != controlledSemanticDraw.EventDepth
                  : cost.RemainingCount != this.RemainingCount ||
                    cost.FromHandDraw != this.FromHandDraw ||
                    cost.EventDepth != 0 ||
                    !PrototypeGameEngine.ValidateControlledDrawCost(
                        this.Run.Player, combat,
                        cost with { FromHandDraw = true })))) ||
            (deck.Pending is null && cost is null &&
             randomPileCardPending is null &&
             drawInsertionPending is null &&
             generatedCardPending is null &&
             potionGenerationPending is null &&
             poolShufflePending is null &&
             cardSelectionPending is null &&
             cardTransformPending is null &&
             cardShortlistPending is null &&
             randomHandCostPending is null &&
             handCostSelectionPending is null &&
             stratagemSelectionPending is null &&
             drawHookChoicePending is null &&
             targetPending is null &&
             this.RemainingCount != 0) ||
            (deck.Pending is not null &&
             ((automaticHandDrawEffectDrawPending ||
               automaticBeforeHandDrawEffectDrawPending)
                 ? controlledSemanticDraw?.RemainingCount is not > 0
                 : this.RemainingCount <= 0)))
            throw new InvalidDataException(
                "Semantic deck/drawn-cost checkpoint is inconsistent.");
        if (controlledSemanticDraw?.StandaloneSemanticDrawSegment == true &&
            controlledSemanticDraw.RemainingCount != this.RemainingCount)
            throw new InvalidDataException(
                "Standalone semantic draw continuation has a stale count.");
        if (drawHookChoicePending is { } hookChoice)
        {
            bool cursorMatches = hookChoice.AutomaticHandDraw
                ? this.AutomaticHandDraw &&
                  combat.PendingChoice?.AutomaticSemanticDrawHookChoice == true &&
                  (hookChoice.BeforeHandDraw
                      ? pipelineCursor?.Mechanics.IsAtStep(
                            PrototypeAutomaticStepKind.DrawPlayerHand) == true &&
                        semanticBeforeHandDrawProgress &&
                        this.RequestedCount == 0 && this.RemainingCount == 0
                      : hasPipelineSemanticDrawProgress &&
                        pipelineRemaining == this.RemainingCount &&
                        pipelineRequested == this.RequestedCount)
                : !this.AutomaticHandDraw &&
                  combat.PendingChoice?.StandaloneSemanticDrawHookChoice == true &&
                  PrototypeGameEngine.TryGetStandaloneSemanticDrawOperation(
                      combat.PendingChoice, out var hookDraw) &&
                  hookDraw.Amount == this.RemainingCount;
            if (!cursorMatches)
                throw new InvalidDataException(
                    "CardDrawn hook-choice cursor is inconsistent.");
        }
        if (stratagemSelectionPending is { } selectionEvent)
        {
            bool cursorMatches = this.AutomaticHandDraw
                ? hasPipelineSemanticDrawProgress &&
                  pipelineRemaining == this.RemainingCount &&
                  pipelineRequested == this.RequestedCount
                : PrototypeGameEngine.TryGetStandaloneSemanticDrawOperation(
                      combat.PendingChoice!, out var draw) &&
                  draw.Amount == this.RemainingCount;
            if (!cursorMatches || this.RemainingCount <= 0 ||
                selectionEvent.MaximumSelections !=
                    PrototypeGameEngine.ExpectedStratagemSelectionCount(
                        combat))
                throw new InvalidDataException(
                    "Standalone Stratagem selection cursor is inconsistent.");
        }
        if (drawInsertionPending is { } insertionEvent)
        {
            var insertion = combat.PendingChoice?.ControlledDrawInsertion;
            if ((!automaticHandDrawChildInsertionPending &&
                 !automaticBeforeHandDrawChildInsertionPending &&
                 (this.RemainingCount <= 0 ||
                  insertion?.SemanticDrawContinuation?.RemainingCount !=
                    this.RemainingCount)) ||
                insertionEvent.Request.BoundedCount !=
                    insertionEvent.EligibleDrawPileInstanceIds.Length + 1)
                throw new InvalidDataException(
                    "Semantic draw-insertion cursor is inconsistent.");
        }
        if (generatedCardPending is { } generatedCardEvent)
        {
            var insertion = combat.PendingChoice?.ControlledDrawInsertion;
            var generation = combat.PendingChoice?.ControlledGeneration;
            bool insertionModel = insertion is
                    { Stage: "card_model",
                      Operation.Kind: PrototypeCombatEffectKind
                          .CreateRandomFreeAttacksInDrawPile } &&
                (automaticHandDrawChildInsertionPending ||
                 automaticBeforeHandDrawChildInsertionPending) &&
                generatedCardEvent.CandidateCardIds.SequenceEqual(
                    insertion.CandidateCardIds);
            bool generatedModel = generation is not null &&
                (automaticHandDrawGeneratedCardPending ||
                 automaticBeforeHandDrawGeneratedCardPending) &&
                generatedCardEvent.CandidateCardIds.SequenceEqual(
                    generation.CandidateCardIds);
            if (!insertionModel && !generatedModel)
                throw new InvalidDataException(
                    "Semantic generated-card cursor is inconsistent.");
            SemanticGenerationChoiceV111.ValidateBoundary(this.Run);
        }
        if (potionGenerationPending is { } potionGenerationEvent)
        {
            var generation = combat.PendingChoice?.ControlledPotionGeneration;
            if ((!automaticHandDrawPotionGenerationPending &&
                 !automaticBeforeHandDrawPotionGenerationPending) ||
                generation is null ||
                !potionGenerationEvent.CandidatePotionIds.SequenceEqual(
                    generation.CandidatePotionIds))
                throw new InvalidDataException(
                    "Semantic potion-generation cursor is inconsistent.");
            SemanticPotionGenerationChoiceV111.ValidateBoundary(this.Run);
        }
        if (poolShufflePending is not null &&
            !automaticHandDrawPoolShufflePending &&
            !automaticBeforeHandDrawPoolShufflePending)
            throw new InvalidDataException(
                "Semantic pool-shuffle cursor is inconsistent.");
        if (cardSelectionPending is not null &&
            !automaticHandDrawCardSelectionPending &&
            !automaticBeforeHandDrawCardSelectionPending)
            throw new InvalidDataException(
                "Semantic card-selection cursor is inconsistent.");
        if (cardTransformPending is not null &&
            !automaticHandDrawCardTransformPending &&
            !automaticBeforeHandDrawCardTransformPending)
            throw new InvalidDataException(
                "Semantic card-transform cursor is inconsistent.");
        if (cardShortlistPending is not null &&
            !automaticHandDrawCardShortlistPending &&
            !automaticBeforeHandDrawCardShortlistPending)
            throw new InvalidDataException(
                "Semantic card-shortlist cursor is inconsistent.");
        if ((randomHandCostPending is not null ||
             handCostSelectionPending is not null) &&
            !automaticHandDrawRandomHandCostPending &&
            !automaticBeforeHandDrawRandomHandCostPending)
            throw new InvalidDataException(
                "Semantic random-hand-cost cursor is inconsistent.");
    }

    public SemanticCombatDrawV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork(),
        Stochastic = Stochastic.Fork()
    };

    internal static bool TryGetAutomaticSemanticHandDrawProgress(
        CombatState combat, out int requested, out int remaining)
    {
        requested = 0;
        remaining = 0;
        var cursor = combat.AutomaticPipelineContinuation;
        return cursor is not null &&
            cursor.Stochastic.TryGetSemanticHandDrawProgress(
                cursor.Mechanics, out requested, out remaining);
    }
}

public static class SemanticCombatDrawEngineV111
{
    private static SemanticCombatDrawV111 PublishDrawProgress(
        SemanticCombatDrawV111 state,
        PrototypeDrawCardsResumeResultV111 progress)
    {
        progress.Validate();
        var next = state with
        {
            Run = progress.Run,
            RemainingCount = progress.RemainingCount
        };
        if (progress.RequestedCount is { } requested)
            next = next with { RequestedCount = requested };
        return next;
    }

    private static SemanticCombatDrawV111 RecordNewSemanticDrawChoices(
        SemanticCombatDrawV111 source, SemanticCombatDrawV111 resumed)
    {
        var priorCount = source.Transcript.Length;
        var transcript = resumed.Transcript;
        if (transcript.Length < priorCount)
            throw new InvalidDataException(
                "Semantic draw continuation removed committed draw decisions.");
        var stochastic = resumed.Stochastic;
        for (var index = priorCount; index < transcript.Length; index++)
            stochastic = stochastic.AppendDeckChoice(index);
        return resumed with { Stochastic = stochastic };
    }

    internal static SemanticGeneratedCardEventV111?
        DescribePendingGeneratedCard(SemanticCombatDrawV111 state)
    {
        var run = state.Run;
        var combat = run.World?.Combat;
        var pending = combat?.PendingChoice;
        var insertion = pending?.ControlledDrawInsertion;
        if (combat is null || pending is null) return null;
        if (insertion is
                { Stage: "card_model",
                  Operation.Kind:
                    PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile } &&
            (PrototypeGameEngine
                 .IsAutomaticHandDrawHookDrawInsertionContinuation(
                     combat, pending) ||
             PrototypeGameEngine
                 .IsAutomaticBeforeHandDrawHookDrawInsertionContinuation(
                     combat, pending)))
        {
            SemanticDrawInsertionChoiceV111.ValidateBoundary(run);
            var ownerId = pending.SourceCardInstanceId is { } sourceCardId
                ? "card_instance:" + sourceCardId
                : "effect:" + insertion.Operation.Kind;
            var siteId = StochasticSiteRegistryV111
                .CombatRandomDrawAttackCardModelSiteId;
            return SemanticGenerationChoiceV111.CreateEvent(
                state.Fingerprint, siteId, ownerId,
                state.GeneratedCardTranscript.Count(decision =>
                    decision.Event.SiteId == siteId),
                insertion.CandidateCardIds,
                combat.Stochastic.NativeRng?.RunStreams is not null
                    ? "run.combat_card_generation" : "prototype.combat");
        }
        if (PrototypeGameEngine
                .IsAutomaticHandDrawGeneratedCardContinuation(
                    combat, pending))
        {
            var siteId = StochasticSiteRegistryV111
                .CombatGeneratedCardSelectSiteId;
            return SemanticGenerationChoiceV111.DescribePending(
                run, state.Fingerprint,
                state.GeneratedCardTranscript.Count(decision =>
                    decision.Event.SiteId == siteId));
        }
        return null;
    }

    internal static SemanticPotionGenerationEventV111?
        DescribePendingPotionGeneration(SemanticCombatDrawV111 state)
    {
        if (!state.AutomaticHandDraw ||
            state.Run.World?.Combat is not { } combat ||
            combat.PendingChoice is not { } pending ||
            !PrototypeGameEngine
                .IsAutomaticHandDrawPotionGenerationContinuation(
                    combat, pending))
            return null;
        return SemanticPotionGenerationChoiceV111.DescribePending(
            state.Run, state.Fingerprint,
            state.PotionGenerationTranscript.Count(decision =>
                decision.Event.SiteId ==
                    SemanticPotionGenerationChoiceV111.SiteId));
    }

    internal static SemanticPoolShuffleEventV111?
        DescribePendingPoolShuffle(SemanticCombatDrawV111 state)
    {
        if (!state.AutomaticHandDraw ||
            state.Run.World?.Combat is not { } combat ||
            combat.PendingChoice is not { } pending ||
            !PrototypeGameEngine
                .IsAutomaticHandDrawPoolShuffleContinuation(
                    combat, pending))
            return null;
        if (SemanticPoolShuffleChoiceV111.SiteIdForOperation(
                pending.ControlledPoolShuffle!.Operation.Kind.ToString()) is null)
            throw new InvalidDataException(
                "Semantic hand-draw pool shuffle has no registered site.");
        return SemanticPoolShuffleChoiceV111.DescribePending(
            state.Run, state.Fingerprint,
            state.PoolShuffleTranscript.Length);
    }

    internal static SemanticCardSelectionEventV111?
        DescribePendingCardSelection(SemanticCombatDrawV111 state)
    {
        if (!state.AutomaticHandDraw ||
            state.Run.World?.Combat is not { } combat ||
            combat.PendingChoice is not { } pending ||
            !PrototypeGameEngine
                .IsAutomaticHandDrawHookCardSelectionContinuation(
                    combat, pending))
            return null;
        return SemanticCardSelectionEngineV111.DescribePending(
            state.Run, CanonicalJson.Sha256(state.Run));
    }

    internal static SemanticCardTransformEventV111?
        DescribePendingCardTransform(SemanticCombatDrawV111 state)
    {
        if (!state.AutomaticHandDraw ||
            state.Run.World?.Combat is not { } combat ||
            combat.PendingChoice is not { } pending ||
            !PrototypeGameEngine
                .IsAutomaticHandDrawHookCardTransformContinuation(
                    combat, pending))
            return null;
        return SemanticCardTransformChoiceV111.DescribePending(
            state.Run, CanonicalJson.Sha256(state.Run),
            state.CardTransformTranscript.Length);
    }

    internal static SemanticCardShortlistEventV111?
        DescribePendingCardShortlist(SemanticCombatDrawV111 state)
    {
        if (!state.AutomaticHandDraw ||
            state.Run.World?.Combat is not { } combat ||
            combat.PendingChoice is not { } pending ||
            !PrototypeGameEngine
                .IsAutomaticHandDrawHookCardShortlistContinuation(
                    combat, pending))
            return null;
        return SemanticCardShortlistChoiceV111.DescribePending(
            state.Run, CanonicalJson.Sha256(state.Run),
            state.CardShortlistTranscript.Length);
    }

    internal static SemanticCostEventV111?
        DescribePendingRandomHandCost(SemanticCombatDrawV111 state)
    {
        if (!state.AutomaticHandDraw ||
            state.Run.World?.Combat is not { } combat ||
            combat.PendingChoice is not { } pending ||
            pending.ControlledRandomCost?.Operation.Kind !=
                PrototypeCombatEffectKind
                    .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed ||
            !PrototypeGameEngine
                .IsAutomaticHandDrawHookRandomHandCostContinuation(
                    combat, pending))
            return null;
        return SemanticRandomHandCostChoiceV111.DescribeCostEvent(
            state.Run, CanonicalJson.Sha256(state.Run),
            state.RandomHandCostTranscript.Length);
    }

    internal static SemanticHandCostSelectionEventV111?
        DescribePendingHandCostSelection(SemanticCombatDrawV111 state)
    {
        if (!state.AutomaticHandDraw ||
            state.Run.World?.Combat is not { } combat ||
            combat.PendingChoice is not { } pending ||
            pending.ControlledRandomCost is not { } randomCost ||
            randomCost.Operation.Kind != PrototypeCombatEffectKind
                .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed ||
            !PrototypeGameEngine
                .IsAutomaticHandDrawHookRandomHandCostContinuation(
                    combat, pending))
            return null;
        var modelIds = randomCost.EligibleCardInstanceIds.Select(id =>
            combat.Cards.Single(card => card.InstanceId == id).CardId)
            .ToArray();
        return SemanticHandCostSelectionChoiceV111.CreateEvent(
            CanonicalJson.Sha256(state.Run), randomCost, modelIds,
            state.HandCostSelectionTranscript.Length,
            combat.Stochastic.NativeRng is not null
                ? SemanticHandCostSelectionChoiceV111.NativeStreamId
                : SemanticHandCostSelectionChoiceV111.StreamId);
    }

    internal static SemanticDrawInsertionEventV111?
        DescribePendingDrawInsertion(SemanticCombatDrawV111 state)
    {
        var run = state.Run;
        var combat = run.World?.Combat;
        var pending = combat?.PendingChoice;
        var insertion = pending?.ControlledDrawInsertion;
        if (insertion is null)
            return null;
        if (combat is not null && pending is not null &&
            (PrototypeGameEngine
                 .IsAutomaticHandDrawHookDrawInsertionContinuation(
                     combat, pending) ||
             PrototypeGameEngine
                 .IsAutomaticBeforeHandDrawHookDrawInsertionContinuation(
                     combat, pending)))
        {
            if (insertion.Stage != "position")
                return null;
            return SemanticDrawInsertionChoiceV111
                .DescribeAutomaticSemanticDrawContinuation(
                    run, state.Fingerprint);
        }
        if (insertion.AfterShuffleRelic is null)
            return null;
        var semanticDraw = insertion.SemanticDrawContinuation;
        bool automaticSegment = state.AutomaticHandDraw &&
            semanticDraw?.AutomaticSemanticHandDrawSegment == true;
        bool standaloneSegment = !state.AutomaticHandDraw &&
            semanticDraw?.StandaloneSemanticDrawSegment == true;
        bool automaticProgressMatches = automaticSegment && combat is not null &&
            SemanticCombatDrawV111.TryGetAutomaticSemanticHandDrawProgress(
                combat, out int requested, out int remaining) &&
            requested == state.RequestedCount &&
            remaining == state.RemainingCount;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true } ||
            pending is not { ChoiceId: "controlled_draw_insertion",
                OuterChoiceContinuation: not null } ||
            insertion.Stage != "position" ||
            semanticDraw is null ||
            (!standaloneSegment && !automaticSegment) ||
            (standaloneSegment &&
             combat!.AutomaticPipelineContinuation is not null) ||
            (automaticSegment && !automaticProgressMatches) ||
            semanticDraw.RemainingCount != state.RemainingCount ||
            !PrototypeGameEngine.ValidateControlledDrawInsertion(
                combat!, insertion))
            throw new InvalidDataException(
                "After-shuffle insertion is not a valid semantic draw stage.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat!,
            "After-shuffle insertion is not a valid semantic draw stage.",
            "After-shuffle insertion is not a valid semantic draw stage.");

        return SemanticDrawInsertionChoiceV111.DescribePendingEvent(
            run, state.Fingerprint, insertion.RequestOrdinal,
            insertion.RequestOwnerId, insertion.RequestOwnerId,
            appendPileIdentityToLegacyOwner: true,
            appendPileIdentityToNativeOwner: true);
    }

    internal static SemanticCardSelectionEventV111?
        DescribePendingStratagemSelection(SemanticCombatDrawV111 state)
    {
        var run = state.Run;
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        bool automaticSelection = state.AutomaticHandDraw &&
            choice?.AutomaticSemanticStratagemDrawSelection == true;
        bool standaloneSelection = !state.AutomaticHandDraw &&
            choice?.SemanticStratagemDrawSelection == true;
        if (combat is not { SemanticDeck:
                { Mode: SemanticDeckStateV111.NatureMode, Pending: null } })
            return null;
        int expectedSelections =
            PrototypeGameEngine.ExpectedStratagemSelectionCount(combat);
        bool automaticProgressMatches = automaticSelection &&
            SemanticCombatDrawV111.TryGetAutomaticSemanticHandDrawProgress(
                combat, out int requested, out int remaining) &&
            requested == state.RequestedCount &&
            remaining == state.RemainingCount;
        if (
            (standaloneSelection && !combat.IsPlayerTurn) ||
            choice is not { ChoiceId: "select_cards" } ||
            choice.Selection.SourceZone != PrototypeCardZone.DrawPile ||
            state.RemainingCount <= 0 ||
            (!automaticSelection && !standaloneSelection) ||
            (automaticSelection &&
             choice!.SemanticStratagemDrawSelection) ||
            (standaloneSelection &&
             choice!.AutomaticSemanticStratagemDrawSelection) ||
            (automaticSelection && !automaticProgressMatches) ||
            (standaloneSelection &&
             (combat!.AutomaticPipelineContinuation is not null ||
              !HasStandaloneSemanticDrawOperation(choice!))) ||
            expectedSelections <= 0)
            return null;

        var decision = SemanticCardSelectionEngineV111.DescribePending(
            run, CanonicalJson.Sha256(run));
        if (decision.MaximumSelections != expectedSelections ||
            decision.MinimumSelections != expectedSelections)
            throw new InvalidDataException(
                "Standalone Stratagem selection differs from its power stacks.");
        return decision;
    }

    private static bool HasStandaloneSemanticDrawOperation(
        PendingCombatChoiceState choice)
    {
        return PrototypeGameEngine.TryGetStandaloneSemanticDrawOperation(
            choice, out _);
    }

    public static SemanticCombatDrawV111 Begin(
        RunState source, int cardCount, bool fromHandDraw = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (cardCount <= 0) throw new ArgumentOutOfRangeException(nameof(cardCount));
        if (source.Phase != RunPhase.Combat ||
            source.World?.Combat is not { } combat ||
            combat.Stochastic.SemanticDeck is not null ||
            combat.PendingChoice is not null ||
            combat.AutomaticPipelineContinuation is not null)
            throw new InvalidOperationException(
                "Expected an ordinary pre-draw combat without a pending choice.");
        var baseRun = source.Fork();
        var world = baseRun.World!;
        var semanticCombat =
            SemanticDrawPileAdapterV111.InitializeSemanticDrawPile(
                world.Combat!, SemanticDeckEngineV111.BeginNature(
                    world.Combat!.DrawPile));
        var attached = baseRun with
        {
            World = world with
            {
                Combat = semanticCombat
            }
        };
        var (advanced, remaining) = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.BeginSemanticCombatDraw(
                attached, cardCount, fromHandDraw));
        var result = new SemanticCombatDrawV111(
            baseRun, advanced, cardCount, remaining, fromHandDraw);
        Validate(result);
        return result;
    }

    /// <summary>
    /// Enter the genuine BeforeHandDraw / DrawPlayerHand automatic pipeline
    /// stage from an existing, source-shaped checkpoint. Previous enemy
    /// turns, intent choices and preceding automatic steps belong to
    /// the supplied source; neither they nor BeforeHandDraw are rerun on
    /// subsequent Nature resolutions.
    /// </summary>
    public static SemanticCombatDrawV111 BeginAutomaticHandDraw(RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var sourceCombat = source.World?.Combat;
        if (source.Phase != RunPhase.Combat ||
            sourceCombat is null ||
            PrototypeGameEngine
                .ClassifyAutomaticPipelineResumeBoundary(source) !=
            PrototypeAutomaticPipelineResumeBoundaryKindV111.DrawPlayerHand)
            throw new InvalidOperationException(
                "Semantic automatic hand draw needs a clean DrawPlayerHand checkpoint.");
        try
        {
            SemanticDrawPileAdapterV111
                .ValidateSemanticHandDrawSourceDeck(sourceCombat,
                    "Semantic automatic hand draw has an unsupported source deck.");
        }
        catch (InvalidDataException exception)
        {
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.DrawPlayerHand " +
                "operation=unsupported_source_deck",
                exception);
        }
        var origin = source.Fork();
        var world = origin.World!;
        var originCombat = world.Combat!;
        var semanticCombat = sourceCombat.Stochastic.SemanticDeck is null
            ? SemanticDrawPileAdapterV111.InitializeSemanticDrawPile(
                originCombat,
                SemanticDeckEngineV111.BeginNature(originCombat.DrawPile))
            : originCombat;
        var attached = origin with
        {
            World = world with
            {
                Combat = semanticCombat
            }
        };
        PrototypeDrawCardsResumeResultV111 handDraw =
            ControlledChanceGuardV111.ExecuteGuarded(() =>
                PrototypeGameEngine.BeginSemanticAutomaticHandDraw(attached));
        var run = handDraw.Run;
        int requested = handDraw.RequireRequestedCount();
        int remaining = handDraw.RemainingCount;
        var result = new SemanticCombatDrawV111(
            origin, run, requested, remaining, true, AutomaticHandDraw: true);
        Validate(result);
        return result;
    }

    /// <summary>Nature chooses a physical instance at the saved draw site;
    /// the normal mechanical DrawCards routine then performs the draw,
    /// cost checks and CardDrawn hooks before another Nature request.</summary>
    public static SemanticCombatDrawV111 Resolve(
        SemanticCombatDrawV111 state, string fingerprint, int selectedIndex)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic combat draw fingerprint.");
        if (state.DrawHookChoicePending is not null)
            throw new InvalidOperationException(
                "Resolve the pending CardDrawn hook choice by action ID.");
        // Choice resume owners take and fork the immutable checkpoint after
        // validating the selected typed outcome.
        var run = state.Run;
        var world = run.World!;
        var combat = world.Combat!;

        if (state.GeneratedCardPending is { } generatedCardEvent)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= generatedCardEvent.CandidateCardIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            return ResolveGeneratedCard(
                state, state.Fingerprint,
                generatedCardEvent.CandidateCardIds[selectedIndex]);
        }

        if (state.RandomPileCardPending is { } pileCardEvent)
        {
            var decision = SemanticRandomPileCardEngineV111.Resolve(
                pileCardEvent, selectedIndex);
            var (progress, _) = SemanticChoiceResumeV111.Resume(run, decision);
            _ = progress.RequireRequestedCount();
            var nextPileChoice = PublishDrawProgress(state, progress) with
            {
                Stochastic = state.Stochastic.AppendRandomPileCard(decision)
            };
            nextPileChoice = RecordNewSemanticDrawChoices(
                state, nextPileChoice);
            Validate(nextPileChoice);
            return nextPileChoice;
        }

        if (state.DrawInsertionPending is { } insertionEvent)
        {
            var decision = SemanticDrawInsertionChoiceV111.Select(
                insertionEvent, insertionEvent.EventId, selectedIndex);
            int drawnBefore = combat.CounterState.CardsDrawnThisCombat;
            var resumedInsertion = SemanticChoiceResumeV111.Resume(
                run, decision);
            var resumed = resumedInsertion.Run;
            int insertionRemaining = state.AutomaticHandDraw
                ? GetAutomaticHandDrawRemainingCount(resumed)
                : PrototypeGameEngine.GetSemanticDrawRemainingCount(
                    resumed, state.RemainingCount, drawnBefore);
            PrototypeDrawCardsResumeResultV111 insertionProgress;
            if (state.AutomaticHandDraw &&
                resumed.World?.Combat is
                    { PendingChoice: null,
                      PendingKnowledgeDemonChoice: null,
                      SemanticDeck: { Pending: null } } completedInsertion &&
                TryGetAutomaticSemanticHandDrawProgress(
                    completedInsertion, out int requestedCount,
                    out int savedRemaining))
            {
                var handDraw = PrototypeGameEngine
                    .ResumeSemanticAutomaticHandDraw(
                        resumed, savedRemaining);
                if (handDraw.RequireRequestedCount() != requestedCount)
                    throw new InvalidDataException(
                        "Nested draw insertion changed the active hand-draw request.");
                insertionProgress = handDraw;
            }
            else
                insertionProgress = new PrototypeDrawCardsResumeResultV111(
                    resumed, insertionRemaining,
                    PipelineBoundary: resumedInsertion.PipelineBoundary);
            var nextInsertion = PublishDrawProgress(
                state, insertionProgress) with
            {
                Stochastic = state.Stochastic.AppendDrawInsertion(decision)
            };
            nextInsertion = RecordNewSemanticDrawChoices(
                state, nextInsertion);
            Validate(nextInsertion);
            return nextInsertion;
        }

        if (state.CostPending is { } costRequest)
        {
            costRequest.Validate();
            if (selectedIndex < 0 || selectedIndex >= costRequest.SupportCount)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            // The chosen cost does not consume the native combat RNG cursor:
            // it is an explicit counterfactual Nature decision.
            var decision = new RandomIndexDecisionV111(
                costRequest, selectedIndex, "semantic-controlled-v1");
            decision.Validate();
            var resumedCost = ControlledChanceContinuationExecutorV111
                .ResolveSelectedIndex<
                SemanticCombatDrawV111,
                PrototypeDrawCardsResumeResultV111>(
                state, costRequest, selectedIndex, (checkpoint, replayedIndex) =>
                {
                    var checkpointCombat = checkpoint.Run.World?.Combat ??
                        throw new InvalidDataException(
                            "Semantic draw-cost checkpoint lost combat.");
                    var progress = SemanticChoiceResumeV111
                        .ResumeCombatDrawCost(
                            checkpoint.Run, replayedIndex,
                            checkpoint.RemainingCount,
                            checkpointCombat.CounterState
                                .CardsDrawnThisCombat);
                    return progress;
                });
            var costNext = PublishDrawProgress(state, resumedCost) with
            {
                Stochastic = state.Stochastic.AppendCost(decision)
            };
            var costNextWithChronology = RecordNewSemanticDrawChoices(
                state, costNext);
            Validate(costNextWithChronology);
            return costNextWithChronology;
        }

        bool automaticBeforeHandDrawEffectDraw =
            state.AutomaticHandDraw && combat.PendingChoice is
                { } beforeHandDrawEffectChoice &&
            PrototypeGameEngine
                .IsAutomaticBeforeHandDrawHookSemanticDrawContinuation(
                    combat, beforeHandDrawEffectChoice);
        if (combat.PendingChoice?.ControlledSemanticDraw?
                .StandaloneSemanticDrawSegment == true ||
            automaticBeforeHandDrawEffectDraw ||
            (state.AutomaticHandDraw &&
             combat.PendingChoice is { } nestedEffectDraw &&
             PrototypeGameEngine
                 .IsAutomaticHandDrawHookSemanticDrawContinuation(
                     combat, nestedEffectDraw)))
        {
            var effectDrawDecision = SemanticDrawChoiceV111.Select(
                state.Pending ?? throw new InvalidDataException(
                    "Semantic card-effect draw lost its pending event."),
                selectedIndex);
            var resumedEffectDraw = SemanticChoiceResumeV111.ResumeEffectDraw(
                run, effectDrawDecision,
                allowEngineOwnedPrototypeReward: false);
            if (automaticBeforeHandDrawEffectDraw)
            {
                var beforeProgress = ResumeNestedAutomaticHandDraw(
                    state, resumedEffectDraw,
                    "BeforeHandDraw effect-draw");
                var beforeNext = PublishDrawProgress(
                    state, beforeProgress);
                beforeNext = RecordNewSemanticDrawChoices(
                    state, beforeNext);
                Validate(beforeNext);
                return beforeNext;
            }
            var resumedDraw = resumedEffectDraw.Run;
            int remaining;
            if (state.AutomaticHandDraw &&
                resumedDraw.World?.Combat is
                    { PendingChoice: null,
                      PendingKnowledgeDemonChoice: null,
                      SemanticDeck: { Pending: null } } completedChildDraw &&
                TryGetAutomaticSemanticHandDrawProgress(
                    completedChildDraw, out int requestedCount,
                    out int savedRemaining))
            {
                var handDraw = PrototypeGameEngine
                    .ResumeSemanticAutomaticHandDraw(
                        resumedDraw, savedRemaining);
                if (handDraw.RequireRequestedCount() != requestedCount)
                    throw new InvalidDataException(
                        "Nested effect draw changed the active hand-draw request.");
                resumedDraw = handDraw.Run;
                remaining = handDraw.RemainingCount;
            }
            else
                remaining = state.AutomaticHandDraw
                    ? GetAutomaticHandDrawRemainingCount(resumedDraw)
                    : PrototypeGameEngine.GetSemanticDrawRemainingCount(
                        resumedDraw, state.RemainingCount,
                        combat.CounterState.CardsDrawnThisCombat);
            var drawProgress = new PrototypeDrawCardsResumeResultV111(
                resumedDraw, remaining);
            var drawNext = PublishDrawProgress(state, drawProgress);
            drawNext = RecordNewSemanticDrawChoices(state, drawNext);
            Validate(drawNext);
            return drawNext;
        }

        if (combat.Stochastic.SemanticDeck?.Pending is null ||
            state.RemainingCount <= 0)
            throw new InvalidOperationException("No semantic draw card pending.");
        var cardDecision = SemanticDrawChoiceV111.Select(
            state.Pending ?? throw new InvalidDataException(
                "Semantic draw frame lost its pending request."),
            selectedIndex);
        var (progress, _) =
            SemanticChoiceResumeV111.ResumeDrawSegment(
                run, cardDecision, state.RemainingCount,
                state.FromHandDraw);
        var next = PublishDrawProgress(state, progress);
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

    public static SemanticCombatDrawV111 ResolveGeneratedCard(
        SemanticCombatDrawV111 state, string fingerprint, string cardId)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic generated-card fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var pending = state.GeneratedCardPending ??
            throw new InvalidOperationException(
                "No semantic draw-generated card choice is pending.");
        var decision = SemanticGenerationChoiceV111.Select(
            pending, pending.EventId, cardId);
        var resumed = pending.SiteId ==
                StochasticSiteRegistryV111
                    .CombatRandomDrawAttackCardModelSiteId
            ? SemanticChoiceResumeV111.Resume(state.Run, decision)
            : SemanticChoiceResumeV111
                .ResumeGeneratedCardForHandDraw(state.Run, decision);
        var resumedCombat = resumed.Run.World?.Combat ??
            throw new InvalidDataException(
                "Semantic generated-card choice removed combat.");
        PrototypeDrawCardsResumeResultV111 progress;
        if (pending.SiteId ==
            StochasticSiteRegistryV111.CombatRandomDrawAttackCardModelSiteId)
        {
            if (resumedCombat.PendingChoice?.ControlledDrawInsertion is not
                { Stage: "position" })
                throw new InvalidDataException(
                    "Semantic generated-card choice did not advance to its insertion ticket.");
            int remaining = GetAutomaticHandDrawRemainingCount(resumed.Run);
            progress = PrototypeDrawCardsResumeResultV111.ForHandDraw(
                resumed.Run, state.RequestedCount, remaining,
                resumed.PipelineBoundary);
        }
        else
            progress = ResumeNestedAutomaticHandDraw(
                state, resumed, "Generated-card");

        progress = progress.EnsurePipelineBoundary();
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendGeneratedCard(decision)
        };
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

    public static SemanticCombatDrawV111 ResolvePotionGeneration(
        SemanticCombatDrawV111 state, string fingerprint, string potionId)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw potion-generation fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(potionId);
        var pending = state.PotionGenerationPending ??
            throw new InvalidOperationException(
                "No semantic draw potion-generation choice is pending.");
        var decision = SemanticPotionGenerationChoiceV111.Select(
            pending, pending.EventId, potionId);
        var resumed = SemanticChoiceResumeV111
            .ResumePotionGenerationForHandDraw(state.Run, decision);
        var progress = ResumeNestedAutomaticHandDraw(
            state, resumed, "Potion-generation");

        progress = progress.EnsurePipelineBoundary();
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendPotionGeneration(decision)
        };
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

    public static SemanticCombatDrawV111 ResolvePoolShuffle(
        SemanticCombatDrawV111 state, string fingerprint, int swapIndex)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw pool-shuffle fingerprint.");
        var pending = state.PoolShufflePending ??
            throw new InvalidOperationException(
                "No semantic draw pool-shuffle choice is pending.");
        var decision = SemanticPoolShuffleChoiceV111.Select(
            pending, pending.EventId, swapIndex);
        var resumed = SemanticChoiceResumeV111
            .ResumePoolShuffleForHandDraw(state.Run, decision);
        var progress = ResumeNestedAutomaticHandDraw(
            state, resumed, "Pool-shuffle");
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendPoolShuffle(decision)
        };
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

    public static SemanticCombatDrawV111 ResolveCardSelection(
        SemanticCombatDrawV111 state, string fingerprint, string eventId,
        string actionId)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw card-selection fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        var pending = state.CardSelectionPending ??
            throw new InvalidOperationException(
                "No semantic draw card-selection choice is pending.");
        var decision = SemanticCardSelectionEngineV111.Select(
            pending, eventId, actionId);
        var resumed = SemanticChoiceResumeV111.Resume(state.Run, decision);
        var progress = ResumeNestedAutomaticHandDraw(
            state, resumed, "Card-selection");
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendCardSelection(decision)
        };
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

    public static SemanticCombatDrawV111 ResolveCardTransform(
        SemanticCombatDrawV111 state, string fingerprint, string eventId,
        string cardId)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw card-transform fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var pending = state.CardTransformPending ??
            throw new InvalidOperationException(
                "No semantic draw card-transform choice is pending.");
        var decision = SemanticCardTransformChoiceV111.Select(
            pending, eventId, cardId);
        var resumed = SemanticChoiceResumeV111.Resume(state.Run, decision);
        var progress = ResumeNestedAutomaticHandDraw(
            state, resumed, "Card-transform");
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendCardTransform(decision)
        };
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

    public static SemanticCombatDrawV111 ResolveCardShortlist(
        SemanticCombatDrawV111 state, string fingerprint, string eventId,
        long cardInstanceId)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw card-shortlist fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = state.CardShortlistPending ??
            throw new InvalidOperationException(
                "No semantic draw card-shortlist choice is pending.");
        var decision = SemanticCardShortlistChoiceV111.Select(
            pending, eventId, cardInstanceId);
        var resumed = SemanticChoiceResumeV111.Resume(state.Run, decision);
        var progress = ResumeNestedAutomaticHandDraw(
            state, resumed, "Card-shortlist");
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendCardShortlist(decision)
        };
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

    public static SemanticCombatDrawV111 ResolveRandomHandCost(
        SemanticCombatDrawV111 state, string fingerprint, string eventId,
        int selectedCost)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw random-hand-cost fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = state.RandomHandCostPending ??
            throw new InvalidOperationException(
                "No semantic draw random-hand-cost event is pending.");
        var decision = SemanticCostChoiceV111.Select(
            pending, eventId, selectedCost);
        var resumed = SemanticChoiceResumeV111.Resume(state.Run, decision);
        var progress = ResumeNestedAutomaticHandDraw(
            state, resumed, "Random-hand-cost");
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendRandomHandCost(decision)
        };
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

    public static SemanticCombatDrawV111 ResolveHandCostSelection(
        SemanticCombatDrawV111 state, string fingerprint, string eventId,
        long cardInstanceId)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw hand-cost-selection fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = state.HandCostSelectionPending ??
            throw new InvalidOperationException(
                "No semantic draw hand-cost-selection event is pending.");
        var decision = SemanticHandCostSelectionChoiceV111.Select(
            pending, eventId, cardInstanceId);
        var resumed = SemanticChoiceResumeV111.Resume(state.Run, decision);
        var progress = ResumeNestedAutomaticHandDraw(
            state, resumed, "Hand-cost selection");
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendHandCostSelection(decision)
        };
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

    private static PrototypeDrawCardsResumeResultV111
        ResumeNestedAutomaticHandDraw(
            SemanticCombatDrawV111 state,
            SemanticChoiceResumeResultV111 resumed,
            string choiceKind) =>
        PrototypeGameEngine.ResumeSemanticAutomaticHandDrawAfterNestedChoice(
            state.Run, resumed.Run, resumed.Boundary, choiceKind,
            state.RequestedCount, state.RemainingCount);

    public static SemanticCombatDrawV111 ResolveStratagemSelection(
        SemanticCombatDrawV111 state, string fingerprint, string actionId)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic combat draw fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        var pending = state.StratagemSelectionPending ??
            throw new InvalidOperationException(
                "No semantic Stratagem selection is pending.");
        int drawnBefore = state.Run.World!.Combat!
            .CounterState.CardsDrawnThisCombat;
        var (progress, decision) =
            SemanticChoiceResumeV111.ResumeStratagemSelection(
                state.Run, pending, actionId,
                state.RemainingCount, state.AutomaticHandDraw,
                drawnBefore);
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendStratagemSelection(decision)
        };
        var nextWithChronology = RecordNewSemanticDrawChoices(
            state, next);
        Validate(nextWithChronology);
        return nextWithChronology;
    }

    public static SemanticCombatDrawV111 ResolveDrawHookChoice(
        SemanticCombatDrawV111 state, string fingerprint, string actionId)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic combat draw fingerprint.");
        var pending = state.DrawHookChoicePending ??
            throw new InvalidOperationException(
                "No nested CardDrawn hook choice is pending.");
        int drawnBefore = state.Run.World!.Combat!
            .CounterState.CardsDrawnThisCombat;
        var (progress, decision) =
            SemanticChoiceResumeV111.ResumeDrawHookChoice(
                state.Run, pending, actionId,
                state.RequestedCount, state.RemainingCount, drawnBefore);
        _ = progress.RequireRequestedCount();
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendDrawHookChoice(decision)
        };
        var nextWithChronology = RecordNewSemanticDrawChoices(
            state, next);
        Validate(nextWithChronology);
        return nextWithChronology;
    }

    private static int GetAutomaticHandDrawRemainingCount(RunState run)
    {
        var combat = run.World?.Combat ?? throw new InvalidDataException(
            "Automatic semantic hand-draw resume removed combat.");
        if (TryGetAutomaticSemanticHandDrawProgress(
                combat, out _, out int remaining))
            return remaining;
        var cursor = combat.AutomaticPipelineContinuation;
        if (cursor is not null &&
            (cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                 cursor.Mechanics) ||
             cursor.Mechanics.IsImmediatelyAfterStep(
                 PrototypeAutomaticStepKind.DrawPlayerHand)) &&
            !cursor.Stochastic.HasSavedHandDrawProgress())
            return 0;
        throw new InvalidDataException(
            "Automatic semantic hand-draw resume lost its cursor.");
    }

    public static SemanticCombatDrawV111 ResolveTarget(
        SemanticCombatDrawV111 state, string fingerprint,
        string eventId, int enemyInstanceId)
    {
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic combat-draw target fingerprint.");
        var pending = state.TargetPending ??
            throw new InvalidOperationException(
                "No automatic hand-draw target event is pending.");
        var decision = SemanticTargetChoiceV111.Select(
            pending, eventId, enemyInstanceId);
        var resumedTarget = SemanticChoiceResumeV111.ResumeTarget(
            state.Run, decision);
        if (resumedTarget.Run.World?.Combat is not
                { PendingKnowledgeDemonChoice: null } resumedCombat ||
            !TryGetAutomaticSemanticHandDrawProgress(
                resumedCombat, out int requested, out int remaining) ||
            requested != state.RequestedCount ||
            remaining != state.RemainingCount)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                "operation=unsupported_resumed_target_boundary");

        if (resumedCombat.PendingChoice is
                { ControlledTarget: not null } childTarget)
        {
            var taggedCombat = resumedCombat with
            {
                PendingChoice = childTarget with
                {
                    AutomaticSemanticDrawTargetChoice = true
                }
            };
            var taggedRun = resumedTarget.Run with
            {
                World = resumedTarget.Run.World! with
                {
                    Combat = taggedCombat
                }
            };
            SemanticTargetChoiceV111.ValidateBoundary(taggedRun);
            PrototypeStateInvariants.Validate(taggedRun);
            var chained = state with
            {
                Run = taggedRun,
                Stochastic = state.Stochastic.AppendTarget(decision)
            };
            chained = RecordNewSemanticDrawChoices(state, chained);
            Validate(chained);
            return chained;
        }
        if (resumedCombat.PendingChoice is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                "operation=unsupported_resumed_target_boundary");

        var progress = PrototypeGameEngine
            .ResumeSemanticAutomaticHandDrawAfterNestedChoice(
                state.Run, resumedTarget.Run, resumedTarget.Boundary,
                "random-target", state.RequestedCount,
                state.RemainingCount);
        var next = PublishDrawProgress(state, progress) with
        {
            Stochastic = state.Stochastic.AppendTarget(decision)
        };
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

    public static string Save(SemanticCombatDrawV111 state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    /// <summary>Full replay from the supplied source combat and each
    /// semantic Nature decision, with exact resulting RunState comparison.
    /// An altered pending event or transcript fails even if locally valid.</summary>
    public static SemanticCombatDrawV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var stored = JsonSerializer.Deserialize<SemanticCombatDrawV111>(json)
            ?? throw new InvalidDataException("Null semantic combat draw.");
        return Replay(stored);
    }

    internal static SemanticCombatDrawV111 Replay(
        SemanticCombatDrawV111 stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        Validate(stored);
        var reconstructed = stored.AutomaticHandDraw
            ? BeginAutomaticHandDraw(stored.Origin)
            : Begin(stored.Origin, stored.RequestedCount, stored.FromHandDraw);
        var transcriptCursor =
            new SemanticCombatDrawReplayCursorV111(stored);
        while (transcriptCursor.HasRemaining)
        {
            if (reconstructed.DrawHookChoicePending is { } hookChoice)
            {
                var hookDecision = transcriptCursor.NextDrawHookChoice();
                hookDecision.Validate();
                if (CanonicalJson.Sha256(hookDecision.Event) !=
                    CanonicalJson.Sha256(hookChoice))
                    throw new InvalidDataException(
                        "Draw-hook choice transcript diverges from source support.");
                reconstructed = ResolveDrawHookChoice(
                    reconstructed, reconstructed.Fingerprint,
                    hookDecision.ActionId);
                continue;
            }
            if (reconstructed.TargetPending is { } target)
            {
                var targetDecision = transcriptCursor.NextTargetChoice();
                targetDecision.Validate();
                if (CanonicalJson.Sha256(targetDecision.Event) !=
                    CanonicalJson.Sha256(target))
                    throw new InvalidDataException(
                        "Automatic hand-draw target transcript diverges from source support.");
                reconstructed = ResolveTarget(
                    reconstructed, reconstructed.Fingerprint,
                    targetDecision.Event.EventId,
                    targetDecision.SelectedEnemyInstanceId);
                continue;
            }
            if (reconstructed.StratagemSelectionPending is { } selection)
            {
                var selectionDecision =
                    transcriptCursor.NextStratagemChoice();
                selectionDecision.Validate();
                if (CanonicalJson.Sha256(selectionDecision.Event) !=
                    CanonicalJson.Sha256(selection))
                    throw new InvalidDataException(
                        "Stratagem selection transcript diverges from source support.");
                reconstructed = ResolveStratagemSelection(
                    reconstructed, reconstructed.Fingerprint,
                    selectionDecision.ActionId);
                continue;
            }
            if (reconstructed.RandomPileCardPending is { } pileCard)
            {
                var pileCardDecision = transcriptCursor.NextPileCardChoice();
                pileCardDecision.Validate();
                if (CanonicalJson.Sha256(pileCardDecision.Event) !=
                    CanonicalJson.Sha256(pileCard))
                    throw new InvalidDataException(
                        "Random pile-card transcript diverges from source support.");
                reconstructed = Resolve(
                    reconstructed, reconstructed.Fingerprint,
                    pileCardDecision.SelectedIndex);
                continue;
            }
            if (reconstructed.GeneratedCardPending is { } generatedCard)
            {
                var generatedDecision =
                    transcriptCursor.NextGeneratedCardChoice();
                generatedDecision.Validate();
                if (CanonicalJson.Sha256(generatedDecision.Event) !=
                    CanonicalJson.Sha256(generatedCard))
                    throw new InvalidDataException(
                        "Generated-card transcript diverges from source support.");
                reconstructed = ResolveGeneratedCard(
                    reconstructed, reconstructed.Fingerprint,
                    generatedDecision.SelectedCardId);
                continue;
            }
            if (reconstructed.PotionGenerationPending is { } potion)
            {
                var potionDecision =
                    transcriptCursor.NextPotionGenerationChoice();
                potionDecision.Validate();
                if (CanonicalJson.Sha256(potionDecision.Event) !=
                    CanonicalJson.Sha256(potion))
                    throw new InvalidDataException(
                        "Potion-generation transcript diverges from source support.");
                reconstructed = ResolvePotionGeneration(
                    reconstructed, reconstructed.Fingerprint,
                    potionDecision.SelectedPotionId);
                continue;
            }
            if (reconstructed.PoolShufflePending is { } poolShuffle)
            {
                var poolShuffleDecision =
                    transcriptCursor.NextPoolShuffleChoice();
                poolShuffleDecision.Validate();
                if (CanonicalJson.Sha256(poolShuffleDecision.Event) !=
                    CanonicalJson.Sha256(poolShuffle))
                    throw new InvalidDataException(
                        "Pool-shuffle transcript diverges from source support.");
                reconstructed = ResolvePoolShuffle(
                    reconstructed, reconstructed.Fingerprint,
                    poolShuffleDecision.SelectedSwapIndex);
                continue;
            }
            if (reconstructed.CardSelectionPending is { } cardSelection)
            {
                var selectionDecision =
                    transcriptCursor.NextCardSelectionChoice();
                selectionDecision.Validate();
                if (CanonicalJson.Sha256(selectionDecision.Event) !=
                    CanonicalJson.Sha256(cardSelection))
                    throw new InvalidDataException(
                        "Card-selection transcript diverges from source support.");
                reconstructed = ResolveCardSelection(
                    reconstructed, reconstructed.Fingerprint,
                    selectionDecision.Event.EventId,
                    selectionDecision.ActionId);
                continue;
            }
            if (reconstructed.CardTransformPending is { } cardTransform)
            {
                var transformDecision =
                    transcriptCursor.NextCardTransformChoice();
                transformDecision.Validate();
                if (CanonicalJson.Sha256(transformDecision.Event) !=
                    CanonicalJson.Sha256(cardTransform))
                    throw new InvalidDataException(
                        "Card-transform transcript diverges from source support.");
                reconstructed = ResolveCardTransform(
                    reconstructed, reconstructed.Fingerprint,
                    transformDecision.Event.EventId,
                    transformDecision.SelectedCardId);
                continue;
            }
            if (reconstructed.CardShortlistPending is { } cardShortlist)
            {
                var shortlistDecision =
                    transcriptCursor.NextCardShortlistChoice();
                shortlistDecision.Validate();
                if (CanonicalJson.Sha256(shortlistDecision.Event) !=
                    CanonicalJson.Sha256(cardShortlist))
                    throw new InvalidDataException(
                        "Card-shortlist transcript diverges from source support.");
                reconstructed = ResolveCardShortlist(
                    reconstructed, reconstructed.Fingerprint,
                    shortlistDecision.Event.EventId,
                    shortlistDecision.SelectedCardInstanceId);
                continue;
            }
            if (reconstructed.RandomHandCostPending is { } randomHandCost)
            {
                var costDecision =
                    transcriptCursor.NextRandomHandCostChoice();
                costDecision.Validate();
                if (CanonicalJson.Sha256(costDecision.Event) !=
                    CanonicalJson.Sha256(randomHandCost))
                    throw new InvalidDataException(
                        "Random-hand-cost transcript diverges from source support.");
                reconstructed = ResolveRandomHandCost(
                    reconstructed, reconstructed.Fingerprint,
                    costDecision.Event.EventId, costDecision.SelectedCost);
                continue;
            }
            if (reconstructed.HandCostSelectionPending is
                { } handCostSelection)
            {
                var handCostDecision =
                    transcriptCursor.NextHandCostSelectionChoice();
                handCostDecision.Validate();
                if (CanonicalJson.Sha256(handCostDecision.Event) !=
                    CanonicalJson.Sha256(handCostSelection))
                    throw new InvalidDataException(
                        "Hand-cost-selection transcript diverges from source support.");
                reconstructed = ResolveHandCostSelection(
                    reconstructed, reconstructed.Fingerprint,
                    handCostDecision.Event.EventId,
                    handCostDecision.SelectedCardInstanceId);
                continue;
            }
            if (reconstructed.DrawInsertionPending is { } insertion)
            {
                var insertionDecision = transcriptCursor.NextInsertion();
                insertionDecision.Validate();
                if (CanonicalJson.Sha256(insertionDecision.Event) !=
                    CanonicalJson.Sha256(insertion))
                    throw new InvalidDataException(
                        "Draw-insertion transcript diverges from source support.");
                reconstructed = Resolve(
                    reconstructed, reconstructed.Fingerprint,
                    insertionDecision.SelectedPosition);
                continue;
            }
            if (reconstructed.CostPending is { } currentCost)
            {
                var cost = transcriptCursor.NextCostChoice();
                cost.Validate();
                var requested = cost.Request;
                if (cost.ProviderMode != "semantic-controlled-v1" ||
                    cost.ProbabilityStatus != "UNVERIFIED" ||
                    requested.SiteId != currentCost.SiteId ||
                    requested.StreamId != currentCost.StreamId ||
                    requested.OwnerId != currentCost.OwnerId ||
                    requested.Ordinal != currentCost.Ordinal ||
                    requested.SourceVersion != currentCost.SourceVersion ||
                    requested.OperationKind != currentCost.OperationKind ||
                    requested.BoundedCount != currentCost.BoundedCount ||
                    !requested.OrderedCandidates.SequenceEqual(
                        currentCost.OrderedCandidates))
                    throw new InvalidDataException(
                        "Semantic drawn-cost transcript diverges from source request.");
                reconstructed = Resolve(
                    reconstructed, reconstructed.Fingerprint, cost.SelectedIndex);
                continue;
            }
            if (reconstructed.Pending is not { } pending)
                throw new InvalidDataException(
                    "Semantic draw/cost stage mismatch in provenance replay.");
            var decision = transcriptCursor.NextDeckChoice();
            if (pending.EventId != decision.EventId ||
                pending.Ordinal != decision.Ordinal ||
                pending.SiteId != decision.SiteId ||
                !pending.CandidateCardInstanceIds.SequenceEqual(
                    decision.CandidateCardInstanceIds) ||
                pending.Purpose != decision.Purpose ||
                pending.TopOffset != decision.TopOffset)
                throw new InvalidDataException(
                    "Semantic draw transcript disagrees with replayed mechanics.");
            reconstructed = Resolve(
                reconstructed, reconstructed.Fingerprint, decision.SelectedIndex);
        }
        transcriptCursor.ValidateComplete();
        var comparisonSnapshot = stored;
        if (stored.BoundaryTranscript.Length == 0 &&
            reconstructed.BoundaryTranscript.Length > 0)
            comparisonSnapshot = stored with
            {
                BoundaryTranscript = reconstructed.BoundaryTranscript,
                BoundaryTranscriptVersion =
                    reconstructed.BoundaryTranscriptVersion
            };
        if (stored.BoundaryTranscriptVersion <
                SemanticCombatDrawStochasticStateV111
                    .CurrentBoundaryTranscriptVersion &&
            stored.RandomHandCostTranscript.Length == 0 &&
            stored.HandCostSelectionTranscript.Length == 0)
            comparisonSnapshot = comparisonSnapshot with
            {
                BoundaryTranscriptVersion =
                    reconstructed.BoundaryTranscriptVersion
            };
        if (CanonicalJson.Sha256(reconstructed) !=
            CanonicalJson.Sha256(comparisonSnapshot))
            throw new InvalidDataException(
                "Semantic combat draw snapshot fails complete provenance replay.");
        return reconstructed;
    }

    public static void Validate(SemanticCombatDrawV111 state) =>
        state.Validate();
}
