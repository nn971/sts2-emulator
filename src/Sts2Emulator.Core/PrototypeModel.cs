using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public enum PrototypeRoomType
{
    Combat,
    Elite,
    Event,
    Shop,
    Rest,
    Boss,
    Unknown,
    Treasure
}

public enum PrototypeActOneRegion
{
    Overgrowth,
    Underdocks
}

public sealed record PrototypeActOneEncounterPoolState
{
    private string[] _remainingWeakEncounterIds;
    private string[]? _remainingNormalEncounterIds;
    private string[]? _remainingEliteEncounterIds;

    [JsonConstructor]
    public PrototypeActOneEncounterPoolState(
        PrototypeActOneRegion Region,
        int OrdinaryCombatsStarted,
        string[] RemainingWeakEncounterIds,
        string[]? RemainingNormalEncounterIds = null,
        string[]? RemainingEliteEncounterIds = null,
        string? BossEncounterId = null)
    {
        this.Region = Region;
        this.OrdinaryCombatsStarted = OrdinaryCombatsStarted;
        _remainingWeakEncounterIds = CloneRequired(
            RemainingWeakEncounterIds, nameof(RemainingWeakEncounterIds));
        _remainingNormalEncounterIds = CloneOptional(
            RemainingNormalEncounterIds);
        _remainingEliteEncounterIds = CloneOptional(
            RemainingEliteEncounterIds);
        this.BossEncounterId = BossEncounterId;
    }

    public PrototypeActOneRegion Region { get; init; }
    public int OrdinaryCombatsStarted { get; init; }
    public string[] RemainingWeakEncounterIds
    {
        get => (string[])_remainingWeakEncounterIds.Clone();
        init => _remainingWeakEncounterIds = CloneRequired(
            value, nameof(RemainingWeakEncounterIds));
    }
    public string[]? RemainingNormalEncounterIds
    {
        get => CloneOptional(_remainingNormalEncounterIds);
        init => _remainingNormalEncounterIds = CloneOptional(value);
    }
    public string[]? RemainingEliteEncounterIds
    {
        get => CloneOptional(_remainingEliteEncounterIds);
        init => _remainingEliteEncounterIds = CloneOptional(value);
    }
    public string? BossEncounterId { get; init; }

    public PrototypeActOneEncounterPoolState Fork() => this with
    {
        RemainingWeakEncounterIds = _remainingWeakEncounterIds,
        RemainingNormalEncounterIds = _remainingNormalEncounterIds,
        RemainingEliteEncounterIds = _remainingEliteEncounterIds
    };

    public void Deconstruct(
        out PrototypeActOneRegion Region,
        out int OrdinaryCombatsStarted,
        out string[] RemainingWeakEncounterIds,
        out string[]? RemainingNormalEncounterIds,
        out string[]? RemainingEliteEncounterIds,
        out string? BossEncounterId)
    {
        Region = this.Region;
        OrdinaryCombatsStarted = this.OrdinaryCombatsStarted;
        RemainingWeakEncounterIds = this.RemainingWeakEncounterIds;
        RemainingNormalEncounterIds = this.RemainingNormalEncounterIds;
        RemainingEliteEncounterIds = this.RemainingEliteEncounterIds;
        BossEncounterId = this.BossEncounterId;
    }

