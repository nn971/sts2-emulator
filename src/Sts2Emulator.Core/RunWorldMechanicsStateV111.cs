namespace Sts2Emulator.Core;

/// <summary>
/// Persistent world mechanics for one run, excluding provider-committed bag
/// orders held by <see cref="RunWorldStochasticStateV111"/>.
/// </summary>
public sealed record RunWorldMechanicsStateV111(
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
    PrototypeCompletedRoomRecord[]? CompletedRoomHistory = null,
    PrototypeUnknownRoomOddsState? UnknownRoomOdds = null,
    int ShopRemovalsUsed = 0,
    int PotionRewardOddsThousandths =
        PrototypeNativePotionRewardOdds.InitialThousandths,
    int CardRarityOffsetBasisPoints =
        PrototypeNativeCardRarityOdds.InitialOffsetBasisPoints,
    bool NativeOvergrowthOpening = false,
    ActIdentity? ActIdentity = null,
    bool FreedRepy = false,
    long? LastCombatEndDecisionIndex = null,
    PrototypeCustomRewardReturnState? CustomRewardReturn = null,
    int? DefeatRemainingEnemyHp = null);
