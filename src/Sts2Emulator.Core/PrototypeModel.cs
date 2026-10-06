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
    ApplyEnemyStatus
}

public enum PrototypeTurnStage
{
    EnemyTurnStart,
    EnemyAction,
    EnemyTurnEnd,
    PlayerTurnStart
}

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

public sealed record PrototypeCombatEffectSpec(
    PrototypeCombatEffectKind Kind,
    int Amount,
    int UpgradeDelta = 0,
    string? StatusId = null)
{
    public int AmountAtUpgrade(int upgradeLevel) => Amount + (UpgradeDelta * upgradeLevel);
}

public sealed record PrototypeRunEffectSpec(
    PrototypeRunEffectKind Kind,
    int Amount = 0,
    string? CardId = null);

public sealed record PrototypeCardDefinition(
    string Id,
    string Name,
    int Cost,
    PrototypeCardTarget Target,
    PrototypeCombatEffectSpec[] Effects);

public sealed record PrototypePotionDefinition(
    string Id,
    string Name,
    PrototypeCardTarget Target,
    PrototypeCombatEffectSpec[] Effects);

public sealed record PrototypeRelicDefinition(
    string Id,
    string Name,
    int FirstTurnDrawBonus = 0,
    int EnergyPerTurnBonus = 0);

public sealed record PrototypeEnemyMoveDefinition(
    string Id,
    int Damage,
    int DamagePerAct = 0);

public sealed record PrototypeEnemyDefinition(
    string Id,
    string Name,
    int MaxHp,
    int HpPerAct,
    PrototypeEnemyMoveDefinition[] Moves);

public sealed record PrototypeEncounterDefinition(
    string Id,
    PrototypeRoomType RoomType,
    string[] EnemyIds);

public sealed record PrototypeStatusDefinition(
    string Id,
    PrototypeTurnStage? TriggerStage = null,
    PrototypeStatusTriggerKind TriggerKind = PrototypeStatusTriggerKind.None,
    int DecayOnTrigger = 0,
    PrototypeTurnStage? DecayStage = null,
    int DecayAtStage = 0,
    int OutgoingDamageNumerator = 1,
    int OutgoingDamageDenominator = 1);

public sealed record PrototypeEventChoiceDefinition(
    string Id,
    string Label,
    PrototypeRunEffectSpec[] Effects);

public sealed record PrototypeEventDefinition(
    string Id,
    string Name,
    PrototypeEventChoiceDefinition[] Choices);

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
    PrototypeRoomType[] RoomPool,
    PrototypeTurnStage[] EnemyTurnSequence);

public sealed record MapNodeState(
    string NodeId,
    int Act,
    int Floor,
    PrototypeRoomType RoomType);

public sealed record MapState(MapNodeState[] Options)
{
    public MapState Fork() => this with { Options = (MapNodeState[])Options.Clone() };
}

public sealed record EnemyCombatState(
    int InstanceId,
    string EnemyId,
    int Hp,
    int Block,
    int MoveIndex,
    Dictionary<string, int> Statuses)
{
    public EnemyCombatState Fork() => this with
    {
        Statuses = new Dictionary<string, int>(Statuses, StringComparer.Ordinal)
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
    EnemyCombatState[] Enemies)
{
    public CombatState Fork() => this with
    {
        Hand = (long[])Hand.Clone(),
        DrawPile = (long[])DrawPile.Clone(),
        DiscardPile = (long[])DiscardPile.Clone(),
        ExhaustPile = (long[])ExhaustPile.Clone(),
        Enemies = Enemies.Select(enemy => enemy.Fork()).ToArray()
    };
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
    ShopOffer? RelicOffer)
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
    string? TerminalOutcome)
{
    public RunWorldState Fork() => this with
    {
        Map = Map.Fork(),
        Combat = Combat?.Fork(),
        Reward = Reward?.Fork(),
        Shop = Shop?.Fork(),
        Event = Event is null ? null : Event with { }
    };
}

public sealed record ChooseMapNodePayload(string NodeId);
public sealed record PlayCardPayload(long CardInstanceId, int? TargetEnemyId);
public sealed record UsePotionPayload(int Slot, int? TargetEnemyId);
public sealed record ChooseCardPayload(int Index);
public sealed record BuyOfferPayload(int OfferId);
public sealed record EventChoicePayload(string ChoiceId);
public sealed record UpgradeCardPayload(long CardInstanceId);

public static class PrototypeJson
{
    public static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
}
