namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string NativeTreasureRelicConditioningPolicyId =
        "native-treasure-relic-bag-draw-v1";

    /// <summary>
    /// Query the configured native treasure-room relic draw without exposing
    /// the private order of the persistent rarity queues. The law combines
    /// the nominal rarity thresholds with equal conditional support over the
    /// still-hidden queue permutations.
    /// </summary>
    public static ChanceLawV111 QueryNativeTreasureRelicDraw(
        RunState run, string siteId, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        PrototypeStateInvariants.Validate(run);
        if (siteId != "treasure.relic_grab_bag.draw")
            throw new NotSupportedException(
                $"Native treasure Chance Law query does not support site '{siteId}'.");
        if (ordinal != 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (run.Phase != RunPhase.MapChoice || run.World is not { } world ||
            run.Stochastic.NativeRunRng is null ||
            run.Stochastic.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, run.Player)
                .Any(node => node.RoomType == PrototypeRoomType.Treasure) ||
            PrototypeGameEngine.WillSkipNextTreasure(run.Player))
            throw new InvalidOperationException(
                "Native treasure Chance Laws require a non-skipped configured Treasure node before entry.");

        var bag = world.Stochastic.RelicBag ??
            PrototypeNativeRelicGrabBag.CreateNatureUnordered(run.Player);
        var candidatesByTier = new Dictionary<PrototypeRelicRarity, string[]>
        {
            [PrototypeRelicRarity.Common] =
                PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Common, merchant: false,
                    player: run.Player)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            [PrototypeRelicRarity.Uncommon] =
                PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Uncommon, merchant: false,
                    player: run.Player)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            [PrototypeRelicRarity.Rare] =
                PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Rare, merchant: false,
                    player: run.Player)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray()
        };
        var rarityBranches =
            PrototypeNativeRelicGrabBag.NativeRarityBranches();
        var resolvedBranches = rarityBranches.Select(branch =>
        {
            var plan = PrototypeNativeRelicGrabBag.PlanPull(
                bag, branch.Rarity, merchant: false, run.Player);
            var candidates = plan.SelectedTier is { } tier
                ? candidatesByTier[tier] : [];
            return (Rarity: branch.Rarity.ToString(),
                BasisPoints: branch.BasisPoints,
                SelectedTier: plan.SelectedTier, Candidates: candidates);
        }).ToArray();

        var denominator = resolvedBranches
            .Where(branch => branch.Candidates.Length > 0)
            .Select(branch => branch.Candidates.Length)
            .Aggregate(1L, (current, count) => LeastCommonMultiple(
                current, count));
        var outcomeWeights = new SortedDictionary<string, long>(
            StringComparer.Ordinal);
        foreach (var branch in resolvedBranches)
        {
            if (branch.Candidates.Length == 0)
            {
                outcomeWeights["no_relic"] = checked(
                    outcomeWeights.GetValueOrDefault("no_relic") +
                    branch.BasisPoints * denominator);
                continue;
            }

            var candidateWeight = checked(branch.BasisPoints *
                (denominator / branch.Candidates.Length));
            foreach (var candidate in branch.Candidates)
                outcomeWeights["relic:" + candidate] = checked(
                    outcomeWeights.GetValueOrDefault("relic:" + candidate) +
                    candidateWeight);
        }

        var outcomes = outcomeWeights.Select(pair =>
            new ChanceLawOutcomeV111(pair.Key, pair.Value)).ToArray();
        var ownerId =
            $"act:{world.Act}:floor:{world.Floor}:treasure:relic_grab_bag";
        var conditioning = new
        {
            siteId, ownerId, ordinal,
            rarityBranches = rarityBranches.Select(branch => new
            {
                rarity = branch.Rarity.ToString(), branch.BasisPoints,
                fallbackTiers = PrototypeNativeRelicGrabBag
                    .PullFallbackTiers(branch.Rarity)
                    .Select(tier => tier.ToString()).ToArray()
            }).ToArray(),
            candidatesByTier = candidatesByTier.OrderBy(
                pair => pair.Key.ToString(), StringComparer.Ordinal)
                .Select(pair => new
                {
                    rarity = pair.Key.ToString(), candidates = pair.Value
                }).ToArray(),
            resolvedBranches = resolvedBranches.Select(branch => new
            {
                branch.Rarity, branch.BasisPoints,
                selectedTier = branch.Candidates.Length == 0
                    ? (string?)null : branch.SelectedTier!.Value.ToString(),
                branch.Candidates
            }).ToArray(),
            denominator
        };
        return CreateNativeBoundaryLaw(
            siteId, "conditional_relic_bag_draw", ownerId, ordinal,
            NativeTreasureRelicConditioningPolicyId,
            CanonicalJson.Sha256(conditioning),
            "native-relic-rarity-and-hidden-queue-permutation-unverified",
            outcomes);
    }

    /// <summary>
    /// Query the rarity float consumed by configured native Treasure entry.
    /// The owner and stream are taken from the actual typed request so a
    /// serialized replay checks the source occurrence exactly.
    /// </summary>
    public static ChanceLawV111 QueryNativeTreasureRarity(
        RunState run, RandomUnitFloatRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        PrototypeStateInvariants.Validate(run);
        var world = ValidateNativeTreasureQueryBoundary(run);
        request.Validate();
        if (request.SiteId != "relic_grab_bag.rarity.unit_float" ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            request.OwnerId != PrototypeNativeRelicGrabBag
                .NativeTreasureRarityOwnerId)
            throw new ArgumentException(
                "Native treasure rarity request differs from its entry boundary.");

        var rarityWeights = NativeRelicRarityGridWeights();
        var bag = world.Stochastic.RelicBag ??
            PrototypeNativeRelicGrabBag.CreateNatureUnordered(run.Player);
        var conditioning = new
        {
            request,
            candidatesByTier = new Dictionary<string, string[]>(
                StringComparer.Ordinal)
            {
                ["Common"] = PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Common, merchant: false,
                    run.Player).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                ["Uncommon"] = PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Uncommon, merchant: false,
                    run.Player).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                ["Rare"] = PrototypeNativeRelicGrabBag.EligiblePullIds(
                    bag, PrototypeRelicRarity.Rare, merchant: false,
                    run.Player).OrderBy(id => id, StringComparer.Ordinal).ToArray()
            },
            branches = PrototypeNativeRelicGrabBag.NativeRarityBranches()
                .Select(branch => new
                {
                    rarity = branch.Rarity.ToString(), branch.BasisPoints,
                    gridWeight = branch.Rarity switch
                    {
                        PrototypeRelicRarity.Common => rarityWeights.Common,
                        PrototypeRelicRarity.Uncommon => rarityWeights.Uncommon,
                        PrototypeRelicRarity.Rare => rarityWeights.Rare,
                        _ => throw new ArgumentOutOfRangeException()
                    },
                    fallbackTiers = PrototypeNativeRelicGrabBag
                        .PullFallbackTiers(branch.Rarity)
                        .Select(tier => tier.ToString()).ToArray()
                }).ToArray()
        };
        var resolvedBranches = PrototypeNativeRelicGrabBag
            .NativeRarityBranches().Select(branch =>
            {
                var plan = PrototypeNativeRelicGrabBag.PlanPull(
                    bag, branch.Rarity, merchant: false, run.Player);
                return (Branch: branch, Plan: plan);
            }).ToArray();
        var outcomeWeights = new SortedDictionary<string, long>(
            StringComparer.Ordinal);
        foreach (var (branch, plan) in resolvedBranches)
        {
            var outcomeId = plan.CandidateIds.Length == 0
                ? "no_relic"
                : "rarity:" + branch.Rarity.ToString().ToLowerInvariant();
            var weight = branch.Rarity switch
            {
                PrototypeRelicRarity.Common => rarityWeights.Common,
                PrototypeRelicRarity.Uncommon => rarityWeights.Uncommon,
                PrototypeRelicRarity.Rare => rarityWeights.Rare,
                _ => throw new ArgumentOutOfRangeException()
            };
            outcomeWeights[outcomeId] = checked(
                outcomeWeights.GetValueOrDefault(outcomeId) +
                weight);
        }
        var outcomes = outcomeWeights.Select(pair =>
            new ChanceLawOutcomeV111(pair.Key, pair.Value)).ToArray();
        return CreateNativeBoundaryLaw(
            request.SiteId, "unit_float_threshold", request.OwnerId,
            request.Ordinal, "native-treasure-rarity-threshold-v1",
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatThresholdSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query a Treasure relic identity after its rarity float has been
    /// selected. Candidate identities are canonicalized so private queue
    /// order is absent from both the request and the Nature projection.
    /// </summary>
    public static ChanceLawV111 QueryNativeTreasureRelicSelection(
        RunState run, RandomIndexRequestV111 request,
        PrototypeRelicRarity rarity, string[] candidateIds)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateIds);
        PrototypeStateInvariants.Validate(run);
        var world = ValidateNativeTreasureQueryBoundary(run);
        request.Validate();
        if (request.SiteId != PrototypeNativeRelicGrabBag
                .NativeTreasureModelSiteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            request.OperationKind != "choose_index" ||
            !Enum.IsDefined(rarity) || candidateIds.Length == 0 ||
            request.SupportCount != candidateIds.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidateIds.Length)) ||
            candidateIds.Any(string.IsNullOrWhiteSpace) ||
            candidateIds.Distinct(StringComparer.Ordinal).Count() !=
                candidateIds.Length ||
            !candidateIds.SequenceEqual(candidateIds.OrderBy(
                id => id, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new ArgumentException(
                "Native treasure relic selection request is malformed.");

        var bag = world.Stochastic.RelicBag ??
            PrototypeNativeRelicGrabBag.CreateNatureUnordered(run.Player);
        var plan = PrototypeNativeRelicGrabBag.PlanNaturePull(
            bag, rarity, merchant: false, run.Player);
        var expectedCandidates = plan.CandidateIds;
        var ownerId = PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
            world, rarity, candidateIds);
        if (plan.SelectedTier is null || request.OwnerId != ownerId ||
            !candidateIds.SequenceEqual(expectedCandidates,
                StringComparer.Ordinal))
            throw new InvalidDataException(
                "Native treasure relic support differs from its conditioned hidden queue.");

        var conditioning = new
        {
            request,
            requestedRarity = rarity.ToString(),
            selectedTier = plan.SelectedTier.Value.ToString(),
            fallbackTiers = PrototypeNativeRelicGrabBag
                .PullFallbackTiers(rarity).Select(tier => tier.ToString())
                .ToArray(),
            candidateIds
        };
        var outcomes = candidateIds.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"relic_model:{id}:index:{index}", 1L)).ToArray();
        return CreateNativeBoundaryLaw(
            request.SiteId, request.OperationKind, request.OwnerId,
            request.Ordinal, "uniform-native-treasure-relic-identity-v1",
            CanonicalJson.Sha256(conditioning),
            "native-treasure-hidden-relic-queue-identity-unverified",
            outcomes);
    }

    private static RunWorldState ValidateNativeTreasureQueryBoundary(
        RunState run)
    {
        if (run.Phase != RunPhase.MapChoice || run.World is not { } world ||
            run.Stochastic.NativeRunRng is null ||
            run.Stochastic.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, run.Player)
                .Any(node => node.RoomType == PrototypeRoomType.Treasure) ||
            PrototypeGameEngine.WillSkipNextTreasure(run.Player))
            throw new InvalidOperationException(
                "Native treasure Chance Laws require a non-skipped configured Treasure node before entry.");
        return world;
    }

    private static long LeastCommonMultiple(long left, long right)
    {
        static long GreatestCommonDivisor(long a, long b)
        {
            while (b != 0)
                (a, b) = (b, a % b);
            return a;
        }

        return checked(left / GreatestCommonDivisor(left, right) * right);
    }
    /// <summary>
    /// Query the legacy Treasure relic model draw after excluding relics the
    /// player already owns. Configured Native relic-grab-bag generation uses
    /// a separate route.
    /// </summary>
    public static ChanceLawV111 QueryLegacyTreasureRelicModel(
        int act, int floor, string[] ownedRelicIds, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (floor < 0)
            throw new ArgumentOutOfRangeException(nameof(floor));
        ArgumentNullException.ThrowIfNull(ownedRelicIds);
        if (ownedRelicIds.Any(string.IsNullOrWhiteSpace) ||
            ownedRelicIds.Distinct(StringComparer.Ordinal).Count() !=
                ownedRelicIds.Length)
            throw new ArgumentException(
                "Owned relic IDs must be non-empty and unique.",
                nameof(ownedRelicIds));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "treasure.relic_model.index_draw";
        const string streamId = "reward";
        var owned = ownedRelicIds.ToHashSet(StringComparer.Ordinal);
        var pool = PrototypeContent.RelicPool
            .Where(relicId => !owned.Contains(relicId))
            .ToArray();
        if (pool.Length == 0)
            throw new ArgumentException(
                "The legacy Treasure relic draw has no available pool and consumes no index draw.",
                nameof(ownedRelicIds));

        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"act:{act}:floor:{floor}:pool:" + poolFingerprint;
        var outcomes = pool.Select(relicId =>
            new ChanceLawOutcomeV111("relic:" + relicId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            act,
            floor,
            orderedAvailableRelicPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-treasure-relic-model-pool-v1", conditioning,
            "Equal index weights over the ordered prototype relic pool after " +
            "owned relics are excluded; source-pool fidelity and Native " +
            "relic-bag parity remain unverified.", outcomes,
            operationKind: "choose_index");
    }

}
