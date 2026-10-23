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
    [JsonIgnore] public SemanticCardSelectionEventV111?
        StratagemSelectionPending =>
            SemanticCombatDrawEngineV111
                .DescribePendingStratagemSelection(this);
    [JsonIgnore] public SemanticDrawHookChoiceEventV111?
        DrawHookChoicePending =>
            SemanticDrawHookChoiceEngineV111.DescribePending(Run);
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
        DrawInsertionPending is null && StratagemSelectionPending is null &&
        DrawHookChoicePending is null && CostPending is null &&
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
        bool completedAutomaticHandDraw = this.AutomaticHandDraw &&
            this.RequestedCount == 0 && this.RemainingCount == 0 &&
            completedHookCursor is { } completedCursor &&
            completedCursor.Mechanics.IsImmediatelyAfterStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) &&
            completedCursor.Stochastic.HandDrawProgress is null &&
            this.Run.World?.Combat is
                { PendingChoice: null,
                  PendingKnowledgeDemonChoice: null,
                  SemanticDeck: { Pending: null } };
        if (this.RequestedCount < 0 ||
            this.RemainingCount < 0 ||
            this.RemainingCount > this.RequestedCount ||
            (this.Mechanics.HasAutomaticPipelineDrawProgress &&
             (this.Mechanics.RequestedCount != 0 ||
              this.Mechanics.RemainingCount != 0)) ||
            (this.RequestedCount == 0 && randomPileCardPending is null &&
             drawInsertionPending is null &&
             drawHookChoicePending?.BeforeHandDraw != true &&
             !automaticBeforeHandDrawPoolShuffle &&
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
        bool semanticBeforeHandDrawProgress =
            IsSemanticBeforeHandDrawProgress(pipelineCursor);
        if (this.AutomaticHandDraw)
        {
            bool awaiting = deck.Pending is not null || cost is not null ||
                drawInsertionPending is not null ||
                stratagemSelectionPending is not null ||
                drawHookChoicePending is not null ||
                automaticBeforeHandDrawPoolShuffle;
            if (randomPileCardPending is not null)
            {
                if (pipelineCursor is null ||
                    !pipelineCursor.Mechanics.IsAtStep(
                        PrototypeAutomaticStepKind.DrawPlayerHand) ||
                    !semanticBeforeHandDrawProgress ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    combat.PendingChoice?.ControlledRandomPileCard is null ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw choice cursor is inconsistent.");
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
                  pipelineCursor.Stochastic.HandDrawProgress is not null)))
                throw new InvalidDataException(
                    "Semantic automatic hand-draw cursor is inconsistent.");
        }
        if ((combat.PendingChoice is not null && cost is null &&
             randomPileCardPending is null &&
             drawInsertionPending is null &&
             !standaloneSemanticDrawPending &&
             stratagemSelectionPending is null &&
             drawHookChoicePending is null &&
             !automaticBeforeHandDrawPoolShuffle) ||
            (cost is not null &&
             (deck.Pending is not null ||
              cost.RemainingCount != this.RemainingCount ||
              cost.FromHandDraw != this.FromHandDraw ||
              cost.EventDepth != 0 ||
              !PrototypeGameEngine.ValidateControlledDrawCost(
                  this.Run.Player, combat, cost with { FromHandDraw = true }))) ||
            (deck.Pending is null && cost is null &&
             drawInsertionPending is null &&
             stratagemSelectionPending is null &&
             drawHookChoicePending is null &&
             this.RemainingCount != 0) ||
            (deck.Pending is not null && this.RemainingCount <= 0))
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
            if (this.RemainingCount <= 0 ||
                insertion?.SemanticDrawContinuation?.RemainingCount !=
                    this.RemainingCount ||
                insertionEvent.Request.BoundedCount !=
                    insertionEvent.EligibleDrawPileInstanceIds.Length + 1)
                throw new InvalidDataException(
                    "Semantic draw-insertion cursor is inconsistent.");
        }
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

    private static bool IsSemanticBeforeHandDrawProgress(
        PrototypeAutomaticPipelineContinuationState? cursor) =>
        cursor?.Stochastic.HandDrawProgress is
            { Kind: PrototypeAutomaticHandDrawProgressKindV111
                .SemanticBeforeHandDraw,
              RequestedCount: null,
              RemainingCount: null };
}

