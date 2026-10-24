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
    RandomIndexRequestV111 Request, string SupportFingerprint,
    string[]? CandidateLabels = null)
{
    private string[]? _candidateLabels = CandidateLabels is null
        ? null : (string[])CandidateLabels.Clone();

    public string[]? CandidateLabels
    {
        get => _candidateLabels is null ? null :
            (string[])_candidateLabels.Clone();
        init => _candidateLabels = value is null ? null :
            (string[])value.Clone();
    }

    public const string CurrentSchemaId =
        "sts2-v111-semantic-end-turn-index-event-v2";

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Request);
        Request.Validate();
        var candidateLabels = this.CandidateLabels;
        if (candidateLabels is not null &&
            (candidateLabels.Length != Request.SupportCount ||
             candidateLabels.Any(string.IsNullOrWhiteSpace) ||
             candidateLabels.Distinct(StringComparer.Ordinal).Count() !=
                 candidateLabels.Length))
            throw new InvalidDataException(
                "Invalid semantic enemy-action candidate labels.");
        var supportFingerprint = CanonicalJson.Sha256(Request);
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = SemanticPilotCombatV111.SchemaId,
            boundaryFingerprint = BoundaryFingerprint,
            request = Request,
            candidateLabels
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
        string boundaryFingerprint, RandomIndexRequestV111 request,
        string[]? candidateLabels = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var detachedRequest = request with
        {
            OrderedCandidates = (int[])request.OrderedCandidates.Clone()
        };
        var detachedLabels = candidateLabels is null ? null :
            (string[])candidateLabels.Clone();
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = SemanticPilotCombatV111.SchemaId,
            boundaryFingerprint,
            request = detachedRequest,
            candidateLabels = detachedLabels
        });
        var semanticEvent = new SemanticEndTurnIndexEventV111(
            CurrentSchemaId, eventId, boundaryFingerprint, detachedRequest,
            CanonicalJson.Sha256(detachedRequest), detachedLabels);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public int IndexOfCandidateLabel(string candidateLabel)
    {
        Validate();
        return ControlledChanceContinuationExecutorV111
            .IndexOfCandidateLabel(
                CandidateLabels, Request.SupportCount, candidateLabel);
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

    public SemanticIntentTranscriptEntryV111[] SemanticIntentTranscript
    {
        get => Stochastic.SemanticIntentTranscript;
        init => Stochastic = Stochastic with
        {
            SemanticIntentTranscript = value
        };
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
    public int BoundaryTranscriptVersion
    {
        get => Stochastic.BoundaryTranscriptVersion;
        init => Stochastic = Stochastic with
        {
            BoundaryTranscriptVersion = value
        };
    }
    public SemanticEndTurnNestedChoiceV111[] NestedChoiceTranscript
    {
        get => Stochastic.NestedChoiceTranscript;
        init => Stochastic = Stochastic with { NestedChoiceTranscript = value };
    }
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public SemanticTargetEventV111? TargetPending =>
        Complete
            ? null
            : HandDraw is { AtBoundary: false } handDraw
                ? handDraw.TargetPending
                : SemanticPilotCombatEngineV111.DescribePendingTargetEvent(
                    Run, Fingerprint, TargetTranscript.Length);
    [JsonIgnore] public SemanticEndTurnIndexEventV111?
        EnemyActionEventPending
    {
        get
        {
            if (Complete || EnemyActionPending is not { } request)
                return null;
            var labels = Run.World?.Combat?.AutomaticPipelineContinuation?
                .Stochastic.ControlledEnemyAction?.PendingCandidateLabels;
            return SemanticEndTurnIndexEventV111.Create(
                Fingerprint, request, labels);
        }
    }
    [JsonIgnore] public RandomIndexRequestV111? EnemyActionPending =>
        Complete ? null : Run.World?.Combat?.AutomaticPipelineContinuation?
            .ControlledEnemyAction?.PendingRequest;
    [JsonIgnore] public SemanticKnowledgeDemonChoiceEventV111?
        KnowledgeDemonChoicePending => Complete ? null :
            SemanticKnowledgeDemonChoiceEngineV111.DescribePending(Run);
    [JsonIgnore] public RandomIndexRequestV111? IntentPending =>
        Intents?.Pending;
    [JsonIgnore] public SemanticIntentEventV111? IntentChoicePending
    {
        get
        {
            var intents = Intents;
            if (Complete || intents is null)
                return null;
            var request = intents.Pending;
            if (request is null)
                return null;
            var support = ControlledTurnIntentEngineV111
                .DescribePendingSemanticIntent(intents);
            return support is null ? null : SemanticIntentChoiceV111.CreateEvent(
                Fingerprint, request, support);
        }
    }
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
                combat.Stochastic.SemanticDeck is
                    { Pending: { Purpose: "draw" } } deck
                ? SemanticDeckEngineV111.Inspect(deck) : null;
        }
    }
    [JsonIgnore] public SemanticGeneratedCardEventV111?
        GeneratedCardPending
    {
        get
        {
            if (Complete) return null;
            if (HandDraw is { AtBoundary: false } handDraw)
                return handDraw.GeneratedCardPending;
            return PrototypeGameEngine
                    .HasPendingSupportedAutomaticPipelineSemanticGeneration(Run)
                ? SemanticGenerationChoiceV111.DescribePending(
                    Run, Fingerprint,
                    NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_generated_card"))
                : null;
        }
    }
    [JsonIgnore] public SemanticPotionGenerationEventV111?
        PotionGenerationPending
    {
        get
        {
            if (Complete) return null;
            if (HandDraw is { AtBoundary: false } handDraw)
                return handDraw.PotionGenerationPending;
            return PrototypeGameEngine
                    .HasPendingSupportedAutomaticPipelineSemanticPotionGeneration(
                        Run)
                ? SemanticPotionGenerationChoiceV111.DescribePending(
                    Run, Fingerprint,
                    NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_potion_generation"))
                : null;
        }
    }
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
    [JsonIgnore] public SemanticPoolShuffleEventV111? PoolShufflePending
    {
        get
        {
            if (Complete) return null;
            if (HandDraw is { AtBoundary: false } handDraw)
                return handDraw.PoolShufflePending;
            return PrototypeGameEngine
                    .HasPendingSupportedAutomaticPipelineSemanticPoolShuffle(
                        Run)
                ? SemanticPoolShuffleChoiceV111.DescribePending(
                    Run, Fingerprint,
                    NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_pool_shuffle"))
                : null;
        }
    }
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
        NestedCardSelectionPending
    {
        get
        {
            if (Complete) return null;
            if (HandDraw is { AtBoundary: false } handDraw)
                return handDraw.CardSelectionPending;
            return PrototypeGameEngine
                    .HasPendingSupportedAutomaticPipelineSemanticCardSelection(
                        Run)
                ? SemanticCardSelectionEngineV111.DescribePending(
                    Run, CanonicalJson.Sha256(Run))
                : null;
        }
    }
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
                .AutomaticPipelineContinuation!.Stochastic.ControlledEnemyAction!;
            cursor.Validate(PrototypeContent.Rules.EndTurnPipeline.Length);
            if (CanonicalJson.Sha256(cursor.Decisions) !=
                    CanonicalJson.Sha256(this.Stochastic.EnemyActionTranscript) ||
                this.Stochastic.EnemyActionCandidateLabelsVersion == 1 &&
                    CanonicalJson.Sha256(cursor.DecisionCandidateLabels ??
                        Enumerable.Repeat<string[]>(
                            null!, cursor.Decisions.Length).ToArray()) !=
                    CanonicalJson.Sha256(
                        this.Stochastic.EnemyActionCandidateLabels))
                throw new InvalidDataException(
                    "Semantic enemy-action transcript differs from its replay frame.");
        }
        else if (this.TargetPending is { } target)
        {
            target.Validate();
            bool handDrawOwnsTarget =
                this.HandDraw is { AtBoundary: false } handDraw &&
                handDraw.TargetPending is not null;
            if (this.Complete || this.Intents is not null ||
                (this.HandDraw is { AtBoundary: false } &&
                 !handDrawOwnsTarget) ||
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
            if (target.Ordinal != (handDrawOwnsTarget
                    ? this.HandDraw!.TargetTranscript.Length
                    : this.Stochastic.TargetTranscript.Length))
                throw new InvalidDataException(
                    "Automatic-pipeline target ordinal differs from its transcript.");
        }
        else if (this.KnowledgeDemonChoicePending is { } knowledgeDemon)
        {
            knowledgeDemon.Validate();
            if (this.Complete || this.Intents is not null ||
                this.HandDraw is not null ||
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
        this.IntentChoicePending?.Validate();
        this.DrawInsertionPending?.Validate();
        this.GeneratedCardPending?.Validate();
        this.NestedDrawHookChoicePending?.Validate();
        if (string.IsNullOrWhiteSpace(this.NativeEncounterEntry) ||
            this.Stochastic.IntentTranscript is null ||
            this.Stochastic.EnemyActionTranscript is null ||
            this.Stochastic.TargetTranscript is null ||
            this.Stochastic.BoundaryTranscript is null ||
            (this.PlayerActionSource is null) != (this.PlayerActionId is null) ||
            (this.Complete && (this.Intents is not null ||
                (PrototypeGameEngine
                    .IsAtCombatRewardOrTerminalBoundary(this.Run)
                    ? this.HandDraw is { AtBoundary: false }
                    : this.HandDraw is not { AtBoundary: true }))) ||
            (!this.Complete && this.Intents is null &&
             this.HandDraw is null && this.EnemyActionPending is null &&
             this.TargetPending is null &&
             this.KnowledgeDemonChoicePending is null &&
             !PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticChoice(
                    this.Run)) ||
            (!this.Complete && this.HandDraw is { AtBoundary: true } &&
             this.TargetPending is null &&
             !PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticChoice(this.Run)) ||
            (this.Intents is not null && this.HandDraw is not null))
            throw new InvalidDataException("Invalid semantic end-turn stages.");
        if (this.Intents is { } intents)
        {
            intents.Validate();
            if (CanonicalJson.Sha256(intents.Transcript) !=
                CanonicalJson.Sha256(this.Stochastic.IntentTranscript) ||
                CanonicalJson.Sha256(this.Run) !=
                CanonicalJson.Sha256(this.Origin))
                throw new InvalidDataException("Invalid pending intent stage.");
        }
        if (this.HandDraw is { } hand)
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
        RunState source, string nativeEncounterEntry)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEncounterEntry);
        ValidateOrigin(source);
        var intents = PrototypeGameEngine
            .BeginAutomaticPipelineIntents(
                source, nativeEncounterEntry);
        var state = new SemanticEndTurnV111(
            source.Fork(), source.Fork(), nativeEncounterEntry,
            intents, null, [])
        {
            BoundaryTranscriptVersion =
                SemanticEndTurnStochasticStateV111
                    .CurrentBoundaryTranscriptVersion
        };
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
        var advanced = ControlledChanceGuardV111.ExecuteGuarded(
            () => environment.Step(
                playerBoundary.Fork(), actions[0].ActionId).State,
            allowEngineOwnedPrototypeReward: true);
        var resumeBoundary = PrototypeGameEngine
            .ClassifyAutomaticPipelineResumeBoundary(advanced);
        if (resumeBoundary ==
            PrototypeAutomaticPipelineResumeBoundaryKindV111.RewardOrTerminal)
            return (null, advanced);
        if (resumeBoundary is
                PrototypeAutomaticPipelineResumeBoundaryKindV111.ControlledChoice or
                PrototypeAutomaticPipelineResumeBoundaryKindV111.SemanticChoice)
        {
            var suspendedChoice = new SemanticEndTurnV111(
                playerBoundary.Fork(), advanced.Fork(),
                nativeEncounterEntry, null, null, [])
            {
                PlayerActionSource = playerBoundary.Fork(),
                PlayerActionId = actions[0].ActionId,
                BoundaryTranscriptVersion =
                    SemanticEndTurnStochasticStateV111
                        .CurrentBoundaryTranscriptVersion
            };
            Validate(suspendedChoice);
            return (suspendedChoice, suspendedChoice.Run);
        }
        if (resumeBoundary !=
            PrototypeAutomaticPipelineResumeBoundaryKindV111.AdvanceTurn)
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE " +
                "site=combat.semantic_end_turn " +
                "operation=unexpected_player_action_boundary kind=" +
                resumeBoundary);
        var state = BeginAtAdvanceTurn(advanced, nativeEncounterEntry);
        var continuation = state with
        {
            PlayerActionSource = playerBoundary.Fork(),
            PlayerActionId = actions[0].ActionId
        };
        return (continuation, continuation.Run);
    }

    public static SemanticEndTurnV111 ResolveByCandidateLabel(
        SemanticEndTurnV111 source, string fingerprint,
        string candidateLabel)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn fingerprint.");
        var pending = source.EnemyActionEventPending ??
            throw new InvalidOperationException(
                "No semantic enemy-action outcome is pending.");
        if (!StringComparer.Ordinal.Equals(
                pending.BoundaryFingerprint, fingerprint))
            throw new InvalidDataException(
                "Enemy-action event belongs to another end-turn boundary.");
        return Resolve(source, fingerprint,
            pending.IndexOfCandidateLabel(candidateLabel));
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
            var candidateLabels = state.EnemyActionEventPending?
                .CandidateLabels;
            var (resumed, decision) =
                ControlledChanceContinuationExecutorV111
                .ResolveSelectedIndexValueWithDecision<SemanticEndTurnV111,
                    PrototypeAutomaticPipelineResumeResultV111>(
                    state, enemyActionRequest, selectedIndex,
                    (checkpoint, replayedIndex) =>
                        ControlledChanceGuardV111.ExecuteGuarded(
                            () => PrototypeGameEngine
                                .ResumeControlledEnemyAction(
                                    checkpoint.Run.Fork(), replayedIndex),
                            allowEngineOwnedPrototypeReward: true));
            var stochastic = state.Stochastic.AppendEnemyAction(
                decision, candidateLabels);
            state = ContinueAutomaticPipelineBoundaryChoice(
                state, resumed,
                stochastic, "combat.enemy_action");
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
        else if (state.HandDraw is { AtBoundary: false } generatedCardHandDraw &&
                 generatedCardHandDraw.GeneratedCardPending is
                    { } generatedCardHandDrawEvent)
        {
            if (selectedIndex < 0 || selectedIndex >=
                    generatedCardHandDrawEvent.CandidateCardIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var nextHand = SemanticCombatDrawEngineV111.ResolveGeneratedCard(
                generatedCardHandDraw, generatedCardHandDraw.Fingerprint,
                generatedCardHandDrawEvent.CandidateCardIds[selectedIndex]);
            state = ContinueHandDraw(state, nextHand);
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
        else if (state.HandDraw is { AtBoundary: false } potionHandDraw &&
                 potionHandDraw.PotionGenerationPending is { } handPotion)
        {
            if (selectedIndex < 0 ||
                selectedIndex >= handPotion.CandidatePotionIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var nextHand = SemanticCombatDrawEngineV111
                .ResolvePotionGeneration(
                    potionHandDraw, potionHandDraw.Fingerprint,
                    handPotion.CandidatePotionIds[selectedIndex]);
            state = ContinueHandDraw(state, nextHand);
        }
        else if (state.HandDraw is { AtBoundary: false } poolHandDraw &&
                 poolHandDraw.PoolShufflePending is { } handPoolShuffle)
        {
            if (selectedIndex < 0 ||
                selectedIndex > handPoolShuffle.NextSwapIndex)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var nextHand = SemanticCombatDrawEngineV111.ResolvePoolShuffle(
                poolHandDraw, poolHandDraw.Fingerprint, selectedIndex);
            state = ContinueHandDraw(state, nextHand);
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
        else if (state.HandDraw is { AtBoundary: false } costHandDraw &&
                 costHandDraw.CostPending is not null)
        {
            var nextHand = SemanticCombatDrawEngineV111.Resolve(
                costHandDraw, costHandDraw.Fingerprint, selectedIndex);
            state = ContinueHandDraw(state, nextHand);
        }
        else if (state.HandDraw is { AtBoundary: false } activeHandDraw &&
                 activeHandDraw.Pending is { Purpose: "draw" } &&
                 activeHandDraw.Run.World?.Combat is { } activeCombat &&
                 (activeCombat.PendingChoice is null ||
                  activeCombat.PendingChoice is { } nestedEffectDraw &&
                  PrototypeGameEngine
                      .IsAutomaticHandDrawHookSemanticDrawContinuation(
                          activeCombat, nestedEffectDraw)))
        {
            var nextHand = SemanticCombatDrawEngineV111.Resolve(
                activeHandDraw, activeHandDraw.Fingerprint,
                selectedIndex);
            state = ContinueHandDraw(state, nextHand);
        }
        else if (state.HandDraw is { AtBoundary: false } pileCardHandDraw &&
                 pileCardHandDraw.RandomPileCardPending is not null)
        {
            var nextHand = SemanticCombatDrawEngineV111.Resolve(
                pileCardHandDraw, pileCardHandDraw.Fingerprint,
                selectedIndex);
            state = ContinueHandDraw(state, nextHand);
        }
        else if (state.HandDraw is { AtBoundary: false } handDraw &&
                 handDraw.DrawInsertionPending is not null)
        {
            var nextHand = SemanticCombatDrawEngineV111.Resolve(
                handDraw, handDraw.Fingerprint, selectedIndex);
            state = ContinueHandDraw(state, nextHand);
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
                pending, selectedIndex, state.Run.Stochastic.PrototypeRng);
            var intentTranscript = next.Transcript;
            var existingIntentTranscript = state.Stochastic.IntentTranscript;
            var previousIntentCount = existingIntentTranscript.Length;
            if (intentTranscript.Length != previousIntentCount + 1 ||
                CanonicalJson.Sha256(intentTranscript
                    .Take(previousIntentCount).ToArray()) !=
                CanonicalJson.Sha256(existingIntentTranscript))
                throw new InvalidDataException(
                    "Turn-intent resolution did not append one ticket to its saved prefix.");
            state = state with
            {
                Intents = next,
                Stochastic = state.Stochastic.AppendIntentDecision(
                    intentTranscript[^1])
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
            state = ContinueHandDraw(state, next);
        }
        else
            throw new InvalidOperationException("No pending semantic end-turn choice.");
        Validate(state);
        return state;
    }

    public static SemanticEndTurnV111 ResolveIntent(
        SemanticEndTurnV111 source, string fingerprint, string eventId,
        string intentId)
    {
        Validate(source);
        if (!StringComparer.Ordinal.Equals(source.Fingerprint, fingerprint))
            throw new InvalidOperationException(
                "Stale semantic end-turn intent fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var pending = source.IntentChoicePending ??
            throw new InvalidOperationException(
                "No weighted semantic intent choice is pending.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Semantic intent event ID is stale or mismatched.");
        var selection = SemanticIntentChoiceV111.Select(pending, intentId);
        var transcriptIndex = source.Stochastic.IntentTranscript.Length;
        var advanced = Resolve(
            source, fingerprint, selection.SourceTicket);
        if (advanced.Stochastic.IntentTranscript.Length !=
            transcriptIndex + 1)
            throw new InvalidDataException(
                "Semantic intent resolution did not append one primitive ticket.");
        var next = advanced with
        {
            Stochastic = advanced.Stochastic.AppendSemanticIntent(
                new SemanticIntentTranscriptEntryV111(
                    transcriptIndex, selection.Decision))
        };
        Validate(next);
        return next;
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
        if (source.HandDraw is { AtBoundary: false } handDraw &&
            handDraw.GeneratedCardPending is { } handDrawEvent)
        {
            if (CanonicalJson.Sha256(handDrawEvent) !=
                CanonicalJson.Sha256(pending))
                throw new InvalidDataException(
                    "Automatic draw-generated card event changed during resume.");
            var nextHand = SemanticCombatDrawEngineV111.ResolveGeneratedCard(
                handDraw, handDraw.Fingerprint,
                decision.SelectedCardId);
            var next = ContinueHandDraw(source, nextHand);
            Validate(next);
            return next;
        }
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
        if (source.HandDraw is { AtBoundary: false } handDraw &&
            handDraw.PotionGenerationPending is { } handDrawEvent)
        {
            if (CanonicalJson.Sha256(handDrawEvent) !=
                CanonicalJson.Sha256(pending))
                throw new InvalidDataException(
                    "Automatic draw potion-generation event changed during resume.");
            var nextHand = SemanticCombatDrawEngineV111
                .ResolvePotionGeneration(
                    handDraw, handDraw.Fingerprint,
                    decision.SelectedPotionId);
            var next = ContinueHandDraw(source, nextHand);
            Validate(next);
            return next;
        }
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
        if (source.HandDraw is { AtBoundary: false } handDraw &&
            handDraw.PoolShufflePending is { } handDrawEvent)
        {
            if (CanonicalJson.Sha256(handDrawEvent) !=
                CanonicalJson.Sha256(pending))
                throw new InvalidDataException(
                    "Automatic draw pool-shuffle event changed during resume.");
            var nextHand = SemanticCombatDrawEngineV111.ResolvePoolShuffle(
                handDraw, handDraw.Fingerprint, swapIndex);
            var next = ContinueHandDraw(source, nextHand);
            Validate(next);
            return next;
        }
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
                "No automatic hand-draw or pipeline card-selection event is pending.");
        var decision = SemanticCardSelectionEngineV111.Select(
            pending, eventId, actionId);
        if (source.HandDraw is { AtBoundary: false } handDraw &&
            handDraw.CardSelectionPending is { } handDrawEvent)
        {
            if (CanonicalJson.Sha256(handDrawEvent) !=
                CanonicalJson.Sha256(pending))
                throw new InvalidDataException(
                    "Automatic draw card-selection event changed during resume.");
            var nextHand = SemanticCombatDrawEngineV111.ResolveCardSelection(
                handDraw, handDraw.Fingerprint, eventId, actionId);
            var nextHandDraw = ContinueHandDraw(source, nextHand);
            Validate(nextHandDraw);
            return nextHandDraw;
        }
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
        if (source.HandDraw is { AtBoundary: false } activeHandDraw &&
            activeHandDraw.TargetPending is not null)
        {
            var resumedHandDraw = SemanticCombatDrawEngineV111.ResolveTarget(
                activeHandDraw, activeHandDraw.Fingerprint, eventId,
                enemyInstanceId);
            var nextHandDraw = ContinueHandDraw(source, resumedHandDraw);
            Validate(nextHandDraw);
            return nextHandDraw;
        }
        var decision = SemanticTargetChoiceV111.Select(
            pending, eventId, enemyInstanceId);
        var resumed = SemanticChoiceResumeV111.ResumeTarget(
            source.Run, decision);

        var stochastic = source.Stochastic.AppendTarget(decision);
        var next = ContinueAutomaticPipelineBoundaryChoice(
            source, resumed, stochastic, pending.SiteId);
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
        PrototypeAutomaticPipelineResumeResultV111 resumed,
        SemanticEndTurnStochasticStateV111 stochastic,
        string siteId)
    {
        var next = source with
        {
            Stochastic = stochastic
        };
        if (TryPublishResumedPipelineBoundary(
                next, resumed, out var published))
            return published;

        if (next.HandDraw is { AtBoundary: true })
            return CompleteHandDrawAtPlayerBoundary(
                next, resumed);

        if (resumed.Boundary ==
            PrototypeAutomaticPipelineResumeBoundaryKindV111.DrawPlayerHand)
            return BeginHandDrawAtPipelineBoundary(
                next, resumed, "choice_resume");

        if (resumed.Boundary ==
            PrototypeAutomaticPipelineResumeBoundaryKindV111.AdvanceTurn)
        {
            var nextTurn = BeginAtAdvanceTurn(
                resumed.Run, source.NativeEncounterEntry);
            return nextTurn with
            {
                PlayerActionSource = source.PlayerActionSource?.Fork(),
                PlayerActionId = source.PlayerActionId,
                Stochastic = stochastic.CarryBoundaryChronologyTo(
                    nextTurn.Stochastic)
            };
        }

        throw new NotSupportedException(
            "UNSUPPORTED_CONTROLLED_CHANCE site=" + siteId +
            " operation=unexpected_pipeline_boundary");
    }

    private static SemanticEndTurnV111 BeginHandDrawAtPipelineBoundary(
        SemanticEndTurnV111 source,
        PrototypeAutomaticPipelineResumeResultV111 checkpoint,
        string operation)
    {
        if (checkpoint.Boundary !=
            PrototypeAutomaticPipelineResumeBoundaryKindV111.DrawPlayerHand)
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE " +
                "site=combat.semantic_end_turn operation=" + operation +
                " kind=" + checkpoint.Boundary);
        var draw = SemanticCombatDrawEngineV111.BeginAutomaticHandDraw(
            checkpoint.Run);
        return ContinueHandDraw(source, draw);
    }

    private static bool TryPublishResumedPipelineBoundary(
        SemanticEndTurnV111 source,
        PrototypeAutomaticPipelineResumeResultV111 resumed,
        out SemanticEndTurnV111 published)
    {
        if (resumed.Boundary ==
            PrototypeAutomaticPipelineResumeBoundaryKindV111.RewardOrTerminal)
        {
            published = CompleteAtBoundary(
                source, resumed.Run,
                discardSuspendedFrames: source.HandDraw is not
                    { AtBoundary: true });
            return true;
        }

        if (resumed.Boundary ==
                PrototypeAutomaticPipelineResumeBoundaryKindV111.ControlledChoice &&
            (source.HandDraw is null ||
             resumed.Run.World?.Combat?.PendingChoice?.ControlledTarget is not null))
        {
            published = source with { Run = resumed.Run };
            return true;
        }

        if (resumed.Boundary ==
            PrototypeAutomaticPipelineResumeBoundaryKindV111.SemanticChoice)
        {
            published = source with { Run = resumed.Run };
            return true;
        }

        published = source;
        return false;
    }

    internal static SemanticEndTurnV111 ResumeNestedSemanticChoice(
        SemanticEndTurnV111 source,
        SemanticEndTurnNestedChoiceV111 choice)
    {
        ValidateNestedAutomaticChoice(source, choice);
        var resumed = SemanticChoiceResumeV111.ResumeAutomaticPipelineChoice(
            source.Run, choice);
        return PublishNestedAutomaticChoice(
            source, resumed.Resume, choice, resumed.SiteId);
    }

    private static void ValidateNestedAutomaticChoice(
        SemanticEndTurnV111 source,
        SemanticEndTurnNestedChoiceV111 choice)
    {
        // This facade owns end-turn boundary freshness. The typed resume
        // owner validates the choice envelope, live source event, and
        // selected payload before mechanics replay.
        Validate(source);
        ArgumentNullException.ThrowIfNull(choice);
        if (source.Complete ||
            source.HandDraw is { AtBoundary: false } ||
            (!PrototypeGameEngine
                .HasPendingSupportedAutomaticPipelineSemanticChoice(
                    source.Run) &&
             source.KnowledgeDemonChoicePending is null))
            throw new InvalidOperationException(
                "No supported nested automatic semantic choice is pending.");
    }

    private static SemanticEndTurnV111 PublishNestedAutomaticChoice(
        SemanticEndTurnV111 source,
        PrototypeAutomaticPipelineResumeResultV111 resumed,
        SemanticEndTurnNestedChoiceV111 choice, string siteId)
    {
        var stochastic = source.Stochastic.AppendNestedChoice(choice);
        var next = ContinueAutomaticPipelineBoundaryChoice(
            source, resumed, stochastic, siteId);
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

    private static SemanticEndTurnV111 CompleteIntents(SemanticEndTurnV111 state)
    {
        var handoff = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.ResumeControlledTurnAfterIntents(
                state.Run.Fork(), stopBeforeSemanticHandDraw: true));
        var withoutIntents = state with { Intents = null };
        if (TryPublishResumedPipelineBoundary(
                withoutIntents, handoff, out var published))
            return published;
        return BeginHandDrawAtPipelineBoundary(
            withoutIntents, handoff, "post_intent_handoff");
    }

    private static SemanticEndTurnV111 ContinueHandDraw(
        SemanticEndTurnV111 source, SemanticCombatDrawV111 handDraw)
    {
        var next = source with
        {
            Run = handDraw.Run,
            HandDraw = handDraw
        };
        return handDraw.AtBoundary ? CompleteHandDraw(next) : next;
    }

    private static SemanticEndTurnV111 CompleteHandDraw(SemanticEndTurnV111 state)
    {
        if (state.HandDraw is not { AtBoundary: true } draw)
            throw new InvalidOperationException(
                "Cannot finish end turn with pending drawn-card choices.");
        var result = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.ResumeAutomaticPipelineContinuation(
                draw.Run.Fork()));
        if (TryPublishResumedPipelineBoundary(
                state, result, out var published))
            return published;
        return CompleteHandDrawAtPlayerBoundary(
            state, result);
    }

    private static SemanticEndTurnV111 CompleteHandDrawAtPlayerBoundary(
        SemanticEndTurnV111 state,
        PrototypeAutomaticPipelineResumeResultV111 result)
    {
        if (result.Boundary !=
            PrototypeAutomaticPipelineResumeBoundaryKindV111.PlayerDecision)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.semantic_end_turn " +
                "operation=not_at_next_player_boundary");
        if (result.Run.World?.Combat is not { } combat)
            throw new InvalidDataException(
                "Completed semantic end-turn lost its combat state.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Completed semantic end-turn has no Nature deck.",
            "Completed semantic end-turn has an invalid Nature deck boundary.");
        var actions = new PrototypeAiEnvironment().Observe(result.Run).LegalActions;
        if (!actions.Any())
            throw new InvalidOperationException(
                "The next player boundary has no available legal actions.");
        return CompleteAtBoundary(
            state, result.Run, discardSuspendedFrames: false);
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
        int boundaryIndex = 0, nestedChoiceIndex = 0;
        SemanticEndTurnV111 ResumeNestedChoice(
            SemanticEndTurnNestedChoiceV111 choice)
        {
            return ResumeNestedSemanticChoice(reproduced, choice);
        }
        void ReplayAvailableBoundaryChoices()
        {
            while (boundaryIndex < saved.Stochastic.BoundaryTranscript.Length)
            {
                var hasEnemyAction = reproduced.EnemyActionPending is not null;
                var hasTarget = reproduced.TargetPending is not null;
                var hasNestedChoice =
                    PrototypeGameEngine
                        .HasPendingSupportedAutomaticPipelineSemanticChoice(
                            reproduced.Run) ||
                    reproduced.KnowledgeDemonChoicePending is not null;
                if (!hasEnemyAction && !hasTarget && !hasNestedChoice)
                    return;

                var boundary = saved.Stochastic.BoundaryTranscript[boundaryIndex];
                boundary.Validate();
                if (boundary.Kind == "enemy_action")
                {
                    if (!hasEnemyAction ||
                        boundary.TranscriptIndex >=
                            saved.Stochastic.EnemyActionTranscript.Length)
                        throw new InvalidDataException(
                            "Semantic end-turn enemy-action transcript order is invalid.");
                    var choice = saved.Stochastic.EnemyActionTranscript[
                        boundary.TranscriptIndex];
                    choice.Validate();
                    var semanticLabels =
                        saved.Stochastic.EnemyActionCandidateLabelsVersion == 1
                            ? saved.Stochastic.EnemyActionCandidateLabels[
                                boundary.TranscriptIndex]
                            : null;
                    var replayedEvent = reproduced.EnemyActionEventPending;
                    if (choice.ProviderMode != "controlled-v111" ||
                        choice.ProbabilityStatus != "UNVERIFIED" ||
                        reproduced.EnemyActionPending is not { } request ||
                        !ControlledStochasticChanceReplayV111.RequestsMatch(
                            request, choice.Request) ||
                        semanticLabels is not null &&
                            (replayedEvent?.CandidateLabels is not { } liveLabels ||
                             !liveLabels.SequenceEqual(
                                 semanticLabels, StringComparer.Ordinal)))
                        throw new InvalidDataException(
                            "Semantic end-turn enemy-action transcript diverged.");
                    reproduced = semanticLabels is null
                        ? Resolve(reproduced, reproduced.Fingerprint,
                            choice.SelectedIndex)
                        : ResolveByCandidateLabel(
                            reproduced, reproduced.Fingerprint,
                            semanticLabels[choice.SelectedIndex]);
                }
                else if (boundary.Kind == "random_target")
                {
                    if (!hasTarget || boundary.TranscriptIndex >=
                        saved.Stochastic.TargetTranscript.Length)
                        throw new InvalidDataException(
                            "Semantic end-turn target transcript order is invalid.");
                    var choice = saved.Stochastic.TargetTranscript[
                        boundary.TranscriptIndex];
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
                else
                {
                    if (!hasNestedChoice || nestedChoiceIndex >=
                            saved.Stochastic.NestedChoiceTranscript.Length ||
                        saved.Stochastic.NestedChoiceTranscript[
                            nestedChoiceIndex].Kind !=
                            boundary.Kind)
                        throw new InvalidDataException(
                            "Semantic end-turn nested choice boundary diverged during replay.");
                    reproduced = ResumeNestedChoice(
                        saved.Stochastic.NestedChoiceTranscript[nestedChoiceIndex++]);
                }
                boundaryIndex++;
            }
        }
        if (saved.Stochastic.BoundaryTranscript.Length > 0)
            ReplayAvailableBoundaryChoices();
        else
        {
            // Snapshots written before automatic target suspension only had
            // a typed enemy-action transcript.
            for (var transcriptIndex = 0; transcriptIndex <
                 saved.Stochastic.EnemyActionTranscript.Length;
                 transcriptIndex++)
            {
                var choice = saved.Stochastic.EnemyActionTranscript[
                    transcriptIndex];
                var semanticLabels =
                    saved.Stochastic.EnemyActionCandidateLabelsVersion == 1
                        ? saved.Stochastic.EnemyActionCandidateLabels[
                            transcriptIndex]
                        : null;
                choice.Validate();
                var replayedEvent = reproduced.EnemyActionEventPending;
                if (choice.ProviderMode != "controlled-v111" ||
                    choice.ProbabilityStatus != "UNVERIFIED" ||
                    reproduced.EnemyActionPending is not { } request ||
                    !ControlledStochasticChanceReplayV111.RequestsMatch(
                        request, choice.Request) ||
                    semanticLabels is not null &&
                        (replayedEvent?.CandidateLabels is not { } liveLabels ||
                         !liveLabels.SequenceEqual(
                             semanticLabels, StringComparer.Ordinal)))
                    throw new InvalidDataException(
                        "Semantic end-turn enemy-action transcript diverged.");
                reproduced = semanticLabels is null
                    ? Resolve(reproduced, reproduced.Fingerprint,
                        choice.SelectedIndex)
                    : ResolveByCandidateLabel(
                        reproduced, reproduced.Fingerprint,
                        semanticLabels[choice.SelectedIndex]);
            }
        }
        var semanticIntentsByIndex = saved.Stochastic.SemanticIntentTranscript
            .ToDictionary(entry => entry.TranscriptIndex);
        for (var intentIndex = 0;
             intentIndex < saved.Stochastic.IntentTranscript.Length; intentIndex++)
        {
            var choice = saved.Stochastic.IntentTranscript[intentIndex];
            choice.Validate();
            if (reproduced.IntentPending is not { } request ||
                choice.ProviderMode != "controlled-v111" ||
                choice.ProbabilityStatus != "UNVERIFIED" ||
                !ControlledStochasticChanceReplayV111.RequestsMatch(
                    request, choice.Request))
                throw new InvalidDataException(
                    "Semantic end-turn intent transcript diverged.");
            if (semanticIntentsByIndex.TryGetValue(
                    intentIndex, out var semanticChoice))
            {
                var pending = reproduced.IntentChoicePending ??
                    throw new InvalidDataException(
                        "Semantic end-turn replay lost its weighted intent event.");
                if (CanonicalJson.Sha256(pending) !=
                    CanonicalJson.Sha256(semanticChoice.Decision.Event))
                    throw new InvalidDataException(
                        "Semantic end-turn intent identity support diverged.");
                reproduced = ResolveIntent(
                    reproduced, reproduced.Fingerprint, pending.EventId,
                    semanticChoice.Decision.IntentId);
            }
            else
                reproduced = Resolve(
                    reproduced, reproduced.Fingerprint,
                    choice.SelectedIndex);
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
        if (saved.Stochastic.BoundaryTranscript.Length > 0)
        {
            ReplayAvailableBoundaryChoices();
            if (boundaryIndex != saved.Stochastic.BoundaryTranscript.Length ||
                nestedChoiceIndex != saved.Stochastic.NestedChoiceTranscript.Length)
                throw new InvalidDataException(
                    "Semantic end-turn boundary transcript omitted a decision.");
        }
        var comparisonSnapshot = saved;
        if (saved.HandDraw is { } savedHand &&
            savedHand.BoundaryTranscript.Length == 0 &&
            reproduced.HandDraw is { } replayedHand &&
            replayedHand.BoundaryTranscript.Length > 0)
            comparisonSnapshot = saved with
            {
                HandDraw = savedHand with
                {
                    BoundaryTranscript = replayedHand.BoundaryTranscript,
                    BoundaryTranscriptVersion =
                        replayedHand.BoundaryTranscriptVersion
                }
            };
        if (saved.Stochastic.BoundaryTranscriptVersion == 0 &&
            reproduced.Stochastic.BoundaryTranscriptVersion ==
                SemanticEndTurnStochasticStateV111
                    .CurrentBoundaryTranscriptVersion)
            comparisonSnapshot = comparisonSnapshot with
            {
                BoundaryTranscript =
                    saved.Stochastic.BoundaryTranscript.Length == 0
                        ? reproduced.Stochastic.BoundaryTranscript
                        : saved.Stochastic.BoundaryTranscript,
                BoundaryTranscriptVersion =
                    SemanticEndTurnStochasticStateV111
                        .CurrentBoundaryTranscriptVersion
            };
        if (CanonicalJson.Sha256(reproduced) !=
            CanonicalJson.Sha256(comparisonSnapshot))
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
            PrototypeGameEngine
                .ClassifyAutomaticPipelineResumeBoundary(run) !=
                PrototypeAutomaticPipelineResumeBoundaryKindV111.AdvanceTurn ||
            !SemanticDrawPileAdapterV111
                .IsSupportedSemanticHandDrawSourceDeck(combat))
            throw new InvalidOperationException(
                "Semantic end-turn requires an exhausted draw pile or an " +
                "existing idle Nature deck at the genuine post-enemy-action " +
                "AdvanceTurn checkpoint.");
    }
}
