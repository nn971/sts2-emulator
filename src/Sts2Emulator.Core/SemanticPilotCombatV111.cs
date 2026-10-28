using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Sts2Emulator.Core;

/// <summary>One externally replayable action or provider decision.</summary>
public sealed record SemanticPilotContinuationV111(
    string Kind, string? ActionId = null, int? SelectedIndex = null,
    SemanticIntentDecisionV111? IntentDecision = null,
    SemanticDrawDecisionV111? DrawDecision = null,
    SemanticTargetDecisionV111? TargetDecision = null,
    SemanticGeneratedCardDecisionV111? GeneratedCardDecision = null,
    SemanticCardSelectionDecisionV111? CardSelectionDecision = null,
    SemanticCostDecisionV111? CostDecision = null,
    SemanticPoolShuffleDecisionV111? PoolShuffleDecision = null,
    SemanticDrawInsertionDecisionV111? DrawInsertionDecision = null,
    SemanticHandCostSelectionDecisionV111? HandCostSelectionDecision = null,
    SemanticCardShortlistDecisionV111? CardShortlistDecision = null,
    SemanticRandomPileCardDecisionV111? RandomPileCardDecision = null,
    SemanticDrawHookChoiceDecisionV111? DrawHookChoiceDecision = null,
    SemanticPotionGenerationDecisionV111? PotionGenerationDecision = null,
    SemanticCardTransformDecisionV111? CardTransformDecision = null,
    SemanticDeckRevealDecisionV111? DeckRevealDecision = null,
    SemanticKnowledgeDemonChoiceDecisionV111?
        KnowledgeDemonChoiceDecision = null);

/// <summary>
/// Independently replayable setup provenance. The default entry is a
/// source-shaped encounter fixture; SourceRouteEntry instead replays the
/// configured Act 1 start into the real native weak-combat entry hook.
/// </summary>
/// <summary>
    /// Combat-ended reward selection is outside the two-site Nature game:
    /// legacy prototype reward RNG, isolated under the guarded source stream.
    /// Its exact cursor use is replayable, while native source parity is
    /// explicitly UNVERIFIED.
    /// </summary>
public sealed record SemanticRewardAuditV111(
    string ProviderId, ulong BeforeCalls, ulong AfterCalls,
    string ProbabilityStatus = "UNVERIFIED")
{
    public const string PrototypeProviderId =
        "engine-owned-prototype-reward-unverified-v1";
    public ulong Calls => AfterCalls - BeforeCalls;
}

public sealed record SemanticNativePolicyAuditV111(
    string ProviderId, RandomBoundaryEventV111[] Decisions,
    string ProbabilityStatus = "UNVERIFIED")
{
    public const string NativeProviderId =
        "engine-owned-native-v111-comparator-unverified-v1";

    public RandomBoundaryTranscriptEntryV111[] ProviderTrace { get; init; } = [];

    public void Validate()
    {
        if (ProviderId != NativeProviderId ||
            ProbabilityStatus != "UNVERIFIED" || Decisions is null ||
            ProviderTrace is null)
            throw new InvalidDataException(
                "Native policy baseline has invalid provider provenance.");

        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            if (decision is null || decision.Sequence != index + 1 ||
                decision.ProviderMode != "native-v111" ||
                decision.Request is null || decision.Request.Ordinal < 0 ||
                string.IsNullOrWhiteSpace(decision.Request.StreamId))
                throw new InvalidDataException(
                    "Native policy index decision is invalid at " + index + ".");
            new RandomIndexDecisionV111(decision.Request,
                decision.SelectedIndex, decision.ProviderMode,
                decision.NativeCursorBefore, decision.NativeCursorAfter).Validate();
        }

        var traceIndices = new List<RandomBoundaryEventV111>();
        for (var index = 0; index < ProviderTrace.Length; index++)
        {
            var entry = ProviderTrace[index];
            if (entry is null || entry.Sequence != index + 1)
                throw new InvalidDataException(
                    "Native policy mixed trace sequence is invalid at " + index + ".");
            entry.Validate();
            ValidateNativeTraceProvenance(entry.ReadProviderMode(), index);
            if (entry.OperationKind == "index")
            {
                var value = entry.DeserializeEvent<RandomBoundaryEventV111>();
                traceIndices.Add(value with
                {
                    Sequence = traceIndices.Count + 1
                });
            }
        }

        // Older snapshots have only the compatibility index list. Newer
        // snapshots retain the full mixed trace and project its index entries.
        if (ProviderTrace.Length > 0 && traceIndices.Count != Decisions.Length)
            throw new InvalidDataException(
                "Native policy index projection differs from its mixed trace.");
        for (var index = 0; ProviderTrace.Length > 0 &&
             index < traceIndices.Count; index++)
        {
            if (CanonicalJson.Sha256(traceIndices[index]) !=
                CanonicalJson.Sha256(Decisions[index]))
                throw new InvalidDataException(
                    "Native policy index projection differs from its mixed trace at " +
                    index + ".");
        }
    }

    private static void ValidateNativeTraceProvenance(
        string providerMode, int index)
    {
        if (providerMode != "native-v111")
            throw new InvalidDataException(
                "Native policy trace has non-native provenance at " + index + ".");
    }
}

public sealed record SemanticSourceRouteAuditV111(
    string ProviderId, ActIdentity ActOneRegion, string[] ActionIds,
    string MapNodeId, string EncounterId, string NativeEncounterEntry,
    RandomBoundaryEventV111[] NativeEntryDecisions,
    string ProbabilityStatus = "UNVERIFIED")
{
    public const string SourceRouteProviderId =
        "engine-owned-v111-native-weak-route-entry-unverified-v1";
}

public sealed record SemanticSeededEntryV111(
    string Seed, int Act, string EncounterId, string NativeEncounterEntry,
    int TotalFloor, int Ascension, long FirstMonsterCombatId,
    int? MapColumn, int? MapRow,
    string? OtherChanceProviderId = null,
    string? FixtureDrawCostRelicId = null,
    bool ReplayOpeningDrawHooks = false,
    bool NativePolicyBaseline = false,
    bool SourceRouteEntry = false,
    ActIdentity RouteActOneRegion = ActIdentity.Overgrowth,
    string? RouteNeowChoiceId = null,
    string? RouteMapNodeId = null,
    string? FixtureRandomTargetCardId = null,
    string? FixtureGenerationPotionId = null,
    string? FixtureSelectionCardId = null,
    bool FixtureNatureDrawCosts = false,
    string? FixturePoolGenerationPotionId = null,
    string? FixtureGeneratedDrawInsertionCardId = null,
    bool FixtureRandomFreeAttackDrawInsertion = false,
    int? FixtureRandomizeHandCostsMaxExclusive = null,
    int? FixtureSetRandomHandCardCost = null,
    string? FixtureSelectionPotionId = null,
    string? FixturePotionGenerationCardId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    string? FixtureTopAutoplayPotionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FixtureDeckPresetId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ResearchCombatFixtureV111? FixtureSpec = null);

