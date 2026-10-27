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
        init
        {
            var current = Mechanics;
            var priorProgress = ReadAutomaticPipelineDrawProgress(
                current.Run, current.AutomaticHandDraw);
            var nextProgress = ReadAutomaticPipelineDrawProgress(
                value, current.AutomaticHandDraw);
            int requested = current.RequestedCount;
            int remaining = current.RemainingCount;
            if (nextProgress is not null)
            {
                requested = 0;
                remaining = 0;
            }
            else if (priorProgress is { } progress)
            {
                requested = value.World?.Combat?.AutomaticPipelineContinuation
                    ?.ControlledSemanticDrawRequested ??
                    (progress.Requested > 0
                        ? progress.Requested : requested);
                remaining = 0;
            }
            Mechanics = current with
            {
                Continuation = current.Continuation with { Current = value },
                RequestedCount = requested,
                RemainingCount = remaining
            };
        }
    }

    public int RequestedCount
    {
        get => ReadAutomaticPipelineDrawProgress(
            Mechanics.Run, Mechanics.AutomaticHandDraw)?.Requested ??
            Mechanics.RequestedCount;
        init
        {
            var mechanics = Mechanics;
            Mechanics = mechanics with
            {
                RequestedCount = ReadAutomaticPipelineDrawProgress(
                    mechanics.Run, mechanics.AutomaticHandDraw) is null
                    ? value : 0
            };
        }
    }

    public int RemainingCount
    {
        get => ReadAutomaticPipelineDrawProgress(
            Mechanics.Run, Mechanics.AutomaticHandDraw)?.Remaining ??
            Mechanics.RemainingCount;
        init
        {
            var mechanics = Mechanics;
            Mechanics = mechanics with
            {
                RemainingCount = ReadAutomaticPipelineDrawProgress(
                    mechanics.Run, mechanics.AutomaticHandDraw) is null
                    ? value : 0
            };
        }
    }

    public bool FromHandDraw
    {
        get => Mechanics.FromHandDraw;
        init => Mechanics = Mechanics with { FromHandDraw = value };
    }

    public bool AutomaticHandDraw
    {
        get => Mechanics.AutomaticHandDraw;
        init
        {
            var mechanics = Mechanics;
            var pipelineProgress = ReadAutomaticPipelineDrawProgress(
                mechanics.Run, value);
            Mechanics = mechanics with
            {
                AutomaticHandDraw = value,
                RequestedCount = pipelineProgress is null
                    ? mechanics.RequestedCount : 0,
                RemainingCount = pipelineProgress is null
                    ? mechanics.RemainingCount : 0
            };
        }
    }

    internal static bool HasAutomaticPipelineDrawCursor(
        RunState run, bool automaticHandDraw) =>
        ReadAutomaticPipelineDrawProgress(run, automaticHandDraw) is not null;

    private static (int Requested, int Remaining)?
        ReadAutomaticPipelineDrawProgress(
            RunState run, bool automaticHandDraw)
    {
        if (!automaticHandDraw || run.World?.Combat is not { } combat ||
            combat.AutomaticPipelineContinuation is not { } cursor)
            return null;
        if (cursor.ControlledSemanticBeforeHandDrawPending)
            return (0, 0);
        if (!cursor.Stochastic.TryGetSemanticHandDrawProgress(
                cursor.Mechanics, out var requested, out var remaining))
            return null;
        return (requested, remaining);
    }

    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public SemanticDeckEventV111? Pending =>
        Run.World?.Combat?.SemanticDeck is
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
                relic ? "combat.card_draw.relic_energy_cost"
                      : "combat.card_draw.slither_energy_cost",
                "semantic.combat", "card_instance:" + card.InstanceId +
                    ":model:" + card.CardId + ":stage:" + cost.Stage,
                CostTranscript.Length, [],
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
        (Run.World?.Combat?.SemanticDeck?.Transcript ?? [])
            .Skip(Origin.World?.Combat?.SemanticDeck?.Transcript?.Length ?? 0)
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

    public void Validate()
    {
        this.Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.SemanticCombatDraw);
        this.Stochastic.Validate();
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
            completedCursor.NextStepIndex ==
                PrototypeGameEngine.SemanticHandDrawStepIndex + 1 &&
            completedCursor.Stochastic.HandDrawProgress is null &&
            this.Run.World?.Combat is
                { PendingChoice: null,
                  PendingKnowledgeDemonChoice: null,
                  SemanticDeck: { Pending: null } };
        if (this.RequestedCount < 0 ||
            this.RemainingCount < 0 ||
            this.RemainingCount > this.RequestedCount ||
            (ReadAutomaticPipelineDrawProgress(
                 this.Run, this.AutomaticHandDraw) is not null &&
             (this.Mechanics.RequestedCount != 0 ||
              this.Mechanics.RemainingCount != 0)) ||
            (this.RequestedCount == 0 && randomPileCardPending is null &&
             drawInsertionPending is null &&
             drawHookChoicePending?.BeforeHandDraw != true &&
             !automaticBeforeHandDrawPoolShuffle &&
             !completedAutomaticHandDraw) ||
            this.Origin.Phase != RunPhase.Combat ||
            this.Origin.World?.Combat is not { } originCombat ||
            (!this.AutomaticHandDraw && originCombat.SemanticDeck is not null) ||
            (this.AutomaticHandDraw &&
             originCombat.SemanticDeck is { } prior &&
             (this.Run.World?.Combat?.SemanticDeck?.Transcript?.Length ?? 0) <
                 (prior.Transcript?.Length ?? 0)) ||
            originCombat.PendingChoice is not null ||
            (!this.AutomaticHandDraw &&
             originCombat.AutomaticPipelineContinuation is not null) ||
            (this.AutomaticHandDraw &&
             (originCombat.AutomaticPipelineContinuation is not { } initial ||
              !initial.Mechanics.IsAtStep(
                  PrototypeAutomaticStepKind.DrawPlayerHand) ||
              initial.ControlledSemanticDrawRemaining is not null ||
              initial.ControlledSemanticBeforeHandDrawPending)) ||
            this.Run.Phase != RunPhase.Combat ||
            this.Run.World?.Combat is not { } combat ||
            combat.SemanticDeck is not { } deck ||
            (!this.AutomaticHandDraw &&
             combat.AutomaticPipelineContinuation is not null) ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new InvalidDataException("Invalid semantic combat draw stage.");
        if (this.AutomaticHandDraw && originCombat.SemanticDeck is not null)
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
        if (this.AutomaticHandDraw)
        {
            var cursor = combat.AutomaticPipelineContinuation;
            bool awaiting = deck.Pending is not null || cost is not null ||
                drawInsertionPending is not null ||
                stratagemSelectionPending is not null ||
                drawHookChoicePending is not null ||
                automaticBeforeHandDrawPoolShuffle;
            if (randomPileCardPending is not null)
            {
                if (cursor is null ||
                    !cursor.Mechanics.IsAtStep(
                        PrototypeAutomaticStepKind.DrawPlayerHand) ||
                    !cursor.ControlledSemanticBeforeHandDrawPending ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    combat.PendingChoice?.ControlledRandomPileCard is null ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw choice cursor is inconsistent.");
            }
            else if (drawHookChoicePending?.BeforeHandDraw == true)
            {
                if (cursor is null ||
                    !cursor.Mechanics.IsAtStep(
                        PrototypeAutomaticStepKind.DrawPlayerHand) ||
                    !cursor.ControlledSemanticBeforeHandDrawPending ||
                    cursor.ControlledSemanticDrawRemaining is not null ||
                    cursor.ControlledSemanticDrawRequested is not null ||
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
                if (cursor is null ||
                    !cursor.Mechanics.IsAtStep(
                        PrototypeAutomaticStepKind.DrawPlayerHand) ||
                    !cursor.ControlledSemanticBeforeHandDrawPending ||
                    cursor.ControlledSemanticDrawRemaining is not null ||
                    cursor.ControlledSemanticDrawRequested is not null ||
                    this.RequestedCount != 0 || this.RemainingCount != 0 ||
                    !beforeHandDrawShuffleChoice.AutomaticSemanticDrawHookChoice ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw pool-shuffle cursor is inconsistent.");
            }
            else if (cursor is null ||
                cursor.ControlledSemanticBeforeHandDrawPending ||
                (awaiting &&
                 (!PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat) ||
                  cursor.ControlledSemanticDrawRemaining != this.RemainingCount ||
                  cursor.ControlledSemanticDrawRequested != this.RequestedCount)) ||
                (!awaiting &&
                 (this.RemainingCount != 0 ||
                  cursor.NextStepIndex !=
                      PrototypeGameEngine.SemanticHandDrawStepIndex + 1 ||
                  cursor.ControlledSemanticDrawRemaining is not null)))
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
                      ? combat.AutomaticPipelineContinuation?
                            .ControlledSemanticBeforeHandDrawPending == true &&
                        combat.AutomaticPipelineContinuation.Mechanics.IsAtStep(
                            PrototypeAutomaticStepKind.DrawPlayerHand) &&
                        this.RequestedCount == 0 && this.RemainingCount == 0
                      : PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat) &&
                        combat.AutomaticPipelineContinuation!
                            .ControlledSemanticDrawRemaining == this.RemainingCount &&
                        combat.AutomaticPipelineContinuation
                            .ControlledSemanticDrawRequested == this.RequestedCount)
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
                ? PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat) &&
                  combat.AutomaticPipelineContinuation!
                      .ControlledSemanticDrawRemaining == this.RemainingCount &&
                  combat.AutomaticPipelineContinuation
                      .ControlledSemanticDrawRequested == this.RequestedCount
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
}

