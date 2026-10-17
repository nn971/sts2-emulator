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
    [JsonIgnore]
    public SemanticCombatDrawStochasticStateV111 Stochastic { get; init; }
        = new();
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

    public SemanticCombatDrawV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork(),
        Stochastic = Stochastic.Fork()
    };
}

public static class SemanticCombatDrawEngineV111
{
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
                combat!, insertion) ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode, Pending: null } deck ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "After-shuffle insertion is not a valid semantic draw stage.");

        var pile = (long[])insertion.EligibleDrawPile.Clone();
        var cardModelId = insertion.SelectedCardId!;
        var request = new RandomIndexRequestV111(
            insertion.RequestSiteId,
            "prototype.combat",
            insertion.RequestOwnerId + ":card:" + cardModelId +
                ":draw_pile:" + CanonicalJson.Sha256(pile),
            insertion.RequestOrdinal, [],
            OperationKind: "bounded_integer",
            BoundedCount: pile.Length + 1);
        request.Validate();
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            Request = request,
            GeneratedCardModelId = cardModelId,
            EligibleDrawPileInstanceIds = pile
        });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId = SemanticDrawInsertionEventV111.EventSchemaId,
            BoundaryFingerprint = state.Fingerprint,
            SupportFingerprint = supportFingerprint
        });
        var result = new SemanticDrawInsertionEventV111(
            SemanticDrawInsertionEventV111.EventSchemaId, eventId,
            state.Fingerprint, supportFingerprint, request, cardModelId,
            pile);
        result.Validate();
        return result;
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
                    { Mode: SemanticDeckStateV111.NatureMode,
                    Pending: null } } ||
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
            !combat.PlayerPowers.Any(power =>
                power.PowerId == "proto.power.stratagem" &&
                power.Stacks > 0 && combat.Hand.Length < 10))
            return null;

        var decision = SemanticCardSelectionEngineV111.DescribePending(
            run, CanonicalJson.Sha256(run));
        int expectedSelections = ExpectedStratagemSelectionCount(combat);
        if (decision.MaximumSelections != expectedSelections ||
            decision.MinimumSelections != expectedSelections)
            throw new InvalidDataException(
                "Standalone Stratagem selection differs from its power stacks.");
        return decision;
    }

    private static bool HasStandaloneSemanticDrawOperation(
        PendingCombatChoiceState choice)
    {
        return TryGetStandaloneSemanticDrawOperation(choice, out _);
    }

    private static bool TryGetStandaloneSemanticDrawOperation(
        PendingCombatChoiceState choice,
        out PrototypeQueuedOperation operation)
    {
        if (choice.Continuation.FirstOrDefault(item =>
                item.Kind == PrototypeCombatEffectKind.DrawCards &&
                item.StandaloneSemanticDrawSegment) is { } drawOperation)
        {
            operation = drawOperation;
            return true;
        }
        for (PrototypeChoiceResolutionContinuationState? current =
                choice.OuterChoiceContinuation;
             current is not null;
             current = current.Parent)
            if (TryGetStandaloneSemanticDrawOperation(current,
                    out operation))
                return true;
        operation = null!;
        return false;
    }

    private static bool TryGetStandaloneSemanticDrawOperation(
        PrototypeChoiceResolutionContinuationState continuation,
        out PrototypeQueuedOperation operation)
    {
        for (PrototypeChoiceResolutionContinuationState? current = continuation;
             current is not null;
             current = current.Parent)
            if (current.Operations.FirstOrDefault(item =>
                    item.Kind == PrototypeCombatEffectKind.DrawCards &&
                    item.StandaloneSemanticDrawSegment) is { } drawOperation)
            {
                operation = drawOperation;
                return true;
            }
        operation = null!;
        return false;
    }

    private static int StratagemPickCount(CombatState combat) =>
        combat.PlayerPowers.Where(power =>
                power.PowerId == "proto.power.stratagem")
            .Sum(power => Math.Max(0, power.Stacks));

    private static int ExpectedStratagemSelectionCount(CombatState combat) =>
        Math.Min(StratagemPickCount(combat),
            Math.Min(combat.DrawPile.Length, Math.Max(0, 10 - combat.Hand.Length)));

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
        var attached = baseRun with
        {
            World = world with
            {
                Combat = world.Combat! with
                {
                    SemanticDeck = SemanticDeckEngineV111.BeginNature(
                        world.Combat!.DrawPile)
                }
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
            (sourceCombat.SemanticDeck is { } existing &&
             (existing.Pending is not null ||
              existing.Mode != SemanticDeckStateV111.NatureMode ||
              !SemanticDeckEngineV111.AllCardInstanceIds(existing)
                  .Order().SequenceEqual(sourceCombat.DrawPile.Order()))) ||
            sourceCombat.PendingKnowledgeDemonChoice is not null ||
            sourceCombat.AutomaticPipelineContinuation is not { } cursor ||
            cursor.NextStepIndex != PrototypeGameEngine.SemanticHandDrawStepIndex ||
            cursor.ControlledDrawRemaining is not null ||
            cursor.ControlledDrawCostRemaining is not null ||
            cursor.ControlledSemanticDrawRemaining is not null ||
            cursor.OpeningStage is not null ||
            cursor.PendingPostDispatchEventKind is not null)
            throw new InvalidOperationException(
                "Semantic automatic hand draw needs a clean DrawPlayerHand checkpoint.");
        var origin = source.Fork();
        var world = origin.World!;
        var attached = origin with
        {
            World = world with
            {
                Combat = world.Combat! with
                {
                    SemanticDeck = sourceCombat.SemanticDeck?.Fork() ??
                        SemanticDeckEngineV111.BeginNature(
                            world.Combat!.DrawPile)
                }
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
        Validate(state);
        if (state.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic combat draw fingerprint.");
        if (state.DrawHookChoicePending is not null)
            throw new InvalidOperationException(
                "Resolve the pending CardDrawn hook choice by action ID.");
        var run = state.Run.Fork();
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
            var nextPileChoice = state with
            {
                Run = resumed,
                RequestedCount = requested,
                RemainingCount = pileCardRemaining,
                RandomPileCardTranscript = state.RandomPileCardTranscript
                    .Append(decision.Fork()).ToArray()
            };
            Validate(nextPileChoice);
            return nextPileChoice;
        }

        if (state.DrawInsertionPending is { } insertionEvent)
        {
            var decision = new SemanticDrawInsertionDecisionV111(
                insertionEvent, selectedIndex);
            decision.Validate();
            int drawnBefore = combat.CounterState.CardsDrawnThisCombat;
            RunState resumed;
            using (ControlledChanceGuardV111.Enter())
                resumed = PrototypeGameEngine.ResumeControlledDrawInsertion(
                    run, decision.SelectedPosition);
            var insertionRemaining = RemainingSemanticDrawCount(
                resumed, state.RemainingCount, drawnBefore);
            var nextInsertion = state with
            {
                Run = resumed,
                RemainingCount = insertionRemaining,
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
            RunState advancedCost;
            int costRemaining;
            if (combat.PendingChoice?.ControlledSemanticDraw?
                    .StandaloneSemanticDrawSegment == true)
            {
                using (ControlledChanceGuardV111.Enter())
                    advancedCost =
                        PrototypeGameEngine
                            .ResumeControlledSemanticEffectDrawCost(
                                run, selectedIndex);
                costRemaining = RemainingSemanticDrawCount(
                    advancedCost, state.RemainingCount,
                    combat.CounterState.CardsDrawnThisCombat);
            }
            else
            {
                using (ControlledChanceGuardV111.Enter())
                    (advancedCost, costRemaining) =
                        PrototypeGameEngine.ResumeSemanticCombatDrawCost(
                            run, selectedIndex);
            }
            var costNext = state with
            {
                Run = advancedCost,
                RemainingCount = costRemaining,
                CostTranscript = state.CostTranscript.Append(decision).ToArray()
            };
            Validate(costNext);
            return costNext;
        }

        if (combat.PendingChoice?.ControlledSemanticDraw?
                .StandaloneSemanticDrawSegment == true)
        {
            RunState resumedDraw;
            using (ControlledChanceGuardV111.Enter())
                resumedDraw = PrototypeGameEngine
                    .ResumeControlledSemanticEffectDraw(run, selectedIndex);
            var drawNext = state with
            {
                Run = resumedDraw,
                RemainingCount = RemainingSemanticDrawCount(
                    resumedDraw, state.RemainingCount,
                    combat.CounterState.CardsDrawnThisCombat)
            };
            Validate(drawNext);
            return drawNext;
        }

        var deck = combat.SemanticDeck!;
        if (deck.Pending is null || state.RemainingCount <= 0)
            throw new InvalidOperationException("No semantic draw card pending.");
        var resolvedDeck = SemanticDeckEngineV111.Resolve(
            deck, deck.Fingerprint, selectedIndex);
        run = run with
        {
            World = world with
            {
                Combat = combat with { SemanticDeck = resolvedDeck }
            }
        };
        RunState advanced;
        int remaining;
        using (ControlledChanceGuardV111.Enter())
            (advanced, remaining) =
                PrototypeGameEngine.BeginSemanticCombatDraw(
                    run, state.RemainingCount, state.FromHandDraw);
        var next = state with { Run = advanced, RemainingCount = remaining };
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
        if (!pending.Candidates.Any(candidate =>
                candidate.ActionId == actionId))
            throw new InvalidOperationException(
                "Action is outside the pending Stratagem selection support.");

        int drawnBefore = state.Run.World!.Combat!
            .CounterState.CardsDrawnThisCombat;
        var selection = SemanticCardSelectionEngineV111.Step(
            state.Run.Fork(), actionId);
        var decision = selection.Decision ??
            throw new InvalidDataException(
                "Stratagem selection omitted its source decision.");
        if (CanonicalJson.Sha256(decision.Event) !=
            CanonicalJson.Sha256(pending))
            throw new InvalidDataException(
                "Stratagem selection event changed during resolution.");
        RunState resumedRun = selection.Run;
        int remaining;
        if (state.AutomaticHandDraw)
        {
            using (ControlledChanceGuardV111.Enter())
                (resumedRun, remaining) =
                    PrototypeGameEngine.BeginSemanticCombatDraw(
                        resumedRun, state.RemainingCount,
                        fromHandDraw: true);
        }
        else
            remaining = RemainingSemanticDrawCount(
                selection.Run, state.RemainingCount, drawnBefore);
        var next = state with
        {
            Run = resumedRun,
            RemainingCount = remaining,
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
        bool beforeHandDraw = pending.BeforeHandDraw;
        var (run, decision) = SemanticDrawHookChoiceEngineV111.Resolve(
            state.Run.Fork(), pending, actionId);
        int remaining;
        int requested = state.RequestedCount;
        if (pending.AutomaticHandDraw)
        {
            var combat = run.World?.Combat ??
                throw new InvalidDataException(
                    "Automatic draw-hook choice removed combat.");
            if (beforeHandDraw &&
                combat.PendingChoice?.AutomaticSemanticDrawHookChoice == true)
            {
                remaining = 0;
            }
            else if (beforeHandDraw)
            {
                using (ControlledChanceGuardV111.Enter())
                    (run, requested, remaining) =
                        PrototypeGameEngine
                            .ResumeSemanticBeforeHandDrawActionChoice(run);
            }
            else if (combat.PendingChoice?.AutomaticSemanticDrawHookChoice == true)
                remaining = combat.AutomaticPipelineContinuation?
                    .ControlledSemanticDrawRemaining ??
                    throw new InvalidDataException(
                        "Automatic draw-hook choice lost its cursor.");
            else
            {
                using (ControlledChanceGuardV111.Enter())
                    (run, remaining) =
                        PrototypeGameEngine.BeginSemanticCombatDraw(
                            run, state.RemainingCount, fromHandDraw: true);
            }
        }
        else
            remaining = RemainingSemanticDrawCount(
                run, state.RemainingCount, drawnBefore);
        var next = state with
        {
            Run = run,
            RequestedCount = requested,
            RemainingCount = remaining,
            DrawHookChoiceTranscript = state.DrawHookChoiceTranscript
                .Append(decision.Fork()).ToArray()
        };
        Validate(next);
        return next;
    }

    private static int RemainingSemanticDrawCount(
        RunState run, int previousRemaining, int drawnBefore)
    {
        var combat = run.World?.Combat ?? throw new InvalidDataException(
            "Semantic draw continuation removed combat.");
        var pending = combat.PendingChoice;
        if (pending is not null &&
            TryGetStandaloneSemanticDrawOperation(pending,
                out var drawOperation))
            return drawOperation.Amount;
        int? savedCount = pending?.ControlledSemanticDraw?.RemainingCount ??
            pending?.ControlledDrawInsertion?.SemanticDrawContinuation?
                .RemainingCount ??
            pending?.ControlledDrawCost?.RemainingCount;
        if (savedCount is { } remaining)
            return remaining;
        if (PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat))
            return combat.AutomaticPipelineContinuation!
                .ControlledSemanticDrawRemaining!.Value;
        if (combat.SemanticDeck?.Pending is not null)
            return Math.Max(0, previousRemaining -
                (combat.CounterState.CardsDrawnThisCombat - drawnBefore));
        return 0;
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

    public static void Validate(SemanticCombatDrawV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var randomPileCardPending = state.RandomPileCardPending;
        var drawInsertionPending = state.DrawInsertionPending;
        var completedHookCursor =
            state.Run.World?.Combat?.AutomaticPipelineContinuation;
        var drawHookChoicePending = state.DrawHookChoicePending;
        bool completedAfterHook = state.AutomaticHandDraw &&
            state.RandomPileCardTranscript is { Length: > 0 } &&
            completedHookCursor?.NextStepIndex ==
                PrototypeGameEngine.SemanticHandDrawStepIndex + 1;
        if (state.RequestedCount < 0 ||
            state.RemainingCount < 0 ||
            state.RemainingCount > state.RequestedCount ||
            (state.RequestedCount == 0 && randomPileCardPending is null &&
             drawInsertionPending is null &&
             drawHookChoicePending?.BeforeHandDraw != true &&
             !completedAfterHook) ||
            state.Origin.Phase != RunPhase.Combat ||
            state.Origin.World?.Combat is not { } originCombat ||
            (!state.AutomaticHandDraw && originCombat.SemanticDeck is not null) ||
            (state.AutomaticHandDraw &&
             originCombat.SemanticDeck is { } prior &&
             (prior.Pending is not null ||
              prior.Mode != SemanticDeckStateV111.NatureMode ||
              !SemanticDeckEngineV111.AllCardInstanceIds(prior)
                  .Order().SequenceEqual(originCombat.DrawPile.Order()) ||
              (state.Run.World?.Combat?.SemanticDeck?.Transcript?.Length ?? 0) <
                  (prior.Transcript?.Length ?? 0))) ||
            originCombat.PendingChoice is not null ||
            (!state.AutomaticHandDraw &&
             originCombat.AutomaticPipelineContinuation is not null) ||
            (state.AutomaticHandDraw &&
             (originCombat.AutomaticPipelineContinuation is not { } initial ||
              initial.NextStepIndex != PrototypeGameEngine.SemanticHandDrawStepIndex ||
              initial.ControlledSemanticDrawRemaining is not null ||
              initial.ControlledSemanticBeforeHandDrawPending)) ||
            state.Run.Phase != RunPhase.Combat ||
            state.Run.World?.Combat is not { } combat ||
            combat.SemanticDeck is not { } deck ||
            (!state.AutomaticHandDraw &&
             combat.AutomaticPipelineContinuation is not null) ||
            combat.PendingKnowledgeDemonChoice is not null ||
            state.CostTranscript is null ||
            state.DrawInsertionTranscript is null ||
            state.StratagemSelectionTranscript is null ||
            state.DrawHookChoiceTranscript is null)
            throw new InvalidDataException("Invalid semantic combat draw stage.");
        SemanticDeckEngineV111.Validate(deck);
        var cost = combat.PendingChoice?.ControlledDrawCost;
        var controlledSemanticDraw =
            combat.PendingChoice?.ControlledSemanticDraw;
        bool standaloneSemanticDrawPending = controlledSemanticDraw?
            .StandaloneSemanticDrawSegment == true ||
            combat.PendingChoice?.StandaloneSemanticDrawHookChoice == true;
        var stratagemSelectionPending = state.StratagemSelectionPending;
        if (state.AutomaticHandDraw)
        {
            var cursor = combat.AutomaticPipelineContinuation;
            bool awaiting = deck.Pending is not null || cost is not null ||
                drawInsertionPending is not null ||
                stratagemSelectionPending is not null ||
                drawHookChoicePending is not null;
            if (randomPileCardPending is not null)
            {
                if (cursor is null ||
                    cursor.NextStepIndex !=
                        PrototypeGameEngine.SemanticHandDrawStepIndex ||
                    !cursor.ControlledSemanticBeforeHandDrawPending ||
                    state.RequestedCount != 0 || state.RemainingCount != 0 ||
                    combat.PendingChoice?.ControlledRandomPileCard is null ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw choice cursor is inconsistent.");
            }
            else if (drawHookChoicePending?.BeforeHandDraw == true)
            {
                if (cursor is null ||
                    cursor.NextStepIndex !=
                        PrototypeGameEngine.SemanticHandDrawStepIndex ||
                    !cursor.ControlledSemanticBeforeHandDrawPending ||
                    cursor.ControlledSemanticDrawRemaining is not null ||
                    cursor.ControlledSemanticDrawRequested is not null ||
                    state.RequestedCount != 0 || state.RemainingCount != 0 ||
                    combat.PendingChoice?.AutomaticSemanticDrawHookChoice != true ||
                    deck.Pending is not null || cost is not null)
                    throw new InvalidDataException(
                        "Semantic BeforeHandDraw action cursor is inconsistent.");
            }
            else if (cursor is null ||
                cursor.ControlledSemanticBeforeHandDrawPending ||
                (awaiting &&
                 (!PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat) ||
                  cursor.ControlledSemanticDrawRemaining != state.RemainingCount ||
                  cursor.ControlledSemanticDrawRequested != state.RequestedCount)) ||
                (!awaiting &&
                 (state.RemainingCount != 0 ||
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
             drawHookChoicePending is null) ||
            (cost is not null &&
             (deck.Pending is not null ||
              cost.RemainingCount != state.RemainingCount ||
              cost.FromHandDraw != state.FromHandDraw ||
              cost.EventDepth != 0 ||
              !PrototypeGameEngine.ValidateControlledDrawCost(
                  state.Run.Player, combat, cost with { FromHandDraw = true }))) ||
            (deck.Pending is null && cost is null &&
             drawInsertionPending is null &&
             stratagemSelectionPending is null &&
             drawHookChoicePending is null &&
             state.RemainingCount != 0) ||
            (deck.Pending is not null && state.RemainingCount <= 0))
            throw new InvalidDataException(
                "Semantic deck/drawn-cost checkpoint is inconsistent.");
        foreach (var decision in state.CostTranscript)
        {
            decision.Validate();
            if (decision.ProviderMode != "semantic-controlled-v1" ||
                decision.ProbabilityStatus != "UNVERIFIED")
                throw new InvalidDataException(
                    "Unexpected semantic drawn-cost transcript provenance.");
        }
        if (state.RandomPileCardTranscript is null)
            throw new InvalidDataException(
                "Missing semantic random pile-card transcript.");
        foreach (var decision in state.RandomPileCardTranscript)
            decision.Validate();
        foreach (var decision in state.DrawInsertionTranscript)
            decision.Validate();
        foreach (var decision in state.StratagemSelectionTranscript)
            decision.Validate();
        foreach (var decision in state.DrawHookChoiceTranscript)
            decision.Validate();
        if (controlledSemanticDraw?.StandaloneSemanticDrawSegment == true &&
            controlledSemanticDraw.RemainingCount != state.RemainingCount)
            throw new InvalidDataException(
                "Standalone semantic draw continuation has a stale count.");
        if (drawHookChoicePending is { } hookChoice)
        {
            bool cursorMatches = hookChoice.AutomaticHandDraw
                ? state.AutomaticHandDraw &&
                  combat.PendingChoice?.AutomaticSemanticDrawHookChoice == true &&
                  (hookChoice.BeforeHandDraw
                      ? combat.AutomaticPipelineContinuation?
                            .ControlledSemanticBeforeHandDrawPending == true &&
                        combat.AutomaticPipelineContinuation.NextStepIndex ==
                            PrototypeGameEngine.SemanticHandDrawStepIndex &&
                        state.RequestedCount == 0 && state.RemainingCount == 0
                      : PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat) &&
                        combat.AutomaticPipelineContinuation!
                            .ControlledSemanticDrawRemaining == state.RemainingCount &&
                        combat.AutomaticPipelineContinuation
                            .ControlledSemanticDrawRequested == state.RequestedCount)
                : !state.AutomaticHandDraw &&
                  combat.PendingChoice?.StandaloneSemanticDrawHookChoice == true &&
                  TryGetStandaloneSemanticDrawOperation(
                      combat.PendingChoice, out var hookDraw) &&
                  hookDraw.Amount == state.RemainingCount;
            if (!cursorMatches)
                throw new InvalidDataException(
                    "CardDrawn hook-choice cursor is inconsistent.");
        }
        if (stratagemSelectionPending is { } selectionEvent)
        {
            bool cursorMatches = state.AutomaticHandDraw
                ? PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat) &&
                  combat.AutomaticPipelineContinuation!
                      .ControlledSemanticDrawRemaining == state.RemainingCount &&
                  combat.AutomaticPipelineContinuation
                      .ControlledSemanticDrawRequested == state.RequestedCount
                : TryGetStandaloneSemanticDrawOperation(
                      combat.PendingChoice!, out var draw) &&
                  draw.Amount == state.RemainingCount;
            if (!cursorMatches || state.RemainingCount <= 0 ||
                selectionEvent.MaximumSelections !=
                    ExpectedStratagemSelectionCount(combat))
                throw new InvalidDataException(
                    "Standalone Stratagem selection cursor is inconsistent.");
        }
        if (drawInsertionPending is { } insertionEvent)
        {
            var insertion = combat.PendingChoice?.ControlledDrawInsertion;
            if (state.RemainingCount <= 0 ||
                insertion?.SemanticDrawContinuation?.RemainingCount !=
                    state.RemainingCount ||
                insertionEvent.Request.BoundedCount !=
                    insertionEvent.EligibleDrawPileInstanceIds.Length + 1)
                throw new InvalidDataException(
                    "Semantic draw-insertion cursor is inconsistent.");
        }
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Semantic provider state and physical draw-pile membership differ.");
    }
}
