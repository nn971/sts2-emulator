namespace Sts2Emulator.Core;

/// <summary>
/// Committed run-world stochastic realizations whose ordering persists across
/// room boundaries. Their existing RunWorldState fields remain the snapshot
/// wire projection during migration.
/// </summary>
public sealed record RunWorldStochasticStateV111(
    PrototypeActOneEncounterPoolState? ActOneEncounterPool = null,
    PrototypeRelicBagState? RelicBag = null,
    PrototypeLaterActEncounterBagV111? LaterActEncounterBag = null,
    PrototypeLaterActEncounterBagV111[]? PreparedLaterActEncounterBags = null)
{
    public RunWorldStochasticStateV111 Fork() => this with
    {
        ActOneEncounterPool = ActOneEncounterPool?.Fork(),
        RelicBag = RelicBag?.Fork(),
        LaterActEncounterBag = LaterActEncounterBag?.Fork(),
        PreparedLaterActEncounterBags = PreparedLaterActEncounterBags?
            .Select(bag => bag.Fork()).ToArray()
    };

    internal static RunWorldStochasticStateV111 WithActOneEncounterPool(
        RunWorldStochasticStateV111? current,
        PrototypeActOneEncounterPoolState? pool) =>
        (current ?? new RunWorldStochasticStateV111()) with
        {
            ActOneEncounterPool = pool
        };

    internal static RunWorldStochasticStateV111 WithRelicBag(
        RunWorldStochasticStateV111? current,
        PrototypeRelicBagState? bag) =>
        (current ?? new RunWorldStochasticStateV111()) with
        {
            RelicBag = bag
        };

    internal static RunWorldStochasticStateV111 WithLaterActEncounterBag(
        RunWorldStochasticStateV111? current,
        PrototypeLaterActEncounterBagV111? bag) =>
        (current ?? new RunWorldStochasticStateV111()) with
        {
            LaterActEncounterBag = bag
        };

    internal static RunWorldStochasticStateV111 WithPreparedLaterActEncounterBags(
        RunWorldStochasticStateV111? current,
        PrototypeLaterActEncounterBagV111[]? bags) =>
        (current ?? new RunWorldStochasticStateV111()) with
        {
            PreparedLaterActEncounterBags = bags
        };
}
