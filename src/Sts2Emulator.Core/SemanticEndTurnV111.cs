using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>Chronological reference into the typed chance transcripts kept
/// by a serialized automatic-pipeline continuation.</summary>
public sealed record SemanticEndTurnBoundaryChoiceV111(
    string Kind, int TranscriptIndex)
{
    public void Validate()
    {
        if (Kind is not ("enemy_action" or "random_target") ||
            TranscriptIndex < 0)
            throw new InvalidDataException(
                "Invalid semantic end-turn boundary transcript entry.");
    }
}

/// <summary>Chance event for one bounded random index suspended inside the
/// automatic enemy-action pipeline.</summary>
public sealed record SemanticEndTurnIndexEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    RandomIndexRequestV111 Request, string SupportFingerprint)
{
    public const string CurrentSchemaId =
        "sts2-v111-semantic-end-turn-index-event-v1";

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Request);
        Request.Validate();
        var supportFingerprint = CanonicalJson.Sha256(Request);
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = SemanticPilotCombatV111.SchemaId,
            boundaryFingerprint = BoundaryFingerprint,
            request = Request
        });
        if (SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            !PrototypeControlledEnemyActionContinuationV111
                .IsEnemyActionSite(Request.SiteId) ||
            SupportFingerprint != supportFingerprint || EventId != eventId)
            throw new InvalidDataException(
                "Invalid semantic end-turn index event.");
    }

    public static SemanticEndTurnIndexEventV111 Create(
        string boundaryFingerprint, RandomIndexRequestV111 request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var detachedRequest = request with
        {
            OrderedCandidates = (int[])request.OrderedCandidates.Clone()
        };
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = SemanticPilotCombatV111.SchemaId,
            boundaryFingerprint,
            request = detachedRequest
        });
        var semanticEvent = new SemanticEndTurnIndexEventV111(
            CurrentSchemaId, eventId, boundaryFingerprint, detachedRequest,
            CanonicalJson.Sha256(detachedRequest));
        semanticEvent.Validate();
        return semanticEvent;
    }
}

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
    /// <summary>
    /// Atomic mechanics/provider envelope for the suspended end-turn action.
    /// Existing continuation properties remain flattened snapshot fields.
    /// </summary>
    [JsonIgnore]
    public SimulationStateV111<SemanticEndTurnMechanicsStateV111,
        SemanticEndTurnStochasticStateV111> Simulation { get; init; } = new(
            new SemanticEndTurnMechanicsStateV111(
                Origin, Run, NativeEncounterEntry, Intents, HandDraw, Complete),
            new SemanticEndTurnStochasticStateV111(
                IntentTranscript, [], [], [], HandDraw?.Stochastic));

    [JsonIgnore]
    public SemanticEndTurnMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public SemanticEndTurnStochasticStateV111 Stochastic
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
        init => Mechanics = Mechanics with { Run = value };
    }

    public string NativeEncounterEntry
    {
        get => Mechanics.NativeEncounterEntry;
        init => Mechanics = Mechanics with { NativeEncounterEntry = value };
    }

    public ControlledTurnIntentsV111? Intents
    {
        get => Run.World?.Combat?.AutomaticPipelineContinuation?
            .ControlledTurnIntents;
        init => Run = PrototypeGameEngine
            .SetControlledTurnIntentContinuation(Run, value);
    }

    public SemanticCombatDrawV111? HandDraw
    {
        get => Mechanics.HandDraw is { } draw
            ? draw with
            {
                Stochastic = Stochastic.HandDraw ??
                    new SemanticCombatDrawStochasticStateV111()
            }
            : null;
        init
        {
            Mechanics = Mechanics with { HandDraw = value };
            Stochastic = Stochastic with
            {
                HandDraw = value?.Stochastic.Fork()
            };
        }
    }

    public bool Complete
    {
        get => Mechanics.Complete;
        init => Mechanics = Mechanics with { Complete = value };
    }

    public RandomIndexDecisionV111[] IntentTranscript
    {
        get => Stochastic.IntentTranscript;
        init => Stochastic = Stochastic with { IntentTranscript = value };
    }

    public RunState? PlayerActionSource
    {
        get => Mechanics.PlayerActionSource;
        init => Mechanics = Mechanics with { PlayerActionSource = value };
    }

    public string? PlayerActionId
    {
        get => Mechanics.PlayerActionId;
        init => Mechanics = Mechanics with { PlayerActionId = value };
    }
    public RandomIndexDecisionV111[] EnemyActionTranscript
    {
        get => Stochastic.EnemyActionTranscript;
        init => Stochastic = Stochastic with { EnemyActionTranscript = value };
    }
    public SemanticTargetDecisionV111[] TargetTranscript
    {
        get => Stochastic.TargetTranscript;
        init => Stochastic = Stochastic with { TargetTranscript = value };
    }
    public SemanticEndTurnBoundaryChoiceV111[] BoundaryTranscript
    {
        get => Stochastic.BoundaryTranscript;
        init => Stochastic = Stochastic with { BoundaryTranscript = value };
    }
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public SemanticTargetEventV111? TargetPending =>
        Complete || HandDraw is not null
            ? null
            : SemanticPilotCombatEngineV111.DescribePendingTargetEvent(
                Run, Fingerprint, TargetTranscript.Length);
    [JsonIgnore] public SemanticEndTurnIndexEventV111?
        EnemyActionEventPending =>
            !Complete && EnemyActionPending is { } request
                ? SemanticEndTurnIndexEventV111.Create(Fingerprint, request)
                : null;
    [JsonIgnore] public RandomIndexRequestV111? EnemyActionPending =>
        Complete ? null : Run.World?.Combat?.AutomaticPipelineContinuation?
            .ControlledEnemyAction?.PendingRequest;
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
        Complete &&
        PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary(Run);

    public void Validate()
    {
        this.Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.SemanticEndTurn);
        this.Stochastic.Validate();
        if (this.EnemyActionPending is not null)
        {
            if (this.Complete || this.Intents is not null ||
                this.HandDraw is not null ||
                this.PlayerActionSource is not { } playerSource ||
                !PrototypeGameEngine
                    .IsAtCombatPlayerDecisionBoundary(playerSource))
                throw new InvalidDataException(
                    "Invalid pending semantic enemy-action stage.");
            var cursor = this.Run.World!.Combat!
                .AutomaticPipelineContinuation!.ControlledEnemyAction!;
            cursor.Validate(PrototypeContent.Rules.EndTurnPipeline.Length);
            if (CanonicalJson.Sha256(cursor.Decisions) !=
                CanonicalJson.Sha256(this.EnemyActionTranscript))
                throw new InvalidDataException(
                    "Semantic enemy-action transcript differs from its replay frame.");
        }
        else if (this.TargetPending is { } target)
        {
            target.Validate();
            if (this.Complete || this.Intents is not null ||
                this.HandDraw is not null ||
                this.PlayerActionSource is not { } playerSource ||
                playerSource.Phase != RunPhase.Combat ||
                this.Run.World?.Combat?.PendingChoice?
                    .ControlledTarget is null)
                throw new InvalidDataException(
                    "Invalid automatic-pipeline target stage.");
            if (target.Ordinal != this.TargetTranscript.Length)
                throw new InvalidDataException(
                    "Automatic-pipeline target ordinal differs from its transcript.");
        }
        else if (!this.Complete ||
                 this.Run.Phase is not (RunPhase.Terminal or RunPhase.Reward))
        {
            SemanticEndTurnEngineV111.ValidateOrigin(this.Origin);
        }
        this.EnemyActionEventPending?.Validate();
        if (string.IsNullOrWhiteSpace(this.NativeEncounterEntry) ||
            this.IntentTranscript is null ||
            this.EnemyActionTranscript is null ||
            this.TargetTranscript is null ||
            this.BoundaryTranscript is null ||
            (this.PlayerActionSource is null) != (this.PlayerActionId is null) ||
            (this.Complete && (this.Intents is not null ||
                (this.Run.Phase is RunPhase.Terminal or RunPhase.Reward
                    ? this.HandDraw is { AtBoundary: false }
                    : this.HandDraw is not { AtBoundary: true }))) ||
            (!this.Complete && this.Intents is null &&
             this.HandDraw is null && this.EnemyActionPending is null &&
             this.TargetPending is null) ||
            (this.Intents is not null && this.HandDraw is not null))
            throw new InvalidDataException("Invalid semantic end-turn stages.");
        if (this.Intents is { } intents)
        {
            intents.Validate();
            if (CanonicalJson.Sha256(intents.Transcript) !=
                CanonicalJson.Sha256(this.IntentTranscript) ||
                CanonicalJson.Sha256(this.Run) !=
                CanonicalJson.Sha256(this.Origin))
                throw new InvalidDataException("Invalid pending intent stage.");
        }
        if (this.HandDraw is { } hand)
        {
            hand.Validate();
            if (!this.Complete &&
                CanonicalJson.Sha256(this.Run) != CanonicalJson.Sha256(hand.Run))
                throw new InvalidDataException(
                    "Pending end-turn hand state differs from the draw continuation.");
            if (this.Complete && this.Run.Phase == RunPhase.Combat &&
                !this.AtPlayerBoundary)
                throw new InvalidDataException(
                    "Completed end-turn result is not a player decision.");
            if (this.Complete &&
                (this.Run.Phase is RunPhase.Terminal or RunPhase.Reward) &&
                !hand.AtBoundary)
                throw new InvalidDataException(
                    "Terminal end-turn retained an incomplete hand draw.");
        }
    }

    public SemanticEndTurnV111 Fork() => this with
    {
        Origin = Origin.Fork(), Run = Run.Fork(),
        Intents = Intents?.Fork(), HandDraw = HandDraw?.Fork(),
        PlayerActionSource = PlayerActionSource?.Fork(),
        Stochastic = Stochastic.Fork()
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
        var starting = playerBoundary.World?.Combat;
        if (!PrototypeGameEngine
                .IsAtCombatPlayerDecisionBoundary(playerBoundary) ||
            starting is null ||
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
        if (advanced.World?.Combat?.AutomaticPipelineContinuation?
                .ControlledEnemyAction is not null)
        {
            var suspendedEnemyAction = new SemanticEndTurnV111(
                playerBoundary.Fork(), advanced.Fork(),
                nativeEncounterEntry, null, null, [])
            {
                PlayerActionSource = playerBoundary.Fork(),
                PlayerActionId = actions[0].ActionId
            };
            Validate(suspendedEnemyAction);
            return (suspendedEnemyAction, suspendedEnemyAction.Run);
        }
        if (advanced.World?.Combat?.PendingChoice?.ControlledTarget is not null)
        {
            var suspendedTarget = new SemanticEndTurnV111(
                playerBoundary.Fork(), advanced.Fork(),
                nativeEncounterEntry, null, null, [])
            {
                PlayerActionSource = playerBoundary.Fork(),
                PlayerActionId = actions[0].ActionId
            };
            Validate(suspendedTarget);
            return (suspendedTarget, suspendedTarget.Run);
        }
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
        if (state.EnemyActionPending is { } enemyActionRequest)
        {
            var decision = new RandomIndexDecisionV111(
                enemyActionRequest with
                {
                    OrderedCandidates = (int[])
                        enemyActionRequest.OrderedCandidates.Clone()
                }, selectedIndex, "controlled-v111");
            decision.Validate();
            RunState resumed = ControlledChanceContinuationExecutorV111
                .ResolveSelectedIndex<SemanticEndTurnV111, RunState>(
                    state, enemyActionRequest, selectedIndex,
                    (checkpoint, replayedIndex) =>
                    {
                        using (ControlledChanceGuardV111.Enter())
                        using (ControlledChanceGuardV111
                            .AllowEngineOwnedPrototypeReward())
                            return PrototypeGameEngine
                                .ResumeControlledEnemyAction(
                                    checkpoint.Run.Fork(), replayedIndex);
                    });
            var transcript = state.EnemyActionTranscript.Append(decision).ToArray();
            var boundaries = EnsureBoundaryTranscript(state).Append(
                new SemanticEndTurnBoundaryChoiceV111(
                    "enemy_action", state.EnemyActionTranscript.Length)).ToArray();
            state = ContinueAutomaticPipelineBoundaryChoice(
                state, resumed, transcript,
                (SemanticTargetDecisionV111[])state.TargetTranscript.Clone(),
                boundaries, "combat.enemy_action");
        }
        else if (state.TargetPending is { } target)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= target.CandidateEnemyInstanceIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            state = ResolveTarget(state, state.Fingerprint, target.EventId,
                target.CandidateEnemyInstanceIds[selectedIndex]);
        }
        else if (state.Intents is { Pending: not null } pending)
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

    public static SemanticEndTurnV111 ResolveTarget(
        SemanticEndTurnV111 source, string fingerprint,
        string eventId, int enemyInstanceId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn target fingerprint.");
        var pending = source.TargetPending ??
            throw new InvalidOperationException(
                "No automatic-pipeline random-target event is pending.");
        var decision = SemanticTargetChoiceV111.Select(
            pending, eventId, enemyInstanceId);
        var resumed = SemanticTargetChoiceV111.Resume(
            source.Run, decision);

        var targetTranscript = source.TargetTranscript
            .Append(decision.Fork()).ToArray();
        var boundaryTranscript = EnsureBoundaryTranscript(source).Append(
            new SemanticEndTurnBoundaryChoiceV111(
                "random_target", source.TargetTranscript.Length)).ToArray();
        var next = ContinueAutomaticPipelineBoundaryChoice(
            source, resumed,
            (RandomIndexDecisionV111[])
                source.EnemyActionTranscript.Clone(),
            targetTranscript, boundaryTranscript, pending.SiteId);
        Validate(next);
        return next;
    }

    private static SemanticEndTurnV111 ContinueAutomaticPipelineBoundaryChoice(
        SemanticEndTurnV111 source,
        RunState resumed,
        RandomIndexDecisionV111[] enemyActionTranscript,
        SemanticTargetDecisionV111[] targetTranscript,
        SemanticEndTurnBoundaryChoiceV111[] boundaryTranscript,
        string siteId)
    {
        var next = source with
        {
            Run = resumed,
            EnemyActionTranscript = enemyActionTranscript,
            TargetTranscript = targetTranscript,
            BoundaryTranscript = boundaryTranscript
        };
        if (resumed.Phase is RunPhase.Reward or RunPhase.Terminal)
            return next with
            {
                Intents = null,
                HandDraw = null,
                Complete = true
            };

        var combat = resumed.World?.Combat;
        if (combat?.PendingChoice?.ControlledTarget is not null ||
            combat?.AutomaticPipelineContinuation?
                .ControlledEnemyAction is not null)
            return next;

        if (combat?.AutomaticPipelineContinuation?.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.AdvanceTurn) == true)
        {
            return BeginAtAdvanceTurn(
                resumed, source.NativeEncounterEntry) with
            {
                PlayerActionSource = source.PlayerActionSource?.Fork(),
                PlayerActionId = source.PlayerActionId,
                EnemyActionTranscript = enemyActionTranscript,
                TargetTranscript = targetTranscript,
                BoundaryTranscript = boundaryTranscript
            };
        }

        throw new NotSupportedException(
            "UNSUPPORTED_CONTROLLED_CHANCE site=" + siteId +
            " operation=unexpected_pipeline_boundary");
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

    private static SemanticEndTurnBoundaryChoiceV111[] EnsureBoundaryTranscript(
        SemanticEndTurnV111 state)
    {
        if (state.BoundaryTranscript.Length > 0)
            return (SemanticEndTurnBoundaryChoiceV111[])
                state.BoundaryTranscript.Clone();
        return Enumerable.Range(0, state.EnemyActionTranscript.Length)
            .Select(index => new SemanticEndTurnBoundaryChoiceV111(
                "enemy_action", index))
            .Concat(Enumerable.Range(0, state.TargetTranscript.Length)
                .Select(index => new SemanticEndTurnBoundaryChoiceV111(
                    "random_target", index)))
            .ToArray();
    }

    private static SemanticEndTurnV111 CompleteIntents(SemanticEndTurnV111 state)
    {
        var intents = state.Intents ??
            throw new InvalidOperationException("No source intent checkpoint.");
        if (!intents.Complete)
            throw new InvalidOperationException("Enemy intent tickets are pending.");
        intents.Validate();
        RunState handCheckpoint;
        using (ControlledChanceGuardV111.Enter())
            handCheckpoint = PrototypeGameEngine.ResumeControlledTurnAfterIntents(
                state.Run.Fork(), intents.Combat.Fork(),
                stopBeforeSemanticHandDraw: true);
        var combat = handCheckpoint.World?.Combat ??
            throw new InvalidOperationException("Next-turn combat disappeared.");
        if (combat.AutomaticPipelineContinuation?.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) != true ||
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
            result = PrototypeGameEngine.ResumeAutomaticPipelineContinuation(
                draw.Run.Fork());
        if (result.Phase is RunPhase.Terminal or RunPhase.Reward)
            return state with { Run = result, Complete = true };
        if (!PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary(result) ||
            result.World?.Combat?.SemanticDeck is not { Pending: null })
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
        if (saved.BoundaryTranscript.Length > 0)
        {
            int enemyActionIndex = 0, targetIndex = 0;
            foreach (var boundary in saved.BoundaryTranscript)
            {
                boundary.Validate();
                if (boundary.Kind == "enemy_action")
                {
                    if (boundary.TranscriptIndex != enemyActionIndex ||
                        enemyActionIndex >= saved.EnemyActionTranscript.Length)
                        throw new InvalidDataException(
                            "Semantic end-turn enemy-action transcript order is invalid.");
                    var choice = saved.EnemyActionTranscript[enemyActionIndex++];
                    choice.Validate();
                    if (choice.ProviderMode != "controlled-v111" ||
                        choice.ProbabilityStatus != "UNVERIFIED" ||
                        reproduced.EnemyActionPending is not { } request ||
                        CanonicalJson.Sha256(request) !=
                            CanonicalJson.Sha256(choice.Request))
                        throw new InvalidDataException(
                            "Semantic end-turn enemy-action transcript diverged.");
                    reproduced = Resolve(
                        reproduced, reproduced.Fingerprint,
                        choice.SelectedIndex);
                }
                else
                {
                    if (boundary.TranscriptIndex != targetIndex ||
                        targetIndex >= saved.TargetTranscript.Length)
                        throw new InvalidDataException(
                            "Semantic end-turn target transcript order is invalid.");
                    var choice = saved.TargetTranscript[targetIndex++];
                    choice.Validate();
                    if (reproduced.TargetPending is not { } targetEvent ||
                        CanonicalJson.Sha256(targetEvent) !=
                            CanonicalJson.Sha256(choice.Event))
                        throw new InvalidDataException(
                            "Semantic end-turn target eligibility changed during replay.");
                    reproduced = ResolveTarget(reproduced,
                        reproduced.Fingerprint, choice.Event.EventId,
                        choice.SelectedEnemyInstanceId);
                }
            }
            if (enemyActionIndex != saved.EnemyActionTranscript.Length ||
                targetIndex != saved.TargetTranscript.Length)
                throw new InvalidDataException(
                    "Semantic end-turn boundary transcript omitted a decision.");
        }
        else
        {
            // Snapshots written before automatic target suspension only had
            // a typed enemy-action transcript.
            foreach (var choice in saved.EnemyActionTranscript)
            {
                choice.Validate();
                if (choice.ProviderMode != "controlled-v111" ||
                    choice.ProbabilityStatus != "UNVERIFIED" ||
                    reproduced.EnemyActionPending is not { } request ||
                    CanonicalJson.Sha256(request) !=
                        CanonicalJson.Sha256(choice.Request))
                    throw new InvalidDataException(
                        "Semantic end-turn enemy-action transcript diverged.");
                reproduced = Resolve(
                    reproduced, reproduced.Fingerprint,
                    choice.SelectedIndex);
            }
        }
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
            var replayedHand = SemanticCombatDrawEngineV111.Replay(savedHand);
            var boundaryHand = reproduced.HandDraw ??
                throw new InvalidDataException(
                    "Semantic end-turn replay lost its hand-draw boundary.");
            if (CanonicalJson.Sha256(boundaryHand.Origin) !=
                CanonicalJson.Sha256(replayedHand.Origin))
                throw new InvalidDataException(
                    "Semantic end-turn hand-draw origin changed during replay.");
            if (reproduced.Complete)
            {
                if (CanonicalJson.Sha256(boundaryHand) !=
                    CanonicalJson.Sha256(replayedHand))
                    throw new InvalidDataException(
                        "Completed semantic end-turn hand draw diverged.");
            }
            else
            {
                reproduced = reproduced with
                {
                    Run = replayedHand.Run,
                    HandDraw = replayedHand
                };
                if (replayedHand.AtBoundary)
                    reproduced = CompleteHandDraw(reproduced);
            }
        }
        if (CanonicalJson.Sha256(reproduced) != CanonicalJson.Sha256(saved))
            throw new InvalidDataException(
                "Semantic end-turn full source replay disagrees with snapshot.");
        return reproduced;
    }

    public static void Validate(SemanticEndTurnV111 state) =>
        state.Validate();

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

    internal static void ValidateOrigin(RunState run)
    {
        if (run.Phase != RunPhase.Combat ||
            run.World?.Combat is not { } combat ||
            combat.AutomaticPipelineContinuation is not { } checkpoint ||
            checkpoint.NextStepIndex < 0 ||
            checkpoint.Mechanics.CurrentStepKind !=
                PrototypeAutomaticStepKind.AdvanceTurn ||
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
