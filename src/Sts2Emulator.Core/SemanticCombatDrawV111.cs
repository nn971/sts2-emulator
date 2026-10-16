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
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public SemanticDeckEventV111? Pending =>
        Run.World?.Combat?.SemanticDeck is { } deck
            ? SemanticDeckEngineV111.Inspect(deck) : null;
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
        Pending is null && CostPending is null && RemainingCount == 0;
    // The provider's transcript survives across turns. Only decisions
    // newly made during THIS draw segment are replayed by this envelope.
    [JsonIgnore] public SemanticDeckChoiceV111[] Transcript =>
        (Run.World?.Combat?.SemanticDeck?.Transcript ?? [])
            .Skip(Origin.World?.Combat?.SemanticDeck?.Transcript?.Length ?? 0)
            .ToArray();
    public RandomIndexDecisionV111[] CostTranscript { get; init; } = [];

    public SemanticCombatDrawV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork(),
        CostTranscript = CostTranscript.Select(x => x with
        {
            Request = x.Request with
            {
                OrderedCandidates = (int[])x.Request.OrderedCandidates.Clone()
            }
        }).ToArray()
    };
}

public static class SemanticCombatDrawEngineV111
{
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
        var run = state.Run.Fork();
        var world = run.World!;
        var combat = world.Combat!;

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
            using (ControlledChanceGuardV111.Enter())
                (advancedCost, costRemaining) =
                    PrototypeGameEngine.ResumeSemanticCombatDrawCost(
                        run, selectedIndex);
            var costNext = state with
            {
                Run = advancedCost,
                RemainingCount = costRemaining,
                CostTranscript = state.CostTranscript.Append(decision).ToArray()
            };
            Validate(costNext);
            return costNext;
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
        while (deckCursor < stored.Transcript.Length ||
               costCursor < stored.CostTranscript.Length)
        {
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
        if (state.RequestedCount <= 0 ||
            state.RemainingCount < 0 ||
            state.RemainingCount > state.RequestedCount ||
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
              initial.ControlledSemanticDrawRemaining is not null)) ||
            state.Run.Phase != RunPhase.Combat ||
            state.Run.World?.Combat is not { } combat ||
            combat.SemanticDeck is not { } deck ||
            (!state.AutomaticHandDraw &&
             combat.AutomaticPipelineContinuation is not null) ||
            combat.PendingKnowledgeDemonChoice is not null ||
            state.CostTranscript is null)
            throw new InvalidDataException("Invalid semantic combat draw stage.");
        SemanticDeckEngineV111.Validate(deck);
        var cost = combat.PendingChoice?.ControlledDrawCost;
        if (state.AutomaticHandDraw)
        {
            var cursor = combat.AutomaticPipelineContinuation;
            bool awaiting = deck.Pending is not null || cost is not null;
            if (cursor is null ||
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
        if ((combat.PendingChoice is not null && cost is null) ||
            (cost is not null &&
             (deck.Pending is not null ||
              cost.RemainingCount != state.RemainingCount ||
              cost.EventDepth != 0 ||
              !PrototypeGameEngine.ValidateControlledDrawCost(
                  state.Run.Player, combat, cost with { FromHandDraw = true }))) ||
            (deck.Pending is null && cost is null && state.RemainingCount != 0) ||
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
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()))
            throw new InvalidDataException(
                "Semantic provider state and physical draw-pile membership differ.");
    }
}
