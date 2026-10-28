using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>Chronological reference into typed decision transcripts kept by
/// a serialized automatic-pipeline continuation.</summary>
public sealed record SemanticEndTurnBoundaryChoiceV111(
    string Kind, int TranscriptIndex)
{
    public void Validate()
    {
        if (Kind is not ("enemy_action" or "random_target" or
                "knowledge_demon_choice" or
                "semantic_draw" or "semantic_cost" or
                "semantic_draw_insertion" or "semantic_generated_card" or
                "semantic_potion_generation" or
                "semantic_card_transform" or
                "semantic_card_shortlist" or "semantic_pool_shuffle" or
                "semantic_hand_cost_selection" or
                "semantic_card_selection" or
                "semantic_draw_hook_choice") ||
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
    public SemanticEndTurnNestedChoiceV111[] NestedChoiceTranscript
    {
        get => Stochastic.NestedChoiceTranscript;
        init => Stochastic = Stochastic with { NestedChoiceTranscript = value };
    }
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public SemanticTargetEventV111? TargetPending =>
        Complete || HandDraw is { AtBoundary: false }
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
    [JsonIgnore] public SemanticKnowledgeDemonChoiceEventV111?
        KnowledgeDemonChoicePending => Complete ? null :
            SemanticKnowledgeDemonChoiceEngineV111.DescribePending(Run);
    [JsonIgnore] public RandomIndexRequestV111? IntentPending =>
        Intents?.Pending;
    [JsonIgnore] public SemanticDeckEventV111? DrawPending
    {
        get
        {
            if (Complete) return null;
            if (HandDraw?.Pending is { } handDraw) return handDraw;
            var combat = Run.World?.Combat;
            return HandDraw is not { AtBoundary: false } &&
                PrototypeGameEngine
                    .HasPendingSupportedAutomaticPipelineSemanticDraw(Run) &&
                combat?.PendingChoice is
                { ChoiceId: "controlled_semantic_draw",
                  ControlledSemanticDraw: not null } &&
                combat.SemanticDeck is
                    { Pending: { Purpose: "draw" } } deck
                ? SemanticDeckEngineV111.Inspect(deck) : null;
        }
    }
    [JsonIgnore] public SemanticGeneratedCardEventV111?
        GeneratedCardPending => !Complete &&
            HandDraw is not { AtBoundary: false } &&
            PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticGeneration(Run)
            ? SemanticGenerationChoiceV111.DescribePending(
                Run, Fingerprint,
                NestedChoiceTranscript.Count(choice =>
                    choice.Kind == "semantic_generated_card"))
            : null;
    [JsonIgnore] public SemanticPotionGenerationEventV111?
        PotionGenerationPending => !Complete &&
            HandDraw is not { AtBoundary: false } &&
            PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticPotionGeneration(
                    Run)
            ? SemanticPotionGenerationChoiceV111.DescribePending(
                Run, Fingerprint,
                NestedChoiceTranscript.Count(choice =>
                    choice.Kind == "semantic_potion_generation"))
            : null;
    [JsonIgnore] public SemanticCardTransformEventV111?
        CardTransformPending => !Complete &&
            HandDraw is not { AtBoundary: false } &&
            PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticCardTransform(
                    Run)
            ? SemanticCardTransformChoiceV111.DescribePending(
                Run, Fingerprint,
                NestedChoiceTranscript.Count(choice =>
                    choice.Kind == "semantic_card_transform"))
            : null;
    [JsonIgnore] public SemanticCardShortlistEventV111?
        CardShortlistPending => !Complete &&
            HandDraw is not { AtBoundary: false } &&
            PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticCardShortlist(
                    Run)
            ? SemanticCardShortlistChoiceV111.DescribePending(
                Run, Fingerprint,
                NestedChoiceTranscript.Count(choice =>
                    choice.Kind == "semantic_card_shortlist"))
            : null;
    [JsonIgnore] public SemanticPoolShuffleEventV111? PoolShufflePending =>
        !Complete && HandDraw is not { AtBoundary: false } &&
            PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticPoolShuffle(Run)
            ? SemanticPoolShuffleChoiceV111.DescribePending(
                Run, Fingerprint,
                NestedChoiceTranscript.Count(choice =>
                    choice.Kind == "semantic_pool_shuffle"))
            : null;
    [JsonIgnore] public SemanticCostEventV111? NestedCostPending
    {
        get
        {
            if (Complete || HandDraw is { AtBoundary: false }) return null;
            if (Run.World?.Combat?.PendingChoice is
                    { ChoiceId: "controlled_draw_cost",
                      ControlledDrawCost: not null,
                      ControlledSemanticDraw: not null } &&
                PrototypeGameEngine
                    .HasPendingSupportedAutomaticPipelineSemanticDraw(Run))
                return SemanticCostChoiceV111.DescribeEffectDrawEvent(
                    Run, Fingerprint, NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_cost"));
            if (PrototypeGameEngine
                    .HasPendingSupportedAutomaticPipelineSemanticRandomHandCost(
                        Run) &&
                Run.World?.Combat?.PendingChoice?.ControlledRandomCost?.Operation
                    .Kind == PrototypeCombatEffectKind
                        .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed)
                return SemanticRandomHandCostChoiceV111.DescribeCostEvent(
                    Run, Fingerprint,
                    NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_cost"));
            return null;
        }
    }
    [JsonIgnore] public SemanticCardSelectionEventV111?
        NestedCardSelectionPending =>
            !Complete && HandDraw is not { AtBoundary: false } &&
            PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticCardSelection(
                    Run)
            ? SemanticCardSelectionEngineV111.DescribePending(
                Run, CanonicalJson.Sha256(Run))
            : null;
    [JsonIgnore] public SemanticDrawHookChoiceEventV111?
        NestedDrawHookChoicePending =>
            !Complete && HandDraw is not { AtBoundary: false } &&
            PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticDrawHookChoice(
                    Run)
            ? SemanticDrawHookChoiceEngineV111.DescribePending(Run)
            : null;
    [JsonIgnore] public SemanticHandCostSelectionEventV111?
        NestedHandCostSelectionPending
    {
        get
        {
            if (Complete || HandDraw is { AtBoundary: false } ||
                !PrototypeGameEngine
                    .HasPendingSupportedAutomaticPipelineSemanticRandomHandCost(
                        Run) ||
                Run.World?.Combat?.PendingChoice?.ControlledRandomCost is not
                    { Operation.Kind: PrototypeCombatEffectKind
                        .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed } randomCost)
                return null;
            var combat = Run.World!.Combat!;
            var models = randomCost.EligibleCardInstanceIds.Select(id =>
                combat.Cards.Single(card => card.InstanceId == id).CardId)
                .ToArray();
            return SemanticHandCostSelectionChoiceV111.CreateEvent(
                Fingerprint, randomCost, models,
                NestedChoiceTranscript.Count(choice =>
                    choice.Kind == "semantic_hand_cost_selection"));
        }
    }
    [JsonIgnore] public SemanticDrawInsertionEventV111?
        NestedDrawInsertionPending =>
            !Complete && HandDraw is not { AtBoundary: false } &&
            PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticDraw(Run)
                ? SemanticDrawInsertionChoiceV111
                    .DescribeAutomaticSemanticDrawContinuation(Run, Fingerprint)
                : null;
    [JsonIgnore] public SemanticRandomPileCardEventV111?
        RandomPileCardPending =>
            Complete ? null : HandDraw?.RandomPileCardPending;
    [JsonIgnore] public SemanticDrawInsertionEventV111?
        DrawInsertionPending =>
            Complete ? null : HandDraw?.DrawInsertionPending ??
                NestedDrawInsertionPending;
    [JsonIgnore] public SemanticCardSelectionEventV111?
        StratagemSelectionPending =>
            Complete ? null : HandDraw?.StratagemSelectionPending;
    [JsonIgnore] public SemanticDrawHookChoiceEventV111?
        DrawHookChoicePending =>
            Complete ? null : HandDraw?.DrawHookChoicePending ??
                NestedDrawHookChoicePending;
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
        // The split mechanics/provider hand frame is only a projection.
        // Reuse one instance throughout this read-only invariant check.
        var handDraw = this.HandDraw;
        if (this.EnemyActionPending is not null)
        {
            if (this.Complete || this.Intents is not null ||
                handDraw is not null ||
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
                handDraw is { AtBoundary: false } ||
                this.Run.World?.Combat?.PendingChoice?
                    .ControlledTarget is null ||
                !PrototypeGameEngine
                    .HasPendingControlledAutomaticPipelineChoice(this.Run))
                throw new InvalidDataException(
                    "Invalid automatic-pipeline target stage.");
            if (this.PlayerActionSource is { } playerSource)
            {
                if (!PrototypeGameEngine
                    .IsAtCombatPlayerDecisionBoundary(playerSource))
                    throw new InvalidDataException(
                        "Automatic-pipeline target lost its player-action source boundary.");
            }
            else
                SemanticEndTurnEngineV111.ValidateOrigin(this.Origin);
            if (target.Ordinal != this.TargetTranscript.Length)
                throw new InvalidDataException(
                    "Automatic-pipeline target ordinal differs from its transcript.");
        }
        else if (this.KnowledgeDemonChoicePending is { } knowledgeDemon)
        {
            knowledgeDemon.Validate();
            if (this.Complete || this.Intents is not null ||
                handDraw is not null ||
                this.Run.World?.Combat?.PendingKnowledgeDemonChoice is null ||
                this.PlayerActionSource is not { } playerSource ||
                !PrototypeGameEngine
                    .IsAtCombatPlayerDecisionBoundary(playerSource))
                throw new InvalidDataException(
                    "Invalid automatic-pipeline Knowledge Demon choice stage.");
        }
        else if (!this.Complete ||
                 !PrototypeGameEngine
                    .IsAtCombatRewardOrTerminalBoundary(this.Run))
        {
            SemanticEndTurnEngineV111.ValidateOrigin(this.Origin);
        }
        this.EnemyActionEventPending?.Validate();
        this.DrawInsertionPending?.Validate();
        this.NestedDrawHookChoicePending?.Validate();
        if (string.IsNullOrWhiteSpace(this.NativeEncounterEntry) ||
            this.IntentTranscript is null ||
            this.EnemyActionTranscript is null ||
            this.TargetTranscript is null ||
            this.BoundaryTranscript is null ||
            (this.PlayerActionSource is null) != (this.PlayerActionId is null) ||
            (this.Complete && (this.Intents is not null ||
                (PrototypeGameEngine
                    .IsAtCombatRewardOrTerminalBoundary(this.Run)
                    ? handDraw is { AtBoundary: false }
                    : handDraw is not { AtBoundary: true }))) ||
            (!this.Complete && this.Intents is null &&
             handDraw is null && this.EnemyActionPending is null &&
             this.TargetPending is null &&
             this.KnowledgeDemonChoicePending is null &&
             !PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticChoice(
                    this.Run)) ||
            (!this.Complete && handDraw is { AtBoundary: true } &&
             this.TargetPending is null &&
             !PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticChoice(this.Run)) ||
            (this.Intents is not null && handDraw is not null))
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
        if (handDraw is { } hand)
        {
            hand.Validate();
            if (!this.Complete &&
                CanonicalJson.Sha256(this.Run) != CanonicalJson.Sha256(hand.Run))
            {
                if (!hand.AtBoundary ||
                    !PrototypeGameEngine
                        .HasPendingSupportedAutomaticPipelineChoice(
                            this.Run))
                    throw new InvalidDataException(
                        "Pending end-turn hand state differs from the draw continuation.");
            }
            if (this.Complete && this.Run.Phase == RunPhase.Combat &&
                !this.AtPlayerBoundary)
                throw new InvalidDataException(
                    "Completed end-turn result is not a player decision.");
            if (this.Complete &&
                PrototypeGameEngine
                    .IsAtCombatRewardOrTerminalBoundary(this.Run) &&
                !hand.AtBoundary)
                throw new InvalidDataException(
                    "Terminal end-turn retained an incomplete hand draw.");
        }
        if (PrototypeGameEngine
            .HasPendingSupportedAutomaticPipelineSemanticDraw(this.Run))
        {
            var choice = this.Run.World!.Combat!.PendingChoice!;
            if (choice.ChoiceId == "controlled_semantic_draw")
                SemanticDrawChoiceV111.ValidateBoundary(this.Run);
            else if (choice.ChoiceId == "controlled_draw_cost")
                SemanticCostChoiceV111.ValidateDrawBoundary(this.Run);
            else if (choice.ChoiceId == "controlled_draw_insertion")
                SemanticDrawInsertionChoiceV111.ValidateBoundary(this.Run);
        }
        if (PrototypeGameEngine
            .HasPendingSupportedAutomaticPipelineSemanticGeneration(this.Run))
            SemanticGenerationChoiceV111.ValidateBoundary(this.Run);
        if (PrototypeGameEngine
            .HasPendingSupportedAutomaticPipelineSemanticPotionGeneration(
                this.Run))
            SemanticPotionGenerationChoiceV111.ValidateBoundary(this.Run);
        if (PrototypeGameEngine
            .HasPendingSupportedAutomaticPipelineSemanticCardTransform(
                this.Run))
            SemanticCardTransformChoiceV111.ValidateBoundary(this.Run,
                this.Run.World!.Combat!.PendingChoice!
                    .ControlledCardTransform!);
        if (PrototypeGameEngine
            .HasPendingSupportedAutomaticPipelineSemanticCardShortlist(
                this.Run))
            SemanticCardShortlistChoiceV111.ValidateBoundary(this.Run);
        if (PrototypeGameEngine
            .HasPendingSupportedAutomaticPipelineSemanticPoolShuffle(
                this.Run))
            SemanticPoolShuffleChoiceV111.ValidateBoundary(this.Run);
        if (PrototypeGameEngine
            .HasPendingSupportedAutomaticPipelineSemanticRandomHandCost(
                this.Run))
        {
            SemanticRandomHandCostChoiceV111.ValidateBoundary(this.Run);
            if (this.NestedHandCostSelectionPending is not null)
                SemanticRandomHandCostChoiceV111.ValidateBoundary(this.Run);
        }
        if (PrototypeGameEngine
            .HasPendingSupportedAutomaticPipelineSemanticCardSelection(
                this.Run))
            SemanticCardSelectionEngineV111.ValidatePendingBoundary(this.Run);
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
    /// A providerless draw pile must be exhausted so any new semantic shuffle
    /// is source-triggered by the later recycle; an idle Nature deck may
    /// retain its unresolved physical pile.
    /// </summary>
    public static SemanticEndTurnV111 BeginAtAdvanceTurn(
        RunState source, string nativeEncounterEntry) =>
        BeginAtAdvanceTurnCore(source, nativeEncounterEntry, audit: true);

    private static SemanticEndTurnV111 BeginAtAdvanceTurnCore(
        RunState source, string nativeEncounterEntry, bool audit)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEncounterEntry);
        if (audit)
            ValidateOrigin(source);
        var combat = source.World?.Combat ??
            throw new InvalidOperationException(
                "End-turn continuation requires an active combat.");
        var intents = ControlledTurnIntentEngineV111.Begin(
            combat, nativeEncounterEntry);
        // H8 owns the current result exclusively. Share an immutable
        // checkpoint here; deterministic resolution forks only at its real
        // mechanics transition. v111 retains its defensive deep copies.
        var state = audit
            ? new SemanticEndTurnV111(
                source.Fork(), source.Fork(), nativeEncounterEntry,
                intents, null, [])
            : new SemanticEndTurnV111(
                source, source, nativeEncounterEntry, intents, null, []);
        if (intents.Complete)
            state = CompleteIntents(state);
        if (audit)
            Validate(state);
        return state;
    }

    /// <summary>
    /// H8 has already checked one legal end-turn identity at the session
    /// boundary and executed the real environment step on a private mechanics
    /// copy. Assemble its continuation directly without another observation,
    /// another step, or repeated full-state input/output audit. The
    /// originating run is immutable while the privately advanced result is
    /// constructed. Reproducibility is checked by explicit H8 journal replay.
    /// </summary>
    internal static (SemanticEndTurnV111? Continuation, RunState Result)
        ContinueFromH8PlayerEndTurn(
        RunState playerBoundary, RunState advanced,
        string nativeEncounterEntry, string actionId)
    {
        ArgumentNullException.ThrowIfNull(playerBoundary);
        ArgumentNullException.ThrowIfNull(advanced);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEncounterEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (PrototypeGameEngine
                .IsAtCombatRewardOrTerminalBoundary(advanced))
            return (null, advanced);
        if (PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineChoice(advanced))
        {
            var suspendedChoice = new SemanticEndTurnV111(
                playerBoundary, advanced, nativeEncounterEntry,
                null, null, [])
            {
                PlayerActionSource = playerBoundary,
                PlayerActionId = actionId
            };
            return (suspendedChoice, suspendedChoice.Run);
        }
        var state = BeginAtAdvanceTurnCore(
            advanced, nativeEncounterEntry, audit: false);
        var continuation = state with
        {
            PlayerActionSource = playerBoundary,
            PlayerActionId = actionId
        };
        return (continuation, continuation.Run);
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
            !SemanticDrawPileAdapterV111
                .IsSupportedSemanticHandDrawSourceDeck(starting))
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
        if (PrototypeGameEngine
                .IsAtCombatRewardOrTerminalBoundary(advanced))
            return (null, advanced);
        if (PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineChoice(advanced))
        {
            var suspendedChoice = new SemanticEndTurnV111(
                playerBoundary.Fork(), advanced.Fork(),
                nativeEncounterEntry, null, null, [])
            {
                PlayerActionSource = playerBoundary.Fork(),
                PlayerActionId = actions[0].ActionId
            };
            Validate(suspendedChoice);
            return (suspendedChoice, suspendedChoice.Run);
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
        => ResolveCore(source, fingerprint, selectedIndex, prevalidated: false);

    /// <summary>
    /// The pilot has already validated its nested end-turn frame and checked
    /// the caller's full pilot fingerprint. Only use this synchronously before
    /// any mutation of the validated source. Public entry remains checked.
    /// </summary>
    internal static SemanticEndTurnV111 ResolveFromValidatedPilot(
        SemanticEndTurnV111 source, int selectedIndex)
        => ResolveCore(source, null, selectedIndex, prevalidated: true);

    /// <summary>
    /// H8 only: a versioned runtime cursor has verified a real pending
    /// automatic hand draw. Operates on the privately owned continuation
    /// without first duplicating its entire Origin/Run frame. The inner
    /// mechanics resolver forks the RunState it actually advances. Full
    /// end-turn auditing is deferred to an explicit journal replay.
    /// </summary>
    internal static SemanticEndTurnV111 ResolveFromH8DrawCursor(
        SemanticEndTurnV111 source, int selectedIndex)
    {
        if (source.Complete ||
            source.HandDraw is not { AtBoundary: false, Pending: not null } ||
            source.EnemyActionPending is not null ||
            source.Intents is { Pending: not null })
            throw new InvalidOperationException(
                "H8 optimized end-turn requires an active automatic hand draw.");
        return ResolveCore(source, null, selectedIndex, prevalidated: true,
            trustedH8HandDraw: true);
    }

    private static SemanticEndTurnV111 ResolveCore(
        SemanticEndTurnV111 source, string? fingerprint, int selectedIndex,
        bool prevalidated, bool trustedH8HandDraw = false)
    {
        if (!prevalidated)
        {
            SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_endturn_input_validate", () => Validate(source));
            var inputFingerprint = SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_endturn_input_fingerprint", () => source.Fingerprint);
            if (!StringComparer.Ordinal.Equals(inputFingerprint, fingerprint))
                throw new InvalidOperationException("Stale semantic end-turn fingerprint.");
        }
        if (source.Complete)
            throw new InvalidOperationException("End-turn Nature choices are complete.");
        // The H8 hand-draw branch is copy-on-write at the actual RunState
        // transition: avoid cloning the entire enclosing end-turn frame.
        // All other legacy branches preserve their defensive deep fork.
        var state = trustedH8HandDraw ? source :
            SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_endturn_state_fork", source.Fork);
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
        else if (state.NestedCardSelectionPending is { } cardSelection)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= cardSelection.Candidates.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            state = ResolveCardSelection(
                state, state.Fingerprint, cardSelection.EventId,
                cardSelection.Candidates[selectedIndex].ActionId);
        }
        else if (state.NestedDrawHookChoicePending is { } drawHookChoice)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= drawHookChoice.Candidates.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            state = ResolveDrawHookChoice(
                state, state.Fingerprint,
                drawHookChoice.Candidates[selectedIndex].ActionId);
        }
        else if (state.GeneratedCardPending is { } generatedCard)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= generatedCard.CandidateCardIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var decision = SemanticGenerationChoiceV111.Select(
                generatedCard, generatedCard.EventId,
                generatedCard.CandidateCardIds[selectedIndex]);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_generated_card",
                GeneratedCardDecision: decision.Fork());
            state = ResumeNestedSemanticChoice(state, choice);
        }
        else if (state.PotionGenerationPending is { } generatedPotion)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= generatedPotion.CandidatePotionIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var decision = SemanticPotionGenerationChoiceV111.Select(
                generatedPotion, generatedPotion.EventId,
                generatedPotion.CandidatePotionIds[selectedIndex]);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_potion_generation",
                PotionGenerationDecision: decision.Fork());
            state = ResumeNestedSemanticChoice(state, choice);
        }
        else if (state.CardTransformPending is { } cardTransform)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= cardTransform.CandidateCardIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var decision = SemanticCardTransformChoiceV111.Select(
                cardTransform, cardTransform.EventId,
                cardTransform.CandidateCardIds[selectedIndex]);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_card_transform",
                CardTransformDecision: decision.Fork());
            state = ResumeNestedSemanticChoice(state, choice);
        }
        else if (state.CardShortlistPending is { } cardShortlist)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= cardShortlist.CandidateCardInstanceIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var decision = SemanticCardShortlistChoiceV111.Select(
                cardShortlist, cardShortlist.EventId,
                cardShortlist.CandidateCardInstanceIds[selectedIndex]);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_card_shortlist",
                CardShortlistDecision: decision.Fork());
            state = ResumeNestedSemanticChoice(state, choice);
        }
        else if (state.PoolShufflePending is { } poolShuffle)
        {
            if (selectedIndex < 0 ||
                selectedIndex > poolShuffle.NextSwapIndex)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var decision = SemanticPoolShuffleChoiceV111.Select(
                poolShuffle, poolShuffle.EventId, selectedIndex);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_pool_shuffle",
                PoolShuffleDecision: decision.Fork());
            state = ResumeNestedSemanticChoice(state, choice);
        }
        else if (state.NestedCostPending is { } nestedCost)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= nestedCost.CandidateCosts.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var decision = SemanticCostChoiceV111.Select(
                nestedCost, nestedCost.EventId,
                nestedCost.CandidateCosts[selectedIndex]);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_cost", CostDecision: decision.Fork());
            state = ResumeNestedSemanticChoice(state, choice);
        }
        else if (state.NestedHandCostSelectionPending is
                 { } handCostSelection)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= handCostSelection.CandidateCardInstanceIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var decision = SemanticHandCostSelectionChoiceV111.Select(
                handCostSelection, handCostSelection.EventId,
                handCostSelection.CandidateCardInstanceIds[selectedIndex]);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_hand_cost_selection",
                HandCostSelectionDecision: decision.Fork());
            state = ResumeNestedSemanticChoice(state, choice);
        }
        else if (state.HandDraw is { AtBoundary: false } handDraw &&
                 handDraw.DrawInsertionPending is not null)
        {
            var nextHand = SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_endturn_combat_draw",
                () => SemanticCombatDrawEngineV111.ResolveFromValidatedEndTurn(
                    handDraw, selectedIndex));
            state = SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_endturn_continue_hand",
                () => ContinueHandDraw(state, nextHand));
        }
        else if (state.NestedDrawInsertionPending is { } nestedInsertion)
        {
            var decision = SemanticDrawInsertionChoiceV111.Select(
                nestedInsertion, nestedInsertion.EventId, selectedIndex);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_draw_insertion",
                DrawInsertionDecision: decision.Fork());
            state = ResumeNestedSemanticChoice(state, choice);
        }
        else if (state.HandDraw is not { AtBoundary: false } &&
                 PrototypeGameEngine
                    .HasPendingSupportedAutomaticPipelineSemanticDraw(
                        state.Run) &&
                 state.DrawPending is { Purpose: "draw" } nestedDraw &&
                 state.Run.World?.Combat?.PendingChoice?.ControlledSemanticDraw
                    is not null)
        {
            var decision = SemanticDrawChoiceV111.Select(
                nestedDraw, selectedIndex);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_draw", DrawDecision: decision.Fork());
            state = ResumeNestedSemanticChoice(state, choice);
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
            var next = SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_endturn_combat_draw",
                () => trustedH8HandDraw
                    ? SemanticCombatDrawEngineV111.ResolveFromH8Cursor(
                        hand, selectedIndex)
                    : SemanticCombatDrawEngineV111.ResolveFromValidatedEndTurn(
                        hand, selectedIndex));
            state = SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_endturn_continue_hand",
                () => ContinueHandDraw(state, next));
        }
        else
            throw new InvalidOperationException("No pending semantic end-turn choice.");
        if (!trustedH8HandDraw)
            SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_endturn_output_validate", () => Validate(state));
        return state;
    }

    public static SemanticEndTurnV111 ResolveGeneratedCard(
        SemanticEndTurnV111 source, string fingerprint, string eventId,
        string cardId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn generated-card fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var pending = source.GeneratedCardPending ??
            throw new InvalidOperationException(
                "No automatic-pipeline generated-card event is pending.");
        var decision = SemanticGenerationChoiceV111.Select(
            pending, eventId, cardId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_generated_card",
            GeneratedCardDecision: decision.Fork());
        var next = ResumeNestedSemanticChoice(source, choice);
        Validate(next);
        return next;
    }

    public static SemanticEndTurnV111 ResolvePotionGeneration(
        SemanticEndTurnV111 source, string fingerprint, string eventId,
        string potionId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn potion-generation fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(potionId);
        var pending = source.PotionGenerationPending ??
            throw new InvalidOperationException(
                "No automatic-pipeline potion-generation event is pending.");
        var decision = SemanticPotionGenerationChoiceV111.Select(
            pending, eventId, potionId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_potion_generation",
            PotionGenerationDecision: decision.Fork());
        var next = ResumeNestedSemanticChoice(source, choice);
        Validate(next);
        return next;
    }

    public static SemanticEndTurnV111 ResolveCardTransform(
        SemanticEndTurnV111 source, string fingerprint, string eventId,
        string cardId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn card-transform fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var pending = source.CardTransformPending ??
            throw new InvalidOperationException(
                "No automatic-pipeline card-transform event is pending.");
        var decision = SemanticCardTransformChoiceV111.Select(
            pending, eventId, cardId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_card_transform",
            CardTransformDecision: decision.Fork());
        var next = ResumeNestedSemanticChoice(source, choice);
        Validate(next);
        return next;
    }

    public static SemanticEndTurnV111 ResolveCardShortlist(
        SemanticEndTurnV111 source, string fingerprint, string eventId,
        long cardInstanceId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn card-shortlist fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = source.CardShortlistPending ??
            throw new InvalidOperationException(
                "No automatic-pipeline card-shortlist event is pending.");
        var decision = SemanticCardShortlistChoiceV111.Select(
            pending, eventId, cardInstanceId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_card_shortlist",
            CardShortlistDecision: decision.Fork());
        var next = ResumeNestedSemanticChoice(source, choice);
        Validate(next);
        return next;
    }

    public static SemanticEndTurnV111 ResolvePoolShuffle(
        SemanticEndTurnV111 source, string fingerprint, string eventId,
        int swapIndex)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn pool-shuffle fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = source.PoolShufflePending ??
            throw new InvalidOperationException(
                "No automatic-pipeline pool-shuffle event is pending.");
        var decision = SemanticPoolShuffleChoiceV111.Select(
            pending, eventId, swapIndex);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_pool_shuffle",
            PoolShuffleDecision: decision.Fork());
        var next = ResumeNestedSemanticChoice(source, choice);
        Validate(next);
        return next;
    }

    public static SemanticEndTurnV111 ResolveHandCostSelection(
        SemanticEndTurnV111 source, string fingerprint, string eventId,
        long cardInstanceId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn hand-cost-selection fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = source.NestedHandCostSelectionPending ??
            throw new InvalidOperationException(
                "No automatic-pipeline hand-cost-selection event is pending.");
        var decision = SemanticHandCostSelectionChoiceV111.Select(
            pending, eventId, cardInstanceId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_hand_cost_selection",
            HandCostSelectionDecision: decision.Fork());
        var next = ResumeNestedSemanticChoice(source, choice);
        Validate(next);
        return next;
    }

    public static SemanticEndTurnV111 ResolveCardSelection(
        SemanticEndTurnV111 source, string fingerprint, string eventId,
        string actionId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn card-selection fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = source.NestedCardSelectionPending ??
            throw new InvalidOperationException(
                "No automatic-pipeline card-selection event is pending.");
        var decision = SemanticCardSelectionEngineV111.Select(
            pending, eventId, actionId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_card_selection",
            CardSelectionDecision: decision.Fork());
        var next = ResumeNestedSemanticChoice(source, choice);
        Validate(next);
        return next;
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

    public static SemanticEndTurnV111 ResolveDrawInsertion(
        SemanticEndTurnV111 source, string fingerprint, string eventId,
        int position)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = source.DrawInsertionPending ??
            throw new InvalidOperationException(
                "No semantic draw insertion is pending.");
        var decision = SemanticDrawInsertionChoiceV111.Select(
            pending, eventId, position);

        if (source.HandDraw is { AtBoundary: false } hand &&
            hand.DrawInsertionPending is not null)
        {
            var nextHand = SemanticCombatDrawEngineV111.Resolve(
                hand, hand.Fingerprint, decision.SelectedPosition);
            var recorded = nextHand.DrawInsertionTranscript.LastOrDefault();
            if (recorded is null ||
                CanonicalJson.Sha256(recorded) !=
                    CanonicalJson.Sha256(decision))
                throw new InvalidDataException(
                    "Automatic hand-draw insertion transcript diverged.");
            var next = ContinueHandDraw(source, nextHand);
            Validate(next);
            return next;
        }

        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_draw_insertion",
            DrawInsertionDecision: decision.Fork());
        var nested = ResumeNestedSemanticChoice(source, choice);
        Validate(nested);
        return nested;
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
            EnemyActionTranscript = enemyActionTranscript,
            TargetTranscript = targetTranscript,
            BoundaryTranscript = boundaryTranscript
        };
        if (PrototypeGameEngine
                .IsAtCombatRewardOrTerminalBoundary(resumed))
            return CompleteAtBoundary(
                next, resumed,
                discardSuspendedFrames: next.HandDraw is not
                    { AtBoundary: true });

        if (PrototypeGameEngine
                .HasPendingControlledAutomaticPipelineChoice(resumed) &&
            (next.HandDraw is null ||
             resumed.World?.Combat?.PendingChoice?.ControlledTarget is not null))
            return next with { Run = resumed };

        if (PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticChoice(resumed))
            return next with { Run = resumed };

        if (source.Run.World?.Combat is
                { PendingChoice: { } pending } sourceCombat &&
            PrototypeGameEngine
                .IsCrossbowTurnStartGenerationContinuation(
                    sourceCombat, pending))
        {
            if (!PrototypeGameEngine
                    .IsCrossbowTurnStartDrawBoundary(resumed))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK " +
                    "site=combat.semantic_end_turn " +
                    "operation=crossbow_draw_handoff_boundary");
            var draw = SemanticCombatDrawEngineV111.BeginAutomaticHandDraw(
                resumed);
            return ContinueHandDraw(next, draw);
        }

        if (next.HandDraw is { AtBoundary: true })
            return CompleteHandDrawAtPlayerBoundary(next, resumed);

        var combat = resumed.World?.Combat;
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

    private static SemanticEndTurnV111 ResumeNestedSemanticChoice(
        SemanticEndTurnV111 source,
        SemanticEndTurnNestedChoiceV111 choice)
    {
        var resumed = SemanticNestedChoiceResumeV111.Resume(
            source.Run, choice);
        return ContinueNestedAutomaticChoice(
            source, resumed.Run, choice, resumed.SiteId);
    }

    internal static SemanticEndTurnV111 ContinueNestedAutomaticChoice(
        SemanticEndTurnV111 source, RunState resumed,
        SemanticEndTurnNestedChoiceV111 choice, string siteId)
    {
        Validate(source);
        ArgumentNullException.ThrowIfNull(resumed);
        ArgumentNullException.ThrowIfNull(choice);
        choice.Validate();
        if (source.Complete ||
            source.HandDraw is { AtBoundary: false } ||
            (!PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticChoice(
                    source.Run) &&
             source.KnowledgeDemonChoicePending is null))
            throw new InvalidOperationException(
                "No supported nested automatic semantic choice is pending.");

        var expectedKind = choice.Kind;
        if (expectedKind == "semantic_draw")
        {
            var pending = source.DrawPending;
            SemanticDrawChoiceV111.ValidateDecision(
                pending, choice.DrawDecision!);
        }
        else if (expectedKind == "semantic_cost")
        {
            var pending = source.NestedCostPending;
            SemanticCostChoiceV111.ValidateDecision(
                pending, choice.CostDecision!,
                choice.CostDecision!.SelectedIndex);
        }
        else if (expectedKind == "semantic_generated_card")
        {
            var pending = source.GeneratedCardPending;
            SemanticGenerationChoiceV111.ValidateDecision(
                pending, choice.GeneratedCardDecision!,
                choice.GeneratedCardDecision!.SelectedCardId);
        }
        else if (expectedKind == "semantic_potion_generation")
        {
            var pending = source.PotionGenerationPending;
            SemanticPotionGenerationChoiceV111.ValidateDecision(
                pending, choice.PotionGenerationDecision!,
                choice.PotionGenerationDecision!.SelectedPotionId);
        }
        else if (expectedKind == "semantic_card_transform")
        {
            var pending = source.CardTransformPending;
            SemanticCardTransformChoiceV111.ValidateDecision(
                pending, choice.CardTransformDecision!,
                choice.CardTransformDecision!.SelectedCardId);
        }
        else if (expectedKind == "semantic_card_shortlist")
        {
            var pending = source.CardShortlistPending;
            SemanticCardShortlistChoiceV111.ValidateDecision(
                pending, choice.CardShortlistDecision!,
                choice.CardShortlistDecision!.SelectedCardInstanceId);
        }
        else if (expectedKind == "semantic_pool_shuffle")
        {
            var pending = source.PoolShufflePending;
            SemanticPoolShuffleChoiceV111.ValidateDecision(
                pending, choice.PoolShuffleDecision!);
        }
        else if (expectedKind == "semantic_hand_cost_selection")
        {
            var pending = source.NestedHandCostSelectionPending;
            SemanticHandCostSelectionChoiceV111.ValidateDecision(
                pending, choice.HandCostSelectionDecision!,
                choice.HandCostSelectionDecision!.SelectedCardInstanceId);
        }
        else if (expectedKind == "semantic_card_selection")
        {
            var pending = source.NestedCardSelectionPending;
            SemanticCardSelectionEngineV111.ValidateDecision(
                pending, choice.CardSelectionDecision!);
        }
        else if (expectedKind == "semantic_draw_hook_choice")
        {
            var pending = source.NestedDrawHookChoicePending;
            if (pending is null ||
                CanonicalJson.Sha256(pending) !=
                    CanonicalJson.Sha256(
                        choice.DrawHookChoiceDecision!.Event))
                throw new InvalidDataException(
                    "Nested draw-hook choice differs from its source event.");
            choice.DrawHookChoiceDecision.Validate();
        }
        else if (expectedKind == "knowledge_demon_choice")
        {
            var pending = source.KnowledgeDemonChoicePending;
            var decision = choice.KnowledgeDemonChoiceDecision!;
            if (pending is null ||
                CanonicalJson.Sha256(pending) !=
                    CanonicalJson.Sha256(decision.Event))
                throw new InvalidDataException(
                    "Nested Knowledge Demon choice differs from its source event.");
            decision.Validate();
        }
        else
        {
            SemanticDrawInsertionChoiceV111.ValidateDecision(
                source.NestedDrawInsertionPending,
                choice.DrawInsertionDecision!,
                choice.DrawInsertionDecision!.SelectedPosition);
        }

        var nestedTranscript = source.NestedChoiceTranscript.Append(
            choice.Fork()).ToArray();
        var boundaries = EnsureBoundaryTranscript(source).Append(
            new SemanticEndTurnBoundaryChoiceV111(
                expectedKind, source.NestedChoiceTranscript.Count(item =>
                    item.Kind == expectedKind))).ToArray();
        var next = ContinueAutomaticPipelineBoundaryChoice(
            source with { NestedChoiceTranscript = nestedTranscript },
            resumed,
            (RandomIndexDecisionV111[])source.EnemyActionTranscript.Clone(),
            (SemanticTargetDecisionV111[])source.TargetTranscript.Clone(),
            boundaries, siteId);
        Validate(next);
        return next;
    }

    public static SemanticEndTurnV111 ResolveDrawHookChoice(
        SemanticEndTurnV111 source, string fingerprint, string actionId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException("Stale semantic end-turn fingerprint.");
        if (source.Complete)
            throw new InvalidOperationException(
                "No automatic CardDrawn hook action is pending.");
        if (source.NestedDrawHookChoicePending is { } nestedHookChoice)
        {
            var decision = SemanticDrawHookChoiceEngineV111.Select(
                source.Run, nestedHookChoice, actionId);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_draw_hook_choice",
                DrawHookChoiceDecision: decision.Fork());
            var nested = ResumeNestedSemanticChoice(source, choice);
            Validate(nested);
            return nested;
        }
        if (source.HandDraw is not { } hand ||
            hand.DrawHookChoicePending is null)
            throw new InvalidOperationException(
                "No automatic CardDrawn hook action is pending.");
        var nextHand = SemanticCombatDrawEngineV111.ResolveDrawHookChoice(
            hand, hand.Fingerprint, actionId);
        var state = ContinueHandDraw(source, nextHand);
        Validate(state);
        return state;
    }

    public static SemanticEndTurnV111 ResolveKnowledgeDemonChoice(
        SemanticEndTurnV111 source, string fingerprint, string actionId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn fingerprint.");
        if (source.Complete ||
            source.KnowledgeDemonChoicePending is not { } pending)
            throw new InvalidOperationException(
                "No automatic Knowledge Demon choice is pending.");
        var decision = SemanticKnowledgeDemonChoiceEngineV111.Select(
            source.Run, pending, actionId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "knowledge_demon_choice",
            KnowledgeDemonChoiceDecision: decision.Fork());
        var next = ResumeNestedSemanticChoice(source, choice);
        Validate(next);
        return next;
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
        var state = ContinueHandDraw(source, nextHand);
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
        RunState handCheckpoint;
        using (ControlledChanceGuardV111.Enter())
            handCheckpoint = PrototypeGameEngine.ResumeControlledTurnAfterIntents(
                state.Run.Fork(),
                stopBeforeSemanticHandDraw: true);
        if (PrototypeGameEngine
                .IsAtCombatRewardOrTerminalBoundary(handCheckpoint))
            return CompleteAtBoundary(
                state with { Intents = null }, handCheckpoint,
                discardSuspendedFrames: true);
        if (handCheckpoint.World?.Combat is
                { PendingChoice: { } pending } combat &&
            PrototypeGameEngine
                .IsCrossbowTurnStartGenerationContinuation(
                    combat, pending))
            return state with { Run = handCheckpoint, Intents = null };
        var draw = SemanticCombatDrawEngineV111.BeginAutomaticHandDraw(
            handCheckpoint);
        return ContinueHandDraw(state with { Intents = null }, draw);
    }

    /// <summary>
    /// H8 M2 native transition handoff. The session already owns a valid
    /// automatic hand-draw cursor; its mechanics RunState was advanced by
    /// the game engine, not by a nested semantic draw resolver. Reattach the
    /// resulting projection once at the boundary (for legacy snapshot/replay)
    /// and perform normal automatic-pipeline completion when appropriate.
    /// No recursive semantic output audit runs on this trusted path.
    /// </summary>
    internal static SemanticEndTurnV111 ContinueFromH8AdvancedDraw(
        SemanticEndTurnV111 source, SemanticCombatDrawV111 advancedDraw)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(advancedDraw);
        if (source.Complete ||
            source.HandDraw is not { AtBoundary: false, Pending: not null } ||
            !advancedDraw.AutomaticHandDraw ||
            !advancedDraw.FromHandDraw)
            throw new InvalidOperationException(
                "H8 automatic draw cursor is no longer active.");
        return ContinueHandDraw(source, advancedDraw, trustedH8: true);
    }

    private static SemanticEndTurnV111 ContinueHandDraw(
        SemanticEndTurnV111 source, SemanticCombatDrawV111 handDraw,
        bool trustedH8 = false)
    {
        var next = source with
        {
            Run = handDraw.Run,
            HandDraw = handDraw
        };
        return handDraw.AtBoundary ? CompleteHandDraw(next, trustedH8) : next;
    }

    private static SemanticEndTurnV111 CompleteHandDraw(
        SemanticEndTurnV111 state, bool trustedH8)
    {
        if (state.HandDraw is not { AtBoundary: true } draw)
            throw new InvalidOperationException(
                "Cannot finish end turn with pending drawn-card choices.");
        RunState result;
        // This is real resumed turn mechanics, formerly included in the
        // profiler's broad "legacy projection" bucket. Keep it independent
        // of snapshot publication and provider bookkeeping.
        using (H8HotPathProfilerV1.Begin("h8.draw.resume_turn_mechanics"))
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeAutomaticPipelineContinuation(
                draw.Run.Fork());
        if (PrototypeGameEngine
                .IsAtCombatRewardOrTerminalBoundary(result))
            return CompleteAtBoundary(
                state, result, discardSuspendedFrames: false);
        if (result.World?.Combat?.PendingChoice?.ControlledTarget is not null ||
            PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticChoice(result))
            return state with { Run = result };
        return CompleteHandDrawAtPlayerBoundary(state, result, trustedH8);
    }

    private static SemanticEndTurnV111 CompleteHandDrawAtPlayerBoundary(
        SemanticEndTurnV111 state, RunState result,
        bool trustedH8 = false)
    {
        if (!PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary(result))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.semantic_end_turn " +
                "operation=not_at_next_player_boundary");
        if (result.World?.Combat is not { } combat)
            throw new InvalidDataException(
                "Completed semantic end-turn lost its combat state.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Completed semantic end-turn has no Nature deck.",
            "Completed semantic end-turn has an invalid Nature deck boundary.");
        // H8 will construct its source-public observation and action IDs at
        // the version-owned player boundary. Creating a whole legacy frame
        // here also hashes hidden RunState and repeats all action encoding.
        // Check only action existence on the trusted transition. Keep the
        // externally checked v111 validation path byte-for-byte unchanged.
        bool hasActions;
        using (H8HotPathProfilerV1.Begin("h8.draw.next_player_check"))
            hasActions = trustedH8
                ? new PrototypeGameEngine().GetLegalActions(result).Any()
                : new PrototypeAiEnvironment().Observe(result)
                    .LegalActions.Any();
        if (!hasActions)
            throw new InvalidOperationException(
                "The next player boundary has no available legal actions.");
        return CompleteAtBoundary(
            state, result, discardSuspendedFrames: false);
    }

    private static SemanticEndTurnV111 CompleteAtBoundary(
        SemanticEndTurnV111 source, RunState run,
        bool discardSuspendedFrames)
    {
        var completed = source with { Run = run };
        if (discardSuspendedFrames)
            completed = completed with
            {
                Intents = null,
                HandDraw = null
            };
        return completed with { Complete = true };
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
        int boundaryIndex = 0, enemyActionIndex = 0, targetIndex = 0;
        int nestedChoiceIndex = 0, nestedDrawIndex = 0, nestedCostIndex = 0,
            nestedInsertionIndex = 0, nestedGeneratedCardIndex = 0,
            nestedPotionGenerationIndex = 0, nestedCardTransformIndex = 0,
            nestedCardShortlistIndex = 0, nestedPoolShuffleIndex = 0,
            nestedHandCostSelectionIndex = 0, nestedCardSelectionIndex = 0,
            nestedDrawHookChoiceIndex = 0,
            nestedKnowledgeDemonChoiceIndex = 0;
        SemanticEndTurnV111 ResumeNestedChoice(
            SemanticEndTurnNestedChoiceV111 choice)
        {
            return ResumeNestedSemanticChoice(reproduced, choice);
        }
        void ReplayAvailableBoundaryChoices()
        {
            while (boundaryIndex < saved.BoundaryTranscript.Length)
            {
                var hasEnemyAction = reproduced.EnemyActionPending is not null;
                var hasTarget = reproduced.TargetPending is not null;
                var hasNestedDraw =
                    reproduced.HandDraw is not { AtBoundary: false } &&
                    reproduced.Run.World?.Combat?.PendingChoice is
                        { ChoiceId: "controlled_semantic_draw",
                          ControlledSemanticDraw: not null } &&
                    PrototypeGameEngine
                        .HasPendingSupportedAutomaticPipelineSemanticDraw(
                            reproduced.Run);
                var hasNestedCost = reproduced.NestedCostPending is not null;
                var hasNestedInsertion =
                    reproduced.NestedDrawInsertionPending is not null;
                var hasNestedGeneratedCard =
                    reproduced.GeneratedCardPending is not null;
                var hasNestedPotionGeneration =
                    reproduced.PotionGenerationPending is not null;
                var hasNestedCardTransform =
                    reproduced.CardTransformPending is not null;
                var hasNestedCardShortlist =
                    reproduced.CardShortlistPending is not null;
                var hasNestedPoolShuffle =
                    reproduced.PoolShufflePending is not null;
                var hasNestedHandCostSelection =
                    reproduced.NestedHandCostSelectionPending is not null;
                var hasNestedCardSelection =
                    reproduced.NestedCardSelectionPending is not null;
                var hasNestedDrawHookChoice =
                    reproduced.NestedDrawHookChoicePending is not null;
                var hasKnowledgeDemonChoice =
                    reproduced.KnowledgeDemonChoicePending is not null;
                if (!hasEnemyAction && !hasTarget &&
                    !hasNestedDraw && !hasNestedCost && !hasNestedInsertion &&
                    !hasNestedGeneratedCard && !hasNestedPotionGeneration &&
                    !hasNestedCardTransform && !hasNestedCardShortlist &&
                    !hasNestedPoolShuffle && !hasNestedHandCostSelection &&
                    !hasNestedCardSelection && !hasNestedDrawHookChoice &&
                    !hasKnowledgeDemonChoice)
                    return;

                var boundary = saved.BoundaryTranscript[boundaryIndex];
                boundary.Validate();
                if (boundary.Kind == "enemy_action")
                {
                    if (!hasEnemyAction ||
                        boundary.TranscriptIndex != enemyActionIndex ||
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
                else if (boundary.Kind == "random_target")
                {
                    if (!hasTarget ||
                        boundary.TranscriptIndex != targetIndex ||
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
                else if (boundary.Kind == "semantic_draw")
                {
                    if (!hasNestedDraw ||
                        boundary.TranscriptIndex != nestedDrawIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_draw", DrawDecision: { } drawChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn nested draw transcript order is invalid.");
                    drawChoice.Validate();
                    var pending = reproduced.DrawPending ??
                        throw new InvalidDataException(
                            "Semantic end-turn replay lost its nested draw event.");
                    SemanticDrawChoiceV111.ValidateDecision(pending, drawChoice);
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedDrawIndex++;
                }
                else if (boundary.Kind == "semantic_cost")
                {
                    if (!hasNestedCost ||
                        boundary.TranscriptIndex != nestedCostIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_cost", CostDecision: { } costChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn nested cost transcript order is invalid.");
                    costChoice.Validate();
                    SemanticCostChoiceV111.ValidateDecision(
                        reproduced.NestedCostPending, costChoice,
                        costChoice.SelectedIndex);
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedCostIndex++;
                }
                else if (boundary.Kind == "semantic_draw_insertion")
                {
                    if (!hasNestedInsertion ||
                        boundary.TranscriptIndex != nestedInsertionIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_draw_insertion",
                              DrawInsertionDecision: { } insertionChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn nested draw-insertion transcript order is invalid.");
                    insertionChoice.Validate();
                    SemanticDrawInsertionChoiceV111.ValidateDecision(
                        reproduced.NestedDrawInsertionPending,
                        insertionChoice,
                        insertionChoice.SelectedPosition);
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedInsertionIndex++;
                }
                else if (boundary.Kind == "semantic_generated_card")
                {
                    if (!hasNestedGeneratedCard ||
                        boundary.TranscriptIndex != nestedGeneratedCardIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_generated_card",
                              GeneratedCardDecision: { } generatedChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn generated-card transcript order is invalid.");
                    var pending = reproduced.GeneratedCardPending ??
                        throw new InvalidDataException(
                            "Semantic end-turn replay lost its generated-card event.");
                    SemanticGenerationChoiceV111.ValidateDecision(
                        pending, generatedChoice,
                        generatedChoice.SelectedCardId);
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedGeneratedCardIndex++;
                }
                else if (boundary.Kind == "semantic_potion_generation")
                {
                    if (!hasNestedPotionGeneration ||
                        boundary.TranscriptIndex != nestedPotionGenerationIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_potion_generation",
                              PotionGenerationDecision: { } potionChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn potion-generation transcript order is invalid.");
                    var pending = reproduced.PotionGenerationPending ??
                        throw new InvalidDataException(
                            "Semantic end-turn replay lost its potion-generation event.");
                    SemanticPotionGenerationChoiceV111.ValidateDecision(
                        pending, potionChoice,
                        potionChoice.SelectedPotionId);
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedPotionGenerationIndex++;
                }
                else if (boundary.Kind == "semantic_card_transform")
                {
                    if (!hasNestedCardTransform ||
                        boundary.TranscriptIndex != nestedCardTransformIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_card_transform",
                              CardTransformDecision: { } transformChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn card-transform transcript order is invalid.");
                    var pending = reproduced.CardTransformPending ??
                        throw new InvalidDataException(
                            "Semantic end-turn replay lost its card-transform event.");
                    SemanticCardTransformChoiceV111.ValidateDecision(
                        pending, transformChoice,
                        transformChoice.SelectedCardId);
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedCardTransformIndex++;
                }
                else if (boundary.Kind == "semantic_card_shortlist")
                {
                    if (!hasNestedCardShortlist ||
                        boundary.TranscriptIndex != nestedCardShortlistIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_card_shortlist",
                              CardShortlistDecision: { } shortlistChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn card-shortlist transcript order is invalid.");
                    var pending = reproduced.CardShortlistPending ??
                        throw new InvalidDataException(
                            "Semantic end-turn replay lost its card-shortlist event.");
                    SemanticCardShortlistChoiceV111.ValidateDecision(
                        pending, shortlistChoice,
                        shortlistChoice.SelectedCardInstanceId);
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedCardShortlistIndex++;
                }
                else if (boundary.Kind == "semantic_pool_shuffle")
                {
                    if (!hasNestedPoolShuffle ||
                        boundary.TranscriptIndex != nestedPoolShuffleIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_pool_shuffle",
                              PoolShuffleDecision: { } shuffleChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn pool-shuffle transcript order is invalid.");
                    var pending = reproduced.PoolShufflePending ??
                        throw new InvalidDataException(
                            "Semantic end-turn replay lost its pool-shuffle event.");
                    SemanticPoolShuffleChoiceV111.ValidateDecision(
                        pending, shuffleChoice);
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedPoolShuffleIndex++;
                }
                else if (boundary.Kind == "semantic_card_selection")
                {
                    if (!hasNestedCardSelection ||
                        boundary.TranscriptIndex != nestedCardSelectionIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_card_selection",
                              CardSelectionDecision: { } cardSelectionChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn card-selection transcript order is invalid.");
                    var pending = reproduced.NestedCardSelectionPending ??
                        throw new InvalidDataException(
                            "Semantic end-turn replay lost its card-selection event.");
                    SemanticCardSelectionEngineV111.ValidateDecision(
                        pending, cardSelectionChoice);
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedCardSelectionIndex++;
                }
                else if (boundary.Kind == "semantic_draw_hook_choice")
                {
                    if (!hasNestedDrawHookChoice ||
                        boundary.TranscriptIndex !=
                            nestedDrawHookChoiceIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_draw_hook_choice",
                              DrawHookChoiceDecision: { } hookChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn draw-hook transcript order is invalid.");
                    var pending = reproduced.NestedDrawHookChoicePending ??
                        throw new InvalidDataException(
                            "Semantic end-turn replay lost its draw-hook event.");
                    if (CanonicalJson.Sha256(pending) !=
                        CanonicalJson.Sha256(hookChoice.Event))
                        throw new InvalidDataException(
                            "Semantic end-turn draw-hook event changed during replay.");
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedDrawHookChoiceIndex++;
                }
                else if (boundary.Kind == "semantic_hand_cost_selection")
                {
                    if (!hasNestedHandCostSelection ||
                        boundary.TranscriptIndex !=
                            nestedHandCostSelectionIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "semantic_hand_cost_selection",
                              HandCostSelectionDecision: { } handCostChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn hand-cost-selection transcript order is invalid.");
                    var pending = reproduced.NestedHandCostSelectionPending ??
                        throw new InvalidDataException(
                            "Semantic end-turn replay lost its hand-cost-selection event.");
                    SemanticHandCostSelectionChoiceV111.ValidateDecision(
                        pending, handCostChoice,
                        handCostChoice.SelectedCardInstanceId);
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedHandCostSelectionIndex++;
                }
                else if (boundary.Kind == "knowledge_demon_choice")
                {
                    if (!hasKnowledgeDemonChoice ||
                        boundary.TranscriptIndex !=
                            nestedKnowledgeDemonChoiceIndex ||
                        nestedChoiceIndex >=
                            saved.NestedChoiceTranscript.Length ||
                        saved.NestedChoiceTranscript[nestedChoiceIndex] is not
                            { Kind: "knowledge_demon_choice",
                              KnowledgeDemonChoiceDecision: { } demonChoice })
                        throw new InvalidDataException(
                            "Semantic end-turn Knowledge Demon transcript order is invalid.");
                    var pending = reproduced.KnowledgeDemonChoicePending ??
                        throw new InvalidDataException(
                            "Semantic end-turn replay lost its Knowledge Demon event.");
                    if (CanonicalJson.Sha256(pending) !=
                        CanonicalJson.Sha256(demonChoice.Event))
                        throw new InvalidDataException(
                            "Knowledge Demon choice event changed during replay.");
                    reproduced = ResumeNestedChoice(
                        saved.NestedChoiceTranscript[nestedChoiceIndex++]);
                    nestedKnowledgeDemonChoiceIndex++;
                }
                boundaryIndex++;
            }
        }
        if (saved.BoundaryTranscript.Length > 0)
            ReplayAvailableBoundaryChoices();
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
                reproduced = ContinueHandDraw(reproduced, replayedHand);
        }
        if (saved.BoundaryTranscript.Length > 0)
        {
            ReplayAvailableBoundaryChoices();
            if (boundaryIndex != saved.BoundaryTranscript.Length ||
                enemyActionIndex != saved.EnemyActionTranscript.Length ||
                targetIndex != saved.TargetTranscript.Length ||
                nestedChoiceIndex != saved.NestedChoiceTranscript.Length ||
                nestedDrawIndex != saved.NestedChoiceTranscript.Count(choice =>
                    choice.Kind == "semantic_draw") ||
                nestedCostIndex != saved.NestedChoiceTranscript.Count(choice =>
                    choice.Kind == "semantic_cost") ||
                nestedInsertionIndex != saved.NestedChoiceTranscript.Count(
                    choice => choice.Kind == "semantic_draw_insertion") ||
                nestedGeneratedCardIndex != saved.NestedChoiceTranscript.Count(
                    choice => choice.Kind == "semantic_generated_card") ||
                nestedPotionGenerationIndex !=
                    saved.NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_potion_generation") ||
                nestedCardTransformIndex !=
                    saved.NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_card_transform") ||
                nestedCardShortlistIndex !=
                    saved.NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_card_shortlist") ||
                nestedPoolShuffleIndex !=
                    saved.NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_pool_shuffle") ||
                nestedHandCostSelectionIndex !=
                    saved.NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_hand_cost_selection") ||
                nestedCardSelectionIndex !=
                    saved.NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_card_selection") ||
                nestedDrawHookChoiceIndex !=
                    saved.NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_draw_hook_choice") ||
                nestedKnowledgeDemonChoiceIndex !=
                    saved.NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "knowledge_demon_choice"))
                throw new InvalidDataException(
                    "Semantic end-turn boundary transcript omitted a decision.");
        }
        if (CanonicalJson.Sha256(reproduced) != CanonicalJson.Sha256(saved))
            throw new InvalidDataException(
                "Semantic end-turn full source replay disagrees with snapshot.");
        return reproduced;
    }

    public static void Validate(SemanticEndTurnV111 state) =>
        state.Validate();

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
            !SemanticDrawPileAdapterV111
                .IsSupportedSemanticHandDrawSourceDeck(combat) ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new InvalidOperationException(
                "Semantic end-turn requires an exhausted draw pile or an " +
                "existing idle Nature deck at the genuine post-enemy-action " +
                "AdvanceTurn checkpoint.");
    }
}
