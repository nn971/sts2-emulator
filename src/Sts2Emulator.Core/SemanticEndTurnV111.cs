using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// A versioned end-turn continuation: commit real next-turn intents, execute
/// the genuine automatic hand-draw stage with deferred Nature card identities,
/// then finish the actual automatic pipeline to the next player decision.
/// A new semantic provider starts only from an exhausted draw pile; an
/// existing provider-owned Nature deck is reused with its saved constraints.
/// </summary>
public sealed record SemanticEndTurnV111(
    RunState Origin,
    RunState Run,
    string NativeEncounterEntry,
    ControlledTurnIntentsV111? Intents,
    SemanticCombatDrawV111? HandDraw,
    RandomIndexDecisionV111[] IntentTranscript,
    bool Complete = false)
{
    public const string SchemaId = "sts2-v111-semantic-end-turn-v1";
    public RunState? PlayerActionSource { get; init; }
    public string? PlayerActionId { get; init; }
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public RandomIndexRequestV111? IntentPending =>
        Intents?.Pending;
    [JsonIgnore] public SemanticDeckEventV111? DrawPending =>
        Complete ? null : HandDraw?.Pending;
    [JsonIgnore] public SemanticRandomPileCardEventV111?
        RandomPileCardPending =>
            Complete ? null : HandDraw?.RandomPileCardPending;
    [JsonIgnore] public SemanticDrawInsertionEventV111?
        DrawInsertionPending =>
            Complete ? null : HandDraw?.DrawInsertionPending;
    [JsonIgnore] public SemanticCardSelectionEventV111?
        StratagemSelectionPending =>
            Complete ? null : HandDraw?.StratagemSelectionPending;
    [JsonIgnore] public SemanticDrawHookChoiceEventV111?
        DrawHookChoicePending =>
            Complete ? null : HandDraw?.DrawHookChoicePending;
    [JsonIgnore] public RandomIndexRequestV111? CostPending =>
        Complete ? null : HandDraw?.CostPending;
    [JsonIgnore] public bool AtPlayerBoundary =>
        Complete && Run.Phase == RunPhase.Combat &&
        Run.World?.Combat is { IsPlayerTurn: true,
            AutomaticPipelineContinuation: null, PendingChoice: null,
            PendingKnowledgeDemonChoice: null };

    public SemanticEndTurnV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork(),
        Intents = Intents?.Fork(), HandDraw = HandDraw?.Fork(),
        PlayerActionSource = PlayerActionSource?.Fork(),
        IntentTranscript = IntentTranscript.Select(t => t with
        {
            Request = t.Request with
            {
                OrderedCandidates = (int[])t.Request.OrderedCandidates.Clone()
            }
        }).ToArray()
    };
}

