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
    private string[]? _encounterHistory = Clone(EncounterHistory);
    private string[]? _eventHistory = Clone(EventHistory);
    private PrototypeCompletedRoomRecord[]? _completedRoomHistory =
        Clone(CompletedRoomHistory);

    public string[]? EncounterHistory
    {
        get => Clone(_encounterHistory);
        init => _encounterHistory = Clone(value);
    }

    public string[]? EventHistory
    {
        get => Clone(_eventHistory);
        init => _eventHistory = Clone(value);
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
        get => Clone(_completedRoomHistory);
        init => _completedRoomHistory = Clone(value);
    }

    private static T[]? Clone<T>(T[]? values) =>
        values is null ? null : (T[])values.Clone();
}