public static class SemanticCombatDrawEngineV111
{
    private sealed record SemanticDrawCostResumeV111(
        RunState Run, int Remaining);

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
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true } ||
            pending is not { ChoiceId: "controlled_draw_insertion",
                OuterChoiceContinuation: not null } ||
            insertion.Stage != "position" ||
            semanticDraw is null ||
            (!standaloneSegment && !automaticSegment) ||
            (standaloneSegment &&
             combat!.AutomaticPipelineContinuation is not null) ||
            (automaticSegment &&
             (!PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat!) ||
              combat.AutomaticPipelineContinuation!
                  .ControlledSemanticDrawRemaining != state.RemainingCount ||
              combat.AutomaticPipelineContinuation
                  .ControlledSemanticDrawRequested != state.RequestedCount)) ||
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
            (automaticSelection &&
             (!PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat!) ||
              combat.AutomaticPipelineContinuation!
                  .ControlledSemanticDrawRemaining != state.RemainingCount ||
              combat.AutomaticPipelineContinuation
                  .ControlledSemanticDrawRequested != state.RequestedCount)) ||
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
            combat.SemanticDeck is not null ||
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
            sourceCombat.PendingChoice is not null ||
            sourceCombat.PendingKnowledgeDemonChoice is not null ||
            sourceCombat.AutomaticPipelineContinuation is not { } cursor ||
            !cursor.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            cursor.ControlledDrawRemaining is not null ||
            cursor.ControlledDrawCostRemaining is not null ||
            cursor.ControlledSemanticDrawRemaining is not null ||
            cursor.OpeningStage is not null ||
            cursor.PendingPostDispatchEventKind is not null)
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
        var semanticCombat = sourceCombat.SemanticDeck is null
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
                .ControlledSemanticBeforeHandDrawPending == true)
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
        SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_combat_input_validate", () => Validate(state));
        var inputFingerprint = SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_combat_input_fingerprint", () => state.Fingerprint);
        if (inputFingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic combat draw fingerprint.");
        if (state.DrawHookChoicePending is not null)
            throw new InvalidOperationException(
                "Resolve the pending CardDrawn hook choice by action ID.");
        var run = SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_combat_run_fork", state.Run.Fork);
        var world = run.World!;
        var combat = world.Combat!;

        if (state.RandomPileCardPending is { } pileCardEvent)
        {
            var decision = SemanticRandomPileCardEngineV111.Resolve(
                pileCardEvent, selectedIndex);
            RunState resumed;
            int requested;
            int pileCardRemaining;
            using (ControlledChanceGuardV111.Enter())
                (resumed, requested, pileCardRemaining) =
                    PrototypeGameEngine.ResumeSemanticRandomPileCardChoice(
                        run, decision);
            var nextPileChoice = PublishDrawProgress(
                state, resumed, requested, pileCardRemaining) with
            {
                RandomPileCardTranscript = state.RandomPileCardTranscript
                    .Append(decision.Fork()).ToArray()
            };
            Validate(nextPileChoice);
            return nextPileChoice;
        }

        if (state.DrawInsertionPending is { } insertionEvent)
        {
            var decision = SemanticDrawInsertionChoiceV111.Select(
                insertionEvent, insertionEvent.EventId, selectedIndex);
            int drawnBefore = combat.CounterState.CardsDrawnThisCombat;
            var resumed = SemanticDrawInsertionChoiceV111.Resume(
                run, decision);
            var insertionRemaining =
                PrototypeGameEngine.GetSemanticDrawRemainingCount(
                    resumed, state.RemainingCount, drawnBefore);
            var nextInsertion = PublishDrawProgress(
                state, resumed, remainingCount: insertionRemaining) with
            {
                DrawInsertionTranscript = state.DrawInsertionTranscript
                    .Append(decision.Fork()).ToArray()
            };
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
                SemanticCombatDrawV111, SemanticDrawCostResumeV111>(
                state, costRequest, selectedIndex, (checkpoint, replayedIndex) =>
                {
                    RunState checkpointRun = checkpoint.Run.Fork();
                    var checkpointCombat = checkpointRun.World!.Combat!;
                    if (checkpointCombat.PendingChoice?.ControlledSemanticDraw?
                            .StandaloneSemanticDrawSegment == true)
                    {
                        var advanced = SemanticCostChoiceV111
                            .ResumeEffectDrawCost(
                                checkpointRun, replayedIndex,
                                allowEngineOwnedPrototypeReward: false);
                        return new SemanticDrawCostResumeV111(
                            advanced,
                            PrototypeGameEngine.GetSemanticDrawRemainingCount(
                                advanced, checkpoint.RemainingCount,
                                checkpointCombat.CounterState
                                    .CardsDrawnThisCombat));
                    }

                    using (ControlledChanceGuardV111.Enter())
                    {
                        var (advanced, remaining) = PrototypeGameEngine
                            .ResumeSemanticCombatDrawCost(
                                checkpointRun, replayedIndex);
                        return new SemanticDrawCostResumeV111(
                            advanced, remaining);
                    }
                });
            var costNext = PublishDrawProgress(
                state, resumedCost.Run,
                remainingCount: resumedCost.Remaining) with
            {
                CostTranscript = state.CostTranscript.Append(decision).ToArray()
            };
            Validate(costNext);
            return costNext;
        }

        if (combat.PendingChoice?.ControlledSemanticDraw?
                .StandaloneSemanticDrawSegment == true)
        {
            var effectDrawDecision = SemanticDrawChoiceV111.Select(
                state.Pending ?? throw new InvalidDataException(
                    "Semantic card-effect draw lost its pending event."),
                selectedIndex);
            var resumedDraw = SemanticDrawChoiceV111
                .ResumeControlledEffectDraw(
                    run, effectDrawDecision,
                    allowEngineOwnedPrototypeReward: false);
            var drawNext = PublishDrawProgress(
                state, resumedDraw,
                remainingCount: PrototypeGameEngine.GetSemanticDrawRemainingCount(
                    resumedDraw, state.RemainingCount,
                    combat.CounterState.CardsDrawnThisCombat));
            Validate(drawNext);
            return drawNext;
        }

        if (combat.SemanticDeck?.Pending is null ||
            state.RemainingCount <= 0)
            throw new InvalidOperationException("No semantic draw card pending.");
        var cardDecision = SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_combat_select",
            () => SemanticDrawChoiceV111.Select(
                state.Pending ?? throw new InvalidDataException(
                    "Semantic draw frame lost its pending request."),
                selectedIndex));
        var resolvedCombat = SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_combat_commit",
            () => SemanticDrawChoiceV111.Commit(combat, cardDecision));
        run = run with
        {
            World = world with
            {
                Combat = resolvedCombat
            }
        };
        var (advanced, remaining) = SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_combat_engine_mechanics", () =>
            {
                using (ControlledChanceGuardV111.Enter())
                    return PrototypeGameEngine.BeginSemanticCombatDraw(
                        run, state.RemainingCount, state.FromHandDraw);
            });
        SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_combat_recorded_choice",
            () => SemanticDrawChoiceV111.ValidateRecordedChoice(
                advanced, cardDecision.Event, cardDecision.SelectedIndex));
        var next = SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_combat_publish",
            () => PublishDrawProgress(
                state, advanced, remainingCount: remaining));
        SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_combat_output_validate", () => Validate(next));
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
        var (resumedRun, remaining, decision) =
            PrototypeGameEngine.ResumeSemanticStratagemSelection(
                state.Run.Fork(), pending, actionId,
                state.RemainingCount, state.AutomaticHandDraw,
                drawnBefore);
        var next = PublishDrawProgress(
            state, resumedRun, remainingCount: remaining) with
        {
            StratagemSelectionTranscript =
                state.StratagemSelectionTranscript
                    .Append(decision.Fork()).ToArray()
        };
        Validate(next);
        return next;
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
            PrototypeGameEngine.ResumeSemanticDrawHookChoice(
                state.Run.Fork(), pending, actionId,
                state.RequestedCount, state.RemainingCount, drawnBefore);
        var next = PublishDrawProgress(
            state, run, requested, remaining) with
        {
            DrawHookChoiceTranscript = state.DrawHookChoiceTranscript
                .Append(decision.Fork()).ToArray()
        };
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
        int deckCursor = 0;
        int costCursor = 0;
        int pileCardCursor = 0;
        int insertionCursor = 0;
        int stratagemSelectionCursor = 0;
        int drawHookChoiceCursor = 0;
        while (deckCursor < stored.Transcript.Length ||
               costCursor < stored.CostTranscript.Length ||
               pileCardCursor < stored.RandomPileCardTranscript.Length ||
               insertionCursor < stored.DrawInsertionTranscript.Length ||
               stratagemSelectionCursor <
                   stored.StratagemSelectionTranscript.Length ||
               drawHookChoiceCursor <
                   stored.DrawHookChoiceTranscript.Length)
        {
            if (reconstructed.DrawHookChoicePending is { } hookChoice)
            {
                if (drawHookChoiceCursor >=
                    stored.DrawHookChoiceTranscript.Length)
                    throw new InvalidDataException(
                        "Missing nested CardDrawn hook choice in replay.");
                var hookDecision =
                    stored.DrawHookChoiceTranscript[drawHookChoiceCursor];
                hookDecision.Validate();
                if (CanonicalJson.Sha256(hookDecision.Event) !=
                    CanonicalJson.Sha256(hookChoice))
                    throw new InvalidDataException(
                        "Draw-hook choice transcript diverges from source support.");
                reconstructed = ResolveDrawHookChoice(
                    reconstructed, reconstructed.Fingerprint,
                    hookDecision.ActionId);
                drawHookChoiceCursor++;
                continue;
            }
            if (reconstructed.StratagemSelectionPending is { } selection)
            {
                if (stratagemSelectionCursor >=
                    stored.StratagemSelectionTranscript.Length)
                    throw new InvalidDataException(
                        "Missing standalone Stratagem selection in replay.");
                var selectionDecision =
                    stored.StratagemSelectionTranscript[
                        stratagemSelectionCursor];
                selectionDecision.Validate();
                if (CanonicalJson.Sha256(selectionDecision.Event) !=
                    CanonicalJson.Sha256(selection))
                    throw new InvalidDataException(
                        "Stratagem selection transcript diverges from source support.");
                reconstructed = ResolveStratagemSelection(
                    reconstructed, reconstructed.Fingerprint,
                    selectionDecision.ActionId);
                stratagemSelectionCursor++;
                continue;
            }
            if (reconstructed.RandomPileCardPending is { } pileCard)
            {
                if (pileCardCursor >=
                    stored.RandomPileCardTranscript.Length)
                    throw new InvalidDataException(
                        "Missing semantic random pile-card decision in replay.");
                var pileCardDecision =
                    stored.RandomPileCardTranscript[pileCardCursor];
                pileCardDecision.Validate();
                if (CanonicalJson.Sha256(pileCardDecision.Event) !=
                    CanonicalJson.Sha256(pileCard))
                    throw new InvalidDataException(
                        "Random pile-card transcript diverges from source support.");
                reconstructed = Resolve(
                    reconstructed, reconstructed.Fingerprint,
                    pileCardDecision.SelectedIndex);
                pileCardCursor++;
                continue;
            }
            if (reconstructed.DrawInsertionPending is { } insertion)
            {
                if (insertionCursor >=
                    stored.DrawInsertionTranscript.Length)
                    throw new InvalidDataException(
                        "Missing semantic draw-insertion decision in replay.");
                var insertionDecision =
                    stored.DrawInsertionTranscript[insertionCursor];
                insertionDecision.Validate();
                if (CanonicalJson.Sha256(insertionDecision.Event) !=
                    CanonicalJson.Sha256(insertion))
                    throw new InvalidDataException(
                        "Draw-insertion transcript diverges from source support.");
                reconstructed = Resolve(
                    reconstructed, reconstructed.Fingerprint,
                    insertionDecision.SelectedPosition);
                insertionCursor++;
                continue;
            }
            if (reconstructed.CostPending is { } currentCost)
            {
                if (costCursor >= stored.CostTranscript.Length)
                    throw new InvalidDataException(
                        "Missing semantic drawn-cost decision in replay.");
                var cost = stored.CostTranscript[costCursor];
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
                costCursor++;
                continue;
            }
            if (reconstructed.Pending is not { } pending ||
                deckCursor >= stored.Transcript.Length)
                throw new InvalidDataException(
                    "Semantic draw/cost stage mismatch in provenance replay.");
            var decision = stored.Transcript[deckCursor];
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
            deckCursor++;
        }
        if (CanonicalJson.Sha256(reconstructed) != CanonicalJson.Sha256(stored))
            throw new InvalidDataException(
                "Semantic combat draw snapshot fails complete provenance replay.");
        return reconstructed;
    }

    public static void Validate(SemanticCombatDrawV111 state) =>
        state.Validate();
}
