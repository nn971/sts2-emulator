using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Committed run-world stochastic realizations whose ordering persists across
/// room boundaries. Their existing RunWorldState fields remain the snapshot
/// wire projection during migration.
/// </summary>
public sealed record RunWorldStochasticStateV111
{
    private PrototypeActOneEncounterPoolState? _actOneEncounterPool;
    private PrototypeRelicBagState? _relicBag;
    private PrototypeLaterActEncounterBagV111? _laterActEncounterBag;
    private PrototypeLaterActEncounterBagV111[]?
        _preparedLaterActEncounterBags;

    [JsonConstructor]
    public RunWorldStochasticStateV111(
        PrototypeActOneEncounterPoolState? ActOneEncounterPool = null,
        PrototypeRelicBagState? RelicBag = null,
        PrototypeLaterActEncounterBagV111? LaterActEncounterBag = null,
        PrototypeLaterActEncounterBagV111[]? PreparedLaterActEncounterBags = null)
    {
        _actOneEncounterPool = ActOneEncounterPool?.Fork();
        _relicBag = RelicBag?.Fork();
        _laterActEncounterBag = LaterActEncounterBag?.Fork();
        _preparedLaterActEncounterBags = CloneBags(
            PreparedLaterActEncounterBags);
    }

    public PrototypeActOneEncounterPoolState? ActOneEncounterPool
    {
        get => _actOneEncounterPool?.Fork();
        init => _actOneEncounterPool = value?.Fork();
    }
    public PrototypeRelicBagState? RelicBag
    {
        get => _relicBag?.Fork();
        init => _relicBag = value?.Fork();
    }
    public PrototypeLaterActEncounterBagV111? LaterActEncounterBag
    {
        get => _laterActEncounterBag?.Fork();
        init => _laterActEncounterBag = value?.Fork();
    }
    public PrototypeLaterActEncounterBagV111[]? PreparedLaterActEncounterBags
    {
        get => CloneBags(_preparedLaterActEncounterBags);
        init => _preparedLaterActEncounterBags = CloneBags(value);
    }

    public RunWorldStochasticStateV111 Fork() => this with
    {
        ActOneEncounterPool = _actOneEncounterPool?.Fork(),
        RelicBag = _relicBag?.Fork(),
        LaterActEncounterBag = _laterActEncounterBag?.Fork(),
        PreparedLaterActEncounterBags = CloneBags(
            _preparedLaterActEncounterBags)
    };

    public void Deconstruct(
        out PrototypeActOneEncounterPoolState? ActOneEncounterPool,
        out PrototypeRelicBagState? RelicBag,
        out PrototypeLaterActEncounterBagV111? LaterActEncounterBag,
        out PrototypeLaterActEncounterBagV111[]?
            PreparedLaterActEncounterBags)
    {
        ActOneEncounterPool = this.ActOneEncounterPool;
        RelicBag = this.RelicBag;
        LaterActEncounterBag = this.LaterActEncounterBag;
        PreparedLaterActEncounterBags = this.PreparedLaterActEncounterBags;
    }

    public void Validate(RunWorldState owner, RunState run)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(run);
        var actOneEncounterPool = ActOneEncounterPool;
        var relicBag = RelicBag;
        var laterActEncounterBag = LaterActEncounterBag;
        var preparedLaterActEncounterBags = PreparedLaterActEncounterBags;
        ValidateActOneEncounterPool(owner, actOneEncounterPool);
        if (relicBag is not null)
            PrototypeNativeRelicGrabBag.Validate(relicBag, run.Player);

