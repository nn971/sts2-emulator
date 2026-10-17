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

    public void Validate(RunWorldState owner, PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(player);
        ValidateActOneEncounterPool(owner);
        if (RelicBag is { } relicBag)
            PrototypeNativeRelicGrabBag.Validate(relicBag, player);
    }

    private void ValidateActOneEncounterPool(RunWorldState world)
    {
        var pool = ActOneEncounterPool;
        if (pool is null) return;
        if (world.ActOneRegion is { } region && region != pool.Region)
            throw new InvalidOperationException(
                "Act 1 encounter pool region disagrees with the run region.");

        var weakPool = pool.Region == PrototypeActOneRegion.Overgrowth
            ? PrototypeContent.OvergrowthWeakEncounterPool
            : PrototypeNativeUnderdocks.SupportedWeakEncounterIds;
        var normalPool = pool.Region == PrototypeActOneRegion.Overgrowth
            ? PrototypeContent.OvergrowthNormalEncounterPool
            : PrototypeNativeUnderdocks.SupportedNormalEncounterIds;
        var elitePool = pool.Region == PrototypeActOneRegion.Overgrowth
            ? PrototypeContent.OvergrowthEliteEncounterPool
            : PrototypeNativeUnderdocks.SupportedEliteEncounterIds;
        var bossPool = pool.Region == PrototypeActOneRegion.Overgrowth
            ? PrototypeContent.OvergrowthBossEncounterPool
            : PrototypeNativeUnderdocks.NativeBossEncounterIds;

        if (pool.OrdinaryCombatsStarted < 0 ||
            pool.OrdinaryCombatsStarted >
                PrototypeNativeOvergrowthMap.RoomRows)
            throw new InvalidOperationException(
                $"Invalid Act 1 ordinary-combat counter {pool.OrdinaryCombatsStarted}.");

        var weakStarted = Math.Min(3, pool.OrdinaryCombatsStarted);
        if (pool.RemainingWeakEncounterIds.Length !=
                weakPool.Length - weakStarted ||
            pool.RemainingWeakEncounterIds.Distinct(StringComparer.Ordinal)
                .Count() != pool.RemainingWeakEncounterIds.Length ||
            pool.RemainingWeakEncounterIds.Any(id =>
                !weakPool.Contains(id, StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Act 1 weak encounter bag is inconsistent with its region and combat counter.");

        if (pool.RemainingNormalEncounterIds is { } normal)
        {
            var normalStarted = Math.Max(0,
                pool.OrdinaryCombatsStarted - 3);
            var normalDrawsInCurrentBag = normalStarted % normalPool.Length;
            var expectedRemaining = normalDrawsInCurrentBag == 0
                ? normalPool.Length
                : normalPool.Length - normalDrawsInCurrentBag;
            if (normalStarted > 0 && normalDrawsInCurrentBag == 0)
                expectedRemaining = 0;
            if (normal.Length != expectedRemaining ||
                normal.Distinct(StringComparer.Ordinal).Count() !=
                    normal.Length ||
                normal.Any(id => !normalPool.Contains(
                    id, StringComparer.Ordinal)))
                throw new InvalidOperationException(
                    "Act 1 normal encounter bag is inconsistent with its region and combat counter.");
        }

        if (pool.RemainingEliteEncounterIds is { } elite &&
            (elite.Distinct(StringComparer.Ordinal).Count() != elite.Length ||
             elite.Any(id => !elitePool.Contains(
                 id, StringComparer.Ordinal))))
            throw new InvalidOperationException(
                "Act 1 elite encounter bag contains a foreign or duplicate encounter.");
        if (pool.BossEncounterId is null ||
            !bossPool.Contains(pool.BossEncounterId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Act 1 run has an invalid region-selected boss encounter.");
    }

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
