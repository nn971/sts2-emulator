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
    GainPlayerBlock,
    DrawCards,
    ApplyEnemyStatus,
    ChooseCards,
    CreateCardsInHand,
    CreateCardsInHandFromPowerCardPayload,
    ApplyPlayerPower,
    ApplyEnemyPower,
    DamagePlayer,
    GainEnergy,
    MultiplyEnemyStatus,
    GainPlayerBlockAndApplyPowerFromActualGain,
    ModifySourceCardEnergyCost,
    SetHandCardsEnergyCostUntilTurnEndOrPlayed
}

public enum PrototypeCardKeyword
{
    Retain,
    Sly,
    Ethereal
}

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
    PrototypeCardKeywordOverride[]? KeywordOverrides = null)
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
    ExhaustPile
}

public enum PrototypeCardSelectionResolutionKind
{
    Preserve,
    MoveToDiscard,
    MoveToExhaust
}

public enum PrototypeEffectTarget
{
    ActionTargetEnemy,
    AllEnemies,
    RandomEnemy
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

public enum PrototypeAutomaticStepKind
{
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
    LoseHp,
    GainGold,
    AddCard
}

public sealed record PrototypeCardSelectionSpec(
    PrototypeCardZone SourceZone,
    int MinSelections,
    int MaxSelections,
    PrototypeCardSelectionResolutionKind Resolution,
    PrototypeCardType? RequiredCardType = null);

public sealed record PrototypeSelectedCardPowerSpec(
    string PowerId,
    int Amount,
    int UpgradeDelta = 0)
{
    public int AmountAt(int upgradeLevel) =>
        Amount + (UpgradeDelta * upgradeLevel);
}

public sealed record PrototypeSelectedCardPowerAction(
    string PowerId,
    int Amount);

public sealed record PrototypeSelectedCardKeywordSpec(
    PrototypeCardKeyword Keyword,
    bool Enabled = true,
    PrototypeCardKeywordOverrideExpiry Expiry =
        PrototypeCardKeywordOverrideExpiry.EndOfTurn);

public sealed record PrototypeSelectedCardKeywordAction(
    PrototypeCardKeyword Keyword,
    bool Enabled,
    PrototypeCardKeywordOverrideExpiry Expiry);

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
    PrototypeSelectedCardKeywordSpec? SelectedCardKeyword = null)
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
    PrototypeSelectedCardKeywordAction? SelectedCardKeyword = null,
    PrototypeCombatCardSnapshot? PowerCardPayload = null);

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
    string[]? Tags = null);

public sealed record PrototypePotionDefinition(
    string Id,
    string Name,
    PrototypeCardTarget Target,
    PrototypeCombatEffectSpec[] Effects);

public sealed record PrototypeRelicTriggerSpec(
    PrototypeCombatEventKind EventKind,
    PrototypeCombatEffectSpec[] Effects,
    int EveryNth = 1);

public sealed record PrototypeRelicDefinition(
    string Id,
    string Name,
    int FirstTurnDrawBonus = 0,
    int EnergyPerTurnBonus = 0,
    PrototypeRelicTriggerSpec[]? Triggers = null);

public sealed record PrototypePowerTriggerSpec(
    PrototypeCombatEventKind EventKind,
    PrototypeCombatEffectSpec[] Effects,
    bool RequiresOwnerTarget = false,
    bool ExcludeHandDraw = false,
    bool RequiresPlayerTurn = false,
    bool RemoveSourcePowerAfterTrigger = false);

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
    PrototypeCardType? ReplayCardType = null,
    int AdditionalPlayCount = 0,
    bool ConsumeOnMatchingPlayCountModification = false,
    bool IsInstanced = false,
    bool RequiresCardPayload = false);

public sealed record PrototypePowerInstanceState(
    string PowerId,
    int Stacks,
    long ApplicationOrder,
    PrototypeCombatCardSnapshot? CardPayload = null)
{
    public PrototypePowerInstanceState Fork() => this with
    {
        CardPayload = CardPayload?.Fork()
    };
}

public enum PrototypeEnemyEffectKind
{
    DamagePlayer,
    GainBlock
}

public sealed record PrototypeEnemyEffectSpec(
    PrototypeEnemyEffectKind Kind,
    int Amount,
    int AmountPerAct = 0,
    int Repetitions = 1);

public sealed record PrototypeEnemyMoveDefinition(
    string Id,
    PrototypeEnemyEffectSpec[] Effects);

public sealed record PrototypeStartingPowerSpec(
    string PowerId,
    int Stacks);

public sealed record PrototypeEnemyDefinition(
    string Id,
    string Name,
    int MaxHp,
    int HpPerAct,
    PrototypeEnemyMoveDefinition[] Moves,
    PrototypeStartingPowerSpec[]? StartingPowers = null);

public sealed record PrototypeEncounterDefinition(
    string Id,
    PrototypeRoomType RoomType,
    string[] EnemyIds,
    int MinAct = 1,
    int MaxAct = int.MaxValue,
    int MinFloor = 1,
    int MaxFloor = int.MaxValue,
    int Weight = 1);

public sealed record PrototypeStatusDefinition(
    string Id,
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
    PrototypePowerInstanceState[]? Powers = null)
{
    public EnemyCombatState Fork() => this with
    {
        Statuses = new Dictionary<string, int>(Statuses, StringComparer.Ordinal),
        Powers = Powers is null
            ? null
            : Powers.Select(power => power.Fork()).ToArray()
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
    PrototypeCardKeywordOverride[]? KeywordOverrides = null)
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
    PrototypeSelectedCardPowerAction? SelectedCardPower = null,
    PrototypeSelectedCardKeywordAction? SelectedCardKeyword = null)
{
    public PendingCombatChoiceState Fork() => this with
    {
        CandidateCardInstanceIds = (long[])CandidateCardInstanceIds.Clone(),
        Continuation = (PrototypeQueuedOperation[])Continuation.Clone(),
        CompletionEvents = (PrototypeCombatEvent[])CompletionEvents.Clone()
    };
}

public sealed record PrototypeCombatCounters(
    int AttacksPlayedThisTurn = 0,
    int SkillsPlayedThisTurn = 0,
    int CardsDiscardedThisTurn = 0,
    int CardsDrawnThisCombat = 0);

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
    bool IsPlayerTurn = true)
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
        PendingChoice = PendingChoice?.Fork(),
        Counters = Counters is null ? null : Counters with { }
    };

    public CombatRelicState[] RelicStates =>
        Relics ?? Array.Empty<CombatRelicState>();

    public CombatPotionState[] PotionStates =>
        Potions ?? Array.Empty<CombatPotionState>();

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
    bool EndsAct)
{
    public RewardState Fork() => this with { CardOptions = (string[])CardOptions.Clone() };
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
    string[]? EventHistory = null)
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
            : (string[])EventHistory.Clone()
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