    private static string[] CloneRequired(string[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (string[])values.Clone();
    }

    private static string[]? CloneOptional(string[]? values) =>
        values is null ? null : (string[])values.Clone();
}

public enum PrototypeCardRarity
{
    Basic,
    Common,
    Uncommon,
    Rare,
    Ancient,
    Event,
    Curse,
    Status,
    Token,
    Quest
}

public enum PrototypeCardType
{
    Unknown,
    Attack,
    Skill,
    Power,
    Status,
    Curse,
    Quest
}

public enum PrototypeCardTarget
{
    None,
    Enemy
}

public enum PrototypeCombatEffectKind
{
    DamageEnemy,
    DamageAllEnemiesRepeatPerKill,
    LoseEnemyHp,
    DiscardHand,
    ExhaustHand,
    RemoveEnemyBlock,
    RemoveEnemyPower,
    TriggerEnemyStatus,
    GainPlayerBlock,
    MultiplyPlayerBlock,
    DrawCards,
    RecycleDrawPileForEffectDraw,
    MoveRandomRareDrawCardsToHand,
    MoveRandomPowerDrawCardToHandFree,
    AddRandomZeroCostCardsToHand,
    EmpowerRandomDrawCardReplay,
    AcquireRandomCombatPotion,
    CreateRandomCharacterAttackCardsInHand,
    CreateRandomFreeAttacksInDrawPile,
    CreateGeneratedCardsAtRandomDrawPositions,
    AutoPlayRandomCardsFromZone,
    IncreaseSourcePowerStacks,
    ApplyEnemyStatus,
    ChooseCards,
    ChooseGeneratedCards,
    CreateDistinctColorlessCardsInHand,
    CreateCardsInHand,
    CreateCardsInHandFromPowerCardPayload,
    CreateEventSourceCardCopyInHand,
    AutoPlayTaggedCardsFromZone,
    AutoPlayTopDrawCards,
    AutoPlayPlayableCardsFromHand,
    AutoPlayCombatCard,
    ApplyPlayerPower,
    ApplyEnemyPower,
    HealPlayer,
    UpgradeHandCards,
    UpgradeAllCards,
    DamagePlayer,
    GainEnergy,
    MultiplyEnemyStatus,
    GainPlayerBlockAndApplyPowerFromActualGain,
    GainToricToughnessBlock,
    ModifySourceCardEnergyCost,
    SetHandCardsEnergyCostUntilTurnEndOrPlayed,
    ReduceHandCardCostsToOne,
    SetRandomHandCardEnergyCostUntilTurnEndOrPlayed,
    RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed,
    ModifyEventSourceCardKeyword,
    GainPlayerBlockFromEnemyStatusTotal,
    CreateRandomCharacterSkillCardsInHand,
    CreateDistinctCharacterCommonCardsInHand,
    CreateRandomFreeCharacterCardInHand,
    GainEnergyForGold,
    LosePlayerMaxHp,
    ReturnHandToDrawAndShuffle,
    ChooseGeneratedColorlessCards,
    CreateRandomCharacterPowerCardsInHand,
    StunEnemy,
    BuffMaulDamage
}

public enum PrototypeCardKeyword
{
    Retain,
    Sly,
    Ethereal
}

public enum PrototypeCardAfflictionKind
{
    Bound,
    Entangled,
    Galvanized,
    Hexed,
    Ringing,
    Smog,
    Tainted
}

public sealed record PrototypeCardAffliction(
    PrototypeCardAfflictionKind Kind,
    int Amount = 1,
    int? SourceEnemyInstanceId = null);

[Flags]
public enum PrototypeCardKeywordOverrideExpiry
{
    None = 0,
    EndOfTurn = 1,
    WhenPlayed = 2
}

public sealed record PrototypeCardKeywordOverride(
    PrototypeCardKeyword Keyword,
    bool Enabled,
    PrototypeCardKeywordOverrideExpiry Expiry);

[Flags]
public enum PrototypeTemporaryCardCostExpiry
{
    None = 0,
    EndOfTurn = 1,
    WhenPlayed = 2
}

public sealed record PrototypeTemporaryCardCost(
    int Cost,
    PrototypeTemporaryCardCostExpiry Expiry);

public sealed record PrototypeCombatCardSnapshot(
    string CardId,
    int UpgradeLevel,
    JsonElement State,
    int CombatEnergyCostDelta = 0,
    PrototypeTemporaryCardCost? TemporaryEnergyCost = null,
    PrototypeCardKeywordOverride[]? KeywordOverrides = null,
    int ReplayCount = 0,
    PrototypeCardEnchantment? Enchantment = null,
    bool EnchantmentTriggeredThisCombat = false,
    PrototypeCardAffliction? Affliction = null,
    int SuppressedUpgradeLevels = 0,
    int WitherFakeUpgradeLevel = 0)
{
    private JsonElement _state = State.Clone();
    private PrototypeCardKeywordOverride[]? _keywordOverrides =
        CloneKeywordOverrides(KeywordOverrides);

    public JsonElement State
    {
        get => _state.Clone();
        init => _state = value.Clone();
    }

    public PrototypeCardKeywordOverride[]? KeywordOverrides
    {
        get => CloneKeywordOverrides(_keywordOverrides);
        init => _keywordOverrides = CloneKeywordOverrides(value);
    }

    public PrototypeCombatCardSnapshot Fork() => this with
    {
        State = _state,
        TemporaryEnergyCost = TemporaryEnergyCost is null
            ? null
            : TemporaryEnergyCost with { },
        KeywordOverrides = _keywordOverrides
    };

    private static PrototypeCardKeywordOverride[]? CloneKeywordOverrides(
        PrototypeCardKeywordOverride[]? values) => values?.Select(item =>
            item is null ? null! : item with { }).ToArray();
}

public enum PrototypeCardZone
{
    Hand,
    DrawPile,
    DiscardPile,
    ExhaustPile,
    PlayPile,
    ChoicePool
}

public enum PrototypeCardSelectionResolutionKind
{
    Preserve,
    MoveToHand,
    MoveToDiscard,
    MoveToExhaust,
    MoveToDrawTop,
    TransformRandom,
    CopyToHand
}

public enum PrototypeEffectTarget
{
    ActionTargetEnemy,
    AllEnemies,
    RandomEnemy,
    SourcePowerOwnerEnemy
}

public enum PrototypeEffectSourceKind
{
    System,
    Card,
    Potion,
    Power,
    Relic
}

public enum PrototypeCombatPredicateKind
{
    DrawPileEmpty,
    TargetHasStatus,
    HandEmptyAtEnqueue,
    HandHasNoAttacks,
    HandHasOnlyAttacks
}

public enum PrototypeCombatCountKind
{
    SkillsInHand,
    SkillsPlayedThisTurn,
    AttacksPlayedThisTurn,
    CardsDiscardedThisTurn,
    CardsDrawnThisCombat,
    CardsPlayedThisCombat,
    OtherCardsInHand,
    DrawPileCards,
    PlayerBlock,
    TargetDebuffs,
    DiscardPileCards
}

public sealed record PrototypeCombatPredicateSpec(
    PrototypeCombatPredicateKind Kind,
    string? StatusId = null);

public enum PrototypeCardCostKind
{
    Fixed,
    X
}

public enum PrototypeTurnStage
{
    EnemyTurnStart,
    EnemyAction,
    EnemyTurnEnd,
    PlayerTurnStart
}

public enum PrototypeCombatEventKind
{
    CombatStarted,
    CombatWon,
    PotionUsed,
    PlayerTurnStarted,
    PlayerTurnEnded,
    AutoPrePlayPhaseEntered,
    BeforeHandDraw,
    CardPlayed,
    CardDrawn,
    CardDiscarded,
    CardExhausted,
    EnemyDamaged,
    EnemyDefeated,
    PlayerSideTurnStarted,
    OpeningPlayerTurnStarted
}

public sealed record PrototypeCombatEvent(
    PrototypeCombatEventKind Kind,
    long? SourceCardInstanceId = null,
    string? CardId = null,
    int? TargetEnemyId = null,
    int Amount = 0,
    long? PowerApplicationOrderCeiling = null,
    bool FromHandDraw = false,
    bool IsAutoPlay = false,
    bool IsPoweredAttack = false);

public sealed record PrototypeEventSubscriberState(
    long ApplicationOrder,
    PrototypeCombatEffectSpec[] Effects,
    int PowerStacks,
    PrototypeEffectSourceKind SourceKind,
    long? SourcePowerApplicationOrder = null,
    int? SourcePowerEnemyId = null,
    PrototypeCombatCardSnapshot? PowerCardPayload = null,
    bool RemoveSourcePowerAfterTrigger = false,
    int? RelicStateIndex = null,
    int? RelicTriggerIndex = null,
    int EveryNth = 1,
    int? MaxTriggersPerCounterWindow = null,
    int? PowerTriggerIndex = null,
    bool CountdownBeforeTrigger = false,
    int SourcePowerStoredValue = 0)
{
    private PrototypeCombatEffectSpec[]? _effects = CloneEffects(Effects);
    private PrototypeCombatCardSnapshot? _powerCardPayload =
        PowerCardPayload?.Fork();

    public PrototypeCombatEffectSpec[] Effects
    {
        get => CloneEffects(_effects)!;
        init => _effects = CloneEffects(value);
    }

    public PrototypeCombatCardSnapshot? PowerCardPayload
    {
        get => _powerCardPayload?.Fork();
        init => _powerCardPayload = value?.Fork();
    }

    public PrototypeEventSubscriberState Fork() => this with
    {
        Effects = _effects!,
        PowerCardPayload = _powerCardPayload
    };

    private static PrototypeCombatEffectSpec[]? CloneEffects(
        PrototypeCombatEffectSpec[]? effects) => effects is null
        ? null
        : (PrototypeCombatEffectSpec[])effects.Clone();
}

public sealed record PrototypeEventDispatchContinuationState(
    PrototypeCombatEvent CombatEvent,
    PrototypeEventSubscriberState CurrentSubscriber,
    PrototypeEventSubscriberState[] RemainingSubscribers,
    int EventDepth)
{
    private PrototypeCombatEvent? _combatEvent = CombatEvent is null
        ? null
        : CombatEvent with { };
    private PrototypeEventSubscriberState? _currentSubscriber =
        CurrentSubscriber?.Fork();
    private PrototypeEventSubscriberState[]? _remainingSubscribers =
        CloneSubscribers(RemainingSubscribers);

    public PrototypeCombatEvent CombatEvent
    {
        get => _combatEvent is null ? null! : _combatEvent with { };
        init => _combatEvent = value is null ? null : value with { };
    }

    public PrototypeEventSubscriberState CurrentSubscriber
    {
        get => _currentSubscriber?.Fork()!;
        init => _currentSubscriber = value?.Fork();
    }

    public PrototypeEventSubscriberState[] RemainingSubscribers
    {
        get => CloneSubscribers(_remainingSubscribers)!;
        init => _remainingSubscribers = CloneSubscribers(value);
    }

    public PrototypeEventDispatchContinuationState Fork() => this with
    {
        CombatEvent = _combatEvent!,
        CurrentSubscriber = _currentSubscriber!,
        RemainingSubscribers = _remainingSubscribers!
    };

    private static PrototypeEventSubscriberState[]? CloneSubscribers(
        PrototypeEventSubscriberState[]? subscribers) => subscribers?.Select(
            subscriber => subscriber is null ? null! : subscriber.Fork())
            .ToArray();
}

public enum PrototypeOpeningCombatStage
{
    PlayerSideStart,
    BeforeHandDraw,
    DrawHand,
    AfterHandDraw,
    CombatStarted,
    PlayerTurnStarted,
    AutoPrePlay,
    ImbuedAutoPlay,
    LateRelicAutoPlay,
    Complete,
    BoneTeaAfterTurnStart,
    InitialEnemyIntents,
    // Appended to preserve serialized enum values. This is the native
    // post-dispatch History Course policy boundary after AutoPrePlay.
    AfterAutoPrePlayPolicy
}

public sealed record PrototypeAutomaticPipelineContinuationState(
    int NextStepIndex,
    PrototypeCombatEventKind? PendingPostDispatchEventKind = null,
    PrototypeOpeningCombatStage? OpeningStage = null,
    int? ControlledDrawRemaining = null,
    int? ControlledDrawCostRemaining = null,
    // Separately versioned semantic Nature cursor: it tracks one ordinary
    // DrawPlayerHand invocation after BeforeHandDraw, independent of the
    // legacy Fisher-Yates swap and drawn-cost continuation markers.
    int? ControlledSemanticDrawRemaining = null,
    int? ControlledSemanticDrawRequested = null,
    // BeforeHandDraw can itself contain a semantic chance event. This marker
    // resumes after the saved event-dispatch continuation, before applying
    // the deterministic hand-draw prelude and starting DrawCards.
    bool ControlledSemanticBeforeHandDrawPending = false,
    bool ControlledRecycleStratagemPending = false,
    bool ControlledDrawHookChoicePending = false,
    bool ControlledBeforeHandDrawHookChoicePending = false,
    PrototypeControlledEnemyActionContinuationV111? ControlledEnemyAction = null,
    ControlledTurnIntentsV111? ControlledTurnIntents = null)
{
    /// <summary>
    /// Atomic mechanics/provider envelope for the automatic pipeline. Existing
    /// continuation members remain flattened snapshot projections.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<PrototypeAutomaticPipelineMechanicsStateV111,
        PrototypeAutomaticPipelineStochasticStateV111> Simulation { get; init; } =
        new(
            new PrototypeAutomaticPipelineMechanicsStateV111(
                NextStepIndex, PendingPostDispatchEventKind, OpeningStage),
            PrototypeAutomaticPipelineStochasticStateV111.FromFlattenedProgress(
                ControlledDrawRemaining, ControlledDrawCostRemaining,
                ControlledSemanticDrawRemaining,
                ControlledSemanticDrawRequested,
                ControlledSemanticBeforeHandDrawPending,
                ControlledRecycleStratagemPending,
                ControlledDrawHookChoicePending,
                ControlledBeforeHandDrawHookChoicePending,
                ControlledEnemyAction,
                ControlledTurnIntents));

    /// <summary>Mechanics projection of <see cref="Simulation"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public PrototypeAutomaticPipelineMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    /// <summary>Provider projection of <see cref="Simulation"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public PrototypeAutomaticPipelineStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    /// <summary>
    /// Replace the active hand-draw boundary as one validated provider-state
    /// transition. Boundary adapters should not assemble it from flattened
    /// counter properties, which can temporarily describe conflicting modes.
    /// </summary>
    public PrototypeAutomaticPipelineContinuationState WithHandDrawProgress(
        PrototypeAutomaticHandDrawProgressV111? progress)
    {
        progress?.Validate();
        return this with
        {
            Stochastic = Stochastic with
            {
                HandDrawProgress = progress,
                CompletedSemanticHandDrawRequested = null
            }
        };
    }

    public PrototypeAutomaticPipelineContinuationState
        WithCompletedSemanticHandDrawReceipt(int? requestedCount) => this with
    {
        Stochastic = Stochastic.WithCompletedSemanticHandDrawReceipt(
            requestedCount)
    };

    public int NextStepIndex
    {
        get => Mechanics.NextStepIndex;
        init => Mechanics = Mechanics with { NextStepIndex = value };
    }

    public PrototypeCombatEventKind? PendingPostDispatchEventKind
    {
        get => Mechanics.PendingPostDispatchEventKind;
        init => Mechanics = Mechanics with
        { PendingPostDispatchEventKind = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeOpeningCombatStage? OpeningStage
    {
        get => Mechanics.OpeningStage;
        init => Mechanics = Mechanics with { OpeningStage = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? ControlledDrawRemaining
    {
        get => Stochastic.ControlledDrawRemaining;
        init => Stochastic = Stochastic with { ControlledDrawRemaining = value };
    }

    public int? ControlledDrawCostRemaining
    {
        get => Stochastic.ControlledDrawCostRemaining;
        init => Stochastic = Stochastic with
        { ControlledDrawCostRemaining = value };
    }

    public int? ControlledSemanticDrawRemaining
    {
        get => Stochastic.ControlledSemanticDrawRemaining;
        init => Stochastic = Stochastic with
        { ControlledSemanticDrawRemaining = value };
    }

    public int? ControlledSemanticDrawRequested
    {
        get => Stochastic.ControlledSemanticDrawRequested;
        init => Stochastic = Stochastic with
        { ControlledSemanticDrawRequested = value };
    }

    public bool ControlledSemanticBeforeHandDrawPending
    {
        get => Stochastic.ControlledSemanticBeforeHandDrawPending;
        init => Stochastic = Stochastic with
        { ControlledSemanticBeforeHandDrawPending = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ControlledRecycleStratagemPending
    {
        get => Stochastic.ControlledRecycleStratagemPending;
        init => Stochastic = Stochastic with
        { ControlledRecycleStratagemPending = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ControlledDrawHookChoicePending
    {
        get => Stochastic.ControlledDrawHookChoicePending;
        init => Stochastic = Stochastic with
        { ControlledDrawHookChoicePending = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ControlledBeforeHandDrawHookChoicePending
    {
        get => Stochastic.ControlledBeforeHandDrawHookChoicePending;
        init => Stochastic = Stochastic with
        { ControlledBeforeHandDrawHookChoicePending = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeControlledEnemyActionContinuationV111?
        ControlledEnemyAction
    {
        get => Stochastic.ControlledEnemyAction;
        init => Stochastic = Stochastic with
        { ControlledEnemyAction = value };
    }

    /// <summary>
    /// Nature's intent frame is mechanically suspended at this pipeline's
    /// AdvanceTurn cursor, or at the source-route InitialEnemyIntents opening
    /// cursor. Facades retain flattened serialized projections for compatibility.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledTurnIntentsV111? ControlledTurnIntents
    {
        get => Stochastic.ControlledTurnIntents;
        init => Stochastic = Stochastic with
        { ControlledTurnIntents = value };
    }

    public void Validate(CombatState combat)
    {
        Mechanics.Validate(combat);
        Stochastic.Validate(combat, this);
    }

    /// <summary>
    /// Validate the composed cursor and provider payload, then report
    /// whether all blocking provider outcomes have been committed so the
    /// automatic mechanics runner may advance.
    /// </summary>
    public bool IsReadyForAutomaticPipelineResume(CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        Validate(combat);
        return combat.Stochastic.SemanticDeck?.Pending is null &&
            !Stochastic.HasSavedHandDrawProgress() &&
            Stochastic.ControlledEnemyAction is null &&
            Stochastic.ControlledTurnIntents is null;
    }

    public PrototypeAutomaticPipelineContinuationState Fork() => this with
    {
        Stochastic = Stochastic.Fork()
    };
}

/// <summary>
/// A source-owned, blocking choice in the enemy-side action pipeline.
/// The two displayed choices are ephemeral status-card models: choosing
/// one applies its power immediately and never inserts a deck card.
/// </summary>
public sealed record PrototypeKnowledgeDemonChoiceState(
    int EnemyInstanceId, int EnemyTurn, int CurseIndex,
    string[] OptionCardIds)
{
    private string[]? _optionCardIds = CloneOptionCardIds(OptionCardIds);

    public string[] OptionCardIds
    {
        get => CloneOptionCardIds(_optionCardIds)!;
        init => _optionCardIds = CloneOptionCardIds(value);
    }

    public PrototypeKnowledgeDemonChoiceState Fork() => this with
    {
        OptionCardIds = _optionCardIds!
    };

    private static string[]? CloneOptionCardIds(string[]? values) =>
        values is null ? null : (string[])values.Clone();
}

public enum PrototypeAutomaticStepKind
{
    ResolvePlayerEndTurnHandEffects,
    DispatchCombatEvent,
    DiscardPlayerHand,
    DispatchEnemyStatusStage,
    ResetEnemyBlock,
    ResolveEnemyActions,
    AdvanceTurn,
    ResetPlayerBlock,
    RefreshPlayerEnergy,
    DrawPlayerHand,
    ResolveLateRelicAutoplay
}

public sealed record PrototypeAutomaticStep(
    PrototypeAutomaticStepKind Kind,
    PrototypeTurnStage? Stage = null,
    PrototypeCombatEventKind? EventKind = null);

public enum PrototypeStatusTriggerKind
{
    None,
    DamageSelfByStacks
}

public enum PrototypeRunEffectKind
{
    Heal,
    HealToFull,
    HealPercentMaxHp,
    GainMaxHp,
    LoseMaxHp,
    LoseHp,
    LoseHpAfterDeckChoices,
    GainGold,
    LoseGold,
    LoseGoldAfterDeckChoices,
    AddCard,
    AddCardAfterDeckChoices,
    GainRandomRareCard,
    OfferThreeRareCards,
    OfferThreeCardsAndPotion,
    OfferFiveClassCards,
    OfferThreeColorlessCards,
    OfferCardBundles,
    OfferRandomRelic,
    OfferNeowsBonesRelics,
    GainPotionSlots,
    TransformFirstCardOfId,
    GainRelic,
    GainPotion,
    FillPotionSlots,
    LoseAllGold,
    GainRandomRelic,
    GainRandomPotion,
    GainRandomUncommonPotion,
    UpgradeRandomCard,
    UpgradeAllCards,
    UpgradeLastCardOfId,
    OfferWaxRelics,
    OfferTwoColorlessCards
}

public enum PrototypeRunEventKind
{
    RelicAcquired,
    ShopEntered,
    CardAdded,
    RoomCompleted,
    RestSiteHealed
}

public sealed record PrototypeCompletedRoomRecord(
    int Act,
    int Floor,
    string NodeId,
    PrototypeRoomType RoomType);

public sealed record PrototypeRouteConditionSpec(
    PrototypeRoomType? RoomType = null,
    int MinimumCompletedVisits = 0,
    int MinimumConsecutiveCompleted = 0,
    int EveryNthMatchingVisit = 0,
    bool CurrentActOnly = false,
    int MinAct = 1,
    int MaxAct = int.MaxValue,
    int MinFloor = 0,
    int MaxFloor = int.MaxValue);

public sealed record PrototypeCardSelectionSpec(
    PrototypeCardZone SourceZone,
    int MinSelections,
    int MaxSelections,
    PrototypeCardSelectionResolutionKind Resolution,
    PrototypeCardType? RequiredCardType = null,
    int SelectionsPerPowerStack = 0,
    bool RemoveUnselectedFromSource = false,
    bool RequireEnergyCostingCard = false,
    bool SequentialOptional = false,
    bool DrawEqualToSelectionsOnCompletion = false,
    int MaxSelectionsUpgradeDelta = 0,
    // Offer a random subset of the matching source pile without moving
    // unchosen cards. Native Seeker Strike offers up to three options.
    int RandomCandidateCount = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool RequireAttackOrPower = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int CopiesPerSelection = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int CopiesPerSelectionUpgradeDelta = 0);

public sealed record PrototypeSelectedCardPowerSpec(
    string PowerId,
    int Amount,
    int UpgradeDelta = 0,
    bool ClearAfflictionFromPayload = false,
    bool RestoreSuppressedUpgradesInPayload = false)
{
    public int AmountAt(int upgradeLevel) =>
        Amount + (UpgradeDelta * upgradeLevel);
}

public sealed record PrototypeSelectedCardPowerAction(
    string PowerId,
    int Amount,
    bool ClearAfflictionFromPayload = false,
    bool RestoreSuppressedUpgradesInPayload = false);

public sealed record PrototypeCardKeywordOverrideSpec(
    PrototypeCardKeyword Keyword,
    bool Enabled = true,
    PrototypeCardKeywordOverrideExpiry Expiry =
        PrototypeCardKeywordOverrideExpiry.EndOfTurn);

public sealed record PrototypeCardCostSpec(
    PrototypeCardCostKind Kind,
    int Amount = 0,
    int UpgradeDelta = 0,
    PrototypeCombatCountKind? ReductionCountKind = null,
    int ReductionPerCount = 0,
    int MinimumAmount = 0)
{
    public static implicit operator PrototypeCardCostSpec(int fixedCost) =>
        new(PrototypeCardCostKind.Fixed, fixedCost);

    public int AmountAt(
        int upgradeLevel,
        int reductionCount = 0)
    {
        var baseAmount = Amount + (UpgradeDelta * upgradeLevel);
        if (Kind != PrototypeCardCostKind.Fixed
            || ReductionCountKind is null
            || ReductionPerCount <= 0)
        {
            return baseAmount;
        }

        return Math.Max(
            MinimumAmount,
            baseAmount - (ReductionPerCount * Math.Max(0, reductionCount)));
    }

    public bool IsPlayable(
        int availableEnergy,
        int upgradeLevel = 0,
        int reductionCount = 0) =>
        Kind switch
        {
            PrototypeCardCostKind.Fixed =>
                AmountAt(upgradeLevel, reductionCount) <= availableEnergy,
            PrototypeCardCostKind.X => true,
            _ => throw new ArgumentOutOfRangeException()
        };

    public int ResolveEnergySpent(
        int availableEnergy,
        int upgradeLevel = 0,
        int reductionCount = 0) =>
        Kind switch
        {
            PrototypeCardCostKind.Fixed =>
                AmountAt(upgradeLevel, reductionCount),
            PrototypeCardCostKind.X => availableEnergy,
            _ => throw new ArgumentOutOfRangeException()
        };
}

public sealed record PrototypeCombatEffectSpec(
    PrototypeCombatEffectKind Kind,
    int Amount,
    int UpgradeDelta = 0,
    string? StatusId = null,
    PrototypeCardSelectionSpec? Selection = null,
    string? CardId = null,
    PrototypeEffectTarget Target = PrototypeEffectTarget.ActionTargetEnemy,
    int Repetitions = 1,
    int RepetitionUpgradeDelta = 0,
    int AmountPerEnergySpent = 0,
    int RepetitionsPerEnergySpent = 0,
    string? PowerId = null,
    int AmountPerPowerStack = 0,
    int RepetitionsPerPowerStack = 0,
    int GeneratedCardUpgradeLevel = 0,
    int GeneratedCardUpgradePerSourceUpgrade = 0,
    PrototypeCombatPredicateSpec? Condition = null,
    PrototypeCombatCountKind? CountKind = null,
    int AmountPerCount = 0,
    int RepetitionsPerCount = 0,
    int AmountPerCountUpgradeDelta = 0,
    PrototypeSelectedCardPowerSpec? SelectedCardPower = null,
    PrototypeCardKeywordOverrideSpec? SelectedCardKeyword = null,
    PrototypeCardKeywordOverrideSpec? DrawnCardKeyword = null,
    PrototypeCardKeywordOverrideSpec? EventSourceCardKeyword = null,
    PrototypeCardEnchantment? GeneratedCardEnchantment = null,
    PrototypeCardZone? AutoPlaySourceZone = null,
    string? RequiredCardTag = null,
    bool UpgradeAutoPlayedCardsOnSourceUpgrade = false,
    int ExtraCardRewardsOnFatal = 0,
    string? PlayerPowerOnFatalId = null,
    int PlayerPowerOnFatalAmount = 0,
    PrototypeCardType? GeneratedChoiceCardType = null,
    bool GeneratedChoiceCardsFreeThisTurn = true,
    bool GeneratedChoiceCardsUpgraded = false,
    bool GeneratedChoiceCardsUpgradeWithSource = false,
    bool GeneratedChoiceMustPick = false,
    bool GeneratedChoiceCharacterCards = false,
    bool GeneratedChoiceCardsRetain = false,
    bool GeneratedChoiceUsesNativeCardGenerationRng = false,
    PrototypeTemporaryCardCost? SelectedCardTemporaryCost = null,
    int GoldOnFatal = 0,
    int GoldOnFatalUpgradeDelta = 0,
    int PowerStoredValue = 0,
    int PowerStoredValueUpgradeDelta = 0,
    bool UseSourcePowerStoredValue = false,
    bool GainBlockEqualToAttackDamage = false,
    bool SplashUnpoweredAttackToOtherEnemies = false,
    PrototypeCardType? AutoPlayRequiredCardType = null,
    bool AutoPlayFallbackToUnplayable = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    int PlayerGoldCost = 0,
    bool GeneratedCharacterCardsFreeThisTurn = false)
{
    public int AmountAt(int upgradeLevel, int energySpent) =>
        Amount
        + (UpgradeDelta * upgradeLevel)
        + (AmountPerEnergySpent * energySpent);

    public int RepetitionsAt(int upgradeLevel, int energySpent) =>
        Repetitions
        + (RepetitionUpgradeDelta * upgradeLevel)
        + (RepetitionsPerEnergySpent * energySpent);
}

public sealed record PrototypeQueuedOperation(
    PrototypeCombatEffectKind Kind,
    int Amount,
    int? TargetEnemyId = null,
    string? StatusId = null,
    PrototypeCardSelectionSpec? Selection = null,
    string? CardId = null,
    string? PowerId = null,
    int GeneratedCardUpgradeLevel = 0,
    PrototypeEffectTarget TargetMode = PrototypeEffectTarget.ActionTargetEnemy,
    PrototypeEffectSourceKind SourceKind = PrototypeEffectSourceKind.System,
    bool IsPoweredAttack = false,
    PrototypeCombatPredicateSpec? Condition = null,
    PrototypeSelectedCardPowerAction? SelectedCardPower = null,
    PrototypeCardKeywordOverrideSpec? SelectedCardKeyword = null,
    PrototypeCardKeywordOverrideSpec? DrawnCardKeyword = null,
    PrototypeCardKeywordOverrideSpec? EventSourceCardKeyword = null,
    PrototypeCombatCardSnapshot? PowerCardPayload = null,
    PrototypeCardEnchantment? GeneratedCardEnchantment = null,
    PrototypeCardZone? AutoPlaySourceZone = null,
    string? RequiredCardTag = null,
    bool UpgradeAutoPlayedCardsBeforePlay = false,
    int ExtraCardRewardsOnFatal = 0,
    string? PlayerPowerOnFatalId = null,
    int PlayerPowerOnFatalAmount = 0,
    PrototypeCardType? GeneratedChoiceCardType = null,
    bool GeneratedChoiceCardsFreeThisTurn = true,
    bool GeneratedChoiceCardsUpgraded = false,
    bool GeneratedChoiceMustPick = false,
    bool GeneratedChoiceCharacterCards = false,
    bool GeneratedChoiceCardsRetain = false,
    bool GeneratedChoiceUsesNativeCardGenerationRng = false,
    PrototypeTemporaryCardCost? SelectedCardTemporaryCost = null,
    int GoldOnFatal = 0,
    int PowerStoredValue = 0,
    long? CardInstanceId = null,
    bool GainBlockEqualToAttackDamage = false,
    bool SplashUnpoweredAttackToOtherEnemies = false,
    PrototypeCardType? AutoPlayRequiredCardType = null,
    bool AutoPlayFallbackToUnplayable = false,
    long? SourcePowerApplicationOrder = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    int PlayerGoldCost = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool FromHandDraw = false,
    bool GeneratedCharacterCardsFreeThisTurn = false,
    int? EnergySpent = null,
    long[]? DeferredDrawnCardInstanceIds = null,
    int? DrawEventDepth = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool StandaloneSemanticDrawSegment = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool SemanticStratagemDrawSelection = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool AutomaticSemanticStratagemDrawSelection = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    int? DrawEqualSelectionCount = null)
{
    private long[]? _deferredDrawnCardInstanceIds = Copy(
        DeferredDrawnCardInstanceIds);

    public long[]? DeferredDrawnCardInstanceIds
    {
        get => Copy(_deferredDrawnCardInstanceIds);
        init => _deferredDrawnCardInstanceIds = Copy(value);
    }

    public PrototypeQueuedOperation Fork() => this with
    {
        DeferredDrawnCardInstanceIds = _deferredDrawnCardInstanceIds
    };

    private static long[]? Copy(long[]? values) => values is null
        ? null : (long[])values.Clone();
}

public sealed record PrototypeRunEffectSpec(
    PrototypeRunEffectKind Kind,
    int Amount = 0,
    string? CardId = null,
    string? RelicId = null,
    string? PotionId = null);

public sealed record PrototypeCardDefinition(
    string Id,
    string Name,
    PrototypeCardCostSpec Cost,
    PrototypeCardTarget Target,
    PrototypeCombatEffectSpec[] Effects,
    bool ExhaustOnUse = false,
    bool LoseExhaustOnUpgrade = false,
    PrototypeCardRarity Rarity = PrototypeCardRarity.Common,
    bool Innate = false,
    bool InnateOnUpgrade = false,
    bool Retain = false,
    bool Sly = false,
    bool Ethereal = false,
    bool Unplayable = false,
    bool Eternal = false,
    bool RewardEligible = true,
    PrototypeCardType Type = PrototypeCardType.Unknown,
    PrototypeCombatPredicateSpec? PlayCondition = null,
    string[]? Tags = null,
    int EndTurnDamageIfInHand = 0,
    bool EndTurnDamageUnblockable = false,
    int EndTurnGoldLossIfInHand = 0,
    bool MechanicsImplemented = true,
    bool MultiplayerOnly = false,
    bool RetainOnUpgrade = false,
    bool LoseEtherealOnUpgrade = false,
    bool CanBeGeneratedInCombat = true,
    int MaxUpgradeLevel = 1,
    bool PreventsOtherManualCardPlaysWhileInHand = false);

public sealed record PrototypePotionDefinition(
    string Id,
    string Name,
    PrototypeCardTarget Target,
    PrototypeCombatEffectSpec[] Effects,
    bool UsableOutsideCombat = false,
    PrototypeRunEffectSpec[]? RunEffects = null,
    bool AutomaticUsage = false,
    int DeathPreventionHealPercent = 0,
    bool GrantsExtraTurn = false);

public sealed record PrototypeRelicTriggerSpec(
    PrototypeCombatEventKind EventKind,
    PrototypeCombatEffectSpec[] Effects,
    int EveryNth = 1,
    int? MaxPlayerHpPercent = null,
    int? MinPlayerHpPercent = null,
    int? TurnEquals = null,
    int? MinTurn = null,
    bool RequiresZeroPlayerBlock = false,
    PrototypeCardType? RequiredSourceCardType = null,
    bool ResetCounterEachTurn = false,
    int? MaxTriggersPerCounterWindow = null,
    int? MaxAttacksPlayedLastTurn = null,
    bool RequiresEmptyHand = false,
    string? RequiredSourceCardTag = null,
    bool RequiresPlayerSide = false);

public sealed record PrototypeRelicRunTriggerSpec(
    PrototypeRunEventKind EventKind,
    PrototypeRunEffectSpec[] Effects,
    PrototypeCardType? RequiredCardType = null,
    PrototypeRouteConditionSpec? RouteCondition = null);

public enum PrototypePersistentDeckChoiceKind
{
    Remove,
    Upgrade,
    Transform,
    Enchant
}

public sealed record PrototypeRelicDeckChoiceSpec(
    PrototypePersistentDeckChoiceKind Kind,
    int Selections,
    bool UpgradeTransformedCards = false,
    PrototypeCardEnchantmentKind? EnchantmentKind = null,
    int EnchantmentAmount = 1,
    bool RequiresUpgradable = false,
    PrototypeCardType? RequiredCardType = null,
    string? TransformToCardId = null,
    bool PreserveUpgradeOnTransformation = false,
    bool PreserveEnchantmentOnTransformation = false,
    int? MinimumSelections = null);

public sealed record PrototypeRelicDefinition(
    string Id,
    string Name,
    int FirstTurnDrawBonus = 0,
    int EnergyPerTurnBonus = 0,
    PrototypeRelicTriggerSpec[]? Triggers = null,
    PrototypeRelicRunTriggerSpec[]? RunTriggers = null,
    PrototypeCardType? UpgradeAddedCardType = null,
    bool PreserveUnusedEnergy = false,
    int RewardCardChoiceBonus = 0,
    int ExtraNormalCombatCardRewardGroups = 0,
    int ShopPriceNumerator = 1,
    int ShopPriceDenominator = 1,
    int? ShopRemovalPriceOverride = null,
    bool PreventPotionAcquisition = false,
    int ExtraPotionSlots = 0,
    bool PreventRestHealing = false,
    bool PreventRestUpgrade = false,
    int MaxCardsPlayablePerTurn = 0,
    bool HideEnemyIntents = false,
    PrototypeRelicDeckChoiceSpec? AcquisitionDeckChoice = null,
    int AcquisitionHpLossAfterDeckChoices = 0,
    bool PreventsHandDiscard = false,
    int XValueBonus = 0,
    int HandDrawBonus = 0,
    int HandDrawBonusThroughTurn = 0,
    int RandomizeDrawnCardCostMaxExclusive = 0,
    bool ForceCombatPotionReward = false,
    int EnchantNewBlockCardsNimble = 0,
    // General run/combat modifier hooks, including event-only relics.
    int GoldOnRoomEntryUntilPurchase = 0,
    int RestHealCardRewardCount = 0,
    int MinPoweredAttackHpLoss = 0,
    int EnemyVulnerableOnBlockBroken = 0,
    string? AfterShuffleGeneratedCardId = null,
    int EnergyBonusMinTurn = 1,
    bool AddsCombatPet = false,
    bool AddsPet = false,
    int PowerCardEnergyCostIncrease = 0,
    string? PersistentTriggerCountProperty = null,
    int AdditionalCardPlayCountOncePerCombat = 0,
    int? FreeCardAtManualCardPlayCount = null,
    bool ProcureRandomPotionsBeforeCombat = false,
    string? CardIdAfterAcquisitionDeckChoices = null,
    int ExtraEliteRelicRewardCount = 0,
    bool PreventGoldGain = false,
    string? AppliedEnemyStatusAmountBonusId = null,
    int AppliedEnemyStatusAmountBonus = 0,
    int WeakAttackDamageReductionPercent = 0);

public sealed record PrototypePowerTriggerSpec(
    PrototypeCombatEventKind EventKind,
    PrototypeCombatEffectSpec[] Effects,
    bool RequiresOwnerTarget = false,
    bool ExcludeHandDraw = false,
    bool RequiresPlayerTurn = false,
    bool RemoveSourcePowerAfterTrigger = false,
    PrototypeCardType? RequiredSourceCardType = null,
    int EveryNth = 1,
    bool CountdownBeforeTrigger = false);

public sealed record PrototypePowerDefinition(
    string Id,
    string Name,
    int BlockBonusPerStack,
    PrototypePowerTriggerSpec[] Triggers,
    int AttackRetaliationPerStack = 0,
    bool AllowNegative = false,
    bool RemoveAtPlayerTurnEnd = false,
    int BlockAfterClearPerStack = 0,
    int PlayerBlockAtTurnEndPerStack = 0,
    int EnergyAfterResetPerStack = 0,
    int HandDrawBonusPerStack = 0,
    int DiscardAfterPlayerTurnStartPerStack = 0,
    bool RemoveAfterBlockClear = false,
    bool RemoveAfterEnergyReset = false,
    bool RemoveAfterHandDraw = false,
    PrototypeCardType? FreeCardType = null,
    bool ConsumeOnMatchingCardPlay = false,
    bool PreventsAdditionalDraw = false,
    bool PreventsPlayerBlockClear = false,
    bool DecrementAfterPlayerTurnStart = false,
    string? AttackDamageBonusRequiredCardTag = null,
    int AttackDamageBonusPerStack = 0,
    int PlayerAttackDamageBonusPerStack = 0,
    string? AttackDamageBonusRequiredTargetStatus = null,
    int AttackDamageBonusTargetNumeratorPerStack = 0,
    int AttackDamageBonusTargetDenominator = 1,
    PrototypeCardKeyword? GrantedCardKeyword = null,
    string? GrantedCardKeywordRequiredCardTag = null,
    string? FirstAttackDamageBonusRequiredCardTag = null,
    int FirstAttackDamageBonusPerStack = 0,
    PrototypeCardType? ReplayCardType = null,
    bool ReplayAnyCardType = false,
    int AdditionalPlayCount = 0,
    bool ConsumeOnMatchingPlayCountModification = false,
    bool IsInstanced = false,
    bool RequiresCardPayload = false,
    bool SourceBoundToEnemy = false,
    bool DoesNotStack = false,
    bool IsDebuff = false,
    bool BlocksNextDebuff = false,
    PrototypeCardAfflictionKind? SourceBoundCardAffliction = null,
    PrototypeCardAfflictionKind? AppliedCardAffliction = null,
    PrototypeCardType? AppliedCardAfflictionRequiredCardType = null,
    int AfflictedCardEnergyCostPerStack = 0,
    bool SkipCardsWithExistingAffliction = false,
    bool ClearSourceAfflictionWhenRemoved = false,
    bool ClearAppliedCardAfflictionWhenRemoved = false,
    bool DowngradeExistingCardsOnApply = false,
    bool RestoreDowngradedCardsWhenLastSourceRemoved = false,
    bool DecrementAtPlayerTurnEnd = false,
    bool DecrementAfterHandCleanup = false,
    int PlayerAttackDamageNumerator = 1,
    int PlayerAttackDamageDenominator = 1,
    int PlayerIncomingAttackDamageNumerator = 1,
    int PlayerIncomingAttackDamageDenominator = 1,
    int PlayerCardBlockNumerator = 1,
    int PlayerCardBlockDenominator = 1,
    int PlayerBlockGainNumeratorPerStack = 1,
    int PlayerBlockGainDenominatorPerStack = 1,
    int PlayerIncomingDamageCap = 0,
    bool DecrementAtEnemyTurnEnd = false,
    int EnemyHpLossCapPerTrigger = 0,
    bool ConsumeOnEnemyHpLoss = false,
    int MaxCardsPlayablePerTurn = 0,
    bool TriggerOwnerAtHpAtOrBelowStacks = false,
    bool ClearOwnerStrengthOnHpThresholdTrigger = false,
    string? OwnerAiStateOnHpThresholdTrigger = null,
    int EnemyAttackDamageBonusPerStack = 0,
    int EnemyStrengthGainAtTurnEndPerStack = 0,
    int EnemyIncomingAttackDamagePercentPerCardPlayed = 0,
    bool RemoveAtEnemyTurnEnd = false,
    bool NegativeApplicationIsDebuff = false,
    string? ExtraEnemyStatusTriggerStatusId = null,
    int ExtraEnemyStatusTriggersPerStack = 0,
    bool PreventsHandDiscard = false,
    string? AllEnemyTargetCardTag = null,
    bool OwnerDeathTriggersFatal = true,
    int AllyDeathStrengthPerStack = 0,
    int AllyDeathBlockPerStack = 0,
    bool ConsumeOnAllyDeath = false,
    bool StunOnAllyDeath = false,
    int EnemyStrengthAtSideTurnEndPerStack = 0,
    bool SkipInitialEnemySideTurnEnd = false,
    int StrengthPerUnblockedAttackHitPerStack = 0,
    int EnemyStartingBlockPerStack = 0,
    int EnemyBlockAtSideTurnEndPerStack = 0,
    int EnemyStacksDecayAtSideTurnStartAfterFirst = 0,
    int EnemyStacksDecayAtSideTurnEnd = 0,
    PrototypeCardAfflictionKind? SkillPlayAffliction = null,
    bool BlockPlayOfMatchingAffliction = false,
    bool ClearMatchingAfflictionAtPlayerTurnEnd = false,
    bool EnemyHpLossLimitedPerSideTurnByStacks = false,
    int EnemyBlockAfterFirstUnblockedCardAttackPerStack = 0,
    bool ConsumeAfterEnemyAttack = false,
    bool WakeOwnerOnUnblockedAttackDamage = false,
    string? OwnerAiStateOnWake = null,
    string[]? RemoveOwnerPowersOnWake = null,
    bool StunOwnerOnWake = false,
    string? OwnerAiStateOnPowerExpiry = null,
    string[]? RemoveOwnerPowersOnPowerExpiry = null,
    string? LastStandAiStateId = null,
    int LastStandHp = 0,
    bool ResetTriggerCountersAtPlayerTurnEnd = false,
    // Native NoBlockPower applies only to card-sourced Block, not
    // unpowered effects from relics or other powers.
    bool PreventsCardBlock = false,
    string? BlockBonusRequiredCardTag = null,
    bool PlayedAttacksAndSkillsReturnToDraw = false,
    bool LethalAfterUnblockedPoweredAttack = false,
    // Reversible enemy strength effects; changes are applied after
    // debuff prevention, then restored at enemy side-turn end.
    int EnemyStrengthOnApplyPerStack = 0,
    int EnemyStrengthRestoreAtSideTurnEndPerStack = 0,
    bool ReturnNextDiscardedCardToDraw = false,
    bool SnapshotGenerationStacksAtTurnStart = false,
    // Tunneler's BurrowedPower vetoes ordinary block clearing at
    // the enemy-side turn boundary until its Block is broken.
    bool PreventsEnemyBlockClear = false,
    // Tender: temporary Strength/Dexterity penalties for each completed card.
    int PlayerStrengthDexterityLossPerCardPlayedPerStack = 0,
    // Curl Up: grant Block only after the triggering attack card finishes.
    int EnemyBlockAfterAttackingCardPlayedPerStack = 0,
    // Scroll of Biting Paper Cuts: each unblocked powered hit
    // reduces the victim's persistent maximum HP.
    int PlayerMaxHpLossOnUnblockedAttackHitPerStack = 0,
    // Living Shield/Rampart: independently owned ally block grants
    // at the start of the player's side-turn.
    int AllyBlockAtPlayerTurnStartPerStack = 0,
    string? AllyBlockTargetEnemyId = null,
    // Soar: powered incoming attacks are multiplied by this fraction.
    int EnemyIncomingPoweredAttackNumerator = 1,
    int EnemyIncomingPoweredAttackDenominator = 1,
    // Galvanic: Power cards carry this affliction's damage amount.
    int GalvanizePowerCardsPerStack = 0,
    int PlayerStrengthAtTurnEndPerStack = 0,
    int EnergyBonusWhileActive = 0,
    bool DecrementAfterEnergyReset = false,
    int EnemyOutgoingPoweredAttackNumerator = 1,
    int EnemyOutgoingPoweredAttackDenominator = 1,
    int PlayerAttackDamageMultiplierWhileActiveNumerator = 1,
    int PlayerAttackDamageMultiplierWhileActiveDenominator = 1,
    bool RemoveAfterFirstPoweredAttack = false,
    int HandDrawBonusWhileActive = 0,
    bool DecrementAfterHandDraw = false,
    int EnemyHpLossAtSideTurnEndPerStack = 0,
    bool PreventsNextHpLoss = false,
    bool GeneratedCharacterCardsFreeThisTurn = false,
    int EnemyStacksDecayOnUnblockedDamage = 0);

public sealed record PrototypePowerInstanceState(
    string PowerId,
    int Stacks,
    long ApplicationOrder,
    PrototypeCombatCardSnapshot? CardPayload = null,
    int? SourceEnemyInstanceId = null,
    bool SkipNextEnemySideTurnEnd = false,
    int StoredValue = 0,
    int[]? TriggerCounts = null,
    long? PendingAttackingCardInstanceId = null,
    // A curse applied at hand cleanup must survive its first
    // immediately following player-turn-end duration tick.
    bool SkipNextPlayerTurnEnd = false)
{
    public PrototypePowerInstanceState Fork() => this with
    {
        CardPayload = CardPayload?.Fork(),
        TriggerCounts = TriggerCounts is null
            ? null
            : (int[])TriggerCounts.Clone()
    };
}

public enum PrototypeEnemyEffectKind
{
    DamagePlayer,
    GainBlock,
    ApplyPlayerPower,
    ApplyEnemyPower,
    AddCardsToDiscard,
    AddCardsToHand,
    AddCardsToRandomDraw,
    SummonEnemy,
    KillSelf,
    StealPlayerGold,
    EscapeEnemy,
    HealSelf,
    StoreEnemyPowerAsDamage,
    StealPlayerCard,
    // Native Ovicopter egg hatching preserves the combat instance.
    TransformEnemy,
    // The Obscura's Wail buffs every living teammate.
    ApplyAllEnemyPower,
    // Insatiable's Frantic Escapes enter draw/discard at random indices.
    AddCardsToRandomDiscard,
    // Knowledge Demon must ask the player to choose between curses.
    RequireNativeChoice,
    RemoveEnemyPower,
    // Lost/Forgotten: apply -stats to the player, grant +stats
    // to the owner, and track the successful stolen amount.
    StealPlayerPower,
    // Guardbot: grant fixed Block to every living ally of an enemy type.
    GrantBlockToEnemyType,
    GrantStrengthToOtherLivingEnemies,
    ReviveTestSubject,
    IntensifyWither
}

public sealed record PrototypeAscensionDelta(
    int MinAscension,
    int Delta);

public sealed record PrototypeEnemyEffectSpec(
    PrototypeEnemyEffectKind Kind,
    int Amount,
    int AmountPerAct = 0,
    int Repetitions = 1,
    string? PowerId = null,
    PrototypeAscensionDelta[]? AscensionDeltas = null,
    bool IsAttack = true,
    PrototypeAscensionDelta[]? RepetitionAscensionDeltas = null,
    string? CardId = null,
    string? EnemyId = null,
    string[]? SummonSlotNames = null,
    int ExtraAmountPerPriorMoveUse = 0,
    bool UseStoredEnemyDamage = false,
    // Native egg slots remain occupied even by dead eggs.
    bool ReserveSummonSlotAfterDeath = false,
    // Source-backed conditional buffs, read before the action executes.
    string? OwnerPowerStackConditionId = null,
    int? OwnerPowerStacksLessThan = null,
    int? OwnerPowerStacksAtLeast = null,
    // The Forgotten's Dread adds the owner's current Dexterity to
    // its printed damage, before ordinary attack modifiers.
    string? ExtraAmountFromOwnerPowerId = null,
    // Fabricator pools: the next bot must differ from the last
    // monster summoned by the same parent, living or dead.
    string[]? SummonEnemyPool = null,
    bool SelectFirstAvailableSummonSlot = false,
    int[]? SummonSlotFormationPositions = null)
{
    public int AmountAt(
        int act,
        int ascension) =>
        Amount
        + ((act - 1) * AmountPerAct)
        + (AscensionDeltas
            ?? Array.Empty<PrototypeAscensionDelta>())
            .Where(delta =>
                ascension >= delta.MinAscension)
            .Sum(delta => delta.Delta);

    public int RepetitionsAt(int ascension) =>
        Repetitions
        + (RepetitionAscensionDeltas
            ?? Array.Empty<PrototypeAscensionDelta>())
            .Where(delta =>
                ascension >= delta.MinAscension)
            .Sum(delta => delta.Delta);
}

public enum PrototypeEnemyMovePolicy
{
    SequentialLoop,
    UniformRandomAfterOpener,
    StateMachine
}

public enum PrototypeEnemyAiStateKind
{
    Move,
    Random,
    Conditional
}

public enum PrototypeEnemyAiConditionKind
{
    IsAlone,
    IsFront,
    IsNotFront,
    SlotNameEquals,
    IsOffBalance,
    IsNotOffBalance,
    HasEnemyPower,
    LacksEnemyPower,
    LivingEnemiesAtMost,
    LivingEnemiesGreaterThan,
    MoveUsedFewerThan,
    MoveUsedAtLeast,
    HpAtLeastHalf,
    HpBelowHalf,
    HasLivingEnemyId,
    LacksLivingEnemyId
}

public enum PrototypeEnemyAiRepeatRule
{
    CanRepeatForever,
    CannotRepeat,
    CanRepeatXTimes,
    UseOnlyOnce
}

public sealed record PrototypeEnemyAiBranch(
    string TargetStateId,
    int Weight = 1,
    PrototypeEnemyAiRepeatRule RepeatRule =
        PrototypeEnemyAiRepeatRule.CanRepeatForever,
    int MaxTimes = 0,
    bool RequiresAvailableSummon = false);

public sealed record PrototypeEnemyAiConditionalBranch(
    string TargetStateId,
    PrototypeEnemyAiConditionKind Condition,
    string? Value = null);

public sealed record PrototypeEnemyAiStateDefinition(
    string Id,
    PrototypeEnemyAiStateKind Kind,
    int? MoveIndex = null,
    string? NextStateId = null,
    PrototypeEnemyAiBranch[]? Branches = null,
    PrototypeEnemyAiConditionalBranch[]? ConditionalBranches = null);

public sealed record PrototypeEnemyAiDefinition(
    string InitialStateId,
    PrototypeEnemyAiStateDefinition[] States);

public sealed record PrototypeEnemyMoveDefinition(
    string Id,
    PrototypeEnemyEffectSpec[] Effects,
    int MaxConsecutiveUses = 0);

public sealed record PrototypeStartingPowerSpec(
    string PowerId,
    int Stacks,
    PrototypeAscensionDelta[]? AscensionDeltas = null)
{
    public int StacksAt(int ascension) =>
        Stacks
        + (AscensionDeltas
            ?? Array.Empty<PrototypeAscensionDelta>())
            .Where(delta =>
                ascension >= delta.MinAscension)
            .Sum(delta => delta.Delta);
}

public sealed record PrototypeEnemyDeathSummonSpec(
    string EnemyId,
    int FormationPosition,
    string? SlotName = null,
    bool TransferStolenGold = false,
    int SkipEnemyActions = 1);

public sealed record PrototypeEnemyDefinition(
    string Id,
    string Name,
    int MaxHp,
    int HpPerAct,
    PrototypeEnemyMoveDefinition[] Moves,
    PrototypeStartingPowerSpec[]? StartingPowers = null,
    int MoveLoopStartIndex = 0,
    PrototypeEnemyMovePolicy MovePolicy =
        PrototypeEnemyMovePolicy.SequentialLoop,
    int OpeningMoveIndex = 0,
    PrototypeAscensionDelta[]? HpAscensionDeltas = null,
    int[]? OpeningMoveIndices = null,
    int RandomMovePoolStartIndex = 0,
    int? MinHp = null,
    PrototypeAscensionDelta[]? MinHpAscensionDeltas = null,
    PrototypeEnemyAiDefinition? Ai = null,
    bool IsMinion = false,
    bool RevivesOnEnemyTurn = false,
    PrototypeEnemyDeathSummonSpec[]? DeathSummons = null,
    int NonSummonMovesBeforeEligible = 0,
    int MaxCoordinatedSummons = 0,
    bool RecoverCarriedGoldOnDeath = false,
    float EscapedRewardProportionWithGold = 1f,
    float EscapedRewardProportionWithoutGold = 1f)
{
    public (int Min, int Max) HpRangeAt(
        int act,
        int ascension)
    {
        var max = MaxHp
            + ((act - 1) * HpPerAct)
            + (HpAscensionDeltas
                ?? Array.Empty<PrototypeAscensionDelta>())
                .Where(delta =>
                    ascension >= delta.MinAscension)
                .Sum(delta => delta.Delta);

        var minDeltas =
            MinHpAscensionDeltas
            ?? HpAscensionDeltas
            ?? Array.Empty<PrototypeAscensionDelta>();
        var min = (MinHp ?? MaxHp)
            + ((act - 1) * HpPerAct)
            + minDeltas
                .Where(delta =>
                    ascension >= delta.MinAscension)
                .Sum(delta => delta.Delta);

        if (min > max)
        {
            throw new InvalidOperationException(
                $"Enemy '{Id}' has invalid HP range {min}-{max}.");
        }

        return (min, max);
    }

    public int HpAt(
        int act,
        int ascension) =>
        HpRangeAt(act, ascension).Max;
}

public enum PrototypeEncounterFormationPolicy
{
    Fixed,
    ChooseDistinct
}

public sealed record PrototypeEncounterEnemySpec(
    string EnemyId,
    int FormationPosition,
    string? SlotName = null,
    int? LeaderFormationPosition = null);

public sealed record PrototypeEncounterSelectionGroup(
    string[] EnemyPool,
    int[] FormationPositions,
    bool ChooseDistinct = false,
    string[]? SlotNames = null);

public sealed record PrototypeEncounterFormationVariant(
    PrototypeEncounterEnemySpec[]? Formation = null,
    PrototypeEncounterSelectionGroup[]? SelectionGroups = null);

public sealed record PrototypeEncounterDefinition(
    string Id,
    PrototypeRoomType RoomType,
    string[] EnemyIds,
    int MinAct = 1,
    int MaxAct = int.MaxValue,
    int MinFloor = 1,
    int MaxFloor = int.MaxValue,
    int Weight = 1,
    PrototypeEncounterEnemySpec[]? Formation = null,
    PrototypeEncounterFormationPolicy FormationPolicy =
        PrototypeEncounterFormationPolicy.Fixed,
    string[]? EnemyPool = null,
    int EnemyCount = 0,
    PrototypeEncounterSelectionGroup[]? SelectionGroups = null,
    PrototypeEncounterFormationVariant[]? FormationVariants = null,
    string[]? CyclicOpeningAiStateIds = null,
    bool UniqueEvenEnemyHpWithinEncounter = false,
    // Fixed openings after the once-rotated encounter-local prefix
    // (e.g. fourth Scroll of Biting in the normal formation).
    string[]? FixedTrailingOpeningAiStateIds = null)
{
    public PrototypeEncounterEnemySpec[] FixedEnemySpecs =>
        Formation
        ?? (SelectionGroups is { Length: > 0 }
            || FormationVariants is { Length: > 0 }
            || FormationPolicy
                == PrototypeEncounterFormationPolicy.ChooseDistinct
            ? Array.Empty<PrototypeEncounterEnemySpec>()
            : EnemyIds
                .Select((enemyId, index) =>
                    new PrototypeEncounterEnemySpec(
                        enemyId,
                        index))
                .ToArray());

    /// <summary>
    /// Legacy compatibility adapter. Shared prototype-combat stream
    /// behavior and its exact draw order are unchanged.
    /// </summary>
    public PrototypeEncounterEnemySpec[] ResolveEnemySpecs(RngBundle rng) =>
        ResolveEnemySpecs(rng, "encounter:" + Id);

    /// <summary>Typed legacy adapter with stable encounter ownership.</summary>
    public PrototypeEncounterEnemySpec[] ResolveEnemySpecs(
        RngBundle rng,
        string ownerId)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        const string streamId = "combat";
        const string siteId = "combat.encounter.formation.index_draw";
        var provider = new LegacyPrototypeRandomIndexProviderV111();
        var ordinal = 0;
        return ResolveEnemySpecs(maxExclusive =>
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var choice = StochasticProviderScopeV111.ResolveIndex(
                provider, new RandomIndexRequestV111(
                    siteId,
                    streamId,
                    ownerId,
                    ordinal++,
                    Enumerable.Range(0, maxExclusive).ToArray()),
                rng);
            return choice.SelectedIndex;
        });
    }

    /// <summary>
    /// Per-encounter choice hook used by pinned native context RNGs.
    /// Callers must supply the native EncounterModel.Id.Entry and lifetime;
    /// this selection algorithm is still source-shaped, not a general
    /// proof of exact native GenerateMonsters() parity.
    /// </summary>
    public PrototypeEncounterEnemySpec[] ResolveEnemySpecs(
        Func<int, int> nextIndex)
    {
        ArgumentNullException.ThrowIfNull(nextIndex);
        PrototypeEncounterFormationVariant? variant = null;
        if (FormationVariants is { Length: > 0 } variants)
        {
            var variantIndex = variants.Length == 1
                ? 0
                : nextIndex(variants.Length);
            variant = variants[variantIndex];
        }

        var resolved = (variant?.Formation ?? FixedEnemySpecs)
            .ToList();

        if (FormationPolicy
                == PrototypeEncounterFormationPolicy.ChooseDistinct)
        {
            var pool = EnemyPool
                ?? throw new InvalidOperationException(
                    $"Encounter '{Id}' is missing its enemy pool.");
            if (EnemyCount <= 0 || EnemyCount > pool.Length)
            {
                throw new InvalidOperationException(
                    $"Encounter '{Id}' has invalid distinct enemy count {EnemyCount}.");
            }

            var candidates = pool.ToList();
            for (var position = 0;
                 position < EnemyCount;
                 position++)
            {
                var index = candidates.Count == 1
                    ? 0
                    : nextIndex(candidates.Count);
                resolved.Add(
                    new PrototypeEncounterEnemySpec(
                        candidates[index],
                        position));
                candidates.RemoveAt(index);
            }
        }

        foreach (var group in
                 variant?.SelectionGroups
                 ?? SelectionGroups
                 ?? Array.Empty<
                     PrototypeEncounterSelectionGroup>())
        {
            if (group.EnemyPool.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Encounter '{Id}' has an empty selection group.");
            }

            if (group.SlotNames is { } slotNames
                && slotNames.Length
                    != group.FormationPositions.Length)
            {
                throw new InvalidOperationException(
                    $"Encounter '{Id}' has mismatched slot-name and position counts.");
            }

            if (group.ChooseDistinct
                && group.FormationPositions.Length
                    > group.EnemyPool.Length)
            {
                throw new InvalidOperationException(
                    $"Encounter '{Id}' cannot choose " +
                    $"{group.FormationPositions.Length} distinct enemies " +
                    $"from a pool of {group.EnemyPool.Length}.");
            }

            var candidates = group.EnemyPool.ToList();
            for (var index = 0;
                 index < group.FormationPositions.Length;
                 index++)
            {
                var choiceIndex = candidates.Count == 1
                    ? 0
                    : nextIndex(candidates.Count);
                var selected = candidates[choiceIndex];
                resolved.Add(
                    new PrototypeEncounterEnemySpec(
                        selected,
                        group.FormationPositions[index],
                        group.SlotNames?[index]));

                if (group.ChooseDistinct)
                {
                    candidates.RemoveAt(choiceIndex);
                }
            }
        }

        var duplicatePosition = resolved
            .GroupBy(spec => spec.FormationPosition)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePosition is not null)
        {
            throw new InvalidOperationException(
                $"Encounter '{Id}' assigns multiple enemies to " +
                $"formation position {duplicatePosition.Key}.");
        }

        return resolved
            .OrderBy(spec => spec.FormationPosition)
            .ToArray();
    }
}

public sealed record PrototypeStatusDefinition(
    string Id,
    bool IsDebuff = false,
    PrototypeTurnStage? TriggerStage = null,
    PrototypeStatusTriggerKind TriggerKind = PrototypeStatusTriggerKind.None,
    int DecayOnTrigger = 0,
    PrototypeTurnStage? DecayStage = null,
    int DecayAtStage = 0,
    int OutgoingDamageNumerator = 1,
    int OutgoingDamageDenominator = 1,
    int IncomingAttackDamageNumerator = 1,
    int IncomingAttackDamageDenominator = 1);

public sealed record PrototypeEventDeckChoiceSpec(
    PrototypePersistentDeckChoiceKind Kind,
    int Selections,
    bool UpgradeTransformedCards = false,
    string? TransformToCardId = null,
    PrototypeCardEnchantmentKind? EnchantmentKind = null,
    bool BasicCardsOnly = false,
    string? RequiredCardTag = null,
    PrototypeCardType? RequiredCardType = null,
    int EnchantmentAmount = 1);

public sealed record PrototypeEventChoiceDefinition(
    string Id,
    string Label,
    PrototypeRunEffectSpec[] Effects,
    PrototypeEventDeckChoiceSpec? DeckChoice = null,
    PrototypeRouteConditionSpec? RouteCondition = null);

public sealed record PrototypeEventDefinition(
    string Id,
    string Name,
    PrototypeEventChoiceDefinition[] Choices,
    int MinAct = 1,
    int MaxAct = int.MaxValue,
    int Weight = 1,
    bool OncePerRun = true);

public sealed record PrototypeMapFloorRule(
    int MinFloor,
    int MaxFloor,
    PrototypeRoomType[] RoomPool,
    bool AllowDuplicateSpecialRooms = false,
    int MinNodes = 1,
    int MaxNodes = 1,
    PrototypeRoomType[]? RequiredRoomTypes = null,
    bool AvoidMatchingSpecialPredecessors = false)
{
    public PrototypeRoomType[] RequiredRooms =>
        RequiredRoomTypes
        ?? Array.Empty<PrototypeRoomType>();
}

public sealed record PrototypeRuleset(
    string Id,
    int Acts,
    int FloorsPerAct,
    int StartingHp,
    int StartingGold,
    int PotionSlots,
    int BaseEnergy,
    int HandSize,
    int RestHealPercent,
    int RestTrainMaxHp,
    PrototypeMapFloorRule[] MapFloorRules,
    PrototypeAutomaticStep[] EndTurnPipeline)
{
    public int MaxHandSize { get; init; } = 10;

    public bool HasHandCapacity(int currentHandSize) =>
        currentHandSize < MaxHandSize;

    public int RemainingHandCapacity(int currentHandSize) =>
        Math.Max(0, MaxHandSize - currentHandSize);
}

public sealed record MapNodeState
{
    private string[]? _nextNodeIds;

    [JsonConstructor]
    public MapNodeState(
        string NodeId,
        int Act,
        int Floor,
        PrototypeRoomType RoomType,
        string[]? NextNodeIds = null)
    {
        this.NodeId = NodeId;
        this.Act = Act;
        this.Floor = Floor;
        this.RoomType = RoomType;
        this.NextNodeIds = NextNodeIds;
    }

    public string NodeId { get; init; }
    public int Act { get; init; }
    public int Floor { get; init; }
    public PrototypeRoomType RoomType { get; init; }
    public string[]? NextNodeIds
    {
        get => _nextNodeIds is null
            ? null : (string[])_nextNodeIds.Clone();
        init => _nextNodeIds = Copy(value);
    }

    public MapNodeState Fork() => this with
    {
        _nextNodeIds = Copy(_nextNodeIds)
    };

    public void Deconstruct(
        out string NodeId,
        out int Act,
        out int Floor,
        out PrototypeRoomType RoomType,
        out string[]? NextNodeIds)
    {
        NodeId = this.NodeId;
        Act = this.Act;
        Floor = this.Floor;
        RoomType = this.RoomType;
        NextNodeIds = this.NextNodeIds;
    }

    private static string[]? Copy(string[]? values) =>
        values is null ? null : (string[])values.Clone();
}

public sealed record MapState
{
    private MapNodeState[]? _nodes;
    private string[]? _entryNodeIds;

    [JsonConstructor]
    public MapState(
        MapNodeState[] Nodes,
        string? CurrentNodeId = null,
        string[]? EntryNodeIds = null,
        string? GenerationProfileId = null)
    {
        this.Nodes = Nodes;
        this.CurrentNodeId = CurrentNodeId;
        this.EntryNodeIds = EntryNodeIds;
        this.GenerationProfileId = GenerationProfileId;
    }

    public MapNodeState[] Nodes
    {
        get => _nodes is null ? null! : (MapNodeState[])_nodes.Clone();
        init => _nodes = Copy(value);
    }
    public string? CurrentNodeId { get; init; }
    public string[]? EntryNodeIds
    {
        get => _entryNodeIds is null ? null : (string[])_entryNodeIds.Clone();
        init => _entryNodeIds = Copy(value);
    }
    public string? GenerationProfileId { get; init; }

    public MapState Fork() => this with
    {
        _nodes = _nodes!.Select(node => node.Fork()).ToArray(),
        _entryNodeIds = Copy(_entryNodeIds)
    };

    public void Deconstruct(
        out MapNodeState[] Nodes,
        out string? CurrentNodeId,
        out string[]? EntryNodeIds,
        out string? GenerationProfileId)
    {
        Nodes = this.Nodes;
        CurrentNodeId = this.CurrentNodeId;
        EntryNodeIds = this.EntryNodeIds;
        GenerationProfileId = this.GenerationProfileId;
    }

    public MapNodeState[] AvailableNodes()
    {
        IEnumerable<string> ids;
        if (CurrentNodeId is null)
        {
            ids = _entryNodeIds ?? Array.Empty<string>();
        }
        else
        {
            var nodes = _nodes ?? throw new InvalidOperationException(
                "Map has no node collection.");
            var current = nodes.FirstOrDefault(node =>
                StringComparer.Ordinal.Equals(node.NodeId, CurrentNodeId))
                ?? throw new InvalidOperationException($"Map current node '{CurrentNodeId}' is missing.");
            ids = current.NextNodeIds ?? Array.Empty<string>();
        }

        var byId = (_nodes ?? throw new InvalidOperationException(
            "Map has no node collection.")).ToDictionary(
                node => node.NodeId, StringComparer.Ordinal);
        return ids
            .Select(id => byId.TryGetValue(id, out var node)
                ? node
                : throw new InvalidOperationException($"Map edge targets missing node '{id}'."))
            .ToArray();
    }

    // Compatibility/readability alias for code that only needs the currently legal map choices.
    public MapNodeState[] Options => AvailableNodes();

    private static MapNodeState[]? Copy(MapNodeState[]? values) =>
        values is null ? null : (MapNodeState[])values.Clone();

    private static string[]? Copy(string[]? values) =>
        values is null ? null : (string[])values.Clone();
}

public sealed record EnemyCombatState(
    int InstanceId,
    string EnemyId,
    int Hp,
    int Block,
    int MoveIndex,
    Dictionary<string, int> Statuses,
    PrototypePowerInstanceState[]? Powers = null,
    string? LastMoveId = null,
    int ConsecutiveMoveUses = 0,
    string? AiStateId = null,
    Dictionary<string, int>? MoveUseCounts = null,
    int FormationPosition = 0,
    string? SlotName = null,
    int? LeaderEnemyInstanceId = null,
    bool SkipNextEnemyAction = false,
    int EnemyActionSkipsRemaining = 0,
    bool DeathEffectsResolved = false,
    int NonSummonMovesUntilEligible = 0,
    int SharedSummonsUsed = 0,
    bool SharedSummonUsedThisTurn = false,
    int StolenGold = 0,
    bool Escaped = false,
    int HpLossBudgetUsed = 0,
    bool GainedReactiveBlockThisTurn = false,
    bool LastStandTriggered = false,
    int StoredEnemyDamage = 0,
    // Committed at combat entry / the player-turn boundary, never on Observe.
    int? PlannedMoveIndex = null,
    string? PlannedNextAiStateId = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    int? StunFollowUpMoveIndex = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? StunFollowUpAiStateId = null,
    // BowlbugRock.Imbalanced tracks whether its prior attack was fully
    // blocked; survives intent commitment and deterministic snapshots.
    bool IsOffBalance = false,
    // Each Swipe carries the original persistent card, not merely its
    // type. Preserve its instance ID, upgrade and persistent state.
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    CardInstance[]? StolenCards = null,
    bool ReattachDeadMoveResolved = false,
    // The actual rolled maximum, carried across forks and native snapshots.
    // Older hand-built fixtures retain the default for compatibility.
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    int MaxHp = 0,
    int BossPhase = 0,
    bool BossPendingRevival = false,
    int ExtraMultiClawCount = 0,
    int WitherUpgradeCount = 0,
    bool NemesisIntangibleNext = false,
    // Native ToughEgg.Hatch changes the existing creature in place.
    // HatchlingId below is a move/observation proxy, not a new model.
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool NativeToughEggHatched = false)
{
    public EnemyCombatState Fork() => this with
    {
        Statuses = new Dictionary<string, int>(Statuses, StringComparer.Ordinal),
        Powers = Powers is null
            ? null
            : Powers.Select(power => power.Fork()).ToArray(),
        MoveUseCounts = MoveUseCounts is null
            ? null
            : new Dictionary<string, int>(
                MoveUseCounts,
                StringComparer.Ordinal),
        StolenCards = StolenCards is null
            ? null
            : StolenCards.Select(card => card with
            {
                PersistentState = card.PersistentState.Clone()
            }).ToArray()
    };

    public PrototypePowerInstanceState[] PowerStates =>
        Powers ?? Array.Empty<PrototypePowerInstanceState>();
}

public sealed record CombatCardInstance(
    long InstanceId,
    long? PersistentCardInstanceId,
    string CardId,
    int UpgradeLevel,
    bool IsTemporary,
    JsonElement State,
    int CombatEnergyCostDelta = 0,
    PrototypeTemporaryCardCost? TemporaryEnergyCost = null,
    PrototypeCardKeywordOverride[]? KeywordOverrides = null,
    int ReplayCount = 0,
    PrototypeCardEnchantment? Enchantment = null,
    bool EnchantmentTriggeredThisCombat = false,
    PrototypeCardAffliction? Affliction = null,
    int SuppressedUpgradeLevels = 0,
    int WitherFakeUpgradeLevel = 0,
    // CreateDupe copies (currently History Course). Duplicated attacks
    // are excluded from next-turn History Course history.
    bool IsHistoryCourseDupe = false)
{
    public CombatCardInstance Fork() => this with
    {
        State = State.Clone(),
        TemporaryEnergyCost = TemporaryEnergyCost is null
            ? null
            : TemporaryEnergyCost with { },
        KeywordOverrides = KeywordOverrides is null
            ? null
            : KeywordOverrides.Select(item => item with { }).ToArray()
    };
}

public sealed record CombatPotionState(
    int Slot,
    string PotionId,
    JsonElement State)
{
    public CombatPotionState Fork() => this with { State = State.Clone() };
}

public sealed record CombatRelicState(
    int PersistentIndex,
    string RelicId,
    long ApplicationOrder,
    int[] TriggerCounts,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool IsMelted = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool HadLeftoverEnergy = false,
    bool PaelsEyeUsedThisCombat = false,
    int PaelsLegionCooldown = 0,
    bool PaelsLegionTriggeredBlockLastTurn = false,
    long? PaelsLegionAffectedCardPlaySourceId = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool UsedFirstCardPlayModifierThisCombat = false)
{
    public CombatRelicState Fork() => this with
    {
        TriggerCounts = (int[])TriggerCounts.Clone()
    };
}

public sealed record PrototypeCardPlaySeriesState(
    long SourceCardInstanceId,
    PrototypeCardZone SourceCardDestination,
    int? TargetEnemyId,
    int EnergySpent,
    int PlayCount,
    int NextPlayIndex,
    bool RemoveSourceCardOnCompletion = false,
    bool IsAutoPlay = false)
{
    public bool HasRemainingExecutions => NextPlayIndex < PlayCount;
}

public sealed record PrototypeChoiceResolutionContinuationState(
    long? SourceCardInstanceId,
    PrototypeCardZone SourceCardDestination,
    PrototypeQueuedOperation[] Operations,
    PrototypeCombatEvent[] PendingDiscardEvents,
    long[] PendingSlyCardInstanceIds,
    PrototypeCombatEvent[] CompletionEvents,
    PrototypeCardPlaySeriesState? CardPlaySeries = null,
    bool MoveSourceCardOnCompletion = true,
    bool RemoveSourceCardOnCompletion = false,
    bool SourceCardAlreadyMoved = false,
    PrototypeEventDispatchContinuationState? EventDispatchContinuation = null,
    PrototypeChoiceResolutionContinuationState? Parent = null,
    bool CompletePlayerHandCleanup = false)
{
    public PrototypeEnemyDefeatFollowUpV111? EnemyDefeatFollowUp
    {
        get;
        init;
    }
    public PrototypeRepeatedDamageContinuationV111?
        DamageRepeatContinuation { get; init; }

    public PrototypeChoiceResolutionContinuationState Fork() => this with
    {
        Operations = Operations.Select(operation => operation.Fork())
            .ToArray(),
        PendingDiscardEvents =
            (PrototypeCombatEvent[])PendingDiscardEvents.Clone(),
        PendingSlyCardInstanceIds =
            (long[])PendingSlyCardInstanceIds.Clone(),
        CompletionEvents =
            (PrototypeCombatEvent[])CompletionEvents.Clone(),
        EventDispatchContinuation =
            EventDispatchContinuation?.Fork(),
        Parent = Parent?.Fork(),
        EnemyDefeatFollowUp = EnemyDefeatFollowUp?.Fork(),
        DamageRepeatContinuation = DamageRepeatContinuation?.Fork()
    };
}

/// <summary>
/// Mechanics that follow an enemy-defeat event after one of its subscribers
/// suspends. The event itself resumes through the ordinary event-dispatch
/// continuation; this value retains only the post-event death and fatal work.
/// </summary>
public sealed record PrototypeEnemyDefeatFollowUpV111(
    PrototypeQueuedOperation Operation,
    bool FatalEligible,
    int EventDepth,
    int? DefeatedEnemyInstanceId = null)
{
    public PrototypeEnemyDefeatFollowUpV111 Fork() => this with
    {
        Operation = Operation.Fork()
    };
}

/// <summary>
/// Cursor for the source operation that repeats an all-enemy attack once per
/// kill. The current round's target snapshot and next target index keep a
/// suspended defeat hook from replaying earlier hits.
/// </summary>
public sealed record PrototypeRepeatedDamageContinuationV111(
    PrototypeQueuedOperation Operation,
    int PendingRounds,
    int[] CurrentRoundTargetIds,
    int NextTargetIndex,
    int EventDepth)
{
    private int[]? _currentRoundTargetIds =
        Copy(CurrentRoundTargetIds);

    public int[] CurrentRoundTargetIds
    {
        get => Copy(_currentRoundTargetIds!);
        init => _currentRoundTargetIds = Copy(value);
    }

    public PrototypeRepeatedDamageContinuationV111 Fork() => this with
    {
        Operation = Operation.Fork(),
        CurrentRoundTargetIds = _currentRoundTargetIds!
    };

    private static int[] Copy(int[]? values) => values is null
        ? throw new ArgumentNullException(nameof(values))
        : (int[])values.Clone();
}

/// <summary>
/// Internal Nature-owned random-target suspension. Eligible IDs belong to
/// enemies, never card identities or the public player choice menu.
/// </summary>
public sealed record PrototypeControlledTargetChoiceV111(
    string Kind,
    int[] EligibleEnemyInstanceIds,
    long? SlyCardInstanceId = null,
    long? AutoPlayCardInstanceId = null)
{
    public PrototypeControlledTargetChoiceV111 Fork() => this with
    {
        EligibleEnemyInstanceIds = (int[])EligibleEnemyInstanceIds.Clone()
    };
}

/// <summary>
/// Private Nature-owned card-generation choice. The ordered source pool is
/// preserved by identity and is never part of the fair player observation.
/// </summary>
public sealed record PrototypeControlledGeneratedCardV111(
    PrototypeQueuedOperation Operation,
    string[] CandidateCardIds)
{
    public PrototypeControlledGeneratedCardV111 Fork() => this with
    {
        Operation = Operation with { },
        CandidateCardIds = (string[])CandidateCardIds.Clone()
    };
}

/// <summary>Private Nature-owned random combat-potion model choice. The
/// operation queue resumes after one selected potion has been applied.</summary>
public sealed record PrototypeControlledPotionGenerationV111(
    PrototypeQueuedOperation Operation,
    string[] CandidatePotionIds)
{
    public PrototypeControlledPotionGenerationV111 Fork() => this with
    {
        Operation = Operation with { },
        CandidatePotionIds = (string[])CandidatePotionIds.Clone()
    };
}

/// <summary>Nature-owned replacement choice suspended while completing a
/// validated transform card selection. Replacement card IDs are aligned with
/// the selected source instance IDs.</summary>
public sealed record PrototypeControlledCardTransformV111(
    PendingCombatChoiceState OriginalChoice,
    long[] SelectedCardInstanceIds,
    string[] ReplacementCardIds,
    int NextTransformIndex,
    string[] CandidateCardIds)
{
    public PrototypeControlledCardTransformV111 Fork() => this with
    {
        OriginalChoice = OriginalChoice.Fork(),
        SelectedCardInstanceIds = (long[])SelectedCardInstanceIds.Clone(),
        ReplacementCardIds = (string[])ReplacementCardIds.Clone(),
        CandidateCardIds = (string[])CandidateCardIds.Clone()
    };
}

/// <summary>Nature-owned source operation suspended before draw pile insertion.
/// Pending card models are never materialized until Nature selects a position.
/// </summary>
public sealed record PrototypeControlledDrawInsertionV111(
    PrototypeQueuedOperation Operation, string Stage, string? SelectedCardId,
    string[] CandidateCardIds, long[] EligibleDrawPile,
    PrototypeControlledSemanticDrawV111? SemanticDrawContinuation = null,
    PrototypeAfterShuffleRelicInsertionV111? AfterShuffleRelic = null,
    PrototypeAfterShuffleRelicInsertionV111[]? CompletedAfterShuffleRelics = null,
    PrototypeAfterShuffleRelicInsertionV111[]? RemainingAfterShuffleRelics = null,
    string RequestSiteId = StochasticSiteRegistryV111.DrawPileRandomInsertSiteId,
    string RequestOwnerId = "prototype.combat",
    int RequestOrdinal = 0,
    PrototypePersonalHiveDamageContinuationV111?
        PersonalHiveDamageContinuation = null,
    int? AutomaticHandDrawRemaining = null)
{
    public const string PersonalHiveDazedSiteId =
        "combat.effect.personal_hive.dazed.random_insert_index";

    public PrototypeControlledDrawInsertionV111 Fork() => this with
    {
        Operation = Operation with { },
        CandidateCardIds = (string[])CandidateCardIds.Clone(),
        EligibleDrawPile = (long[])EligibleDrawPile.Clone(),
        SemanticDrawContinuation = SemanticDrawContinuation?.Fork(),
        AfterShuffleRelic = AfterShuffleRelic is null
            ? null : AfterShuffleRelic with { },
        CompletedAfterShuffleRelics = CompletedAfterShuffleRelics is null
            ? null : CompletedAfterShuffleRelics
                .Select(item => item with { }).ToArray(),
        RemainingAfterShuffleRelics = RemainingAfterShuffleRelics is null
            ? null : RemainingAfterShuffleRelics
                .Select(item => item with { }).ToArray(),
        PersonalHiveDamageContinuation =
            PersonalHiveDamageContinuation?.Fork()
    };
}

public sealed record PrototypeAfterShuffleRelicInsertionV111(
    string RelicId, long RelicApplicationOrder, string CardId);

public sealed record PrototypePersonalHiveDamageContinuationV111(
    PrototypeQueuedOperation Operation,
    int TargetEnemyId,
    EnemyCombatState SplashTarget,
    int DamageAmount,
    int Overkill,
    int DamageDealt,
    bool Defeated,
    bool FatalEligible,
    int HiveStackCount,
    int EventDepth)
{
    public PrototypePersonalHiveDamageContinuationV111 Fork() => this with
    {
        Operation = Operation.Fork(),
        SplashTarget = SplashTarget.Fork()
    };
}

/// <summary>
/// Source-ordered full-pool shuffle waiting on a Nature swap.
/// The exact source eligibility, partial permutation and saved effect queue
/// are private; only the bounded swap ticket and ordered card IDs go to Nature.
/// </summary>
public sealed record PrototypeControlledPoolShuffleV111(
    PrototypeQueuedOperation Operation, string[] OriginalCardIds,
    string[] CurrentCardIds, int NextSwapIndex, int SelectedCount)
{
    public PrototypeControlledPoolShuffleV111 Fork() => this with
    {
        Operation = Operation with { },
        OriginalCardIds = (string[])OriginalCardIds.Clone(),
        CurrentCardIds = (string[])CurrentCardIds.Clone()
    };
}

/// <summary>Nature-owned hand-cost operation with an ordered per-card cursor.
/// Saved eligibility is checked against live hand instances.</summary>
public sealed record PrototypeControlledRandomCostV111(
    PrototypeQueuedOperation Operation, long[] EligibleCardInstanceIds,
    int NextCardIndex = 0)
{
    public PrototypeControlledRandomCostV111 Fork() => this with
    {
        Operation = Operation with { },
        EligibleCardInstanceIds = (long[])EligibleCardInstanceIds.Clone()
    };
}

/// <summary>Source-ordered without-replacement shortlist from a card pile.
/// The source player's select_cards choice is published only after the
/// requested random prefix has been committed.</summary>
public sealed record PrototypeControlledCardShortlistV111(
    PrototypeQueuedOperation Operation,
    long[] OriginalCandidateCardInstanceIds,
    long[] RemainingCandidateCardInstanceIds,
    long[] SelectedCandidateCardInstanceIds,
    int RequiredCount)
{
    public PrototypeControlledCardShortlistV111 Fork() => this with
    {
        Operation = Operation with { },
        OriginalCandidateCardInstanceIds =
            (long[])OriginalCandidateCardInstanceIds.Clone(),
        RemainingCandidateCardInstanceIds =
            (long[])RemainingCandidateCardInstanceIds.Clone(),
        SelectedCandidateCardInstanceIds =
            (long[])SelectedCandidateCardInstanceIds.Clone()
    };
}

/// <summary>Drawn card already moved to hand. Nature must resolve optional
/// relic cost then Slither cost before dispatching CardDrawn exactly once.
/// RemainingCount excludes this card and keeps its physical draw order.</summary>
public sealed record PrototypeControlledDrawCostV111(
    long CardInstanceId, int RemainingCount, bool FromHandDraw,
    int EventDepth, int RelicMaxExclusive, bool HasSlither,
    string Stage)
{
    public PrototypeControlledDrawCostV111 Fork() => this with { };
}

/// <summary>Semantic draw suspended inside a card-effect operation queue.
/// The queued effect suffix is stored in the pending choice's outer
/// continuation and resumes after the selected card and its draw hooks.</summary>
public sealed record PrototypeControlledSemanticDrawV111(
    int RemainingCount, bool FromHandDraw, int EventDepth,
    PrototypeCardKeywordOverrideSpec? DrawnCardKeyword,
    long[]? DeferredKeywordCardInstanceIds = null,
    bool StratagemSelectionPending = false,
    bool StandaloneSemanticDrawSegment = false,
    bool AutomaticSemanticHandDrawSegment = false)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? DrawEqualSelectionCount { get; init; }

    public PrototypeControlledSemanticDrawV111 Fork() => this with
    {
        DrawnCardKeyword = DrawnCardKeyword is null
            ? null : DrawnCardKeyword with { },
        DeferredKeywordCardInstanceIds = DeferredKeywordCardInstanceIds is null
            ? null : (long[])DeferredKeywordCardInstanceIds.Clone()
    };
}

/// <summary>Unresolved selection of a physical card from a pile, suspended
/// inside an automatic hook. Eligibility and candidate order are preserved.</summary>
public sealed record PrototypeControlledRandomPileCardV111(
    string SiteId, int Ordinal, long[] CandidateCardInstanceIds,
    string SupportFingerprint)
{
    public PrototypeControlledRandomPileCardV111 Fork() => this with
    {
        CandidateCardInstanceIds =
            (long[])CandidateCardInstanceIds.Clone()
    };
}

public sealed record PendingCombatChoiceState(
    string ChoiceId,
    long? SourceCardInstanceId,
    PrototypeCardZone SourceCardDestination,
    PrototypeCardSelectionSpec Selection,
    long[] CandidateCardInstanceIds,
    PrototypeQueuedOperation[] Continuation,
    PrototypeCombatEvent[] CompletionEvents,
    PrototypeCardPlaySeriesState? CardPlaySeries = null,
    bool MoveSourceCardOnCompletion = true,
    bool RemoveSourceCardOnCompletion = false,
    bool SourceCardAlreadyMoved = false,
    PrototypeSelectedCardPowerAction? SelectedCardPower = null,
    PrototypeCardKeywordOverrideSpec? SelectedCardKeyword = null,
    PrototypeTemporaryCardCost? SelectedCardTemporaryCost = null,
    long[]? SequentialSelectedCardInstanceIds = null,
    PrototypeEventDispatchContinuationState? EventDispatchContinuation = null,
    PrototypeChoiceResolutionContinuationState? OuterChoiceContinuation = null,
    PrototypeControlledTargetChoiceV111? ControlledTarget = null,
    PrototypeControlledGeneratedCardV111? ControlledGeneration = null,
    PrototypeControlledPotionGenerationV111? ControlledPotionGeneration = null,
    PrototypeControlledCardTransformV111? ControlledCardTransform = null,
    PrototypeControlledDrawInsertionV111? ControlledDrawInsertion = null,
    PrototypeControlledPoolShuffleV111? ControlledPoolShuffle = null,
    PrototypeControlledRandomCostV111? ControlledRandomCost = null,
    PrototypeControlledDrawCostV111? ControlledDrawCost = null,
    PrototypeControlledCardShortlistV111? ControlledCardShortlist = null,
    PrototypeControlledRandomPileCardV111? ControlledRandomPileCard = null,
    PrototypeControlledSemanticDrawV111? ControlledSemanticDraw = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool SemanticStratagemDrawSelection = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool AutomaticSemanticStratagemDrawSelection = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool StandaloneSemanticDrawHookChoice = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool AutomaticSemanticDrawHookChoice = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool AutomaticSemanticDrawTargetChoice = false)
{
    /// <summary>
    /// Keeps the flattened, snapshot-compatible pending-choice payloads
    /// mutually exclusive while recognizing the one semantic draw-cost
    /// payload that may carry its matching draw frame.
    /// </summary>
    internal bool HasValidControlledPayloadShape()
    {
        const int target = 1 << 0;
        const int generation = 1 << 1;
        const int potionGeneration = 1 << 2;
        const int cardTransform = 1 << 3;
        const int drawInsertion = 1 << 4;
        const int poolShuffle = 1 << 5;
        const int randomCost = 1 << 6;
        const int drawCost = 1 << 7;
        const int cardShortlist = 1 << 8;
        const int randomPileCard = 1 << 9;
        const int semanticDraw = 1 << 10;

        int mask =
            (this.ControlledTarget is not null ? target : 0) |
            (this.ControlledGeneration is not null ? generation : 0) |
            (this.ControlledPotionGeneration is not null
                ? potionGeneration : 0) |
            (this.ControlledCardTransform is not null ? cardTransform : 0) |
            (this.ControlledDrawInsertion is not null ? drawInsertion : 0) |
            (this.ControlledPoolShuffle is not null ? poolShuffle : 0) |
            (this.ControlledRandomCost is not null ? randomCost : 0) |
            (this.ControlledDrawCost is not null ? drawCost : 0) |
            (this.ControlledCardShortlist is not null ? cardShortlist : 0) |
            (this.ControlledRandomPileCard is not null ? randomPileCard : 0) |
            (this.ControlledSemanticDraw is not null ? semanticDraw : 0);

        int semanticDrawModes =
            (this.SemanticStratagemDrawSelection ? 1 << 0 : 0) |
            (this.AutomaticSemanticStratagemDrawSelection ? 1 << 1 : 0) |
            (this.StandaloneSemanticDrawHookChoice ? 1 << 2 : 0) |
            (this.AutomaticSemanticDrawHookChoice ? 1 << 3 : 0);
        if ((semanticDrawModes & (semanticDrawModes - 1)) != 0 ||
            (mask != 0 && semanticDrawModes != 0) ||
            (this.AutomaticSemanticDrawTargetChoice &&
             (this.ChoiceId != "controlled_random_target" ||
              mask != target)))
            return false;

        return this.ChoiceId switch
        {
            "controlled_random_target" => mask == target,
            "controlled_generated_card" => mask == generation,
            "controlled_potion_generation" => mask == potionGeneration,
            "controlled_card_transform" => mask == cardTransform,
            "controlled_draw_insertion" => mask == drawInsertion,
            "controlled_pool_shuffle" => mask == poolShuffle,
            "controlled_random_hand_cost" => mask == randomCost,
            "controlled_draw_cost" => mask == drawCost ||
                mask == (drawCost | semanticDraw),
            "controlled_card_shortlist" => mask == cardShortlist,
            "controlled_random_pile_card" => mask == randomPileCard,
            "controlled_semantic_draw" => mask == semanticDraw,
            _ => mask == 0 &&
                this.ChoiceId is not null &&
                !this.ChoiceId.StartsWith("controlled_",
                    StringComparison.Ordinal)
        };
    }

    public PendingCombatChoiceState Fork() => this with
    {
        CandidateCardInstanceIds = (long[])CandidateCardInstanceIds.Clone(),
        Continuation = Continuation.Select(operation => operation.Fork())
            .ToArray(),
        CompletionEvents = (PrototypeCombatEvent[])CompletionEvents.Clone(),
        SelectedCardTemporaryCost = SelectedCardTemporaryCost is null
            ? null
            : SelectedCardTemporaryCost with { },
        SequentialSelectedCardInstanceIds =
            SequentialSelectedCardInstanceIds is null
                ? null
                : (long[])SequentialSelectedCardInstanceIds.Clone(),
        EventDispatchContinuation = EventDispatchContinuation?.Fork(),
        OuterChoiceContinuation = OuterChoiceContinuation?.Fork(),
        ControlledTarget = ControlledTarget?.Fork(),
        ControlledGeneration = ControlledGeneration?.Fork(),
        ControlledPotionGeneration = ControlledPotionGeneration?.Fork(),
        ControlledCardTransform = ControlledCardTransform?.Fork(),
        ControlledDrawInsertion = ControlledDrawInsertion?.Fork(),
        ControlledPoolShuffle = ControlledPoolShuffle?.Fork(),
        ControlledRandomCost = ControlledRandomCost?.Fork(),
        ControlledDrawCost = ControlledDrawCost?.Fork(),
        ControlledCardShortlist = ControlledCardShortlist?.Fork(),
        ControlledRandomPileCard = ControlledRandomPileCard?.Fork(),
        ControlledSemanticDraw = ControlledSemanticDraw?.Fork()
    };
}

public sealed record PrototypeCombatCounters(
    int CardsPlayedThisTurn = 0,
    int AttacksPlayedThisTurn = 0,
    int SkillsPlayedThisTurn = 0,
    int CardsDiscardedThisTurn = 0,
    int CardsDrawnThisCombat = 0,
    string[]? PlayedCardTagsThisTurn = null,
    int AttacksPlayedLastTurn = 0,
    int CardsPlayedThisCombat = 0,
    int ManualCardsPlayedThisTurn = 0,
    long[]? PlayedCardIdsThisTurn = null,
    long[]? PlayedCardIdsLastTurn = null,
    // Captured at the *finished* CardPlayed event; a copy is retained
    // across turn boundaries and through deterministic snapshot forks.
    PrototypeCombatCardSnapshot? LastOriginalAttackThisTurn = null,
    PrototypeCombatCardSnapshot? LastOriginalAttackLastTurn = null,
    int MaulDamageBonus = 0)
{
    private string[]? _playedCardTagsThisTurn = Copy(
        PlayedCardTagsThisTurn);
    private long[]? _playedCardIdsThisTurn = Copy(
        PlayedCardIdsThisTurn);
    private long[]? _playedCardIdsLastTurn = Copy(
        PlayedCardIdsLastTurn);

    public string[]? PlayedCardTagsThisTurn
    {
        get => Copy(_playedCardTagsThisTurn);
        init => _playedCardTagsThisTurn = Copy(value);
    }

    public long[]? PlayedCardIdsThisTurn
    {
        get => Copy(_playedCardIdsThisTurn);
        init => _playedCardIdsThisTurn = Copy(value);
    }

    public long[]? PlayedCardIdsLastTurn
    {
        get => Copy(_playedCardIdsLastTurn);
        init => _playedCardIdsLastTurn = Copy(value);
    }

    public string[] PlayedTags =>
        PlayedCardTagsThisTurn ?? Array.Empty<string>();

    public int PlaysWithTag(string tag) =>
        PlayedTags.Count(item =>
            StringComparer.Ordinal.Equals(item, tag));

    public PrototypeCombatCounters Fork() => this with
    {
        PlayedCardTagsThisTurn = _playedCardTagsThisTurn,
        PlayedCardIdsThisTurn = _playedCardIdsThisTurn,
        PlayedCardIdsLastTurn = _playedCardIdsLastTurn,
        LastOriginalAttackThisTurn = LastOriginalAttackThisTurn?.Fork(),
        LastOriginalAttackLastTurn = LastOriginalAttackLastTurn?.Fork()
    };

    private static T[]? Copy<T>(T[]? values) => values is null
        ? null : (T[])values.Clone();
}

public sealed record PrototypeToricShield(
    int BlockAmount,
    int ClearsRemaining);

public sealed record PrototypeCombatPetState(
    string PetId,
    int SourceRelicIndex,
    string SkinId);

/// <summary>
/// Reproducible source-targeted auto-play outcome, kept in the *combat
/// snapshot* rather than observed as a future random seed.
/// </summary>
public sealed record PrototypeAutoPlayTargetRoll(
    long CardInstanceId,
    int Turn,
    int CandidateCount,
    int SelectedIndex,
    int TargetEnemyInstanceId);

public sealed partial record CombatState(
    int Turn,
    int Energy,
    int PlayerBlock,
    long[] Hand,
    long[] DrawPile,
    long[] DiscardPile,
    long[] ExhaustPile,
    EnemyCombatState[] Enemies,
    long NextCardInstanceId,
    CombatCardInstance[] Cards,
    PrototypePowerInstanceState[] PlayerPowers,
    long NextPowerApplicationOrder,
    CombatRelicState[]? Relics = null,
    CombatPotionState[]? Potions = null,
    PendingCombatChoiceState? PendingChoice = null,
    PrototypeCombatCounters? Counters = null,
    bool IsPlayerTurn = true,
    PrototypeAutomaticPipelineContinuationState? AutomaticPipelineContinuation = null,
    int Act = 1,
    int Ascension = 0,
    int ExtraCardRewardsEarned = 0,
    long[]? ChoicePool = null,
    long[]? PlayPile = null,
    PrototypeToricShield[]? ToricShields = null,
    long[]? TransformedPersistentCardIds = null,
    bool BoundCardPlayedThisTurn = false,
    NativeCombatRngV111? NativeRng = null,
    PrototypeKnowledgeDemonChoiceState? PendingKnowledgeDemonChoice = null,
    int KnowledgeDemonChoiceResolvedTurn = 0,
    PrototypeAutoPlayTargetRoll[]? AutoPlayTargetRolls = null,
    bool PaelsEyeExtraTurnPending = false,
    PrototypeCombatPetState[]? Pets = null,
    string? CurrentMapNodeId = null,
    int PotionExtraTurnsPending = 0,
    bool IsPlayerSideActive = true,
    SemanticDeckStateV111? SemanticDeck = null)
{
    /// <summary>
    /// Atomic mechanics/provider envelope for this combat. Existing snapshot
    /// fields remain flattened projections.
    /// </summary>
    private SimulationStateV111<CombatMechanicsStateV111,
        CombatStochasticStateV111> _simulation = new(
            new CombatMechanicsStateV111(
                Turn, Energy, PlayerBlock,
                new CombatCardZonesV111(
                    Hand, DrawPile, DiscardPile, ExhaustPile,
                    PlayPile, ChoicePool),
                Enemies, NextCardInstanceId, Cards, PlayerPowers,
                NextPowerApplicationOrder, Relics, Potions, PendingChoice,
                Counters, IsPlayerTurn, AutomaticPipelineContinuation, Act,
                Ascension, ExtraCardRewardsEarned,
                ToricShields, TransformedPersistentCardIds,
                BoundCardPlayedThisTurn, PendingKnowledgeDemonChoice,
                KnowledgeDemonChoiceResolvedTurn, AutoPlayTargetRolls,
                PaelsEyeExtraTurnPending, Pets, CurrentMapNodeId,
                PotionExtraTurnsPending, IsPlayerSideActive),
            new CombatStochasticStateV111(NativeRng, SemanticDeck));

    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<CombatMechanicsStateV111,
        CombatStochasticStateV111> Simulation
    {
        get => _simulation;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if ((_simulation.Stochastic.SemanticDeck is not null ||
                 value.Stochastic.SemanticDeck is not null) &&
                !_simulation.Mechanics.DrawPile.SequenceEqual(
                    value.Mechanics.DrawPile))
                throw new InvalidOperationException(
                    "Provider-owned draw-pile changes require " +
                    "SemanticDrawPileAdapterV111.");
            _simulation = value;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public CombatMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init
        {
            if (Stochastic.SemanticDeck is not null &&
                !Mechanics.DrawPile.SequenceEqual(value.DrawPile))
                throw new InvalidOperationException(
                    "Semantic-deck draw-pile changes require " +
                    "SemanticDrawPileAdapterV111.");
            Simulation = Simulation with { Mechanics = value };
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public CombatStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    /// <summary>
    /// Optional profile snapshot needed to resolve Native Colorless epoch
    /// cards. Null legacy snapshots remain readable but fail closed if a
    /// Native Colorless generation operation needs this information.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? ConfiguredColorlessGenerationFullyUnlocked
    {
        get => Mechanics.ConfiguredColorlessGenerationFullyUnlocked;
        init => Mechanics = Mechanics with
        { ConfiguredColorlessGenerationFullyUnlocked = value };
    }

    /// <summary>
    /// Optional exact Colorless epoch unlock set copied from the configured
    /// run profile. Null means no explicit override: all epochs when the
    /// configured profile is fully unlocked, unknown for a partial profile.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string[]? ConfiguredUnlockedColorlessEpochs
    {
        get => Mechanics.ConfiguredUnlockedColorlessEpochs;
        init => Mechanics = Mechanics with
        { ConfiguredUnlockedColorlessEpochs = value };
    }

    /// <summary>
    /// Atomically publishes a draw-pile mechanics/provider transition. The
    /// adapter validates the candidate correspondence before using this
    /// internal path around the compatibility envelope setter guard.
    /// </summary>
    internal CombatState CommitSemanticDrawPileTransition(
        CombatCardZonesV111 cardZones,
        SemanticDeckStateV111? semanticDeck)
    {
        ArgumentNullException.ThrowIfNull(cardZones);
        SemanticDrawPileAdapterV111.ValidateProviderCorrespondence(
            semanticDeck, cardZones.DrawPile);
        var next = this with { };
        next._simulation = _simulation with
        {
            Mechanics = _simulation.Mechanics with
            {
                CardZones = cardZones
            },
            Stochastic = _simulation.Stochastic with
            {
                SemanticDeck = semanticDeck
            }
        };
        return next;
    }

    public int Turn
    {
        get => Mechanics.Turn;
        init => Mechanics = Mechanics with { Turn = value };
    }

    public int Energy
    {
        get => Mechanics.Energy;
        init => Mechanics = Mechanics with { Energy = value };
    }

    public int PlayerBlock
    {
        get => Mechanics.PlayerBlock;
        init => Mechanics = Mechanics with { PlayerBlock = value };
    }

    public long[] Hand
    {
        get => Mechanics.Hand;
        init => Mechanics = Mechanics with { Hand = value };
    }

    public long[] DrawPile
    {
        get => Mechanics.DrawPile;
        init => Mechanics = Mechanics with { DrawPile = value };
    }

    public long[] DiscardPile
    {
        get => Mechanics.DiscardPile;
        init => Mechanics = Mechanics with { DiscardPile = value };
    }

    public long[] ExhaustPile
    {
        get => Mechanics.ExhaustPile;
        init => Mechanics = Mechanics with { ExhaustPile = value };
    }

    public EnemyCombatState[] Enemies
    {
        get => Mechanics.Enemies;
        init => Mechanics = Mechanics with { Enemies = value };
    }

    public long NextCardInstanceId
    {
        get => Mechanics.NextCardInstanceId;
        init => Mechanics = Mechanics with { NextCardInstanceId = value };
    }

    public CombatCardInstance[] Cards
    {
        get => Mechanics.Cards;
        init => Mechanics = Mechanics with { Cards = value };
    }

    public PrototypePowerInstanceState[] PlayerPowers
    {
        get => Mechanics.PlayerPowers;
        init => Mechanics = Mechanics with { PlayerPowers = value };
    }

    public long NextPowerApplicationOrder
    {
        get => Mechanics.NextPowerApplicationOrder;
        init => Mechanics = Mechanics with
        { NextPowerApplicationOrder = value };
    }

    public CombatRelicState[]? Relics
    {
        get => Mechanics.Relics;
        init => Mechanics = Mechanics with { Relics = value };
    }

    public CombatPotionState[]? Potions
    {
        get => Mechanics.Potions;
        init => Mechanics = Mechanics with { Potions = value };
    }

    public PendingCombatChoiceState? PendingChoice
    {
        get => Mechanics.PendingChoice;
        init => Mechanics = Mechanics with { PendingChoice = value };
    }

    public PrototypeCombatCounters? Counters
    {
        get => Mechanics.Counters;
        init => Mechanics = Mechanics with { Counters = value };
    }

    public bool IsPlayerTurn
    {
        get => Mechanics.IsPlayerTurn;
        init => Mechanics = Mechanics with { IsPlayerTurn = value };
    }

    public PrototypeAutomaticPipelineContinuationState?
        AutomaticPipelineContinuation
    {
        get => Mechanics.AutomaticPipelineContinuation;
        init => Mechanics = Mechanics with
        { AutomaticPipelineContinuation = value };
    }

    public int Act
    {
        get => Mechanics.Act;
        init => Mechanics = Mechanics with { Act = value };
    }

    public int Ascension
    {
        get => Mechanics.Ascension;
        init => Mechanics = Mechanics with { Ascension = value };
    }

    public int ExtraCardRewardsEarned
    {
        get => Mechanics.ExtraCardRewardsEarned;
        init => Mechanics = Mechanics with { ExtraCardRewardsEarned = value };
    }

    public long[]? ChoicePool
    {
        get => Mechanics.ChoicePool;
        init => Mechanics = Mechanics with { ChoicePool = value };
    }

    public long[]? PlayPile
    {
        get => Mechanics.PlayPile;
        init => Mechanics = Mechanics with { PlayPile = value };
    }

    public PrototypeToricShield[]? ToricShields
    {
        get => Mechanics.ToricShields;
        init => Mechanics = Mechanics with { ToricShields = value };
    }

    public long[]? TransformedPersistentCardIds
    {
        get => Mechanics.TransformedPersistentCardIds;
        init => Mechanics = Mechanics with
        { TransformedPersistentCardIds = value };
    }

    public bool BoundCardPlayedThisTurn
    {
        get => Mechanics.BoundCardPlayedThisTurn;
        init => Mechanics = Mechanics with
        { BoundCardPlayedThisTurn = value };
    }

    public PrototypeKnowledgeDemonChoiceState?
        PendingKnowledgeDemonChoice
    {
        get => Mechanics.PendingKnowledgeDemonChoice;
        init => Mechanics = Mechanics with
        { PendingKnowledgeDemonChoice = value };
    }

    public int KnowledgeDemonChoiceResolvedTurn
    {
        get => Mechanics.KnowledgeDemonChoiceResolvedTurn;
        init => Mechanics = Mechanics with
        { KnowledgeDemonChoiceResolvedTurn = value };
    }

    public PrototypeAutoPlayTargetRoll[]? AutoPlayTargetRolls
    {
        get => Mechanics.AutoPlayTargetRolls;
        init => Mechanics = Mechanics with { AutoPlayTargetRolls = value };
    }

    public bool PaelsEyeExtraTurnPending
    {
        get => Mechanics.PaelsEyeExtraTurnPending;
        init => Mechanics = Mechanics with
        { PaelsEyeExtraTurnPending = value };
    }

    public PrototypeCombatPetState[]? Pets
    {
        get => Mechanics.Pets;
        init => Mechanics = Mechanics with { Pets = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CurrentMapNodeId
    {
        get => Mechanics.CurrentMapNodeId;
        init => Mechanics = Mechanics with { CurrentMapNodeId = value };
    }

    public int PotionExtraTurnsPending
    {
        get => Mechanics.PotionExtraTurnsPending;
        init => Mechanics = Mechanics with
        { PotionExtraTurnsPending = value };
    }

    public bool IsPlayerSideActive
    {
        get => Mechanics.IsPlayerSideActive;
        init => Mechanics = Mechanics with { IsPlayerSideActive = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeCombatRngV111? NativeRng
    {
        get => Stochastic.NativeRng;
        init => Stochastic = CombatStochasticStateV111.WithNativeRng(
            Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SemanticDeckStateV111? SemanticDeck
    {
        get => Stochastic.SemanticDeck;
        init => Stochastic = CombatStochasticStateV111.WithSemanticDeck(
            Stochastic, value);
    }

    public CombatState Fork() => this with
    {
        Mechanics = Mechanics.Fork(),
        Stochastic = Stochastic.Fork()
    };

    public CombatRelicState[] RelicStates =>
        Relics ?? Array.Empty<CombatRelicState>();

    public CombatPotionState[] PotionStates =>
        Potions ?? Array.Empty<CombatPotionState>();

    public long[] ChoiceCardIds =>
        ChoicePool ?? Array.Empty<long>();

    public long[] PlayCardIds =>
        PlayPile ?? Array.Empty<long>();

    public PrototypeCombatCounters CounterState =>
        Counters ?? new PrototypeCombatCounters();
}

public sealed record PrototypePendingDeckChoiceState(
    string SourceRelicId,
    PrototypePersistentDeckChoiceKind Kind,
    int RemainingSelections,
    long[] CandidateCardInstanceIds,
    bool UpgradeTransformedCards = false)
{
    private long[] _candidateCardInstanceIds =
        CloneRequired(CandidateCardInstanceIds,
            nameof(CandidateCardInstanceIds));

    public long[] CandidateCardInstanceIds
    {
        get => (long[])_candidateCardInstanceIds.Clone();
        init => _candidateCardInstanceIds = CloneRequired(
            value, nameof(CandidateCardInstanceIds));
    }

    public int MinimumSelectionsRemaining { get; init; } = RemainingSelections;
    public string? CardIdAfterChoices { get; init; }
    public int HpLossAfterChoices { get; init; }

    public PrototypePendingDeckChoiceState Fork() => this with
    {
        CandidateCardInstanceIds =
            _candidateCardInstanceIds
    };

    private static long[] CloneRequired(
        long[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (long[])values.Clone();
    }
}

public sealed record RewardState(
    string SourceRoom,
    string[] CardOptions,
    string? PotionOption,
    string? RelicOption,
    bool CardResolved,
    bool PotionResolved,
    bool RelicResolved,
    bool EndsAct,
    string[][]? ExtraCardOptions = null,
    int ExtraCardRewardsResolved = 0,
    string[]? RelicOptions = null,
    PrototypePendingDeckChoiceState? PendingDeckChoice = null,
    bool CardOptionsUpgraded = false,
    bool[]? ExtraCardOptionsUpgraded = null,
    string? AddCardAfterReward = null,
    string[][]? CardBundles = null,
    string[]? ExtraRelicRewardIds = null,
    int ExtraRelicsResolved = 0,
    bool[]? CardOptionUpgradeFlags = null,
    bool[][]? ExtraCardOptionUpgradeFlags = null,
    int? GoldOption = null,
    bool GoldResolved = true,
    bool IndependentSelection = false,
    bool[]? ExtraCardGroupsResolved = null,
    bool[]? ExtraRelicGroupsResolved = null,
    int[]? ExtraGoldOptions = null,
    bool[]? ExtraGoldGroupsResolved = null,
    // Native Swipe.BeforeDeath restores the deck version and adds a
    // special card reward; the card is already in the deck and must
    // not be duplicated when this reward is inspected.
    CardInstance[]? ReturnedStolenCards = null,
    bool[]? ExtraRelicWaxFlags = null,
    PrototypeCardRewardGenerator?[]? CardRewardGenerators = null,
    string[]? ExtraPotionOptions = null,
    bool[]? ExtraPotionGroupsResolved = null,
    PrototypeCardEnchantment?[]? CardOptionEnchantments = null,
    PrototypeCardEnchantment?[][]? ExtraCardOptionEnchantments = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool AddRandomCurseAfterReward = false)
{
    private string[]? _cardOptions = Copy(CardOptions);
    private string[][]? _extraCardOptions = CloneNested(ExtraCardOptions);
    private string[]? _relicOptions = Copy(RelicOptions);
    private bool[]? _extraCardOptionsUpgraded = Copy(ExtraCardOptionsUpgraded);
    private string[][]? _cardBundles = CloneNested(CardBundles);
    private string[]? _extraRelicRewardIds = Copy(ExtraRelicRewardIds);
    private bool[]? _cardOptionUpgradeFlags = Copy(CardOptionUpgradeFlags);
    private bool[][]? _extraCardOptionUpgradeFlags =
        CloneNested(ExtraCardOptionUpgradeFlags);
    private bool[]? _extraCardGroupsResolved = Copy(ExtraCardGroupsResolved);
    private bool[]? _extraRelicGroupsResolved = Copy(ExtraRelicGroupsResolved);
    private int[]? _extraGoldOptions = Copy(ExtraGoldOptions);
    private bool[]? _extraGoldGroupsResolved = Copy(ExtraGoldGroupsResolved);
    private CardInstance[]? _returnedStolenCards = CloneCards(ReturnedStolenCards);
    private bool[]? _extraRelicWaxFlags = Copy(ExtraRelicWaxFlags);
    private PrototypeCardRewardGenerator?[]? _cardRewardGenerators =
        Copy(CardRewardGenerators);
    private string[]? _extraPotionOptions = Copy(ExtraPotionOptions);
    private bool[]? _extraPotionGroupsResolved = Copy(ExtraPotionGroupsResolved);
    private PrototypeCardEnchantment?[]? _cardOptionEnchantments =
        CloneEnchantments(CardOptionEnchantments);
    private PrototypeCardEnchantment?[][]? _extraCardOptionEnchantments =
        CloneNestedEnchantments(ExtraCardOptionEnchantments);

    public string[] CardOptions
    {
        get => _cardOptions is null ? null! : (string[])_cardOptions.Clone();
        init => _cardOptions = Copy(value);
    }

    public string[][]? ExtraCardOptions
    {
        get => CloneNested(_extraCardOptions);
        init => _extraCardOptions = CloneNested(value);
    }

    public string[]? RelicOptions
    {
        get => Copy(_relicOptions);
        init => _relicOptions = Copy(value);
    }

    public bool[]? ExtraCardOptionsUpgraded
    {
        get => Copy(_extraCardOptionsUpgraded);
        init => _extraCardOptionsUpgraded = Copy(value);
    }

    public string[][]? CardBundles
    {
        get => CloneNested(_cardBundles);
        init => _cardBundles = CloneNested(value);
    }

    public string[]? ExtraRelicRewardIds
    {
        get => Copy(_extraRelicRewardIds);
        init => _extraRelicRewardIds = Copy(value);
    }

    public bool[]? CardOptionUpgradeFlags
    {
        get => Copy(_cardOptionUpgradeFlags);
        init => _cardOptionUpgradeFlags = Copy(value);
    }

    public bool[][]? ExtraCardOptionUpgradeFlags
    {
        get => CloneNested(_extraCardOptionUpgradeFlags);
        init => _extraCardOptionUpgradeFlags = CloneNested(value);
    }

    public bool[]? ExtraCardGroupsResolved
    {
        get => Copy(_extraCardGroupsResolved);
        init => _extraCardGroupsResolved = Copy(value);
    }

    public bool[]? ExtraRelicGroupsResolved
    {
        get => Copy(_extraRelicGroupsResolved);
        init => _extraRelicGroupsResolved = Copy(value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int[]? ExtraGoldOptions
    {
        get => Copy(_extraGoldOptions);
        init => _extraGoldOptions = Copy(value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool[]? ExtraGoldGroupsResolved
    {
        get => Copy(_extraGoldGroupsResolved);
        init => _extraGoldGroupsResolved = Copy(value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CardInstance[]? ReturnedStolenCards
    {
        get => CloneCards(_returnedStolenCards);
        init => _returnedStolenCards = CloneCards(value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool[]? ExtraRelicWaxFlags
    {
        get => Copy(_extraRelicWaxFlags);
        init => _extraRelicWaxFlags = Copy(value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeCardRewardGenerator?[]? CardRewardGenerators
    {
        get => Copy(_cardRewardGenerators);
        init => _cardRewardGenerators = Copy(value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string[]? ExtraPotionOptions
    {
        get => Copy(_extraPotionOptions);
        init => _extraPotionOptions = Copy(value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool[]? ExtraPotionGroupsResolved
    {
        get => Copy(_extraPotionGroupsResolved);
        init => _extraPotionGroupsResolved = Copy(value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeCardEnchantment?[]? CardOptionEnchantments
    {
        get => CloneEnchantments(_cardOptionEnchantments);
        init => _cardOptionEnchantments = CloneEnchantments(value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeCardEnchantment?[][]? ExtraCardOptionEnchantments
    {
        get => CloneNestedEnchantments(_extraCardOptionEnchantments);
        init => _extraCardOptionEnchantments =
            CloneNestedEnchantments(value);
    }

    [System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? ExtraRelicGroupsSkippable { get; init; }

    /// <summary>
    /// Native rewards resolve each extra offer by its stable original
    /// group index. The legacy integer counters remain a count of
    /// completed groups and retain prefix semantics when flags are null.
    /// </summary>
    public bool IsExtraCardGroupResolved(int index) =>
        ExtraCardGroupsResolved is { } flags
            ? flags[index]
            : index < ExtraCardRewardsResolved;

    public bool IsExtraRelicGroupResolved(int index) =>
        ExtraRelicGroupsResolved is { } flags
            ? flags[index]
            : index < ExtraRelicsResolved;

    public bool IsExtraRelicGroupSkippable(int index) =>
        ExtraRelicGroupsSkippable != false;

    public bool IsExtraPotionGroupResolved(int index) =>
        ExtraPotionGroupsResolved is { } flags && flags[index];

    public int FirstPendingExtraCardGroupIndex
    {
        get
        {
            var count = ExtraCardOptions?.Length ?? 0;
            for (var index = 0; index < count; index++)
            {
                if (!IsExtraCardGroupResolved(index))
                {
                    return index;
                }
            }
            return -1;
        }
    }

    public int FirstPendingExtraRelicGroupIndex
    {
        get
        {
            var count = ExtraRelicRewardIds?.Length ?? 0;
            for (var index = 0; index < count; index++)
            {
                if (!IsExtraRelicGroupResolved(index))
                {
                    return index;
                }
            }
            return -1;
        }
    }

    public RewardState ResolveExtraCardGroup(int index)
    {
        if (IsExtraCardGroupResolved(index))
        {
            throw new InvalidOperationException(
                "Card reward group has already been resolved.");
        }
        var flags = ExtraCardGroupsResolved is null
            ? null
            : (bool[])ExtraCardGroupsResolved.Clone();
        if (flags is not null)
        {
            flags[index] = true;
        }
        return this with
        {
            ExtraCardRewardsResolved = ExtraCardRewardsResolved + 1,
            ExtraCardGroupsResolved = flags
        };
    }

    public RewardState ResolveExtraRelicGroup(int index)
    {
        if (IsExtraRelicGroupResolved(index))
        {
            throw new InvalidOperationException(
                "Relic reward group has already been resolved.");
        }
        var flags = ExtraRelicGroupsResolved is null
            ? null
            : (bool[])ExtraRelicGroupsResolved.Clone();
        if (flags is not null)
        {
            flags[index] = true;
        }
        return this with
        {
            ExtraRelicsResolved = ExtraRelicsResolved + 1,
            ExtraRelicGroupsResolved = flags
        };
    }

    public RewardState ResolveExtraPotionGroup(int index)
    {
        if (IsExtraPotionGroupResolved(index))
            throw new InvalidOperationException(
                "Potion reward group has already been resolved.");
        var flags = (bool[])ExtraPotionGroupsResolved!.Clone();
        flags[index] = true;
        return this with { ExtraPotionGroupsResolved = flags };
    }

    public string[] CardOptionsForGroup(int groupIndex) =>
        groupIndex == 0
            ? CardOptions
            : ExtraCardOptions![groupIndex - 1];

    public bool[] CardUpgradeFlagsForGroup(int groupIndex)
    {
        if (groupIndex == 0)
        {
            return Enumerable.Range(0, CardOptions.Length)
                .Select(index => CardOptionsUpgraded
                    || (CardOptionUpgradeFlags is { } flags
                        && flags[index]))
                .ToArray();
        }
        var extraIndex = groupIndex - 1;
        var options = ExtraCardOptions![extraIndex];
        var silver = ExtraCardOptionsUpgraded is { } upgrades
            && upgrades[extraIndex];
        return Enumerable.Range(0, options.Length)
            .Select(index => silver
                || (ExtraCardOptionUpgradeFlags is { } flags
                    && flags[extraIndex][index]))
            .ToArray();
    }

    public PrototypeCardEnchantment?[] CardEnchantmentsForGroup(
        int groupIndex)
    {
        if (groupIndex == 0)
        {
            return CardOptionEnchantments is { } enchantments
                ? (PrototypeCardEnchantment?[])enchantments.Clone()
                : new PrototypeCardEnchantment?[CardOptions.Length];
        }

        var extraIndex = groupIndex - 1;
        var options = ExtraCardOptions![extraIndex];
        return ExtraCardOptionEnchantments is { } extras
            ? (PrototypeCardEnchantment?[])extras[extraIndex].Clone()
            : new PrototypeCardEnchantment?[options.Length];
    }

    public string[] CurrentCardOptions =>
        !CardResolved
            ? CardOptions
            : FirstPendingExtraCardGroupIndex is var index && index >= 0
                ? ExtraCardOptions![index]
                : Array.Empty<string>();

    /// <summary>Upgrades for the next pending card reward group.</summary>
    public bool[] CurrentCardOptionUpgradeFlags =>
        !CardResolved
            ? CardUpgradeFlagsForGroup(0)
            : FirstPendingExtraCardGroupIndex is var index && index >= 0
                ? CardUpgradeFlagsForGroup(index + 1)
                : Array.Empty<bool>();

    public PrototypeCardEnchantment?[] CurrentCardOptionEnchantments =>
        !CardResolved
            ? CardEnchantmentsForGroup(0)
            : FirstPendingExtraCardGroupIndex is var index && index >= 0
                ? CardEnchantmentsForGroup(index + 1)
                : Array.Empty<PrototypeCardEnchantment?>();

    public string[] CurrentRelicOptions =>
        RelicOptions is { Length: > 0 }
            ? RelicOptions
            : RelicOption is null
                ? Array.Empty<string>()
                : [RelicOption];

    public RewardState Fork() => this with
    {
        _cardOptions = Copy(_cardOptions),
        _extraCardOptions = CloneNested(_extraCardOptions),
        _relicOptions = Copy(_relicOptions),
        _extraCardOptionsUpgraded = Copy(_extraCardOptionsUpgraded),
        _cardOptionUpgradeFlags = Copy(_cardOptionUpgradeFlags),
        _extraCardOptionUpgradeFlags =
            CloneNested(_extraCardOptionUpgradeFlags),
        _cardBundles = CloneNested(_cardBundles),
        _extraRelicRewardIds = Copy(_extraRelicRewardIds),
        _extraCardGroupsResolved = Copy(_extraCardGroupsResolved),
        _extraRelicGroupsResolved = Copy(_extraRelicGroupsResolved),
        _extraRelicWaxFlags = Copy(_extraRelicWaxFlags),
        _cardRewardGenerators = CloneGenerators(_cardRewardGenerators),
        _extraGoldOptions = Copy(_extraGoldOptions),
        _extraGoldGroupsResolved = Copy(_extraGoldGroupsResolved),
        _extraPotionOptions = Copy(_extraPotionOptions),
        _extraPotionGroupsResolved = Copy(_extraPotionGroupsResolved),
        _cardOptionEnchantments = CloneEnchantments(
            _cardOptionEnchantments),
        _extraCardOptionEnchantments = CloneNestedEnchantments(
            _extraCardOptionEnchantments),
        PendingDeckChoice = PendingDeckChoice?.Fork(),
        _returnedStolenCards = CloneCards(_returnedStolenCards)
    };

    private static T[]? Copy<T>(T[]? values) =>
        values is null ? null : (T[])values.Clone();

    private static T[][]? CloneNested<T>(T[][]? groups) =>
        groups is null
            ? null
            : groups.Select(group => group is null
                ? null!
                : (T[])group.Clone()).ToArray();

    private static CardInstance[]? CloneCards(CardInstance[]? cards) =>
        cards is null
            ? null
            : cards.Select(card => card is null
                ? null!
                : card with
                {
                    PersistentState = card.PersistentState.Clone()
                }).ToArray();

    private static PrototypeCardEnchantment?[]? CloneEnchantments(
        PrototypeCardEnchantment?[]? enchantments) =>
        enchantments is null
            ? null
            : enchantments.Select(enchantment => enchantment is null
                ? null
                : enchantment with { }).ToArray();

    private static PrototypeCardEnchantment?[][]? CloneNestedEnchantments(
        PrototypeCardEnchantment?[][]? groups) =>
        groups is null
            ? null
            : groups.Select(group => group is null
                ? null!
                : CloneEnchantments(group)!).ToArray();

    private static PrototypeCardRewardGenerator?[]? CloneGenerators(
        PrototypeCardRewardGenerator?[]? generators) =>
        generators is null
            ? null
            : generators.Select(generator => generator is null
                ? null
                : generator with { }).ToArray();
}

public enum PrototypeCardRewardGeneratorKind
{
    RewardPool,
    RarePool,
    DistinctRewardPool,
    RarityPool,
    Encounter
}

public sealed record PrototypeCardRewardGenerator(
    PrototypeCardRewardGeneratorKind Kind,
    int Act,
    PrototypeRoomType? Room = null,
    bool Rerolled = false,
    PrototypeCardRarity? Rarity = null,
    PrototypeCardType? CardType = null);

public sealed record ShopOffer(
    int OfferId,
    string ItemId,
    int Price,
    bool Sold,
    int? BasePrice = null,
    bool OnSale = false)
{
    public int UndiscountedPrice => BasePrice ?? Price;
}

public sealed record ShopState
{
    private ShopOffer[]? _cardOffers;
    private ShopOffer[]? _additionalPotionOffers;
    private ShopOffer[]? _additionalRelicOffers;

    [JsonConstructor]
    public ShopState(
        ShopOffer[] CardOffers,
        ShopOffer? PotionOffer,
        ShopOffer? RelicOffer,
        int RemovalPrice = 75,
        bool RemovalUsed = false,
        int? BaseRemovalPrice = null,
        ShopOffer[]? AdditionalPotionOffers = null,
        ShopOffer[]? AdditionalRelicOffers = null,
        PrototypePendingDeckChoiceState? PendingDeckChoice = null,
        bool LordsParasolPurchaseFlowActive = false,
        int? LordsParasolPendingPotionOfferId = null,
        bool LordsParasolRemovalChoicePending = false,
        bool LordsParasolPotionOffersSkipped = false)
    {
        this.CardOffers = CardOffers;
        this.PotionOffer = PotionOffer;
        this.RelicOffer = RelicOffer;
        this.RemovalPrice = RemovalPrice;
        this.RemovalUsed = RemovalUsed;
        this.BaseRemovalPrice = BaseRemovalPrice;
        this.AdditionalPotionOffers = AdditionalPotionOffers;
        this.AdditionalRelicOffers = AdditionalRelicOffers;
        this.PendingDeckChoice = PendingDeckChoice;
        this.LordsParasolPurchaseFlowActive = LordsParasolPurchaseFlowActive;
        this.LordsParasolPendingPotionOfferId = LordsParasolPendingPotionOfferId;
        this.LordsParasolRemovalChoicePending = LordsParasolRemovalChoicePending;
        this.LordsParasolPotionOffersSkipped = LordsParasolPotionOffersSkipped;
    }

    public ShopOffer[] CardOffers
    {
        get => _cardOffers is null ? null! : (ShopOffer[])_cardOffers.Clone();
        init => _cardOffers = Copy(value);
    }
    public ShopOffer? PotionOffer { get; init; }
    public ShopOffer? RelicOffer { get; init; }
    public int RemovalPrice { get; init; }
    public bool RemovalUsed { get; init; }
    public int? BaseRemovalPrice { get; init; }
    public ShopOffer[]? AdditionalPotionOffers
    {
        get => Copy(_additionalPotionOffers);
        init => _additionalPotionOffers = Copy(value);
    }
    public ShopOffer[]? AdditionalRelicOffers
    {
        get => Copy(_additionalRelicOffers);
        init => _additionalRelicOffers = Copy(value);
    }
    public PrototypePendingDeckChoiceState? PendingDeckChoice { get; init; }
    public bool LordsParasolPurchaseFlowActive { get; init; }
    public int? LordsParasolPendingPotionOfferId { get; init; }
    public bool LordsParasolRemovalChoicePending { get; init; }
    public bool LordsParasolPotionOffersSkipped { get; init; }

    public int UndiscountedRemovalPrice =>
        BaseRemovalPrice ?? RemovalPrice;

    public ShopOffer[] PotionOffers =>
        (PotionOffer is null
            ? Array.Empty<ShopOffer>()
            : [PotionOffer])
        .Concat(
            AdditionalPotionOffers
            ?? Array.Empty<ShopOffer>())
        .ToArray();

    public ShopOffer[] RelicOffers =>
        (RelicOffer is null
            ? Array.Empty<ShopOffer>()
            : [RelicOffer])
        .Concat(
            AdditionalRelicOffers
            ?? Array.Empty<ShopOffer>())
        .ToArray();

    public ShopState Fork() => new(
        _cardOffers!,
        PotionOffer is null ? null : PotionOffer with { },
        RelicOffer is null ? null : RelicOffer with { },
        RemovalPrice,
        RemovalUsed,
        BaseRemovalPrice,
        _additionalPotionOffers,
        _additionalRelicOffers,
        PendingDeckChoice?.Fork(),
        LordsParasolPurchaseFlowActive,
        LordsParasolPendingPotionOfferId,
        LordsParasolRemovalChoicePending,
        LordsParasolPotionOffersSkipped);

    public void Deconstruct(
        out ShopOffer[] CardOffers,
        out ShopOffer? PotionOffer,
        out ShopOffer? RelicOffer,
        out int RemovalPrice,
        out bool RemovalUsed,
        out int? BaseRemovalPrice,
        out ShopOffer[]? AdditionalPotionOffers,
        out ShopOffer[]? AdditionalRelicOffers,
        out PrototypePendingDeckChoiceState? PendingDeckChoice,
        out bool LordsParasolPurchaseFlowActive,
        out int? LordsParasolPendingPotionOfferId,
        out bool LordsParasolRemovalChoicePending,
        out bool LordsParasolPotionOffersSkipped)
    {
        CardOffers = this.CardOffers;
        PotionOffer = this.PotionOffer;
        RelicOffer = this.RelicOffer;
        RemovalPrice = this.RemovalPrice;
        RemovalUsed = this.RemovalUsed;
        BaseRemovalPrice = this.BaseRemovalPrice;
        AdditionalPotionOffers = this.AdditionalPotionOffers;
        AdditionalRelicOffers = this.AdditionalRelicOffers;
        PendingDeckChoice = this.PendingDeckChoice;
        LordsParasolPurchaseFlowActive = this.LordsParasolPurchaseFlowActive;
        LordsParasolPendingPotionOfferId = this.LordsParasolPendingPotionOfferId;
        LordsParasolRemovalChoicePending = this.LordsParasolRemovalChoicePending;
        LordsParasolPotionOffersSkipped = this.LordsParasolPotionOffersSkipped;
    }

    private static ShopOffer[]? Copy(ShopOffer[]? offers) =>
        offers is null ? null : (ShopOffer[])offers.Clone();
}

public sealed record PrototypePendingEventDeckChoiceState(
    string ChoiceId,
    PrototypePersistentDeckChoiceKind Kind,
    int RemainingSelections,
    long[] CandidateCardInstanceIds,
    bool UpgradeTransformedCards = false,
    string? SourceRelicId = null,
    string? TransformToCardId = null,
    PrototypeCardEnchantmentKind? EnchantmentKind = null,
    bool BasicCardsOnly = false,
    string? RequiredCardTag = null,
    PrototypeCardType? RequiredCardType = null,
    int EnchantmentAmount = 1,
    bool RequiresUpgradable = false,
    bool PreserveUpgradeOnTransformation = false,
    bool PreserveEnchantmentOnTransformation = false,
    int? MinimumSelectionsRemaining = null)
{
    private long[] _candidateCardInstanceIds =
        CloneRequired(CandidateCardInstanceIds,
            nameof(CandidateCardInstanceIds));

    public long[] CandidateCardInstanceIds
    {
        get => (long[])_candidateCardInstanceIds.Clone();
        init => _candidateCardInstanceIds = CloneRequired(
            value, nameof(CandidateCardInstanceIds));
    }

    public PrototypePendingEventDeckChoiceState Fork() => this with
    {
        CandidateCardInstanceIds =
            _candidateCardInstanceIds
    };

    private static long[] CloneRequired(
        long[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (long[])values.Clone();
    }
}

public sealed record PrototypePendingEventPotionReplacementState(
    string ChoiceId,
    string PotionId,
    int[] CandidateSlots)
{
    private int[] _candidateSlots =
        CloneRequired(CandidateSlots, nameof(CandidateSlots));

    public int[] CandidateSlots
    {
        get => (int[])_candidateSlots.Clone();
        init => _candidateSlots = CloneRequired(value, nameof(CandidateSlots));
    }

    public PrototypePendingEventPotionReplacementState Fork() => this with
    {
        CandidateSlots = _candidateSlots
    };

    private static int[] CloneRequired(int[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (int[])values.Clone();
    }
}

public sealed partial record EventState(
    string EventId,
    string? ChosenChoiceId = null,
    PrototypePendingEventDeckChoiceState? PendingDeckChoice = null,
    PrototypePendingEventPotionReplacementState? PendingPotionReplacement = null,
    string[]? QueuedPotionIds = null,
    PrototypePendingEventDeckChoiceState[]? QueuedDeckChoices = null,
    string[]? OfferedChoiceIds = null,
    RewardState? PendingReward = null,
    int DeferredHpLoss = 0,
    int NativePageIndex = 0,
    string? DeferredCardId = null,
    int NativeEventGold = 0,
    int NativeEventSecondaryGold = 0,
    int? NativeEventPotionSlot = null,
    string? NativeDishId = null,
    string? NativeLastDishId = null,
    int NativeDishCount = 0,
    bool NativeDishRollPending = false,
    int DeferredGoldLoss = 0,
    // Native Glory quest continuation; these rolls are fixed when the
    // chest is opened, independent of reward selection or fork order.
    string? RepyFirstChoiceId = null,
    string[]? RepyChestPotionIds = null,
    string[]? RepyChestRelicIds = null,
    int RepyChestGroupsResolved = 0,
    // Glory Tinker Time chooses type and rider on separate pages;
    // the type is persisted independently of the random rider offer.
    string? NativeTinkerType = null,
    RngStreamState? NativeEventRng = null,
    bool? NativeTanxTriEligible = null,
    bool? NativeNonupeipeBraceletEligible = null,
    PrototypeFuturePotionOffer[]? NativeFuturePotionOffers = null,
    string? NativeEventRngProviderMode = null,
    PrototypeControlledEventChoiceContinuationV111? ControlledChoice = null)
{
    /// <summary>
    /// Atomic in-memory mechanics/provider envelope for the active event.
    /// Existing members remain the flattened snapshot and source-call
    /// representation.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<EventMechanicsStateV111,
        EventStochasticStateV111> Simulation { get; init; } = new(
            new EventMechanicsStateV111(
                EventId, ChosenChoiceId, PendingDeckChoice,
                PendingPotionReplacement, QueuedPotionIds, QueuedDeckChoices,
                OfferedChoiceIds, PendingReward, DeferredHpLoss,
                NativePageIndex, DeferredGoldLoss, RepyFirstChoiceId,
                RepyChestGroupsResolved, NativeTanxTriEligible,
                NativeNonupeipeBraceletEligible),
            new EventStochasticStateV111(
                NativeEventRng, NativeEventRngProviderMode, ControlledChoice,
                new EventCommittedRandomStateV111(
                    DeferredCardId, NativeEventGold,
                    NativeEventSecondaryGold, NativeEventPotionSlot,
                    NativeDishId, NativeLastDishId, NativeDishCount,
                    NativeDishRollPending, RepyChestPotionIds,
                    RepyChestRelicIds, NativeTinkerType,
                    NativeFuturePotionOffers)));

    /// <summary>
    /// Compatibility projection of the mechanics part of <see cref="Simulation"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public EventMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    /// <summary>
    /// Compatibility projection of the provider part of <see cref="Simulation"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public EventStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public string EventId
    {
        get => Mechanics.EventId;
        init => Mechanics = Mechanics with { EventId = value };
    }

    public string? ChosenChoiceId
    {
        get => Mechanics.ChosenChoiceId;
        init => Mechanics = Mechanics with { ChosenChoiceId = value };
    }

    public PrototypePendingEventDeckChoiceState? PendingDeckChoice
    {
        get => Mechanics.PendingDeckChoice;
        init => Mechanics = Mechanics with { PendingDeckChoice = value };
    }

    public PrototypePendingEventPotionReplacementState? PendingPotionReplacement
    {
        get => Mechanics.PendingPotionReplacement;
        init => Mechanics = Mechanics with { PendingPotionReplacement = value };
    }

    public string[]? QueuedPotionIds
    {
        get => Mechanics.QueuedPotionIds;
        init => Mechanics = Mechanics with { QueuedPotionIds = value };
    }

    public PrototypePendingEventDeckChoiceState[]? QueuedDeckChoices
    {
        get => Mechanics.QueuedDeckChoices;
        init => Mechanics = Mechanics with { QueuedDeckChoices = value };
    }

    public string[]? OfferedChoiceIds
    {
        get => Mechanics.OfferedChoiceIds;
        init => Mechanics = Mechanics with { OfferedChoiceIds = value };
    }

    public RewardState? PendingReward
    {
        get => Mechanics.PendingReward;
        init => Mechanics = Mechanics with { PendingReward = value };
    }

    public int DeferredHpLoss
    {
        get => Mechanics.DeferredHpLoss;
        init => Mechanics = Mechanics with { DeferredHpLoss = value };
    }

    public int NativePageIndex
    {
        get => Mechanics.NativePageIndex;
        init => Mechanics = Mechanics with { NativePageIndex = value };
    }

    public int DeferredGoldLoss
    {
        get => Mechanics.DeferredGoldLoss;
        init => Mechanics = Mechanics with { DeferredGoldLoss = value };
    }

    public string? RepyFirstChoiceId
    {
        get => Mechanics.RepyFirstChoiceId;
        init => Mechanics = Mechanics with { RepyFirstChoiceId = value };
    }

    public int RepyChestGroupsResolved
    {
        get => Mechanics.RepyChestGroupsResolved;
        init => Mechanics = Mechanics with { RepyChestGroupsResolved = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? NativeTanxTriEligible
    {
        get => Mechanics.NativeTanxTriEligible;
        init => Mechanics = Mechanics with { NativeTanxTriEligible = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? NativeNonupeipeBraceletEligible
    {
        get => Mechanics.NativeNonupeipeBraceletEligible;
        init => Mechanics = Mechanics with
        { NativeNonupeipeBraceletEligible = value };
    }

    public string? DeferredCardId
    {
        get => Stochastic.RandomOutcomes.DeferredCardId;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with { DeferredCardId = value });
    }

    public int NativeEventGold
    {
        get => Stochastic.RandomOutcomes.NativeEventGold;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with { NativeEventGold = value });
    }

    public int NativeEventSecondaryGold
    {
        get => Stochastic.RandomOutcomes.NativeEventSecondaryGold;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with
            { NativeEventSecondaryGold = value });
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? NativeEventPotionSlot
    {
        get => Stochastic.RandomOutcomes.NativeEventPotionSlot;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with
            { NativeEventPotionSlot = value });
    }

    public string? NativeDishId
    {
        get => Stochastic.RandomOutcomes.NativeDishId;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with { NativeDishId = value });
    }

    public string? NativeLastDishId
    {
        get => Stochastic.RandomOutcomes.NativeLastDishId;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with { NativeLastDishId = value });
    }

    public int NativeDishCount
    {
        get => Stochastic.RandomOutcomes.NativeDishCount;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with { NativeDishCount = value });
    }

    public bool NativeDishRollPending
    {
        get => Stochastic.RandomOutcomes.NativeDishRollPending;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with
            { NativeDishRollPending = value });
    }

    public string[]? RepyChestPotionIds
    {
        get => Stochastic.RandomOutcomes.RepyChestPotionIds;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with
            { RepyChestPotionIds = value });
    }

    public string[]? RepyChestRelicIds
    {
        get => Stochastic.RandomOutcomes.RepyChestRelicIds;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with
            { RepyChestRelicIds = value });
    }

    public string? NativeTinkerType
    {
        get => Stochastic.RandomOutcomes.NativeTinkerType;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with { NativeTinkerType = value });
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RngStreamState? NativeEventRng
    {
        get => Stochastic.NativeRng;
        init => Stochastic = EventStochasticStateV111.WithNativeRng(
            Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? NativeEventRngProviderMode
    {
        get => Stochastic.ProviderMode;
        init => Stochastic = EventStochasticStateV111.WithProviderMode(
            Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeControlledEventChoiceContinuationV111? ControlledChoice
    {
        get => Stochastic.ControlledChoice;
        init => Stochastic = EventStochasticStateV111.WithControlledChoice(
            Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeFuturePotionOffer[]? NativeFuturePotionOffers
    {
        get => Stochastic.RandomOutcomes.FuturePotionOffers;
        init => Stochastic = EventStochasticStateV111.WithCommittedRandom(
            Stochastic, Stochastic.RandomOutcomes with
            { FuturePotionOffers = value });
    }

    public string[] RemainingPotionIds =>
        QueuedPotionIds ?? Array.Empty<string>();

    public PrototypePendingEventDeckChoiceState[] RemainingDeckChoices =>
        QueuedDeckChoices ?? Array.Empty<PrototypePendingEventDeckChoiceState>();

    public EventState Fork() => this with
    {
        Mechanics = Mechanics.Fork(),
        Stochastic = Stochastic.Fork(),
    };
}

public sealed record PrototypeFuturePotionOffer(
    int Slot,
    string PotionId,
    PrototypeCardType CardType,
    PrototypeCardRarity CardRarity);

public sealed record PrototypeUnknownRoomOddsState(
    int MonsterWeight = 100,
    int TreasureWeight = 20,
    int ShopWeight = 30)
{
    public const int Scale = 1000;

    public PrototypeUnknownRoomOddsState After(PrototypeRoomType rolled) =>
        this with
        {
            MonsterWeight = rolled == PrototypeRoomType.Combat
                ? 100 : MonsterWeight + 100,
            TreasureWeight = rolled == PrototypeRoomType.Treasure
                ? 20 : TreasureWeight + 20,
            ShopWeight = rolled == PrototypeRoomType.Shop
                ? 30 : ShopWeight + 30
        };
}

public sealed record PrototypeCustomRewardReturnState(
    RunPhase Phase,
    RewardState? Reward,
    PrototypeCustomRewardReturnState? Parent = null)
{
    public PrototypeCustomRewardReturnState Fork() => this with
    { Reward = Reward?.Fork(), Parent = Parent?.Fork() };
}

public sealed record RunWorldState(
    string RulesetId,
    string CharacterId,
    int Act,
    int Floor,
    long NextCardInstanceId,
    PrototypeRoomType? ActiveRoom,
    MapState Map,
    CombatState? Combat,
    RewardState? Reward,
    ShopState? Shop,
    EventState? Event,
    string? TerminalOutcome,
    string[]? EncounterHistory = null,
    string[]? EventHistory = null,
    PrototypeActOneRegion? ActOneRegion = null,
    PrototypeActOneEncounterPoolState? ActOneEncounterPool = null,
    PrototypeCompletedRoomRecord[]? CompletedRoomHistory = null,
    PrototypeUnknownRoomOddsState? UnknownRoomOdds = null,
    int ShopRemovalsUsed = 0,
    PrototypeRelicBagState? RelicBag = null,
    int PotionRewardOddsThousandths =
        PrototypeNativePotionRewardOdds.InitialThousandths,
    int CardRarityOffsetBasisPoints =
        PrototypeNativeCardRarityOdds.InitialOffsetBasisPoints,
    bool NativeOvergrowthOpening = false,
    ActIdentity? ActIdentity = null,
    // Native IRunState.ExtraFields.FreedRepy, set by unlocking Repy's cage.
    bool FreedRepy = false,
    PrototypeLaterActEncounterBagV111? LaterActEncounterBag = null,
    PrototypeLaterActEncounterBagV111[]? PreparedLaterActEncounterBags = null,
    long? LastCombatEndDecisionIndex = null,
    PrototypeCustomRewardReturnState? CustomRewardReturn = null)
{
    /// <summary>
    /// Atomic mechanics/provider envelope for persistent run-world state.
    /// Existing snapshot fields remain flattened projections.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<RunWorldMechanicsStateV111,
        RunWorldStochasticStateV111> Simulation { get; init; } = new(
            new RunWorldMechanicsStateV111(
                RulesetId, CharacterId, Act, Floor, NextCardInstanceId,
                ActiveRoom, Map, Combat, Reward, Shop, Event, TerminalOutcome,
                EncounterHistory, EventHistory, ActOneRegion,
                CompletedRoomHistory, UnknownRoomOdds, ShopRemovalsUsed,
                PotionRewardOddsThousandths, CardRarityOffsetBasisPoints,
                NativeOvergrowthOpening, ActIdentity, FreedRepy,
                LastCombatEndDecisionIndex, CustomRewardReturn),
            new RunWorldStochasticStateV111(
                ActOneEncounterPool, RelicBag, LaterActEncounterBag,
                PreparedLaterActEncounterBags));

    [System.Text.Json.Serialization.JsonIgnore]
    public RunWorldMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public RunWorldStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public string RulesetId
    {
        get => Mechanics.RulesetId;
        init => Mechanics = Mechanics with { RulesetId = value };
    }

    public string CharacterId
    {
        get => Mechanics.CharacterId;
        init => Mechanics = Mechanics with { CharacterId = value };
    }

    public int Act
    {
        get => Mechanics.Act;
        init => Mechanics = Mechanics with { Act = value };
    }

    public int Floor
    {
        get => Mechanics.Floor;
        init => Mechanics = Mechanics with { Floor = value };
    }

    public long NextCardInstanceId
    {
        get => Mechanics.NextCardInstanceId;
        init => Mechanics = Mechanics with { NextCardInstanceId = value };
    }

    public PrototypeRoomType? ActiveRoom
    {
        get => Mechanics.ActiveRoom;
        init => Mechanics = Mechanics with { ActiveRoom = value };
    }

    public MapState Map
    {
        get => Mechanics.Map;
        init => Mechanics = Mechanics with { Map = value };
    }

    public CombatState? Combat
    {
        get => Mechanics.Combat;
        init => Mechanics = Mechanics with { Combat = value };
    }

    public RewardState? Reward
    {
        get => Mechanics.Reward;
        init => Mechanics = Mechanics with { Reward = value };
    }

    public ShopState? Shop
    {
        get => Mechanics.Shop;
        init => Mechanics = Mechanics with { Shop = value };
    }

    public EventState? Event
    {
        get => Mechanics.Event;
        init => Mechanics = Mechanics with { Event = value };
    }

    public string? TerminalOutcome
    {
        get => Mechanics.TerminalOutcome;
        init => Mechanics = Mechanics with { TerminalOutcome = value };
    }

    public string[]? EncounterHistory
    {
        get => Mechanics.EncounterHistory;
        init => Mechanics = Mechanics with { EncounterHistory = value };
    }

    internal RunWorldState AppendEncounterId(string encounterId) => this with
    {
        Mechanics = Mechanics.AppendEncounterId(encounterId)
    };

    public string[]? EventHistory
    {
        get => Mechanics.EventHistory;
        init => Mechanics = Mechanics with { EventHistory = value };
    }

    internal RunWorldState AppendEventId(string eventId) => this with
    {
        Mechanics = Mechanics.AppendEventId(eventId)
    };

    public PrototypeActOneRegion? ActOneRegion
    {
        get => Mechanics.ActOneRegion;
        init => Mechanics = Mechanics with { ActOneRegion = value };
    }

    public PrototypeCompletedRoomRecord[]? CompletedRoomHistory
    {
        get => Mechanics.CompletedRoomHistory;
        init => Mechanics = Mechanics with { CompletedRoomHistory = value };
    }

    internal RunWorldState AppendCompletedRoom(
        PrototypeCompletedRoomRecord completedRoom) => this with
    {
        Mechanics = Mechanics.AppendCompletedRoom(completedRoom)
    };

    public PrototypeUnknownRoomOddsState? UnknownRoomOdds
    {
        get => Mechanics.UnknownRoomOdds;
        init => Mechanics = Mechanics with { UnknownRoomOdds = value };
    }

    public int ShopRemovalsUsed
    {
        get => Mechanics.ShopRemovalsUsed;
        init => Mechanics = Mechanics with { ShopRemovalsUsed = value };
    }

    public int PotionRewardOddsThousandths
    {
        get => Mechanics.PotionRewardOddsThousandths;
        init => Mechanics = Mechanics with
        { PotionRewardOddsThousandths = value };
    }

    public int CardRarityOffsetBasisPoints
    {
        get => Mechanics.CardRarityOffsetBasisPoints;
        init => Mechanics = Mechanics with
        { CardRarityOffsetBasisPoints = value };
    }

    public bool NativeOvergrowthOpening
    {
        get => Mechanics.NativeOvergrowthOpening;
        init => Mechanics = Mechanics with
        { NativeOvergrowthOpening = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ActIdentity? ActIdentity
    {
        get => Mechanics.ActIdentity;
        init => Mechanics = Mechanics with { ActIdentity = value };
    }

    public bool FreedRepy
    {
        get => Mechanics.FreedRepy;
        init => Mechanics = Mechanics with { FreedRepy = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? LastCombatEndDecisionIndex
    {
        get => Mechanics.LastCombatEndDecisionIndex;
        init => Mechanics = Mechanics with
        { LastCombatEndDecisionIndex = value };
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeCustomRewardReturnState? CustomRewardReturn
    {
        get => Mechanics.CustomRewardReturn;
        init => Mechanics = Mechanics with { CustomRewardReturn = value };
    }

    public PrototypeActOneEncounterPoolState? ActOneEncounterPool
    {
        get => Stochastic.ActOneEncounterPool;
        init => Stochastic =
            RunWorldStochasticStateV111.WithActOneEncounterPool(
                Stochastic, value);
    }

    public PrototypeRelicBagState? RelicBag
    {
        get => Stochastic.RelicBag;
        init => Stochastic = RunWorldStochasticStateV111.WithRelicBag(
            Stochastic, value);
    }

    public PrototypeLaterActEncounterBagV111? LaterActEncounterBag
    {
        get => Stochastic.LaterActEncounterBag;
        init => Stochastic =
            RunWorldStochasticStateV111.WithLaterActEncounterBag(
                Stochastic, value);
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PrototypeLaterActEncounterBagV111[]?
        PreparedLaterActEncounterBags
    {
        get => Stochastic.PreparedLaterActEncounterBags;
        init => Stochastic =
            RunWorldStochasticStateV111.WithPreparedLaterActEncounterBags(
                Stochastic, value);
    }

    public RunWorldState Fork() => this with
    {
        Map = Map.Fork(),
        Combat = Combat?.Fork(),
        Reward = Reward?.Fork(),
        CustomRewardReturn = CustomRewardReturn?.Fork(),
        Shop = Shop?.Fork(),
        Event = Event?.Fork(),
        EncounterHistory = EncounterHistory is null
            ? null
            : (string[])EncounterHistory.Clone(),
        EventHistory = EventHistory is null
            ? null
            : (string[])EventHistory.Clone(),
        CompletedRoomHistory = CompletedRoomHistory is null
            ? null
            : (PrototypeCompletedRoomRecord[])CompletedRoomHistory.Clone(),
        Stochastic = Stochastic.Fork()
    };

    public string[] EncounterIds =>
        EncounterHistory ?? Array.Empty<string>();

    public string[] EventIds =>
        EventHistory ?? Array.Empty<string>();

    public PrototypeCompletedRoomRecord[] CompletedRooms =>
        CompletedRoomHistory ?? Array.Empty<PrototypeCompletedRoomRecord>();
}

public sealed record ChooseMapNodePayload(string NodeId);
public sealed record PlayCardPayload(long CardInstanceId, int? TargetEnemyId);
public sealed record UsePotionPayload(int Slot, int? TargetEnemyId);
public sealed record ChooseCardPayload(int Index);
public sealed record ChooseRewardCardGroupPayload(int GroupIndex, int Index);
public sealed record RerollRewardCardPayload(int GroupIndex);
public sealed record ChooseBundlePayload(int Index);
public sealed record ChooseRelicPayload(int Index);
public sealed record ChooseGoldRewardPayload(int GroupIndex);
public sealed record ChooseDeckCardPayload(long CardInstanceId);
public sealed record ReplaceRewardPotionPayload(int Slot);

public sealed record ChooseExtraPotionRewardPayload(int GroupIndex, int Slot = -1);
public sealed record ReplaceShopPotionPayload(int OfferId, int Slot);
public sealed record BuyOfferPayload(int OfferId);
public sealed record EventChoicePayload(string ChoiceId);
public sealed record ChooseControlledEventOutcomePayload(int SelectedIndex);
public sealed record TradeFuturePotionPayload(int Slot);
public sealed record ChooseEventDeckCardPayload(long CardInstanceId);
public sealed record ReplaceEventPotionPayload(int Slot);
public sealed record UpgradeCardPayload(long CardInstanceId);
public sealed record RemoveCardPayload(long CardInstanceId);
public sealed record SelectCardsPayload
{
    private long[] _cardInstanceIds;

    [JsonConstructor]
    public SelectCardsPayload(long[] CardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(CardInstanceIds);
        _cardInstanceIds = (long[])CardInstanceIds.Clone();
    }

    public long[] CardInstanceIds
    {
        get => (long[])_cardInstanceIds.Clone();
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _cardInstanceIds = (long[])value.Clone();
        }
    }

    public void Deconstruct(out long[] CardInstanceIds) =>
        CardInstanceIds = this.CardInstanceIds;
}
public sealed record ChooseKnowledgeCursePayload(string CardId);

public static class PrototypeJson
{
    public static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
}
