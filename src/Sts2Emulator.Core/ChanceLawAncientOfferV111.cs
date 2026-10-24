using System.Globalization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string AncientOfferShuffleConditioningPolicyId =
        "uniform-ancient-offer-fisher-yates-swap-v1";
    public const string DarvOfferVariantConditioningPolicyId =
        "uniform-darv-offer-variant-v1";

    /// <summary>
    /// Query one pre-event Ancient offer decision. Direct offer pools return
    /// stable choice identities; shuffle-based offers return the bounded
    /// Fisher-Yates swap index at the requested ordinal. The same pool builders
    /// and opening boundaries are shared with the event resolvers.
    /// </summary>
    public static ChanceLawV111 QueryAncientOffer(
        RunState run, string siteId, int ordinal) =>
        QueryAncientOffer(run, siteId, ordinal, null, null);

    public static ChanceLawV111 QueryAncientOffer(
        RunState run, string siteId, int ordinal,
        int[]? priorIndices, int? priorVariantIndex = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (siteId == NativeDarvEventV111.OfferVariantSiteId)
        {
            if (priorVariantIndex is not null)
                throw new ArgumentException(
                    "prior_variant_index applies only to Darv relic-shuffle queries.",
                    nameof(priorVariantIndex));
            return QueryDarvOfferVariant(run, ordinal, priorIndices);
        }
        if (siteId == NativeDarvEventV111.RelicShuffleSiteId)
            return QueryDarvRelicShuffle(
                run, ordinal, priorIndices, priorVariantIndex);
        if (priorVariantIndex is not null)
            throw new ArgumentException(
                "prior_variant_index applies only to Darv relic-shuffle queries.",
                nameof(priorVariantIndex));
        if (IsShuffleAncientOfferSite(siteId))
            return QueryAncientOfferShuffle(
                run, siteId, ordinal, priorIndices);
        if (priorIndices is { Length: > 0 })
            throw new ArgumentException(
                "prior_indices apply only to shuffle-based Ancient offers.",
                nameof(priorIndices));
        string ownerId;
        string[] candidates;
        if (siteId == NativeOrobasEventV111.OfferSiteId)
        {
            NativeOrobasEventV111.ValidateOpeningBoundary(run);
            ownerId = "event:OROBAS:act:2:floor:0";
            candidates = NativeOrobasEventV111.OfferCandidateIds(
                run.Player, ordinal);
        }
        else if (siteId == NativePaelRewardsV111.OfferSiteId)
        {
            NativePaelRewardsV111.ValidateOpeningBoundary(run);
            ownerId = "event:PAEL:act:2:floor:0";
            var pools = NativePaelRewardsV111.OfferPools(run.Player);
            if (ordinal < 0 || ordinal >= pools.Length)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            candidates = pools[ordinal].Select(id => "take_" + id).ToArray();
        }
        else if (siteId == NativeAncientRewardsV111.TezcataraOfferSiteId)
        {
            NativeAncientRewardsV111.ValidateTezcataraOpeningBoundary(run);
            ownerId = "event:TEZCATARA:act:2:floor:0";
            var pools = NativeAncientRewardsV111.TezcataraPools(run.Player);
            if (ordinal < 0 || ordinal >= pools.Length)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            candidates = pools[ordinal].Select(id => "take_" + id).ToArray();
        }
        else
        {
            throw new NotSupportedException(
                $"Ancient offer query does not support site '{siteId}'.");
        }

        var orderedChoiceIds = candidates;
        var indexWeights = NativeNextIntGridWeights(orderedChoiceIds.Length);
        var weights = new Dictionary<string, long>(StringComparer.Ordinal);
        var outcomeOrder = new List<string>();
        for (var index = 0; index < orderedChoiceIds.Length; index++)
        {
            var choiceId = orderedChoiceIds[index];
            var outcomeId = "event_choice:" + choiceId;
            if (!weights.TryGetValue(outcomeId, out var weight))
            {
                weights.Add(outcomeId, indexWeights[index]);
                outcomeOrder.Add(outcomeId);
            }
            else
            {
                weights[outcomeId] = checked(weight + indexWeights[index]);
            }
        }
        var outcomes = outcomeOrder.Select(outcomeId =>
            new ChanceLawOutcomeV111(outcomeId, weights[outcomeId])).ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            ownerId,
            ordinal,
            CandidateChoiceIds = orderedChoiceIds,
            policy = EventOfferConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        return Create(eventId, siteId, "event_offer_index", ownerId,
            ordinal, EventOfferConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>Compatibility entry point for the original query-only site.</summary>
    public static ChanceLawV111 QueryOrobasOffer(
        RunState run, int ordinal) => QueryAncientOffer(
            run, NativeOrobasEventV111.OfferSiteId, ordinal);

    /// <summary>
    /// Counts exact 53-bit NextDouble grid preimages for a monotone integer
    /// mapping of the source-rounded unit NextFloat. Binary search locates
    /// each integer outcome's contiguous grid interval.
    /// </summary>
    private static SortedDictionary<int, long>
        NativeNextFloatMappedIntegerWeights(Func<float, int> mapOutcome)
    {
        ArgumentNullException.ThrowIfNull(mapOutcome);
        var weights = new SortedDictionary<int, long>();
        long firstGridIndex = 0;
        long totalWeight = 0;
        while (firstGridIndex < NativeNextDoubleGridSize)
        {
            var mappedOutcome = mapOutcome(
                NativeUnitFloatGridValue(firstGridIndex));
            var low = firstGridIndex + 1;
            var high = NativeNextDoubleGridSize;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                var candidate = mapOutcome(
                    NativeUnitFloatGridValue(middle));
                if (candidate > mappedOutcome)
                    high = middle;
                else
                    low = middle + 1;
            }

            var endGridIndex = low;
            var weight = checked(endGridIndex - firstGridIndex);
            if (weight <= 0 ||
                weights.Count > 0 && weights.Last().Key >= mappedOutcome)
                throw new InvalidDataException(
                    "Native unit-float outcome mapping is not monotone.");
            weights.Add(mappedOutcome, weight);
            totalWeight = checked(totalWeight + weight);
            firstGridIndex = endGridIndex;
        }

        if (weights.Count == 0 ||
            totalWeight != NativeNextDoubleGridSize)
            throw new InvalidOperationException(
                "Native unit-float outcome masses do not cover the input grid.");
        return weights;
    }

    private static ChanceLawV111 CreateNativeBoundaryLaw(
        string siteId, string operationKind, string ownerId, int ordinal,
        string policy, string conditioningFingerprint,
        string provenance, ChanceLawOutcomeV111[] outcomes,
        string probabilityStatus = ChanceLawV111.UnverifiedStatus)
    {
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId, ownerId, ordinal, conditioningFingerprint
        });
        return Create(eventId, siteId, operationKind, ownerId, ordinal,
            policy, conditioningFingerprint, provenance, outcomes,
            probabilityStatus);
    }

    public static int AncientOfferDecisionCount(RunState run, string siteId)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (IsChoiceAncientOfferSite(siteId))
        {
            ValidateAncientOfferOpeningBoundary(run, siteId);
            return 3;
        }
        if (siteId == NativeDarvEventV111.OfferVariantSiteId)
        {
            NativeDarvEventV111.ValidateOpeningBoundary(run);
            return checked(1 + DarvOfferShuffleRequests(run).Length);
        }
        ValidateAncientOfferOpeningBoundary(run, siteId);
        if (siteId == NativeDarvEventV111.RelicShuffleSiteId)
            return DarvOfferShuffleRequests(run).Length;
        var pools = AncientOfferShuffleChoicePools(run, siteId);
        return AncientOfferShuffleRequests(siteId, pools).Length;
    }

    internal static bool IsSupportedAncientOfferSite(string siteId) =>
        IsChoiceAncientOfferSite(siteId) || IsShuffleAncientOfferSite(siteId) ||
        siteId == NativeDarvEventV111.OfferVariantSiteId;

    public static bool IsSupportedAncientOfferQuerySite(string siteId) =>
        IsSupportedAncientOfferSite(siteId);

    internal static bool IsSupportedAncientOfferSessionSite(string siteId) =>
        siteId != NativeDarvEventV111.RelicShuffleSiteId &&
        IsSupportedAncientOfferSite(siteId);

    public static bool IsShuffleAncientOfferSite(string siteId) =>
        siteId is NativeNonupeipeEventV111.ShuffleSiteId or
            NativeTanxEventV111.ShuffleSiteId or
            NativeVakuuEventV111.ShuffleSiteId or
            NativeDarvEventV111.RelicShuffleSiteId;

    internal static ChanceLawV111 QueryAncientOfferSessionStep(
        RunState run, string sessionSiteId,
        ReferenceNatureDecisionV111[] prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (sessionSiteId == NativeDarvEventV111.OfferVariantSiteId)
        {
            if (prefix.Length == 0)
                return QueryDarvOfferVariant(run, 0, null);
            return QueryDarvRelicShuffle(run, prefix.Length - 1,
                prefix.Skip(1).Select(decision => decision.OutcomeIndex)
                    .ToArray(),
                prefix[0].OutcomeIndex);
        }
        var priorIndices = IsShuffleAncientOfferSite(sessionSiteId)
            ? prefix.Select(decision => decision.OutcomeIndex).ToArray()
            : null;
        return QueryAncientOffer(run, sessionSiteId, prefix.Length,
            priorIndices);
    }

    internal static void ValidateAncientOfferOpeningBoundary(
        RunState run, string siteId)
    {
        if (siteId is NativeDarvEventV111.OfferVariantSiteId or
            NativeDarvEventV111.RelicShuffleSiteId)
            NativeDarvEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeOrobasEventV111.OfferSiteId)
            NativeOrobasEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativePaelRewardsV111.OfferSiteId)
            NativePaelRewardsV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeAncientRewardsV111.TezcataraOfferSiteId)
            NativeAncientRewardsV111.ValidateTezcataraOpeningBoundary(run);
        else if (siteId == NativeNonupeipeEventV111.ShuffleSiteId)
            NativeNonupeipeEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeTanxEventV111.ShuffleSiteId)
            NativeTanxEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeVakuuEventV111.ShuffleSiteId)
            NativeVakuuEventV111.ValidateOpeningBoundary(run);
        else
            throw new NotSupportedException(
                $"Ancient offer query does not support site '{siteId}'.");
    }

    internal static string[][] AncientOfferShuffleChoicePools(
        RunState run, string siteId)
    {
        if (siteId == NativeDarvEventV111.RelicShuffleSiteId)
            return [NativeDarvEventV111.PoolOneRelicIdsForAct(
                    run.World!.Act, run.Player)
                .Select(id => "take_" + id).ToArray()];
        if (siteId == NativeNonupeipeEventV111.ShuffleSiteId)
            return [NativeNonupeipeEventV111.CandidateRelics(
                    NativeNonupeipeEventV111.HasBeautifulBraceletEligibility(
                        run.Player))
                .Select(id => "take_" + id).ToArray()];
        if (siteId == NativeTanxEventV111.ShuffleSiteId)
            return [NativeTanxEventV111.CandidateRelics(
                    NativeTanxEventV111.HasTriBoomerangEligibility(run.Player))
                .Select(id => "take_" + id).ToArray()];
        if (siteId == NativeVakuuEventV111.ShuffleSiteId)
            return NativeVakuuEventV111.RelicPools.Select(pool =>
                pool.Select(id => "take_" + id).ToArray()).ToArray();
        throw new NotSupportedException(
            $"Ancient offer query does not support shuffle site '{siteId}'.");
    }

    internal static AncientOfferShuffleRequestV111[]
        AncientOfferShuffleRequests(string siteId, string[][] pools)
    {
        ArgumentNullException.ThrowIfNull(pools);
        if (siteId is not (NativeNonupeipeEventV111.ShuffleSiteId or
                NativeTanxEventV111.ShuffleSiteId or
                NativeVakuuEventV111.ShuffleSiteId))
            throw new NotSupportedException(
                $"Ancient offer query does not support shuffle site '{siteId}'.");
        var streamId = siteId switch
        {
            NativeNonupeipeEventV111.ShuffleSiteId =>
                "context.event.NONUPEIPE.player_0",
            NativeTanxEventV111.ShuffleSiteId =>
                "context.event.TANX.player_0",
            NativeVakuuEventV111.ShuffleSiteId =>
                "context.event.VAKUU.player_0",
            _ => throw new NotSupportedException(
                $"Ancient offer query does not support shuffle site '{siteId}'.")
        };
        var ownerId = siteId switch
        {
            NativeNonupeipeEventV111.ShuffleSiteId =>
                "event:NONUPEIPE:act:3:floor:0",
            NativeTanxEventV111.ShuffleSiteId =>
                "event:TANX:act:3:floor:0",
            NativeVakuuEventV111.ShuffleSiteId =>
                "event:VAKUU:act:3:floor:0",
            _ => throw new NotSupportedException(
                $"Ancient offer query does not support shuffle site '{siteId}'.")
        };
        var requests = new List<AncientOfferShuffleRequestV111>();
        for (var poolIndex = 0; poolIndex < pools.Length; poolIndex++)
        {
            var pool = pools[poolIndex];
            for (var end = pool.Length - 1; end > 0; end--)
            {
                var ordinal = requests.Count;
                requests.Add(new AncientOfferShuffleRequestV111(
                    poolIndex, end,
                    new RandomIndexRequestV111(
                        siteId, streamId, ownerId, ordinal, [],
                        OperationKind: "bounded_integer",
                        BoundedCount: end + 1)));
            }
        }
        return requests.ToArray();
    }

    private static ChanceLawV111 QueryAncientOfferShuffle(
        RunState run, string siteId, int ordinal, int[]? priorIndices)
    {
        ValidateAncientOfferOpeningBoundary(run, siteId);
        var initialPools = AncientOfferShuffleChoicePools(run, siteId);
        var requests = AncientOfferShuffleRequests(siteId, initialPools);
        if (ordinal < 0 || ordinal >= requests.Length)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        priorIndices ??= [];
        if (priorIndices.Length != ordinal)
            throw new ArgumentException(
                "Shuffle-based Ancient offer queries require one prior index for every earlier ordinal.",
                nameof(priorIndices));

        var currentPools = initialPools
            .Select(pool => (string[])pool.Clone()).ToArray();
        for (var index = 0; index < ordinal; index++)
        {
            var request = requests[index];
            var selectedIndex = priorIndices[index];
            if (selectedIndex < 0 ||
                selectedIndex >= request.Request.SupportCount)
                throw new ArgumentOutOfRangeException(nameof(priorIndices),
                    $"Prior swap index {index} is outside its saved support.");
            (currentPools[request.PoolIndex][request.EndIndex],
                currentPools[request.PoolIndex][selectedIndex]) =
                (currentPools[request.PoolIndex][selectedIndex],
                    currentPools[request.PoolIndex][request.EndIndex]);
        }

        var selectedRequest = requests[ordinal].Request;
        var ownerId = selectedRequest.OwnerId;
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            InitialChoicePools = initialPools,
            CurrentChoicePools = currentPools,
            PriorIndices = priorIndices,
            RequestFingerprint = CanonicalJson.Sha256(selectedRequest),
            policy = AncientOfferShuffleConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        var weights = NativeNextIntGridWeights(
            selectedRequest.SupportCount);
        var outcomes = Enumerable.Range(0, selectedRequest.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(CultureInfo.InvariantCulture),
                weights[index])).ToArray();
        return Create(eventId, siteId, selectedRequest.OperationKind,
            ownerId, ordinal, AncientOfferShuffleConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static ChanceLawV111 QueryDarvOfferVariant(
        RunState run, int ordinal, int[]? priorIndices)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        if (ordinal != 0 || priorIndices is { Length: > 0 })
            throw new InvalidOperationException(
                "Darv offer-variant law supports only ordinal zero without a prefix.");
        var request = DarvOfferVariantRequest(run);
        var ownerId = request.OwnerId;
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            act = run.World!.Act,
            request,
            outcomes = new[] { "offer_variant:dusty_tome",
                "offer_variant:three_relics" },
            policy = DarvOfferVariantConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId = request.SiteId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        var weights = NativeNextIntGridWeights(request.SupportCount);
        var outcomes = new[]
        {
            new ChanceLawOutcomeV111("offer_variant:dusty_tome", weights[0]),
            new ChanceLawOutcomeV111("offer_variant:three_relics", weights[1])
        };
        return Create(eventId, request.SiteId, request.OperationKind,
            ownerId, ordinal, DarvOfferVariantConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static ChanceLawV111 QueryDarvRelicShuffle(
        RunState run, int ordinal, int[]? priorIndices,
        int? priorVariantIndex)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        var requests = DarvOfferShuffleRequests(run);
        if (ordinal < 0 || ordinal >= requests.Length)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        priorIndices ??= [];
        if (priorIndices.Length != ordinal)
            throw new ArgumentException(
                "Darv shuffle queries require one prior index per earlier swap.",
                nameof(priorIndices));
        if (priorVariantIndex is { } variant && variant is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(priorVariantIndex));

        var initial = NativeDarvEventV111.PoolOneRelicIdsForAct(
            run.World!.Act, run.Player);
        var current = (string[])initial.Clone();
        for (var index = 0; index < ordinal; index++)
        {
            var selectedIndex = priorIndices[index];
            if (selectedIndex < 0 ||
                selectedIndex >= requests[index].SupportCount)
                throw new ArgumentOutOfRangeException(nameof(priorIndices),
                    $"Prior Darv swap index {index} is outside its support.");
            var end = initial.Length - index - 1;
            (current[end], current[selectedIndex]) =
                (current[selectedIndex], current[end]);
        }

        var selectedRequest = requests[ordinal];
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            act = run.World.Act,
            initial,
            current,
            priorVariantIndex,
            priorIndices,
            request = CanonicalJson.Sha256(selectedRequest),
            policy = AncientOfferShuffleConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId = selectedRequest.SiteId,
            ownerId = selectedRequest.OwnerId,
            ordinal,
            conditioningFingerprint
        });
        var weights = NativeNextIntGridWeights(
            selectedRequest.SupportCount);
        var outcomes = Enumerable.Range(0, selectedRequest.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(CultureInfo.InvariantCulture),
                weights[index])).ToArray();
        return Create(eventId, selectedRequest.SiteId,
            selectedRequest.OperationKind, selectedRequest.OwnerId,
            ordinal, AncientOfferShuffleConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    internal static RandomIndexRequestV111 DarvOfferVariantRequest(
        RunState run)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        var streamId = NativeRngV111Contexts.EventStreamId(
            NativeDarvEventV111.NativeRngEventId);
        var ownerId = $"event:DARV:act:{run.World!.Act}:floor:0";
        var request = new RandomIndexRequestV111(
            NativeDarvEventV111.OfferVariantSiteId,
            streamId, ownerId, 0, [],
            OperationKind: "bounded_integer", BoundedCount: 2);
        request.Validate();
        return request;
    }

    internal static RandomIndexRequestV111[] DarvOfferShuffleRequests(
        RunState run)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        var relics = NativeDarvEventV111.PoolOneRelicIdsForAct(
            run.World!.Act, run.Player);
        var streamId = NativeRngV111Contexts.EventStreamId(
            NativeDarvEventV111.NativeRngEventId);
        var ownerId = $"event:DARV:act:{run.World.Act}:floor:0";
        var requests = new List<RandomIndexRequestV111>();
        for (var end = relics.Length - 1; end > 0; end--)
        {
            var request = new RandomIndexRequestV111(
                NativeDarvEventV111.RelicShuffleSiteId,
                streamId, ownerId, requests.Count, [],
                OperationKind: "bounded_integer", BoundedCount: end + 1);
            request.Validate();
            requests.Add(request);
        }
        return requests.ToArray();
    }

    private static bool IsChoiceAncientOfferSite(string siteId) =>
        siteId is NativeOrobasEventV111.OfferSiteId or
            NativePaelRewardsV111.OfferSiteId or
            NativeAncientRewardsV111.TezcataraOfferSiteId;

}
