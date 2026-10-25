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
    PrototypeCustomRewardReturnState? CustomRewardReturn = null)
{
    private string[]? _encounterHistory = Copy(EncounterHistory);
    private string[]? _eventHistory = Copy(EventHistory);
    private PrototypeCompletedRoomRecord[]? _completedRoomHistory =
        Copy(CompletedRoomHistory);

    public string[]? EncounterHistory
    {
        get => Copy(_encounterHistory);
        init => _encounterHistory = Copy(value);
    }

    public string[]? EventHistory
    {
        get => Copy(_eventHistory);
        init => _eventHistory = Copy(value);
    }

    internal RunWorldMechanicsStateV111 AppendEventId(string eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        return this with
        {
            EventHistory = (_eventHistory ?? []).Append(eventId).ToArray()
        };
    }

    internal RunWorldMechanicsStateV111 AppendEncounterId(
        string encounterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        return this with
        {
            EncounterHistory = (_encounterHistory ?? [])
                .Append(encounterId).ToArray()
        };
    }

    internal RunWorldMechanicsStateV111 AppendCompletedRoom(
        PrototypeCompletedRoomRecord completedRoom)
    {
        ArgumentNullException.ThrowIfNull(completedRoom);
        return this with
        {
            CompletedRoomHistory = (_completedRoomHistory ?? [])
                .Append(completedRoom).ToArray()
        };
    }

    public PrototypeCompletedRoomRecord[]? CompletedRoomHistory
    {
        get => Copy(_completedRoomHistory);
        init => _completedRoomHistory = Copy(value);
    }

    private static T[]? Copy<T>(T[]? values) =>
        values is null ? null : (T[])values.Clone();
}
