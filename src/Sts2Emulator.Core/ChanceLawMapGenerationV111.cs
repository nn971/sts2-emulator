using System.Globalization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    /// <summary>
    /// Query a source-shaped map request. Native map sites use their
    /// source-specific laws; the retained legacy convergence key uses a
    /// compact uniform law over its bounded raw-index support.
    /// </summary>
    public static ChanceLawV111 QueryMapRequest(
        ControlledStochasticRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.IndexRequest is { } index &&
            index.SiteId == "map.legacy.convergence.order_key")
        {
            index.ValidateForLegacyProvider();
            if (index.StreamId != "map" ||
                index.OperationKind != "bounded_integer" ||
                index.BoundedCount != int.MaxValue ||
                index.OrderedCandidates.Length != 0)
                throw new ArgumentException(
                    "Legacy convergence ordering requires its bounded map-stream integer request.",
                    nameof(request));
            var eventId = CanonicalJson.Sha256(new
            {
                boundary = "legacy-map-convergence-order-key",
                request = index
            });
            return QueryRandomIndex(eventId, index);
        }

        return QueryNativeMapRequest(request);
    }

    internal static ChanceLawV111
        QueryLegacyMapEdgeExtraChanceTicketIndices(
            RandomIndexRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.ValidateForLegacyProvider();
        const string siteId = "map.legacy.edge_extra_chance.ticket";
        const int ticketCount = 100;
        const int edgeExtensionTicketCount = 35;
        if (request.SiteId != siteId || request.StreamId != "map" ||
            request.OperationKind != "bounded_integer" ||
            request.BoundedCount != ticketCount ||
            request.OrderedCandidates.Length != 0)
            throw new ArgumentException(
                "Legacy map edge-extension law requires its source-shaped 100-ticket request.",
                nameof(request));

        var outcomes = Enumerable.Range(0, ticketCount)
            .Select(ticket => new ChanceLawOutcomeV111(
                $"ticket:{ticket}:edge_extension:" +
                (ticket < edgeExtensionTicketCount ? "yes" : "no"), 1L))
            .ToArray();
        return CreateLegacyCategoricalLaw(
            siteId, request.StreamId, request.OwnerId, request.Ordinal,
            "legacy-map-edge-extension-ticket-index-v1",
            new
            {
                request,
                ticketCount,
                edgeExtensionThreshold = edgeExtensionTicketCount,
                sourceComparison = "ticket < threshold"
            },
            "Uniform legacy 100-ticket positions annotated with the " +
            "prototype 35-ticket edge-extension branch; map source fidelity " +
            "and ticket weighting remain unverified.",
            outcomes);
    }

    public static string[] NativeMapGaussianIntQuerySiteIds =>
        StochasticSiteRegistryV111.NativeGaussianIntSiteIds;

    public static bool IsNativeMapGenerationRequestSite(string siteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        return StochasticSiteRegistryV111.NativeMapGenerationSessionSiteIds
            .Contains(siteId, StringComparer.Ordinal);
    }

    public static bool IsNativeMapGaussianIntQuerySite(string siteId) =>
        StochasticSiteRegistryV111.IsNativeGaussianIntSite(siteId);

    public static bool MatchesNativeMapGaussianIntRequest(
        RandomGaussianIntRequestV111 request)
        => StochasticSiteRegistryV111
            .MatchesNativeGaussianIntRequest(request);

    /// <summary>
    /// Query the emulator's continuous truncated-normal model for one of the
    /// source-shaped Native map quota Gaussian requests. The model does not
    /// count the finite Native RNG grid and is intentionally unverified.
    /// </summary>
    public static ChanceLawV111 QueryNativeMapGaussianInt(
        string siteId, string streamId, string ownerId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal != 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal),
                "Each Native map quota Gaussian site is queried at ordinal zero.");

        int mean;
        int standardDeviation = 1;
        int minInclusive;
        int maxInclusive;
        string expectedStream;
        string expectedOwner;
        switch (siteId)
        {
            case PrototypeNativeOvergrowthMap.RestCountGaussianSiteId:
                mean = 7;
                minInclusive = 6;
                maxInclusive = 7;
                expectedStream = "context.act_1_map";
                expectedOwner = "act:1:rest_count";
                break;
            case PrototypeNativeOvergrowthMap.UnknownCountGaussianSiteId:
                mean = 12;
                minInclusive = 10;
                maxInclusive = 14;
                expectedStream = "context.act_1_map";
                expectedOwner = "act:1:unknown_count";
                break;
            case PrototypeNativeLaterActMap.HiveRestCountGaussianSiteId:
                mean = 6;
                minInclusive = 6;
                maxInclusive = 7;
                expectedStream = "context.act_2_map";
                expectedOwner = "map:act:2";
                break;
            case PrototypeNativeLaterActMap.UnknownCountGaussianSiteId:
                mean = 12;
                minInclusive = 10;
                maxInclusive = 14;
                if (ownerId is not ("map:act:2" or "map:act:3"))
                    throw new ArgumentException(
                        "Later-act Unknown quota law requires owner map:act:2 or map:act:3.",
                        nameof(ownerId));
                expectedStream = ownerId == "map:act:2"
                    ? "context.act_2_map" : "context.act_3_map";
                expectedOwner = ownerId;
                break;
            default:
                throw new NotSupportedException(
                    $"Native map Gaussian Chance Law does not support site '{siteId}'.");
        }

        if (streamId != expectedStream || ownerId != expectedOwner)
            throw new ArgumentException(
                $"Native map Gaussian site '{siteId}' requires stream '{expectedStream}' and owner '{expectedOwner}'.");

        const double weightScale = 1_000_000_000_000d;
        var outcomes = Enumerable.Range(minInclusive,
                maxInclusive - minInclusive + 1)
            .Select(value => new ChanceLawOutcomeV111(
                "integer:" + value.ToString(CultureInfo.InvariantCulture),
                Math.Max(1L, checked((long)Math.Round(
                    (NormalCdf((value + 0.5 - mean) /
                        (double)standardDeviation) -
                     NormalCdf((value - 0.5 - mean) /
                        (double)standardDeviation)) * weightScale,
                    MidpointRounding.AwayFromZero)))))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-map-truncated-gaussian-request",
            siteId,
            streamId,
            ownerId,
            ordinal
        });
        var conditioning = new
        {
            model = "continuous-normal-rounded-and-truncated-v1",
            mean,
            standardDeviation,
            minInclusive,
            maxInclusive
        };
        return Create(eventId, siteId, "gaussian_int", ownerId, ordinal,
            "native-map-truncated-rounded-normal-model-v1",
            CanonicalJson.Sha256(new { streamId, ownerId, ordinal, conditioning }),
            "continuous truncated-rounded-normal approximation; Native finite-grid and source parity UNVERIFIED",
            outcomes);
    }

    /// <summary>
    /// Query one request emitted by the source-shaped Native Overgrowth,
    /// Hive, or Glory map generator. This dispatcher is the fail-closed law
    /// boundary used by the atomic ReferenceNature map continuation.
    /// </summary>
    public static ChanceLawV111 QueryNativeMapRequest(
        ControlledStochasticRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.GaussianIntRequest is { } gaussian)
        {
            if (!MatchesNativeMapGaussianIntRequest(gaussian))
                throw new NotSupportedException(
                    $"Native map Chance Law does not support request '{gaussian.SiteId}'.");
            return QueryNativeMapGaussianInt(
                gaussian.SiteId, gaussian.StreamId, gaussian.OwnerId,
                gaussian.Ordinal);
        }

        if (request.IndexRequest is not { } index ||
            index.OperationKind != "bounded_integer" ||
            index.OrderedCandidates.Length != 0 ||
            index.BoundedCount is not { } supportCount)
            throw new NotSupportedException(
                "Native map Chance Law supports only bounded-index and Gaussian-integer requests.");

        if (index.SiteId == PrototypeNativeOvergrowthMap.PathStartColumnSiteId)
        {
            RequireMapRequest(index, "context.act_1_map", supportCount: 7);
            return QueryOvergrowthPathStart(index.OwnerId, index.Ordinal);
        }
        if (index.SiteId == PrototypeNativeLaterActMap.PathStartSiteId)
        {
            var act = LaterActFromOwner(index.OwnerId);
            RequireMapRequest(index, $"context.act_{act}_map", supportCount: 7);
            return QueryLaterActPathStart(act, index.Ordinal);
        }
        if (index.SiteId == PrototypeNativeOvergrowthMap.PathDeltaShuffleSiteId)
        {
            var expectedSupport = index.Ordinal % 2 == 0 ? 3 : 2;
            RequireMapRequest(index, "context.act_1_map", expectedSupport);
            return QueryOvergrowthPathDeltaSwap(index.OwnerId, index.Ordinal);
        }
        if (index.SiteId == PrototypeNativeLaterActMap.PathDeltaShuffleSiteId)
        {
            var act = LaterActFromOwner(index.OwnerId);
            var expectedSupport = index.Ordinal % 2 == 0 ? 3 : 2;
            RequireMapRequest(index, $"context.act_{act}_map", expectedSupport);
            return QueryLaterActPathDeltaSwap(act, index.Ordinal);
        }
        if (index.SiteId == PrototypeNativeOvergrowthMap.RoomAssignmentShuffleSiteId)
        {
            RequireMapRequest(index, "context.act_1_map", supportCount);
            return QueryOvergrowthRoomAssignmentSwap(
                index.OwnerId, index.Ordinal, supportCount);
        }
        if (index.SiteId == PrototypeNativeLaterActMap.RoomAssignmentShuffleSiteId)
        {
            var act = LaterActFromOwner(index.OwnerId);
            RequireMapRequest(index, $"context.act_{act}_map", supportCount);
            return QueryLaterActRoomAssignmentSwap(
                act, index.Ordinal, supportCount);
        }
        if (index.SiteId == PrototypeNativeLaterActMap.GloryRestCountSiteId)
        {
            RequireMapRequest(index, "context.act_3_map", supportCount: 2);
            if (index.OwnerId != "map:act:3" || index.Ordinal != 0)
                throw new InvalidDataException(
                    "Glory rest-quota request differs from its source-shaped owner or ordinal.");
            return QueryLaterActGloryRestCount(index.Ordinal);
        }

        throw new NotSupportedException(
            $"Native map Chance Law does not support request '{index.SiteId}'.");
    }

    private static int LaterActFromOwner(string ownerId) => ownerId switch
    {
        "map:act:2" => 2,
        "map:act:3" => 3,
        _ => throw new InvalidDataException(
            "Later-act map request requires owner map:act:2 or map:act:3.")
    };

    private static void RequireMapRequest(
        RandomIndexRequestV111 request, string expectedStream,
        int supportCount)
    {
        if (request.StreamId != expectedStream ||
            request.BoundedCount != supportCount)
            throw new InvalidDataException(
                "Native map request differs from its declared stream or bounded support.");
    }

    private static double NormalCdf(double value)
    {
        var absolute = Math.Abs(value);
        var t = 1.0 / (1.0 + 0.2316419 * absolute);
        var polynomial = t * (0.319381530 + t * (-0.356563782 +
            t * (1.781477937 + t * (-1.821255978 +
            t * 1.330274429))));
        var tail = 0.3989422804014327 *
            Math.Exp(-0.5 * absolute * absolute) * polynomial;
        return value >= 0 ? 1.0 - tail : tail;
    }

    /// <summary>
    /// Query one of the fixed Act 1 boss pools before run initialization.
    /// Outcomes preserve source-ordered encounter identities; these laws do
    /// not claim their legacy seed-to-index distribution is calibrated.
    /// </summary>
    public static ChanceLawV111 QueryOvergrowthBossEncounter(int ordinal) =>
        QueryActOneBossEncounter(
            "run.encounter.overgrowth_boss.index_draw", ordinal);

    public static ChanceLawV111 QueryUnderdocksBossEncounter(int ordinal) =>
        QueryActOneBossEncounter(
            PrototypeNativeUnderdocks.BossEncounterIndexSiteId, ordinal);

    /// <summary>
    /// Query the Glory Ascension 10 second-boss choice conditional on the
    /// already-selected first boss. This exposes only the declared remaining
    /// pool and does not inspect an initialized run's hidden encounter bag.
    /// </summary>
    public static ChanceLawV111 QueryLaterActSecondBoss(
        string firstBossId, int ascension, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstBossId);
        if (ascension < 10)
            throw new ArgumentOutOfRangeException(
                nameof(ascension),
                "A second boss is assigned only at Glory Ascension 10 or higher.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Second-boss assignment requires a nonnegative declared stream ordinal.");

        var bossPool = ExperimentalSilentThreeActRouteV111.Pool(
            3, PrototypeRoomType.Boss, weak: false);
        if (!bossPool.Contains(firstBossId, StringComparer.Ordinal))
            throw new ArgumentException(
                "The declared first boss is not in the Glory boss pool.",
                nameof(firstBossId));
        var alternatives = bossPool.Where(id => id != firstBossId).ToArray();
        var siteId = ExperimentalSilentThreeActRouteV111
            .SecondBossIndexSiteId;
        var ownerId = $"act:3:first_boss:{firstBossId}:alternatives:"
            + CanonicalJson.Sha256(alternatives);
        var outcomes = alternatives.Select(encounterId =>
            new ChanceLawOutcomeV111(
                "encounter:" + encounterId, 1L)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            act = 3,
            ascension,
            firstBossId,
            ownerId,
            orderedEncounterIds = alternatives
        });

        return Create(
            eventId,
            siteId,
            "choose_index",
            ownerId,
            ordinal,
            "uniform-source-ordered-second-boss-index-unverified",
            conditioningFingerprint,
            "equal-index-mass-over-remaining-glory-bosses-unverified",
            outcomes);
    }

    /// <summary>
    /// Query the raw Hive or Glory boss-pool index before the deterministic
    /// first-undiscovered-boss preference is applied.
    /// </summary>
    public static ChanceLawV111 QueryLaterActBossPool(int act, int ordinal)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(
                nameof(act), "Later-act boss selection supports Hive or Glory.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Later-act boss selection requires a nonnegative declared stream ordinal.");

        var siteId = ExperimentalSilentThreeActRouteV111.BossIndexSiteId;
        var bossPool = ExperimentalSilentThreeActRouteV111.Pool(
            act, PrototypeRoomType.Boss, weak: false);
        var ownerId = $"act:{act}:boss_pool:"
            + CanonicalJson.Sha256(bossPool);
        var outcomes = bossPool.Select(encounterId =>
            new ChanceLawOutcomeV111(
                "encounter:" + encounterId, 1L)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            act,
            ownerId,
            orderedEncounterIds = bossPool
        });

        return Create(
            eventId,
            siteId,
            "choose_index",
            ownerId,
            ordinal,
            "uniform-source-ordered-later-act-boss-index-unverified",
            conditioningFingerprint,
            "equal-index-mass-over-ordered-later-act-boss-pool-unverified",
            outcomes);
    }

    /// <summary>
    /// Query the Glory map's single Native bounded draw for a rest-room quota.
    /// The exact NextInt(2) transform is source-verified independently of map
    /// generation order, seed derivation, and cursor parity.
    /// </summary>
    public static ChanceLawV111 QueryLaterActGloryRestCount(int ordinal)
    {
        if (ordinal != 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Glory rest-quota selection has one site-local draw at ordinal zero.");

        const int act = 3;
        const int restCountOffset = 5;
        const int supportCount = 2;
        const string ownerId = "map:act:3";
        var siteId = PrototypeNativeLaterActMap.GloryRestCountSiteId;
        var indexWeights = NativeNextIntGridWeights(supportCount);
        var outcomes = indexWeights.Select((weight, index) =>
            new ChanceLawOutcomeV111(
                $"rest_quota:{restCountOffset + index}", weight)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            act,
            ownerId,
            supportCount,
            orderedRestCounts = new[] { 5, 6 }
        });

        return Create(
            eventId,
            siteId,
            "bounded_integer",
            ownerId,
            ordinal,
            NativeRandomIndexConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query one raw Native path-start column draw before any path-start retry
    /// constraint is applied. The caller declares its site-local ordinal.
    /// </summary>
    public static ChanceLawV111 QueryLaterActPathStart(int act, int ordinal)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(
                nameof(act), "Later-act path starts support Hive or Glory.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Later-act path-start queries require a nonnegative site-local ordinal.");

        return QueryMapPathStart(
            PrototypeNativeLaterActMap.PathStartSiteId,
            $"map:act:{act}", act, ordinal);
    }

    /// <summary>
    /// Query one raw Native Overgrowth path-start draw. The source owner
    /// distinguishes each path and the second-path collision retries.
    /// </summary>
    public static ChanceLawV111 QueryOvergrowthPathStart(
        string ownerId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (!IsOvergrowthPathStartOwner(ownerId))
            throw new ArgumentException(
                "Owner ID must identify an Act 1 path start or path-two retry.",
                nameof(ownerId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Overgrowth path-start queries require a nonnegative site-local ordinal.");

        return QueryMapPathStart(
            PrototypeNativeOvergrowthMap.PathStartColumnSiteId,
            ownerId, 1, ordinal);
    }

    /// <summary>
    /// Query one raw Native Hive/Glory delta-shuffle index. Every three-entry
    /// Fisher-Yates shuffle uses bounds 3 then 2 at successive site ordinals.
    /// </summary>
    public static ChanceLawV111 QueryLaterActPathDeltaSwap(
        int act, int ordinal)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(
                nameof(act), "Later-act path deltas support Hive or Glory.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Later-act path-delta queries require a nonnegative site-local ordinal.");

        return QueryMapPathDeltaSwap(
            PrototypeNativeLaterActMap.PathDeltaShuffleSiteId,
            $"map:act:{act}", ordinal);
    }

    /// <summary>
    /// Query one raw Native Overgrowth delta-shuffle index using its source
    /// path/floor owner and site-local ordinal.
    /// </summary>
    public static ChanceLawV111 QueryOvergrowthPathDeltaSwap(
        string ownerId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (!IsOvergrowthPathDeltaOwner(ownerId))
            throw new ArgumentException(
                "Owner ID must identify an Act 1 path and floor delta shuffle.",
                nameof(ownerId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Overgrowth path-delta queries require a nonnegative site-local ordinal.");

        return QueryMapPathDeltaSwap(
            PrototypeNativeOvergrowthMap.PathDeltaShuffleSiteId,
            ownerId, ordinal);
    }

    private static ChanceLawV111 QueryMapPathDeltaSwap(
        string siteId, string ownerId, int ordinal)
    {
        var supportCount = ordinal % 2 == 0 ? 3 : 2;
        var indexWeights = NativeNextIntGridWeights(supportCount);
        var outcomes = indexWeights.Select((weight, index) =>
            new ChanceLawOutcomeV111($"swap_index:{index}", weight))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            ownerId,
            ordinal,
            sourcePoolSize = 3,
            supportCount
        });

        return Create(
            eventId,
            siteId,
            "bounded_integer",
            ownerId,
            ordinal,
            NativeRandomIndexConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query a Hive/Glory room-placement Fisher-Yates swap using its current
    /// caller-declared bounded support.
    /// </summary>
    public static ChanceLawV111 QueryLaterActRoomAssignmentSwap(
        int act, int ordinal, int supportCount)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(
                nameof(act), "Later-act room placement supports Hive or Glory.");
        var ownerId = $"map:act:{act}";
        return QueryMapRoomAssignmentSwap(
            PrototypeNativeLaterActMap.RoomAssignmentShuffleSiteId,
            ownerId, ordinal, supportCount);
    }

    /// <summary>
    /// Query one Overgrowth room-placement Fisher-Yates swap. The owner binds
    /// the room type, attempt, and ordered-pool fingerprint.
    /// </summary>
    public static ChanceLawV111 QueryOvergrowthRoomAssignmentSwap(
        string ownerId, int ordinal, int supportCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (!IsOvergrowthRoomAssignmentOwner(ownerId))
            throw new ArgumentException(
                "Owner ID must identify an Act 1 room type, placement attempt, and pool fingerprint.",
                nameof(ownerId));
        return QueryMapRoomAssignmentSwap(
            PrototypeNativeOvergrowthMap.RoomAssignmentShuffleSiteId,
            ownerId, ordinal, supportCount);
    }

    private static ChanceLawV111 QueryMapRoomAssignmentSwap(
        string siteId, string ownerId, int ordinal, int supportCount)
    {
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (supportCount < 2)
            throw new ArgumentOutOfRangeException(
                nameof(supportCount),
                "A Fisher-Yates swap requires a bounded support of at least two.");

        var indexWeights = NativeNextIntGridWeights(supportCount);
        var outcomes = indexWeights.Select((weight, index) =>
            new ChanceLawOutcomeV111($"swap_index:{index}", weight))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            ownerId,
            ordinal,
            supportCount
        });

        return Create(
            eventId,
            siteId,
            "bounded_integer",
            ownerId,
            ordinal,
            NativeRandomIndexConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static ChanceLawV111 QueryMapPathStart(
        string siteId, string ownerId, int act, int ordinal)
    {
        const int supportCount = PrototypeNativeLaterActMap.MapWidth;
        var indexWeights = NativeNextIntGridWeights(supportCount);
        var outcomes = indexWeights.Select((weight, index) =>
            new ChanceLawOutcomeV111(
                $"path_column:{index}", weight)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            act,
            ownerId,
            supportCount,
            orderedColumns = Enumerable.Range(0, supportCount).ToArray()
        });

        return Create(
            eventId,
            siteId,
            "bounded_integer",
            ownerId,
            ordinal,
            NativeRandomIndexConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static bool IsOvergrowthPathStartOwner(string ownerId)
    {
        var parts = ownerId.Split(':');
        if (parts.Length != 5 || parts[0] != "act" || parts[1] != "1" ||
            parts[2] != "path" ||
            !int.TryParse(parts[3], NumberStyles.None,
                CultureInfo.InvariantCulture, out var pathOrdinal) ||
            pathOrdinal is < 0 or > 6)
            return false;
        return parts[4] == "start" ||
            pathOrdinal == 1 && parts[4] == "start_retry";
    }

    private static bool IsOvergrowthPathDeltaOwner(string ownerId)
    {
        var parts = ownerId.Split(':');
        return parts.Length == 7 && parts[0] == "act" && parts[1] == "1" &&
            parts[2] == "path" &&
            int.TryParse(parts[3], NumberStyles.None,
                CultureInfo.InvariantCulture, out var pathOrdinal) &&
            pathOrdinal is >= 0 and <= 6 && parts[4] == "floor" &&
            int.TryParse(parts[5], NumberStyles.None,
                CultureInfo.InvariantCulture, out var floorOrdinal) &&
            floorOrdinal is >= 1 and < PrototypeNativeOvergrowthMap.RoomRows &&
            parts[6] == "deltas";
    }

    private static bool IsOvergrowthRoomAssignmentOwner(string ownerId)
    {
        var parts = ownerId.Split(':');
        return parts.Length == 8 && parts[0] == "act" && parts[1] == "1" &&
            parts[2] == "room" &&
            parts[3] is "Rest" or "Shop" or "Elite" or "Unknown" &&
            parts[4] == "attempt" &&
            int.TryParse(parts[5], NumberStyles.None,
                CultureInfo.InvariantCulture, out var attempt) &&
            attempt is >= 0 and < 3 && parts[6] == "pool" &&
            parts[7].Length == 64 && parts[7].All(Uri.IsHexDigit);
    }

    public static ChanceLawV111 QueryLaterActDarvHiveAssignment(int ordinal)
    {
        var siteId = ExperimentalSilentThreeActRouteV111
            .DarvHiveAssignmentSiteId;
        const string ownerId = "run:darv_assignment:hive";
        var outcomes = new[]
        {
            new ChanceLawOutcomeV111("darv_assignment:none", 1L),
            new ChanceLawOutcomeV111("darv_assignment:hive", 1L)
        };
        return CreateDeclaredUnverifiedIndexLaw(
            siteId, ownerId, ordinal,
            "uniform-two-way-darv-assignment-unverified",
            new { siteId, ownerId, ordinal },
            "equal-index-mass-over-two-way-darv-assignment-unverified",
            outcomes, operationKind: "bounded_integer");
    }

    public static ChanceLawV111 QueryLaterActDarvGloryAssignment(
        bool hiveReceivesDarv, int ordinal)
    {
        var siteId = ExperimentalSilentThreeActRouteV111
            .DarvGloryAssignmentSiteId;
        const string ownerId = "run:darv_assignment:glory";
        ChanceLawOutcomeV111[] outcomes = hiveReceivesDarv
            ? [new ChanceLawOutcomeV111("darv_assignment:none", 1L)]
            : [
                new ChanceLawOutcomeV111("darv_assignment:none", 1L),
                new ChanceLawOutcomeV111("darv_assignment:glory", 1L)
            ];
        return CreateDeclaredUnverifiedIndexLaw(
            siteId, ownerId, ordinal,
            "uniform-remaining-darv-assignment-unverified",
            new { siteId, ownerId, ordinal, hiveReceivesDarv },
            "equal-index-mass-over-remaining-darv-assignment-unverified",
            outcomes, operationKind: "bounded_integer");
    }

    public static ChanceLawV111 QueryLaterActAncientAssignment(
        int act, bool includeDarv, int ordinal)
    {
        var choices = ExperimentalSilentThreeActRouteV111.AncientPool(
            act, includeDarv);
        var siteId = ExperimentalSilentThreeActRouteV111
            .AncientAssignmentSiteId;
        var ownerId = $"act:{act}:include_darv:{includeDarv}:pool:"
            + CanonicalJson.Sha256(choices);
        var outcomes = choices.Select(choiceId =>
            new ChanceLawOutcomeV111("ancient_event:" + choiceId, 1L))
            .ToArray();
        return CreateDeclaredUnverifiedIndexLaw(
            siteId, ownerId, ordinal,
            "uniform-source-ordered-later-act-ancient-unverified",
            new { siteId, act, includeDarv, ownerId, orderedChoiceIds = choices },
            "equal-index-mass-over-declared-later-act-ancient-pool-unverified",
            outcomes);
    }

    private static ChanceLawV111 CreateDeclaredUnverifiedIndexLaw(
        string siteId, string ownerId, int ordinal,
        string conditioningPolicyId, object conditioning,
        string probabilityProvenance, ChanceLawOutcomeV111[] outcomes,
        string operationKind = "choose_index")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conditioningPolicyId);
        ArgumentNullException.ThrowIfNull(conditioning);
        ArgumentException.ThrowIfNullOrWhiteSpace(probabilityProvenance);
        ArgumentNullException.ThrowIfNull(outcomes);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var conditioningFingerprint = CanonicalJson.Sha256(conditioning);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        return Create(
            eventId,
            siteId,
            operationKind,
            ownerId,
            ordinal,
            conditioningPolicyId,
            conditioningFingerprint,
            probabilityProvenance,
            outcomes);
    }

    private static ChanceLawV111 QueryActOneBossEncounter(
        string siteId, int ordinal)
    {
        if (ordinal != 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Act 1 boss selection has exactly one draw at ordinal zero.");

        var (bossPool, ownerPrefix) = siteId switch
        {
            "run.encounter.overgrowth_boss.index_draw" =>
                (PrototypeContent.OvergrowthBossEncounterPool,
                    "run:act:1:boss_pool:"),
            PrototypeNativeUnderdocks.BossEncounterIndexSiteId =>
                (PrototypeNativeUnderdocks.NativeBossEncounterIds,
                    "act:1:underdocks:boss_pool:"),
            _ => throw new NotSupportedException(
                $"Act 1 boss law does not support site '{siteId}'.")
        };
        var ownerId = ownerPrefix
            + CanonicalJson.Sha256(bossPool);
        var outcomes = bossPool.Select(encounterId =>
            new ChanceLawOutcomeV111(
                "encounter:" + encounterId, 1L)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            ownerId,
            ordinal,
            orderedEncounterIds = bossPool
        });

        return Create(
            eventId,
            siteId,
            "choose_index",
            ownerId,
            ordinal,
            "uniform-source-ordered-boss-index-unverified",
            conditioningFingerprint,
            "equal-index-mass-over-configured-underdocks-boss-pool-unverified",
            outcomes);
    }
}
