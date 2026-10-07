using System.Text.Json;

namespace Sts2Emulator.Core;

public enum PrototypeRoomType
{
    Combat,
    Elite,
    Event,
    Shop,
    Rest,
    Boss
}

public enum PrototypeActOneRegion
{
    Overgrowth,
    Underdocks
}

public sealed record PrototypeActOneEncounterPoolState(
    PrototypeActOneRegion Region,
    int OrdinaryCombatsStarted,
    string[] RemainingWeakEncounterIds,
    string[]? RemainingNormalEncounterIds = null,
    string[]? RemainingEliteEncounterIds = null,
    string? BossEncounterId = null)
{
    public PrototypeActOneEncounterPoolState Fork() => this with
    {
        RemainingWeakEncounterIds =
            (string[])RemainingWeakEncounterIds.Clone(),
        RemainingNormalEncounterIds =
            RemainingNormalEncounterIds is null
                ? null
                : (string[])RemainingNormalEncounterIds.Clone(),
        RemainingEliteEncounterIds =
            RemainingEliteEncounterIds is null
                ? null
                : (string[])RemainingEliteEncounterIds.Clone()
    };
}

public enum PrototypeCardRarity
{
    Basic,
    Common,
    Uncommon,
    Rare,
    Ancient,
    Curse,
    Status,
    Token
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
    RemoveEnemyBlock,
    RemoveEnemyPower,
    TriggerEnemyStatus,
    GainPlayerBlock,
    DrawCards,
    ApplyEnemyStatus,
    ChooseCards,
    ChooseGeneratedCards,
    CreateCardsInHand,
    CreateCardsInHandFromPowerCardPayload,
    AutoPlayTaggedCardsFromZone,
    ApplyPlayerPower,
    ApplyEnemyPower,
    HealPlayer,
    UpgradeHandCards,
    DamagePlayer,
    GainEnergy,
    MultiplyEnemyStatus,
    GainPlayerBlockAndApplyPowerFromActualGain,
    ModifySourceCardEnergyCost,
    SetHandCardsEnergyCostUntilTurnEndOrPlayed,
    ModifyEventSourceCardKeyword,
    GainPlayerBlockFromEnemyStatusTotal
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
    int SuppressedUpgradeLevels = 0)
{
    public PrototypeCombatCardSnapshot Fork() => this with
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

public enum PrototypeCardZone
{
    Hand,
    DrawPile,
    DiscardPile,
    ExhaustPile,
    ChoicePool
}

public enum PrototypeCardSelectionResolutionKind
{
    Preserve,
    MoveToHand,
    MoveToDiscard,
    MoveToExhaust
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
    TargetHasStatus
}

public enum PrototypeCombatCountKind
{
    SkillsInHand,
    SkillsPlayedThisTurn,
    AttacksPlayedThisTurn,
    CardsDiscardedThisTurn,
    CardsDrawnThisCombat,
    OtherCardsInHand
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
    BeforeHandDraw,
    CardPlayed,
    CardDrawn,
    CardDiscarded,
    CardExhausted,
    EnemyDamaged,
    EnemyDefeated
}

public sealed record PrototypeCombatEvent(
    PrototypeCombatEventKind Kind,
    long? SourceCardInstanceId = null,
    string? CardId = null,
    int? TargetEnemyId = null,
    int Amount = 0,
    long? PowerApplicationOrderCeiling = null,
    bool FromHandDraw = false);

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
    int EveryNth = 1)
{
    public PrototypeEventSubscriberState Fork() => this with
    {
        Effects = (PrototypeCombatEffectSpec[])Effects.Clone(),
        PowerCardPayload = PowerCardPayload?.Fork()
    };
}

public sealed record PrototypeEventDispatchContinuationState(
    PrototypeCombatEvent CombatEvent,
    PrototypeEventSubscriberState CurrentSubscriber,
    PrototypeEventSubscriberState[] RemainingSubscribers,
    int EventDepth)
{
    public PrototypeEventDispatchContinuationState Fork() => this with
    {
        CombatEvent = CombatEvent with { },
        CurrentSubscriber = CurrentSubscriber.Fork(),
        RemainingSubscribers = RemainingSubscribers
            .Select(item => item.Fork())
            .ToArray()
    };
}

