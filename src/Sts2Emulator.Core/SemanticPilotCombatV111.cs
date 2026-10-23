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
        KnowledgeDemonChoiceDecision = null)
{
    public SemanticPilotContinuationV111 Fork()
    {
        Validate();
        return this switch
        {
            { IntentDecision: { } decision } => this with
            { IntentDecision = decision.Fork() },
            { DrawDecision: { } decision } => this with
            { DrawDecision = decision.Fork() },
            { DeckRevealDecision: { } decision } => this with
            { DeckRevealDecision = decision.Fork() },
            { RandomPileCardDecision: { } decision } => this with
            { RandomPileCardDecision = decision.Fork() },
            { DrawHookChoiceDecision: { } decision } => this with
            { DrawHookChoiceDecision = decision.Fork() },
            { KnowledgeDemonChoiceDecision: { } decision } => this with
            { KnowledgeDemonChoiceDecision = decision.Fork() },
            { TargetDecision: { } decision } => this with
            { TargetDecision = decision.Fork() },
            { GeneratedCardDecision: { } decision } => this with
            { GeneratedCardDecision = decision.Fork() },
            { PotionGenerationDecision: { } decision } => this with
            { PotionGenerationDecision = decision.Fork() },
            { CardTransformDecision: { } decision } => this with
            { CardTransformDecision = decision.Fork() },
            { CardSelectionDecision: { } decision } => this with
            { CardSelectionDecision = decision.Fork() },
            { CostDecision: { } decision } => this with
            { CostDecision = decision.Fork() },
            { PoolShuffleDecision: { } decision } => this with
            { PoolShuffleDecision = decision.Fork() },
            { DrawInsertionDecision: { } decision } => this with
            { DrawInsertionDecision = decision.Fork() },
            { HandCostSelectionDecision: { } decision } => this with
            { HandCostSelectionDecision = decision.Fork() },
            { CardShortlistDecision: { } decision } => this with
            { CardShortlistDecision = decision.Fork() },
            _ => this
        };
    }

    public void Validate()
    {
        int decisionCount = (IntentDecision is null ? 0 : 1) +
            (DrawDecision is null ? 0 : 1) +
            (TargetDecision is null ? 0 : 1) +
            (GeneratedCardDecision is null ? 0 : 1) +
            (CardSelectionDecision is null ? 0 : 1) +
            (CostDecision is null ? 0 : 1) +
            (PoolShuffleDecision is null ? 0 : 1) +
            (DrawInsertionDecision is null ? 0 : 1) +
            (HandCostSelectionDecision is null ? 0 : 1) +
            (CardShortlistDecision is null ? 0 : 1) +
            (RandomPileCardDecision is null ? 0 : 1) +
            (DrawHookChoiceDecision is null ? 0 : 1) +
            (PotionGenerationDecision is null ? 0 : 1) +
            (CardTransformDecision is null ? 0 : 1) +
            (DeckRevealDecision is null ? 0 : 1) +
            (KnowledgeDemonChoiceDecision is null ? 0 : 1);
        bool noInlinePayload = ActionId is null && SelectedIndex is null;
        if (Kind == "player" && !string.IsNullOrWhiteSpace(ActionId) &&
            SelectedIndex is null && decisionCount == 0)
            return;
        if (Kind == "nature" && ActionId is null &&
            SelectedIndex is int selectedIndex && selectedIndex >= 0 &&
            decisionCount == 0)
            return;
        if (Kind == "semantic_knowledge_demon_choice" &&
            !string.IsNullOrWhiteSpace(ActionId) &&
            KnowledgeDemonChoiceDecision is { } knowledgeDecision &&
            ActionId == knowledgeDecision.ActionId &&
            SelectedIndex is null && decisionCount == 1)
        {
            knowledgeDecision.Validate();
            return;
        }
        if (!noInlinePayload || decisionCount != 1)
            throw new InvalidDataException(
                "Invalid semantic pilot continuation payload.");

        if (Kind == "semantic_intent" && IntentDecision is { } intent)
            intent.Validate();
        else if (Kind == "semantic_draw" && DrawDecision is { } draw)
            draw.Validate();
        else if (Kind == "semantic_deck_reveal" &&
                 DeckRevealDecision is { } reveal)
            reveal.Validate();
        else if (Kind == "semantic_random_pile_card" &&
                 RandomPileCardDecision is { } pileCard)
            pileCard.Validate();
        else if (Kind == "semantic_draw_hook_choice" &&
                 DrawHookChoiceDecision is { } drawHook)
            drawHook.Validate();
        else if (Kind == "semantic_target" && TargetDecision is { } target)
            target.Validate();
        else if (Kind == "semantic_generated_card" &&
                 GeneratedCardDecision is { } generatedCard)
            generatedCard.Validate();
        else if (Kind == "semantic_potion_generation" &&
                 PotionGenerationDecision is { } generatedPotion)
            generatedPotion.Validate();
        else if (Kind == "semantic_card_transform" &&
                 CardTransformDecision is { } transformedCard)
            transformedCard.Validate();
        else if (Kind == "semantic_card_selection" &&
                 CardSelectionDecision is { } cardSelection)
            cardSelection.Validate();
        else if (Kind == "semantic_cost" && CostDecision is { } cost)
            cost.Validate();
        else if (Kind == "semantic_hand_cost_selection" &&
                 HandCostSelectionDecision is { } handCost)
            handCost.Validate();
        else if (Kind == "semantic_card_shortlist" &&
                 CardShortlistDecision is { } shortlist)
            shortlist.Validate();
        else if (Kind == "semantic_pool_shuffle" &&
                 PoolShuffleDecision is { } poolShuffle)
            poolShuffle.Validate();
        else if (Kind == "semantic_draw_insertion" &&
                 DrawInsertionDecision is { } drawInsertion)
            drawInsertion.Validate();
        else
            throw new InvalidDataException(
                "Semantic pilot continuation kind and payload disagree.");
    }
}

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
    public const string WeightedIntentAuditSchemaV1 =
        "sts2-v111-native-weighted-intent-audit-v1";
    public const string CardInstanceIndexAuditSchemaV1 =
        "sts2-v111-native-card-instance-index-audit-v1";
    public const string CardInstanceIndexAuditSchemaV2 =
        "sts2-v111-native-card-instance-index-audit-v2";
    public const string CardEnergyCostAuditSchemaV1 =
        "sts2-v111-native-card-energy-cost-audit-v1";
    public const string CardEnergyCostAuditSchemaV2 =
        "sts2-v111-native-card-energy-cost-audit-v2";
    public const string PoolShuffleAuditSchemaV1 =
        "sts2-v111-native-pool-shuffle-audit-v1";
    public const string PoolShuffleAuditSchemaV2 =
        "sts2-v111-native-pool-shuffle-audit-v2";
    public const string PoolShuffleAuditSchemaV3 =
        "sts2-v111-native-pool-shuffle-audit-v3";
    public const string PoolShuffleAuditSchemaV4 =
        "sts2-v111-native-pool-shuffle-audit-v4";
    public const string GeneratedCardModelAuditSchemaV1 =
        "sts2-v111-native-generated-card-model-audit-v1";
    public const string GeneratedCardModelAuditSchemaV2 =
        "sts2-v111-native-generated-card-model-audit-v2";
    public const string CardOddsAuditSchemaV1 =
        SemanticCardOddsNativeAuditV111.CurrentSchemaId;
    public const string PotionRewardOddsAuditSchemaV1 =
        SemanticPotionRewardOddsNativeAuditV111.CurrentSchemaId;
    public const string PotionFactoryAuditSchemaV1 =
        SemanticPotionFactoryNativeAuditV111.CurrentSchemaId;
    public const string CombatGoldAuditSchemaV1 =
        SemanticCombatGoldNativeAuditV111.CurrentSchemaId;
    public const string RelicRewardAuditSchemaV1 =
        SemanticRelicRewardNativeAuditV111.CurrentSchemaId;
    public const string EncounterFormationAuditSchemaV1 =
        "sts2-v111-native-encounter-formation-audit-v1";
    public const string EncounterFormationAuditSchemaV2 =
        "sts2-v111-native-encounter-formation-audit-v2";
    public const string CardTransformAuditSchemaV1 =
        "sts2-v111-native-card-transform-audit-v1";
    public const string RandomTargetAuditSchemaV1 =
        "sts2-v111-native-random-target-audit-v1";
    public const string PotionGenerationAuditSchemaV1 =
        "sts2-v111-native-potion-generation-audit-v1";
    public const string DrawInsertionAuditSchemaV1 =
        "sts2-v111-native-draw-insertion-audit-v1";
    public const string EnemyPileInsertionAuditSchemaV1 =
        "sts2-v111-native-enemy-pile-insertion-audit-v1";
    public const string ProviderTraceSchemaV1 =
        "sts2-v111-native-policy-mixed-trace-v1";

    /// <summary>Null for older snapshots that predate weighted intent audit
    /// coverage validation.</summary>
    public string? WeightedIntentAuditSchema { get; init; }
    /// <summary>V1 covers combat selections; V2 adds Fishing Rod's Native
    /// combat-end card selection.</summary>
    public string? CardInstanceIndexAuditSchema { get; init; }
    /// <summary>Null for older snapshots that predate typed per-card energy
    /// cost annotations; V1 covers hand costs and V2 also covers draw hooks.</summary>
    public string? CardEnergyCostAuditSchema { get; init; }
    /// <summary>V1 covers combat-card shuffles; V2 adds relic-bag queues;
    /// V3 adds Native entry/recycle draw-pile shuffles; V4 adds War Hammer
    /// and boss-reward relic offer shuffles.</summary>
    public string? PoolShuffleAuditSchema { get; init; }
    /// <summary>V1 covers combat generated-card choices; V2 also covers
    /// Native combat-reward card-model offers.</summary>
    public string? GeneratedCardModelAuditSchema { get; init; }
    /// <summary>Null for older snapshots; V1 covers Native encounter-card
    /// rarity and upgrade float outcomes.</summary>
    public string? CardOddsAuditSchema { get; init; }
    /// <summary>Null for older snapshots; V1 covers Native combat-potion
    /// presence and pity-update outcomes.</summary>
    public string? PotionRewardOddsAuditSchema { get; init; }
    /// <summary>Null for older snapshots; V1 covers Native PotionFactory
    /// reward rarity and model outcomes.</summary>
    public string? PotionFactoryAuditSchema { get; init; }
    /// <summary>Null for older snapshots; V1 covers raw Native combat-gold
    /// amounts before later reward modifiers.</summary>
    public string? CombatGoldAuditSchema { get; init; }
    /// <summary>Null for older snapshots; V1 covers Native relic-reward
    /// rarity and committed queue outcomes.</summary>
    public string? RelicRewardAuditSchema { get; init; }
    /// <summary>Null for older snapshots; V1 requires the source-route
    /// encounter-formation result and its typed index choices.</summary>
    public string? EncounterFormationAuditSchema { get; init; }
    /// <summary>Null for older snapshots that predate typed Native card
    /// transform replacement results.</summary>
    public string? CardTransformAuditSchema { get; init; }
    /// <summary>Null for older snapshots that predate typed selected
    /// enemy identities at Native random-target sites.</summary>
    public string? RandomTargetAuditSchema { get; init; }
    /// <summary>Null for older snapshots that predate typed Native combat
    /// potion model results.</summary>
    public string? PotionGenerationAuditSchema { get; init; }
    /// <summary>Null for older snapshots that predate typed Native draw-pile
    /// insertion outcomes.</summary>
    public string? DrawInsertionAuditSchema { get; init; }
    /// <summary>Null for older snapshots that predate typed Native enemy
    /// pile-insertion outcomes.</summary>
    public string? EnemyPileInsertionAuditSchema { get; init; }
    /// <summary>Null for older snapshots that did not require a complete
    /// mixed provider trace.</summary>
    public string? ProviderTraceSchema { get; init; }
    /// <summary>Canonical fingerprint for the complete mixed trace in marked
    /// snapshots; null on older snapshots.</summary>
    public string? ProviderTraceFingerprint { get; init; }

    private RandomBoundaryEventV111[]? _decisions =
        CloneEvents(Decisions);
    private RandomBoundaryTranscriptEntryV111[]? _providerTrace = [];
    private SemanticIntentAuditEntryV111[]? _weightedIntentOutcomes = [];
    private SemanticCardInstanceIndexAuditV111[]? _cardInstanceIndexOutcomes = [];
    private SemanticCardEnergyCostAuditV111[]? _cardEnergyCostOutcomes = [];
    private SemanticDrawInsertionNativeAuditV111[]? _drawInsertionOutcomes = [];
    private SemanticEnemyPileInsertionNativeAuditV111[]?
        _enemyPileInsertionOutcomes = [];
    private SemanticPoolShuffleDecisionV111[]? _poolShuffleOutcomes = [];
    private SemanticGeneratedCardNativeAuditV111[]?
        _generatedCardModelOutcomes = [];
    private SemanticCardTransformNativeAuditV111[]? _cardTransformOutcomes = [];
    private SemanticTargetNativeAuditV111[]? _randomTargetOutcomes = [];
    private SemanticPotionGenerationNativeAuditV111[]?
        _potionGenerationOutcomes = [];
    private SemanticCardOddsNativeAuditV111[]? _cardOddsOutcomes = [];
    private SemanticPotionRewardOddsNativeAuditV111[]?
        _potionRewardOddsOutcomes = [];
    private SemanticPotionFactoryNativeAuditV111[]?
        _potionFactoryOutcomes = [];
    private SemanticCombatGoldNativeAuditV111[]? _combatGoldOutcomes = [];
    private SemanticRelicRewardNativeAuditV111[]? _relicRewardOutcomes = [];
    private NativeEncounterFormationAuditV111[]?
        _encounterFormationOutcomes = [];

    public RandomBoundaryEventV111[] Decisions
    {
        get => CloneEvents(_decisions)!;
        init => _decisions = CloneEvents(value);
    }
    public RandomBoundaryTranscriptEntryV111[] ProviderTrace
    {
        get => CloneTrace(_providerTrace)!;
        init => _providerTrace = CloneTrace(value);
    }
    public SemanticIntentAuditEntryV111[] WeightedIntentOutcomes
    {
        get => CloneIntentOutcomes(_weightedIntentOutcomes)!;
        init => _weightedIntentOutcomes = CloneIntentOutcomes(value);
    }
    public SemanticCardInstanceIndexAuditV111[] CardInstanceIndexOutcomes
    {
        get => CloneCardInstanceIndexOutcomes(
            _cardInstanceIndexOutcomes)!;
        init => _cardInstanceIndexOutcomes = CloneCardInstanceIndexOutcomes(
            value);
    }
    public SemanticCardEnergyCostAuditV111[] CardEnergyCostOutcomes
    {
        get => CloneCardEnergyCostOutcomes(_cardEnergyCostOutcomes)!;
        init => _cardEnergyCostOutcomes = CloneCardEnergyCostOutcomes(value);
    }
    public SemanticDrawInsertionNativeAuditV111[] DrawInsertionOutcomes
    {
        get => CloneDrawInsertionOutcomes(_drawInsertionOutcomes)!;
        init => _drawInsertionOutcomes = CloneDrawInsertionOutcomes(value);
    }
    public SemanticEnemyPileInsertionNativeAuditV111[]
        EnemyPileInsertionOutcomes
    {
        get => CloneEnemyPileInsertionOutcomes(
            _enemyPileInsertionOutcomes)!;
        init => _enemyPileInsertionOutcomes =
            CloneEnemyPileInsertionOutcomes(value);
    }
    public SemanticPoolShuffleDecisionV111[] PoolShuffleOutcomes
    {
        get => ClonePoolShuffleOutcomes(_poolShuffleOutcomes)!;
        init => _poolShuffleOutcomes = ClonePoolShuffleOutcomes(value);
    }
    public SemanticGeneratedCardNativeAuditV111[]
        GeneratedCardModelOutcomes
    {
        get => CloneGeneratedCardModelOutcomes(
            _generatedCardModelOutcomes)!;
        init => _generatedCardModelOutcomes =
            CloneGeneratedCardModelOutcomes(value);
    }
    public SemanticCardTransformNativeAuditV111[] CardTransformOutcomes
    {
        get => CloneCardTransformOutcomes(_cardTransformOutcomes)!;
        init => _cardTransformOutcomes = CloneCardTransformOutcomes(value);
    }
    public SemanticTargetNativeAuditV111[] RandomTargetOutcomes
    {
        get => CloneRandomTargetOutcomes(_randomTargetOutcomes)!;
        init => _randomTargetOutcomes = CloneRandomTargetOutcomes(value);
    }
    public SemanticPotionGenerationNativeAuditV111[] PotionGenerationOutcomes
    {
        get => ClonePotionGenerationOutcomes(_potionGenerationOutcomes)!;
        init => _potionGenerationOutcomes =
            ClonePotionGenerationOutcomes(value);
    }
    public SemanticCardOddsNativeAuditV111[] CardOddsOutcomes
    {
        get => CloneCardOddsOutcomes(_cardOddsOutcomes)!;
        init => _cardOddsOutcomes = CloneCardOddsOutcomes(value);
    }
    public SemanticPotionRewardOddsNativeAuditV111[]
        PotionRewardOddsOutcomes
    {
        get => ClonePotionRewardOddsOutcomes(
            _potionRewardOddsOutcomes)!;
        init => _potionRewardOddsOutcomes =
            ClonePotionRewardOddsOutcomes(value);
    }
    public SemanticPotionFactoryNativeAuditV111[] PotionFactoryOutcomes
    {
        get => ClonePotionFactoryOutcomes(_potionFactoryOutcomes)!;
        init => _potionFactoryOutcomes =
            ClonePotionFactoryOutcomes(value);
    }
    public SemanticCombatGoldNativeAuditV111[] CombatGoldOutcomes
    {
        get => CloneCombatGoldOutcomes(_combatGoldOutcomes)!;
        init => _combatGoldOutcomes = CloneCombatGoldOutcomes(value);
    }
    public SemanticRelicRewardNativeAuditV111[] RelicRewardOutcomes
    {
        get => CloneRelicRewardOutcomes(_relicRewardOutcomes)!;
        init => _relicRewardOutcomes = CloneRelicRewardOutcomes(value);
    }
    public NativeEncounterFormationAuditV111[] EncounterFormationOutcomes
    {
        get => CloneEncounterFormationOutcomes(
            _encounterFormationOutcomes)!;
        init => _encounterFormationOutcomes =
            CloneEncounterFormationOutcomes(value);
    }

    public SemanticNativePolicyAuditV111 Fork() => this with
    {
        Decisions = CloneEvents(_decisions)!,
        ProviderTrace = CloneTrace(_providerTrace)!,
        WeightedIntentOutcomes = CloneIntentOutcomes(
            _weightedIntentOutcomes)!,
        CardInstanceIndexOutcomes = CloneCardInstanceIndexOutcomes(
            _cardInstanceIndexOutcomes)!,
        CardEnergyCostOutcomes = CloneCardEnergyCostOutcomes(
            _cardEnergyCostOutcomes)!,
        DrawInsertionOutcomes = CloneDrawInsertionOutcomes(
            _drawInsertionOutcomes)!,
        EnemyPileInsertionOutcomes = CloneEnemyPileInsertionOutcomes(
            _enemyPileInsertionOutcomes)!,
        PoolShuffleOutcomes = ClonePoolShuffleOutcomes(
            _poolShuffleOutcomes)!,
        GeneratedCardModelOutcomes = CloneGeneratedCardModelOutcomes(
            _generatedCardModelOutcomes)!,
        CardTransformOutcomes = CloneCardTransformOutcomes(
            _cardTransformOutcomes)!,
        RandomTargetOutcomes = CloneRandomTargetOutcomes(
            _randomTargetOutcomes)!,
        PotionGenerationOutcomes = ClonePotionGenerationOutcomes(
            _potionGenerationOutcomes)!,
        CardOddsOutcomes = CloneCardOddsOutcomes(_cardOddsOutcomes)!,
        PotionRewardOddsOutcomes = ClonePotionRewardOddsOutcomes(
            _potionRewardOddsOutcomes)!,
        PotionFactoryOutcomes = ClonePotionFactoryOutcomes(
            _potionFactoryOutcomes)!,
        CombatGoldOutcomes = CloneCombatGoldOutcomes(_combatGoldOutcomes)!,
        RelicRewardOutcomes = CloneRelicRewardOutcomes(_relicRewardOutcomes)!,
        EncounterFormationOutcomes = CloneEncounterFormationOutcomes(
            _encounterFormationOutcomes)!
    };

    private static RandomBoundaryEventV111[]? CloneEvents(
        RandomBoundaryEventV111[]? values) => values?.Select(value =>
            value is null ? null! : value.Fork()).ToArray();

    private static RandomBoundaryTranscriptEntryV111[]? CloneTrace(
        RandomBoundaryTranscriptEntryV111[]? values) => values?.Select(value =>
            value is null ? null! : value.Fork()).ToArray();

    private static SemanticIntentAuditEntryV111[]? CloneIntentOutcomes(
        SemanticIntentAuditEntryV111[]? values) => values?.Select(value =>
            value is null ? null! : value.Fork()).ToArray();

    private static SemanticCardInstanceIndexAuditV111[]?
        CloneCardInstanceIndexOutcomes(
            SemanticCardInstanceIndexAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticCardEnergyCostAuditV111[]?
        CloneCardEnergyCostOutcomes(
            SemanticCardEnergyCostAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticDrawInsertionNativeAuditV111[]?
        CloneDrawInsertionOutcomes(
            SemanticDrawInsertionNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticEnemyPileInsertionNativeAuditV111[]?
        CloneEnemyPileInsertionOutcomes(
            SemanticEnemyPileInsertionNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticPoolShuffleDecisionV111[]?
        ClonePoolShuffleOutcomes(
            SemanticPoolShuffleDecisionV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticGeneratedCardNativeAuditV111[]?
        CloneGeneratedCardModelOutcomes(
            SemanticGeneratedCardNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticCardTransformNativeAuditV111[]?
        CloneCardTransformOutcomes(
            SemanticCardTransformNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticTargetNativeAuditV111[]?
        CloneRandomTargetOutcomes(
            SemanticTargetNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticPotionGenerationNativeAuditV111[]?
        ClonePotionGenerationOutcomes(
            SemanticPotionGenerationNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticCardOddsNativeAuditV111[]?
        CloneCardOddsOutcomes(
            SemanticCardOddsNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticPotionRewardOddsNativeAuditV111[]?
        ClonePotionRewardOddsOutcomes(
            SemanticPotionRewardOddsNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticPotionFactoryNativeAuditV111[]?
        ClonePotionFactoryOutcomes(
            SemanticPotionFactoryNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticCombatGoldNativeAuditV111[]?
        CloneCombatGoldOutcomes(
            SemanticCombatGoldNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static SemanticRelicRewardNativeAuditV111[]?
        CloneRelicRewardOutcomes(
            SemanticRelicRewardNativeAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    private static NativeEncounterFormationAuditV111[]?
        CloneEncounterFormationOutcomes(
            NativeEncounterFormationAuditV111[]? values) =>
        values?.Select(value => value is null ? null! : value.Fork())
            .ToArray();

    public void Validate()
    {
        var decisions = _decisions;
        var providerTrace = _providerTrace;
        var weightedIntentOutcomes = _weightedIntentOutcomes;
        var cardInstanceIndexOutcomes = _cardInstanceIndexOutcomes;
        var cardEnergyCostOutcomes = _cardEnergyCostOutcomes;
        var drawInsertionOutcomes = _drawInsertionOutcomes;
        var enemyPileInsertionOutcomes = _enemyPileInsertionOutcomes;
        var poolShuffleOutcomes = _poolShuffleOutcomes;
        var generatedCardModelOutcomes = _generatedCardModelOutcomes;
        var cardTransformOutcomes = _cardTransformOutcomes;
        var randomTargetOutcomes = _randomTargetOutcomes;
        var potionGenerationOutcomes = _potionGenerationOutcomes;
        var cardOddsOutcomes = _cardOddsOutcomes;
        var potionRewardOddsOutcomes = _potionRewardOddsOutcomes;
        var potionFactoryOutcomes = _potionFactoryOutcomes;
        var combatGoldOutcomes = _combatGoldOutcomes;
        var relicRewardOutcomes = _relicRewardOutcomes;
        var encounterFormationOutcomes = _encounterFormationOutcomes;
        if (ProviderId != NativeProviderId ||
            ProbabilityStatus != "UNVERIFIED" || decisions is null ||
            providerTrace is null || weightedIntentOutcomes is null ||
            cardInstanceIndexOutcomes is null ||
            cardEnergyCostOutcomes is null ||
            drawInsertionOutcomes is null ||
            enemyPileInsertionOutcomes is null ||
            poolShuffleOutcomes is null ||
            generatedCardModelOutcomes is null ||
            cardTransformOutcomes is null ||
            randomTargetOutcomes is null ||
            potionGenerationOutcomes is null ||
            cardOddsOutcomes is null ||
            potionRewardOddsOutcomes is null ||
            potionFactoryOutcomes is null ||
            combatGoldOutcomes is null ||
            relicRewardOutcomes is null ||
            encounterFormationOutcomes is null ||
            (WeightedIntentAuditSchema is not null &&
             WeightedIntentAuditSchema != WeightedIntentAuditSchemaV1) ||
            (CardInstanceIndexAuditSchema is not null &&
             CardInstanceIndexAuditSchema is not (
                 CardInstanceIndexAuditSchemaV1 or
                 CardInstanceIndexAuditSchemaV2)) ||
            (CardEnergyCostAuditSchema is not null &&
             CardEnergyCostAuditSchema is not (
                 CardEnergyCostAuditSchemaV1 or
                 CardEnergyCostAuditSchemaV2)) ||
            (PoolShuffleAuditSchema is not null &&
             PoolShuffleAuditSchema is not (
                 PoolShuffleAuditSchemaV1 or PoolShuffleAuditSchemaV2 or
                 PoolShuffleAuditSchemaV3 or PoolShuffleAuditSchemaV4)) ||
            (GeneratedCardModelAuditSchema is not null &&
             GeneratedCardModelAuditSchema is not (
                 GeneratedCardModelAuditSchemaV1 or
                 GeneratedCardModelAuditSchemaV2)) ||
            (CardOddsAuditSchema is not null &&
             CardOddsAuditSchema != CardOddsAuditSchemaV1) ||
            (PotionRewardOddsAuditSchema is not null &&
             PotionRewardOddsAuditSchema != PotionRewardOddsAuditSchemaV1) ||
            (PotionFactoryAuditSchema is not null &&
             PotionFactoryAuditSchema != PotionFactoryAuditSchemaV1) ||
            (CombatGoldAuditSchema is not null &&
             CombatGoldAuditSchema != CombatGoldAuditSchemaV1) ||
            (RelicRewardAuditSchema is not null &&
             RelicRewardAuditSchema != RelicRewardAuditSchemaV1) ||
            (EncounterFormationAuditSchema is not null &&
             EncounterFormationAuditSchema is not (
                EncounterFormationAuditSchemaV1 or
                EncounterFormationAuditSchemaV2)) ||
            (CardTransformAuditSchema is not null &&
             CardTransformAuditSchema != CardTransformAuditSchemaV1) ||
            (RandomTargetAuditSchema is not null &&
             RandomTargetAuditSchema != RandomTargetAuditSchemaV1) ||
            (PotionGenerationAuditSchema is not null &&
             PotionGenerationAuditSchema != PotionGenerationAuditSchemaV1) ||
            (DrawInsertionAuditSchema is not null &&
             DrawInsertionAuditSchema != DrawInsertionAuditSchemaV1) ||
            (EnemyPileInsertionAuditSchema is not null &&
             EnemyPileInsertionAuditSchema !=
                EnemyPileInsertionAuditSchemaV1) ||
            (ProviderTraceSchema is not null &&
             ProviderTraceSchema != ProviderTraceSchemaV1) ||
            (ProviderTraceSchema is null &&
             ProviderTraceFingerprint is not null) ||
            (ProviderTraceSchema == ProviderTraceSchemaV1 &&
             (string.IsNullOrWhiteSpace(ProviderTraceFingerprint) ||
              ProviderTraceFingerprint !=
                CanonicalJson.Sha256(providerTrace))))
            throw new InvalidDataException(
                "Native policy baseline has invalid provider provenance.");

        for (var index = 0; index < decisions.Length; index++)
        {
            var decision = decisions[index];
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

        var weightedEventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var auditEntry in weightedIntentOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native policy weighted intent audit entry is null.");
            auditEntry.Validate();
            var semanticEvent = auditEntry.Outcome.Event;
            var expectedDecision = auditEntry.ProviderDecision;
            var matchingDecisions = decisions.Where(decision =>
                CanonicalJson.Sha256(new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter)) ==
                CanonicalJson.Sha256(expectedDecision)).ToArray();
            if (matchingDecisions.Length != 1 ||
                !weightedEventIds.Add(semanticEvent.EventId))
                throw new InvalidDataException(
                    "Native policy weighted intent outcome does not match its primitive decision.");
        }

        var cardIndexOutcomeEventIds = new HashSet<string>(
            StringComparer.Ordinal);
        var cardIndexOutcomeDecisions = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var auditEntry in cardInstanceIndexOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native policy card-instance index audit entry is null.");
            auditEntry.Validate();
            var decisionFingerprint = CanonicalJson.Sha256(
                auditEntry.ProviderDecision);
            var matchingDecisions = decisions.Count(decision =>
                CanonicalJson.Sha256(new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter)) == decisionFingerprint);
            if (matchingDecisions != 1 ||
                !cardIndexOutcomeEventIds.Add(auditEntry.EventId) ||
                !cardIndexOutcomeDecisions.Add(decisionFingerprint))
                throw new InvalidDataException(
                    "Native policy card-instance outcome does not uniquely match its primitive decision.");
        }

        var cardCostOutcomeEventIds = new HashSet<string>(StringComparer.Ordinal);
        var cardCostOutcomeDecisions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var auditEntry in cardEnergyCostOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native policy card energy-cost audit entry is null.");
            auditEntry.Validate();
            var decisionFingerprint = CanonicalJson.Sha256(
                auditEntry.ProviderDecision);
            var matchingDecisions = decisions.Count(decision =>
                CanonicalJson.Sha256(new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter)) == decisionFingerprint);
            if (matchingDecisions != 1 ||
                !cardCostOutcomeEventIds.Add(auditEntry.EventId) ||
                !cardCostOutcomeDecisions.Add(decisionFingerprint))
                throw new InvalidDataException(
                    "Native policy card energy-cost outcome does not uniquely match its primitive decision.");
        }

        var drawInsertionEventIds = new HashSet<string>(StringComparer.Ordinal);
        var drawInsertionDecisions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var auditEntry in drawInsertionOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native policy draw-insertion audit entry is null.");
            auditEntry.Validate();
            var decisionFingerprint = CanonicalJson.Sha256(
                auditEntry.ProviderDecision);
            var matchingDecisions = decisions.Count(decision =>
                CanonicalJson.Sha256(new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter)) == decisionFingerprint);
            if (matchingDecisions != 1 ||
                !drawInsertionEventIds.Add(auditEntry.EventId) ||
                !drawInsertionDecisions.Add(decisionFingerprint))
                throw new InvalidDataException(
                    "Native policy draw-insertion outcome does not uniquely match its primitive decision.");
        }

        var enemyPileInsertionEventIds =
            new HashSet<string>(StringComparer.Ordinal);
        var enemyPileInsertionDecisions =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (var auditEntry in enemyPileInsertionOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native policy enemy pile-insertion audit entry is null.");
            auditEntry.Validate();
            var decisionFingerprint = CanonicalJson.Sha256(
                auditEntry.ProviderDecision);
            var matchingDecisions = decisions.Count(decision =>
                CanonicalJson.Sha256(new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter)) == decisionFingerprint);
            if (matchingDecisions != 1 ||
                !enemyPileInsertionEventIds.Add(auditEntry.EventId) ||
                !enemyPileInsertionDecisions.Add(decisionFingerprint))
                throw new InvalidDataException(
                    "Native policy enemy pile-insertion outcome does not uniquely match its primitive decision.");
        }

        var poolShuffleEventIds = new HashSet<string>(StringComparer.Ordinal);
        var poolShuffleRequests = new HashSet<string>(StringComparer.Ordinal);
        foreach (var auditEntry in poolShuffleOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native policy pool-shuffle audit entry is null.");
            auditEntry.Validate();
            var poolEvent = auditEntry.Event;
            var requestFingerprint = CanonicalJson.Sha256(new
            {
                poolEvent.Request,
                auditEntry.SelectedSwapIndex
            });
            var matchingDecisions = decisions.Where(decision =>
                CanonicalJson.Sha256(decision.Request) ==
                    CanonicalJson.Sha256(poolEvent.Request) &&
                decision.SelectedIndex == auditEntry.SelectedSwapIndex)
                .ToArray();
            if (matchingDecisions.Length != 1)
                throw new InvalidDataException(
                    "Native policy pool-shuffle outcome does not uniquely match its primitive decision.");
            var matchedDecision = matchingDecisions[0];
            SemanticPoolShuffleChoiceV111.ValidateNativeAudit(
                auditEntry, new RandomIndexDecisionV111(
                    matchedDecision.Request, matchedDecision.SelectedIndex,
                    matchedDecision.ProviderMode,
                    matchedDecision.NativeCursorBefore,
                    matchedDecision.NativeCursorAfter));
            if (!poolShuffleEventIds.Add(poolEvent.EventId) ||
                !poolShuffleRequests.Add(requestFingerprint))
                throw new InvalidDataException(
                    "Native policy pool-shuffle outcome does not uniquely match its primitive decision.");
        }
        var poolShuffleChains = new Dictionary<string,
            (int NextSwapIndex, int NextOrdinal, string[] ExpectedCurrent,
                string[] Original, int SelectedCount, string Operation,
                long? SourceCardInstanceId)>(StringComparer.Ordinal);
        foreach (var auditEntry in poolShuffleOutcomes)
        {
            var poolEvent = auditEntry.Event;
            var request = poolEvent.Request;
            var chainKey = CanonicalJson.Sha256(new
            {
                request.SiteId,
                request.StreamId,
                request.OwnerId
            });
            var currentCards = poolEvent.CurrentCardIds;
            var originalCards = poolEvent.OriginalCardIds;
            if (poolShuffleChains.TryGetValue(chainKey, out var previous))
            {
                if (poolEvent.NextSwapIndex != previous.NextSwapIndex ||
                    request.Ordinal != previous.NextOrdinal ||
                    poolEvent.SelectedCount != previous.SelectedCount ||
                    poolEvent.SourceOperationKind != previous.Operation ||
                    poolEvent.SourceCardInstanceId !=
                        previous.SourceCardInstanceId ||
                    !originalCards.SequenceEqual(
                        previous.Original, StringComparer.Ordinal) ||
                    !currentCards.SequenceEqual(
                        previous.ExpectedCurrent, StringComparer.Ordinal))
                    throw new InvalidDataException(
                        "Native pool-shuffle outcomes do not form one ordered Fisher–Yates sequence.");
            }
            else if (poolEvent.NextSwapIndex != originalCards.Length - 1 ||
                     !currentCards.SequenceEqual(
                         originalCards, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "Native pool-shuffle outcome does not begin at the original ordered support.");
            }

            var expectedCurrent = (string[])currentCards.Clone();
            (expectedCurrent[poolEvent.NextSwapIndex],
                expectedCurrent[auditEntry.SelectedSwapIndex]) =
                (expectedCurrent[auditEntry.SelectedSwapIndex],
                    expectedCurrent[poolEvent.NextSwapIndex]);
            if (poolEvent.NextSwapIndex == 1)
                poolShuffleChains.Remove(chainKey);
            else
                poolShuffleChains[chainKey] = (
                    poolEvent.NextSwapIndex - 1, request.Ordinal + 1,
                    expectedCurrent, originalCards,
                    poolEvent.SelectedCount, poolEvent.SourceOperationKind,
                    poolEvent.SourceCardInstanceId);
        }
        if (poolShuffleChains.Count != 0)
            throw new InvalidDataException(
                "Native policy pool-shuffle annotations end before a full permutation is recorded.");

        var generatedCardEventIds = new HashSet<string>(StringComparer.Ordinal);
        var generatedCardDecisionFingerprints = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var auditEntry in generatedCardModelOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native generated-card model audit entry is null.");
            auditEntry.Validate();
            var decisionFingerprint = CanonicalJson.Sha256(
                auditEntry.ProviderDecision);
            var matchingDecisions = decisions.Count(decision =>
                CanonicalJson.Sha256(new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter)) == decisionFingerprint);
            if (matchingDecisions != 1 ||
                !generatedCardEventIds.Add(auditEntry.EventId) ||
                !generatedCardDecisionFingerprints.Add(decisionFingerprint))
                throw new InvalidDataException(
                    "Native generated-card model outcome does not uniquely match its primitive decision.");
        }
        var cardTransformEventIds = new HashSet<string>(StringComparer.Ordinal);
        var cardTransformDecisionFingerprints = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var auditEntry in cardTransformOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native card-transform audit entry is null.");
            auditEntry.Validate();
            var decisionFingerprint = CanonicalJson.Sha256(
                auditEntry.ProviderDecision);
            var matchingDecisions = decisions.Count(decision =>
                CanonicalJson.Sha256(new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter)) == decisionFingerprint);
            if (matchingDecisions != 1 ||
                !cardTransformEventIds.Add(auditEntry.EventId) ||
                !cardTransformDecisionFingerprints.Add(decisionFingerprint))
                throw new InvalidDataException(
                    "Native card-transform outcome does not uniquely match its primitive decision.");
        }
        var randomTargetEventIds = new HashSet<string>(StringComparer.Ordinal);
        var randomTargetDecisionFingerprints = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var auditEntry in randomTargetOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native random-target audit entry is null.");
            auditEntry.Validate();
            var eventId = auditEntry.Outcome.Event.EventId;
            var decisionFingerprint = CanonicalJson.Sha256(
                auditEntry.ProviderDecision);
            var matchingDecisions = decisions.Count(decision =>
                CanonicalJson.Sha256(new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter)) == decisionFingerprint);
            if (matchingDecisions != 1 ||
                !randomTargetEventIds.Add(eventId) ||
                !randomTargetDecisionFingerprints.Add(decisionFingerprint))
                throw new InvalidDataException(
                    "Native random-target outcome does not uniquely match its primitive decision.");
        }
        var potionGenerationEventIds = new HashSet<string>(
            StringComparer.Ordinal);
        var potionGenerationDecisionFingerprints = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var auditEntry in potionGenerationOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native potion-generation audit entry is null.");
            auditEntry.Validate();
            var eventId = auditEntry.Outcome.Event.EventId;
            var decisionFingerprint = CanonicalJson.Sha256(
                auditEntry.ProviderDecision);
            var matchingDecisions = decisions.Count(decision =>
                CanonicalJson.Sha256(new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter)) == decisionFingerprint);
            if (matchingDecisions != 1 ||
                !potionGenerationEventIds.Add(eventId) ||
                !potionGenerationDecisionFingerprints.Add(
                    decisionFingerprint))
                throw new InvalidDataException(
                    "Native potion-generation outcome does not uniquely match its primitive decision.");
        }
        var cardOddsDecisionFingerprints = new HashSet<string>(
            StringComparer.Ordinal);
        var cardOddsOutcomeFingerprints = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var auditEntry in cardOddsOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native card-odds audit entry is null.");
            auditEntry.Validate();
            if (!cardOddsDecisionFingerprints.Add(CanonicalJson.Sha256(
                    auditEntry.ProviderDecision)) ||
                !cardOddsOutcomeFingerprints.Add(
                    auditEntry.OutcomeFingerprint))
                throw new InvalidDataException(
                    "Native card-odds outcome duplicates a provider decision or fingerprint.");
        }
        var potionRewardOddsDecisions = new HashSet<string>(
            StringComparer.Ordinal);
        var potionRewardOddsFingerprints = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var auditEntry in potionRewardOddsOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native potion-reward odds audit entry is null.");
            auditEntry.Validate();
            if (!potionRewardOddsDecisions.Add(CanonicalJson.Sha256(
                    auditEntry.ProviderDecision)) ||
                !potionRewardOddsFingerprints.Add(
                    auditEntry.OutcomeFingerprint))
                throw new InvalidDataException(
                    "Native potion-reward odds outcome duplicates a provider decision or fingerprint.");
        }
        var potionFactoryEventIds = new HashSet<string>(StringComparer.Ordinal);
        var potionFactoryRarityDecisions = new HashSet<string>(
            StringComparer.Ordinal);
        var potionFactoryModelDecisions = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var auditEntry in potionFactoryOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native PotionFactory audit entry is null.");
            auditEntry.Validate();
            if (!potionFactoryEventIds.Add(auditEntry.EventId) ||
                !potionFactoryRarityDecisions.Add(CanonicalJson.Sha256(
                    auditEntry.RarityDecision)) ||
                !potionFactoryModelDecisions.Add(CanonicalJson.Sha256(
                    auditEntry.ModelDecision)))
                throw new InvalidDataException(
                    "Native PotionFactory outcome duplicates an event or primitive decision.");
        }
        var combatGoldEvents = new HashSet<string>(StringComparer.Ordinal);
        var combatGoldDecisions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var auditEntry in combatGoldOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native combat-gold audit entry is null.");
            auditEntry.Validate();
            if (!combatGoldEvents.Add(auditEntry.EventId) ||
                !combatGoldDecisions.Add(CanonicalJson.Sha256(
                    auditEntry.ProviderDecision)))
                throw new InvalidDataException(
                    "Native combat-gold outcome duplicates an event or primitive decision.");
        }
        var relicRewardEvents = new HashSet<string>(StringComparer.Ordinal);
        var relicRewardDecisions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var auditEntry in relicRewardOutcomes)
        {
            if (auditEntry is null)
                throw new InvalidDataException(
                    "Native relic-reward audit entry is null.");
            auditEntry.Validate();
            if (!relicRewardEvents.Add(auditEntry.EventId) ||
                !relicRewardDecisions.Add(CanonicalJson.Sha256(
                    auditEntry.ProviderDecision)))
                throw new InvalidDataException(
                    "Native relic-reward outcome duplicates an event or primitive decision.");
        }

        var traceIndices = new List<RandomBoundaryEventV111>();
        var traceEncounterFormationDecisions =
            new List<RandomIndexDecisionV111>();
        var traceInitialHpDecisions =
            new List<RandomIndexDecisionV111>();
        var traceIntentDecisions = new List<RandomIndexDecisionV111>();
        var traceCardInstanceDecisions =
            new List<RandomIndexDecisionV111>();
        var traceCardEnergyCostDecisions =
            new List<RandomIndexDecisionV111>();
        var traceDrawInsertionDecisions =
            new List<RandomIndexDecisionV111>();
        var traceEnemyPileInsertionDecisions =
            new List<RandomIndexDecisionV111>();
        var tracePoolShuffleDecisions =
            new List<RandomIndexDecisionV111>();
        var traceNativeEntryShuffleDecisions =
            new List<RandomIndexDecisionV111>();
        var traceNativeRecycleShuffleDecisions =
            new List<RandomIndexDecisionV111>();
        var traceRelicQueueShuffleDecisions =
            new List<RandomIndexDecisionV111>();
        var tracePersistentNativeShuffleDecisions =
            new List<RandomIndexDecisionV111>();
        var traceGeneratedCardModelDecisions =
            new List<RandomIndexDecisionV111>();
        var traceCardTransformDecisions =
            new List<RandomIndexDecisionV111>();
        var traceRandomTargetDecisions =
            new List<RandomIndexDecisionV111>();
        var tracePotionGenerationDecisions =
            new List<RandomIndexDecisionV111>();
        var traceCardOddsDecisions =
            new List<RandomUnitFloatDecisionV111>();
        var tracePotionRewardOddsDecisions =
            new List<RandomUnitFloatDecisionV111>();
        var tracePotionFactoryRarityDecisions =
            new List<RandomUnitFloatDecisionV111>();
        var tracePotionFactoryModelDecisions =
            new List<RandomIndexDecisionV111>();
        var traceCombatGoldDecisions =
            new List<RandomIndexDecisionV111>();
        var traceRelicRewardDecisions =
            new List<RandomUnitFloatDecisionV111>();
        for (var index = 0; index < providerTrace.Length; index++)
        {
            var entry = providerTrace[index];
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
                if (value.Request.SiteId ==
                    NativeEncounterFormationFactoryV111.FormationSiteId)
                    traceEncounterFormationDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (value.Request.SiteId ==
                    NativeCombatRngEntryV111.InitialUniqueHpSiteId)
                    traceInitialHpDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (value.Request.SiteId ==
                        StochasticSiteRegistryV111.NativeCombatGoldSiteId &&
                    value.Request.StreamId ==
                        NativePlayerRngV111.RewardsStreamId &&
                    value.Request.OperationKind == "bounded_integer")
                    traceCombatGoldDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (value.Request.SiteId == StochasticSiteRegistryV111
                        .NativePotionFactoryModelSiteId &&
                    value.Request.StreamId ==
                        NativePlayerRngV111.RewardsStreamId)
                    tracePotionFactoryModelDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (StochasticSiteRegistryV111.CombatIntentSiteIds
                    .Contains(value.Request.SiteId, StringComparer.Ordinal))
                    traceIntentDecisions.Add(new RandomIndexDecisionV111(
                        value.Request, value.SelectedIndex, value.ProviderMode,
                        value.NativeCursorBefore, value.NativeCursorAfter));
                if (StochasticSiteRegistryV111
                    .IsNativePolicyCardInstanceIndexAuditSite(
                        value.Request.SiteId))
                    traceCardInstanceDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (StochasticSiteRegistryV111
                    .IsCombatCardEnergyCostAuditSite(value.Request.SiteId))
                    traceCardEnergyCostDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (StochasticSiteRegistryV111.ControlledDrawInsertionSiteIds
                    .Contains(value.Request.SiteId, StringComparer.Ordinal))
                    traceDrawInsertionDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (StochasticSiteRegistryV111.CombatEnemyPileInsertionSiteIds
                    .Contains(value.Request.SiteId, StringComparer.Ordinal))
                    traceEnemyPileInsertionDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (StochasticSiteRegistryV111.NativePolicyPoolShuffleSiteIds
                    .Contains(value.Request.SiteId, StringComparer.Ordinal))
                {
                    var poolShuffleDecision = new RandomIndexDecisionV111(
                        value.Request, value.SelectedIndex,
                        value.ProviderMode, value.NativeCursorBefore,
                        value.NativeCursorAfter);
                    if (StochasticSiteRegistryV111
                        .CombatCardPoolShuffleSiteIds.Contains(
                            value.Request.SiteId, StringComparer.Ordinal))
                        tracePoolShuffleDecisions.Add(poolShuffleDecision);
                    else if (value.Request.SiteId ==
                        NativeFisherYatesV111.SiteId)
                        traceNativeEntryShuffleDecisions.Add(
                            poolShuffleDecision);
                    else if (value.Request.SiteId ==
                        ControlledRecycleShuffleEngineV111.SiteId)
                        traceNativeRecycleShuffleDecisions.Add(
                            poolShuffleDecision);
                    else if (value.Request.SiteId ==
                        PrototypeNativeRelicGrabBag.QueueShuffleSiteId)
                        traceRelicQueueShuffleDecisions.Add(
                            poolShuffleDecision);
                    else if (value.Request.SiteId is
                        StochasticSiteRegistryV111.WarHammerEliteUpgradeShuffleSiteId or
                        StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId or
                        StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId)
                        tracePersistentNativeShuffleDecisions.Add(
                            poolShuffleDecision);
                    else
                        throw new InvalidDataException(
                            "Native policy pool-shuffle site has no audit projection.");
                }
                if (SemanticGeneratedCardNativeAuditV111
                    .IsSupportedAuditSite(value.Request.SiteId))
                    traceGeneratedCardModelDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (value.Request.SiteId ==
                    SemanticCardTransformChoiceV111.SiteId)
                    traceCardTransformDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (StochasticSiteRegistryV111.CombatRandomTargetSiteIds
                    .Contains(value.Request.SiteId, StringComparer.Ordinal))
                    traceRandomTargetDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (value.Request.SiteId ==
                    SemanticPotionGenerationChoiceV111.SiteId)
                    tracePotionGenerationDecisions.Add(
                        new RandomIndexDecisionV111(
                            value.Request, value.SelectedIndex,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
            }
            else if (entry.OperationKind == "unit_float")
            {
                var value = entry
                    .DeserializeEvent<RandomUnitFloatBoundaryEventV111>();
                if (value.Request.SiteId is
                    SemanticCardOddsNativeAuditV111.RaritySiteId or
                    SemanticCardOddsNativeAuditV111.UpgradeSiteId)
                    traceCardOddsDecisions.Add(
                        new RandomUnitFloatDecisionV111(
                            value.Request, value.SelectedValue,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (value.Request.SiteId == StochasticSiteRegistryV111
                        .RewardPotionOddsUnitFloatSiteId)
                    tracePotionRewardOddsDecisions.Add(
                        new RandomUnitFloatDecisionV111(
                            value.Request, value.SelectedValue,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (value.Request.SiteId == StochasticSiteRegistryV111
                        .RelicGrabBagRaritySiteId &&
                    value.Request.StreamId ==
                        NativePlayerRngV111.RewardsStreamId)
                    traceRelicRewardDecisions.Add(
                        new RandomUnitFloatDecisionV111(
                            value.Request, value.SelectedValue,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
                if (value.Request.SiteId == StochasticSiteRegistryV111
                        .NativePotionFactoryRarityRollSiteId &&
                    value.Request.StreamId ==
                        NativePlayerRngV111.RewardsStreamId)
                    tracePotionFactoryRarityDecisions.Add(
                        new RandomUnitFloatDecisionV111(
                            value.Request, value.SelectedValue,
                            value.ProviderMode, value.NativeCursorBefore,
                            value.NativeCursorAfter));
            }
        }

        if (EncounterFormationAuditSchema is
                EncounterFormationAuditSchemaV1 or
                EncounterFormationAuditSchemaV2 ||
            encounterFormationOutcomes.Length > 0)
        {
            if ((EncounterFormationAuditSchema is
                    EncounterFormationAuditSchemaV1 or
                    EncounterFormationAuditSchemaV2) &&
                encounterFormationOutcomes.Length != 1)
                throw new InvalidDataException(
                    "Native source-route audit requires exactly one encounter-formation result.");

            var annotatedFormationChoices =
                new List<RandomIndexDecisionV111>();
            foreach (var outcome in encounterFormationOutcomes)
            {
                if (outcome is null || outcome.Formation is null ||
                    outcome.ProviderMode != "native-v111")
                    throw new InvalidDataException(
                        "Native encounter-formation audit has invalid provider provenance.");
                var formation = outcome.Formation;
                var expectedStream =
                    NativeRngV111Contexts.EncounterFormationStreamId(
                        formation.NativeEncounterEntry,
                        formation.TotalFloor);
                if (string.IsNullOrWhiteSpace(
                        formation.NativeEncounterEntry) ||
                    formation.TotalFloor < 0 ||
                    formation.Enemies.Length == 0 ||
                    formation.MonsterContexts.Length !=
                        formation.Enemies.Length ||
                    formation.EncounterRng.StreamId != expectedStream)
                    throw new InvalidDataException(
                        "Native encounter-formation audit has invalid output provenance.");

                var choices = outcome.Choices;
                for (var index = 0; index < choices.Length; index++)
                {
                    var choice = choices[index];
                    choice.Validate();
                    if (choice.ProviderMode != "native-v111" ||
                        choice.Request.SiteId !=
                            NativeEncounterFormationFactoryV111.FormationSiteId ||
                        choice.Request.StreamId != expectedStream ||
                        choice.Request.OwnerId !=
                            $"encounter:{formation.NativeEncounterEntry}:floor:{formation.TotalFloor}" ||
                        choice.Request.Ordinal != index ||
                        choice.Request.OperationKind != "choose_index")
                        throw new InvalidDataException(
                            "Native encounter-formation audit has an unsupported choice request.");
                    annotatedFormationChoices.Add(choice);
                }
            }

            if (annotatedFormationChoices.Count !=
                traceEncounterFormationDecisions.Count)
                throw new InvalidDataException(
                    "Native encounter-formation outcomes are incomplete for their mixed trace.");
            foreach (var decision in traceEncounterFormationDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(decision);
                if (annotatedFormationChoices.Count(choice =>
                        CanonicalJson.Sha256(choice) == decisionFingerprint) != 1)
                    throw new InvalidDataException(
                        "Native encounter-formation decision does not have exactly one typed result annotation.");
            }

            if (EncounterFormationAuditSchema ==
                    EncounterFormationAuditSchemaV2)
            {
                var formation = encounterFormationOutcomes.Single().Formation;
                var contexts = formation.MonsterContexts;
                var contextIds = contexts.Select(context =>
                    context.CombatId).ToArray();
                var outcomes = encounterFormationOutcomes.Single()
                    .InitialHpOutcomes;
                if (outcomes is null || outcomes.Length != contexts.Length ||
                    !outcomes.Select(outcome => outcome.CombatId)
                        .SequenceEqual(contextIds))
                    throw new InvalidDataException(
                        "Native source-route audit requires one ordered initial-HP result for every formation monster context.");

                var contextOwners = contextIds.Select(id =>
                    "creature:" + id).ToHashSet(StringComparer.Ordinal);
                var expectedHpDecisions = traceInitialHpDecisions
                    .Where(decision => contextOwners.Contains(
                        decision.Request.OwnerId)).ToArray();
                var annotatedHpDecisions = new List<RandomIndexDecisionV111>();
                foreach (var outcome in outcomes)
                {
                    if (outcome is null)
                        throw new InvalidDataException(
                            "Native initial-HP outcome is null.");
                    outcome.Validate();
                    var context = contexts.SingleOrDefault(candidate =>
                        candidate.CombatId == outcome.CombatId);
                    if (context is null || context.Enemy.EnemyId !=
                            outcome.EnemyId)
                        throw new InvalidDataException(
                            "Native initial-HP outcome disagrees with its formation context.");
                    annotatedHpDecisions.Add(outcome.ProviderDecision);
                }
                if (annotatedHpDecisions.Count != expectedHpDecisions.Length)
                    throw new InvalidDataException(
                        "Native initial-HP outcomes are incomplete for the source-route trace.");
                foreach (var decision in expectedHpDecisions)
                {
                    var fingerprint = CanonicalJson.Sha256(decision);
                    if (annotatedHpDecisions.Count(outcome =>
                            CanonicalJson.Sha256(outcome) == fingerprint) != 1)
                        throw new InvalidDataException(
                            "Native initial-HP decision does not have exactly one typed result annotation.");
                }
            }
            else if (encounterFormationOutcomes.Any(outcome =>
                         outcome.InitialHpOutcomes.Length > 0))
                throw new InvalidDataException(
                    "Native initial-HP outcomes require the encounter-formation v2 schema.");
        }

        // Older snapshots have only the compatibility index list. Newer
        // snapshots retain the full mixed trace and project its index entries.
        if ((ProviderTraceSchema == ProviderTraceSchemaV1 ||
             providerTrace.Length > 0) &&
            traceIndices.Count != decisions.Length)
            throw new InvalidDataException(
                "Native policy index projection differs from its mixed trace.");
        for (var index = 0; providerTrace.Length > 0 &&
             index < traceIndices.Count; index++)
        {
            if (CanonicalJson.Sha256(traceIndices[index]) !=
                CanonicalJson.Sha256(decisions[index]))
                throw new InvalidDataException(
                    "Native policy index projection differs from its mixed trace at " +
                    index + ".");
        }

        // The schema marker enforces completeness even for an empty array.
        // A nonempty unmarked array still receives the same consistency check,
        // while older empty arrays retain snapshot compatibility.
        if (WeightedIntentAuditSchema == WeightedIntentAuditSchemaV1 ||
            weightedIntentOutcomes.Length > 0)
        {
            if (traceIntentDecisions.Count != weightedIntentOutcomes.Length)
                throw new InvalidDataException(
                    "Native policy weighted intent outcomes are incomplete for their mixed trace.");

            foreach (var intentDecision in traceIntentDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(intentDecision);
                var matchingOutcomes = weightedIntentOutcomes.Count(outcome =>
                    CanonicalJson.Sha256(outcome.ProviderDecision) ==
                    decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native policy intent ticket does not have exactly one weighted outcome annotation.");
            }
        }

        // The card-instance schema covers every registered source selection
        // in its support; old snapshots retain their unmarked shape.
        bool completeFishingRodCardSelection =
            CardInstanceIndexAuditSchema == CardInstanceIndexAuditSchemaV2;
        var requiredCardInstanceDecisions = completeFishingRodCardSelection
            ? traceCardInstanceDecisions
            : traceCardInstanceDecisions.Where(decision =>
                decision.Request.SiteId !=
                    PrototypeGameEngine.FishingRodUpgradeSiteId).ToList();
        var requiredCardInstanceOutcomes = cardInstanceIndexOutcomes
            .Where(outcome => completeFishingRodCardSelection ||
                outcome.Request.SiteId !=
                    PrototypeGameEngine.FishingRodUpgradeSiteId).ToArray();
        if (CardInstanceIndexAuditSchema is
                CardInstanceIndexAuditSchemaV1 or
                CardInstanceIndexAuditSchemaV2 ||
            cardInstanceIndexOutcomes.Length > 0)
        {
            if (requiredCardInstanceDecisions.Count !=
                requiredCardInstanceOutcomes.Length)
                throw new InvalidDataException(
                    "Native policy card-instance outcomes are incomplete for their mixed trace.");

            foreach (var cardDecision in requiredCardInstanceDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(cardDecision);
                var matchingOutcomes = requiredCardInstanceOutcomes.Count(outcome =>
                    CanonicalJson.Sha256(outcome.ProviderDecision) ==
                    decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native card-instance index decision does not have exactly one semantic identity annotation.");
            }
        }

        bool completeAllCardCostSites =
            CardEnergyCostAuditSchema == CardEnergyCostAuditSchemaV2;
        var requiredCardCostDecisions = completeAllCardCostSites
            ? traceCardEnergyCostDecisions
            : traceCardEnergyCostDecisions.Where(decision =>
                decision.Request.SiteId == StochasticSiteRegistryV111
                    .CombatRandomHandCardEnergyCostSiteId).ToList();
        var requiredCardCostOutcomes = completeAllCardCostSites
            ? cardEnergyCostOutcomes
            : cardEnergyCostOutcomes.Where(outcome =>
                outcome.Outcome.Event.Request.SiteId ==
                    StochasticSiteRegistryV111
                        .CombatRandomHandCardEnergyCostSiteId).ToArray();
        if (CardEnergyCostAuditSchema is
                CardEnergyCostAuditSchemaV1 or
                CardEnergyCostAuditSchemaV2 ||
            cardEnergyCostOutcomes.Length > 0)
        {
            if (requiredCardCostDecisions.Count !=
                requiredCardCostOutcomes.Length)
                throw new InvalidDataException(
                    "Native policy card energy-cost outcomes are incomplete for their mixed trace.");

            foreach (var costDecision in requiredCardCostDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(costDecision);
                var matchingOutcomes = requiredCardCostOutcomes.Count(outcome =>
                    CanonicalJson.Sha256(outcome.ProviderDecision) ==
                    decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native card energy-cost decision does not have exactly one typed outcome annotation.");
            }
        }

        if (DrawInsertionAuditSchema == DrawInsertionAuditSchemaV1)
        {
            if (traceDrawInsertionDecisions.Count !=
                drawInsertionOutcomes.Length)
                throw new InvalidDataException(
                    "Native policy draw-insertion outcomes are incomplete for their mixed trace.");

            foreach (var insertionDecision in traceDrawInsertionDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(
                    insertionDecision);
                var matchingOutcomes = drawInsertionOutcomes.Count(outcome =>
                    CanonicalJson.Sha256(outcome.ProviderDecision) ==
                    decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native draw-insertion decision does not have exactly one typed support annotation.");
            }
        }

        if (EnemyPileInsertionAuditSchema ==
            EnemyPileInsertionAuditSchemaV1)
        {
            if (traceEnemyPileInsertionDecisions.Count !=
                enemyPileInsertionOutcomes.Length)
                throw new InvalidDataException(
                    "Native policy enemy pile-insertion outcomes are incomplete for their mixed trace.");

            foreach (var insertionDecision in
                traceEnemyPileInsertionDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(
                    insertionDecision);
                var matchingOutcomes = enemyPileInsertionOutcomes.Count(
                    outcome => CanonicalJson.Sha256(
                        outcome.ProviderDecision) == decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native enemy pile-insertion decision does not have exactly one typed support annotation.");
            }
        }

        bool completeRelicQueueShuffles = PoolShuffleAuditSchema is
            PoolShuffleAuditSchemaV2 or PoolShuffleAuditSchemaV3 or
            PoolShuffleAuditSchemaV4;
        bool completeNativeDrawPileShuffles =
            PoolShuffleAuditSchema is PoolShuffleAuditSchemaV3 or
                PoolShuffleAuditSchemaV4;
        bool completePersistentNativeShuffles =
            PoolShuffleAuditSchema == PoolShuffleAuditSchemaV4;
        var requiredPoolShuffleDecisions =
            new List<RandomIndexDecisionV111>(tracePoolShuffleDecisions);
        if (completeNativeDrawPileShuffles)
        {
            requiredPoolShuffleDecisions.AddRange(
                traceNativeEntryShuffleDecisions);
            requiredPoolShuffleDecisions.AddRange(
                traceNativeRecycleShuffleDecisions);
        }
        if (completeRelicQueueShuffles)
            requiredPoolShuffleDecisions.AddRange(
                traceRelicQueueShuffleDecisions);
        if (completePersistentNativeShuffles)
            requiredPoolShuffleDecisions.AddRange(
                tracePersistentNativeShuffleDecisions);
        var requiredPoolShuffleOutcomes = poolShuffleOutcomes.Where(outcome =>
            StochasticSiteRegistryV111.CombatCardPoolShuffleSiteIds
                .Contains(outcome.Event.Request.SiteId, StringComparer.Ordinal) ||
            (completeNativeDrawPileShuffles &&
             (outcome.Event.Request.SiteId is
                 NativeFisherYatesV111.SiteId or
                 ControlledRecycleShuffleEngineV111.SiteId)) ||
            (completeRelicQueueShuffles && outcome.Event.Request.SiteId ==
                PrototypeNativeRelicGrabBag.QueueShuffleSiteId) ||
            (completePersistentNativeShuffles &&
             (outcome.Event.Request.SiteId is
                 StochasticSiteRegistryV111.WarHammerEliteUpgradeShuffleSiteId or
                 StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId or
                 StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId)))
            .ToArray();
        if (PoolShuffleAuditSchema is
                PoolShuffleAuditSchemaV1 or PoolShuffleAuditSchemaV2 or
                PoolShuffleAuditSchemaV3 or PoolShuffleAuditSchemaV4 ||
            poolShuffleOutcomes.Length > 0)
        {
            if (requiredPoolShuffleDecisions.Count !=
                requiredPoolShuffleOutcomes.Length)
                throw new InvalidDataException(
                    "Native policy pool-shuffle outcomes are incomplete for their mixed trace.");

            foreach (var poolDecision in requiredPoolShuffleDecisions)
            {
                var matchingOutcomes = requiredPoolShuffleOutcomes.Count(outcome =>
                    CanonicalJson.Sha256(outcome.Event.Request) ==
                        CanonicalJson.Sha256(poolDecision.Request) &&
                    outcome.SelectedSwapIndex == poolDecision.SelectedIndex);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native pool-shuffle decision does not have exactly one ordered-support annotation.");
            }
        }

        bool completeNativeRewardCardModels =
            GeneratedCardModelAuditSchema ==
                GeneratedCardModelAuditSchemaV2;
        var requiredGeneratedCardModelDecisions =
            completeNativeRewardCardModels
                ? traceGeneratedCardModelDecisions
                : traceGeneratedCardModelDecisions.Where(decision =>
                    decision.Request.SiteId !=
                        StochasticSiteRegistryV111
                            .NativeRewardCardModelSiteId).ToList();
        var requiredGeneratedCardModelOutcomes =
            generatedCardModelOutcomes.Where(outcome =>
                completeNativeRewardCardModels ||
                outcome.Outcome.Event.SiteId !=
                    StochasticSiteRegistryV111
                        .NativeRewardCardModelSiteId).ToArray();
        if (GeneratedCardModelAuditSchema is
                GeneratedCardModelAuditSchemaV1 or
                GeneratedCardModelAuditSchemaV2 ||
            generatedCardModelOutcomes.Length > 0)
        {
            if (requiredGeneratedCardModelDecisions.Count !=
                requiredGeneratedCardModelOutcomes.Length)
                throw new InvalidDataException(
                    "Native generated-card model outcomes are incomplete for their mixed trace.");

            foreach (var generatedDecision in
                requiredGeneratedCardModelDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(
                    generatedDecision);
                var matchingOutcomes =
                    requiredGeneratedCardModelOutcomes.Count(
                    outcome => CanonicalJson.Sha256(
                        outcome.ProviderDecision) == decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native generated-card model decision does not have exactly one typed outcome annotation.");
            }
        }

        if (CardOddsAuditSchema == CardOddsAuditSchemaV1 ||
            cardOddsOutcomes.Length > 0)
        {
            if (traceCardOddsDecisions.Count != cardOddsOutcomes.Length)
                throw new InvalidDataException(
                    "Native card-odds outcomes are incomplete for their mixed trace.");

            foreach (var cardOddsDecision in traceCardOddsDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(
                    cardOddsDecision);
                var matchingOutcomes = cardOddsOutcomes.Count(outcome =>
                    CanonicalJson.Sha256(outcome.ProviderDecision) ==
                        decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native card-odds float decision does not have exactly one typed outcome annotation.");
            }
        }

        if (PotionRewardOddsAuditSchema ==
                PotionRewardOddsAuditSchemaV1 ||
            potionRewardOddsOutcomes.Length > 0)
        {
            if (tracePotionRewardOddsDecisions.Count !=
                potionRewardOddsOutcomes.Length)
                throw new InvalidDataException(
                    "Native potion-reward odds outcomes are incomplete for their mixed trace.");

            foreach (var potionOddsDecision in
                tracePotionRewardOddsDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(
                    potionOddsDecision);
                var matchingOutcomes = potionRewardOddsOutcomes.Count(
                    outcome => CanonicalJson.Sha256(
                        outcome.ProviderDecision) == decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native potion-reward odds float decision does not have exactly one typed outcome annotation.");
            }
        }

        if (PotionFactoryAuditSchema == PotionFactoryAuditSchemaV1 ||
            potionFactoryOutcomes.Length > 0)
        {
            if (tracePotionFactoryRarityDecisions.Count !=
                    potionFactoryOutcomes.Length ||
                tracePotionFactoryModelDecisions.Count !=
                    potionFactoryOutcomes.Length)
                throw new InvalidDataException(
                    "Native PotionFactory outcomes are incomplete for their mixed trace.");

            foreach (var potionRarityDecision in
                tracePotionFactoryRarityDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(
                    potionRarityDecision);
                var matchingOutcomes = potionFactoryOutcomes.Count(
                    outcome => CanonicalJson.Sha256(
                        outcome.RarityDecision) == decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native PotionFactory rarity decision does not have exactly one typed outcome annotation.");
            }

            foreach (var potionModelDecision in
                tracePotionFactoryModelDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(
                    potionModelDecision);
                var matchingOutcomes = potionFactoryOutcomes.Count(
                    outcome => CanonicalJson.Sha256(
                        outcome.ModelDecision) == decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native PotionFactory model decision does not have exactly one typed outcome annotation.");
            }
        }

        if (CombatGoldAuditSchema == CombatGoldAuditSchemaV1 ||
            combatGoldOutcomes.Length > 0)
        {
            if (traceCombatGoldDecisions.Count !=
                combatGoldOutcomes.Length)
                throw new InvalidDataException(
                    "Native combat-gold outcomes are incomplete for their mixed trace.");
            foreach (var goldDecision in traceCombatGoldDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(goldDecision);
                var matchingOutcomes = combatGoldOutcomes.Count(outcome =>
                    CanonicalJson.Sha256(outcome.ProviderDecision) ==
                    decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native combat-gold decision does not have exactly one typed outcome annotation.");
            }
        }

        if (RelicRewardAuditSchema == RelicRewardAuditSchemaV1 ||
            relicRewardOutcomes.Length > 0)
        {
            if (traceRelicRewardDecisions.Count !=
                relicRewardOutcomes.Length)
                throw new InvalidDataException(
                    "Native relic-reward outcomes are incomplete for their mixed trace.");
            foreach (var relicDecision in traceRelicRewardDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(relicDecision);
                var matchingOutcomes = relicRewardOutcomes.Count(outcome =>
                    CanonicalJson.Sha256(outcome.ProviderDecision) ==
                    decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native relic-reward float decision does not have exactly one typed outcome annotation.");
            }
        }

        if (CardTransformAuditSchema == CardTransformAuditSchemaV1 ||
            cardTransformOutcomes.Length > 0)
        {
            if (traceCardTransformDecisions.Count !=
                cardTransformOutcomes.Length)
                throw new InvalidDataException(
                    "Native card-transform outcomes are incomplete for their mixed trace.");

            foreach (var transformDecision in traceCardTransformDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(
                    transformDecision);
                var matchingOutcomes = cardTransformOutcomes.Count(
                    outcome => CanonicalJson.Sha256(
                        outcome.ProviderDecision) == decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native card-transform decision does not have exactly one typed outcome annotation.");
            }
        }

        if (RandomTargetAuditSchema == RandomTargetAuditSchemaV1 ||
            randomTargetOutcomes.Length > 0)
        {
            if (traceRandomTargetDecisions.Count !=
                randomTargetOutcomes.Length)
                throw new InvalidDataException(
                    "Native policy random-target outcomes are incomplete for their mixed trace.");

            foreach (var targetDecision in traceRandomTargetDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(
                    targetDecision);
                var matchingOutcomes = randomTargetOutcomes.Count(outcome =>
                    CanonicalJson.Sha256(outcome.ProviderDecision) ==
                    decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native random-target decision does not have exactly one selected-identity annotation.");
            }
        }

        if (PotionGenerationAuditSchema ==
                PotionGenerationAuditSchemaV1 ||
            potionGenerationOutcomes.Length > 0)
        {
            if (tracePotionGenerationDecisions.Count !=
                potionGenerationOutcomes.Length)
                throw new InvalidDataException(
                    "Native policy potion-generation outcomes are incomplete for their mixed trace.");

            foreach (var potionDecision in tracePotionGenerationDecisions)
            {
                var decisionFingerprint = CanonicalJson.Sha256(
                    potionDecision);
                var matchingOutcomes = potionGenerationOutcomes.Count(
                    outcome => CanonicalJson.Sha256(
                        outcome.ProviderDecision) == decisionFingerprint);
                if (matchingOutcomes != 1)
                    throw new InvalidDataException(
                        "Native potion-generation decision does not have exactly one model annotation.");
            }
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

    private string[]? _actionIds = CopyActionIds(ActionIds);
    private RandomBoundaryEventV111[]? _nativeEntryDecisions =
        CloneEvents(NativeEntryDecisions);

    public string[] ActionIds
    {
        get => CopyActionIds(_actionIds)!;
        init => _actionIds = CopyActionIds(value);
    }
    public RandomBoundaryEventV111[] NativeEntryDecisions
    {
        get => CloneEvents(_nativeEntryDecisions)!;
        init => _nativeEntryDecisions = CloneEvents(value);
    }

    public SemanticSourceRouteAuditV111 Fork() => this with
    {
        ActionIds = CopyActionIds(_actionIds)!,
        NativeEntryDecisions = CloneEvents(_nativeEntryDecisions)!
    };

    private static string[]? CopyActionIds(string[]? values) =>
        values is null ? null : (string[])values.Clone();

    private static RandomBoundaryEventV111[]? CloneEvents(
        RandomBoundaryEventV111[]? values) => values?.Select(value =>
            value is null ? null! : value.Fork()).ToArray();
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
    string? FixtureTopAutoplayPotionId = null);

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

    public SemanticPilotContinuationV111[] History
    {
        get => Mechanics.History;
        init => Mechanics = Mechanics with { History = value };
    }

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
            { } continuation && continuation.Mechanics.IsAtOpeningBoundary(
                PrototypeOpeningCombatStage.InitialEnemyIntents)
                    ? continuation.Stochastic.ControlledTurnIntents : null;
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
        Stochastic.SeededEntry?.NativePolicyBaseline ?? false;
    [JsonIgnore] public string? SourceOpeningFingerprint =>
        Stochastic.SeededEntry is { } entry
            ? CanonicalJson.Sha256(new
            {
                Entry = entry with { NativePolicyBaseline = false },
                OpeningSource,
                Stochastic.SourceRouteAudit
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
                combat.Stochastic.SemanticDeck is
                    { Pending: { Purpose: "draw" } } deck
                    ? SemanticDeckEngineV111.Inspect(deck) : null;
        }
    }
    [JsonIgnore] public SemanticDeckEventV111? DeckRevealPending
    {
        get
        {
            var deck = Run.World?.Combat?.Stochastic.SemanticDeck;
            return deck?.Pending?.Purpose is "peek" or "scry"
                ? SemanticDeckEngineV111.Inspect(deck)
                : null;
        }
    }
    [JsonIgnore] public SemanticRandomPileCardEventV111?
        RandomPileCardPending => PendingEndTurn?.RandomPileCardPending;

    public void Validate()
    {
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
        foreach (var transition in this.History)
            (transition ?? throw new InvalidDataException(
                "Semantic pilot history contains a null transition."))
                .Validate();
        if (this.PendingEndTurn is { } endTurn)
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
            if (initialIntents.Complete || this.PendingEndTurn is not null ||
                this.Stochastic.SeededEntry?.SourceRouteEntry != true ||
                this.Run.World?.Combat is not { } entryCombat ||
                CanonicalJson.Sha256(
                    PrototypeGameEngine.ClearAutomaticPipelineContinuation(
                        entryCombat)) !=
                    CanonicalJson.Sha256(
                        PrototypeGameEngine.ClearAutomaticPipelineContinuation(
                            initialIntents.BaselineCombat)) ||
                entryCombat.Stochastic.NativeRng is not null ||
                entryCombat.Stochastic.SemanticDeck is not null ||
                this.Run.Stochastic.NativeRunRng is not null)
                throw new InvalidDataException(
                    "Initial intent continuation diverged from the source-route boundary.");
        }
        if (this.Run.World?.Combat?.PendingChoice is
                { ChoiceId: "controlled_semantic_draw",
                  ControlledSemanticDraw: not null })
        {
            SemanticDrawChoiceV111.ValidateBoundary(this.Run);
            if ((this.PendingEndTurn is not null &&
                 !this.HasPendingEndTurnNestedSemanticChoice) ||
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
            if ((this.PendingEndTurn is not null &&
                 !this.HasPendingEndTurnNestedSemanticChoice) ||
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
            if (this.PendingEndTurn is null ||
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
            if (this.PendingEndTurn is null ||
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
            if (this.PendingEndTurn is null ||
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
            if ((this.PendingEndTurn is not null &&
                 this.PendingEndTurn.TargetPending is null) ||
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
                (this.PendingEndTurn is not null &&
                 this.PendingEndTurn.GeneratedCardPending is null) ||
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
                (this.PendingEndTurn is not null &&
                 this.PendingEndTurn.PotionGenerationPending is null) ||
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
                (this.PendingEndTurn is not null &&
                 this.PendingEndTurn.CardTransformPending is null) ||
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
            if ((this.PendingEndTurn is not null &&
                 this.PendingEndTurn.NestedCardSelectionPending is null) ||
                this.PendingInitialIntents is not null ||
                this.TargetPending is not null ||
                this.GeneratedCardPending is not null ||
                this.UsesNativePolicyBaseline || this.CombatTerminal)
                throw new InvalidDataException(
                    "Card-selection continuation conflicts with another boundary.");
        }
        if (this.DeckRevealPending is { } deckRevealPending)
        {
            var deck = this.Run.World?.Combat?.Stochastic.SemanticDeck ??
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
                this.PendingEndTurn is not null ||
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
                this.PendingEndTurn?.PoolShufflePending is not null;
            bool handReturnShuffle = poolShufflePending.SourceOperationKind ==
                nameof(PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle);
            bool recycleShuffle = poolShufflePending.SourceOperationKind ==
                nameof(PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw);
            bool randomRareToHandShuffle =
                poolShufflePending.SourceOperationKind ==
                    nameof(PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand);
            bool randomShortlistShuffle = poolShufflePending.SourceOperationKind ==
                nameof(PrototypeCombatEffectKind.ChooseCards);
            bool colorlessPotionShuffle =
                IsColorlessPotionGeneratedChoiceShuffle(
                    combat, combat?.PendingChoice, poolShufflePending);
            bool anointedRareShuffle = IsAnointedRareToHandShuffle(
                combat, combat?.PendingChoice, poolShufflePending);
            bool seekerStrikeShuffle = IsSeekerStrikeDrawPileShortlistShuffle(
                combat, combat?.PendingChoice, poolShufflePending);
            bool discoveryShuffle = IsDiscoveryGeneratedChoiceShuffle(
                combat, combat?.PendingChoice, poolShufflePending);
            bool splashShuffle = IsSplashGeneratedChoiceShuffle(
                combat, combat?.PendingChoice, poolShufflePending);
            bool jackOfAllTradesShuffle =
                IsJackOfAllTradesDistinctColorlessShuffle(
                    combat, combat?.PendingChoice, poolShufflePending);
            bool neowsAbundanceShuffle = IsNeowsAbundanceGeneratedChoiceShuffle(
                combat, combat?.PendingChoice,
                combat?.PendingChoice?.ControlledPoolShuffle);
            var fixtureMatches = automaticPipelinePoolShuffle ||
                handReturnShuffle || recycleShuffle ||
                colorlessPotionShuffle || anointedRareShuffle ||
                seekerStrikeShuffle || discoveryShuffle || splashShuffle ||
                jackOfAllTradesShuffle ||
                neowsAbundanceShuffle ||
                (randomRareToHandShuffle
                    ? this.Stochastic.SeededEntry?.FixtureSelectionCardId ==
                        "proto.colorless.anointed"
                    : randomShortlistShuffle
                        ? this.Stochastic.SeededEntry?.FixtureSelectionCardId is not null
                        : this.Stochastic.SeededEntry?.FixturePoolGenerationPotionId ==
                            "proto.potion.colorless");
            if ((!fixtureMatches && !automaticEndTurnShuffle) ||
                (this.PendingEndTurn is not null &&
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
            if ((this.PendingEndTurn is not null &&
                 this.PendingEndTurn.DrawInsertionPending is null) ||
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
            if ((this.PendingEndTurn is not null &&
                 this.PendingEndTurn.CardShortlistPending is null) ||
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
            if ((this.PendingEndTurn is not null &&
                 this.PendingEndTurn.NestedHandCostSelectionPending is null) ||
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
                        : this.PendingEndTurn?.CostPending is null) ||
                (this.PendingEndTurn is not null && automaticHandCost &&
                 this.PendingEndTurn.NestedCostPending is null))
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
        if (this.Stochastic.SeededEntry is { } entry)
        {
            var routeCostProviderExpected = entry.SourceRouteEntry &&
                !entry.NativePolicyBaseline;
            var otherChanceExpected = entry.OtherChanceProviderId is not null ||
                routeCostProviderExpected;
            if (entry.Seed != this.OpeningSource.Seed ||
                entry.Act != this.OpeningSource.Origin.Combat.Act ||
                entry.NativeEncounterEntry != this.NativeEncounterEntry ||
                entry.TotalFloor != this.OpeningSource.Origin.Formation.TotalFloor ||
                otherChanceExpected !=
                    (this.Run.Stochastic.SemanticOtherChance is not null) ||
                (entry.OtherChanceProviderId is not null &&
                 entry.OtherChanceProviderId !=
                    this.Run.Stochastic.SemanticOtherChance?.ProviderId) ||
                (routeCostProviderExpected &&
                 !SemanticOtherChanceEngineV111.IsSupportedProviderId(
                     this.Run.Stochastic.SemanticOtherChance?.ProviderId)))
                throw new InvalidDataException(
                    "Seeded Nature entry provenance disagrees with its opening.");
        }
        if (this.Stochastic.SeededEntry is { SourceRouteEntry: true } routeEntry)
        {
            var route = this.Stochastic.SourceRouteAudit ?? throw new InvalidDataException(
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
        else if (this.Stochastic.SourceRouteAudit is not null)
            throw new InvalidDataException(
                "Fixture semantic session cannot retain a source-route audit.");
        if (this.UsesNativePolicyBaseline)
        {
            if (this.Run.Stochastic.SemanticOtherChance is not null ||
                this.PendingEndTurn is not null ||
                this.PendingInitialIntents is not null)
                throw new InvalidDataException(
                    "Native policy baseline cannot retain Nature-only continuations.");
            var nativeAudit = this.Stochastic.NativePolicyAudit ??
                throw new InvalidDataException(
                    "Native policy baseline is missing provider provenance.");
            if (this.Stochastic.SeededEntry is
                    { SourceRouteEntry: true } &&
                (nativeAudit.EncounterFormationAuditSchema is
                    SemanticNativePolicyAuditV111
                        .EncounterFormationAuditSchemaV1 or
                    SemanticNativePolicyAuditV111
                        .EncounterFormationAuditSchemaV2))
            {
                var routeAudit = this.Stochastic.SourceRouteAudit ??
                    throw new InvalidDataException(
                        "Native source-route formation audit has no route record.");
                var formationAudit = nativeAudit
                    .EncounterFormationOutcomes.Single();
                var routeFormationChoices = routeAudit.NativeEntryDecisions
                    .Where(decision => decision.Request.SiteId ==
                        NativeEncounterFormationFactoryV111.FormationSiteId)
                    .Select(decision => new RandomIndexDecisionV111(
                        decision.Request, decision.SelectedIndex,
                        decision.ProviderMode, decision.NativeCursorBefore,
                        decision.NativeCursorAfter))
                    .ToArray();
                if (CanonicalJson.Sha256(formationAudit.Formation) !=
                        CanonicalJson.Sha256(
                            this.OpeningSource.Origin.Formation) ||
                    formationAudit.Formation.NativeEncounterEntry !=
                        routeAudit.NativeEncounterEntry ||
                    CanonicalJson.Sha256(formationAudit.Choices) !=
                        CanonicalJson.Sha256(routeFormationChoices))
                    throw new InvalidDataException(
                        "Native encounter-formation audit disagrees with the saved source route or combat origin.");
                if (nativeAudit.EncounterFormationAuditSchema ==
                    SemanticNativePolicyAuditV111
                        .EncounterFormationAuditSchemaV2)
                {
                    foreach (var outcome in
                             formationAudit.InitialHpOutcomes)
                    {
                        var enemy = this.OpeningSource.Origin.Combat.Enemies
                            .SingleOrDefault(candidate =>
                                candidate.InstanceId == outcome.CombatId);
                        if (enemy is null || enemy.EnemyId !=
                                outcome.EnemyId ||
                            enemy.MaxHp != outcome.SelectedHp)
                            throw new InvalidDataException(
                                "Native initial-HP outcome disagrees with the source-route combat origin.");
                    }
                }
            }
            if (this.AtPlayerBoundary)
                SemanticPilotCombatEngineV111.ValidateNativePolicyBoundary(this.Run);
            else
                NativeRunRngV111.Validate(this.Run);
        }
        else if (this.AtPlayerBoundary)
            SemanticPlayerActionEngineV111.ValidateBoundary(this.Run);
        else if (this.Stochastic.NativePolicyAudit is not null)
            throw new InvalidDataException(
                "Nature-controlled sessions cannot retain native policy decisions.");
        if (this.CombatTerminal && this.PendingEndTurn is not null)
            throw new InvalidDataException(
                "Terminal combat retained an unresolved chance.");
    }

    private bool HasPendingEndTurnNestedSemanticChoice =>
        this.PendingEndTurn is { Complete: false } endTurn &&
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
        History = History.Select(transition => transition.Fork()).ToArray(),
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
            combat.Stochastic.SemanticDeck is not null ||
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
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Seed);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.EncounterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.NativeEncounterEntry);
        if (entry.Act is not (1 or 2 or 3) || entry.TotalFloor < 1 ||
            entry.FirstMonsterCombatId < 1)
            throw new ArgumentOutOfRangeException(nameof(entry),
                "Seeded Nature combat requires a positive floor and monster IDs.");
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
                entry.MapColumn, entry.MapRow)
            : PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
                entry.Seed, entry.Act, entry.EncounterId,
                entry.NativeEncounterEntry, entry.TotalFloor, entry.Ascension,
                entry.FirstMonsterCombatId, entry.MapColumn, entry.MapRow);
        var rng = prepared.NativeStreams.Fork();
        using var setupJournal = RandomBoundaryJournalV111.Begin();
        var nativeProvider = new NativeRandomIndexProviderV111();
        var hp = ControlledInitialHpV111.Begin(
            prepared.Combat, prepared.Formation, prepared.NativeStreams);
        while (!hp.Complete)
        {
            var pending = hp.Pending!;
            var decision = StochasticProviderScopeV111.ResolveIndex(
                nativeProvider, pending, rng);
            hp = ControlledInitialHpV111.Resolve(
                hp, hp.Fingerprint, decision.SelectedIndex);
        }
        var intents = ControlledIntentsV111.BeginAfterControlledHp(hp);
        while (!intents.Complete)
        {
            var pending = intents.Pending!;
            var decision = StochasticProviderScopeV111.ResolveIndex(
                nativeProvider, pending, rng);
            intents = ControlledIntentsV111.Resolve(
                intents, intents.Fingerprint, decision.SelectedIndex);
        }
        var opening = ControlledPlayableCombatEngineV111.Begin(
            intents, entry.Seed, entry.EncounterId);
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
        if (entry.OtherChanceProviderId is not null &&
            !SemanticOtherChanceEngineV111.IsSupportedProviderId(
                entry.OtherChanceProviderId))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE provider=" +
                entry.OtherChanceProviderId);
        var state = Begin(opening) with
        {
            SeededEntry = entry,
            OtherChance = entry.OtherChanceProviderId is null
                ? null : SemanticOtherChanceEngineV111.Begin(
                    rng, entry.OtherChanceProviderId)
        };
        if (entry.ReplayOpeningDrawHooks)
            state = ReapplyCommittedOpeningThroughDrawCards(state);
        if (entry.NativePolicyBaseline)
            state = AttachNativePolicyBaseline(state, rng,
                opening.Origin.Formation,
                setupJournal.Entries.Where(decision =>
                    decision.ProviderMode == "native-v111").ToArray(),
                setupJournal.EntriesForProvider("native-v111"),
                setupJournal.CardInstanceIndexOutcomes.ToArray(),
                setupJournal.CardEnergyCostOutcomes.ToArray(),
                setupJournal.DrawInsertionOutcomes.ToArray(),
                setupJournal.EnemyPileInsertionOutcomes.ToArray(),
                setupJournal.PoolShuffleOutcomes.ToArray(),
                setupJournal.GeneratedCardModelOutcomes.ToArray(),
                setupJournal.CardTransformOutcomes.ToArray(),
                setupJournal.RandomTargetOutcomes.ToArray(),
                setupJournal.PotionGenerationOutcomes.ToArray(),
                setupJournal.CardOddsOutcomes.ToArray(),
                setupJournal.PotionRewardOddsOutcomes.ToArray(),
                setupJournal.PotionFactoryOutcomes.ToArray(),
                setupJournal.CombatGoldOutcomes.ToArray(),
                setupJournal.RelicRewardOutcomes.ToArray());
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
        potionId is ("proto.potion.attack" or
            "proto.potion.skill" or
            "proto.potion.power" or
            "proto.potion.touch_of_insanity" or
            "proto.potion.droplet_of_precognition" or
            "proto.potion.gamblers_brew" or
            "proto.potion.liquid_memories");

    private static bool IsSemanticEffectDrawPotion(string? potionId) =>
        potionId is ("proto.native.underdocks.glowwater_potion" or
            "proto.potion.clarity" or
            "proto.potion.cure_all" or
            "proto.potion.snecko_oil" or
            "proto.potion.swift");

    private static bool IsSemanticDeterministicCombatPotion(
        string? potionId) =>
        potionId is ("proto.potion.cunning" or
            "proto.potion.binding" or
            "proto.potion.ship_in_a_bottle" or
            "proto.potion.heart_of_iron" or
            "proto.potion.gigantification" or
            "proto.potion.shackling" or
            "proto.potion.mazaleths_gift" or
            "proto.potion.radiant_tincture" or
            "proto.potion.lucky_tonic" or
            "proto.potion.block" or
            "proto.potion.explosive" or
            "proto.potion.explosive_ampoule" or
            "proto.potion.dexterity" or
            "proto.potion.fysh_oil" or
            "proto.potion.energy" or
            "proto.potion.strength" or
            "proto.potion.regen" or
            "proto.potion.duplicator" or
            "proto.potion.flex" or
            "proto.potion.blessing_of_the_forge" or
            "proto.potion.speed" or
            "proto.potion.liquid_bronze" or
            "proto.potion.ghost_in_a_jar" or
            "proto.potion.fortifier" or
            "proto.potion.stable_serum");

    private static bool IsSemanticDeterministicTargetPotion(
        string? potionId) =>
        potionId is ("proto.potion.beetle_juice" or
            "proto.potion.powdered_demise" or
            "proto.potion.poison" or
            "proto.potion.weak" or
            "proto.potion.vulnerable" or
            "proto.potion.fire");

    private static bool IsSemanticDeterministicDamagePotion(
        string? potionId) =>
        potionId is ("proto.potion.fire" or
            "proto.potion.explosive" or
            "proto.potion.explosive_ampoule");

    private static bool IsSemanticEffectDrawCard(string cardId) =>
        cardId is ("proto.common.restlessness" or
            "proto.status.slimed" or
            "proto.colorless.finesse" or
            "proto.colorless.flash_of_steel" or
            "proto.colorless.impatience" or
            "proto.colorless.master_of_strategy" or
            "proto.colorless.scrawl" or
            "proto.colorless.thinking_ahead" or
            "proto.silent.backflip" or
            "proto.silent.quick_slash" or
            "proto.silent.adrenaline" or
            "proto.silent.calculated_gamble" or
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
        if (cardId is not ("proto.native.neow.abundance" or
            "proto.silent.survivor" or
            "proto.colorless.seeker_strike" or
            "proto.colorless.anointed" or
            "proto.colorless.discovery" or
            "proto.colorless.splash" or
            "proto.colorless.volley" or
            "proto.colorless.jack_of_all_trades" or
            "proto.colorless.secret_technique" or
            "proto.colorless.secret_weapon" or
            "proto.colorless.purity" or
            "proto.colorless.thinking_ahead" or
            "proto.colorless.scrawl" or
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
            combat.Stochastic.NativeRng is not { } nativeCombat ||
            run.Stochastic.NativeRunRng is null)
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
        var nativeFormationAudit = new NativeEncounterFormationAuditV111(
            nativeCombat.Formation.Fork(),
            nativeDecisions.Where(decision => decision.Request.SiteId ==
                    NativeEncounterFormationFactoryV111.FormationSiteId)
                .Select(decision => new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter))
                .ToArray(),
            "native-v111")
        {
            InitialHpOutcomes = setupJournal.Entries
                .Where(decision => decision.ProviderMode == "native-v111" &&
                    decision.Request.SiteId ==
                        NativeCombatRngEntryV111.InitialUniqueHpSiteId)
                .Select(decision =>
                {
                    var providerDecision = new RandomIndexDecisionV111(
                        decision.Request, decision.SelectedIndex,
                        decision.ProviderMode,
                        decision.NativeCursorBefore,
                        decision.NativeCursorAfter);
                    var enemy = combat.Enemies.Single(candidate =>
                        decision.Request.OwnerId ==
                            "creature:" + candidate.InstanceId);
                    return new NativeInitialHpOutcomeV111(
                        enemy.InstanceId, enemy.EnemyId,
                        decision.Request.OrderedCandidates,
                        providerDecision.SelectedValue, providerDecision);
                }).ToArray()
        };
        var nativePolicyTrace = setupJournal.EntriesForProvider("native-v111");
        var nativeCardInstanceOutcomes =
            setupJournal.CardInstanceIndexOutcomes.ToArray();
        var nativeCardEnergyCostOutcomes =
            setupJournal.CardEnergyCostOutcomes.ToArray();
        var nativeDrawInsertionOutcomes =
            setupJournal.DrawInsertionOutcomes.ToArray();
        var nativeEnemyPileInsertionOutcomes =
            setupJournal.EnemyPileInsertionOutcomes.ToArray();
        var nativePoolShuffleOutcomes =
            setupJournal.PoolShuffleOutcomes.ToArray();
        var nativeGeneratedCardModelOutcomes =
            setupJournal.GeneratedCardModelOutcomes.ToArray();
        var nativeCardTransformOutcomes =
            setupJournal.CardTransformOutcomes.ToArray();
        var nativeRandomTargetOutcomes =
            setupJournal.RandomTargetOutcomes.ToArray();
        var nativePotionGenerationOutcomes =
            setupJournal.PotionGenerationOutcomes.ToArray();
        var nativeCardOddsOutcomes =
            setupJournal.CardOddsOutcomes.ToArray();
        var nativePotionRewardOddsOutcomes =
            setupJournal.PotionRewardOddsOutcomes.ToArray();
        var nativePotionFactoryOutcomes =
            setupJournal.PotionFactoryOutcomes.ToArray();
        var nativeCombatGoldOutcomes =
            setupJournal.CombatGoldOutcomes.ToArray();
        var nativeRelicRewardOutcomes =
            setupJournal.RelicRewardOutcomes.ToArray();
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
        var baselineCombat = combat.Fork();
        baselineCombat = baselineCombat with
        {
            Stochastic = CombatStochasticStateV111.WithSemanticDeck(
                baselineCombat.Stochastic, null)
        };
        var nativeFreeBaselineCombat = baselineCombat with
        {
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                baselineCombat.Stochastic, null)
        };
        var nativeFreeBaselineFork = baselineCombat.Fork();
        nativeFreeBaselineFork = nativeFreeBaselineFork with
        {
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                nativeFreeBaselineFork.Stochastic, null)
        };
        var origin = new ControlledIntentStateV111(
            nativeFreeBaselineFork,
            nativeFreeBaselineCombat,
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
        SemanticIntentAuditEntryV111[] nativeIntentOutcomes = [];
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
                        combat, RngBundle.Empty,
                        NativeCombatRngEntryV111.CommittedIntentSiteId);
                nativeIntentOutcomes = committed.WeightedOutcomes;
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
                Stochastic = CombatStochasticStateV111.WithSemanticDeck(
                    CombatStochasticStateV111.WithNativeRng(
                        combat.Stochastic, null), null)
            };
            semanticRun = run with
            {
                Stochastic = RunStochasticStateV111.WithNativeRunRng(
                    run.Stochastic, null),
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
                    ProviderTrace = nativePolicyTrace,
                    WeightedIntentOutcomes = nativeIntentOutcomes,
                    CardInstanceIndexOutcomes =
                        nativeCardInstanceOutcomes,
                    CardEnergyCostOutcomes =
                        nativeCardEnergyCostOutcomes,
                    DrawInsertionOutcomes =
                        nativeDrawInsertionOutcomes,
                    EnemyPileInsertionOutcomes =
                        nativeEnemyPileInsertionOutcomes,
                    PoolShuffleOutcomes = nativePoolShuffleOutcomes,
                    GeneratedCardModelOutcomes =
                        nativeGeneratedCardModelOutcomes,
                    CardTransformOutcomes = nativeCardTransformOutcomes,
                    RandomTargetOutcomes = nativeRandomTargetOutcomes,
                    PotionGenerationOutcomes =
                        nativePotionGenerationOutcomes,
                    CardOddsOutcomes = nativeCardOddsOutcomes,
                    PotionRewardOddsOutcomes =
                        nativePotionRewardOddsOutcomes,
                    PotionFactoryOutcomes = nativePotionFactoryOutcomes,
                    CombatGoldOutcomes = nativeCombatGoldOutcomes,
                    RelicRewardOutcomes = nativeRelicRewardOutcomes,
                    EncounterFormationOutcomes = [nativeFormationAudit],
                    WeightedIntentAuditSchema =
                        SemanticNativePolicyAuditV111
                            .WeightedIntentAuditSchemaV1,
                    CardInstanceIndexAuditSchema =
                        SemanticNativePolicyAuditV111
                            .CardInstanceIndexAuditSchemaV2,
                    CardEnergyCostAuditSchema =
                        SemanticNativePolicyAuditV111
                            .CardEnergyCostAuditSchemaV2,
                    DrawInsertionAuditSchema =
                        SemanticNativePolicyAuditV111
                            .DrawInsertionAuditSchemaV1,
                    EnemyPileInsertionAuditSchema =
                        SemanticNativePolicyAuditV111
                            .EnemyPileInsertionAuditSchemaV1,
                    PoolShuffleAuditSchema =
                        SemanticNativePolicyAuditV111
                            .PoolShuffleAuditSchemaV4,
                    GeneratedCardModelAuditSchema =
                        SemanticNativePolicyAuditV111
                            .GeneratedCardModelAuditSchemaV2,
                    CardOddsAuditSchema =
                        SemanticNativePolicyAuditV111
                            .CardOddsAuditSchemaV1,
                    PotionRewardOddsAuditSchema =
                        SemanticNativePolicyAuditV111
                            .PotionRewardOddsAuditSchemaV1,
                    PotionFactoryAuditSchema =
                        SemanticNativePolicyAuditV111
                            .PotionFactoryAuditSchemaV1,
                    CombatGoldAuditSchema =
                        SemanticNativePolicyAuditV111
                            .CombatGoldAuditSchemaV1,
                    RelicRewardAuditSchema =
                        SemanticNativePolicyAuditV111
                            .RelicRewardAuditSchemaV1,
                    EncounterFormationAuditSchema =
                        SemanticNativePolicyAuditV111
                            .EncounterFormationAuditSchemaV2,
                    CardTransformAuditSchema =
                        SemanticNativePolicyAuditV111
                            .CardTransformAuditSchemaV1,
                    RandomTargetAuditSchema =
                        SemanticNativePolicyAuditV111
                            .RandomTargetAuditSchemaV1,
                    PotionGenerationAuditSchema =
                        SemanticNativePolicyAuditV111
                            .PotionGenerationAuditSchemaV1,
                    ProviderTraceSchema =
                        SemanticNativePolicyAuditV111
                            .ProviderTraceSchemaV1,
                    ProviderTraceFingerprint =
                        CanonicalJson.Sha256(nativePolicyTrace)
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
        RandomBoundaryTranscriptEntryV111[] setupTrace,
        SemanticCardInstanceIndexAuditV111[] setupCardInstanceOutcomes,
        SemanticCardEnergyCostAuditV111[] setupCardEnergyCostOutcomes,
        SemanticDrawInsertionNativeAuditV111[] setupDrawInsertionOutcomes,
        SemanticEnemyPileInsertionNativeAuditV111[]
            setupEnemyPileInsertionOutcomes,
        SemanticPoolShuffleDecisionV111[] setupPoolShuffleOutcomes,
        SemanticGeneratedCardNativeAuditV111[]
            setupGeneratedCardModelOutcomes,
        SemanticCardTransformNativeAuditV111[]
            setupCardTransformOutcomes,
        SemanticTargetNativeAuditV111[] setupRandomTargetOutcomes,
        SemanticPotionGenerationNativeAuditV111[]
            setupPotionGenerationOutcomes,
        SemanticCardOddsNativeAuditV111[] setupCardOddsOutcomes,
        SemanticPotionRewardOddsNativeAuditV111[]
            setupPotionRewardOddsOutcomes,
        SemanticPotionFactoryNativeAuditV111[] setupPotionFactoryOutcomes,
        SemanticCombatGoldNativeAuditV111[] setupCombatGoldOutcomes,
        SemanticRelicRewardNativeAuditV111[] setupRelicRewardOutcomes)
    {
        ArgumentNullException.ThrowIfNull(nativeStreams);
        ArgumentNullException.ThrowIfNull(formation);
        var run = state.Run;
        var world = run.World ?? throw new InvalidDataException(
            "Native policy entry is missing its run world.");
        var combat = world.Combat ?? throw new InvalidDataException(
            "Native policy entry is missing its combat.");
        if (run.Stochastic.NativeRunRng is not null || combat.Stochastic.NativeRng is not null ||
            combat.Stochastic.SemanticDeck is null ||
            combat.Stochastic.SemanticDeck.Mode != SemanticDeckStateV111.NatureMode)
            throw new InvalidOperationException(
                "Native policy entry requires a fresh semantic seeded opening.");

        var exactDeck = SemanticDeckEngineV111.BeginNative(
            combat.DrawPile.Reverse().ToArray());
        var nativeCombat =
            SemanticDrawPileAdapterV111.ApplyCommittedPhysicalMutation(
                combat with
                {
                    Stochastic = CombatStochasticStateV111.WithNativeRng(
                        combat.Stochastic,
                        new NativeCombatRngV111(
                            nativeStreams.Fork(), formation.Fork()))
                }, combat.DrawPile, exactDeck);
        NativeCombatRngEntryV111.Validate(nativeCombat);
        var nativeRun = run with
        {
            Stochastic = RunStochasticStateV111.WithNativeRunRng(
                run.Stochastic, nativeStreams.Fork()),
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
                ProviderTrace = setupTrace,
                CardInstanceIndexOutcomes = setupCardInstanceOutcomes,
                CardEnergyCostOutcomes = setupCardEnergyCostOutcomes,
                DrawInsertionOutcomes = setupDrawInsertionOutcomes,
                EnemyPileInsertionOutcomes =
                    setupEnemyPileInsertionOutcomes,
                PoolShuffleOutcomes = setupPoolShuffleOutcomes,
                GeneratedCardModelOutcomes =
                    setupGeneratedCardModelOutcomes,
                CardTransformOutcomes = setupCardTransformOutcomes,
                RandomTargetOutcomes = setupRandomTargetOutcomes,
                PotionGenerationOutcomes = setupPotionGenerationOutcomes,
                CardOddsOutcomes = setupCardOddsOutcomes,
                PotionRewardOddsOutcomes =
                    setupPotionRewardOddsOutcomes,
                PotionFactoryOutcomes = setupPotionFactoryOutcomes,
                CombatGoldOutcomes = setupCombatGoldOutcomes,
                RelicRewardOutcomes = setupRelicRewardOutcomes,
                WeightedIntentAuditSchema =
                    SemanticNativePolicyAuditV111
                        .WeightedIntentAuditSchemaV1,
                CardInstanceIndexAuditSchema =
                    SemanticNativePolicyAuditV111
                        .CardInstanceIndexAuditSchemaV2,
                CardEnergyCostAuditSchema =
                    SemanticNativePolicyAuditV111
                        .CardEnergyCostAuditSchemaV2,
                DrawInsertionAuditSchema =
                    SemanticNativePolicyAuditV111
                        .DrawInsertionAuditSchemaV1,
                EnemyPileInsertionAuditSchema =
                    SemanticNativePolicyAuditV111
                        .EnemyPileInsertionAuditSchemaV1,
                PoolShuffleAuditSchema =
                    SemanticNativePolicyAuditV111
                        .PoolShuffleAuditSchemaV4,
                GeneratedCardModelAuditSchema =
                    SemanticNativePolicyAuditV111
                        .GeneratedCardModelAuditSchemaV2,
                CardOddsAuditSchema =
                    SemanticNativePolicyAuditV111
                        .CardOddsAuditSchemaV1,
                PotionRewardOddsAuditSchema =
                    SemanticNativePolicyAuditV111
                        .PotionRewardOddsAuditSchemaV1,
                PotionFactoryAuditSchema =
                    SemanticNativePolicyAuditV111
                        .PotionFactoryAuditSchemaV1,
                CombatGoldAuditSchema =
                    SemanticNativePolicyAuditV111
                        .CombatGoldAuditSchemaV1,
                RelicRewardAuditSchema =
                    SemanticNativePolicyAuditV111
                        .RelicRewardAuditSchemaV1,
                CardTransformAuditSchema =
                    SemanticNativePolicyAuditV111
                        .CardTransformAuditSchemaV1,
                RandomTargetAuditSchema =
                    SemanticNativePolicyAuditV111
                        .RandomTargetAuditSchemaV1,
                PotionGenerationAuditSchema =
                    SemanticNativePolicyAuditV111
                        .PotionGenerationAuditSchemaV1,
                ProviderTraceSchema =
                    SemanticNativePolicyAuditV111
                        .ProviderTraceSchemaV1,
                ProviderTraceFingerprint = CanonicalJson.Sha256(setupTrace)
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
            combat.Stochastic.SemanticDeck?.Pending is not null)
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
            result.Stochastic.SemanticDeck?.Pending is not null ||
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
        if (current.AtPlayerChoiceBoundary)
            return current.KnowledgeDemonChoicePending is not null
                ? StepKnowledgeDemonChoice(current, actionId)
                : current.DrawHookChoicePending is not null
                    ? StepDrawHookChoice(current, actionId)
                    : StepCardSelection(current, actionId);
        if (!current.AtPlayerBoundary)
            throw new InvalidOperationException("Nature decision or combat terminal is pending.");
        var environment = new PrototypeAiEnvironment();
        var resolvedAction = environment.ResolvePlayerAction(
            current.Run, actionId);
        if (current.UsesNativePolicyBaseline)
            return StepNativePolicyBaseline(
                current, actionId, resolvedAction.Action);
        var action = resolvedAction.View;
        var source = current.Run.Fork();
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
                ("proto.native.neow.abundance" or
                    "proto.native.neow.neows_fury" or
                    "proto.colorless.secret_technique" or
                    "proto.colorless.secret_weapon" or
                    "proto.colorless.purity" or
                    "proto.colorless.thinking_ahead" or
                    "proto.colorless.scrawl" or
                    "proto.silent.hidden_daggers" or
                    "proto.silent.shadow_step" or
                    "proto.silent.survivor" or "proto.silent.hand_trick" or
                    "proto.silent.nightmare" or
                    NativeGloryAncientsV111.WishCardId);
            var semanticDrawPileSelectionCard = card?.CardId is
                ("proto.colorless.secret_technique" or
                 "proto.colorless.secret_weapon" or
                 "proto.colorless.scrawl");
            var semanticDrawCard = card is not null &&
                IsSemanticEffectDrawCard(card.CardId);
            var semanticAutoplayCard = card?.CardId is
                ("proto.colorless.beat_down" or
                 "proto.colorless.catastrophe");
            var semanticRandomReplayCard =
                card?.CardId == "proto.colorless.hidden_gem";
            var semanticPotionGenerationCard =
                card?.CardId == "proto.colorless.alchemize";
            var semanticRandomRareToHandCard =
                card?.CardId == "proto.colorless.anointed";
            var semanticSeekerStrikeCard =
                card?.CardId == "proto.colorless.seeker_strike";
            var semanticDiscoveryCard =
                card?.CardId == "proto.colorless.discovery";
            var semanticSplashCard =
                card?.CardId == "proto.colorless.splash";
            var semanticJackpotCard =
                card?.CardId == "proto.colorless.jackpot";
            var semanticVolleyCard =
                card?.CardId == "proto.colorless.volley";
            var semanticRandomTargetCard =
                card?.CardId == "proto.silent.bouncing_flask";
            var semanticJackOfAllTradesCard =
                card?.CardId == "proto.colorless.jack_of_all_trades";
            var supportedCardAction = card is not null &&
                (SemanticPlayerActionEngineV111.SupportsCard(card.CardId) ||
                 card.CardId == "proto.silent.ricochet" ||
                 semanticDrawCard ||
                 semanticAutoplayCard ||
                 semanticRandomReplayCard ||
                 semanticPotionGenerationCard ||
                 semanticRandomRareToHandCard ||
                 semanticSeekerStrikeCard ||
                 semanticDiscoveryCard ||
                 semanticSplashCard ||
                 semanticJackpotCard ||
                 semanticVolleyCard ||
                 semanticRandomTargetCard ||
                 semanticJackOfAllTradesCard ||
                 semanticSelectionCard);
            if (!supportedCardAction)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=card_effect_requires_semantic_audit");
            // Preserve actual source damage, relic/power hooks and victory
            // transitions. Supported hidden-pile mutations must update the
            // provider-owned semantic deck at their source boundary.
            using (ControlledChanceGuardV111.Enter())
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                result = environment.StepResolved(
                    source, resolvedAction.Action).State;
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
                     CanonicalJson.Sha256(previous.Stochastic.SemanticDeck) !=
                         CanonicalJson.Sha256(next.Stochastic.SemanticDeck))
            {
                if ((!semanticDrawCard && !semanticAutoplayCard &&
                     !semanticDrawPileSelectionCard &&
                     !semanticRandomRareToHandCard &&
                     !SemanticPlayerActionEngineV111
                        .IsSupportedNostalgiaDrawReturn(
                            source, result, resolvedAction.Action)) ||
                    next is null)
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
            var validSlot = payload.Slot >= 0 &&
                payload.Slot < source.Player.PotionSlots.Length;
            var isGenerationPotion = validSlot &&
                source.Player.PotionSlots[payload.Slot]?.PotionId ==
                    "proto.potion.orobic_acid";
            var isPoolPotion = validSlot &&
                source.Player.PotionSlots[payload.Slot]?.PotionId ==
                    "proto.potion.colorless";
            var isHandReturnPotion = validSlot &&
                source.Player.PotionSlots[payload.Slot]?.PotionId ==
                    "proto.potion.bottled_potential";
            var isDistilledChaos = validSlot &&
                source.Player.PotionSlots[payload.Slot]?.PotionId ==
                    "proto.potion.distilled_chaos";
            var isSemanticDrawPotion = validSlot &&
                IsSemanticEffectDrawPotion(
                    source.Player.PotionSlots[payload.Slot]?.PotionId);
            var isDeterministicPotion = validSlot &&
                IsSemanticDeterministicCombatPotion(
                    source.Player.PotionSlots[payload.Slot]?.PotionId);
            var isDeterministicTargetPotion = validSlot &&
                IsSemanticDeterministicTargetPotion(
                    source.Player.PotionSlots[payload.Slot]?.PotionId);
            var isDeterministicDamagePotion = validSlot &&
                IsSemanticDeterministicDamagePotion(
                    source.Player.PotionSlots[payload.Slot]?.PotionId);
            var isSelectionPotion = validSlot && IsSemanticSelectionPotion(
                source.Player.PotionSlots[payload.Slot]?.PotionId);
            if ((!isGenerationPotion && !isPoolPotion &&
                 !isSelectionPotion && !isHandReturnPotion &&
                 !isDistilledChaos && !isSemanticDrawPotion &&
                 !isDeterministicPotion &&
                 !isDeterministicTargetPotion) ||
                (isDeterministicTargetPotion
                    ? payload.TargetEnemyId is null
                    : payload.TargetEnemyId is not null))
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.player_action " +
                    "operation=potion_effect_requires_semantic_audit");
            using (ControlledChanceGuardV111.Enter())
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                result = environment.StepResolved(
                    source, resolvedAction.Action).State;
            if (result.Phase != RunPhase.Combat &&
                !((isDistilledChaos || isSemanticDrawPotion ||
                   isDeterministicDamagePotion) &&
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
                !isSemanticDrawPotion &&
                !previous.DrawPile.SequenceEqual(next.DrawPile) ||
                !isHandReturnPotion && !isDistilledChaos &&
                !isSemanticDrawPotion &&
                CanonicalJson.Sha256(previous.Stochastic.SemanticDeck) !=
                    CanonicalJson.Sha256(next.Stochastic.SemanticDeck))))
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
        return ResolveEngineOwnedSemanticChoices(nextState);
    }

    internal static SemanticPilotCombatV111
        StepResolvedNativePolicyBaseline(
            SemanticPilotCombatV111 current, string fingerprint,
            string actionId,
            (GameAction Action, PrototypeAiAction View) resolvedAction)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(resolvedAction.Action);
        ArgumentNullException.ThrowIfNull(resolvedAction.View);
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic player-action fingerprint.");
        if (!current.AtPlayerBoundary || !current.UsesNativePolicyBaseline)
            throw new InvalidOperationException(
                "Resolved Native-policy actions require a player boundary.");
        if (!StringComparer.Ordinal.Equals(
                resolvedAction.View.ActionId, actionId) ||
            !StringComparer.Ordinal.Equals(
                PrototypeAiEnvironment.StableActionId(
                    resolvedAction.Action), actionId))
            throw new InvalidDataException(
                "Resolved Native-policy action differs from its action ID.");
        return StepNativePolicyBaseline(
            current, actionId, resolvedAction.Action);
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
        return ResumeAndPublishNestedChoice(
            current, choice,
            new SemanticPilotContinuationV111(
                "semantic_card_selection",
                CardSelectionDecision: decision.Fork()));
    }

    private static SemanticPilotCombatV111 ResumeAndPublishNestedChoice(
        SemanticPilotCombatV111 current,
        SemanticEndTurnNestedChoiceV111 choice,
        SemanticPilotContinuationV111? historyEntry,
        bool normalizeEngineOwnedChoices = false,
        bool validate = true,
        SemanticOtherChanceStateV111? otherChance = null)
    {
        var sourceEndTurn = current.PendingEndTurn;
        var nextEndTurn = sourceEndTurn is { } endTurn
            ? SemanticEndTurnEngineV111.ResumeNestedSemanticChoice(
                endTurn, choice)
            : null;
        var publishedRun = nextEndTurn?.Run ??
            SemanticChoiceResumeV111.Resume(
                current.Run, choice).Run;
        var next = PublishRunContinuation(
            current, publishedRun, nextEndTurn, historyEntry,
            otherChance,
            preservePendingEndTurn: sourceEndTurn is null);
        if (validate)
            Validate(next);
        return normalizeEngineOwnedChoices
            ? ResolveEngineOwnedSemanticChoices(next)
            : next;
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
        if (endTurn.NestedDrawHookChoicePending is not null)
        {
            var nestedDecision = SemanticDrawHookChoiceEngineV111.Select(
                current.Run, pending, actionId);
            var nestedChoice = new SemanticEndTurnNestedChoiceV111(
                "semantic_draw_hook_choice",
                DrawHookChoiceDecision: nestedDecision.Fork());
            return ResolveEngineOwnedSemanticChoices(
                ResumeAndPublishNestedChoice(
                    current, nestedChoice,
                    new SemanticPilotContinuationV111(
                        "semantic_draw_hook_choice",
                        DrawHookChoiceDecision: nestedDecision.Fork())));
        }
        var priorNestedChoiceCount =
            endTurn.Stochastic.NestedChoiceTranscript.Length;
        var priorHandDrawHookChoiceCount =
            endTurn.HandDraw?.DrawHookChoiceTranscript.Length ?? 0;
        var nextEndTurn = SemanticEndTurnEngineV111.ResolveDrawHookChoice(
            endTurn, endTurn.Fingerprint, actionId);
        var decision = nextEndTurn.Stochastic.NestedChoiceTranscript.Length >
                priorNestedChoiceCount
            ? nextEndTurn.Stochastic.NestedChoiceTranscript[^1]
                .DrawHookChoiceDecision
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
        return ResolveEngineOwnedSemanticChoices(next);
    }

    private static SemanticPilotCombatV111 StepKnowledgeDemonChoice(
        SemanticPilotCombatV111 current, string actionId)
    {
        var pending = current.KnowledgeDemonChoicePending ??
            throw new InvalidOperationException(
                "No Knowledge Demon action is pending.");
        _ = current.PendingEndTurn ??
            throw new InvalidDataException(
                "Knowledge Demon action lost its end-turn continuation.");
        var decision = SemanticKnowledgeDemonChoiceEngineV111.Select(
            current.Run, pending, actionId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "knowledge_demon_choice",
            KnowledgeDemonChoiceDecision: decision.Fork());
        var next = ResumeAndPublishNestedChoice(
            current, choice,
            new SemanticPilotContinuationV111(
                "semantic_knowledge_demon_choice", actionId,
                KnowledgeDemonChoiceDecision: decision.Fork()));
        return ResolveEngineOwnedSemanticChoices(next);
    }

    /// <summary>
    /// Resolve one suspended random-enemy effect by stable physical enemy
    /// identity, then resume the original source continuation.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveRandomTarget(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int enemyInstanceId,
        SemanticOtherChanceStateV111? otherChance = null)
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
            if (resolvedEndTurn.Stochastic.TargetTranscript.Length <=
                endTurn.Stochastic.TargetTranscript.Length)
                throw new InvalidDataException(
                    "End-turn target resolution omitted its selected identity.");
            var endTurnDecision =
                resolvedEndTurn.Stochastic.TargetTranscript[^1];
            var resolvedSession = PublishRunContinuation(
                current, resolvedEndTurn.Run, resolvedEndTurn,
                otherChance is null
                    ? new SemanticPilotContinuationV111(
                        "semantic_target",
                        TargetDecision: endTurnDecision.Fork())
                    : null,
                otherChance: otherChance);
            Validate(resolvedSession);
            return ResolveEngineOwnedSemanticChoices(resolvedSession);
        }

        var pending = current.TargetPending ??
            throw new InvalidOperationException(
                "No semantic random-target event is pending.");
        var decision = SemanticTargetChoiceV111.Select(
            pending, eventId, enemyInstanceId);
        RunState result = SemanticChoiceResumeV111.ResumeTarget(
            current.Run, decision);
        if (result.Phase == RunPhase.Combat)
            ValidateSemanticCombatBoundary(result);
        var next = PublishRunContinuation(
            current, result, null,
            otherChance is null
                ? new SemanticPilotContinuationV111(
                    "semantic_target", TargetDecision: decision.Fork())
                : null,
            otherChance: otherChance,
            preservePendingEndTurn: true);
        return ResolveEngineOwnedSemanticChoices(next);
    }

    /// <summary>
    /// Resolve one generated card model from its ordered source pool and
    /// resume the original generation/trigger continuation.
    /// </summary>
    public static SemanticPilotCombatV111 ResolveGeneratedCard(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, string cardId,
        SemanticOtherChanceStateV111? otherChance = null)
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
        return ResumeGeneratedCardDecision(current, expected, otherChance);
    }

    private static SemanticPilotCombatV111 ResumeGeneratedCardDecision(
        SemanticPilotCombatV111 current,
        SemanticGeneratedCardDecisionV111 decision,
        SemanticOtherChanceStateV111? otherChance = null)
    {
        var pending = current.GeneratedCardPending ??
            throw new InvalidOperationException(
                "No semantic generated-card event is pending.");
        SemanticGenerationChoiceV111.ValidateDecision(
            pending, decision, decision.SelectedCardId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_generated_card", GeneratedCardDecision: decision.Fork());
        return ResumeAndPublishNestedChoice(
            current, choice,
            otherChance is null
                ? new SemanticPilotContinuationV111(
                    "semantic_generated_card",
                    GeneratedCardDecision: decision.Fork())
                : null,
            normalizeEngineOwnedChoices: true,
            validate: false,
            otherChance: otherChance);
    }

    public static SemanticPilotCombatV111 ResolvePotionGeneration(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, string potionId,
        SemanticOtherChanceStateV111? otherChance = null)
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
        return ResumePotionGenerationDecision(current, expected, otherChance);
    }

    private static SemanticPilotCombatV111 ResumePotionGenerationDecision(
        SemanticPilotCombatV111 current,
        SemanticPotionGenerationDecisionV111 decision,
        SemanticOtherChanceStateV111? otherChance = null)
    {
        var pending = current.PotionGenerationPending ??
            throw new InvalidOperationException(
                "No semantic potion-generation event is pending.");
        SemanticPotionGenerationChoiceV111.ValidateDecision(
            pending, decision, decision.SelectedPotionId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_potion_generation",
            PotionGenerationDecision: decision.Fork());
        return ResumeAndPublishNestedChoice(
            current, choice,
            otherChance is null
                ? new SemanticPilotContinuationV111(
                    "semantic_potion_generation",
                    PotionGenerationDecision: decision.Fork())
                : null,
            normalizeEngineOwnedChoices: true,
            validate: false,
            otherChance: otherChance);
    }

    public static SemanticPilotCombatV111 ResolveCardTransform(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, string cardId,
        SemanticOtherChanceStateV111? otherChance = null)
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
        return ResumeCardTransformDecision(current, expected, otherChance);
    }

    private static SemanticPilotCombatV111 ResumeCardTransformDecision(
        SemanticPilotCombatV111 current,
        SemanticCardTransformDecisionV111 decision,
        SemanticOtherChanceStateV111? otherChance = null)
    {
        var pending = current.CardTransformPending ??
            throw new InvalidOperationException(
                "No semantic card-transform event is pending.");
        SemanticCardTransformChoiceV111.ValidateDecision(
            pending, decision, decision.SelectedCardId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_card_transform", CardTransformDecision: decision.Fork());
        return ResumeAndPublishNestedChoice(
            current, choice,
            otherChance is null
                ? new SemanticPilotContinuationV111(
                    "semantic_card_transform",
                    CardTransformDecision: decision.Fork())
                : null,
            normalizeEngineOwnedChoices: true,
            validate: false,
            otherChance: otherChance);
    }

    private static SemanticPilotCombatV111 StepNativePolicyBaseline(
        SemanticPilotCombatV111 current, string actionId,
        GameAction resolvedAction)
    {
        var environment = new PrototypeAiEnvironment();
        RunState result;
        using var boundaries = RandomBoundaryJournalV111.Begin();
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedNativeProvider())
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
        using (StochasticProviderScopeV111.EnterBoundary(
            new NativeRandomBoundaryProviderV111(),
            overrideExplicitProviders: false))
            result = environment.StepResolved(
                current.Run.Fork(), resolvedAction).State;

        var audit = current.Stochastic.NativePolicyAudit ??
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
        var combinedTrace = RandomBoundaryJournalV111.ConcatOrdered(
            audit.ProviderTrace, recordedTrace);
        var weightedIntentOutcomes = boundaries.WeightedIntentOutcomes
            .ToArray();
        var cardInstanceIndexOutcomes = boundaries
            .CardInstanceIndexOutcomes.ToArray();
        var cardEnergyCostOutcomes = boundaries
            .CardEnergyCostOutcomes.ToArray();
        var drawInsertionOutcomes = boundaries
            .DrawInsertionOutcomes.ToArray();
        var enemyPileInsertionOutcomes = boundaries
            .EnemyPileInsertionOutcomes.ToArray();
        var poolShuffleOutcomes = boundaries
            .PoolShuffleOutcomes.ToArray();
        var generatedCardModelOutcomes = boundaries
            .GeneratedCardModelOutcomes.ToArray();
        var cardTransformOutcomes = boundaries
            .CardTransformOutcomes.ToArray();
        var randomTargetOutcomes = boundaries
            .RandomTargetOutcomes.ToArray();
        var potionGenerationOutcomes = boundaries
            .PotionGenerationOutcomes.ToArray();
        var cardOddsOutcomes = boundaries.CardOddsOutcomes.ToArray();
        var potionRewardOddsOutcomes = boundaries
            .PotionRewardOddsOutcomes.ToArray();
        var potionFactoryOutcomes = boundaries.PotionFactoryOutcomes.ToArray();
        var combatGoldOutcomes = boundaries.CombatGoldOutcomes.ToArray();
        var relicRewardOutcomes = boundaries.RelicRewardOutcomes.ToArray();

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
                ProviderTrace = combinedTrace,
                ProviderTraceFingerprint =
                    audit.ProviderTraceSchema ==
                        SemanticNativePolicyAuditV111.ProviderTraceSchemaV1
                        ? CanonicalJson.Sha256(combinedTrace)
                        : audit.ProviderTraceFingerprint,
                WeightedIntentOutcomes = audit.WeightedIntentOutcomes
                    .Concat(weightedIntentOutcomes).ToArray(),
                CardInstanceIndexOutcomes =
                    audit.CardInstanceIndexOutcomes
                        .Concat(cardInstanceIndexOutcomes).ToArray(),
                CardEnergyCostOutcomes =
                    audit.CardEnergyCostOutcomes
                        .Concat(cardEnergyCostOutcomes).ToArray(),
                DrawInsertionOutcomes = audit.DrawInsertionOutcomes
                    .Concat(drawInsertionOutcomes).ToArray(),
                EnemyPileInsertionOutcomes =
                    audit.EnemyPileInsertionOutcomes
                        .Concat(enemyPileInsertionOutcomes).ToArray(),
                PoolShuffleOutcomes = audit.PoolShuffleOutcomes
                    .Concat(poolShuffleOutcomes).ToArray(),
                GeneratedCardModelOutcomes =
                    audit.GeneratedCardModelOutcomes
                        .Concat(generatedCardModelOutcomes).ToArray(),
                CardTransformOutcomes = audit.CardTransformOutcomes
                    .Concat(cardTransformOutcomes).ToArray(),
                RandomTargetOutcomes = audit.RandomTargetOutcomes
                    .Concat(randomTargetOutcomes).ToArray(),
                PotionGenerationOutcomes =
                    audit.PotionGenerationOutcomes
                        .Concat(potionGenerationOutcomes).ToArray(),
                CardOddsOutcomes = audit.CardOddsOutcomes
                    .Concat(cardOddsOutcomes).ToArray(),
                PotionRewardOddsOutcomes =
                    audit.PotionRewardOddsOutcomes
                        .Concat(potionRewardOddsOutcomes).ToArray(),
                PotionFactoryOutcomes = audit.PotionFactoryOutcomes
                    .Concat(potionFactoryOutcomes).ToArray(),
                CombatGoldOutcomes = audit.CombatGoldOutcomes
                    .Concat(combatGoldOutcomes).ToArray(),
                RelicRewardOutcomes = audit.RelicRewardOutcomes
                    .Concat(relicRewardOutcomes).ToArray()
            }
        };
        Validate(next);
        return next;
    }

    private static SemanticRewardAuditV111 RecordRewardAudit(
        RunState source, RunState result)
    {
        static ulong Count(RunState run)
        {
            if (!run.Stochastic.PrototypeRng.TryGetStream("reward", out var stream) ||
                stream.Codec != PrototypeRng.Codec)
                throw new InvalidDataException(
                    "Reward RNG stream is missing or has an unsupported codec.");
            return stream.CallCount ?? throw new InvalidDataException(
                "Reward RNG stream has no saved call count.");
        }
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
        string eventId, long cardInstanceId,
        SemanticOtherChanceStateV111? otherChance = null)
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
            null, null, false, null, decision, otherChance);
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
        return ResumeCostDecision(current, decision);
    }

    private static SemanticPilotCombatV111 ResumeCostDecision(
        SemanticPilotCombatV111 current,
        SemanticCostDecisionV111 decision,
        SemanticOtherChanceStateV111? otherChance = null)
    {
        var pending = current.CostPending ??
            throw new InvalidOperationException(
                "No Nature-controlled drawn-card cost is pending.");
        SemanticCostChoiceV111.ValidateDecision(
            pending, decision, decision.SelectedIndex);
        var index = decision.SelectedIndex;
        if (current.Run.World?.Combat?.PendingChoice?
                .ControlledSemanticDraw is not null)
            return Resolve(current, current.Fingerprint, index,
                null, null, false, decision,
                otherChance: otherChance);
        if (pending.TargetCardInstanceId is not null)
        {
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_cost", CostDecision: decision.Fork());
            return ResumeAndPublishNestedChoice(
                current, choice,
                otherChance is null
                    ? new SemanticPilotContinuationV111(
                        "semantic_cost", CostDecision: decision.Fork())
                    : null,
                validate: true, otherChance: otherChance);
        }
        return Resolve(current, current.Fingerprint, index,
            null, null, false, decision,
            otherChance: otherChance);
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
        return ResumeHandCostSelectionDecision(current, decision);
    }

    private static SemanticPilotCombatV111 ResumeHandCostSelectionDecision(
        SemanticPilotCombatV111 current,
        SemanticHandCostSelectionDecisionV111 decision,
        SemanticOtherChanceStateV111? otherChance = null)
    {
        var pending = current.HandCostSelectionPending ??
            throw new InvalidOperationException(
                "No random hand-card cost selection is pending.");
        SemanticHandCostSelectionChoiceV111.ValidateDecision(
            pending, decision, decision.SelectedCardInstanceId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_hand_cost_selection",
            HandCostSelectionDecision: decision.Fork());
        return ResumeAndPublishNestedChoice(
            current, choice,
            otherChance is null
                ? new SemanticPilotContinuationV111(
                    "semantic_hand_cost_selection",
                    HandCostSelectionDecision: decision.Fork())
                : null,
            otherChance: otherChance);
    }

    public static SemanticPilotCombatV111 ResolveCardShortlist(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, long cardInstanceId,
        SemanticOtherChanceStateV111? otherChance = null)
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
        return ResumeCardShortlistDecision(current, decision, otherChance);
    }

    private static SemanticPilotCombatV111 ResumeCardShortlistDecision(
        SemanticPilotCombatV111 current,
        SemanticCardShortlistDecisionV111 decision,
        SemanticOtherChanceStateV111? otherChance = null)
    {
        var pending = current.CardShortlistPending ??
            throw new InvalidOperationException(
                "No random card-pile shortlist is pending.");
        SemanticCardShortlistChoiceV111.ValidateDecision(
            pending, decision, decision.SelectedCardInstanceId);
        var choice = new SemanticEndTurnNestedChoiceV111(
            "semantic_card_shortlist", CardShortlistDecision: decision.Fork());
        return ResumeAndPublishNestedChoice(
            current, choice,
            otherChance is null
                ? new SemanticPilotContinuationV111(
                    "semantic_card_shortlist",
                    CardShortlistDecision: decision.Fork())
                : null,
            normalizeEngineOwnedChoices: true,
            validate: false,
            otherChance: otherChance);
    }

    public static SemanticPilotCombatV111 ResolvePoolShuffle(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int swapIndex,
        SemanticOtherChanceStateV111? otherChance = null)
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
        return ResumeAndPublishNestedChoice(
            current, choice,
            otherChance is null
                ? new SemanticPilotContinuationV111(
                    "semantic_pool_shuffle",
                    PoolShuffleDecision: decision.Fork())
                : null,
            normalizeEngineOwnedChoices: true,
            validate: false,
            otherChance: otherChance);
    }

    public static SemanticPilotCombatV111 ResolveDrawInsertion(
        SemanticPilotCombatV111 current, string fingerprint,
        string eventId, int position,
        SemanticOtherChanceStateV111? otherChance = null)
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
            var priorNestedChoiceCount =
                endTurn.Stochastic.NestedChoiceTranscript.Length;
            var resolvedEndTurn = SemanticEndTurnEngineV111
                .ResolveDrawInsertion(
                    endTurn, endTurn.Fingerprint, eventId, position);
            var endTurnDecision = resolvedEndTurn.HandDraw is { } resolvedHand &&
                    resolvedHand.DrawInsertionTranscript.Length >
                        priorHandInsertionCount
                ? resolvedHand.DrawInsertionTranscript[^1]
                : resolvedEndTurn.Stochastic.NestedChoiceTranscript.Length >
                  priorNestedChoiceCount
                    ? resolvedEndTurn.Stochastic.NestedChoiceTranscript[^1]
                        .DrawInsertionDecision
                    : null;
            if (endTurnDecision is null)
                throw new InvalidDataException(
                    "End-turn draw insertion omitted its replay decision.");
            var resolvedSession = PublishRunContinuation(
                current, resolvedEndTurn.Run, resolvedEndTurn,
                otherChance is null
                    ? new SemanticPilotContinuationV111(
                        "semantic_draw_insertion",
                        DrawInsertionDecision: endTurnDecision.Fork())
                    : null,
                otherChance: otherChance);
            Validate(resolvedSession);
            return otherChance is null
                ? resolvedSession
                : ResolveEngineOwnedSemanticChoices(resolvedSession);
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
        var next = ResumeAndPublishNestedChoice(
            current, choice,
            otherChance is null
                ? new SemanticPilotContinuationV111(
                    "semantic_draw_insertion",
                    DrawInsertionDecision: decision.Fork())
                : null,
            validate: false,
            otherChance: otherChance);
        var result = next.Run;
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
            if (resultCombat!.PendingChoice is { } nextInsertionChoice &&
                PrototypeGameEngine
                    .IsAutomaticPipelineSemanticDrawInsertionContinuation(
                        resultCombat, nextInsertionChoice) &&
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
        Validate(next);
        return otherChance is null
            ? next
            : ResolveEngineOwnedSemanticChoices(next);
    }

    private static SemanticPilotCombatV111 Resolve(
        SemanticPilotCombatV111 current, string fingerprint, int selectedIndex,
        SemanticIntentDecisionV111? semanticIntent,
        SemanticDrawDecisionV111? expectedDeckDecision,
        bool recordSemanticDraw,
        SemanticCostDecisionV111? expectedCostDecision,
        SemanticRandomPileCardDecisionV111? expectedRandomPileCardDecision = null,
        SemanticOtherChanceStateV111? otherChance = null)
    {
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException("Stale semantic Nature fingerprint.");
        if (current.DrawHookChoicePending is not null)
            throw new InvalidOperationException(
                "Resolve the pending CardDrawn hook action by action ID.");
        if (current.PendingEndTurn?.TargetPending is { } automaticTarget)
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
                current.PendingEndTurn?.Intents;
            if (ticket?.Pending is not { } request)
                throw new InvalidOperationException(
                    "Semantic intent decision has no matching pending event.");
            var support = ControlledTurnIntentEngineV111
                .DescribePendingSemanticIntent(ticket) ??
                throw new InvalidDataException(
                    "Missing weighted intent support.");
            var semanticEvent = SemanticIntentChoiceV111.CreateEvent(
                current.Fingerprint, request, support);
            SemanticIntentChoiceV111.ValidateDecision(
                semanticEvent, semanticIntent, selectedIndex);
        }
        var drawEvent = current.DrawPending;
        var randomPileCardEvent = current.RandomPileCardPending;
        if (current.CardShortlistPending is not null)
            throw new InvalidOperationException(
                "Resolve the pending pile shortlist by card instance ID.");
        var costEvent = current.CostPending;
        if (current.HandCostSelectionPending is not null &&
            expectedCostDecision is null)
            throw new InvalidOperationException(
                "Resolve the pending hand-cost selection event by card instance ID.");
        if (current.PendingInitialIntents is { Pending: not null } initialIntents)
        {
            var resolvedInitial = ControlledTurnIntentEngineV111.Resolve(
                initialIntents, selectedIndex, current.Run.Stochastic.PrototypeRng);
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
            if (effectCostDecision.SelectedIndex != selectedIndex)
                throw new InvalidDataException(
                    "Semantic card-effect draw cost chose a different bounded index.");
            var choice = new SemanticEndTurnNestedChoiceV111(
                "semantic_cost", CostDecision: effectCostDecision.Fork());
            var effectCostState = ResumeAndPublishNestedChoice(
                current, choice,
                otherChance is null
                    ? new SemanticPilotContinuationV111(
                        "semantic_cost",
                        CostDecision: effectCostDecision.Fork())
                    : null,
                otherChance: otherChance);
            return effectCostState;
        }
        if (current.Run.World?.Combat?.PendingChoice is
                { ChoiceId: "controlled_semantic_draw",
                  ControlledSemanticDraw: not null })
        {
            if (drawEvent is null)
                throw new InvalidDataException(
                    "Card-effect draw continuation lost its Nature event.");
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
            var effectDrawState = ResumeAndPublishNestedChoice(
                current, choice,
                new SemanticPilotContinuationV111(
                    "semantic_draw",
                    DrawDecision: effectDrawDecision.Fork()));
            return effectDrawState;
        }
        var pending = current.PendingEndTurn ??
            throw new InvalidOperationException("No Nature decision is pending.");
        if (pending.IntentPending is null && pending.DrawPending is null &&
            pending.RandomPileCardPending is null && pending.CostPending is null &&
            pending.EnemyActionPending is null)
            throw new InvalidOperationException(
                "No semantic enemy intent, draw or cost choice is pending.");
        var resolved = SemanticEndTurnEngineV111.Resolve(
            pending, pending.Fingerprint, selectedIndex);
        SemanticDrawDecisionV111? deckDecision = null;
        if (drawEvent is not null)
        {
            deckDecision = SemanticDrawChoiceV111.ValidateRecordedChoice(
                resolved.Run, drawEvent, selectedIndex);
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
        return ResolveEngineOwnedSemanticChoices(PublishRunContinuation(
            current, resolved.Run, resolved,
            otherChance is null
                ? NatureContinuation(
                    selectedIndex, semanticIntent,
                    recordSemanticDraw ? deckDecision : null,
                    costDecision, randomPileCardDecision)
                : null,
            otherChance: otherChance));
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
            selection.Decision, null, false, null);
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
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic draw fingerprint.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var pending = current.DrawPending ??
            throw new InvalidOperationException(
                "Only a pending semantic draw supports card-instance resolution.");
        var decision = SemanticDrawChoiceV111.Select(
            pending, eventId, cardInstanceId);
        return Resolve(current, fingerprint, decision.SelectedIndex,
            null, decision, true, null);
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
        var nextRun = SemanticChoiceResumeV111.ResumeDeckReveal(
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
        var ticket = current.PendingInitialIntents ??
            current.PendingEndTurn?.Intents;
        if (ticket?.Pending is not { } request)
            throw new InvalidOperationException(
                "No semantic intent event is pending.");
        var support = ControlledTurnIntentEngineV111
            .DescribePendingSemanticIntent(ticket) ??
            throw new InvalidDataException("Missing weighted intent support.");
        return SemanticIntentChoiceV111.CreateEvent(
            current.Fingerprint, request, support);
    }

    public static SemanticTargetEventV111? DescribePendingTargetEvent(
        SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn is { } endTurn)
            return endTurn.TargetPending;
        var combat = current.Run.World?.Combat;
        var choice = combat?.PendingChoice;
        if (choice?.SourceCardInstanceId is { } volleySourceCardId &&
            combat?.Cards.Any(card =>
                card.InstanceId == volleySourceCardId &&
                card.CardId == "proto.colorless.volley") == true &&
            !IsVolleyRandomTargetChoice(
                combat, choice, choice.ControlledTarget))
            throw new InvalidDataException(
                "Volley target is outside its supported card-play continuation.");
        var otherChance = current.Run.Stochastic.SemanticOtherChance;
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_target");
        if (otherChance is not null && otherChance.ProviderId !=
            SemanticOtherChanceEngineV111.LegacyProviderId)
            ordinal += otherChance.Decisions.Count(decision =>
                StochasticSiteRegistryV111.CombatRandomTargetSiteIds
                    .Contains(decision.Request.SiteId,
                        StringComparer.Ordinal));
        return DescribePendingTargetEvent(current.Run, current.Fingerprint,
            ordinal);
    }

    private static bool IsVolleyRandomTargetChoice(
        CombatState? combat, PendingCombatChoiceState? choice,
        PrototypeControlledTargetChoiceV111? target)
    {
        if (combat is null || target is not { Kind: "effect_random_enemy",
                SlyCardInstanceId: null, AutoPlayCardInstanceId: null } ||
            choice is not { ChoiceId: "controlled_random_target",
                SourceCardInstanceId: { } sourceCardInstanceId,
                CompletionEvents: [],
                OuterChoiceContinuation: { } continuation } ||
            target.EligibleEnemyInstanceIds.Length == 0)
            return false;
        var sourceCard = combat.Cards.SingleOrDefault(card =>
            card.InstanceId == sourceCardInstanceId &&
            card.CardId == "proto.colorless.volley");
        if (sourceCard is null)
            return false;
        var series = continuation.CardPlaySeries;
        if (series is null || series.SourceCardInstanceId != sourceCardInstanceId ||
            series.IsAutoPlay || series.NextPlayIndex <= 0 ||
            series.NextPlayIndex > series.PlayCount ||
            continuation.Operations.Length == 0 ||
            continuation.Operations.Length > series.EnergySpent)
            return false;
        var damage = 10 + (4 * sourceCard.UpgradeLevel) +
            (sourceCard.Enchantment is
                { Kind: PrototypeCardEnchantmentKind.TezcatarasEmber } ? 3 : 0);
        var firstOperation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.DamageEnemy, damage,
            TargetMode: PrototypeEffectTarget.ActionTargetEnemy,
            SourceKind: PrototypeEffectSourceKind.Card,
            IsPoweredAttack: true);
        var repeatedOperation = firstOperation with
        {
            TargetMode = PrototypeEffectTarget.RandomEnemy
        };
        return continuation.Operations.Select((operation, index) =>
                CanonicalJson.Sha256(operation) == CanonicalJson.Sha256(
                    index == 0 ? firstOperation : repeatedOperation))
            .All(matches => matches) &&
            continuation.SourceCardInstanceId == sourceCardInstanceId &&
            continuation.CompletionEvents.Length == 1 &&
            continuation.CompletionEvents[0] is
                { Kind: PrototypeCombatEventKind.CardPlayed,
                    CardId: "proto.colorless.volley" } &&
            continuation.CompletionEvents[0].SourceCardInstanceId ==
                sourceCardInstanceId &&
            continuation.Parent is null &&
            continuation.EventDispatchContinuation is null;
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
        if (generation?.Operation.Kind ==
                PrototypeCombatEffectKind.AddRandomZeroCostCardsToHand &&
            !IsJackpotZeroCostGenerationChoice(combat, choice, generation))
            throw new InvalidDataException(
                "Zero-cost generated-card choice is outside Jackpot's source action.");
        if (isDrawInsertionModelChoice)
        {
            if (current.Stochastic.SeededEntry?.FixtureRandomFreeAttackDrawInsertion != true &&
                combat?.AutomaticPipelineContinuation is null)
                throw new InvalidDataException(
                    "Random draw-Attack model choice lacks a supported source boundary.");
            SemanticDrawInsertionChoiceV111.ValidateBoundary(current.Run);
        }
        var siteId = generation is not null
            ? StochasticSiteRegistryV111.CombatGeneratedCardSelectSiteId
            : StochasticSiteRegistryV111
                .CombatRandomDrawAttackCardModelSiteId;
        var otherChance = current.Run.Stochastic.SemanticOtherChance;
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_generated_card");
        if (otherChance is not null && otherChance.ProviderId !=
            SemanticOtherChanceEngineV111.LegacyProviderId)
            ordinal += otherChance.Decisions.Count(decision =>
                decision.Request.SiteId == siteId);
        if (generation is not null)
            return SemanticGenerationChoiceV111.DescribePending(
                current.Run, current.Fingerprint, ordinal);
        var candidates = (string[])(generation?.CandidateCardIds ??
            drawInsertion!.CandidateCardIds).Clone();
        var operation = drawInsertion!.Operation.Kind;
        var ownerId = choice!.SourceCardInstanceId is { } sourceCardId
            ? "card_instance:" + sourceCardId
            : "effect:" + operation;
        return SemanticGenerationChoiceV111.CreateEvent(
            current.Fingerprint, siteId, ownerId, ordinal, candidates,
            combat?.Stochastic.NativeRng?.RunStreams is not null
                ? "run.combat_card_generation" : "prototype.combat");
    }

    public static SemanticPotionGenerationEventV111?
        DescribePendingPotionGenerationEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn?.PotionGenerationPending is
            { } endTurnEvent)
            return endTurnEvent;
        var combat = current.Run.World?.Combat;
        var choice = combat?.PendingChoice;
        var generation = choice?.ControlledPotionGeneration;
        if (generation is null) return null;
        if (!IsAlchemizePotionGenerationChoice(combat, choice, generation))
            throw new InvalidDataException(
                "Potion generation is outside the supported Alchemize source action.");
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_potion_generation");
        if (current.Run.Stochastic.SemanticOtherChance is
                { } otherChance && otherChance.ProviderId !=
                SemanticOtherChanceEngineV111.LegacyProviderId)
            ordinal += otherChance.Decisions.Count(decision =>
                decision.Request.SiteId ==
                    SemanticPotionGenerationChoiceV111.SiteId);
        return SemanticPotionGenerationChoiceV111.DescribePending(
            current.Run, current.Fingerprint, ordinal);
    }

    private static bool IsAlchemizePotionGenerationChoice(
        CombatState? combat, PendingCombatChoiceState? choice,
        PrototypeControlledPotionGenerationV111 generation)
    {
        if (combat is null ||
            choice is not { ChoiceId: "controlled_potion_generation",
                SourceCardInstanceId: { } sourceCardInstanceId,
                CompletionEvents: [],
                OuterChoiceContinuation: { } continuation })
            return false;
        var operation = generation.Operation;
        var series = continuation.CardPlaySeries;
        return operation.Kind ==
                PrototypeCombatEffectKind.AcquireRandomCombatPotion &&
            operation.Amount == 1 &&
            operation.SourceKind == PrototypeEffectSourceKind.Card &&
            operation.TargetEnemyId is null && operation.Condition is null &&
            continuation.SourceCardInstanceId == sourceCardInstanceId &&
            continuation.SourceCardDestination ==
                PrototypeCardZone.ExhaustPile &&
            continuation.Operations.Length == 0 &&
            continuation.CompletionEvents.Length == 1 &&
            continuation.CompletionEvents[0] is
                { Kind: PrototypeCombatEventKind.CardPlayed,
                    CardId: "proto.colorless.alchemize" } &&
            continuation.CompletionEvents[0].SourceCardInstanceId ==
                sourceCardInstanceId &&
            series is not null &&
            series.SourceCardInstanceId == sourceCardInstanceId &&
            series.SourceCardDestination == PrototypeCardZone.ExhaustPile &&
            !series.IsAutoPlay && series.NextPlayIndex > 0 &&
            series.NextPlayIndex <= series.PlayCount &&
            continuation.Parent is null &&
            continuation.EventDispatchContinuation is null &&
            combat.Cards.Any(card =>
                card.InstanceId == sourceCardInstanceId &&
                card.CardId == "proto.colorless.alchemize");
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
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_card_transform");
        if (current.Run.Stochastic.SemanticOtherChance is
                { } otherChance && otherChance.ProviderId !=
                SemanticOtherChanceEngineV111.LegacyProviderId)
            ordinal += otherChance.Decisions.Count(decision =>
                decision.Request.SiteId ==
                    SemanticCardTransformChoiceV111.SiteId);
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
            current.Stochastic.SeededEntry?.FixtureNatureDrawCosts == true)
            request = drawCostRequest;
        else if (current.Run.World?.Combat?.PendingChoice?
                    .ControlledRandomCost is { } randomCost &&
                randomCost.Operation.Kind == PrototypeCombatEffectKind
                    .RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed)
        {
            SemanticRandomHandCostChoiceV111.ValidateBoundary(current.Run);
            if (current.Stochastic.SeededEntry?.FixtureRandomizeHandCostsMaxExclusive is
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
                StochasticSiteRegistryV111.CombatRandomHandCardEnergyCostSiteId,
                combat.Stochastic.NativeRng is not null
                    ? SemanticCostChoiceV111.NativeHandCostStreamId
                    : SemanticCostChoiceV111.LegacyHandCostStreamId,
                "effect:" + randomCost.Operation.Kind +
                    ":hand:" + CanonicalJson.Sha256(
                        randomCost.EligibleCardInstanceIds) +
                    ":index:" + randomCost.NextCardIndex,
                current.History.Count(transition =>
                    transition.Kind == "semantic_cost"), [],
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
                current.History.Count(transition =>
                    transition.Kind == "semantic_cost"));
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
        if ((current.Stochastic.SeededEntry?.FixtureSetRandomHandCardCost is
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
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_hand_cost_selection");
        return SemanticHandCostSelectionChoiceV111.CreateEvent(
            current.Fingerprint, randomCost, modelIds, ordinal,
            combat!.Stochastic.NativeRng is not null
                ? SemanticHandCostSelectionChoiceV111.NativeStreamId
                : SemanticHandCostSelectionChoiceV111.StreamId);
    }

    public static SemanticCardShortlistEventV111?
        DescribePendingCardShortlistEvent(SemanticPilotCombatV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.PendingEndTurn?.CardShortlistPending is
            { } endTurnEvent)
            return endTurnEvent;
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_card_shortlist");
        if (current.Run.Stochastic.SemanticOtherChance is
                { } otherChance && otherChance.ProviderId !=
                SemanticOtherChanceEngineV111.LegacyProviderId)
            ordinal += otherChance.Decisions.Count(decision =>
                decision.Request.SiteId is
                    StochasticSiteRegistryV111
                        .CombatRandomReplayCardSelectSiteId or
                    StochasticSiteRegistryV111
                        .CombatAutoplayRandomCardSelectSiteId);
        return SemanticCardShortlistChoiceV111.DescribePending(
            current.Run, current.Fingerprint, ordinal);
    }

    private static bool IsJackpotZeroCostGenerationChoice(
        CombatState? combat, PendingCombatChoiceState? choice,
        PrototypeControlledGeneratedCardV111? generation)
    {
        if (combat is null || generation is null ||
            choice is not { ChoiceId: "controlled_generated_card",
                SourceCardInstanceId: { } sourceCardInstanceId,
                CompletionEvents: [],
                OuterChoiceContinuation: { } continuation })
            return false;
        var sourceCard = combat.Cards.SingleOrDefault(card =>
            card.InstanceId == sourceCardInstanceId &&
            card.CardId == "proto.colorless.jackpot");
        if (sourceCard is null)
            return false;
        var operation = generation.Operation;
        var series = continuation.CardPlaySeries;
        var remainingOperationsMatch = operation.Amount switch
        {
            1 => continuation.Operations.Length == 0,
            2 or 3 => continuation.Operations.Length == 1 &&
                CanonicalJson.Sha256(continuation.Operations[0]) ==
                    CanonicalJson.Sha256(operation with
                    {
                        Amount = operation.Amount - 1
                    }),
            _ => false
        };
        return operation.Kind ==
                PrototypeCombatEffectKind.AddRandomZeroCostCardsToHand &&
            operation.SourceKind == PrototypeEffectSourceKind.Card &&
            operation.Amount is >= 1 and <= 3 &&
            operation.GeneratedCardUpgradeLevel == sourceCard.UpgradeLevel &&
            remainingOperationsMatch &&
            continuation.SourceCardInstanceId == sourceCardInstanceId &&
            continuation.CompletionEvents.Length == 1 &&
            continuation.CompletionEvents[0] is
                { Kind: PrototypeCombatEventKind.CardPlayed,
                    CardId: "proto.colorless.jackpot" } &&
            continuation.CompletionEvents[0].SourceCardInstanceId ==
                sourceCardInstanceId &&
            series is not null &&
            series.SourceCardInstanceId == sourceCardInstanceId &&
            !series.IsAutoPlay && series.NextPlayIndex > 0 &&
            series.NextPlayIndex <= series.PlayCount &&
            continuation.Parent is null &&
            continuation.EventDispatchContinuation is null;
    }

    private static bool IsNeowsAbundanceGeneratedChoiceShuffle(
        CombatState? combat, PendingCombatChoiceState? choice,
        PrototypeControlledPoolShuffleV111? shuffle)
    {
        if (combat is null ||
            choice is not { ChoiceId: "controlled_pool_shuffle",
                SourceCardInstanceId: { } sourceCardInstanceId } ||
            shuffle is null)
            return false;
        var operation = shuffle.Operation;
        return operation.Kind == PrototypeCombatEffectKind.ChooseGeneratedCards &&
            operation.Amount == 3 &&
            operation.SourceKind == PrototypeEffectSourceKind.Card &&
            operation.GeneratedChoiceCardType == PrototypeCardType.Power &&
            operation.GeneratedChoiceCardsFreeThisTurn &&
            operation.GeneratedChoiceCardsUpgraded &&
            operation.GeneratedChoiceMustPick &&
            !operation.GeneratedChoiceCharacterCards &&
            !operation.GeneratedChoiceCardsRetain &&
            !operation.GeneratedChoiceUsesNativeCardGenerationRng &&
            combat.Cards.Any(card =>
                card.InstanceId == sourceCardInstanceId &&
                card.CardId == "proto.native.neow.abundance");
    }

    private static bool IsDiscoveryGeneratedChoiceShuffle(
        CombatState? combat, PendingCombatChoiceState? choice,
        PrototypeControlledPoolShuffleV111? shuffle)
    {
        if (combat is null ||
            choice is not { ChoiceId: "controlled_pool_shuffle",
                SourceCardInstanceId: { } sourceCardInstanceId,
                CompletionEvents: [],
                OuterChoiceContinuation: { } continuation } ||
            shuffle is null)
            return false;
        var operation = shuffle.Operation;
        var series = continuation.CardPlaySeries;
        return operation.Kind == PrototypeCombatEffectKind.ChooseGeneratedCards &&
            operation.Amount == 3 &&
            operation.SourceKind == PrototypeEffectSourceKind.Card &&
            operation.Selection is null && operation.TargetEnemyId is null &&
            operation.GeneratedChoiceCardType is null &&
            operation.GeneratedChoiceCardsFreeThisTurn &&
            !operation.GeneratedChoiceCardsUpgraded &&
            !operation.GeneratedChoiceMustPick &&
            !operation.GeneratedChoiceCharacterCards &&
            !operation.GeneratedChoiceCardsRetain &&
            !operation.GeneratedChoiceUsesNativeCardGenerationRng &&
            continuation.SourceCardInstanceId == sourceCardInstanceId &&
            continuation.Operations.Length == 0 &&
            continuation.CompletionEvents.Length == 1 &&
            continuation.CompletionEvents[0] is
                { Kind: PrototypeCombatEventKind.CardPlayed,
                    CardId: "proto.colorless.discovery" } &&
            continuation.CompletionEvents[0].SourceCardInstanceId ==
                sourceCardInstanceId &&
            series is not null &&
            series.SourceCardInstanceId == sourceCardInstanceId &&
            !series.IsAutoPlay && series.NextPlayIndex > 0 &&
            series.NextPlayIndex <= series.PlayCount &&
            continuation.Parent is null &&
            continuation.EventDispatchContinuation is null &&
            combat.Cards.Any(card =>
                card.InstanceId == sourceCardInstanceId &&
                card.CardId == "proto.colorless.discovery");
    }

    private static bool IsSplashGeneratedChoiceShuffle(
        CombatState? combat, PendingCombatChoiceState? choice,
        PrototypeControlledPoolShuffleV111? shuffle)
    {
        if (combat is null ||
            choice is not { ChoiceId: "controlled_pool_shuffle",
                SourceCardInstanceId: { } sourceCardInstanceId,
                CompletionEvents: [],
                OuterChoiceContinuation: { } continuation } ||
            shuffle is null)
            return false;
        var sourceCard = combat.Cards.SingleOrDefault(card =>
            card.InstanceId == sourceCardInstanceId &&
            card.CardId == "proto.colorless.splash");
        if (sourceCard is null)
            return false;
        var operation = shuffle.Operation;
        var series = continuation.CardPlaySeries;
        return operation.Kind == PrototypeCombatEffectKind.ChooseGeneratedCards &&
            operation.Amount == 3 &&
            operation.SourceKind == PrototypeEffectSourceKind.Card &&
            operation.Selection is null && operation.TargetEnemyId is null &&
            operation.GeneratedChoiceCardType == PrototypeCardType.Attack &&
            operation.GeneratedChoiceCardsFreeThisTurn &&
            operation.GeneratedChoiceCardsUpgraded ==
                (sourceCard.UpgradeLevel > 0) &&
            !operation.GeneratedChoiceMustPick &&
            !operation.GeneratedChoiceCharacterCards &&
            !operation.GeneratedChoiceCardsRetain &&
            !operation.GeneratedChoiceUsesNativeCardGenerationRng &&
            continuation.SourceCardInstanceId == sourceCardInstanceId &&
            continuation.Operations.Length == 0 &&
            continuation.CompletionEvents.Length == 1 &&
            continuation.CompletionEvents[0] is
                { Kind: PrototypeCombatEventKind.CardPlayed,
                    CardId: "proto.colorless.splash" } &&
            continuation.CompletionEvents[0].SourceCardInstanceId ==
                sourceCardInstanceId &&
            series is not null &&
            series.SourceCardInstanceId == sourceCardInstanceId &&
            !series.IsAutoPlay && series.NextPlayIndex > 0 &&
            series.NextPlayIndex <= series.PlayCount &&
            continuation.Parent is null &&
            continuation.EventDispatchContinuation is null;
    }

    private static bool IsJackOfAllTradesDistinctColorlessShuffle(
        CombatState? combat, PendingCombatChoiceState? choice,
        PrototypeControlledPoolShuffleV111? shuffle)
    {
        if (combat is null ||
            choice is not { ChoiceId: "controlled_pool_shuffle",
                SourceCardInstanceId: { } sourceCardInstanceId,
                CompletionEvents: [],
                OuterChoiceContinuation: { } continuation } ||
            shuffle is null)
            return false;
        var sourceCard = combat.Cards.SingleOrDefault(card =>
            card.InstanceId == sourceCardInstanceId &&
            card.CardId == "proto.colorless.jack_of_all_trades");
        if (sourceCard is null)
            return false;
        var operation = shuffle.Operation;
        var series = continuation.CardPlaySeries;
        return operation.Kind ==
                PrototypeCombatEffectKind.CreateDistinctColorlessCardsInHand &&
            operation.Amount == 1 + sourceCard.UpgradeLevel &&
            operation.SourceKind == PrototypeEffectSourceKind.Card &&
            operation.Selection is null && operation.TargetEnemyId is null &&
            operation.Condition is null &&
            continuation.SourceCardInstanceId == sourceCardInstanceId &&
            continuation.SourceCardDestination == PrototypeCardZone.ExhaustPile &&
            continuation.Operations.Length == 0 &&
            continuation.CompletionEvents.Length == 1 &&
            continuation.CompletionEvents[0] is
                { Kind: PrototypeCombatEventKind.CardPlayed,
                    CardId: "proto.colorless.jack_of_all_trades" } &&
            continuation.CompletionEvents[0].SourceCardInstanceId ==
                sourceCardInstanceId &&
            series is not null &&
            series.SourceCardInstanceId == sourceCardInstanceId &&
            !series.IsAutoPlay && series.NextPlayIndex > 0 &&
            series.NextPlayIndex <= series.PlayCount &&
            continuation.Parent is null &&
            continuation.EventDispatchContinuation is null;
    }

    private static bool IsColorlessPotionGeneratedChoiceShuffle(
        CombatState? combat, PendingCombatChoiceState? choice,
        PrototypeControlledPoolShuffleV111? shuffle)
    {
        if (combat is null ||
            choice is not { ChoiceId: "controlled_pool_shuffle",
                SourceCardInstanceId: null, CompletionEvents: [],
                OuterChoiceContinuation: { } continuation } ||
            shuffle is null)
            return false;
        var operation = shuffle.Operation;
        return operation.Kind ==
                PrototypeCombatEffectKind.ChooseGeneratedColorlessCards &&
            operation.Amount == 3 &&
            operation.SourceKind == PrototypeEffectSourceKind.Potion &&
            operation.Selection is null && operation.TargetEnemyId is null &&
            operation.GeneratedChoiceCardType is null &&
            operation.GeneratedChoiceCardsFreeThisTurn &&
            !operation.GeneratedChoiceCardsUpgraded &&
            !operation.GeneratedChoiceMustPick &&
            !operation.GeneratedChoiceCharacterCards &&
            !operation.GeneratedChoiceCardsRetain &&
            !operation.GeneratedChoiceUsesNativeCardGenerationRng &&
            continuation.SourceCardInstanceId is null &&
            continuation.Operations.Length == 0 &&
            continuation.PendingDiscardEvents.Length == 0 &&
            continuation.PendingSlyCardInstanceIds.Length == 0 &&
            continuation.CompletionEvents is
                [{ Kind: PrototypeCombatEventKind.PotionUsed }] &&
            continuation.CardPlaySeries is null &&
            continuation.Parent is null &&
            continuation.EventDispatchContinuation is null;
    }

    private static bool IsAnointedRareToHandShuffle(
        CombatState? combat, PendingCombatChoiceState? choice,
        PrototypeControlledPoolShuffleV111? shuffle)
    {
        if (combat is null ||
            choice is not { ChoiceId: "controlled_pool_shuffle",
                SourceCardInstanceId: { } sourceCardInstanceId,
                CompletionEvents: [],
                OuterChoiceContinuation: { } continuation } ||
            shuffle is null)
            return false;
        var operation = shuffle.Operation;
        var series = continuation.CardPlaySeries;
        return operation.Kind ==
                PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand &&
            operation.Amount == 10 &&
            operation.SourceKind == PrototypeEffectSourceKind.Card &&
            operation.Selection is null && operation.Condition is null &&
            operation.TargetEnemyId is null &&
            continuation.SourceCardInstanceId == sourceCardInstanceId &&
            continuation.SourceCardDestination ==
                PrototypeCardZone.ExhaustPile &&
            continuation.Operations.Length == 0 &&
            continuation.CompletionEvents.Length == 1 &&
            continuation.CompletionEvents[0] is
                { Kind: PrototypeCombatEventKind.CardPlayed,
                    CardId: "proto.colorless.anointed" } &&
            continuation.CompletionEvents[0].SourceCardInstanceId ==
                sourceCardInstanceId &&
            series is not null &&
            series.SourceCardInstanceId == sourceCardInstanceId &&
            series.SourceCardDestination == PrototypeCardZone.ExhaustPile &&
            !series.IsAutoPlay && series.NextPlayIndex > 0 &&
            series.NextPlayIndex <= series.PlayCount &&
            continuation.Parent is null &&
            continuation.EventDispatchContinuation is null &&
            combat.Cards.Any(card =>
                card.InstanceId == sourceCardInstanceId &&
                card.CardId == "proto.colorless.anointed");
    }

    private static bool IsSeekerStrikeDrawPileShortlistShuffle(
        CombatState? combat, PendingCombatChoiceState? choice,
        PrototypeControlledPoolShuffleV111? shuffle)
    {
        if (combat is null ||
            choice is not { ChoiceId: "controlled_pool_shuffle",
                SourceCardInstanceId: { } sourceCardInstanceId,
                CompletionEvents: [],
                OuterChoiceContinuation: { } continuation } ||
            shuffle is null)
            return false;
        var operation = shuffle.Operation;
        var selection = operation.Selection;
        var series = continuation.CardPlaySeries;
        return operation.Kind == PrototypeCombatEffectKind.ChooseCards &&
            operation.Amount == 0 &&
            operation.SourceKind == PrototypeEffectSourceKind.Card &&
            operation.TargetEnemyId is null &&
            selection is
                { SourceZone: PrototypeCardZone.DrawPile,
                  MinSelections: 1, MaxSelections: 1,
                  Resolution: PrototypeCardSelectionResolutionKind.MoveToHand,
                  RandomCandidateCount: 3,
                  RemoveUnselectedFromSource: false,
                  SequentialOptional: false,
                  DrawEqualToSelectionsOnCompletion: false } &&
            continuation.SourceCardInstanceId == sourceCardInstanceId &&
            continuation.SourceCardDestination ==
                PrototypeCardZone.DiscardPile &&
            continuation.Operations.Length == 0 &&
            continuation.CompletionEvents.Length == 1 &&
            continuation.CompletionEvents[0] is
                { Kind: PrototypeCombatEventKind.CardPlayed,
                    CardId: "proto.colorless.seeker_strike" } &&
            continuation.CompletionEvents[0].SourceCardInstanceId ==
                sourceCardInstanceId &&
            series is not null &&
            series.SourceCardInstanceId == sourceCardInstanceId &&
            series.SourceCardDestination == PrototypeCardZone.DiscardPile &&
            !series.IsAutoPlay && series.NextPlayIndex > 0 &&
            series.NextPlayIndex <= series.PlayCount &&
            continuation.Parent is null &&
            continuation.EventDispatchContinuation is null &&
            combat.Cards.Any(card =>
                card.InstanceId == sourceCardInstanceId &&
                card.CardId == "proto.colorless.seeker_strike");
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
        bool colorlessPotionShuffle =
            IsColorlessPotionGeneratedChoiceShuffle(
                combat, choice, shuffle);
        bool anointedRareShuffle = IsAnointedRareToHandShuffle(
            combat, choice, shuffle);
        bool seekerStrikeShuffle = IsSeekerStrikeDrawPileShortlistShuffle(
            combat, choice, shuffle);
        bool discoveryShuffle = IsDiscoveryGeneratedChoiceShuffle(
            combat, choice, shuffle);
        bool splashShuffle = IsSplashGeneratedChoiceShuffle(
            combat, choice, shuffle);
        bool jackOfAllTradesShuffle =
            IsJackOfAllTradesDistinctColorlessShuffle(
                combat, choice, shuffle);
        bool mayhemRecycle = recycle && combat is not null && choice is not null &&
            PrototypeGameEngine.IsMayhemLegacyRecycleContinuation(
                combat, choice);
        bool automaticPipelinePoolShuffle = combat is not null &&
            choice is not null && PrototypeGameEngine
                .IsAutomaticPipelineSemanticPoolShuffleContinuation(
                    combat, choice);
        bool neowsAbundanceShuffle = IsNeowsAbundanceGeneratedChoiceShuffle(
            combat, choice, shuffle);
        var fixtureMatches = automaticPipelinePoolShuffle || handReturn ||
            recycle || colorlessPotionShuffle || anointedRareShuffle ||
            seekerStrikeShuffle || discoveryShuffle || splashShuffle ||
            jackOfAllTradesShuffle ||
            neowsAbundanceShuffle ||
            (randomRareToHand
            ? current.Stochastic.SeededEntry?.FixtureSelectionCardId ==
                "proto.colorless.anointed"
            : randomShortlist
                ? current.Stochastic.SeededEntry?.FixtureSelectionCardId is not null
                : current.Stochastic.SeededEntry?.FixturePoolGenerationPotionId ==
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
        var ordinal = current.History.Count(transition =>
            transition.Kind == "semantic_pool_shuffle");
        var siteId = SemanticPoolShuffleChoiceV111.SiteIdForOperation(
            shuffle.Operation.Kind.ToString()) ?? throw new InvalidDataException(
                "Full-pool shuffle operation has no registered site.");
        if (current.Run.Stochastic.SemanticOtherChance is
                { } otherChance && otherChance.ProviderId !=
                SemanticOtherChanceEngineV111.LegacyProviderId)
            ordinal += otherChance.Decisions.Count(decision =>
                decision.Request.SiteId == siteId);
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
            current.Stochastic.SeededEntry?.FixtureGeneratedDrawInsertionCardId ==
                insertion.Operation.CardId &&
            current.Stochastic.SeededEntry?.FixtureRandomFreeAttackDrawInsertion != true;
        var randomFixtureMatches =
            insertion.Operation.Kind == PrototypeCombatEffectKind
                .CreateRandomFreeAttacksInDrawPile &&
            current.Stochastic.SeededEntry?.FixtureGeneratedDrawInsertionCardId is null &&
            current.Stochastic.SeededEntry?.FixtureRandomFreeAttackDrawInsertion == true;
        var afterShuffleRelicMatches =
            insertion.AfterShuffleRelic is not null &&
            insertion.Operation.Kind == PrototypeCombatEffectKind
                .CreateGeneratedCardsAtRandomDrawPositions &&
            current.Stochastic.SeededEntry?.FixtureGeneratedDrawInsertionCardId is null &&
            current.Stochastic.SeededEntry?.FixtureRandomFreeAttackDrawInsertion != true;
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
            : current.History.Count(transition =>
                transition.Kind == "semantic_draw_insertion");
        if (!afterShuffleRelicMatches && !personalHiveInsertion &&
            current.Run.Stochastic.SemanticOtherChance is
                { } otherChance && otherChance.ProviderId !=
                SemanticOtherChanceEngineV111.LegacyProviderId)
            requestOrdinal += otherChance.Decisions.Count(decision =>
                decision.Request.SiteId == insertion.RequestSiteId);
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
    /// The two-site pilot keeps registered random outcomes outside Nature.
    /// The run-owned Native provider resolves supported semantic
    /// continuations; older snapshots without that owner retain their
    /// compatibility behavior.
    /// </summary>
    private static SemanticPilotCombatV111 ResolveEngineOwnedSemanticChoices(
        SemanticPilotCombatV111 current)
    {
        while (true)
        {
            if (current.EnemyActionPending is { } enemyAction &&
                enemyAction.SiteId == StochasticSiteRegistryV111
                    .CombatEnemyTheftCardSelectSiteId &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } enemyTheftOwner &&
                enemyTheftOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId)
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolveEnemyTheft(enemyTheftOwner, enemyAction);
                current = Resolve(
                    current, current.Fingerprint,
                    choice.Decision.SelectedIndex, null, null, false, null,
                    otherChance: choice.Next);
                continue;
            }

            if (current.EnemyActionPending is { } nativeEnemyAction &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } nativeEnemyActionOwner &&
                nativeEnemyActionOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId &&
                StochasticSiteRegistryV111.SupportsEngineOwnedNativeBoundary(
                    nativeEnemyAction.SiteId, nativeEnemyAction.StreamId,
                    nativeEnemyAction.OperationKind))
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolveEnemyActionRequest(
                        nativeEnemyActionOwner, nativeEnemyAction);
                current = Resolve(
                    current, current.Fingerprint,
                    choice.Decision.SelectedIndex, null, null, false, null,
                    otherChance: choice.Next);
                continue;
            }

            if (current.TargetPending is { } target &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } targetOwner &&
                targetOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId)
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolveRandomTarget(targetOwner, target);
                current = ResolveRandomTarget(
                    current, current.Fingerprint, target.EventId,
                    target.CandidateEnemyInstanceIds[
                        choice.Decision.SelectedIndex], choice.Next);
                continue;
            }

            if (current.GeneratedCardPending is { } generatedCard &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } generatedCardOwner &&
                generatedCardOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId)
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolveGeneratedCard(generatedCardOwner, generatedCard);
                current = ResolveGeneratedCard(
                    current, current.Fingerprint, generatedCard.EventId,
                    generatedCard.CandidateCardIds[
                        choice.Decision.SelectedIndex], choice.Next);
                continue;
            }

            if (current.DrawInsertionPending is { } drawInsertion &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } drawInsertionOwner &&
                drawInsertionOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId &&
                SemanticOtherChanceEngineV111
                    .SupportsEngineOwnedDrawInsertionBoundary(drawInsertion))
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolveDrawInsertion(drawInsertionOwner, drawInsertion);
                current = ResolveDrawInsertion(
                    current, current.Fingerprint, drawInsertion.EventId,
                    choice.Decision.SelectedIndex, choice.Next);
                continue;
            }

            if (current.PotionGenerationPending is { } generatedPotion &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } potionGenerationOwner &&
                potionGenerationOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId)
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolvePotionGeneration(
                        potionGenerationOwner, generatedPotion);
                current = ResolvePotionGeneration(
                    current, current.Fingerprint, generatedPotion.EventId,
                    generatedPotion.CandidatePotionIds[
                        choice.Decision.SelectedIndex], choice.Next);
                continue;
            }

            if (current.CardTransformPending is { } cardTransform &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } cardTransformOwner &&
                cardTransformOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId)
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolveCardTransform(cardTransformOwner, cardTransform);
                current = ResolveCardTransform(
                    current, current.Fingerprint, cardTransform.EventId,
                    cardTransform.CandidateCardIds[
                        choice.Decision.SelectedIndex], choice.Next);
                continue;
            }

            if (current.PoolShufflePending is { } poolShuffle &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } poolShuffleOwner &&
                poolShuffleOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId &&
                SemanticOtherChanceEngineV111
                    .SupportsEngineOwnedPoolShuffleBoundary(poolShuffle))
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolvePoolShuffle(poolShuffleOwner, poolShuffle);
                current = ResolvePoolShuffle(
                    current, current.Fingerprint, poolShuffle.EventId,
                    choice.Decision.SelectedIndex, choice.Next);
                continue;
            }

            if (current.CardShortlistPending is { } cardShortlist &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } cardShortlistOwner &&
                cardShortlistOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId &&
                SemanticOtherChanceEngineV111
                    .SupportsEngineOwnedCardShortlistBoundary(cardShortlist))
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolveCardShortlist(cardShortlistOwner, cardShortlist);
                current = ResolveCardShortlist(
                    current, current.Fingerprint, cardShortlist.EventId,
                    cardShortlist.CandidateCardInstanceIds[
                        choice.Decision.SelectedIndex], choice.Next);
                continue;
            }

            if (current.RandomPileCardPending is { } randomPileCard &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } randomPileCardOwner &&
                randomPileCardOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId)
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolveRandomPileCard(
                        randomPileCardOwner, randomPileCard);
                current = ResolveRandomPileCard(
                    current, current.Fingerprint, randomPileCard.EventId,
                    randomPileCard.CandidateCardInstanceIds[
                        choice.Decision.SelectedIndex], choice.Next);
                continue;
            }

            if (current.HandCostSelectionPending is { } handCost &&
                current.Stochastic.SeededEntry?.FixtureSetRandomHandCardCost
                    is null &&
                current.Run.Stochastic.SemanticOtherChance is
                    { } handOwner &&
                handOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId)
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolveHandCostTarget(handOwner, handCost);
                var decision = SemanticHandCostSelectionChoiceV111.Select(
                    handCost, handCost.EventId,
                    handCost.CandidateCardInstanceIds[
                        choice.Decision.SelectedIndex]);
                current = ResumeHandCostSelectionDecision(
                    current, decision, choice.Next);
                continue;
            }

            if (current.CostPending is { } semanticCost &&
                !(current.Stochastic.SeededEntry?.FixtureNatureDrawCosts == true &&
                  (semanticCost.Request.SiteId is
                    StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId or
                    StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId)) &&
                !(current.Stochastic.SeededEntry?
                    .FixtureRandomizeHandCostsMaxExclusive is not null &&
                  semanticCost.Request.SiteId ==
                    StochasticSiteRegistryV111
                        .CombatRandomHandCardEnergyCostSiteId) &&
                current.Run.Stochastic.SemanticOtherChance is { } costOwner &&
                (costOwner.ProviderId !=
                    SemanticOtherChanceEngineV111.LegacyProviderId ||
                 (semanticCost.Request.SiteId is
                    StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId or
                    StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId)))
            {
                var choice = SemanticOtherChanceEngineV111
                    .ResolveNonNatureCost(costOwner, semanticCost);
                var decision = SemanticCostChoiceV111.Select(
                    semanticCost, semanticCost.EventId,
                    semanticCost.CandidateCosts[
                        choice.Decision.SelectedIndex]);
                current = ResumeCostDecision(
                    current, decision, choice.Next);
                continue;
            }

            if (current.PendingEndTurn is
                { CostPending: { } cost } endTurn)
            {
                if (current.Stochastic.SeededEntry?.FixtureNatureDrawCosts ==
                    true)
                    break;
                var bound = cost.SupportCount;
                if (bound < 1)
                    throw new InvalidDataException(
                        "Invalid engine-owned cost bound.");
                SemanticOtherChanceStateV111? nextOther = null;
                int index;
                if (current.Run.Stochastic.SemanticOtherChance is
                    { } other)
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
                    index = (int)(BitConverter.ToUInt32(hash, 0) %
                        (uint)bound);
                }
                var resolved = SemanticEndTurnEngineV111.Resolve(
                    endTurn, endTurn.Fingerprint, index);
                current = PublishRunContinuation(
                    current, resolved.Run, resolved,
                    otherChance: nextOther);
                continue;
            }
            break;
        }
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
        var rewardAudit = run.Phase == RunPhase.Reward
            ? RecordRewardAudit(current.Run, run)
            : current.Run.Stochastic.SemanticRewardAudit;
        var committedRun = RunStochasticStateV111
            .PublishSemanticPilotContinuation(
                run, current.Run, otherChance, rewardAudit);
        var next = current with
        {
            Run = committedRun
        };
        if (!preservePendingEndTurn)
            next = next with
            {
                PendingEndTurn = pendingEndTurn is { Complete: false }
                    ? pendingEndTurn
                    : null
            };
        return historyEntry is null
            ? next
            : next with
            {
                Mechanics = next.Mechanics.AppendHistory(historyEntry)
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
        var replay = saved.Stochastic.SeededEntry is { } entry
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
                    ResumeGeneratedCardDecision(
                        replay, transition.GeneratedCardDecision),
                "semantic_potion_generation" when
                    transition.PotionGenerationDecision is not null =>
                    ResumePotionGenerationDecision(
                        replay, transition.PotionGenerationDecision),
                "semantic_card_transform" when
                    transition.CardTransformDecision is not null =>
                    ResumeCardTransformDecision(
                        replay, transition.CardTransformDecision),
                "semantic_card_selection" when
                    transition.CardSelectionDecision is not null =>
                    Step(replay, replay.Fingerprint,
                        transition.CardSelectionDecision.ActionId),
                "semantic_cost" when transition.CostDecision is not null =>
                    ResumeCostDecision(replay, transition.CostDecision),
                "semantic_hand_cost_selection" when
                    transition.HandCostSelectionDecision is not null =>
                    ResumeHandCostSelectionDecision(
                        replay, transition.HandCostSelectionDecision),
                "semantic_card_shortlist" when
                    transition.CardShortlistDecision is not null =>
                    ResumeCardShortlistDecision(
                        replay, transition.CardShortlistDecision),
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
        if (!PrototypeGameEngine.IsAtCombatPlayerDecisionBoundary(run))
            throw new InvalidDataException(
                "Native policy action did not reach an active combat boundary.");
        var combat = run.World!.Combat!;
        if (combat.Stochastic.NativeRng is null || run.Stochastic.NativeRunRng is null)
            throw new InvalidDataException(
                "Native policy combat lost its RNG stream owner.");
        if (combat.Stochastic.SemanticDeck is not { } deck ||
            deck.Mode != SemanticDeckStateV111.NativeMode ||
            deck.Pending is not null)
            throw new InvalidDataException(
                "Native policy combat lost its exact semantic deck state.");
        SemanticDrawPileAdapterV111.ValidateSynchronized(combat);
    }
}