        if (run.Configuration is { UsesExperimentalSilentThreeActRoute: true }
            configuration)
        {
            var darvRevealed = configuration.Profile.RevealedEpochs.Contains(
                NativeDarvEventV111.EpochId, StringComparer.Ordinal);
            if (run.ReferenceNatureRunInitializationPending &&
                preparedLaterActEncounterBags is null)
            {
                if (owner.Act != 1 || laterActEncounterBag is not null)
                    throw new InvalidOperationException(
                        "Pending run initialization has an invalid Act 1 encounter-bag boundary.");
                return;
            }
            ValidatePreparedLaterActEncounterBags(
                owner, preparedLaterActEncounterBags, run.Ascension,
                darvRevealed);
            if (owner.Act > 1)
                ValidateLaterActEncounterBag(
                    owner, laterActEncounterBag, run.Ascension,
                    darvRevealed);
            var darvAssignments = (laterActEncounterBag is { } activeBag
                    ? new[] { activeBag.AssignedAncientId }
                    : Array.Empty<string?>())
                .Concat((preparedLaterActEncounterBags ?? [])
                    .Select(bag => bag.AssignedAncientId))
                .Count(id => id == NativeDarvEventV111.EventId);
            if (darvAssignments > 1)
                throw new InvalidOperationException(
                    "The shared Darv Ancient cannot be assigned to multiple acts.");
        }
    }

    private static void ValidatePreparedLaterActEncounterBags(
        RunWorldState world,
        PrototypeLaterActEncounterBagV111[]? prepared,
        int ascension, bool darvRevealed)
    {
        var bags = prepared
            ?? throw new InvalidOperationException(
                "Experimental routes require run-start prepared encounter histories.");
        var expectedActs = Enumerable.Range(world.Act + 1, 3 - world.Act).ToArray();
        if (bags.Any(bag => bag is null)
            || !bags.Select(bag => bag.Act).SequenceEqual(expectedActs))
            throw new InvalidOperationException(
                "Prepared encounter histories must belong to each future act in route order.");
        foreach (var bag in bags)
        {
            if (bag.OrdinaryCombatsStarted != 0 || bag.RemainingElite?.Length != 15)
                throw new InvalidOperationException(
                    "An unvisited act's prepared encounter history has already been consumed.");
            ValidateLaterActEncounterBag(
                world with { Act = bag.Act }, bag, ascension,
                darvRevealed);
        }
    }

    private static void ValidateLaterActEncounterBag(
        RunWorldState world,
        PrototypeLaterActEncounterBagV111? candidate,
        int ascension, bool darvRevealed)
    {
        var bag = candidate
            ?? throw new InvalidOperationException(
                "Configured Hive/Glory requires pre-generated encounter queues.");
        var ordinaryRooms = ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(
            world.Act);
        if (bag.Act != world.Act
            || bag.OrdinaryCombatsStarted < 0
            || bag.OrdinaryCombatsStarted > ordinaryRooms)
            throw new InvalidOperationException(
                "Regional encounter queues have an invalid act/count.");

        // In v111 ActModel.GenerateRooms the act creates two weak encounters,
        // ordinaryRooms - 2 normal encounters, and fifteen elite encounters
        // before rooms are visited. Refill may duplicate IDs across bag cycles.
        // Validate the queue cursor via its remaining lengths, not uniqueness.
        var expectedWeak = Math.Max(0, 2 - bag.OrdinaryCombatsStarted);
        var expectedNormal = ordinaryRooms - 2
            - Math.Max(0, bag.OrdinaryCombatsStarted - 2);
        if (bag.RemainingWeak is null
            || bag.RemainingNormal is null
            || bag.RemainingElite is null
            || bag.RemainingWeak.Length != expectedWeak
            || bag.RemainingNormal.Length != expectedNormal
            || bag.RemainingElite.Length > 15)
            throw new InvalidOperationException(
                "Regional encounter queue lengths disagree with their cursors.");

        foreach (var (values, pool) in new[]
        {
            (bag.RemainingWeak,
                ExperimentalSilentThreeActRouteV111.Pool(
                    world.Act, PrototypeRoomType.Combat, true)),
            (bag.RemainingNormal,
                ExperimentalSilentThreeActRouteV111.Pool(
                    world.Act, PrototypeRoomType.Combat, false)),
            (bag.RemainingElite,
                ExperimentalSilentThreeActRouteV111.Pool(
                    world.Act, PrototypeRoomType.Elite, false))
        })
        {
            if (values.Any(id => !pool.Contains(id, StringComparer.Ordinal)))
                throw new InvalidOperationException(
                    "Regional encounter queue contains foreign entries.");
        }
        if (!ExperimentalSilentThreeActRouteV111.Pool(
            world.Act, PrototypeRoomType.Boss, false)
            .Contains(bag.BossEncounterId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Regional preselected boss is not eligible for this act.");
        if (bag.AssignedAncientId is { } ancientId
            && !ExperimentalSilentThreeActRouteV111.AncientPool(world.Act,
                includeDarv: darvRevealed)
                .Contains(ancientId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Prepared Ancient assignment is unsupported or belongs to another act.");
        var requiresSecondBoss = world.Act == 3 && ascension >= 10;
        if (requiresSecondBoss != (bag.SecondBossEncounterId is not null)
            || bag.SecondBossEncounterId is { } secondBoss
                && (!ExperimentalSilentThreeActRouteV111.Pool(
                        world.Act, PrototypeRoomType.Boss, false)
                    .Contains(secondBoss, StringComparer.Ordinal)
                    || secondBoss == bag.BossEncounterId))
            throw new InvalidOperationException(
                "Regional second-boss assignment disagrees with act and Ascension rules.");
    }

    private static void ValidateActOneEncounterPool(
        RunWorldState world, PrototypeActOneEncounterPoolState? pool)
    {
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

    private static PrototypeLaterActEncounterBagV111[]? CloneBags(
        PrototypeLaterActEncounterBagV111[]? bags) => bags?.Select(bag =>
            bag is null ? null! : bag.Fork()).ToArray();
}