public sealed record PrototypeAutomaticPipelineContinuationState(
    int NextStepIndex,
    PrototypeCombatEventKind? PendingPostDispatchEventKind = null);

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
    DrawPlayerHand
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
    LoseHp,
    GainGold,
    AddCard,
    FillPotionSlots
}

public enum PrototypeRunEventKind
{
    RelicAcquired,
    ShopEntered,
    CardAdded
}

public sealed record PrototypeCardSelectionSpec(
    PrototypeCardZone SourceZone,
    int MinSelections,
    int MaxSelections,
    PrototypeCardSelectionResolutionKind Resolution,
    PrototypeCardType? RequiredCardType = null,
    int SelectionsPerPowerStack = 0,
    bool RemoveUnselectedFromSource = false);

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
    PrototypeTemporaryCardCost? SelectedCardTemporaryCost = null)
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
    PrototypeTemporaryCardCost? SelectedCardTemporaryCost = null);

public sealed record PrototypeRunEffectSpec(
    PrototypeRunEffectKind Kind,
    int Amount = 0,
    string? CardId = null);

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
    bool MechanicsImplemented = true,
    bool MultiplayerOnly = false,
    bool RetainOnUpgrade = false,
    bool CanBeGeneratedInCombat = true);

public sealed record PrototypePotionDefinition(
    string Id,
    string Name,
    PrototypeCardTarget Target,
    PrototypeCombatEffectSpec[] Effects,
    bool UsableOutsideCombat = false,
    PrototypeRunEffectSpec[]? RunEffects = null);

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
    int? MaxAttacksPlayedLastTurn = null);

public sealed record PrototypeRelicRunTriggerSpec(
    PrototypeRunEventKind EventKind,
    PrototypeRunEffectSpec[] Effects,
    PrototypeCardType? RequiredCardType = null);

public sealed record PrototypeRelicDefinition(
    string Id,
    string Name,
    int FirstTurnDrawBonus = 0,
    int EnergyPerTurnBonus = 0,
    PrototypeRelicTriggerSpec[]? Triggers = null,
    PrototypeRelicRunTriggerSpec[]? RunTriggers = null,
    PrototypeCardType? UpgradeAddedCardType = null,
    bool PreserveUnusedEnergy = false);

public sealed record PrototypePowerTriggerSpec(
    PrototypeCombatEventKind EventKind,
    PrototypeCombatEffectSpec[] Effects,
    bool RequiresOwnerTarget = false,
    bool ExcludeHandDraw = false,
    bool RequiresPlayerTurn = false,
    bool RemoveSourcePowerAfterTrigger = false,
    PrototypeCardType? RequiredSourceCardType = null);

public sealed record PrototypePowerDefinition(
    string Id,
    string Name,
    int BlockBonusPerStack,
    PrototypePowerTriggerSpec[] Triggers,
    int AttackRetaliationPerStack = 0,
    bool AllowNegative = false,
    bool RemoveAtPlayerTurnEnd = false,
    int BlockAfterClearPerStack = 0,
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
    bool OwnerDeathTriggersFatal = true);

public sealed record PrototypePowerInstanceState(
    string PowerId,
    int Stacks,
    long ApplicationOrder,
    PrototypeCombatCardSnapshot? CardPayload = null,
    int? SourceEnemyInstanceId = null)
{
    public PrototypePowerInstanceState Fork() => this with
    {
        CardPayload = CardPayload?.Fork()
    };
}

public enum PrototypeEnemyEffectKind
{
    DamagePlayer,
    GainBlock,
    ApplyPlayerPower,
    ApplyEnemyPower,
    AddCardsToDiscard,
    SummonEnemy
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
    string? EnemyId = null)
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
    SlotNameEquals
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
    int MaxTimes = 0);

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
    string? SlotName = null);

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
    PrototypeEnemyDeathSummonSpec[]? DeathSummons = null)
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
    PrototypeEncounterFormationVariant[]? FormationVariants = null)
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

    public PrototypeEncounterEnemySpec[] ResolveEnemySpecs(
        RngBundle rng)
    {
        PrototypeEncounterFormationVariant? variant = null;
        if (FormationVariants is { Length: > 0 } variants)
        {
            var variantIndex = variants.Length == 1
                ? 0
                : PrototypeRng.NextInt(
                    rng,
                    "combat",
                    variants.Length);
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
                    : PrototypeRng.NextInt(
                        rng,
                        "combat",
                        candidates.Count);
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
                    : PrototypeRng.NextInt(
                        rng,
                        "combat",
                        candidates.Count);
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

public sealed record PrototypeEventChoiceDefinition(
    string Id,
    string Label,
    PrototypeRunEffectSpec[] Effects);

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
    bool AllowDuplicateSpecialRooms = false);

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
    PrototypeAutomaticStep[] EndTurnPipeline);