/// <summary>
/// First external playable semantic boundary. Initial opening order is
/// already committed by the separate controlled fixture: this provider
/// copies those exact hidden positions without issuing new Nature tickets.
/// Subsequent fresh recycle epochs are deferred one card at a time.
/// </summary>
public sealed record SemanticPilotCombatV111(
    ControlledPlayableCombatV111 OpeningSource,
    RunState Run, SemanticEndTurnV111? PendingEndTurn,
    SemanticPilotContinuationV111[] History,
    string NativeEncounterEntry,
    string Mode = "sts2-v111-semantic-playable-p0-v1")
{
    public const string SchemaId = "sts2-v111-semantic-playable-p0-v1";
    /// <summary>
    /// Atomic mechanics/provider envelope for the semantic playable session.
    /// Existing continuation properties remain flattened snapshot fields.
    /// </summary>
    [JsonIgnore]
    public SimulationStateV111<SemanticPilotMechanicsStateV111,
        SemanticPilotStochasticStateV111> Simulation { get; init; } = new(
            new SemanticPilotMechanicsStateV111(
                OpeningSource, Run, History, NativeEncounterEntry, Mode)
            {
                PendingEndTurn = PendingEndTurn is { } endTurn
                    ? SemanticPilotEndTurnMechanicsProjectionV111
                        .Capture(endTurn)
                    : null
            },
            new SemanticPilotStochasticStateV111(
                PendingEndTurn: PendingEndTurn?.Stochastic.Fork()));

    [JsonIgnore]
    public SemanticPilotMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    /// <summary>Private state for opted-in RNG and provider provenance.</summary>
    [JsonIgnore]
    public SemanticPilotStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public ControlledPlayableCombatV111 OpeningSource
    {
        get => Mechanics.OpeningSource;
        init => Mechanics = Mechanics with { OpeningSource = value };
    }

    public RunState Run
    {
        get => Mechanics.Run;
        init => Mechanics = Mechanics with { Run = value };
    }

    /// <summary>
    /// H8 uses an immutable append-only history trail instead of repeatedly
    /// copying this v111 array. The original full array is materialized only
    /// for an explicit legacy snapshot, fingerprint, or audit consumer.
    /// </summary>
    [JsonIgnore]
    public H8HistoryTrailV1? H8HistoryTrail { get; init; }

    public SemanticPilotContinuationV111[] History
    {
        get => H8HistoryTrail?.Materialize() ?? Mechanics.History;
        init => Mechanics = Mechanics with { History = value };
    }

    internal int HistoryOrdinal(string kind) =>
        H8HistoryTrail?.CountKind(kind) ??
        Mechanics.History.Count(transition => transition.Kind == kind);

    public string NativeEncounterEntry
    {
        get => Mechanics.NativeEncounterEntry;
        init => Mechanics = Mechanics with
        { NativeEncounterEntry = value };
    }

    public string Mode
    {
        get => Mechanics.Mode;
        init => Mechanics = Mechanics with { Mode = value };
    }

    /// <summary>
    /// Null for legacy precommitted fixtures. Seeded entries are verified by
    /// regenerating the entire source-shaped opening on snapshot restoration.
    /// This field is provider-private and never part of fair observation.
    /// </summary>
    public SemanticSeededEntryV111? SeededEntry
    {
        get => Stochastic.SeededEntry;
        init => Stochastic = Stochastic with { SeededEntry = value };
    }

    public SemanticEndTurnV111? PendingEndTurn
    {
        get
        {
            var mechanics = Mechanics.PendingEndTurn;
            var stochastic = Stochastic.PendingEndTurn;
            if (mechanics is null && stochastic is null)
                return null;
            if (mechanics is null || stochastic is null)
                throw new InvalidDataException(
                    "Semantic pilot end-turn owners disagree.");
            return mechanics.Project(Run, stochastic);
        }
        init
        {
            Mechanics = Mechanics with
            {
                PendingEndTurn = value is null
                    ? null
                    : SemanticPilotEndTurnMechanicsProjectionV111
                        .Capture(value)
            };
            Stochastic = Stochastic with
            {
                PendingEndTurn = value?.Stochastic.Fork()
            };
        }
    }
    public SemanticOtherChanceStateV111? OtherChance
    {
        get => Run.Stochastic.SemanticOtherChance;
        init => Run = Run with
        {
            Stochastic = RunStochasticStateV111.WithSemanticOtherChance(
                Run.Stochastic, value)
        };
    }
    public SemanticRewardAuditV111? RewardAudit
    {
        get => Run.Stochastic.SemanticRewardAudit;
        init => Run = Run with
        {
            Stochastic = RunStochasticStateV111.WithSemanticRewardAudit(
                Run.Stochastic, value)
        };
    }
    public SemanticNativePolicyAuditV111? NativePolicyAudit
    {
        get => Stochastic.NativePolicyAudit;
        init => Stochastic =
            SemanticPilotStochasticStateV111.WithNativePolicyAudit(
                Stochastic, value);
    }
    public SemanticSourceRouteAuditV111? SourceRouteAudit
    {
        get => Stochastic.SourceRouteAudit;
        init => Stochastic =
            SemanticPilotStochasticStateV111.WithSourceRouteAudit(
                Stochastic, value);
    }
    public ControlledTurnIntentsV111? PendingInitialIntents
    {
        get => Run.World?.Combat?.AutomaticPipelineContinuation is
            { OpeningStage: PrototypeOpeningCombatStage.InitialEnemyIntents }
            continuation ? continuation.ControlledTurnIntents : null;
        init => Run = PrototypeGameEngine
            .SetControlledTurnIntentContinuation(Run, value);
    }
    [JsonIgnore] public SemanticTargetEventV111? TargetPending =>
        PendingEndTurn is { } endTurn
            ? endTurn.TargetPending
            : SemanticPilotCombatEngineV111.DescribePendingTargetEvent(this);
    [JsonIgnore] public SemanticGeneratedCardEventV111? GeneratedCardPending =>
        SemanticPilotCombatEngineV111.DescribePendingGeneratedCardEvent(this);
    [JsonIgnore] public SemanticPotionGenerationEventV111?
        PotionGenerationPending =>
            SemanticPilotCombatEngineV111
                .DescribePendingPotionGenerationEvent(this);
    [JsonIgnore] public SemanticCardTransformEventV111?
        CardTransformPending =>
            SemanticPilotCombatEngineV111
                .DescribePendingCardTransformEvent(this);
    [JsonIgnore] public SemanticCardSelectionEventV111? CardSelectionPending =>
        SemanticPilotCombatEngineV111.DescribePendingCardSelectionEvent(this);
    [JsonIgnore] public SemanticCostEventV111? CostPending =>
        SemanticPilotCombatEngineV111.DescribePendingCostEvent(this);
    [JsonIgnore] public SemanticHandCostSelectionEventV111?
        HandCostSelectionPending =>
            SemanticPilotCombatEngineV111
                .DescribePendingHandCostSelectionEvent(this);
    [JsonIgnore] public SemanticCardShortlistEventV111?
        CardShortlistPending =>
            SemanticPilotCombatEngineV111
                .DescribePendingCardShortlistEvent(this);
    [JsonIgnore] public SemanticPoolShuffleEventV111? PoolShufflePending =>
        SemanticPilotCombatEngineV111.DescribePendingPoolShuffleEvent(this);
    [JsonIgnore] public SemanticDrawInsertionEventV111? DrawInsertionPending =>
        PendingEndTurn is { } endTurn
            ? endTurn.DrawInsertionPending
            : SemanticPilotCombatEngineV111
                .DescribePendingDrawInsertionEvent(this);
    [JsonIgnore] public SemanticDrawHookChoiceEventV111?
        DrawHookChoicePending => PendingEndTurn?.DrawHookChoicePending;
    [JsonIgnore] public SemanticKnowledgeDemonChoiceEventV111?
        KnowledgeDemonChoicePending =>
            PendingEndTurn?.KnowledgeDemonChoicePending;
    [JsonIgnore] public string Fingerprint => CanonicalJson.Sha256(this);
    [JsonIgnore] public bool AtPlayerBoundary =>
        PendingInitialIntents is null &&
        PendingEndTurn is null &&
        DeckRevealPending is null &&
        PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary(Run);
    [JsonIgnore] public bool CombatTerminal =>
        PrototypeGameEngine.IsAtCombatRewardOrTerminalBoundary(Run);
    [JsonIgnore] public bool AtPlayerChoiceBoundary =>
        !CombatTerminal &&
        ((PendingEndTurn is null && PendingInitialIntents is null &&
          CardSelectionPending is not null) ||
         (PendingEndTurn is not null && PendingInitialIntents is null &&
          (DrawHookChoicePending is not null ||
           KnowledgeDemonChoicePending is not null ||
           PendingEndTurn?.NestedCardSelectionPending is not null)));
    [JsonIgnore] public bool UsesNativePolicyBaseline =>
        SeededEntry?.NativePolicyBaseline ?? false;
    [JsonIgnore] public string? SourceOpeningFingerprint =>
        SeededEntry is { } entry
            ? CanonicalJson.Sha256(new
            {
                Entry = entry with { NativePolicyBaseline = false },
                OpeningSource,
                SourceRouteAudit
            })
            : null;
    [JsonIgnore] public string? CombatOutcome =>
        Run.Phase switch
        {
            RunPhase.Reward => "victory",
            RunPhase.Terminal => "defeat",
            _ => null
        };
    [JsonIgnore] public RandomIndexRequestV111? IntentPending =>
        PendingInitialIntents?.Pending ?? PendingEndTurn?.IntentPending;
    [JsonIgnore] public RandomIndexRequestV111? EnemyActionPending =>
        PendingEndTurn?.EnemyActionPending;
    [JsonIgnore] public SemanticEndTurnIndexEventV111?
        EnemyActionEventPending => PendingEndTurn is { } endTurn &&
            endTurn.EnemyActionPending is { } request
                ? SemanticEndTurnIndexEventV111.Create(Fingerprint, request)
                : null;
    [JsonIgnore] public SemanticDeckEventV111? DrawPending
    {
        get
        {
            if (PendingEndTurn?.DrawPending is { } endTurnDraw)
                return endTurnDraw;
            var combat = Run.World?.Combat;
            return combat?.PendingChoice is
                { ChoiceId: "controlled_semantic_draw",
                    ControlledSemanticDraw: not null } &&
                combat.SemanticDeck is
                    { Pending: { Purpose: "draw" } } deck
                    ? SemanticDeckEngineV111.Inspect(deck) : null;
        }
    }
    [JsonIgnore] public SemanticDeckEventV111? DeckRevealPending
    {
        get
        {
            var deck = Run.World?.Combat?.SemanticDeck;
            return deck?.Pending?.Purpose is "peek" or "scry"
                ? SemanticDeckEngineV111.Inspect(deck)
                : null;
        }
    }
    [JsonIgnore] public SemanticRandomPileCardEventV111?
        RandomPileCardPending => PendingEndTurn?.RandomPileCardPending;

    public void Validate()
    {
        // Semantic validation only observes an existing immutable-by-contract
        // snapshot. Keep repeated RunState SHA-256 computations local to this
        // validation call; never cache across a transition or JSONL request.
        using var readOnlyHashes =
            CanonicalJson.BeginReadOnlyRunStateHashReuse();
        this.Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.SemanticPilot);
        this.Run.Validate();
        if (this.Mode != SemanticPilotCombatV111.SchemaId ||
            this.History is null ||
            this.NativeEncounterEntry != this.OpeningSource.Origin
                .Formation.NativeEncounterEntry ||
            this.Run.Phase is not (
                RunPhase.Combat or RunPhase.Reward or RunPhase.Terminal))
            throw new InvalidDataException("Invalid semantic playable session.");
        // A pending continuation projection is deterministic and read-only.
        // Capture it once rather than reconstructing it for every guard.
        var pendingEndTurn = this.PendingEndTurn;
        if (pendingEndTurn is { } endTurn)
        {
            endTurn.Validate();
            if (endTurn.Complete ||
                (endTurn.EnemyActionPending is not null &&
                 (this.PendingInitialIntents is not null ||
                  endTurn.IntentPending is not null ||
                  endTurn.DrawPending is not null ||
                  endTurn.HandDraw is not null ||
                  this.UsesNativePolicyBaseline)) ||
                (endTurn.CostPending is not null &&
                 (this.CostPending is null ||
                  this.CostPending.TargetCardInstanceId is not null ||
                  CanonicalJson.Sha256(endTurn.CostPending) !=
                      CanonicalJson.Sha256(this.CostPending.Request))))
                throw new InvalidDataException(
                    "Semantic playable and end-turn continuation diverged.");
        }
        if (this.PendingInitialIntents is { } initialIntents)
        {
            if (initialIntents.Complete || pendingEndTurn is not null ||
                this.SeededEntry?.SourceRouteEntry != true ||
                this.Run.World?.Combat is not { } entryCombat ||
                CanonicalJson.Sha256(entryCombat with
                {
                    AutomaticPipelineContinuation = null
                }) !=
                    CanonicalJson.Sha256(initialIntents.BaselineCombat with
                    {
                        AutomaticPipelineContinuation = null
                    }) ||
                entryCombat.NativeRng is not null ||
                entryCombat.SemanticDeck is not null ||
                this.Run.NativeRunRng is not null)
                throw new InvalidDataException(
                    "Initial intent continuation diverged from the source-route boundary.");
        }
        if (this.Run.World?.Combat?.PendingChoice is
                { ChoiceId: "controlled_semantic_draw",
                  ControlledSemanticDraw: not null })
        {
            SemanticDrawChoiceV111.ValidateBoundary(this.Run);
            if ((pendingEndTurn is not null &&
                 !HasPendingEndTurnNestedSemanticChoiceAt(pendingEndTurn)) ||
                this.PendingInitialIntents is not null ||
                this.IntentPending is not null ||
                this.DrawPending is null ||
                this.RandomPileCardPending is not null ||
                this.CostPending is not null ||
                this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.PotionGenerationPending is not null ||
                this.CardTransformPending is not null ||
                this.CardSelectionPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                this.CardShortlistPending is not null ||
                this.HandCostSelectionPending is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Card-effect draw conflicts with another semantic boundary.");
        }
        if (this.Run.World?.Combat?.PendingChoice is
                { ChoiceId: "controlled_draw_cost",
                  ControlledDrawCost: not null,
                  ControlledSemanticDraw: not null })
        {
            SemanticCostChoiceV111.ValidateDrawBoundary(this.Run);
            if ((pendingEndTurn is not null &&
                 !HasPendingEndTurnNestedSemanticChoiceAt(pendingEndTurn)) ||
                this.PendingInitialIntents is not null ||
                this.IntentPending is not null ||
                this.DrawPending is not null || this.CostPending is null ||
                this.RandomPileCardPending is not null ||
                this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.CardSelectionPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                this.CardShortlistPending is not null ||
                this.HandCostSelectionPending is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Card-effect draw cost conflicts with another semantic boundary.");
        }
        if (this.RandomPileCardPending is { } randomPileCardPending)
        {
            randomPileCardPending.Validate();
            if (pendingEndTurn is null ||
                this.PendingInitialIntents is not null ||
                this.IntentPending is not null || this.DrawPending is not null ||
                this.CostPending is not null || this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.CardSelectionPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                this.CardShortlistPending is not null ||
                this.HandCostSelectionPending is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal ||
                this.Run.World?.Combat?.PendingChoice?
                    .ControlledRandomPileCard is null)
                throw new InvalidDataException(
                    "Random pile-card continuation conflicts with another boundary.");
        }
        if (this.DrawHookChoicePending is { } drawHookChoicePending)
        {
            drawHookChoicePending.Validate();
            if (pendingEndTurn is null ||
                this.PendingInitialIntents is not null ||
                this.IntentPending is not null || this.DrawPending is not null ||
                this.RandomPileCardPending is not null ||
                this.CostPending is not null || this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.CardSelectionPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                this.CardShortlistPending is not null ||
                this.HandCostSelectionPending is not null ||
                !this.AtPlayerChoiceBoundary || this.UsesNativePolicyBaseline ||
                this.CombatTerminal)
                throw new InvalidDataException(
                    "CardDrawn hook action conflicts with another semantic boundary.");
        }
        if (this.KnowledgeDemonChoicePending is { } knowledgeDemonChoicePending)
        {
            knowledgeDemonChoicePending.Validate();
            if (pendingEndTurn is null ||
                this.PendingInitialIntents is not null ||
                this.IntentPending is not null || this.DrawPending is not null ||
                this.RandomPileCardPending is not null ||
                this.CostPending is not null || this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.CardSelectionPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                this.CardShortlistPending is not null ||
                this.HandCostSelectionPending is not null ||
                !this.AtPlayerChoiceBoundary || this.UsesNativePolicyBaseline ||
                this.CombatTerminal)
                throw new InvalidDataException(
                    "Knowledge Demon action conflicts with another semantic boundary.");
        }
        if (this.TargetPending is { } targetPending)
        {
            targetPending.Validate();
            if ((pendingEndTurn is not null &&
                 pendingEndTurn.TargetPending is null) ||
                this.PendingInitialIntents is not null ||
                this.GeneratedCardPending is not null ||
                this.PotionGenerationPending is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Random-target continuation conflicts with another boundary.");
        }
        if (this.GeneratedCardPending is { } generatedCardPending)
        {
            generatedCardPending.Validate();
            if (this.TargetPending is not null ||
                this.PotionGenerationPending is not null ||
                (pendingEndTurn is not null &&
                 pendingEndTurn.GeneratedCardPending is null) ||
                this.PendingInitialIntents is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Generated-card continuation conflicts with another boundary.");
        }
        if (this.PotionGenerationPending is { } potionGenerationPending)
        {
            potionGenerationPending.Validate();
            SemanticPotionGenerationChoiceV111.ValidateBoundary(this.Run);
            if (this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.IntentPending is not null ||
                this.DrawPending is not null ||
                this.RandomPileCardPending is not null ||
                this.CardSelectionPending is not null ||
                this.CostPending is not null ||
                this.HandCostSelectionPending is not null ||
                this.CardShortlistPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                (pendingEndTurn is not null &&
                 pendingEndTurn.PotionGenerationPending is null) ||
                this.PendingInitialIntents is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Potion-generation continuation conflicts with another boundary.");
        }
        else if (this.Run.World?.Combat?.PendingChoice?
                     .ControlledPotionGeneration is not null)
            throw new InvalidDataException(
                "Controlled potion generation has no semantic event.");
        if (this.CardTransformPending is { } cardTransformPending)
        {
            cardTransformPending.Validate();
            SemanticCardTransformChoiceV111.ValidateBoundary(this.Run,
                this.Run.World!.Combat!.PendingChoice!
                    .ControlledCardTransform!);
            if (this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.PotionGenerationPending is not null ||
                this.IntentPending is not null ||
                this.DrawPending is not null ||
                this.RandomPileCardPending is not null ||
                this.CardSelectionPending is not null ||
                this.CostPending is not null ||
                this.HandCostSelectionPending is not null ||
                this.CardShortlistPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                (pendingEndTurn is not null &&
                 pendingEndTurn.CardTransformPending is null) ||
                this.PendingInitialIntents is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Card-transform continuation conflicts with another semantic boundary.");
        }
        else if (this.Run.World?.Combat?.PendingChoice?
                     .ControlledCardTransform is not null)
            throw new InvalidDataException(
                "Controlled card transform has no semantic event.");
        if (this.CardSelectionPending is { } cardSelectionPending)
        {
            cardSelectionPending.Validate();
            SemanticCardSelectionEngineV111.ValidatePendingBoundary(this.Run);
            if ((pendingEndTurn is not null &&
                 pendingEndTurn.NestedCardSelectionPending is null) ||
                this.PendingInitialIntents is not null ||
                this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Card-selection continuation conflicts with another boundary.");
        }
        if (this.DeckRevealPending is { } deckRevealPending)
        {
            var deck = this.Run.World?.Combat?.SemanticDeck ??
                throw new InvalidDataException(
                    "Semantic deck reveal lost its provider this.");
            if (CanonicalJson.Sha256(
                    SemanticDeckEngineV111.Inspect(deck)) !=
                CanonicalJson.Sha256(deckRevealPending) ||
                this.DrawPending is not null ||
                this.IntentPending is not null ||
                this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.PotionGenerationPending is not null ||
                this.CardTransformPending is not null ||
                this.CardSelectionPending is not null ||
                this.CostPending is not null ||
                this.HandCostSelectionPending is not null ||
                this.CardShortlistPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                this.RandomPileCardPending is not null ||
                this.DrawHookChoicePending is not null ||
                pendingEndTurn is not null ||
                this.PendingInitialIntents is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Semantic deck reveal conflicts with another chance boundary.");
        }
        if (this.PoolShufflePending is { } poolShufflePending)
        {
            poolShufflePending.Validate();
            SemanticPoolShuffleChoiceV111.ValidateBoundary(this.Run);
            var combat = this.Run.World?.Combat;
            var mayhemRecycle = combat?.PendingChoice is { } pendingChoice &&
                PrototypeGameEngine.IsMayhemLegacyRecycleContinuation(
                    combat, pendingChoice);
            var automaticPipelinePoolShuffle =
                combat?.PendingChoice is { } automaticPoolShuffleChoice &&
                PrototypeGameEngine
                    .IsAutomaticPipelineSemanticPoolShuffleContinuation(
                        combat, automaticPoolShuffleChoice);
            var automaticEndTurnShuffle =
                pendingEndTurn?.PoolShufflePending is not null;
            bool handReturnShuffle = poolShufflePending.SourceOperationKind ==
                nameof(PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle);
            bool recycleShuffle = poolShufflePending.SourceOperationKind ==
                nameof(PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw);
            bool randomRareToHandShuffle =
                poolShufflePending.SourceOperationKind ==
                    nameof(PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand);
            bool randomShortlistShuffle = poolShufflePending.SourceOperationKind ==
                nameof(PrototypeCombatEffectKind.ChooseCards);
            var fixtureMatches = automaticPipelinePoolShuffle ||
                handReturnShuffle || recycleShuffle ||
                (randomRareToHandShuffle
                    ? this.SeededEntry?.FixtureSelectionCardId ==
                        "proto.colorless.anointed"
                    : randomShortlistShuffle
                        ? this.SeededEntry?.FixtureSelectionCardId is not null
                        : this.SeededEntry?.FixturePoolGenerationPotionId ==
                            "proto.potion.colorless");
            if ((!fixtureMatches && !automaticEndTurnShuffle) ||
                (pendingEndTurn is not null &&
                 !automaticEndTurnShuffle && !mayhemRecycle) ||
                this.PendingInitialIntents is not null ||
                this.IntentPending is not null || this.DrawPending is not null ||
                this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.CardSelectionPending is not null ||
                this.CostPending is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Full-pool shuffle conflicts with another semantic boundary.");
        }
        if (this.DrawInsertionPending is { } drawInsertionPending)
        {
            drawInsertionPending.Validate();
            SemanticDrawInsertionChoiceV111.ValidateBoundary(this.Run);
            if ((pendingEndTurn is not null &&
                 pendingEndTurn.DrawInsertionPending is null) ||
                this.PendingInitialIntents is not null ||
                this.IntentPending is not null || this.DrawPending is not null ||
                this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.CardSelectionPending is not null ||
                this.PoolShufflePending is not null ||
                this.CostPending is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Draw insertion conflicts with another semantic boundary.");
        }
        if (this.CardShortlistPending is { } cardShortlistPending)
        {
            cardShortlistPending.Validate();
            SemanticCardShortlistChoiceV111.ValidateBoundary(this.Run);
            if ((pendingEndTurn is not null &&
                 pendingEndTurn.CardShortlistPending is null) ||
                this.PendingInitialIntents is not null ||
                this.IntentPending is not null || this.DrawPending is not null ||
                this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.CardSelectionPending is not null ||
                this.CostPending is not null ||
                this.HandCostSelectionPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Card shortlist conflicts with another semantic boundary.");
        }
        if (this.HandCostSelectionPending is { } handCostSelectionPending)
        {
            handCostSelectionPending.Validate();
            SemanticRandomHandCostChoiceV111.ValidateBoundary(this.Run);
            if ((pendingEndTurn is not null &&
                 pendingEndTurn.NestedHandCostSelectionPending is null) ||
                this.PendingInitialIntents is not null ||
                this.IntentPending is not null || this.DrawPending is not null ||
                this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.CardSelectionPending is not null ||
                this.CostPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Hand-cost selection conflicts with another semantic boundary.");
        }
        if (this.CostPending is { } costPending)
        {
            costPending.Validate();
            var handCost = costPending.TargetCardInstanceId is not null;
            var effectDrawCost = this.Run.World?.Combat?.PendingChoice is
                { ChoiceId: "controlled_draw_cost",
                  ControlledDrawCost: not null,
                  ControlledSemanticDraw: not null };
            var automaticHandCost = this.Run.World?.Combat?.PendingChoice?
                .ControlledRandomCost is not null;
            if (effectDrawCost)
                SemanticCostChoiceV111.ValidateDrawBoundary(this.Run);
            else if (automaticHandCost)
                SemanticRandomHandCostChoiceV111.ValidateBoundary(this.Run);
            if (this.IntentPending is not null || this.DrawPending is not null ||
                this.CardSelectionPending is not null ||
                this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.PoolShufflePending is not null ||
                this.DrawInsertionPending is not null ||
                this.CardShortlistPending is not null ||
                this.HandCostSelectionPending is not null ||
                (effectDrawCost
                    ? this.Run.World?.Combat?.PendingChoice?
                        .ControlledDrawCost is null
                    : handCost
                        ? this.Run.World?.Combat?.PendingChoice?
                            .ControlledRandomCost is null
                        : pendingEndTurn?.CostPending is null) ||
                (pendingEndTurn is not null && automaticHandCost &&
                 pendingEndTurn.NestedCostPending is null))
                throw new InvalidDataException(
                    "Semantic cost continuation conflicts with another boundary.");
        }
        if (this.Run.World?.Combat?.PendingChoice?
                .ControlledRandomCost is not null &&
            this.CostPending?.TargetCardInstanceId is null &&
            this.HandCostSelectionPending is null)
            throw new InvalidDataException(
                "Source random hand-cost continuation has no semantic event.");
        this.Stochastic.Validate();
        if (this.SeededEntry is { } entry)
        {
            var routeCostProviderExpected = entry.SourceRouteEntry &&
                !entry.NativePolicyBaseline;
            var otherChanceExpected = entry.OtherChanceProviderId is not null ||
                routeCostProviderExpected;
            if (entry.Seed != this.OpeningSource.Seed ||
                entry.Act != this.OpeningSource.Origin.Combat.Act ||
                entry.NativeEncounterEntry != this.NativeEncounterEntry ||
                entry.TotalFloor != this.OpeningSource.Origin.Formation.TotalFloor ||
                otherChanceExpected != (this.OtherChance is not null) ||
                (entry.OtherChanceProviderId is not null &&
                 entry.OtherChanceProviderId != this.OtherChance?.ProviderId) ||
                (routeCostProviderExpected && this.OtherChance?.ProviderId !=
                 SemanticOtherChanceEngineV111.ProviderId))
                throw new InvalidDataException(
                    "Seeded Nature entry provenance disagrees with its opening.");
        }
        if (this.SeededEntry is { SourceRouteEntry: true } routeEntry)
        {
            var route = this.SourceRouteAudit ?? throw new InvalidDataException(
                "Source-route semantic session is missing its route audit.");
            var world = this.Run.World ?? throw new InvalidDataException(
                "Source-route semantic session is missing its run world.");
            if (route.ProviderId != SemanticSourceRouteAuditV111.SourceRouteProviderId ||
                route.ProbabilityStatus != "UNVERIFIED" ||
                route.ActOneRegion != routeEntry.RouteActOneRegion ||
                route.ActionIds is null || route.ActionIds.Length == 0 ||
                route.ActionIds.Any(string.IsNullOrWhiteSpace) ||
                route.MapNodeId != routeEntry.RouteMapNodeId ||
                route.EncounterId != routeEntry.EncounterId ||
                route.NativeEncounterEntry != routeEntry.NativeEncounterEntry ||
                world.Map.CurrentNodeId != route.MapNodeId ||
                (world.EncounterHistory ?? []).LastOrDefault() != route.EncounterId ||
                route.NativeEntryDecisions is null)
                throw new InvalidDataException(
                    "Source-route audit disagrees with the seeded run entry.");
        }
        else if (this.SourceRouteAudit is not null)
            throw new InvalidDataException(
                "Fixture semantic session cannot retain a source-route audit.");
        if (this.UsesNativePolicyBaseline)
        {
            if (this.OtherChance is not null || pendingEndTurn is not null ||
                this.PendingInitialIntents is not null)
                throw new InvalidDataException(
                    "Native policy baseline cannot retain Nature-only continuations.");
            var nativeAudit = this.NativePolicyAudit ??
                throw new InvalidDataException(
                    "Native policy baseline is missing provider provenance.");
            if (this.AtPlayerBoundary)
                SemanticPilotCombatEngineV111.ValidateNativePolicyBoundary(this.Run);
            else
                NativeRunRngV111.Validate(this.Run);
        }
        else if (this.AtPlayerBoundary)
            SemanticPlayerActionEngineV111.ValidateBoundary(this.Run);
        else if (this.NativePolicyAudit is not null)
            throw new InvalidDataException(
                "Nature-controlled sessions cannot retain native policy decisions.");
        if (this.CombatTerminal && pendingEndTurn is not null)
            throw new InvalidDataException(
                "Terminal combat retained an unresolved chance.");
    }

    private bool HasPendingEndTurnNestedSemanticChoice =>
        HasPendingEndTurnNestedSemanticChoiceAt(this.PendingEndTurn);

    private bool HasPendingEndTurnNestedSemanticChoiceAt(
        SemanticEndTurnV111? pendingEndTurn) =>
        pendingEndTurn is { Complete: false } endTurn &&
        endTurn.HandDraw is not { AtBoundary: false } &&
        PrototypeGameEngine
            .HasPendingSupportedAutomaticPipelineSemanticChoice(this.Run) &&
        (endTurn.DrawPending is not null ||
         endTurn.NestedCostPending is not null ||
         endTurn.NestedDrawInsertionPending is not null ||
         endTurn.GeneratedCardPending is not null ||
         endTurn.PotionGenerationPending is not null ||
         endTurn.CardTransformPending is not null ||
         endTurn.CardShortlistPending is not null ||
         endTurn.PoolShufflePending is not null ||
         endTurn.NestedHandCostSelectionPending is not null ||
         endTurn.NestedCardSelectionPending is not null ||
         endTurn.NestedDrawHookChoicePending is not null);

    public SemanticPilotCombatV111 Fork() => this with
    {
        OpeningSource = OpeningSource.Fork(),
        Run = Run.Fork(),
        History = History.Select(transition => transition switch
        {
            { IntentDecision: { } decision } => transition with
            {
                IntentDecision = decision.Fork()
            },
            { DrawDecision: { } decision } => transition with
            {
                DrawDecision = decision.Fork()
            },
            { DeckRevealDecision: { } decision } => transition with
            {
                DeckRevealDecision = decision.Fork()
            },
            { RandomPileCardDecision: { } decision } => transition with
            {
                RandomPileCardDecision = decision.Fork()
            },
            { DrawHookChoiceDecision: { } decision } => transition with
            {
                DrawHookChoiceDecision = decision.Fork()
            },
            { KnowledgeDemonChoiceDecision: { } decision } => transition with
            {
                KnowledgeDemonChoiceDecision = decision.Fork()
            },
            { TargetDecision: { } decision } => transition with
            {
                TargetDecision = decision.Fork()
            },
            { GeneratedCardDecision: { } decision } => transition with
            {
                GeneratedCardDecision = decision.Fork()
            },
            { PotionGenerationDecision: { } decision } => transition with
            {
                PotionGenerationDecision = decision.Fork()
            },
            { CardTransformDecision: { } decision } => transition with
            {
                CardTransformDecision = decision.Fork()
            },
            { CardSelectionDecision: { } decision } => transition with
            {
                CardSelectionDecision = decision.Fork()
            },
            { CostDecision: { } decision } => transition with
            {
                CostDecision = decision.Fork()
            },
            { PoolShuffleDecision: { } decision } => transition with
            {
                PoolShuffleDecision = decision.Fork()
            },
            { DrawInsertionDecision: { } decision } => transition with
            {
                DrawInsertionDecision = decision.Fork()
            },
            { HandCostSelectionDecision: { } decision } => transition with
            {
                HandCostSelectionDecision = decision.Fork()
            },
            { CardShortlistDecision: { } decision } => transition with
            {
                CardShortlistDecision = decision.Fork()
            },
            _ => transition
        }).ToArray(),
        // Explicit compatibility fork publishes independent fully copied
        // semantic history. H8's live owner may re-enable the persistent
        // trail from this audited fork at its new runtime boundary.
        H8HistoryTrail = null,
        PendingEndTurn = PendingEndTurn?.Fork(),
        Stochastic = Stochastic.Fork()
    };
}

public static class SemanticPilotCombatEngineV111
{
    public static SemanticPilotCombatV111 Begin(
        ControlledPlayableCombatV111 completedOpening)
    {
        ArgumentNullException.ThrowIfNull(completedOpening);
        // The source fixture is independently replayable. Never accept its
        // partially committed legacy shuffle or a non-player checkpoint.
        var source = ControlledPlayableCombatEngineV111.Restore(
            ControlledPlayableCombatEngineV111.Save(completedOpening));
        if (!source.AtPlayerBoundary || source.Run.Phase != RunPhase.Combat ||
            source.Run.World?.Combat is not { } combat ||
            combat.SemanticDeck is not null ||
            combat.AutomaticPipelineContinuation is not null ||
            combat.PendingChoice is not null)
            throw new InvalidOperationException(
                "Semantic playable entry requires a completely committed opening fixture.");
        var run = source.Run.Fork();
        var semanticCombat =
            SemanticDrawPileAdapterV111.InitializeFromCommittedOpening(
                combat.Fork());
        run = run with
        {
            World = run.World! with
            {
                Combat = semanticCombat
            }
        };
        var state = new SemanticPilotCombatV111(
            source.Fork(), run, null, [],
            source.Origin.Formation.NativeEncounterEntry);
        state.Validate();
        return state;
    }

    /// <summary>
    /// Build a playable two-site session without asking an external worker to
    /// choose the initial HP, intent or opening shuffle. By default this is a
    /// source-shaped encounter fixture; the opt-in source-route mode replays
    /// the configured Act 1 route and enters combat through the real engine.
    /// Opening order is imported as known and is never randomized again by a
    /// deferred draw ticket. Route probabilities and noncombat RNG remain
    /// explicitly unverified.
    /// </summary>
    public static SemanticPilotCombatV111 BeginSeeded(
        SemanticSeededEntryV111 entry)
    {
        using var entryScope = H8HotPathProfilerV1.Begin("h8.begin.total");
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Seed);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.EncounterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.NativeEncounterEntry);
        // Reject incompatible research configurations before any HP, intent,
        // draw, or RNG tickets are requested.
        entry.FixtureSpec?.ValidateFor(entry);
        if (entry.Act is not (1 or 2 or 3) || entry.TotalFloor < 1 ||
            entry.FirstMonsterCombatId < 1)
            throw new ArgumentOutOfRangeException(nameof(entry),
                "Seeded Nature combat requires a positive floor and monster IDs.");
        if (entry.FixtureDeckPresetId is not null &&
            (entry.Act != 1 || entry.NativePolicyBaseline || entry.SourceRouteEntry))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.conditioned_deck " +
                "operation=only_isolated_act1_without_native_comparator");
        if (entry.FixtureNatureDrawCosts &&
            (entry.FixtureDrawCostRelicId != "proto.relic.snecko_eye" ||
             entry.OtherChanceProviderId is not null))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.seeded_entry " +
                "operation=fixture_draw_cost_choices_require_only_snecko_fixture");
        if (entry.FixtureGeneratedDrawInsertionCardId is not null &&
            entry.FixtureRandomFreeAttackDrawInsertion)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_INSERTION site=combat.seeded_entry " +
                "operation=choose_one_draw_insertion_fixture");
        if (entry.FixtureRandomizeHandCostsMaxExclusive is < 1 or
                > SemanticNatureEnumeratorV111.MaximumSupportedOutcomes)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.seeded_entry " +
                "operation=hand_cost_max_exclusive_out_of_range");
        if (entry.FixtureSetRandomHandCardCost is < 0)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.seeded_entry " +
                "operation=hand_cost_value_out_of_range");
        if (entry.FixtureTopAutoplayPotionId is not null and not
            "proto.potion.distilled_chaos")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=top_autoplay_fixture_requires_distilled_chaos");
        if (entry.FixtureRandomizeHandCostsMaxExclusive is not null &&
            entry.FixtureSetRandomHandCardCost is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.seeded_entry " +
                "operation=choose_one_hand_cost_fixture");
        if (entry.FixturePotionGenerationCardId is not null &&
            (entry.FixtureRandomTargetCardId is not null ||
             entry.FixtureSelectionCardId is not null ||
             entry.FixtureGeneratedDrawInsertionCardId is not null ||
             entry.FixtureRandomFreeAttackDrawInsertion))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=potion_generation_card_fixture_conflicts_with_other_card_fixture");
        if ((entry.FixtureRandomizeHandCostsMaxExclusive is not null ||
             entry.FixtureSetRandomHandCardCost is not null) &&
            (entry.FixtureRandomTargetCardId is not null ||
             entry.FixturePotionGenerationCardId is not null ||
             entry.FixtureGenerationPotionId is not null ||
             entry.FixtureSelectionCardId is not null ||
             entry.FixtureSelectionPotionId is not null ||
             entry.FixturePoolGenerationPotionId is not null ||
             entry.FixtureGeneratedDrawInsertionCardId is not null ||
             entry.FixtureRandomFreeAttackDrawInsertion))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_COST site=combat.seeded_entry " +
                "operation=hand_cost_fixture_conflicts_with_other_pending_choice");
        if (entry.NativePolicyBaseline &&
            (entry.OtherChanceProviderId is not null ||
             entry.FixtureDrawCostRelicId is not null ||
             entry.FixtureRandomTargetCardId is not null ||
             entry.FixtureGenerationPotionId is not null ||
             entry.FixtureSelectionCardId is not null ||
             entry.FixtureSelectionPotionId is not null ||
             entry.FixtureNatureDrawCosts ||
             entry.FixturePoolGenerationPotionId is not null ||
             entry.FixtureGeneratedDrawInsertionCardId is not null ||
             entry.FixtureRandomFreeAttackDrawInsertion ||
             entry.FixtureRandomizeHandCostsMaxExclusive is not null ||
             entry.FixtureSetRandomHandCardCost is not null ||
             entry.FixturePotionGenerationCardId is not null ||
             entry.FixtureTopAutoplayPotionId is not null))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_NATIVE site=combat.seeded_entry " +
                "operation=native_baseline_fixture_loadout_conflict");
        if (entry.SourceRouteEntry &&
            entry.FixturePotionGenerationCardId is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=potion_generation_card_requires_seeded_fixture_entry");
        if (entry.SourceRouteEntry &&
            entry.FixtureTopAutoplayPotionId is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=top_autoplay_potion_requires_seeded_fixture_entry");
        if (entry.SourceRouteEntry)
            return BeginSourceRoute(entry);
        // Act 1 fixed weak entry is deliberately isolated from the
        // Hive/Glory fixture's pre-existing public 2/3-only contract.
        var prepared = entry.Act == 1
            ? PrototypeNativeActOneWeakNatureFixtureV111.CreateCombatRngPreEntry(
                entry.Seed, entry.EncounterId, entry.NativeEncounterEntry,
                entry.TotalFloor, entry.Ascension, entry.FirstMonsterCombatId,
                entry.MapColumn, entry.MapRow,
                richResearch: entry.FixtureSpec?.DistributionId ==
                    ResearchCombatFixtureGeneratorV111.RichDistributionId)
            : PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
                entry.Seed, entry.Act, entry.EncounterId,
                entry.NativeEncounterEntry, entry.TotalFloor, entry.Ascension,
                entry.FirstMonsterCombatId, entry.MapColumn, entry.MapRow);
        var rng = prepared.NativeStreams.Fork();
        using var setupJournal = RandomBoundaryJournalV111.Begin();
        var nativeProvider = new NativeRandomIndexProviderV111();
        var hp = ControlledInitialHpV111.Begin(
            prepared.Combat, prepared.Formation, prepared.NativeStreams);
        using (H8HotPathProfilerV1.Begin("h8.begin.native_hp"))
        {
            while (!hp.Complete)
            {
                var pending = hp.Pending!;
                var decision = StochasticProviderScopeV111.ResolveIndex(
                    nativeProvider, pending, rng);
                hp = ControlledInitialHpV111.Resolve(
                    hp, hp.Fingerprint, decision.SelectedIndex);
            }
        }
        var intents = ControlledIntentsV111.BeginAfterControlledHp(hp);
        using (H8HotPathProfilerV1.Begin("h8.begin.native_intents"))
        {
            while (!intents.Complete)
            {
                var pending = intents.Pending!;
                var decision = StochasticProviderScopeV111.ResolveIndex(
                    nativeProvider, pending, rng);
                intents = ControlledIntentsV111.Resolve(
                    intents, intents.Fingerprint, decision.SelectedIndex);
            }
        }
        var opening = ControlledPlayableCombatEngineV111.Begin(
            intents, entry.Seed, entry.EncounterId, entry.FixtureDeckPresetId,
            entry.FixtureSpec);
        using (H8HotPathProfilerV1.Begin("h8.begin.opening_shuffle"))
        {
            while (!opening.AtPlayerBoundary)
            {
                var pending = opening.Pending ??
                    throw new InvalidDataException(
                        "Engine-owned opening encountered an unsupported chance site.");
                if (pending.SiteId != ControlledPlayableCombatEngineV111.ShuffleSite)
                    throw new NotSupportedException(
                        "UNSUPPORTED_SEMANTIC_OPENING site=" + pending.SiteId);
                var decision = StochasticProviderScopeV111.ResolveIndex(
                    nativeProvider, pending with
                    {
                        SiteId = NativeFisherYatesV111.SiteId
                    }, rng);
                opening = ControlledPlayableCombatEngineV111.Resolve(
                    opening, opening.Fingerprint, decision.SelectedIndex);
            }
        }
        if (entry.OtherChanceProviderId is not null &&
            entry.OtherChanceProviderId != SemanticOtherChanceEngineV111.ProviderId)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE provider=" +
                entry.OtherChanceProviderId);
        SemanticPilotCombatV111 state;
        using (H8HotPathProfilerV1.Begin("h8.begin.semantic_projection"))
            state = Begin(opening) with
            {
                SeededEntry = entry,
                OtherChance = entry.OtherChanceProviderId is null
                    ? null : SemanticOtherChanceEngineV111.Begin(rng)
            };
        if (entry.ReplayOpeningDrawHooks)
        {
            using var openingHooks =
                H8HotPathProfilerV1.Begin("h8.begin.opening_draw_hooks");
            state = ReapplyCommittedOpeningThroughDrawCards(state);
        }
        if (entry.NativePolicyBaseline)
            state = AttachNativePolicyBaseline(state, rng,
                opening.Origin.Formation,
                setupJournal.Entries.Where(decision =>
                    decision.ProviderMode == "native-v111").ToArray(),
                setupJournal.EntriesForProvider("native-v111"));
        if (entry.FixtureDrawCostRelicId is { } relicId)
        {
            if (relicId != "proto.relic.snecko_eye")
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                    "operation=fixture_relic:" + relicId);
            // Explicitly test-only loadout. This is an actual relic used
            // by the real DrawCards source mechanic; its acquisition
            // history is NOT asserted to be native-authentic.
            var world = state.Run.World!;
            var combat = world.Combat!;
            if (state.Run.Player.Relics.Any(x => x.RelicId == relicId))
                throw new InvalidOperationException("Fixture relic already owned.");
            var definition = PrototypeContent.Relic(relicId);
            int index = (combat.Relics ?? []).Length;
            var player = state.Run.Player with
            {
                Relics = state.Run.Player.Relics.Append(
                    new RelicInstance(relicId, PrototypeJson.EmptyObject()))
                    .ToArray()
            };
            var updated = combat with
            {
                Relics = (combat.Relics ?? []).Append(
                    new CombatRelicState(index, relicId, index + 1L,
                        new int[(definition.Triggers ?? []).Length], false))
                    .ToArray()
            };
            state = state with
            {
                Run = state.Run with
                {
                    Player = player,
                    World = world with { Combat = updated }
                }
            };
        }
        if (entry.FixtureRandomTargetCardId is { } targetCardId)
            state = ApplyFixtureRandomTargetCard(state, targetCardId);
        if (entry.FixtureGenerationPotionId is { } potionId)
            state = ApplyFixtureGenerationPotion(state, potionId);
        if (entry.FixtureSelectionCardId is { } selectionCardId)
            state = ApplyFixtureSelectionCard(state, selectionCardId);
        if (entry.FixtureSelectionPotionId is { } selectionPotionId)
            state = ApplyFixtureSelectionPotion(state, selectionPotionId);
        if (entry.FixtureTopAutoplayPotionId is { } topAutoplayPotionId)
            state = ApplyFixtureTopAutoplayPotion(
                state, topAutoplayPotionId);
        if (entry.FixturePoolGenerationPotionId is { } poolPotionId)
            state = ApplyFixturePoolGenerationPotion(state, poolPotionId);
        if (entry.FixtureGeneratedDrawInsertionCardId is { } insertionCardId)
            state = ApplyFixtureGeneratedDrawInsertion(state, insertionCardId);
        if (entry.FixtureRandomFreeAttackDrawInsertion)
            state = ApplyFixtureRandomAttackDrawInsertion(state);
        if (entry.FixtureRandomizeHandCostsMaxExclusive is { } maxExclusive)
            state = ApplyFixtureRandomizeHandCosts(state, maxExclusive);
        if (entry.FixtureSetRandomHandCardCost is { } setCost)
            state = ApplyFixtureSetRandomHandCardCost(state, setCost);
        if (entry.FixturePotionGenerationCardId is { } potionCardId)
            state = ApplyFixturePotionGenerationCard(state, potionCardId);
        using (H8HotPathProfilerV1.Begin("h8.begin.final_validation"))
            state.Validate();
        return state;
    }

    private static SemanticPilotCombatV111 ApplyFixtureGeneratedDrawInsertion(
        SemanticPilotCombatV111 state, string cardId)
    {
        var operation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
            Amount: 1, CardId: cardId);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.BeginControlledDrawInsertionFixture(
                state.Run.Fork(), operation);
        SemanticDrawInsertionChoiceV111.ValidateBoundary(result);
        return state with { Run = result };
    }

    private static SemanticPilotCombatV111 ApplyFixtureRandomAttackDrawInsertion(
        SemanticPilotCombatV111 state)
    {
        var operation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile,
            Amount: 1);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.BeginControlledDrawInsertionFixture(
                state.Run.Fork(), operation);
        SemanticDrawInsertionChoiceV111.ValidateBoundary(result);
        return state with { Run = result };
    }

    private static SemanticPilotCombatV111 ApplyFixtureRandomizeHandCosts(
        SemanticPilotCombatV111 state, int maxExclusive)
    {
        var operation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed,
            Amount: maxExclusive);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.BeginControlledRandomCostFixture(
                state.Run.Fork(), operation);
        SemanticRandomHandCostChoiceV111.ValidateBoundary(result);
        return state with { Run = result };
    }

    private static SemanticPilotCombatV111 ApplyFixtureSetRandomHandCardCost(
        SemanticPilotCombatV111 state, int cost)
    {
        var operation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed,
            Amount: cost);
        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.BeginControlledRandomCostFixture(
                state.Run.Fork(), operation);
        SemanticRandomHandCostChoiceV111.ValidateBoundary(result);
        return state with { Run = result };
    }

    private static SemanticPilotCombatV111 ApplyFixturePoolGenerationPotion(
        SemanticPilotCombatV111 state, string potionId)
    {
        if (potionId != "proto.potion.colorless")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_pool_generation_potion:" + potionId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Pool-generation fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Pool-generation fixture is missing combat.");
        var slot = Array.FindIndex(state.Run.Player.PotionSlots,
            potion => potion is null);
        if (slot < 0 || (combat.Potions ?? []).Any(potion =>
                potion.PotionId == potionId))
            throw new InvalidOperationException(
                "Pool-generation fixture requires an empty potion slot.");
        var slots = (PotionInstance?[])state.Run.Player.PotionSlots.Clone();
        slots[slot] = new PotionInstance(potionId, PrototypeJson.EmptyObject());
        var potions = (combat.Potions ?? []).Append(
            new CombatPotionState(slot, potionId, PrototypeJson.EmptyObject()))
            .OrderBy(potion => potion.Slot).ToArray();
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { PotionSlots = slots },
                World = world with { Combat = combat with { Potions = potions } }
            }
        };
    }

    private static SemanticPilotCombatV111 ApplyFixtureSelectionPotion(
        SemanticPilotCombatV111 state, string potionId)
    {
        if (!IsSemanticSelectionPotion(potionId))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_selection_potion:" + potionId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Selection-potion fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Selection-potion fixture is missing combat.");
        var slot = Array.FindIndex(state.Run.Player.PotionSlots,
            potion => potion is null);
        if (slot < 0 || (combat.Potions ?? []).Any(potion =>
                potion.PotionId == potionId))
            throw new InvalidOperationException(
                "Selection-potion fixture requires an empty potion slot.");
        var slots = (PotionInstance?[])state.Run.Player.PotionSlots.Clone();
        slots[slot] = new PotionInstance(potionId, PrototypeJson.EmptyObject());
        var potions = (combat.Potions ?? []).Append(
            new CombatPotionState(slot, potionId, PrototypeJson.EmptyObject()))
            .OrderBy(potion => potion.Slot).ToArray();
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { PotionSlots = slots },
                World = world with { Combat = combat with { Potions = potions } }
            }
        };
    }

    private static bool IsSemanticSelectionPotion(string? potionId) =>
        potionId is ("proto.potion.touch_of_insanity" or
            "proto.potion.droplet_of_precognition" or
            "proto.potion.liquid_memories");

    private static bool IsSemanticEffectDrawCard(string cardId) =>
        cardId is ("proto.silent.backflip" or
            "proto.silent.quick_slash" or
            "proto.silent.adrenaline" or
            "proto.silent.escape_plan" or
            "proto.silent.expertise" or
            "proto.silent.reflex" or
            "proto.silent.restlessness" or
            "proto.silent.acrobatics" or
            "proto.silent.dagger_throw" or
            "proto.silent.prepared");

    private static SemanticPilotCombatV111 ApplyFixtureSelectionCard(
        SemanticPilotCombatV111 state, string cardId)
    {
        if (cardId is not ("proto.silent.survivor" or
            "proto.colorless.seeker_strike" or
            "proto.colorless.anointed" or
            "proto.silent.nightmare" or
            "proto.colorless.beat_down" or
            "proto.colorless.catastrophe" or
            "proto.colorless.hidden_gem" or
            NativeGloryAncientsV111.WishCardId))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_selection_card:" + cardId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Card-selection fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Card-selection fixture is missing combat.");
        if (combat.Hand.Length == 0)
            throw new InvalidOperationException(
                "Card-selection fixture requires an opening-hand card.");
        var selected = combat.Cards.Single(card =>
            card.InstanceId == combat.Hand[0]);
        var persistentId = selected.PersistentCardInstanceId ??
            throw new InvalidDataException(
                "Card-selection fixture card has no persistent deck identity.");
        var deckIndex = Array.FindIndex(state.Run.Player.Deck, card =>
            card.InstanceId == persistentId);
        if (deckIndex < 0)
            throw new InvalidDataException(
                "Card-selection fixture is absent from the player deck.");
        var cards = combat.Cards.Select(card =>
            card.InstanceId == selected.InstanceId
                ? card with { CardId = cardId }
                : card).ToArray();
        var playerDeck = (CardInstance[])state.Run.Player.Deck.Clone();
        playerDeck[deckIndex] = playerDeck[deckIndex] with { CardId = cardId };
        if (cardId == "proto.colorless.anointed")
        {
            var rareModels = PrototypeContent.RewardCardPool.Where(id =>
            {
                var definition = PrototypeContent.Card(id);
                return definition.Rarity == PrototypeCardRarity.Rare &&
                    definition.MechanicsImplemented &&
                    !definition.MultiplayerOnly;
            }).ToArray();
            if (rareModels.Length == 0 || combat.DrawPile.Length < 2)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                    "operation=anointed_fixture_requires_rare_draw_cards");
            for (var index = 0; index < combat.DrawPile.Length; index++)
            {
                var instanceId = combat.DrawPile[index];
                var sourceCard = cards.Single(card =>
                    card.InstanceId == instanceId);
                var persistentCardId = sourceCard.PersistentCardInstanceId ??
                    throw new InvalidDataException(
                        "Anointed fixture draw card lacks persistent identity.");
                var sourceDeckIndex = Array.FindIndex(playerDeck, card =>
                    card.InstanceId == persistentCardId);
                if (sourceDeckIndex < 0)
                    throw new InvalidDataException(
                        "Anointed fixture draw card is absent from the player deck.");
                var rareModel = rareModels[index % rareModels.Length];
                cards = cards.Select(card => card.InstanceId == instanceId
                    ? card with
                    {
                        CardId = rareModel,
                        State = PrototypeJson.EmptyObject(),
                        Enchantment = null,
                        ReplayCount = 0
                    }
                    : card).ToArray();
                playerDeck[sourceDeckIndex] = playerDeck[sourceDeckIndex] with
                {
                    CardId = rareModel,
                    PersistentState = PrototypeJson.EmptyObject()
                };
            }
        }
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { Deck = playerDeck },
                World = world with { Combat = combat with { Cards = cards } }
            }
        };
    }

    private static SemanticPilotCombatV111 ApplyFixtureRandomTargetCard(
        SemanticPilotCombatV111 state, string cardId)
    {
        if (cardId != "proto.silent.ricochet")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_random_target_card:" + cardId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Random-target fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Random-target fixture is missing combat.");
        if (combat.Hand.Length == 0)
            throw new InvalidOperationException(
                "Random-target fixture requires a card in the opening hand.");
        var selected = combat.Cards.Single(card =>
            card.InstanceId == combat.Hand[0]);
        var persistentId = selected.PersistentCardInstanceId ??
            throw new InvalidDataException(
                "Random-target fixture card has no persistent deck identity.");
        var deckIndex = Array.FindIndex(state.Run.Player.Deck, card =>
            card.InstanceId == persistentId);
        if (deckIndex < 0)
            throw new InvalidDataException(
                "Random-target fixture card is absent from the player deck.");
        var cards = combat.Cards.Select(card =>
            card.InstanceId == selected.InstanceId
                ? card with { CardId = cardId }
                : card).ToArray();
        var playerDeck = (CardInstance[])state.Run.Player.Deck.Clone();
        playerDeck[deckIndex] = playerDeck[deckIndex] with { CardId = cardId };
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { Deck = playerDeck },
                World = world with
                {
                    Combat = combat with { Cards = cards }
                }
            }
        };
    }

    private static SemanticPilotCombatV111 ApplyFixturePotionGenerationCard(
        SemanticPilotCombatV111 state, string cardId)
    {
        if (cardId != "proto.colorless.alchemize")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_potion_generation_card:" + cardId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Potion-generation card fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Potion-generation card fixture is missing combat.");
        if (combat.Hand.Length == 0)
            throw new InvalidOperationException(
                "Potion-generation card fixture requires an opening-hand card.");
        var selected = combat.Cards.Single(card =>
            card.InstanceId == combat.Hand[0]);
        var persistentId = selected.PersistentCardInstanceId ??
            throw new InvalidDataException(
                "Potion-generation fixture card has no persistent deck identity.");
        var deckIndex = Array.FindIndex(state.Run.Player.Deck, card =>
            card.InstanceId == persistentId);
        if (deckIndex < 0)
            throw new InvalidDataException(
                "Potion-generation fixture card is absent from the player deck.");
        var cards = combat.Cards.Select(card =>
            card.InstanceId == selected.InstanceId
                ? card with { CardId = cardId }
                : card).ToArray();
        var playerDeck = (CardInstance[])state.Run.Player.Deck.Clone();
        playerDeck[deckIndex] = playerDeck[deckIndex] with { CardId = cardId };
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { Deck = playerDeck },
                World = world with { Combat = combat with { Cards = cards } }
            }
        };
    }

    private static SemanticPilotCombatV111 ApplyFixtureGenerationPotion(
        SemanticPilotCombatV111 state, string potionId)
    {
        if (potionId != "proto.potion.orobic_acid")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=fixture_generation_potion:" + potionId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Generation fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Generation fixture is missing combat.");
        var slot = Array.FindIndex(state.Run.Player.PotionSlots,
            potion => potion is null);
        if (slot < 0 || (combat.Potions ?? []).Any(potion =>
                potion.PotionId == potionId))
            throw new InvalidOperationException(
                "Generation fixture requires an empty potion slot.");
        var slots = (PotionInstance?[])state.Run.Player.PotionSlots.Clone();
        slots[slot] = new PotionInstance(potionId, PrototypeJson.EmptyObject());
        var potions = (combat.Potions ?? []).Append(
            new CombatPotionState(slot, potionId, PrototypeJson.EmptyObject()))
            .OrderBy(potion => potion.Slot).ToArray();
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { PotionSlots = slots },
                World = world with { Combat = combat with { Potions = potions } }
            }
        };
    }

    private static SemanticPilotCombatV111 ApplyFixtureTopAutoplayPotion(
        SemanticPilotCombatV111 state, string potionId)
    {
        if (potionId != "proto.potion.distilled_chaos")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_LOADOUT site=combat.seeded_entry " +
                "operation=top_autoplay_potion:" + potionId);
        var world = state.Run.World ?? throw new InvalidDataException(
            "Top-autoplay fixture is missing its combat world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Top-autoplay fixture is missing combat.");
        var slot = Array.FindIndex(state.Run.Player.PotionSlots,
            potion => potion is null);
        if (slot < 0 || (combat.Potions ?? []).Any(potion =>
                potion.PotionId == potionId))
            throw new InvalidOperationException(
                "Top-autoplay fixture requires an empty potion slot.");
        var slots = (PotionInstance?[])state.Run.Player.PotionSlots.Clone();
        slots[slot] = new PotionInstance(potionId, PrototypeJson.EmptyObject());
        var potions = (combat.Potions ?? []).Append(
            new CombatPotionState(slot, potionId, PrototypeJson.EmptyObject()))
            .OrderBy(potion => potion.Slot).ToArray();
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { PotionSlots = slots },
                World = world with { Combat = combat with { Potions = potions } }
            }
        };
    }

    private static SemanticPilotCombatV111 BeginSourceRoute(
        SemanticSeededEntryV111 entry)
    {
        if (entry.Act != 1 || entry.Ascension is < 0 or > 10 ||
            entry.RouteActOneRegion is not (ActIdentity.Overgrowth or
                ActIdentity.Underdocks) || entry.OtherChanceProviderId is not null ||
            entry.FixtureDrawCostRelicId is not null ||
            entry.FixtureRandomTargetCardId is not null ||
            entry.FixtureGenerationPotionId is not null ||
            entry.FixtureSelectionCardId is not null ||
            entry.FixtureSelectionPotionId is not null ||
            entry.FixturePoolGenerationPotionId is not null ||
            entry.FixtureGeneratedDrawInsertionCardId is not null ||
            entry.FixtureRandomFreeAttackDrawInsertion ||
            entry.FixtureRandomizeHandCostsMaxExclusive is not null ||
            entry.FixtureSetRandomHandCardCost is not null ||
            entry.ReplayOpeningDrawHooks)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_ROUTE site=act1.entry " +
                "operation=route_configuration");

        var configuration = RunConfiguration.Create(
            "silent", entry.Ascension, entry.RouteActOneRegion) with
        {
            FidelityId = V111Build.NativeWeakCombatEntryFidelityId
        };
        var engine = new PrototypeGameEngine();
        var run = V111RunFactory.Create(entry.Seed, configuration);
        var actions = new List<string>();
        string? neowChoice = null;
        string? mapNodeId = null;
        using var setupJournal = RandomBoundaryJournalV111.Begin();
        using var initialIntentCapture =
            SemanticInitialIntentCaptureV111.Begin();

        for (int step = 0; step < 32 && run.Phase != RunPhase.Combat; step++)
        {
            var legal = engine.GetLegalActions(run);
            if (legal.Count == 0)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_ROUTE site=act1.entry " +
                    "operation=no_legal_route_action");

            GameAction selected;
            if (run.Phase == RunPhase.MapChoice)
            {
                var world = run.World ?? throw new InvalidDataException(
                    "Source route lost its world before the first combat.");
                var available = legal.Where(action =>
                {
                    if (action.Kind != "choose_map_node") return false;
                    var nodeId = action.ReadPayload<ChooseMapNodePayload>()
                        .NodeId;
                    var node = world.Map.Nodes.SingleOrDefault(candidate =>
                        candidate.NodeId == nodeId);
                    return node is not null && node.Floor == 1 &&
                        (world.Map.EntryNodeIds ?? []).Contains(nodeId,
                            StringComparer.Ordinal) &&
                        node.RoomType == PrototypeRoomType.Combat;
                }).ToArray();
                selected = entry.RouteMapNodeId is { Length: > 0 } requested
                    ? available.SingleOrDefault(action =>
                        action.ReadPayload<ChooseMapNodePayload>().NodeId == requested)
                        ?? throw new InvalidOperationException(
                            "Requested source-route map node is not a legal first-floor combat.")
                    : available.FirstOrDefault()
                        ?? throw new NotSupportedException(
                            "UNSUPPORTED_SEMANTIC_ROUTE site=act1.map " +
                            "operation=no_first_floor_combat_entry");
                mapNodeId = selected.ReadPayload<ChooseMapNodePayload>().NodeId;
            }
            else if (run.Phase == RunPhase.Event && run.World?.Event is { } evt &&
                     evt.EventId == PrototypeNativeOvergrowthEvents.NeowEventId &&
                     neowChoice is null)
            {
                var choices = legal.Where(action => action.Kind == "event_choice")
                    .ToArray();
                selected = entry.RouteNeowChoiceId is { Length: > 0 } requested
                    ? choices.SingleOrDefault(action =>
                        action.ReadPayload<EventChoicePayload>().ChoiceId == requested)
                        ?? throw new InvalidOperationException(
                            "Requested Neow route choice is not currently offered.")
                    : choices.FirstOrDefault()
                        ?? throw new NotSupportedException(
                            "UNSUPPORTED_SEMANTIC_ROUTE site=act1.neow " +
                            "operation=no_legal_starting_choice");
                neowChoice = selected.ReadPayload<EventChoicePayload>().ChoiceId;
            }
            else if (run.Phase is RunPhase.Event)
            {
                // Source-shaped opening relics can add a small follow-up
                // choice. Resolve it deterministically before entering the
                // first map node; route actions are recorded for replay.
                selected = legal[0];
            }
            else if (run.Phase == RunPhase.Reward)
            {
                // Some source-shaped Neow relics open a follow-up reward.
                // The first legal option is a deterministic route choice;
                // its exact action is preserved in the replay audit.
                selected = legal[0];
            }
            else if (run.Phase == RunPhase.RunStart)
            {
                selected = legal.Single(action => action.Kind == "start_run");
            }
            else
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_ROUTE site=act1.entry phase=" +
                    run.Phase);

            actions.Add(PrototypeAiEnvironment.StableActionId(selected));
            try
            {
                run = engine.Step(run, selected).State;
            }
            catch (SemanticInitialIntentBoundaryException)
            {
                run = initialIntentCapture.State ??
                    throw new InvalidDataException(
                        "Native combat entry suspended without a saved source state.");
                break;
            }
        }

        if (initialIntentCapture.State is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_ROUTE site=act1.entry " +
                "operation=initial_intent_boundary_not_reached");
        if (run.Phase != RunPhase.Combat || run.World?.Combat is not { } combat ||
            run.World.ActiveRoom != PrototypeRoomType.Combat ||
            combat.PendingChoice is not null ||
            combat.AutomaticPipelineContinuation is not null ||
            combat.Enemies.Any(enemy => enemy.PlannedMoveIndex is not null ||
                enemy.PlannedNextAiStateId is not null) ||
            combat.NativeRng is not { } nativeCombat ||
            run.NativeRunRng is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_ROUTE site=act1.entry " +
                "operation=not_a_native_player_boundary");
        NativeCombatRngEntryV111.Validate(combat);
        NativeRunRngV111.Validate(run);

        var routeWorld = run.World;
        var actualEncounterId = (routeWorld.EncounterHistory ?? [])
            .LastOrDefault()
            ?? throw new InvalidDataException(
                "Source route reached combat without encounter history.");
        var actualNativeEntry = nativeCombat.Formation.NativeEncounterEntry;
        var actualNodeId = mapNodeId ?? routeWorld.Map.CurrentNodeId ??
            throw new InvalidDataException(
                "Source route reached combat without a map node.");
        var actualNode = routeWorld.Map.Nodes.Single(node =>
            node.NodeId == actualNodeId);
        var nodeParts = actualNodeId.Split(':');
        if (nodeParts.Length != 3 ||
            !int.TryParse(nodeParts[2], out var mapColumn))
            throw new InvalidDataException(
                "Source route map node lacks its pinned Act 1 coordinate.");
        if ((entry.EncounterId != "@source-route" &&
             entry.EncounterId != actualEncounterId) ||
            (entry.NativeEncounterEntry != "@source-route" &&
             entry.NativeEncounterEntry != actualNativeEntry) ||
            routeWorld.Floor != 1 ||
            actualNode.RoomType != PrototypeRoomType.Combat)
            throw new InvalidDataException(
                "Source route differs from the requested pinned combat entry.");

        var nativeDecisions = setupJournal.Entries
            .Where(decision => decision.ProviderMode == "native-v111")
            .Select((decision, index) => decision with
            {
                Sequence = index + 1,
                Request = decision.Request with
                {
                    OrderedCandidates = (int[])decision.Request
                        .OrderedCandidates.Clone()
                }
            }).ToArray();
        var nativePolicyTrace = setupJournal.EntriesForProvider("native-v111");
        var routeAudit = new SemanticSourceRouteAuditV111(
            SemanticSourceRouteAuditV111.SourceRouteProviderId,
            entry.RouteActOneRegion, actions.ToArray(), actualNodeId,
            actualEncounterId, actualNativeEntry, nativeDecisions);

        var resolvedEntry = entry with
        {
            EncounterId = actualEncounterId,
            NativeEncounterEntry = actualNativeEntry,
            TotalFloor = routeWorld.Floor,
            FirstMonsterCombatId = nativeCombat.Formation.MonsterContexts
                .First().CombatId,
            MapColumn = mapColumn,
            MapRow = actualNode.Floor - 1,
            RouteNeowChoiceId = neowChoice,
            RouteMapNodeId = actualNodeId
        };
        var baselineCombat = combat.Fork() with { SemanticDeck = null };
        var origin = new ControlledIntentStateV111(
            baselineCombat.Fork() with { NativeRng = null },
            baselineCombat with { NativeRng = null },
            nativeCombat.Formation.Fork(), nativeCombat.RunStreams.Fork(),
            combat.Enemies.Length, [], null,
            Mode: "controlled-v111-source-route-origin-v1",
            PriorHpTranscript: []);
        var sourceOpening = new ControlledPlayableCombatV111(
            entry.Seed, origin, run.Fork(), -1, [], [], null,
            Mode: "controlled-v111-source-route-origin-v1");

        CombatState semanticCombat;
        RunState semanticRun;
        ControlledTurnIntentsV111? pendingInitialIntents = null;
        RandomBoundaryEventV111[] nativePolicyDecisions = nativeDecisions;
        if (entry.NativePolicyBaseline)
        {
            RandomBoundaryEventV111[] committedIntentDecisions;
            RandomBoundaryTranscriptEntryV111[] committedIntentTrace;
            using (var intentJournal = RandomBoundaryJournalV111.Begin())
            {
                PrototypeGameEngine.NativeCommittedIntentAuditV111 committed;
                using (ControlledChanceGuardV111.Enter())
                using (ControlledChanceGuardV111.AllowEngineOwnedNativeProvider())
                using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                    committed = PrototypeGameEngine.CommitEnemyIntentsWithAudit(
                        combat, run.Rng,
                        NativeCombatRngEntryV111.CommittedIntentSiteId);
                using (ControlledChanceGuardV111.Enter())
                using (ControlledChanceGuardV111.AllowEngineOwnedNativeProvider())
                using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                    run = PrototypeGameEngine.ResumeSemanticInitialCombatEntry(
                        run, committed.Combat);
                committedIntentDecisions = intentJournal.Entries
                    .Where(decision => decision.ProviderMode == "native-v111")
                    .ToArray();
                committedIntentTrace = intentJournal
                    .EntriesForProvider("native-v111");
            }
            nativePolicyDecisions = nativeDecisions
                .Concat(committedIntentDecisions)
                .Select((decision, index) => decision with
                {
                    Sequence = index + 1,
                    Request = decision.Request with
                    {
                        OrderedCandidates = (int[])decision.Request
                            .OrderedCandidates.Clone()
                    }
                }).ToArray();
            nativePolicyTrace = RandomBoundaryJournalV111.ConcatOrdered(
                nativePolicyTrace, committedIntentTrace);
            var completedWorld = run.World ?? throw new InvalidDataException(
                "Native source route lost its world after intent resolution.");
            var completedCombat = completedWorld.Combat ??
                throw new InvalidDataException(
                    "Native source route lost combat after intent resolution.");
            semanticCombat =
                SemanticDrawPileAdapterV111.InitializeSemanticDrawPile(
                    completedCombat, SemanticDeckEngineV111.BeginNative(
                        completedCombat.DrawPile.Reverse().ToArray()));
            semanticRun = run with
            {
                World = completedWorld with { Combat = semanticCombat }
            };
        }
        else
        {
            semanticCombat = combat with
            {
                NativeRng = null,
                SemanticDeck = null
            };
            semanticRun = run with
            {
                NativeRunRng = null,
                World = routeWorld with { Combat = semanticCombat }
            };
            pendingInitialIntents = ControlledTurnIntentEngineV111.Begin(
                semanticCombat, actualNativeEntry,
                siteId: NativeCombatRngEntryV111.CommittedIntentSiteId);
            if (pendingInitialIntents.Complete)
                semanticRun = CompleteNatureInitialCombatEntry(
                    semanticRun, pendingInitialIntents);
            else
            {
                semanticCombat = pendingInitialIntents.BaselineCombat.Fork();
                semanticRun = semanticRun with
                {
                    World = routeWorld with { Combat = semanticCombat }
                };
            }
        }

        var state = new SemanticPilotCombatV111(
            sourceOpening, semanticRun, null, [], actualNativeEntry)
        {
            SeededEntry = resolvedEntry,
            OtherChance = entry.NativePolicyBaseline
                ? null
                : SemanticOtherChanceEngineV111.Begin(
                    nativeCombat.RunStreams),
            SourceRouteAudit = routeAudit,
            PendingInitialIntents = pendingInitialIntents is { Complete: false }
                ? pendingInitialIntents
                : null,
            NativePolicyAudit = entry.NativePolicyBaseline
                ? new SemanticNativePolicyAuditV111(
                    SemanticNativePolicyAuditV111.NativeProviderId,
                    nativePolicyDecisions)
                {
                    ProviderTrace = nativePolicyTrace
                }
                : null
        };
        state.Validate();
        return state;
    }

    private static SemanticPilotCombatV111 AttachNativePolicyBaseline(
        SemanticPilotCombatV111 state, RngBundle nativeStreams,
        NativeEncounterFormationV111 formation,
        RandomBoundaryEventV111[] setupDecisions,
        RandomBoundaryTranscriptEntryV111[] setupTrace)
    {
        ArgumentNullException.ThrowIfNull(nativeStreams);
        ArgumentNullException.ThrowIfNull(formation);
        var run = state.Run;
        var world = run.World ?? throw new InvalidDataException(
            "Native policy entry is missing its run world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Native policy entry is missing its combat.");
        if (run.NativeRunRng is not null || combat.NativeRng is not null ||
            combat.SemanticDeck is null ||
            combat.SemanticDeck.Mode != SemanticDeckStateV111.NatureMode)
            throw new InvalidOperationException(
                "Native policy entry requires a fresh semantic seeded opening.");

        var exactDeck = SemanticDeckEngineV111.BeginNative(
            combat.DrawPile.Reverse().ToArray());
        var nativeCombat =
            SemanticDrawPileAdapterV111.ApplyCommittedPhysicalMutation(
                combat with
                {
                    NativeRng = new NativeCombatRngV111(
                        nativeStreams.Fork(), formation.Fork())
                }, combat.DrawPile, exactDeck);
        NativeCombatRngEntryV111.Validate(nativeCombat);
        var nativeRun = run with
        {
            NativeRunRng = nativeStreams.Fork(),
            World = world with { Combat = nativeCombat }
        };
        NativeRunRngV111.Validate(nativeRun);
        return state with
        {
            Run = nativeRun,
            NativePolicyAudit = new SemanticNativePolicyAuditV111(
                SemanticNativePolicyAuditV111.NativeProviderId,
                setupDecisions.Select((decision, index) => decision with
                {
                    Sequence = index + 1,
                    Request = decision.Request with
                    {
                        OrderedCandidates = (int[])decision.Request
                            .OrderedCandidates.Clone()
                    }
                }).ToArray())
            {
                ProviderTrace = setupTrace
            }
        };
    }

    /// <summary>
    /// Recreate the same already committed opening permutation from the
    /// original post-shuffle draw pile and the dealt hand. Execute actual
    /// DrawCards/CardDrawn mechanics once, with all positions private and
    /// known, before the first player decision. The restricted initial
    /// starter hand has no random draw-cost or nested player choices.
    /// This is an opt-in source-mechanic bridge, not full native combat
    /// entry / BeforeHandDraw hook parity.
    /// </summary>
    private static SemanticPilotCombatV111 ReapplyCommittedOpeningThroughDrawCards(
        SemanticPilotCombatV111 state)
    {
        var run = state.Run;
        var world = run.World!;
        var combat = world.Combat!;
        var dealt = (long[])combat.Hand.Clone();
        if (dealt.Length == 0 || combat.PendingChoice is not null ||
            combat.AutomaticPipelineContinuation is not null ||
            combat.SemanticDeck?.Pending is not null)
            throw new InvalidOperationException(
                "Opening hook replay requires a committed ordinary dealt hand.");
        var full = combat.DrawPile.Concat(dealt.Reverse()).ToArray();
        if (full.Length != full.Distinct().Count())
            throw new InvalidDataException(
                "Reconstituted opening pile duplicated physical card identity.");
        var initialDeck = SemanticDeckEngineV111.FromCommittedOpening(
            full.Reverse().ToArray());
        var openingCombat = SemanticDrawPileAdapterV111
            .ApplyCommittedZoneTransition(
                combat,
                combat.Mechanics.CardZones.WithZoneChanges(
                    new Dictionary<PrototypeCardZone, long[]>
                    {
                        [PrototypeCardZone.Hand] = [],
                        [PrototypeCardZone.DrawPile] = full
                    }),
                initialDeck);
        var prepared = run with
        {
            World = world with
            {
                Combat = openingCombat
            }
        };
        RunState drawn;
        int remaining;
        using (ControlledChanceGuardV111.Enter())
            (drawn, remaining) = PrototypeGameEngine.BeginSemanticCombatDraw(
                prepared, dealt.Length, fromHandDraw: true);
        var result = drawn.World?.Combat;
        if (remaining != 0 || drawn.Phase != RunPhase.Combat ||
            result is null || result.PendingChoice is not null ||
            result.AutomaticPipelineContinuation is not null ||
            result.SemanticDeck?.Pending is not null ||
            !result.Hand.SequenceEqual(dealt) ||
            !result.DrawPile.SequenceEqual(combat.DrawPile))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OPENING site=combat.opening.DrawCards " +
                "operation=unmodeled_initial_draw_hook");
        return PublishRunContinuation(
            state, drawn, null, preservePendingEndTurn: true);
    }

    public static SemanticPilotCombatV111 Step(
        SemanticPilotCombatV111 current, string fingerprint, string actionId)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic player-action fingerprint.");
        return StepCurrentPlayerAction(current, actionId, null, false);
    }

    /// <summary>
    /// Internal only: the H8 runtime has already checked its opaque boundary
    /// version and uses the immutable public action frame captured for that
    /// version. Skip the v111 whole-state SHA and redundant observation.
    /// The source action engine still checks legality and executes the same
    /// card, hook, selection, reward and terminal mechanics as legacy Step.
    /// </summary>
    internal static SemanticPilotCombatV111 StepFromH8TrustedBoundary(
        SemanticPilotCombatV111 current, string actionId,
        PrototypeAiFrame playerFrame)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(playerFrame);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        return StepCurrentPlayerAction(current, actionId, playerFrame, true);
    }

    private static SemanticPilotCombatV111 StepCurrentPlayerAction(
        SemanticPilotCombatV111 current, string actionId,
        PrototypeAiFrame? playerFrame, bool trustedH8)
    {
        if (current.AtPlayerChoiceBoundary)
            return current.KnowledgeDemonChoicePending is not null
                ? StepKnowledgeDemonChoice(current, actionId)
                : current.DrawHookChoicePending is not null
                    ? StepDrawHookChoice(current, actionId)
                    : StepCardSelection(current, actionId);
        if (!current.AtPlayerBoundary)
            throw new InvalidOperationException("Nature decision or combat terminal is pending.");
        if (current.UsesNativePolicyBaseline)
            return StepNativePolicyBaseline(current, actionId);
        var environment = new PrototypeAiEnvironment();
        var frame = playerFrame ?? environment.Observe(current.Run);
        var action = frame.LegalActions.SingleOrDefault(a =>
            a.ActionId == actionId) ?? throw new InvalidOperationException(
                "Illegal semantic player action.");
        // PrototypeGameEngine.Step already forks the input before touching
        // combat mechanics. H8's version-owned card/potion path can pass the
        // immutable checkpoint directly and avoid a second complete copy.
        // End-turn continuation construction does not use Step and retains
        // its independently owned defensive fork.
        var source = trustedH8 && action.Kind is "play_card" or "use_potion"
            ? current.Run : current.Run.Fork();
        SemanticEndTurnV111? endTurn = null;
        RunState result;
        if (action.Kind == "end_turn")
        {
            (endTurn, result) =
                SemanticEndTurnEngineV111.BeginFromPlayerActionOrTerminal(
                    source, current.NativeEncounterEntry);
        }
        else if (action.Kind == "play_card")
        {
            var play = action.Payload.Deserialize<PlayCardPayload>() ??
                throw new InvalidDataException("Missing source card payload.");
            var card = source.World!.Combat!.Cards.SingleOrDefault(c =>
                c.InstanceId == play.CardInstanceId);
            var semanticSelectionCard = card?.CardId is
                ("proto.silent.survivor" or "proto.silent.hand_trick" or
                    "proto.silent.nightmare" or
                    NativeGloryAncientsV111.WishCardId);
            var semanticDrawCard = card is not null &&
                IsSemanticEffectDrawCard(card.CardId);
            var semanticAutoplayCard = card?.CardId is
                ("proto.colorless.beat_down" or
                 "proto.colorless.catastrophe");
            var semanticRandomReplayCard =
                card?.CardId == "proto.colorless.hidden_gem";
            var semanticPotionGenerationCard = card?.CardId ==
                current.SeededEntry?.FixturePotionGenerationCardId &&
                card?.CardId == "proto.colorless.alchemize";
            var semanticRandomRareToHandCard =
                card?.CardId == "proto.colorless.anointed";
            var fixtureSelectionCard = card?.CardId ==
                    current.SeededEntry?.FixtureSelectionCardId &&
                card?.CardId == "proto.colorless.seeker_strike";
            var supportedCardAction = card is not null &&
                (SemanticPlayerActionEngineV111.SupportsCard(card.CardId) ||
                 card.CardId == "proto.silent.ricochet" ||
                 semanticDrawCard ||
                 semanticAutoplayCard ||
                 semanticRandomReplayCard ||
                 semanticPotionGenerationCard ||
                 semanticRandomRareToHandCard ||
                 semanticSelectionCard ||
                 fixtureSelectionCard);
            if (!supportedCardAction)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=card_effect_requires_semantic_audit");
            // Preserve actual source damage, relic/power hooks and victory
            // transitions. Supported hidden-pile mutations must update the
            // provider-owned semantic deck at their source boundary.
            using (H8HotPathProfilerV1.Begin("h8.player.engine_card"))
            using (ControlledChanceGuardV111.Enter())
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                result = trustedH8 && playerFrame is not null
                    ? environment.StepFromH8TrustedBoundary(
                        source, playerFrame, actionId).State
                    : environment.Step(source, actionId).State;
            if (!PrototypeGameEngine.IsAtCombatActionResultBoundary(result))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=unsupported_post_card_phase");
            if (result.Phase == RunPhase.Combat)
            {
                if (result.World?.Combat?.PendingChoice?
                        .ControlledSemanticDraw is not null)
                    SemanticDrawChoiceV111.ValidateBoundary(result);
                else if (result.World?.Combat?.PendingChoice?
                        .ControlledCardShortlist is not null)
                    SemanticCardShortlistChoiceV111.ValidateBoundary(result);
                else if (result.World?.Combat?.PendingChoice?.ChoiceId ==
                    "select_cards")
                    SemanticCardSelectionEngineV111
                        .ValidatePendingBoundary(result);
                else if (result.World?.Combat?.PendingChoice?
                    .ControlledPoolShuffle is not null)
                    SemanticPoolShuffleChoiceV111.ValidateBoundary(result);
                else
                    ValidateSemanticCombatBoundary(result);
            }
            var previous = current.Run.World!.Combat!;
            var next = result.World?.Combat;
            if (result.Phase == RunPhase.Reward)
            {
                // The original EnterReward consumes the defeated combat
                // and publishes RewardState. It is a genuine terminal
                // combat victory; no provider-owned deck survives to
                // mutate or expose on the next player decision.
                if (result.World?.Reward is null ||
                    result.World.Combat is not null)
                    throw new InvalidDataException(
                        "Victory did not enter a genuine combat reward boundary.");
            }
            else if (result.Phase == RunPhase.Terminal)
            {
                // A lethal self-damage or combat hook closes the run without
                // leaving a combat or a provider-owned deck to resume.
                if (result.World is not
                    { Combat: null, Reward: null, TerminalOutcome: "defeat" })
                    throw new InvalidDataException(
                        "Combat defeat did not close its active mechanics state.");
            }
            else if (next is null ||
                     !previous.DrawPile.SequenceEqual(next.DrawPile) ||
                     !SameSemanticDeckForPlayerAction(
                         previous.SemanticDeck, next.SemanticDeck, trustedH8))
            {
                if ((!semanticDrawCard && !semanticAutoplayCard &&
                     !semanticRandomRareToHandCard) || next is null)
                    throw new NotSupportedException(
                        "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                        "operation=unmodeled_draw_pile_mutation");
                PrototypeStateInvariants.Validate(result);
            }
        }
        else if (action.Kind == "use_potion")
        {
            var payload = action.Payload.Deserialize<UsePotionPayload>() ??
                throw new InvalidDataException("Missing potion action payload.");
            var fixturePotion = current.SeededEntry?.FixtureGenerationPotionId;
            var poolPotion = current.SeededEntry?.FixturePoolGenerationPotionId;
            var validSlot = payload.Slot >= 0 &&
                payload.Slot < source.Player.PotionSlots.Length;
            var isGenerationPotion = fixturePotion ==
                    "proto.potion.orobic_acid" && validSlot &&
                source.Player.PotionSlots[payload.Slot]?.PotionId == fixturePotion;
            var isPoolPotion = poolPotion == "proto.potion.colorless" &&
                validSlot &&
                source.Player.PotionSlots[payload.Slot]?.PotionId == poolPotion;
            var isHandReturnPotion = validSlot &&
                source.Player.PotionSlots[payload.Slot]?.PotionId ==
                    "proto.potion.bottled_potential";
            var isDistilledChaos = validSlot &&
                source.Player.PotionSlots[payload.Slot]?.PotionId ==
                    "proto.potion.distilled_chaos";
            var isSelectionPotion = validSlot && IsSemanticSelectionPotion(
                source.Player.PotionSlots[payload.Slot]?.PotionId);
            // The H9 structured research fixtures only admit three
            // deterministic, implemented potion effects. Source legality
            // still validates the living target and inventory slot in
            // PrototypeGameEngine.UsePotion. Do not discard offered actions
            // or treat legal potion use as a censored training world.
            var researchPotionId = current.SeededEntry?.FixtureSpec is not null
                && validSlot
                ? source.Player.PotionSlots[payload.Slot]?.PotionId
                : null;
            var isResearchSelfPotion =
                researchPotionId == "proto.potion.block";
            var isResearchTargetPotion = researchPotionId is
                ("proto.potion.fire" or "proto.potion.poison");
            if ((!isGenerationPotion && !isPoolPotion &&
                 !isSelectionPotion && !isHandReturnPotion &&
                 !isDistilledChaos && !isResearchSelfPotion &&
                 !isResearchTargetPotion) ||
                (isResearchTargetPotion
                    ? payload.TargetEnemyId is null
                    : payload.TargetEnemyId is not null))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=potion_effect_requires_semantic_audit");
            using (H8HotPathProfilerV1.Begin("h8.player.engine_potion"))
            using (ControlledChanceGuardV111.Enter())
            // A killing potion completes combat and initializes its reward.
            // The reward generator uses its own RNG stream and is explicitly
            // allowlisted; it must not be mistaken for combat Nature chance.
            // Match the existing play-card action boundary exactly.
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                result = trustedH8 && playerFrame is not null
                    ? environment.StepFromH8TrustedBoundary(
                        source, playerFrame, actionId).State
                    : environment.Step(source, actionId).State;
            if (result.Phase != RunPhase.Combat &&
                !((isDistilledChaos || isResearchTargetPotion) &&
                  PrototypeGameEngine.IsAtCombatActionResultBoundary(result)))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=unsupported_post_potion_phase");
            if (result.Phase == RunPhase.Combat)
            {
                if (result.World?.Combat?.PendingChoice?.ControlledPoolShuffle is not null)
                    SemanticPoolShuffleChoiceV111.ValidateBoundary(result);
                else if (result.World?.Combat?.PendingChoice?
                             .ControlledDrawInsertion is not null)
                    SemanticDrawInsertionChoiceV111.ValidateBoundary(result);
                else if (result.World?.Combat?.PendingChoice?
                             .ControlledSemanticDraw is not null)
                    SemanticDrawChoiceV111.ValidateBoundary(result);
                else if (result.World?.Combat?.PendingChoice is
                         { ChoiceId: "controlled_draw_cost",
                           ControlledSemanticDraw: not null })
                    SemanticCostChoiceV111.ValidateDrawBoundary(result);
                else if (result.World?.Combat?.PendingChoice?.ChoiceId ==
                    "select_cards")
                    SemanticCardSelectionEngineV111.ValidatePendingBoundary(result);
                else
                    ValidateSemanticCombatBoundary(result);
            }
            else if (result.Phase == RunPhase.Reward &&
                     (result.World?.Reward is null ||
                      result.World.Combat is not null))
                throw new InvalidDataException(
                    "Potion victory did not enter a combat reward boundary.");
            var previous = current.Run.World!.Combat!;
            var next = result.World?.Combat;
            if (result.Phase == RunPhase.Combat &&
                (next is null ||
                (!isHandReturnPotion && !isDistilledChaos &&
                !previous.DrawPile.SequenceEqual(next.DrawPile) ||
                !isHandReturnPotion && !isDistilledChaos &&
                !SameSemanticDeckForPlayerAction(
                    previous.SemanticDeck, next.SemanticDeck, trustedH8))))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=unmodeled_draw_pile_mutation");
        }
        else
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action operation=" +
                action.Kind);
        var nextState = PublishRunContinuation(
            current, result, endTurn,
            new SemanticPilotContinuationV111("player", actionId));
        return NormalizeNoncontrolledCosts(nextState);
    }

    /// <summary>
    /// A trusted H8 player frame is version-owned and has already passed its
    /// source legality check. Compare the live provider deck fields directly
    /// rather than serializing its growing transcript on every ordinary play.
    /// Retain the original canonical SHA comparison on the external v111 path.
    /// </summary>
    private static bool SameSemanticDeckForPlayerAction(
        SemanticDeckStateV111? before, SemanticDeckStateV111? after,
        bool trustedH8)
    {
        if (!trustedH8)
            return CanonicalJson.Sha256(before) == CanonicalJson.Sha256(after);
        return before is null
            ? after is null
            : before.HasSameSerializedContentsAs(after);
    }

    private static SemanticPilotCombatV111 StepCardSelection(
        SemanticPilotCombatV111 current, string actionId)
    {
        var pending = DescribePendingCardSelectionEvent(current) ??
            throw new InvalidOperationException(
                "No semantic card-selection event is pending.");
        var decision = SemanticCardSelectionEngineV111.Select(
            pending, pending.EventId, actionId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_card_selection",
            CardSelectionDecision: decision.Fork());
        var (resumedRun, siteId) = SemanticNestedChoiceResumeV111.Resume(
            current.Run, choice);
        var nextEndTurn = current.PendingEndTurn is { } endTurn
            ? SemanticEndTurnEngineV111.ContinueNestedAutomaticChoice(
                endTurn, resumedRun, choice, siteId)
            : null;
        var next = PublishRunContinuation(
            current, resumedRun, nextEndTurn,
            new SemanticPilotContinuationV111(
                "semantic_card_selection",
                CardSelectionDecision: decision.Fork()),
            preservePendingEndTurn: current.PendingEndTurn is null);
        Validate(next);
        return next;
    }

    private static SemanticPilotCombatV111 StepDrawHookChoice(
        SemanticPilotCombatV111 current, string actionId)
    {
        var pending = current.DrawHookChoicePending ??
            throw new InvalidOperationException(
                "No CardDrawn hook action is pending.");
        var endTurn = current.PendingEndTurn ??
            throw new InvalidDataException(
                "Draw-hook action lost its end-turn continuation.");
        var priorNestedChoiceCount = endTurn.NestedChoiceTranscript.Length;
        var priorHandDrawHookChoiceCount =
            endTurn.HandDraw?.DrawHookChoiceTranscript.Length ?? 0;
        var nextEndTurn = SemanticEndTurnEngineV111.ResolveDrawHookChoice(
            endTurn, endTurn.Fingerprint, actionId);
        var decision = nextEndTurn.NestedChoiceTranscript.Length >
                priorNestedChoiceCount
            ? nextEndTurn.NestedChoiceTranscript[^1].DrawHookChoiceDecision
            : nextEndTurn.HandDraw is { } handDraw &&
                handDraw.DrawHookChoiceTranscript.Length >
                    priorHandDrawHookChoiceCount
                ? handDraw.DrawHookChoiceTranscript[^1]
                : null;
        if (decision is null)
            throw new InvalidDataException(
                "Draw-hook action omitted its replay decision.");
        if (CanonicalJson.Sha256(decision.Event) !=
                CanonicalJson.Sha256(pending) ||
            decision.ActionId != actionId)
            throw new InvalidDataException(
                "Draw-hook action transcript diverged from its event.");
        var next = PublishRunContinuation(
            current, nextEndTurn.Run, nextEndTurn,
            new SemanticPilotContinuationV111(
                "semantic_draw_hook_choice",
                DrawHookChoiceDecision: decision.Fork()));
        return NormalizeNoncontrolledCosts(next);
    }

    private static SemanticPilotCombatV111 StepKnowledgeDemonChoice(
        SemanticPilotCombatV111 current, string actionId)
    {
        var pending = current.KnowledgeDemonChoicePending ??
            throw new InvalidOperationException(
                "No Knowledge Demon action is pending.");
        var endTurn = current.PendingEndTurn ??
            throw new InvalidDataException(
                "Knowledge Demon action lost its end-turn continuation.");
        var priorNestedChoiceCount = endTurn.NestedChoiceTranscript.Length;
        var nextEndTurn = SemanticEndTurnEngineV111.ResolveKnowledgeDemonChoice(
            endTurn, endTurn.Fingerprint, actionId);
        var decision = nextEndTurn.NestedChoiceTranscript.Length >
                priorNestedChoiceCount
            ? nextEndTurn.NestedChoiceTranscript[^1]
                .KnowledgeDemonChoiceDecision
            : null;
        if (decision is null ||
            CanonicalJson.Sha256(decision.Event) !=
                CanonicalJson.Sha256(pending) ||
            decision.ActionId != actionId)
            throw new InvalidDataException(
                "Knowledge Demon action omitted its replay decision.");
        var next = PublishRunContinuation(
            current, nextEndTurn.Run, nextEndTurn,
            new SemanticPilotContinuationV111(
                "semantic_knowledge_demon_choice", actionId,
                KnowledgeDemonChoiceDecision: decision.Fork()));
        Validate(next);
        return NormalizeNoncontrolledCosts(next);
    }

    /// <summary>
    /// Resolve one suspended random-enemy effect by stable physical enemy
    /// identity, then resume the original source continuation.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveRandomTarget(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int enemyInstanceId)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic target fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (current.PendingEndTurn is { TargetPending: not null } endTurn)
        {
            var resolvedEndTurn = SemanticEndTurnEngineV111.ResolveTarget(
                endTurn, endTurn.Fingerprint, eventId, enemyInstanceId);
            if (resolvedEndTurn.TargetTranscript.Length <=
                endTurn.TargetTranscript.Length)
                throw new InvalidDataException(
                    "End-turn target resolution omitted its selected identity.");
            var endTurnDecision = resolvedEndTurn.TargetTranscript[^1];
            var resolvedSession = PublishRunContinuation(
                current, resolvedEndTurn.Run, resolvedEndTurn,
                new SemanticPilotContinuationV111(
                    "semantic_target",
                    TargetDecision: endTurnDecision.Fork()));
            Validate(resolvedSession);
            return NormalizeNoncontrolledCosts(resolvedSession);
        }

        var pending = current.TargetPending ??
            throw new InvalidOperationException(
                "No semantic random-target event is pending.");
        var decision = SemanticTargetChoiceV111.Select(
            pending, eventId, enemyInstanceId);
        RunState result = SemanticTargetChoiceV111.Resume(
            current.Run, decision);
        if (result.Phase == RunPhase.Combat)
            ValidateSemanticCombatBoundary(result);
        var next = PublishRunContinuation(
            current, result, null,
            new SemanticPilotContinuationV111(
                "semantic_target", TargetDecision: decision.Fork()),
            preservePendingEndTurn: true);
        return NormalizeNoncontrolledCosts(next);
    }

    /// <summary>
    /// Resolve one generated card model from its ordered source pool and
    /// resume the original generation/trigger continuation.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveGeneratedCard(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, string cardId)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic generated-card fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var pending = current.GeneratedCardPending ??
            throw new InvalidOperationException(
                "No semantic generated-card event is pending.");
        var expected = SemanticGenerationChoiceV111.Select(
            pending, eventId, cardId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_generated_card", GeneratedCardDecision: expected.Fork());
        var (result, siteId) = SemanticNestedChoiceResumeV111.Resume(
            current.Run, choice);
        var nextEndTurn = current.PendingEndTurn is { } endTurn
            ? SemanticEndTurnEngineV111.ContinueNestedAutomaticChoice(
                endTurn, result, choice, siteId)
            : null;
        var next = PublishRunContinuation(
            current, result, nextEndTurn,
            new SemanticPilotContinuationV111(
                "semantic_generated_card",
                GeneratedCardDecision: expected.Fork()),
            preservePendingEndTurn: current.PendingEndTurn is null);
        return NormalizeNoncontrolledCosts(next);
    }

    public static SemanticPilotCombatV111 ResolvePotionGeneration(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, string potionId)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic potion-generation fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(potionId);
        var pending = current.PotionGenerationPending ??
            throw new InvalidOperationException(
                "No semantic potion-generation event is pending.");
        var expected = SemanticPotionGenerationChoiceV111.Select(
            pending, eventId, potionId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_potion_generation",
            PotionGenerationDecision: expected.Fork());
        var (result, siteId) = SemanticNestedChoiceResumeV111.Resume(
            current.Run, choice);
        var nextEndTurn = current.PendingEndTurn is { } endTurn
            ? SemanticEndTurnEngineV111.ContinueNestedAutomaticChoice(
                endTurn, result, choice, siteId)
            : null;
        return NormalizeNoncontrolledCosts(PublishRunContinuation(
            current, result, nextEndTurn,
            new SemanticPilotContinuationV111(
                "semantic_potion_generation",
                PotionGenerationDecision: expected.Fork()),
            preservePendingEndTurn: current.PendingEndTurn is null));
    }

    public static SemanticPilotCombatV111 ResolveCardTransform(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, string cardId)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic card-transform fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var pending = current.CardTransformPending ??
            throw new InvalidOperationException(
                "No semantic card-transform event is pending.");
        var expected = SemanticCardTransformChoiceV111.Select(
            pending, eventId, cardId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_card_transform", CardTransformDecision: expected.Fork());
        var (result, siteId) = SemanticNestedChoiceResumeV111.Resume(
            current.Run, choice);
        var nextEndTurn = current.PendingEndTurn is { } endTurn
            ? SemanticEndTurnEngineV111.ContinueNestedAutomaticChoice(
                endTurn, result, choice, siteId)
            : null;
        return NormalizeNoncontrolledCosts(PublishRunContinuation(
            current, result, nextEndTurn,
            new SemanticPilotContinuationV111(
                "semantic_card_transform",
                CardTransformDecision: expected.Fork()),
            preservePendingEndTurn: current.PendingEndTurn is null));
    }

    private static SemanticPilotCombatV111 StepNativePolicyBaseline(
        SemanticPilotCombatV111 current, string actionId)
    {
        var environment = new PrototypeAiEnvironment();
        var frame = environment.Observe(current.Run);
        if (frame.LegalActions.Count(a => a.ActionId == actionId) != 1)
            throw new InvalidOperationException(
                "Illegal or ambiguous native-policy player action.");
        RunState result;
        using var boundaries = RandomBoundaryJournalV111.Begin();
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedNativeProvider())
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
        using (StochasticProviderScopeV111.EnterBoundary(
            new NativeRandomBoundaryProviderV111(),
            overrideExplicitProviders: false))
            result = environment.Step(current.Run.Fork(), actionId).State;

        var audit = current.NativePolicyAudit ??
            throw new InvalidDataException(
                "Native policy baseline is missing provider provenance.");
        int offset = audit.Decisions.Length;
        var recorded = boundaries.Entries
            .Where(entry => entry.ProviderMode == "native-v111")
            .Select((entry, index) => entry with
        {
            Sequence = offset + index + 1,
            Request = entry.Request with
            {
                OrderedCandidates = (int[])entry.Request.OrderedCandidates.Clone()
            }
            }).ToArray();
        var recordedTrace = boundaries.EntriesForProvider("native-v111");

        if (result.Phase == RunPhase.Combat)
            ValidateNativePolicyBoundary(result);
        else if (!PrototypeGameEngine.IsAtCombatActionResultBoundary(result))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_NATIVE site=combat.player_action " +
                "operation=unsupported_post_action_phase");

        var next = PublishRunContinuation(
            current, result, null,
            new SemanticPilotContinuationV111("player", actionId)) with
        {
            NativePolicyAudit = audit with
            {
                Decisions = audit.Decisions.Concat(recorded).ToArray(),
                ProviderTrace = RandomBoundaryJournalV111.ConcatOrdered(
                    audit.ProviderTrace, recordedTrace)
            }
        };
        Validate(next);
        return next;
    }

    private static SemanticRewardAuditV111 RecordRewardAudit(
        RunState source, RunState result)
    {
        static ulong Count(RunState run) =>
            run.Rng.Streams.Single(x =>
                x.StreamId == "reward" &&
                x.Codec == PrototypeRng.Codec).CallCount
            ?? throw new InvalidDataException(
                "Reward RNG stream has no saved call count.");
        var before = Count(source);
        var after = Count(result);
        if (after < before)
            throw new InvalidDataException(
                "Engine-owned reward RNG cursor rewound on victory.");
        return new SemanticRewardAuditV111(
            SemanticRewardAuditV111.PrototypeProviderId, before, after);
    }

    public static SemanticPilotCombatV111 Resolve(
        SemanticPilotCombatV111 current, string fingerprint, int selectedIndex)
        => Resolve(current, fingerprint, selectedIndex, null, null, false, null);

    public static SemanticPilotCombatV111 ResolveRandomPileCard(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, long cardInstanceId)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic random pile-card fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.RandomPileCardPending ??
            throw new InvalidOperationException(
                "No random pile-card Nature event is pending.");
        var decision = SemanticRandomPileCardEngineV111.Select(
            pending, eventId, cardInstanceId);
        return Resolve(current, fingerprint, decision.SelectedIndex,
            null, null, false, null, decision, prevalidated: true);
    }

    public static SemanticPilotCombatV111 ResolveCost(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int selectedCost)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic cost fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.CostPending ??
            throw new InvalidOperationException(
                "No Nature-controlled drawn-card cost is pending.");
        var decision = SemanticCostChoiceV111.Select(
            pending, eventId, selectedCost);
        var index = decision.SelectedIndex;
        if (current.Run.World?.Combat?.PendingChoice?
                .ControlledSemanticDraw is not null)
            return Resolve(current, fingerprint, index,
                null, null, false, decision, prevalidated: true);
        if (pending.TargetCardInstanceId is not null)
        {
            SemanticCostChoiceV111.ValidateDecision(
                current.CostPending, decision, index);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_cost", CostDecision: decision.Fork());
            var (result, siteId) = SemanticNestedChoiceResumeV111.Resume(
                current.Run, choice);
            var nextEndTurn = current.PendingEndTurn is { } endTurn
                ? SemanticEndTurnEngineV111.ContinueNestedAutomaticChoice(
                    endTurn, result, choice, siteId)
                : null;
            var next = PublishRunContinuation(
                current, result, nextEndTurn,
                new SemanticPilotContinuationV111(
                    "semantic_cost", CostDecision: decision.Fork()),
                preservePendingEndTurn: current.PendingEndTurn is null);
            Validate(next);
            return next;
        }
        return Resolve(current, fingerprint, index, null, null, false, decision);
    }

    public static SemanticPilotCombatV111 ResolveHandCostSelection(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, long cardInstanceId)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic hand-cost selection fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.HandCostSelectionPending ??
            throw new InvalidOperationException(
                "No random hand-card cost selection is pending.");
        var decision = SemanticHandCostSelectionChoiceV111.Select(
            pending, eventId, cardInstanceId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_hand_cost_selection",
            HandCostSelectionDecision: decision.Fork());
        var (result, siteId) = SemanticNestedChoiceResumeV111.Resume(
            current.Run, choice);
        var nextEndTurn = current.PendingEndTurn is { } endTurn
            ? SemanticEndTurnEngineV111.ContinueNestedAutomaticChoice(
                endTurn, result, choice, siteId)
            : null;
        var next = PublishRunContinuation(
            current, result, nextEndTurn,
            new SemanticPilotContinuationV111(
                "semantic_hand_cost_selection",
                HandCostSelectionDecision: decision.Fork()),
            preservePendingEndTurn: current.PendingEndTurn is null);
        Validate(next);
        return next;
    }

    public static SemanticPilotCombatV111 ResolveCardShortlist(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, long cardInstanceId)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic card-shortlist fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.CardShortlistPending ??
            throw new InvalidOperationException(
                "No random card-pile shortlist is pending.");
        var decision = SemanticCardShortlistChoiceV111.Select(
            pending, eventId, cardInstanceId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_card_shortlist", CardShortlistDecision: decision.Fork());
        var (result, siteId) = SemanticNestedChoiceResumeV111.Resume(
            current.Run, choice);
        var nextEndTurn = current.PendingEndTurn is { } endTurn
            ? SemanticEndTurnEngineV111.ContinueNestedAutomaticChoice(
                endTurn, result, choice, siteId)
            : null;
        var next = PublishRunContinuation(
            current, result, nextEndTurn,
            new SemanticPilotContinuationV111(
                "semantic_card_shortlist",
                CardShortlistDecision: decision.Fork()),
            preservePendingEndTurn: current.PendingEndTurn is null);
        Validate(next);
        return next;
    }

    public static SemanticPilotCombatV111 ResolvePoolShuffle(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int swapIndex)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic pool-shuffle fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.PoolShufflePending ??
            throw new InvalidOperationException(
                "No generated-card pool shuffle is pending.");
        var decision = SemanticPoolShuffleChoiceV111.Select(
            pending, eventId, swapIndex);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_pool_shuffle", PoolShuffleDecision: decision.Fork());
        var (result, siteId) = SemanticNestedChoiceResumeV111.Resume(
            current.Run, choice);
        var nextEndTurn = current.PendingEndTurn is { } endTurn
            ? SemanticEndTurnEngineV111.ContinueNestedAutomaticChoice(
                endTurn, result, choice, siteId)
            : null;
        var next = PublishRunContinuation(
            current, result, nextEndTurn,
            new SemanticPilotContinuationV111(
                "semantic_pool_shuffle",
                PoolShuffleDecision: decision.Fork()),
            preservePendingEndTurn: current.PendingEndTurn is null);
        Validate(next);
        return next;
    }

    public static SemanticPilotCombatV111 ResolveDrawInsertion(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int position)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw-insertion fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.DrawInsertionPending ??
            throw new InvalidOperationException(
                "No generated draw-pile insertion is pending.");
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Draw-insertion event ID is stale or mismatched.");
        if (current.PendingEndTurn is { } endTurn)
        {
            var priorHandInsertionCount =
                endTurn.HandDraw?.DrawInsertionTranscript.Length ?? 0;
            var priorNestedChoiceCount = endTurn.NestedChoiceTranscript.Length;
            var resolvedEndTurn = SemanticEndTurnEngineV111
                .ResolveDrawInsertion(
                    endTurn, endTurn.Fingerprint, eventId, position);
            var endTurnDecision = resolvedEndTurn.HandDraw is { } resolvedHand &&
                    resolvedHand.DrawInsertionTranscript.Length >
                        priorHandInsertionCount
                ? resolvedHand.DrawInsertionTranscript[^1]
                : resolvedEndTurn.NestedChoiceTranscript.Length >
                    priorNestedChoiceCount
                    ? resolvedEndTurn.NestedChoiceTranscript[^1]
                        .DrawInsertionDecision
                    : null;
            if (endTurnDecision is null)
                throw new InvalidDataException(
                    "End-turn draw insertion omitted its replay decision.");
            var resolvedSession = PublishRunContinuation(
                current, resolvedEndTurn.Run, resolvedEndTurn,
                new SemanticPilotContinuationV111(
                    "semantic_draw_insertion",
                    DrawInsertionDecision: endTurnDecision.Fork()));
            Validate(resolvedSession);
            return resolvedSession;
        }
        var decision = SemanticDrawInsertionChoiceV111.Select(
            pending, eventId, position);

        var priorCombat = current.Run.World!.Combat!;
        var sourceChoice = priorCombat.PendingChoice;
        var pendingInsertion = sourceChoice?
            .ControlledDrawInsertion ?? throw new InvalidDataException(
                "Draw-insertion choice lost its saved continuation.");
        var resumesAutomaticPipelineInsertion = sourceChoice is not null &&
            PrototypeGameEngine
                .IsAutomaticPipelineSemanticDrawInsertionContinuation(
                    priorCombat, sourceChoice);
        var resumesSemanticDraw =
            pendingInsertion.SemanticDrawContinuation is not null;
        var resumesAfterShuffleRelic =
            pendingInsertion.AfterShuffleRelic is not null;
        var insertedInstanceId = priorCombat.NextCardInstanceId;
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_draw_insertion",
            DrawInsertionDecision: decision.Fork());
        var (result, _) = SemanticNestedChoiceResumeV111.Resume(
            current.Run, choice);
        var resultCombat = result.World?.Combat;
        var expectedPile = priorCombat.DrawPile.ToList();
        expectedPile.Insert(position, insertedInstanceId);
        if ((result.Phase == RunPhase.Combat &&
             resultCombat?.Cards.SingleOrDefault(card =>
                card.InstanceId == insertedInstanceId)?.CardId !=
                pending.GeneratedCardModelId) ||
            (result.Phase == RunPhase.Combat && resultCombat is not
                { IsPlayerTurn: true }) ||
            (result.Phase == RunPhase.Combat &&
                resultCombat!.AutomaticPipelineContinuation is not null &&
                (!resumesAutomaticPipelineInsertion ||
                 !(resultCombat.PendingChoice is { } nextInsertionChoice &&
                   PrototypeGameEngine
                       .IsAutomaticPipelineSemanticDrawInsertionContinuation(
                           resultCombat, nextInsertionChoice)) &&
                 !PrototypeGameEngine
                     .HasPendingSupportedAutomaticPipelineSemanticChoice(
                         result))) ||
            (result.Phase != RunPhase.Combat &&
                (!resumesSemanticDraw && !resumesAfterShuffleRelic &&
                 !resumesAutomaticPipelineInsertion ||
                 !PrototypeGameEngine.IsAtCombatActionResultBoundary(
                    result))) ||
            (result.Phase == RunPhase.Combat && !resumesSemanticDraw &&
                !resumesAfterShuffleRelic &&
                !resumesAutomaticPipelineInsertion &&
                (resultCombat!.NextCardInstanceId != insertedInstanceId + 1 ||
                 resultCombat.PendingChoice is not null ||
                 !resultCombat.DrawPile.SequenceEqual(expectedPile))) ||
            (result.Phase == RunPhase.Combat && resumesSemanticDraw &&
                resultCombat!.NextCardInstanceId <= insertedInstanceId) ||
            (result.Phase == RunPhase.Combat && resumesSemanticDraw &&
                resultCombat!.PendingChoice?.ControlledDrawInsertion is not null &&
                !resultCombat.DrawPile.SequenceEqual(expectedPile)))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=" +
                StochasticSiteRegistryV111.DrawPileRandomInsertSiteId + " " +
                "operation=unmodeled_post_insertion_continuation");

        if (result.Phase == RunPhase.Combat && resumesSemanticDraw)
        {
            ValidateNatureDeckContinuationBoundary(
                resultCombat!, "stale_semantic_draw_continuation");
            ValidateSemanticCombatBoundary(result);
        }
        else if (result.Phase == RunPhase.Combat &&
                 resumesAutomaticPipelineInsertion)
        {
            ValidateNatureDeckContinuationBoundary(
                resultCombat!, "stale_automatic_insertion_continuation",
                StochasticSiteRegistryV111.DrawPileRandomInsertSiteId);
            if (resultCombat!.PendingChoice is { } followingInsertionChoice &&
                PrototypeGameEngine
                    .IsAutomaticPipelineSemanticDrawInsertionContinuation(
                        resultCombat, followingInsertionChoice) &&
                (resultCombat.NextCardInstanceId != insertedInstanceId + 1 ||
                 !resultCombat.DrawPile.SequenceEqual(expectedPile)))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=" +
                    StochasticSiteRegistryV111.DrawPileRandomInsertSiteId +
                    " operation=changed_automatic_insertion_prefix");
            ValidateSemanticCombatBoundary(result);
        }
        else if (result.Phase == RunPhase.Combat)
        {
            if (resumesAfterShuffleRelic)
            {
                ValidateNatureDeckContinuationBoundary(
                    resultCombat!, "stale_after_shuffle_continuation");
                if (resultCombat!.PendingChoice?.ControlledDrawInsertion is
                        { AfterShuffleRelic: not null })
                    SemanticDrawInsertionChoiceV111.ValidateBoundary(result);
                else if (resultCombat!.PendingChoice?.ControlledSemanticDraw is not null)
                    SemanticDrawChoiceV111.ValidateBoundary(result);
                else if (resultCombat.PendingChoice is
                         { ChoiceId: "controlled_draw_cost",
                           ControlledSemanticDraw: not null })
                    SemanticCostChoiceV111.ValidateDrawBoundary(result);
                else
                    ValidateSemanticCombatBoundary(result);
            }
        }
        var next = PublishRunContinuation(
            current, result, null,
            new SemanticPilotContinuationV111(
                "semantic_draw_insertion",
                DrawInsertionDecision: decision.Fork()),
            preservePendingEndTurn: result.Phase == RunPhase.Combat);
        Validate(next);
        return next;
    }

    private static SemanticPilotCombatV111 Resolve(
        SemanticPilotCombatV111 current, string fingerprint, int selectedIndex,
        SemanticIntentDecisionV111? semanticIntent,
        SemanticDrawDecisionV111? expectedDeckDecision,
        bool recordSemanticDraw,
        SemanticCostDecisionV111? expectedCostDecision,
        SemanticRandomPileCardDecisionV111? expectedRandomPileCardDecision = null,
        bool prevalidated = false,
        SemanticDeckEventV111? validatedDrawEvent = null,
        SemanticEndTurnV111? projectedEndTurn = null,
        bool trustedH8AutomaticDraw = false)
    {
        // Stable-ID resolvers already validate both the exact parent state and
        // caller fingerprint before constructing a choice. Avoid repeating the
        // full semantic invariant walk and canonical SHA-256 at this boundary.
        // The public numeric Resolve path still checks them here.
        if (!prevalidated)
        {
            current.Validate();
            if (current.Fingerprint != fingerprint)
                throw new InvalidOperationException("Stale semantic Nature fingerprint.");
        }
        // One mechanics/provider projection for this read-only source.
        var pendingEndTurn = projectedEndTurn ?? current.PendingEndTurn;
        if (pendingEndTurn?.DrawHookChoicePending is not null)
            throw new InvalidOperationException(
                "Resolve the pending CardDrawn hook action by action ID.");
        if (pendingEndTurn?.TargetPending is { } automaticTarget)
        {
            if (selectedIndex < 0 || selectedIndex >=
                    automaticTarget.CandidateEnemyInstanceIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            return ResolveRandomTarget(current, fingerprint,
                automaticTarget.EventId,
                automaticTarget.CandidateEnemyInstanceIds[selectedIndex]);
        }
        if (semanticIntent is not null)
        {
            var ticket = current.PendingInitialIntents ??
                pendingEndTurn?.Intents;
            if (ticket?.Pending is not { } request)
                throw new InvalidOperationException(
                    "Semantic intent decision has no matching pending event.");
            var support = ControlledTurnIntentEngineV111
                .DescribePendingSemanticIntent(ticket) ??
                throw new InvalidDataException(
                    "Missing weighted intent support.");
            var semanticEvent = SemanticIntentChoiceV111.CreateEvent(
                fingerprint, request, support);
            SemanticIntentChoiceV111.ValidateDecision(
                semanticEvent, semanticIntent, selectedIndex);
        }
        var drawEvent = validatedDrawEvent ?? current.DrawPending;
        var randomPileCardEvent = current.RandomPileCardPending;
        if (expectedDeckDecision is not null)
            SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_pilot_validate_choice",
                () => SemanticDrawChoiceV111.ValidateDecision(
                    drawEvent, expectedDeckDecision));
        if (expectedRandomPileCardDecision is not null)
            SemanticRandomPileCardEngineV111.ValidateDecision(
                randomPileCardEvent, expectedRandomPileCardDecision,
                selectedIndex);
        if (current.CardShortlistPending is not null)
            throw new InvalidOperationException(
                "Resolve the pending pile shortlist by card instance ID.");
        var costEvent = current.CostPending;
        if (current.HandCostSelectionPending is not null &&
            expectedCostDecision is null)
            throw new InvalidOperationException(
                "Resolve the pending hand-cost selection event by card instance ID.");
        if (expectedCostDecision is not null)
            SemanticCostChoiceV111.ValidateDecision(
                costEvent, expectedCostDecision, selectedIndex);
        if (current.PendingInitialIntents is { Pending: not null } initialIntents)
        {
            var resolvedInitial = ControlledTurnIntentEngineV111.Resolve(
                initialIntents, selectedIndex, current.Run.Rng);
            var nextRun = current.Run;
            ControlledTurnIntentsV111? pendingInitial = resolvedInitial.Complete
                ? null : resolvedInitial;
            if (resolvedInitial.Complete)
                nextRun = CompleteNatureInitialCombatEntry(
                    nextRun, resolvedInitial);
            var initialState = PublishRunContinuation(
                current, nextRun, null,
                NatureContinuation(selectedIndex, semanticIntent, null),
                preservePendingEndTurn: true) with
            {
                PendingInitialIntents = pendingInitial
            };
            Validate(initialState);
            return initialState;
        }
        if (current.Run.World?.Combat?.PendingChoice is
                { ChoiceId: "controlled_draw_cost",
                  ControlledDrawCost: not null,
                  ControlledSemanticDraw: not null })
        {
            if (costEvent is null || selectedIndex < 0 ||
                selectedIndex >= costEvent.CandidateCosts.Length)
                throw new InvalidDataException(
                    "Card-effect draw cost continuation lost its Nature event.");
            var effectCostDecision = expectedCostDecision ??
                SemanticCostChoiceV111.Select(
                    costEvent, costEvent.EventId,
                    costEvent.CandidateCosts[selectedIndex]);
            SemanticCostChoiceV111.ValidateDecision(
                costEvent, effectCostDecision, selectedIndex);
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_cost", CostDecision: effectCostDecision.Fork());
            var (effectCostResult, siteId) =
                SemanticNestedChoiceResumeV111.Resume(
                    current.Run, choice);
            var nextEndTurn = pendingEndTurn is { } costEndTurn
                ? SemanticEndTurnEngineV111.ContinueNestedAutomaticChoice(
                    costEndTurn, effectCostResult, choice, siteId)
                : null;
            var effectCostState = PublishRunContinuation(
                current, effectCostResult, nextEndTurn,
                new SemanticPilotContinuationV111(
                    "semantic_cost",
                    CostDecision: effectCostDecision.Fork()),
                preservePendingEndTurn: pendingEndTurn is null);
            Validate(effectCostState);
            return effectCostState;
        }
        if (current.Run.World?.Combat?.PendingChoice is
                { ChoiceId: "controlled_semantic_draw",
                  ControlledSemanticDraw: not null })
        {
            if (drawEvent is null)
                throw new InvalidDataException(
                    "Card-effect draw continuation lost its Nature event.");
            if (expectedDeckDecision is not null)
                SemanticDrawChoiceV111.ValidateDecision(
                    drawEvent, expectedDeckDecision);
            if (selectedIndex < 0 ||
                selectedIndex >= drawEvent.CandidateCardInstanceIds.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var effectDrawDecision = SemanticDrawChoiceV111.Select(
                drawEvent, selectedIndex);
            if (expectedDeckDecision is not null &&
                CanonicalJson.Sha256(expectedDeckDecision) !=
                    CanonicalJson.Sha256(effectDrawDecision))
                throw new InvalidOperationException(
                    "Semantic card-effect draw decision is stale or outside support.");
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_draw", DrawDecision: effectDrawDecision.Fork());
            var (effectDrawResult, siteId) =
                SemanticNestedChoiceResumeV111.Resume(
                    current.Run, choice);
            var nextEndTurn = pendingEndTurn is { } drawEndTurn
                ? SemanticEndTurnEngineV111.ContinueNestedAutomaticChoice(
                    drawEndTurn, effectDrawResult, choice, siteId)
                : null;
            var effectDrawState = PublishRunContinuation(
                current, effectDrawResult, nextEndTurn,
                new SemanticPilotContinuationV111(
                    "semantic_draw",
                    DrawDecision: effectDrawDecision.Fork()),
                preservePendingEndTurn: pendingEndTurn is null);
            Validate(effectDrawState);
            return effectDrawState;
        }
        var pending = pendingEndTurn ??
            throw new InvalidOperationException("No Nature decision is pending.");
        if (pending.IntentPending is null && pending.DrawPending is null &&
            pending.RandomPileCardPending is null && pending.CostPending is null &&
            pending.EnemyActionPending is null)
            throw new InvalidOperationException(
                "No semantic enemy intent, draw or cost choice is pending.");
        var resolved = SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_endturn_resolve",
            () => trustedH8AutomaticDraw
                ? SemanticEndTurnEngineV111.ResolveFromH8DrawCursor(
                    pending, selectedIndex)
                : SemanticEndTurnEngineV111.ResolveFromValidatedPilot(
                    pending, selectedIndex));
        SemanticDrawDecisionV111? deckDecision = null;
        if (drawEvent is not null)
        {
            deckDecision = SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_pilot_recorded_choice",
                () => SemanticDrawChoiceV111.ValidateRecordedChoice(
                    resolved.Run, drawEvent, selectedIndex));
        }
        if (expectedDeckDecision is not null &&
            CanonicalJson.Sha256(expectedDeckDecision) !=
                CanonicalJson.Sha256(deckDecision))
            throw new InvalidOperationException(
                "Semantic card decision is stale or outside current support.");
        SemanticCostDecisionV111? costDecision = null;
        if (costEvent is not null)
        {
            var transcript = resolved.HandDraw?.CostTranscript ?? [];
            var recorded = transcript.LastOrDefault(decision =>
                CanonicalJson.Sha256(decision.Request) ==
                CanonicalJson.Sha256(costEvent.Request)) ??
                throw new InvalidDataException(
                    "Semantic draw cost did not record its source event.");
            if (recorded.SelectedIndex != selectedIndex)
                throw new InvalidDataException(
                    "Semantic draw cost selected a different bounded value.");
            costDecision = new SemanticCostDecisionV111(
                costEvent, recorded.SelectedIndex,
                costEvent.CandidateCosts[recorded.SelectedIndex]);
            costDecision.Validate();
        }
        if (expectedCostDecision is not null &&
            CanonicalJson.Sha256(expectedCostDecision) !=
                CanonicalJson.Sha256(costDecision))
            throw new InvalidOperationException(
                "Semantic cost decision is stale or outside current support.");
        SemanticRandomPileCardDecisionV111? randomPileCardDecision = null;
        if (randomPileCardEvent is not null)
        {
            var transcript = resolved.HandDraw?.RandomPileCardTranscript ?? [];
            randomPileCardDecision = transcript.LastOrDefault(decision =>
                decision.Event.EventId == randomPileCardEvent.EventId) ??
                throw new InvalidDataException(
                    "Random pile-card selection did not record its event.");
            if (CanonicalJson.Sha256(randomPileCardDecision.Event) !=
                    CanonicalJson.Sha256(randomPileCardEvent) ||
                randomPileCardDecision.SelectedIndex != selectedIndex)
                throw new InvalidDataException(
                    "Random pile-card selection diverged from its support.");
        }
        if (expectedRandomPileCardDecision is not null &&
            CanonicalJson.Sha256(expectedRandomPileCardDecision) !=
                CanonicalJson.Sha256(randomPileCardDecision))
            throw new InvalidOperationException(
                "Semantic random pile-card decision differs from replay.");
        var next = SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_pilot_publish",
            () => PublishRunContinuation(
                current, resolved.Run, resolved,
                NatureContinuation(
                    selectedIndex, semanticIntent,
                    recordSemanticDraw ? deckDecision : null,
                    costDecision, randomPileCardDecision)));
        return SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_pilot_normalize",
            () => NormalizeNoncontrolledCosts(
                next, auditOutput: !trustedH8AutomaticDraw));
    }

    /// <summary>
    /// Resolve one source-filtered weighted semantic intent by its stable
    /// candidate ID, translating it into the first legal native-shaped
    /// ticket of the corresponding weight interval. Old numeric ticket
    /// resolution stays available for protocol compatibility.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveIntent(
        SemanticPilotCombatV111 current, string fingerprint, string intentId)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic intent fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var ticket = current.PendingInitialIntents ??
            current.PendingEndTurn?.Intents;
        if (ticket?.Pending is null)
            throw new InvalidOperationException(
                "Only a pending enemy_intent supports semantic intent IDs.");
        var support = ControlledTurnIntentEngineV111
            .DescribePendingSemanticIntent(ticket) ??
            throw new InvalidDataException("Missing weighted intent support.");
        var semanticEvent = SemanticIntentChoiceV111.CreateEvent(
            current.Fingerprint, ticket.Pending, support);
        var selection = SemanticIntentChoiceV111.Select(
            semanticEvent, intentId);
        return Resolve(current, fingerprint, selection.SourceTicket,
            selection.Decision, null, false, null, prevalidated: true);
    }

    /// <summary>
    /// H8 bridge for a version-owned source intent request. The session has
    /// already checked its opaque boundary version and intent support.
    /// Retain the legacy event fingerprint solely as transcript data during
    /// migration; skip the complete source-pilot input invariant walk.
    /// The underlying typed intent provider and deterministic turn effects
    /// follow the same mechanics as the checked public resolver.
    /// </summary>
    internal static SemanticPilotCombatV111 ResolveIntentFromH8TrustedBoundary(
        SemanticPilotCombatV111 current, string intentId,
        string? boundaryFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var ticket = current.PendingInitialIntents ??
            current.PendingEndTurn?.Intents;
        if (ticket?.Pending is not { } pending)
            throw new InvalidOperationException(
                "H8 intent requires an active source intent request.");
        var support = ControlledTurnIntentEngineV111
            .DescribePendingSemanticIntent(ticket) ??
            throw new InvalidDataException("H8 intent has no weighted support.");
        // When H8 already exposed this exact version-owned reference law,
        // its pre-choice fingerprint is identical. Do not hash the entire
        // mechanics/transcript twice for one intent provider decision.
        var fingerprint = boundaryFingerprint ?? current.Fingerprint;
        var semanticEvent = SemanticIntentChoiceV111.CreateEvent(
            fingerprint, pending, support);
        var selection = SemanticIntentChoiceV111.Select(
            semanticEvent, intentId);
        using (H8HotPathProfilerV1.Begin("h8.intent.v111_resolution"))
            return Resolve(current, fingerprint, selection.SourceTicket,
                selection.Decision, null, false, null, prevalidated: true);
    }

    /// <summary>
    /// Resolve one deferred draw by its stable semantic event and physical
    /// card-instance identity. The source-compatible numeric resolver stays
    /// available, but semantic consumers need not interpret an index.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveDrawCard(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, long cardInstanceId)
    {
        SemanticDeckEventV111 pending;
        SemanticDrawDecisionV111 decision;
        // This block only inspects the input snapshot. End the read-only
        // lease before any provider choice is committed or mechanics advance.
        using (CanonicalJson.BeginReadOnlySemanticSnapshotHashReuse())
        {
            SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_pilot_input_validate", current.Validate);
            var parentFingerprint = SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_pilot_input_fingerprint", () => current.Fingerprint);
            if (parentFingerprint != fingerprint)
                throw new InvalidOperationException(
                    "Stale semantic draw fingerprint.");
            ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
            pending = current.DrawPending ??
                throw new InvalidOperationException(
                    "Only a pending semantic draw supports card-instance resolution.");
            decision = SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_pilot_select",
                () => SemanticDrawChoiceV111.Select(pending, eventId, cardInstanceId));
        }
        return SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_pilot_resolve",
            () => Resolve(current, fingerprint, decision.SelectedIndex,
                null, decision, true, null, prevalidated: true,
                validatedDrawEvent: pending));
    }

    /// <summary>
    /// H8 bridge: the owning runtime has established the current boundary
    /// version and is the only writer of this private in-memory session.
    /// Validate the selected physical identity against the live provider-owned
    /// support, then resume the exact legacy mechanics without recomputing a
    /// whole-pilot input hash or running a full input audit. Existing output
    /// checks remain in place until H8 owns the continuation cursor.
    ///
    /// Internal only: NEVER expose this directly to a caller that can submit
    /// a stale snapshot or untrusted event fingerprint.
    /// </summary>
    internal static SemanticPilotCombatV111 ResolveFromH8TrustedDrawBoundary(
        SemanticPilotCombatV111 current, long cardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(current);
        var pending = current.DrawPending ??
            throw new InvalidOperationException(
                "H8 draw requires an active controlled draw-card boundary.");
        var eligible = pending.CandidateCardInstanceIds;
        var selectedIndex = Array.IndexOf(eligible, cardInstanceId);
        if (selectedIndex < 0)
            throw new InvalidOperationException(
                "H8 card instance is outside the pending Nature support.");
        var decision = SemanticDrawChoiceV111.Select(
            pending, pending.EventId, cardInstanceId);
        return Resolve(current, string.Empty, decision.SelectedIndex,
            null, decision, true, null, prevalidated: true,
            validatedDrawEvent: pending);
    }

    /// <summary>
    /// M2 experimental path: a session-owned H8 cursor has captured both the
    /// actual end-turn stage and physical Nature support for this exact
    /// boundary version. Resolves only ordinary automatic hand draws; any
    /// other event must use the checked compatibility mechanics route. Full
    /// recursive output audits are deferred to explicit H8 journal replay.
    /// </summary>
    internal static SemanticPilotCombatV111 ResolveFromH8AutomaticDrawCursor(
        SemanticPilotCombatV111 current,
        SemanticEndTurnV111 endTurn,
        SemanticDeckEventV111 drawEvent,
        int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(endTurn);
        ArgumentNullException.ThrowIfNull(drawEvent);
        if (endTurn.HandDraw is not { AtBoundary: false, Pending: not null } ||
            current.Run.World?.Combat?.PendingChoice is
                { ChoiceId: "controlled_semantic_draw",
                  ControlledSemanticDraw: not null } ||
            drawEvent.Purpose != "draw" ||
            selectedIndex < 0 ||
            selectedIndex >= drawEvent.CandidateCardInstanceIds.Length)
            throw new InvalidOperationException(
                "H8 fast draw requires a supported automatic hand-draw cursor.");
        // The selected physical card ID was checked against this exact
        // session's live support before entering the resolver. We still
        // validate the recorded decision produced by the mechanics engine.
        return Resolve(current, string.Empty, selectedIndex,
            null, null, true, null, prevalidated: true,
            validatedDrawEvent: drawEvent, projectedEndTurn: endTurn,
            trustedH8AutomaticDraw: true);
    }

    /// <summary>
    /// Temporary v111 snapshot/journal publication bridge for H8's direct
    /// mechanics executor. H8 owns the physics and chance selection; this
    /// adapter only retains the compatibility transcript at the boundary.
    /// It can disappear once H8 checkpointing replaces legacy pilot records.
    /// </summary>
    internal static SemanticPilotCombatV111 PublishH8AdvancedDraw(
        SemanticPilotCombatV111 current,
        SemanticEndTurnV111 completedHandProgress,
        int selectedIndex,
        SemanticDrawDecisionV111 recordedChoice)
    {
        var next = PublishRunContinuation(
            current, completedHandProgress.Run, completedHandProgress,
            NatureContinuation(selectedIndex, null, recordedChoice));
        return NormalizeNoncontrolledCosts(next, auditOutput: false);
    }

    /// <summary>
    /// H8-only publication of an already executed deterministic player
    /// end-turn. The versioned H8 session and native end-turn executor own
    /// all legality and mechanics; this preserves legacy v111 history for
    /// explicit replay without running a complete pilot audit at each step.
    /// </summary>
    internal static SemanticPilotCombatV111 PublishH8PlayerEndTurn(
        SemanticPilotCombatV111 current,
        SemanticEndTurnV111? endTurn,
        RunState result,
        string actionId)
    {
        var next = PublishRunContinuation(
            current, result, endTurn,
            new SemanticPilotContinuationV111("player", actionId));
        return NormalizeNoncontrolledCosts(next, auditOutput: false);
    }

    /// <summary>
    /// Resolve one deferred peek or scry by physical card identity. The
    /// provider records the revealed position while draw-pile membership and
    /// order remain unchanged.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveDeckReveal(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, long cardInstanceId)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic deck-reveal fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.DeckRevealPending ??
            throw new InvalidOperationException(
                "No semantic peek or scry reveal is pending.");
        var decision = SemanticDeckRevealChoiceV111.Select(
            pending, eventId, cardInstanceId);
        var nextRun = SemanticDeckRevealChoiceV111.Resume(
            current.Run, decision);
        var next = PublishRunContinuation(
            current, nextRun, null,
            new SemanticPilotContinuationV111(
                "semantic_deck_reveal",
                DeckRevealDecision: decision.Fork()),
            preservePendingEndTurn: true);
        Validate(next);
        return next;
    }

    /// <summary>
    /// Stable event identity for the current Nature-facing weighted intent
    /// support. It is bound to the exact pre-choice state and eligible pool.
    /// </summary>
    public static SemanticIntentEventV111 DescribePendingIntentEvent(
        SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return DescribePendingIntentEvent(current, current.Fingerprint);
    }

    internal static SemanticIntentEventV111 DescribePendingIntentEvent(
        SemanticPilotCombatV111 current, string boundaryFingerprint)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        var ticket = current.PendingInitialIntents ??
            current.PendingEndTurn?.Intents;
        if (ticket?.Pending is not { } request)
            throw new InvalidOperationException(
                "No semantic intent event is pending.");
        var support = ControlledTurnIntentEngineV111
            .DescribePendingSemanticIntent(ticket) ??
            throw new InvalidDataException("Missing weighted intent support.");
        return SemanticIntentChoiceV111.CreateEvent(
            boundaryFingerprint, request, support);
    }

    public static SemanticTargetEventV111? DescribePendingTargetEvent(
        SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn is { } endTurn)
            return endTurn.TargetPending;
        return DescribePendingTargetEvent(current.Run, current.Fingerprint,
            current.HistoryOrdinal("semantic_target"));
    }

    internal static SemanticTargetEventV111? DescribePendingTargetEvent(
        RunState run, string boundaryFingerprint, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var target = choice?.ControlledTarget;
        if (target is null) return null;
        SemanticTargetChoiceV111.ValidateBoundary(run);
        var ownerId = target.AutoPlayCardInstanceId is { } autoPlayCardId
            ? "card_instance:" + autoPlayCardId
            : target.SlyCardInstanceId is { } slyCardId
            ? "card_instance:" + slyCardId
            : choice!.SourceCardInstanceId is { } sourceCardId
                ? "card_instance:" + sourceCardId
                : "effect_operation";
        return SemanticTargetChoiceV111.CreateEvent(
            boundaryFingerprint, target.Kind, ownerId, ordinal,
            target.EligibleEnemyInstanceIds);
    }

    public static SemanticGeneratedCardEventV111?
        DescribePendingGeneratedCardEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn?.GeneratedCardPending is { } endTurnEvent)
            return endTurnEvent;
        var combat = current.Run.World?.Combat;
        var choice = combat?.PendingChoice;
        var generation = choice?.ControlledGeneration;
        var drawInsertion = choice?.ControlledDrawInsertion;
        var isDrawInsertionModelChoice = generation is null &&
            drawInsertion is { Stage: "card_model" };
        if (generation is null && !isDrawInsertionModelChoice) return null;
        if (isDrawInsertionModelChoice)
        {
            if (current.SeededEntry?.FixtureRandomFreeAttackDrawInsertion != true &&
                combat?.AutomaticPipelineContinuation is null)
                throw new InvalidDataException(
                    "Random draw-Attack model choice lacks a supported source boundary.");
            SemanticDrawInsertionChoiceV111.ValidateBoundary(current.Run);
        }
        var ordinal = current.HistoryOrdinal("semantic_generated_card");
        if (generation is not null)
            return SemanticGenerationChoiceV111.DescribePending(
                current.Run, current.Fingerprint, ordinal);
        var candidates = (string[])(generation?.CandidateCardIds ??
            drawInsertion!.CandidateCardIds).Clone();
        var siteId = isDrawInsertionModelChoice
            ? "combat.random_draw_attack.card_model"
            : throw new InvalidDataException(
                "Missing semantic generated-card source operation.");
        var operation = drawInsertion!.Operation.Kind;
        var ownerId = choice!.SourceCardInstanceId is { } sourceCardId
            ? "card_instance:" + sourceCardId
            : "effect:" + operation;
        return SemanticGenerationChoiceV111.CreateEvent(
            current.Fingerprint, siteId, ownerId, ordinal, candidates);
    }

    public static SemanticPotionGenerationEventV111?
        DescribePendingPotionGenerationEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn?.PotionGenerationPending is
            { } endTurnEvent)
            return endTurnEvent;
        var combat = current.Run.World?.Combat;
        var generation = combat?.PendingChoice?.ControlledPotionGeneration;
        if (generation is null) return null;
        var ordinal = current.HistoryOrdinal("semantic_potion_generation");
        return SemanticPotionGenerationChoiceV111.DescribePending(
            current.Run, current.Fingerprint, ordinal);
    }

    public static SemanticCardTransformEventV111?
        DescribePendingCardTransformEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn?.CardTransformPending is
            { } endTurnEvent)
            return endTurnEvent;
        var combat = current.Run.World?.Combat;
        var choice = combat?.PendingChoice;
        var transform = choice?.ControlledCardTransform;
        if (transform is null) return null;
        if ((current.PendingEndTurn is not null &&
             current.PendingEndTurn.CardTransformPending is null) ||
            current.PendingInitialIntents is not null ||
            current.UsesNativePolicyBaseline || current.CombatTerminal)
            throw new InvalidDataException(
                "Card-transform choice conflicts with another semantic boundary.");
        var ordinal = current.HistoryOrdinal("semantic_card_transform");
        return SemanticCardTransformChoiceV111.DescribePending(
            current.Run, current.Fingerprint, ordinal);
    }

    public static SemanticCardSelectionEventV111?
        DescribePendingCardSelectionEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn?.NestedCardSelectionPending is
            { } endTurnSelection)
            return endTurnSelection;
        if (current.Run.World?.Combat?.PendingChoice?.ChoiceId != "select_cards")
            return null;
        if ((current.PendingEndTurn is not null &&
             current.PendingEndTurn.NestedCardSelectionPending is null) ||
            current.PendingInitialIntents is not null ||
            current.TargetPending is not null ||
            current.GeneratedCardPending is not null ||
            current.UsesNativePolicyBaseline || current.CombatTerminal)
            throw new InvalidDataException(
                "Source card selection conflicts with another semantic boundary.");
        return SemanticCardSelectionEngineV111.DescribePending(
            current.Run, CanonicalJson.Sha256(current.Run));
    }

    public static SemanticCostEventV111? DescribePendingCostEvent(
        SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn?.NestedCostPending is { } endTurnCost)
            return endTurnCost;
        RandomIndexRequestV111 request;
        long? targetCardInstanceId = null;
        string? targetCardModelId = null;
        if (current.PendingEndTurn?.CostPending is { } drawCostRequest &&
            current.SeededEntry?.FixtureNatureDrawCosts == true)
            request = drawCostRequest;
        else if (current.Run.World?.Combat?.PendingChoice?
                    .ControlledRandomCost is { } randomCost &&
                randomCost.Operation.Kind == PrototypeCombatEffectKind
                    .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed)
        {
            SemanticRandomHandCostChoiceV111.ValidateBoundary(current.Run);
            if (current.SeededEntry?.FixtureRandomizeHandCostsMaxExclusive is
                    { } fixtureMaxExclusive &&
                randomCost.Operation.Amount != fixtureMaxExclusive)
                throw new InvalidDataException(
                    "Random hand-cost operation differs from fixture provenance.");
            var maxExclusive = Math.Max(1, randomCost.Operation.Amount);
            var combat = current.Run.World!.Combat!;
            targetCardInstanceId = randomCost.EligibleCardInstanceIds[
                randomCost.NextCardIndex];
            targetCardModelId = combat.Cards.Single(card =>
                card.InstanceId == targetCardInstanceId).CardId;
            request = new RandomIndexRequestV111(
                "combat.random_hand_card.energy_cost", "prototype.combat",
                "effect:" + randomCost.Operation.Kind +
                    ":hand:" + CanonicalJson.Sha256(
                        randomCost.EligibleCardInstanceIds) +
                    ":index:" + randomCost.NextCardIndex,
                current.HistoryOrdinal("semantic_cost"), [],
                OperationKind: "bounded_integer",
                BoundedCount: maxExclusive);
        }
        else if (current.Run.World?.Combat?.PendingChoice is
                    { ChoiceId: "controlled_draw_cost",
                      ControlledDrawCost: not null,
                      ControlledSemanticDraw: not null })
        {
            if (current.PendingEndTurn?.NestedCostPending is { } nestedCost)
                return nestedCost;
            return SemanticCostChoiceV111.DescribeEffectDrawEvent(
                current.Run, current.Fingerprint,
                current.HistoryOrdinal("semantic_cost"));
        }
        else
            return null;
        return SemanticCostChoiceV111.CreateEvent(
            current.Fingerprint, request, targetCardInstanceId,
            targetCardModelId);
    }

    public static SemanticHandCostSelectionEventV111?
        DescribePendingHandCostSelectionEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn?.NestedHandCostSelectionPending is
            { } endTurnSelection)
            return endTurnSelection;
        var combat = current.Run.World?.Combat;
        var randomCost = combat?.PendingChoice?.ControlledRandomCost;
        if (randomCost is null ||
            randomCost.Operation.Kind != PrototypeCombatEffectKind
                .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed)
            return null;
        SemanticRandomHandCostChoiceV111.ValidateBoundary(current.Run);
        if ((current.SeededEntry?.FixtureSetRandomHandCardCost is
                { } fixtureCost &&
             randomCost.Operation.Amount != fixtureCost) ||
            current.PendingEndTurn is not null ||
            current.PendingInitialIntents is not null ||
            current.CostPending is not null || current.UsesNativePolicyBaseline ||
            current.CombatTerminal)
            throw new InvalidDataException(
                "Random hand-card selection conflicts with another boundary.");
        var modelIds = randomCost.EligibleCardInstanceIds.Select(id =>
            combat!.Cards.Single(card =>
            card.InstanceId == id).CardId).ToArray();
        var ordinal = current.HistoryOrdinal("semantic_hand_cost_selection");
        return SemanticHandCostSelectionChoiceV111.CreateEvent(
            current.Fingerprint, randomCost, modelIds, ordinal);
    }

    public static SemanticCardShortlistEventV111?
        DescribePendingCardShortlistEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn?.CardShortlistPending is
            { } endTurnEvent)
            return endTurnEvent;
        var ordinal = current.HistoryOrdinal("semantic_card_shortlist");
        return SemanticCardShortlistChoiceV111.DescribePending(
            current.Run, current.Fingerprint, ordinal);
    }

    public static SemanticPoolShuffleEventV111?
        DescribePendingPoolShuffleEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn?.PoolShufflePending is
            { } endTurnEvent)
            return endTurnEvent;
        var combat = current.Run.World?.Combat;
        var choice = combat?.PendingChoice;
        var shuffle = choice?.ControlledPoolShuffle;
        if (shuffle is null) return null;
        bool randomRareToHand = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand;
        bool randomShortlist = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.ChooseCards;
        bool handReturn = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle;
        bool recycle = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw;
        bool mayhemRecycle = recycle && combat is not null && choice is not null &&
            PrototypeGameEngine.IsMayhemLegacyRecycleContinuation(
                combat, choice);
        bool automaticPipelinePoolShuffle = combat is not null &&
            choice is not null && PrototypeGameEngine
                .IsAutomaticPipelineSemanticPoolShuffleContinuation(
                    combat, choice);
        var fixtureMatches = automaticPipelinePoolShuffle || handReturn ||
            recycle || (randomRareToHand
            ? current.SeededEntry?.FixtureSelectionCardId ==
                "proto.colorless.anointed"
            : randomShortlist
                ? current.SeededEntry?.FixtureSelectionCardId is not null
                : current.SeededEntry?.FixturePoolGenerationPotionId ==
                    "proto.potion.colorless");
        if (!fixtureMatches ||
            (current.PendingEndTurn is not null && !mayhemRecycle) ||
            current.PendingInitialIntents is not null ||
            current.TargetPending is not null ||
            current.GeneratedCardPending is not null ||
            current.CardSelectionPending is not null ||
            current.UsesNativePolicyBaseline || current.CombatTerminal)
            throw new InvalidDataException(
                "Full-pool shuffle conflicts with another semantic boundary.");
        var ordinal = current.HistoryOrdinal("semantic_pool_shuffle");
        return SemanticPoolShuffleChoiceV111.DescribePending(
            current.Run, current.Fingerprint, ordinal);
    }

    public static SemanticDrawInsertionEventV111?
        DescribePendingDrawInsertionEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var combat = current.Run.World?.Combat;
        var choice = combat?.PendingChoice;
        var insertion = choice?.ControlledDrawInsertion;
        if (insertion is null) return null;
        var automaticPipelineInsertion = combat is not null && choice is not null &&
            PrototypeGameEngine
                .IsAutomaticPipelineSemanticDrawInsertionContinuation(
                    combat, choice);
        if (insertion.Stage != "position") return null;
        var fixedFixtureMatches =
            insertion.Operation.Kind == PrototypeCombatEffectKind
                .CreateGeneratedCardsAtRandomDrawPositions &&
            current.SeededEntry?.FixtureGeneratedDrawInsertionCardId ==
                insertion.Operation.CardId &&
            current.SeededEntry?.FixtureRandomFreeAttackDrawInsertion != true;
        var randomFixtureMatches =
            insertion.Operation.Kind == PrototypeCombatEffectKind
                .CreateRandomFreeAttacksInDrawPile &&
            current.SeededEntry?.FixtureGeneratedDrawInsertionCardId is null &&
            current.SeededEntry?.FixtureRandomFreeAttackDrawInsertion == true;
        var afterShuffleRelicMatches =
            insertion.AfterShuffleRelic is not null &&
            insertion.Operation.Kind == PrototypeCombatEffectKind
                .CreateGeneratedCardsAtRandomDrawPositions &&
            current.SeededEntry?.FixtureGeneratedDrawInsertionCardId is null &&
            current.SeededEntry?.FixtureRandomFreeAttackDrawInsertion != true;
        var personalHiveInsertion =
            insertion.RequestSiteId ==
                PrototypeControlledDrawInsertionV111.PersonalHiveDazedSiteId &&
            insertion.Operation.Kind == PrototypeCombatEffectKind
                .CreateGeneratedCardsAtRandomDrawPositions &&
            insertion.Operation.CardId == "proto.status.dazed";
        if ((!fixedFixtureMatches && !randomFixtureMatches &&
             !afterShuffleRelicMatches && !automaticPipelineInsertion &&
             !personalHiveInsertion) ||
            current.PendingEndTurn is not null ||
            current.PendingInitialIntents is not null ||
            current.TargetPending is not null ||
            current.GeneratedCardPending is not null ||
            current.CardSelectionPending is not null ||
            current.PoolShufflePending is not null ||
            current.UsesNativePolicyBaseline || current.CombatTerminal)
            throw new InvalidDataException(
                "Draw insertion conflicts with another semantic boundary.");
        var requestOrdinal = afterShuffleRelicMatches || personalHiveInsertion
            ? insertion.RequestOrdinal
            : current.HistoryOrdinal("semantic_draw_insertion");
        var nativeOwnerPrefix = afterShuffleRelicMatches || personalHiveInsertion
            ? insertion.RequestOwnerId
            : choice!.SourceCardInstanceId is { } sourceCardInstanceId
                ? "card_instance:" + sourceCardInstanceId
                : "effect:" + insertion.Operation.Kind;
        return SemanticDrawInsertionChoiceV111.DescribePendingEvent(
            current.Run, current.Fingerprint, requestOrdinal,
            insertion.RequestOwnerId, nativeOwnerPrefix,
            appendPileIdentityToLegacyOwner:
                afterShuffleRelicMatches || personalHiveInsertion,
            appendPileIdentityToNativeOwner: true);
    }

    internal static void ValidateSemanticCombatBoundary(RunState run)
    {
        var choice = run.World?.Combat?.PendingChoice;
        if (choice?.ControlledCardTransform is { } cardTransform)
        {
            SemanticCardTransformChoiceV111.ValidateBoundary(
                run, cardTransform);
            return;
        }
        if (choice?.ChoiceId == "select_cards")
        {
            SemanticCardSelectionEngineV111.ValidatePendingBoundary(run);
            return;
        }
        if (choice?.ChoiceId == "controlled_semantic_draw")
        {
            SemanticDrawChoiceV111.ValidateBoundary(run);
            return;
        }
        if (choice is
            { ChoiceId: "controlled_draw_cost",
              ControlledDrawCost: not null,
              ControlledSemanticDraw: not null })
        {
            SemanticCostChoiceV111.ValidateDrawBoundary(run);
            return;
        }
        if (choice?.ControlledTarget is not null)
        {
            SemanticTargetChoiceV111.ValidateBoundary(run);
            return;
        }
        if (choice?.ControlledGeneration is not null)
        {
            SemanticGenerationChoiceV111.ValidateBoundary(run);
            return;
        }
        if (choice?.ControlledPotionGeneration is not null)
        {
            SemanticPotionGenerationChoiceV111.ValidateBoundary(run);
            return;
        }
        if (choice?.ControlledDrawInsertion is not null)
        {
            SemanticDrawInsertionChoiceV111.ValidateBoundary(run);
            return;
        }
        if (choice?.ControlledRandomCost is not null)
        {
            SemanticRandomHandCostChoiceV111.ValidateBoundary(run);
            return;
        }
        if (choice?.ControlledCardShortlist is not null)
        {
            SemanticCardShortlistChoiceV111.ValidateBoundary(run);
            return;
        }
        SemanticPlayerActionEngineV111.ValidateBoundary(run);
    }

    private static void ValidateNatureDeckContinuationBoundary(
        CombatState combat, string operation,
        string siteId = "combat.relic.after_shuffle")
    {
        var message = "UNSUPPORTED_SEMANTIC_DECK " +
            "site=" + siteId + " operation=" + operation;
        try
        {
            SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
                combat, message, message, allowPending: true);
        }
        catch (InvalidDataException exception)
        {
            throw new NotSupportedException(message, exception);
        }
    }

    private static SemanticPilotContinuationV111 NatureContinuation(
        int selectedIndex, SemanticIntentDecisionV111? semanticIntent,
        SemanticDrawDecisionV111? deckDecision,
        SemanticCostDecisionV111? costDecision = null,
        SemanticRandomPileCardDecisionV111? randomPileCardDecision = null) =>
        costDecision is not null
            ? new SemanticPilotContinuationV111("semantic_cost",
                CostDecision: costDecision.Fork())
            : randomPileCardDecision is not null
            ? new SemanticPilotContinuationV111("semantic_random_pile_card",
                RandomPileCardDecision: randomPileCardDecision.Fork())
            : semanticIntent is not null
            ? new SemanticPilotContinuationV111("semantic_intent",
                IntentDecision: semanticIntent)
            : deckDecision is not null
                ? new SemanticPilotContinuationV111("semantic_draw",
                    DrawDecision: deckDecision.Fork())
                : new SemanticPilotContinuationV111("nature",
                    SelectedIndex: selectedIndex);

    private static RunState CompleteNatureInitialCombatEntry(
        RunState checkpoint, ControlledTurnIntentsV111 intents)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(intents);
        if (!intents.Complete)
            throw new InvalidOperationException(
                "Initial combat intents are still pending.");
        intents.Validate();
        RunState resumed;
        using (ControlledChanceGuardV111.Enter())
            resumed = PrototypeGameEngine.ResumeSemanticInitialCombatEntry(
                checkpoint, intents.Combat);

        var world = resumed.World ?? throw new InvalidDataException(
            "Initial intent completion lost the source route world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Initial intent completion lost combat.");
        if (!PrototypeGameEngine
                .IsAtCombatPlayerDecisionBoundary(resumed))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_ROUTE site=combat.opening " +
                "operation=unmodeled_opening_continuation");

        var semanticCombat = SemanticDrawPileAdapterV111
            .InitializeFromCommittedOpening(combat);
        var result = resumed with
        {
            World = world with { Combat = semanticCombat }
        };
        PrototypeStateInvariants.Validate(result);
        return result;
    }

    /// <summary>
    /// The initial two-site pilot does not optimize drawn-card costs.
    /// Bound and source hook are taken from the actual semantic cost
    /// continuation, but sampling is an explicitly declared engine-owned
    /// deterministic fallback, NOT an audited native RNG distribution.
    /// </summary>
    private static SemanticPilotCombatV111 NormalizeNoncontrolledCosts(
        SemanticPilotCombatV111 current, bool auditOutput = true)
    {
        if (current.SeededEntry?.FixtureNatureDrawCosts == true)
            return current;
        while (current.PendingEndTurn is { CostPending: { } cost } endTurn)
        {
            var bound = cost.SupportCount;
            if (bound < 1)
                throw new InvalidDataException("Invalid engine-owned cost bound.");
            SemanticOtherChanceStateV111? nextOther = null;
            int index;
            if (current.OtherChance is { } other)
            {
                var choice = SemanticOtherChanceEngineV111.ResolveDrawCost(
                    other, cost);
                nextOther = choice.Next;
                index = choice.Decision.SelectedIndex;
            }
            else
            {
                // Preserve pre-existing p0-v1 sessions unless the caller
                // explicitly negotiates the named native-codec provider.
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
                    current.OpeningSource.Seed + ":" +
                    endTurn.Fingerprint + ":" + cost.SiteId));
                index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)bound);
            }
            var resolved = SemanticEndTurnEngineV111.Resolve(
                endTurn, endTurn.Fingerprint, index);
            current = PublishRunContinuation(
                current, resolved.Run, resolved, otherChance: nextOther);
        }
        if (auditOutput)
            current.Validate();
        return current;
    }

    private static SemanticPilotCombatV111 PublishRunContinuation(
        SemanticPilotCombatV111 current,
        RunState run,
        SemanticEndTurnV111? pendingEndTurn,
        SemanticPilotContinuationV111? historyEntry = null,
        SemanticOtherChanceStateV111? otherChance = null,
        bool preservePendingEndTurn = false)
    {
        using var scope = H8HotPathProfilerV1.Begin("h8.continuation.publish");
        var next = current with
        {
            Run = run,
            RewardAudit = run.Phase == RunPhase.Reward
                ? RecordRewardAudit(current.Run, run)
                : current.RewardAudit,
            OtherChance = otherChance ?? current.OtherChance
        };
        if (!preservePendingEndTurn)
            next = next with
            {
                PendingEndTurn = pendingEndTurn is { Complete: false }
                    ? pendingEndTurn
                    : null
            };
        if (historyEntry is null)
            return next;
        // A source-valid H8 session maintains event ordinals in its
        // immutable trail. Preserve legacy v111's growing History array
        // only for ordinary v111 sessions and explicit materialization.
        if (next.H8HistoryTrail is { } trail)
            return next with { H8HistoryTrail = trail.Append(historyEntry) };
        return next with
        {
            History = next.History.Append(historyEntry).ToArray()
        };
    }

    public static void Validate(SemanticPilotCombatV111 state) =>
        state.Validate();

    public static string Save(SemanticPilotCombatV111 state)
    {
        state.Validate();
        return JsonSerializer.Serialize(state);
    }

    public static SemanticPilotCombatV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var saved = JsonSerializer.Deserialize<SemanticPilotCombatV111>(json)
            ?? throw new InvalidDataException("Null semantic playable snapshot.");
        Validate(saved);
        var replay = saved.SeededEntry is { } entry
            ? BeginSeeded(entry)
            : Begin(saved.OpeningSource);
        foreach (var transition in saved.History)
        {
            replay = transition.Kind switch
            {
                "player" when transition.ActionId is not null =>
                    Step(replay, replay.Fingerprint, transition.ActionId),
                "semantic_intent" when transition.IntentDecision is not null =>
                    ResolveIntent(replay, replay.Fingerprint,
                        transition.IntentDecision.IntentId),
                "semantic_draw" when transition.DrawDecision is not null =>
                    ResolveDrawCard(replay, replay.Fingerprint,
                        transition.DrawDecision.Event.EventId,
                        transition.DrawDecision.SelectedCardInstanceId),
                "semantic_deck_reveal" when
                    transition.DeckRevealDecision is not null =>
                    ResolveDeckReveal(replay, replay.Fingerprint,
                        transition.DeckRevealDecision.Event.EventId,
                        transition.DeckRevealDecision.SelectedCardInstanceId),
                "semantic_random_pile_card" when
                    transition.RandomPileCardDecision is not null =>
                    ResolveRandomPileCard(replay, replay.Fingerprint,
                        transition.RandomPileCardDecision.Event.EventId,
                        transition.RandomPileCardDecision.SelectedCardInstanceId),
                "semantic_draw_hook_choice" when
                    transition.DrawHookChoiceDecision is not null =>
                    Step(replay, replay.Fingerprint,
                        transition.DrawHookChoiceDecision.ActionId),
                "semantic_knowledge_demon_choice" when
                    transition.KnowledgeDemonChoiceDecision is not null =>
                    Step(replay, replay.Fingerprint,
                        transition.KnowledgeDemonChoiceDecision.ActionId),
                "semantic_target" when transition.TargetDecision is not null =>
                    ResolveRandomTarget(replay, replay.Fingerprint,
                        transition.TargetDecision.Event.EventId,
                        transition.TargetDecision.SelectedEnemyInstanceId),
                "semantic_generated_card" when
                    transition.GeneratedCardDecision is not null =>
                    ResolveGeneratedCard(replay, replay.Fingerprint,
                        transition.GeneratedCardDecision.Event.EventId,
                        transition.GeneratedCardDecision.SelectedCardId),
                "semantic_potion_generation" when
                    transition.PotionGenerationDecision is not null =>
                    ResolvePotionGeneration(replay, replay.Fingerprint,
                        transition.PotionGenerationDecision.Event.EventId,
                        transition.PotionGenerationDecision.SelectedPotionId),
                "semantic_card_transform" when
                    transition.CardTransformDecision is not null =>
                    ResolveCardTransform(replay, replay.Fingerprint,
                        transition.CardTransformDecision.Event.EventId,
                        transition.CardTransformDecision.SelectedCardId),
                "semantic_card_selection" when
                    transition.CardSelectionDecision is not null =>
                    Step(replay, replay.Fingerprint,
                        transition.CardSelectionDecision.ActionId),
                "semantic_cost" when transition.CostDecision is not null =>
                    ResolveCost(replay, replay.Fingerprint,
                        transition.CostDecision.Event.EventId,
                        transition.CostDecision.SelectedCost),
                "semantic_hand_cost_selection" when
                    transition.HandCostSelectionDecision is not null =>
                    ResolveHandCostSelection(replay, replay.Fingerprint,
                        transition.HandCostSelectionDecision.Event.EventId,
                        transition.HandCostSelectionDecision.SelectedCardInstanceId),
                "semantic_card_shortlist" when
                    transition.CardShortlistDecision is not null =>
                    ResolveCardShortlist(replay, replay.Fingerprint,
                        transition.CardShortlistDecision.Event.EventId,
                        transition.CardShortlistDecision.SelectedCardInstanceId),
                "semantic_pool_shuffle" when
                    transition.PoolShuffleDecision is not null =>
                    ResolvePoolShuffle(replay, replay.Fingerprint,
                        transition.PoolShuffleDecision.Event.EventId,
                        transition.PoolShuffleDecision.SelectedSwapIndex),
                "semantic_draw_insertion" when
                    transition.DrawInsertionDecision is not null =>
                    ResolveDrawInsertion(replay, replay.Fingerprint,
                        transition.DrawInsertionDecision.Event.EventId,
                        transition.DrawInsertionDecision.SelectedPosition),
                "nature" when transition.SelectedIndex is not null =>
                    Resolve(replay, replay.Fingerprint,
                        transition.SelectedIndex.Value),
                _ => throw new InvalidDataException(
                    "Unknown semantic playable transcript stage.")
            };
        }
        if (CanonicalJson.Sha256(replay) != CanonicalJson.Sha256(saved))
            throw new InvalidDataException(
                "Semantic playable snapshot disagrees with action/Nature replay.");
        return replay;
    }

    internal static void ValidateNativePolicyBoundary(RunState run)
    {
        NativeRunRngV111.Validate(run);
        if (run.Phase != RunPhase.Combat || run.World?.Combat is not { } combat)
            throw new InvalidDataException(
                "Native policy action did not reach an active combat boundary.");
        if (!combat.IsPlayerTurn || combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.AutomaticPipelineContinuation is not null)
            throw new InvalidDataException(
                "Native policy action did not reach an idle player boundary.");
        if (combat.NativeRng is null || run.NativeRunRng is null)
            throw new InvalidDataException(
                "Native policy combat lost its RNG stream owner.");
        if (combat.SemanticDeck is not { } deck ||
            deck.Mode != SemanticDeckStateV111.NativeMode ||
            deck.Pending is not null)
            throw new InvalidDataException(
                "Native policy combat lost its exact semantic deck state.");
        SemanticDrawPileAdapterV111.ValidateSynchronized(combat);
    }
}