public static class SemanticEndTurnEngineV111
{
    /// <summary>
    /// Start from an actual post-end-turn automatic pipeline checkpoint just
    /// before AdvanceTurn. Enemy actions and preceding hooks already ran.
    /// Only exhausted draw piles are accepted: this guarantees that any new
    /// semantic shuffle is source-triggered by the later recycle.
    /// </summary>
    public static SemanticEndTurnV111 BeginAtAdvanceTurn(
        RunState source, string nativeEncounterEntry)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEncounterEntry);
        ValidateOrigin(source);
        var combat = source.World!.Combat!;
        var intents = ControlledTurnIntentEngineV111.Begin(
            combat, nativeEncounterEntry);
        var state = new SemanticEndTurnV111(
            source.Fork(), source.Fork(), nativeEncounterEntry,
            intents, null, []);
        if (intents.Complete)
            state = CompleteIntents(state);
        Validate(state);
        return state;
    }

    /// <summary>
    /// Also execute the real legal end_turn player action under the controlled
    /// chance guard. This is the complete step -> Nature -> next-step entry
    /// for the restricted source-owned semantic-deck fixture.
    /// </summary>
    public static SemanticEndTurnV111 BeginFromPlayerAction(
        RunState playerBoundary, string nativeEncounterEntry)
    {
        var (continuation, _) = BeginFromPlayerActionOrTerminal(
            playerBoundary, nativeEncounterEntry);
        return continuation ?? throw new InvalidOperationException(
            "The supported end-turn reached a terminal combat outcome; " +
            "consume the terminal result using BeginFromPlayerActionOrTerminal.");
    }

    /// <summary>
    /// Execute exactly one real legal player end_turn. Enemy actions and
    /// automatic hooks can already defeat the player or kill the final enemy
    /// before the next-turn AdvanceTurn RNG boundary. Preserve those genuine
    /// combat terminal transitions without forcing a nonexistent intent
    /// or hand-draw continuation.
    /// </summary>
    public static (SemanticEndTurnV111? Continuation, RunState Result)
        BeginFromPlayerActionOrTerminal(
        RunState playerBoundary, string nativeEncounterEntry)
    {
        ArgumentNullException.ThrowIfNull(playerBoundary);
        if (playerBoundary.Phase != RunPhase.Combat ||
            playerBoundary.World?.Combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null, PendingChoice: null } starting ||
            starting.PendingKnowledgeDemonChoice is not null ||
            !IsSupportedSemanticSourceDeck(starting))
            throw new InvalidOperationException(
                "Expected a real player-choice combat boundary.");
        var environment = new PrototypeAiEnvironment();
        var actions = environment.Observe(playerBoundary)
            .LegalActions.Where(a => a.Kind == "end_turn").ToArray();
        if (actions.Length != 1)
            throw new InvalidOperationException(
                "Expected exactly one legal end_turn player action.");
        RunState advanced;
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            advanced = environment.Step(
                playerBoundary.Fork(), actions[0].ActionId).State;
        if (advanced.Phase is RunPhase.Terminal or RunPhase.Reward)
            return (null, advanced);
        var state = BeginAtAdvanceTurn(advanced, nativeEncounterEntry);
        var continuation = state with
        {
            PlayerActionSource = playerBoundary.Fork(),
            PlayerActionId = actions[0].ActionId
        };
        return (continuation, continuation.Run);
    }

    public static SemanticEndTurnV111 Resolve(
        SemanticEndTurnV111 source, string fingerprint, int selectedIndex)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException("Stale semantic end-turn fingerprint.");
        if (source.Complete)
            throw new InvalidOperationException("End-turn Nature choices are complete.");
        var state = source.Fork();
        if (state.Intents is { Pending: not null } pending)
        {
            var next = ControlledTurnIntentEngineV111.Resolve(
                pending, selectedIndex, state.Run.Rng);
            state = state with
            {
                Intents = next,
                IntentTranscript = next.Transcript.Select(t => t with
                {
                    Request = t.Request with
                    {
                        OrderedCandidates = (int[])t.Request.OrderedCandidates.Clone()
                    }
                }).ToArray()
            };
            if (next.Complete)
                state = CompleteIntents(state);
        }
        else if (state.HandDraw is { AtBoundary: false } hand)
        {
            if (hand.DrawHookChoicePending is not null)
                throw new InvalidOperationException(
                    "Resolve the pending CardDrawn hook action by action ID.");
            if (hand.StratagemSelectionPending is not null)
                throw new InvalidOperationException(
                    "Resolve the pending source Stratagem selection by action ID.");
            var next = SemanticCombatDrawEngineV111.Resolve(
                hand, hand.Fingerprint, selectedIndex);
            state = state with { HandDraw = next, Run = next.Run };
            if (next.AtBoundary)
                state = CompleteHandDraw(state);
        }
        else
            throw new InvalidOperationException("No pending semantic end-turn choice.");
        Validate(state);
        return state;
    }

    public static SemanticEndTurnV111 ResolveDrawHookChoice(
        SemanticEndTurnV111 source, string fingerprint, string actionId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException("Stale semantic end-turn fingerprint.");
        if (source.Complete || source.HandDraw is not { } hand ||
            hand.DrawHookChoicePending is null)
            throw new InvalidOperationException(
                "No automatic CardDrawn hook action is pending.");
        var nextHand = SemanticCombatDrawEngineV111.ResolveDrawHookChoice(
            hand, hand.Fingerprint, actionId);
        var state = source with { HandDraw = nextHand, Run = nextHand.Run };
        if (nextHand.AtBoundary)
            state = CompleteHandDraw(state);
        Validate(state);
        return state;
    }

    public static SemanticEndTurnV111 ResolveStratagemSelection(
        SemanticEndTurnV111 source, string fingerprint, string actionId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn fingerprint.");
        if (source.Complete || source.HandDraw is not { } hand ||
            hand.StratagemSelectionPending is null)
            throw new InvalidOperationException(
                "No automatic Stratagem selection is pending.");
        var nextHand = SemanticCombatDrawEngineV111.ResolveStratagemSelection(
            hand, hand.Fingerprint, actionId);
        var state = source with { HandDraw = nextHand, Run = nextHand.Run };
        if (nextHand.AtBoundary)
            state = CompleteHandDraw(state);
        Validate(state);
        return state;
    }

    private static SemanticEndTurnV111 CompleteIntents(SemanticEndTurnV111 state)
    {
        var intents = state.Intents ??
            throw new InvalidOperationException("No source intent checkpoint.");
        if (!intents.Complete)
            throw new InvalidOperationException("Enemy intent tickets are pending.");
        ControlledTurnIntentEngineV111.Validate(intents);
        RunState handCheckpoint;
        using (ControlledChanceGuardV111.Enter())
            handCheckpoint = PrototypeGameEngine.ResumeControlledTurnAfterIntents(
                state.Run.Fork(), intents.Combat.Fork(),
                stopBeforeSemanticHandDraw: true);
        var combat = handCheckpoint.World?.Combat ??
            throw new InvalidOperationException("Next-turn combat disappeared.");
        if (combat.AutomaticPipelineContinuation?.NextStepIndex !=
                PrototypeGameEngine.SemanticHandDrawStepIndex ||
            !IsSupportedSemanticSourceDeck(combat) ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.semantic_end_turn " +
                "operation=hand_draw_checkpoint_requires_empty_draw_pile");
        var draw = SemanticCombatDrawEngineV111.BeginAutomaticHandDraw(
            handCheckpoint);
        var next = state with
        {
            Run = draw.Run, Intents = null, HandDraw = draw
        };
        return draw.AtBoundary ? CompleteHandDraw(next) : next;
    }

    private static SemanticEndTurnV111 CompleteHandDraw(SemanticEndTurnV111 state)
    {
        if (state.HandDraw is not { AtBoundary: true } draw)
            throw new InvalidOperationException(
                "Cannot finish end turn with pending drawn-card choices.");
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeSemanticAutomaticAfterHandDraw(
                draw.Run.Fork());
        if (result.Phase != RunPhase.Combat ||
            result.World?.Combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null, PendingChoice: null,
                PendingKnowledgeDemonChoice: null })
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.semantic_end_turn " +
                "operation=not_at_next_player_boundary");
        var actions = new PrototypeAiEnvironment().Observe(result).LegalActions;
        if (!actions.Any())
            throw new InvalidOperationException(
                "The next player boundary has no available legal actions.");
        return state with { Run = result, Complete = true };
    }

    public static string Save(SemanticEndTurnV111 state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    /// <summary>Replay the complete player action and every saved Nature
    /// decision or source selection from their original checkpoint.</summary>
    public static SemanticEndTurnV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var saved = JsonSerializer.Deserialize<SemanticEndTurnV111>(json)
            ?? throw new InvalidDataException("Null semantic end-turn snapshot.");
        Validate(saved);
        var reproduced = saved.PlayerActionSource is null
            ? BeginAtAdvanceTurn(saved.Origin, saved.NativeEncounterEntry)
            : BeginFromPlayerAction(
                saved.PlayerActionSource, saved.NativeEncounterEntry);
        if (saved.PlayerActionId is not null &&
            reproduced.PlayerActionId != saved.PlayerActionId)
            throw new InvalidDataException(
                "End-turn action identity differs on source replay.");
        foreach (var choice in saved.IntentTranscript)
        {
            choice.Validate();
            if (reproduced.IntentPending is not { } request ||
                choice.ProviderMode != "controlled-v111" ||
                choice.ProbabilityStatus != "UNVERIFIED" ||
                CanonicalJson.Sha256(request) !=
                    CanonicalJson.Sha256(choice.Request))
                throw new InvalidDataException(
                    "Semantic end-turn intent transcript diverged.");
            reproduced = Resolve(
                reproduced, reproduced.Fingerprint, choice.SelectedIndex);
        }
        if (saved.HandDraw is { } savedHand)
        {
            int card = 0, cost = 0, pileCard = 0, insertion = 0,
                stratagem = 0, hookChoice = 0;
            while (card < savedHand.Transcript.Length ||
                   cost < savedHand.CostTranscript.Length ||
                   pileCard < savedHand.RandomPileCardTranscript.Length ||
                   insertion < savedHand.DrawInsertionTranscript.Length ||
                   stratagem <
                       savedHand.StratagemSelectionTranscript.Length ||
                   hookChoice <
                       savedHand.DrawHookChoiceTranscript.Length)
            {
                if (reproduced.DrawHookChoicePending is { } hookEvent)
                {
                    if (hookChoice >=
                        savedHand.DrawHookChoiceTranscript.Length)
                        throw new InvalidDataException(
                            "Missing semantic end-turn CardDrawn hook action.");
                    var decision =
                        savedHand.DrawHookChoiceTranscript[hookChoice++];
                    decision.Validate();
                    if (CanonicalJson.Sha256(decision.Event) !=
                        CanonicalJson.Sha256(hookEvent))
                        throw new InvalidDataException(
                            "Semantic end-turn CardDrawn hook support changed.");
                    reproduced = ResolveDrawHookChoice(
                        reproduced, reproduced.Fingerprint,
                        decision.ActionId);
                }
                else if (reproduced.RandomPileCardPending is { } pileCardEvent)
                {
                    if (pileCard >=
                        savedHand.RandomPileCardTranscript.Length)
                        throw new InvalidDataException(
                            "Missing semantic end-turn random pile-card choice.");
                    var decision =
                        savedHand.RandomPileCardTranscript[pileCard++];
                    decision.Validate();
                    if (CanonicalJson.Sha256(decision.Event) !=
                        CanonicalJson.Sha256(pileCardEvent))
                        throw new InvalidDataException(
                            "Semantic end-turn random pile-card support changed.");
                    reproduced = Resolve(
                        reproduced, reproduced.Fingerprint,
                        decision.SelectedIndex);
                }
                else if (reproduced.DrawInsertionPending is { } insertionEvent)
                {
                    if (insertion >=
                        savedHand.DrawInsertionTranscript.Length)
                        throw new InvalidDataException(
                            "Missing semantic end-turn insertion decision.");
                    var decision =
                        savedHand.DrawInsertionTranscript[insertion++];
                    decision.Validate();
                    if (CanonicalJson.Sha256(decision.Event) !=
                        CanonicalJson.Sha256(insertionEvent))
                        throw new InvalidDataException(
                            "Semantic end-turn insertion support changed.");
                    reproduced = Resolve(
                        reproduced, reproduced.Fingerprint,
                        decision.SelectedPosition);
                }
                else if (reproduced.StratagemSelectionPending is
                         { } stratagemEvent)
                {
                    if (stratagem >=
                        savedHand.StratagemSelectionTranscript.Length)
                        throw new InvalidDataException(
                            "Missing semantic end-turn Stratagem selection.");
                    var decision =
                        savedHand.StratagemSelectionTranscript[stratagem++];
                    decision.Validate();
                    if (CanonicalJson.Sha256(decision.Event) !=
                        CanonicalJson.Sha256(stratagemEvent))
                        throw new InvalidDataException(
                            "Semantic end-turn Stratagem support changed.");
                    reproduced = ResolveStratagemSelection(
                        reproduced, reproduced.Fingerprint,
                        decision.ActionId);
                }
                else if (reproduced.CostPending is { } costRequest)
                {
                    if (cost >= savedHand.CostTranscript.Length)
                        throw new InvalidDataException(
                            "Missing semantic end-turn drawn-cost ticket.");
                    var decision = savedHand.CostTranscript[cost++];
                    decision.Validate();
                    if (decision.ProviderMode != "semantic-controlled-v1" ||
                        CanonicalJson.Sha256(decision.Request) !=
                            CanonicalJson.Sha256(costRequest))
                        throw new InvalidDataException(
                            "Semantic end-turn drawn cost eligibility changed.");
                    reproduced = Resolve(
                        reproduced, reproduced.Fingerprint, decision.SelectedIndex);
                }
                else if (reproduced.DrawPending is { } cardRequest)
                {
                    if (card >= savedHand.Transcript.Length)
                        throw new InvalidDataException(
                            "Missing semantic end-turn card identity.");
                    var decision = savedHand.Transcript[card++];
                    if (decision.EventId != cardRequest.EventId ||
                        decision.Ordinal != cardRequest.Ordinal ||
                        !decision.CandidateCardInstanceIds.SequenceEqual(
                            cardRequest.CandidateCardInstanceIds))
                        throw new InvalidDataException(
                            "Semantic end-turn card identity eligibility changed.");
                    reproduced = Resolve(
                        reproduced, reproduced.Fingerprint, decision.SelectedIndex);
                }
                else throw new InvalidDataException(
                    "Semantic end-turn Nature replay ended too early.");
            }
        }
        if (CanonicalJson.Sha256(reproduced) != CanonicalJson.Sha256(saved))
            throw new InvalidDataException(
                "Semantic end-turn full source replay disagrees with snapshot.");
        return reproduced;
    }

    public static void Validate(SemanticEndTurnV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateOrigin(state.Origin);
        if (string.IsNullOrWhiteSpace(state.NativeEncounterEntry) ||
            state.IntentTranscript is null ||
            (state.PlayerActionSource is null) != (state.PlayerActionId is null) ||
            (state.Complete && (state.Intents is not null ||
                                state.HandDraw is not { AtBoundary: true })) ||
            (!state.Complete && state.Intents is null &&
             state.HandDraw is null) ||
            (state.Intents is not null && state.HandDraw is not null))
            throw new InvalidDataException("Invalid semantic end-turn stages.");
        if (state.Intents is { } intents)
        {
            ControlledTurnIntentEngineV111.Validate(intents);
            if (CanonicalJson.Sha256(intents.Transcript) !=
                CanonicalJson.Sha256(state.IntentTranscript) ||
                CanonicalJson.Sha256(state.Run) !=
                CanonicalJson.Sha256(state.Origin))
                throw new InvalidDataException("Invalid pending intent stage.");
        }
        if (state.HandDraw is { } hand)
        {
            SemanticCombatDrawEngineV111.Validate(hand);
            if (!state.Complete &&
                CanonicalJson.Sha256(state.Run) != CanonicalJson.Sha256(hand.Run))
                throw new InvalidDataException(
                    "Pending end-turn hand state differs from the draw continuation.");
            if (state.Complete && !state.AtPlayerBoundary)
                throw new InvalidDataException(
                    "Completed end-turn result is not a player decision.");
        }
    }

    /// <summary>Without a provider-owned deck, accept only a truly empty,
    /// uncommitted draw pile with a nonempty discard. Existing Nature decks
    /// are reused unchanged, including a nonempty unresolved pile. Source
    /// mechanics must preserve physical card-instance membership exactly.
    /// </summary>
    private static bool IsSupportedSemanticSourceDeck(CombatState combat)
    {
        if (combat.SemanticDeck is not { } deck)
            return combat.DrawPile.Length == 0 &&
                combat.DiscardPile.Length > 0;
        SemanticDeckEngineV111.Validate(deck);
        return deck.Mode == SemanticDeckStateV111.NatureMode &&
            deck.Pending is null &&
            SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order());
    }

    private static void ValidateOrigin(RunState run)
    {
        if (run.Phase != RunPhase.Combat ||
            run.World?.Combat is not { } combat ||
            combat.AutomaticPipelineContinuation is not { } checkpoint ||
            checkpoint.NextStepIndex < 0 ||
            checkpoint.NextStepIndex >= PrototypeContent.Rules.EndTurnPipeline.Length ||
            PrototypeContent.Rules.EndTurnPipeline[checkpoint.NextStepIndex]
                .Kind != PrototypeAutomaticStepKind.AdvanceTurn ||
            checkpoint.ControlledDrawRemaining is not null ||
            checkpoint.ControlledDrawCostRemaining is not null ||
            checkpoint.ControlledSemanticDrawRemaining is not null ||
            checkpoint.ControlledSemanticDrawRequested is not null ||
            checkpoint.OpeningStage is not null ||
            checkpoint.PendingPostDispatchEventKind is not null ||
            !IsSupportedSemanticSourceDeck(combat) ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new InvalidOperationException(
                "Semantic end-turn requires an exhausted draw pile or an " +
                "existing idle Nature deck at the genuine post-enemy-action " +
                "AdvanceTurn checkpoint.");
    }
}