public sealed record MapNodeState(
    string NodeId,
    int Act,
    int Floor,
    PrototypeRoomType RoomType,
    string[]? NextNodeIds = null)
{
    public MapNodeState Fork() => this with
    {
        NextNodeIds = NextNodeIds is null ? null : (string[])NextNodeIds.Clone()
    };
}

public sealed record MapState(
    MapNodeState[] Nodes,
    string? CurrentNodeId = null,
    string[]? EntryNodeIds = null)
{
    public MapState Fork() => this with
    {
        Nodes = Nodes.Select(node => node.Fork()).ToArray(),
        EntryNodeIds = EntryNodeIds is null ? null : (string[])EntryNodeIds.Clone()
    };

    public MapNodeState[] AvailableNodes()
    {
        IEnumerable<string> ids;
        if (CurrentNodeId is null)
        {
            ids = EntryNodeIds ?? Array.Empty<string>();
        }
        else
        {
            var current = Nodes.FirstOrDefault(node =>
                StringComparer.Ordinal.Equals(node.NodeId, CurrentNodeId))
                ?? throw new InvalidOperationException($"Map current node '{CurrentNodeId}' is missing.");
            ids = current.NextNodeIds ?? Array.Empty<string>();
        }

        var byId = Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        return ids
            .Select(id => byId.TryGetValue(id, out var node)
                ? node
                : throw new InvalidOperationException($"Map edge targets missing node '{id}'."))
            .ToArray();
    }

    // Compatibility/readability alias for code that only needs the currently legal map choices.
    public MapNodeState[] Options => AvailableNodes();
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
    bool DeathEffectsResolved = false)
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
                StringComparer.Ordinal)
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
    int SuppressedUpgradeLevels = 0)
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
    int[] TriggerCounts)
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
    bool RemoveSourceCardOnCompletion = false)
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
    PrototypeChoiceResolutionContinuationState? Parent = null)
{
    public PrototypeChoiceResolutionContinuationState Fork() => this with
    {
        Operations = (PrototypeQueuedOperation[])Operations.Clone(),
        PendingDiscardEvents =
            (PrototypeCombatEvent[])PendingDiscardEvents.Clone(),
        PendingSlyCardInstanceIds =
            (long[])PendingSlyCardInstanceIds.Clone(),
        CompletionEvents =
            (PrototypeCombatEvent[])CompletionEvents.Clone(),
        EventDispatchContinuation =
            EventDispatchContinuation?.Fork(),
        Parent = Parent?.Fork()
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
    PrototypeEventDispatchContinuationState? EventDispatchContinuation = null,
    PrototypeChoiceResolutionContinuationState? OuterChoiceContinuation = null)
{
    public PendingCombatChoiceState Fork() => this with
    {
        CandidateCardInstanceIds = (long[])CandidateCardInstanceIds.Clone(),
        Continuation = (PrototypeQueuedOperation[])Continuation.Clone(),
        CompletionEvents = (PrototypeCombatEvent[])CompletionEvents.Clone(),
        SelectedCardTemporaryCost = SelectedCardTemporaryCost is null
            ? null
            : SelectedCardTemporaryCost with { },
        EventDispatchContinuation = EventDispatchContinuation?.Fork(),
        OuterChoiceContinuation = OuterChoiceContinuation?.Fork()
    };
}

public sealed record PrototypeCombatCounters(
    int CardsPlayedThisTurn = 0,
    int AttacksPlayedThisTurn = 0,
    int SkillsPlayedThisTurn = 0,
    int CardsDiscardedThisTurn = 0,
    int CardsDrawnThisCombat = 0,
    string[]? PlayedCardTagsThisTurn = null,
    int AttacksPlayedLastTurn = 0)
{
    public string[] PlayedTags =>
        PlayedCardTagsThisTurn ?? Array.Empty<string>();

    public int PlaysWithTag(string tag) =>
        PlayedTags.Count(item =>
            StringComparer.Ordinal.Equals(item, tag));

    public PrototypeCombatCounters Fork() => this with
    {
        PlayedCardTagsThisTurn =
            PlayedCardTagsThisTurn is null
                ? null
                : (string[])PlayedCardTagsThisTurn.Clone()
    };
}

public sealed record CombatState(
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
    long[]? ChoicePool = null)
{
    public CombatState Fork() => this with
    {
        Hand = (long[])Hand.Clone(),
        DrawPile = (long[])DrawPile.Clone(),
        DiscardPile = (long[])DiscardPile.Clone(),
        ExhaustPile = (long[])ExhaustPile.Clone(),
        Enemies = Enemies.Select(enemy => enemy.Fork()).ToArray(),
        Cards = Cards.Select(card => card.Fork()).ToArray(),
        PlayerPowers = PlayerPowers.Select(power => power.Fork()).ToArray(),
        Relics = Relics is null
            ? null
            : Relics.Select(relic => relic.Fork()).ToArray(),
        Potions = Potions is null
            ? null
            : Potions.Select(potion => potion.Fork()).ToArray(),
        ChoicePool = ChoicePool is null
            ? null
            : (long[])ChoicePool.Clone(),
        PendingChoice = PendingChoice?.Fork(),
        Counters = Counters?.Fork(),
        AutomaticPipelineContinuation =
            AutomaticPipelineContinuation is null
                ? null
                : AutomaticPipelineContinuation with { }
    };

    public CombatRelicState[] RelicStates =>
        Relics ?? Array.Empty<CombatRelicState>();

    public CombatPotionState[] PotionStates =>
        Potions ?? Array.Empty<CombatPotionState>();

    public long[] ChoiceCardIds =>
        ChoicePool ?? Array.Empty<long>();

    public PrototypeCombatCounters CounterState =>
        Counters ?? new PrototypeCombatCounters();
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
    int ExtraCardRewardsResolved = 0)
{
    public RewardState Fork() => this with
    {
        CardOptions = (string[])CardOptions.Clone(),
        ExtraCardOptions = ExtraCardOptions is null
            ? null
            : ExtraCardOptions
                .Select(group => (string[])group.Clone())
                .ToArray()
    };
}

public sealed record ShopOffer(
    int OfferId,
    string ItemId,
    int Price,
    bool Sold);

public sealed record ShopState(
    ShopOffer[] CardOffers,
    ShopOffer? PotionOffer,
    ShopOffer? RelicOffer,
    int RemovalPrice = 75,
    bool RemovalUsed = false)
{
    public ShopState Fork() => this with
    {
        CardOffers = (ShopOffer[])CardOffers.Clone(),
        PotionOffer = PotionOffer is null ? null : PotionOffer with { },
        RelicOffer = RelicOffer is null ? null : RelicOffer with { }
    };
}

public sealed record EventState(string EventId);

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
    PrototypeActOneEncounterPoolState? ActOneEncounterPool = null)
{
    public RunWorldState Fork() => this with
    {
        Map = Map.Fork(),
        Combat = Combat?.Fork(),
        Reward = Reward?.Fork(),
        Shop = Shop?.Fork(),
        Event = Event is null ? null : Event with { },
        EncounterHistory = EncounterHistory is null
            ? null
            : (string[])EncounterHistory.Clone(),
        EventHistory = EventHistory is null
            ? null
            : (string[])EventHistory.Clone(),
        ActOneEncounterPool = ActOneEncounterPool?.Fork()
    };

    public string[] EncounterIds =>
        EncounterHistory ?? Array.Empty<string>();

    public string[] EventIds =>
        EventHistory ?? Array.Empty<string>();
}

public sealed record ChooseMapNodePayload(string NodeId);
public sealed record PlayCardPayload(long CardInstanceId, int? TargetEnemyId);
public sealed record UsePotionPayload(int Slot, int? TargetEnemyId);
public sealed record ChooseCardPayload(int Index);
public sealed record BuyOfferPayload(int OfferId);
public sealed record EventChoicePayload(string ChoiceId);
public sealed record UpgradeCardPayload(long CardInstanceId);
public sealed record RemoveCardPayload(long CardInstanceId);
public sealed record SelectCardsPayload(long[] CardInstanceIds);

public static class PrototypeJson
{
    public static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
}