public static class SemanticCombatDrawEngineV111
{
    private static SemanticCombatDrawV111 PublishDrawProgress(
        SemanticCombatDrawV111 state, RunState run,
        int? requestedCount = null, int? remainingCount = null)
    {
        var next = state with { Run = run };
        if (requestedCount is { } requested)
            next = next with { RequestedCount = requested };
        if (remainingCount is { } remaining)
            next = next with { RemainingCount = remaining };
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

    internal static SemanticDrawInsertionEventV111?
        DescribePendingDrawInsertion(SemanticCombatDrawV111 state)
    {
        var run = state.Run;
        var combat = run.World?.Combat;
        var pending = combat?.PendingChoice;
        var insertion = pending?.ControlledDrawInsertion;
        if (insertion is null || insertion.AfterShuffleRelic is null)
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
        RunState advanced;
        int remaining;
        using (ControlledChanceGuardV111.Enter())
            (advanced, remaining) =
                PrototypeGameEngine.BeginSemanticCombatDraw(
                    attached, cardCount, fromHandDraw);
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
        RunState run;
        int requested;
        int remaining;
        using (ControlledChanceGuardV111.Enter())
            (run, requested, remaining) =
                PrototypeGameEngine.BeginSemanticAutomaticHandDraw(attached);
        if (run.World?.Combat?.PendingChoice?.ControlledRandomPileCard
                is not null &&
            run.World.Combat.AutomaticPipelineContinuation?
                .Stochastic.HandDrawProgress?.Kind ==
                    PrototypeAutomaticHandDrawProgressKindV111
                        .SemanticBeforeHandDraw)
        {
            requested = 0;
            remaining = 0;
        }
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

        if (state.RandomPileCardPending is { } pileCardEvent)
        {
            var decision = SemanticRandomPileCardEngineV111.Resolve(
                pileCardEvent, selectedIndex);
            RunState resumed;
            int requested;
            int pileCardRemaining;
            (resumed, requested, pileCardRemaining, _) =
                SemanticChoiceResumeV111.Resume(run, decision);
            var nextPileChoice = PublishDrawProgress(
                state, resumed, requested, pileCardRemaining) with
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
            var (resumed, _) = SemanticChoiceResumeV111.Resume(
                run, decision);
            var insertionRemaining =
                PrototypeGameEngine.GetSemanticDrawRemainingCount(
                    resumed, state.RemainingCount, drawnBefore);
            var nextInsertion = PublishDrawProgress(
                state, resumed, remainingCount: insertionRemaining) with
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
                SemanticCombatDrawResumeResultV111>(
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
            var costNext = PublishDrawProgress(
                state, resumedCost.Run,
                remainingCount: resumedCost.RemainingCount) with
            {
                Stochastic = state.Stochastic.AppendCost(decision)
            };
            var costNextWithChronology = RecordNewSemanticDrawChoices(
                state, costNext);
            Validate(costNextWithChronology);
            return costNextWithChronology;
        }

        if (combat.PendingChoice?.ControlledSemanticDraw?
                .StandaloneSemanticDrawSegment == true)
        {
            var effectDrawDecision = SemanticDrawChoiceV111.Select(
                state.Pending ?? throw new InvalidDataException(
                    "Semantic card-effect draw lost its pending event."),
                selectedIndex);
            var (resumedDraw, _) = SemanticChoiceResumeV111.ResumeEffectDraw(
                run, effectDrawDecision,
                allowEngineOwnedPrototypeReward: false);
            var drawNext = PublishDrawProgress(
                state, resumedDraw,
                remainingCount: PrototypeGameEngine.GetSemanticDrawRemainingCount(
                    resumedDraw, state.RemainingCount,
                    combat.CounterState.CardsDrawnThisCombat));
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
        var (advanced, remaining, _) =
            SemanticChoiceResumeV111.ResumeDrawSegment(
                run, cardDecision, state.RemainingCount,
                state.FromHandDraw);
        var next = PublishDrawProgress(
            state, advanced, remainingCount: remaining);
        next = RecordNewSemanticDrawChoices(state, next);
        Validate(next);
        return next;
    }

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
        var next = PublishDrawProgress(
            state, progress.Run,
            remainingCount: progress.RemainingCount) with
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
        var (run, requested, remaining, decision) =
            SemanticChoiceResumeV111.ResumeDrawHookChoice(
                state.Run, pending, actionId,
                state.RequestedCount, state.RemainingCount, drawnBefore);
        var next = PublishDrawProgress(
            state, run, requested, remaining) with
        {
            Stochastic = state.Stochastic.AppendDrawHookChoice(decision)
        };
        var nextWithChronology = RecordNewSemanticDrawChoices(
            state, next);
        Validate(nextWithChronology);
        return nextWithChronology;
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
        if (CanonicalJson.Sha256(reconstructed) !=
            CanonicalJson.Sha256(comparisonSnapshot))
            throw new InvalidDataException(
                "Semantic combat draw snapshot fails complete provenance replay.");
        return reconstructed;
    }

    public static void Validate(SemanticCombatDrawV111 state) =>
        state.Validate();
}
