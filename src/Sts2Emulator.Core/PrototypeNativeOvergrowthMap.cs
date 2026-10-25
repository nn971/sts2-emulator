namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped single-player Act 1 Overgrowth map generator for the pinned
/// v0.111.0 reference. Geometry and placement restrictions are derived from
/// StandardActMap, Overgrowth and MapPointTypeCounts. The historical entry
/// point retains the prototype map stream; the native profile uses the
/// act-local native context stream, with source call-order parity still open.
/// </summary>
public static class PrototypeNativeOvergrowthMap
{
    public const string LegacyGenerationProfileId =
        "native-overgrowth-map-structure-v0.111.0-v1";
    public const string GenerationProfileId =
        "native-overgrowth-map-native-rng-v0.111.0-v1";

    public const string PathStartColumnSiteId =
        "map.overgrowth.path_start_column.index_draw";
    public const string PathDeltaShuffleSiteId =
        "map.overgrowth.path_delta.shuffle_swap_index";
    public const string RestCountGaussianSiteId =
        "map.overgrowth.rest_count.gaussian_component";
    public const string UnknownCountGaussianSiteId =
        "map.overgrowth.unknown_count.gaussian_component";
    public const string RoomAssignmentShuffleSiteId =
        "map.overgrowth.room_assignment.shuffle_swap_index";

    public const int MapWidth = 7;
    public const int RoomRows = 15;
    public const int BossFloor = 16;
    public const int FirstTreasureFloor = 9;
    public const int PreBossRestFloor = 15;

    public static bool IsGenerationProfile(string? profileId) =>
        profileId is GenerationProfileId or LegacyGenerationProfileId;

    private sealed class MapRandom(RngBundle streams, string streamId, bool native)
    {
        private readonly NativeRandomIndexProviderV111 _nativeIndex = new();
        private readonly NativeRandomGaussianIntProviderV111 _nativeGaussian = new();
        private readonly Dictionary<string, int> _ordinals = new(
            StringComparer.Ordinal);

        private int NextOrdinal(string siteId)
        {
            var ordinal = _ordinals.GetValueOrDefault(siteId);
            _ordinals[siteId] = ordinal + 1;
            return ordinal;
        }

        public int NextInt(int maxExclusive, string siteId, string ownerId)
        {
            if (siteId is not (PathStartColumnSiteId))
                throw new NotSupportedException(
                    $"Overgrowth map index adapter does not support site '{siteId}'.");
            if (!native)
                return LegacyRandomChoiceV111.ChooseBoundedInteger(
                    streams, siteId, streamId, ownerId, maxExclusive)
                    .SelectedIndex;
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return StochasticProviderScopeV111.ResolveIndex(
                _nativeIndex, new RandomIndexRequestV111(
                siteId, streamId, ownerId, NextOrdinal(siteId), [],
                OperationKind: "bounded_integer",
                BoundedCount: maxExclusive), streams).SelectedIndex;
        }

        public void Shuffle<T>(T[] values, string siteId, string ownerId)
        {
            if (siteId is not (PathDeltaShuffleSiteId or
                    RoomAssignmentShuffleSiteId))
                throw new NotSupportedException(
                    $"Overgrowth map shuffle adapter does not support site '{siteId}'.");
            if (!native)
            {
                _ = LegacyFisherYatesV111.ShuffleInPlace(
                    values, streams, streamId, siteId, ownerId);
                return;
            }
            var ordinal = _ordinals.GetValueOrDefault(siteId);
            var choices = NativeFisherYatesV111.ShuffleInPlace(
                values, streams, _nativeIndex, streamId, ownerId,
                siteId, ordinal);
            _ordinals[siteId] = ordinal + choices.Length;
        }

        public int NextGaussianInt(
            int mean, int standardDeviation, int min, int max,
            string siteId, string ownerId)
        {
            if (siteId is not (RestCountGaussianSiteId or
                    UnknownCountGaussianSiteId))
                throw new NotSupportedException(
                    $"Overgrowth map Gaussian adapter does not support site '{siteId}'.");
            if (!native)
                return SampleTruncatedGaussian(
                    streams, mean, standardDeviation, min, max,
                    siteId, ownerId);
            return StochasticProviderScopeV111.ResolveGaussianInt(
                _nativeGaussian,
                new RandomGaussianIntRequestV111(
                    siteId, streamId, ownerId, NextOrdinal(siteId),
                    mean, standardDeviation, min, max),
                streams).SelectedValue;
        }
    }

    private sealed class Point(int floor, int column)
    {
        public int Floor { get; } = floor;
        public int Column { get; } = column;
        public PrototypeRoomType? Room { get; set; }
        public HashSet<Point> Children { get; } = [];
        public HashSet<Point> Parents { get; } = [];
        public string Id => $"1:{Floor}:{Column}";
    }

    public static MapState Generate(RngBundle rng, int ascension = 0)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return GenerateCore(new MapRandom(rng, "map", native: false),
            ascension, LegacyGenerationProfileId);
    }

    public static MapState GenerateNative(string seed, int ascension = 0) =>
        GenerateNativeWithAudit(seed, ascension).Map;

    /// <summary>
    /// Generate with StandardActMap's independent act_1_map context stream.
    /// The returned cursor covers this generator's native calls, but does not
    /// certify complete source call-order or final-map parity.
    /// </summary>
    public static (MapState Map, NativeRngSnapshot Rng)
        GenerateNativeWithAudit(string seed, int ascension = 0)
    {
        var stream = NativeRngV111Contexts.ForActMap(seed, 1);
        var bundle = new RngBundle([stream]);
        var map = GenerateCore(
            new MapRandom(bundle, stream.StreamId, native: true),
            ascension, GenerationProfileId);
        return (map, NativeRngV111.ToSnapshot(
            bundle.GetStream(stream.StreamId)));
    }

    private static MapState GenerateCore(
        MapRandom random, int ascension, string generationProfileId)
    {
        var points = new Dictionary<(int Floor, int Column), Point>();
        Point Get(int floor, int col)
        {
            var key = (floor, col);
            if (!points.TryGetValue(key, out var point))
            {
                point = new Point(floor, col);
                points.Add(key, point);
            }

            return point;
        }

        static void Connect(Point from, Point to)
        {
            from.Children.Add(to);
            to.Parents.Add(from);
        }

        var starts = new HashSet<int>();
        // Native StandardActMap traces seven paths. Only the second path's
        // starting column is forced to differ from the first.
        for (var path = 0; path < 7; path++)
        {
            var start = random.NextInt(
                MapWidth, PathStartColumnSiteId,
                $"act:1:path:{path}:start");
            if (path == 1)
            {
                while (starts.Contains(start))
                {
                    start = random.NextInt(
                        MapWidth, PathStartColumnSiteId,
                        $"act:1:path:{path}:start_retry");
                }
            }
            starts.Add(start);

            var current = Get(1, start);
            for (var row = 1; row < RoomRows; row++)
            {
                var deltas = new[] { -1, 0, 1 };
                random.Shuffle(deltas, PathDeltaShuffleSiteId,
                    $"act:1:path:{path}:floor:{row}:deltas");
                Point? chosen = null;
                foreach (var delta in deltas)
                {
                    var destination = Math.Clamp(
                        current.Column + delta, 0, MapWidth - 1);
                    // Crossing diagonals would exchange the left/right order
                    // of two paths within a single floor transition.
                    var crosses = points.Values.Any(other =>
                        other.Floor == row
                        && other.Column != current.Column
                        && other.Children.Any(child =>
                            child.Floor == row + 1
                            && (current.Column < other.Column
                                && destination > child.Column
                                || current.Column > other.Column
                                && destination < child.Column)));
                    if (!crosses)
                    {
                        chosen = Get(row + 1, destination);
                        break;
                    }
                }

                if (chosen is null)
                {
                    throw new InvalidOperationException(
                        "Native Overgrowth map path has no noncrossing continuation.");
                }

                Connect(current, chosen);
                current = chosen;
            }
        }

        var boss = Get(BossFloor, 3);
        foreach (var top in points.Values.Where(point =>
                     point.Floor == RoomRows).ToArray())
        {
            Connect(top, boss);
        }

        foreach (var point in points.Values)
        {
            if (point.Floor == 1)
            {
                point.Room = PrototypeRoomType.Combat;
            }
            else if (point.Floor == FirstTreasureFloor)
            {
                point.Room = PrototypeRoomType.Treasure;
            }
            else if (point.Floor == PreBossRestFloor)
            {
                point.Room = PrototypeRoomType.Rest;
            }
            else if (point.Floor == BossFloor)
            {
                point.Room = PrototypeRoomType.Boss;
            }
        }

        // Native Overgrowth requests two truncated, rounded Gaussian
        // samples: rest (mean 7, sigma 1, clamp [6,7]) and unknown
        // (mean 12, sigma 1, clamp [10,14]). Native mode now routes these
        // through the typed native Gaussian provider; the source generator's
        // exact call order is still a separate parity question.
        var restCount = random.NextGaussianInt(
            7, 1, 6, 7, RestCountGaussianSiteId,
            "act:1:rest_count");
        var unknownCount = random.NextGaussianInt(
            12, 1, 10, 14, UnknownCountGaussianSiteId,
            "act:1:unknown_count");
        var quotas = new (PrototypeRoomType Type, int Count)[]
        {
            (PrototypeRoomType.Rest, restCount),
            (PrototypeRoomType.Shop, 3),
            (PrototypeRoomType.Elite, ascension >= 1 ? 8 : 5),
            (PrototypeRoomType.Unknown, unknownCount)
        };

        var unassigned = points.Values.Where(point =>
            point.Room is null).ToArray();
        foreach (var (type, count) in quotas)
        {
            var remaining = count;
            for (var attempt = 0; attempt < 3 && remaining > 0; attempt++)
            {
                var order = unassigned.Where(point => point.Room is null)
                    .OrderBy(point => point.Floor)
                    .ThenBy(point => point.Column)
                    .ToArray();
                random.Shuffle(order, RoomAssignmentShuffleSiteId,
                    "act:1:room:" + type + ":attempt:" + attempt + ":pool:"
                        + CanonicalJson.Sha256(order.Select(point => point.Id)
                            .ToArray()));
                foreach (var point in order)
                {
                    if (remaining == 0)
                    {
                        break;
                    }

                    if (!CanAssign(type, point))
                    {
                        continue;
                    }

                    point.Room = type;
                    remaining--;
                }
            }
        }

        foreach (var point in unassigned.Where(point => point.Room is null))
        {
            point.Room = PrototypeRoomType.Combat;
        }

        var nodes = points.Values
            .OrderBy(point => point.Floor)
            .ThenBy(point => point.Column)
            .Select(point => new MapNodeState(
                point.Id,
                1,
                point.Floor,
                point.Room!.Value,
                point.Children.OrderBy(child => child.Column)
                    .Select(child => child.Id).ToArray()))
            .ToArray();

        return new MapState(
            Nodes: nodes,
            EntryNodeIds: nodes.Where(node => node.Floor == 1)
                .Select(node => node.NodeId).ToArray(),
            GenerationProfileId: generationProfileId);
    }

    private static int SampleTruncatedGaussian(
        RngBundle rng,
        int mean,
        int standardDeviation,
        int min,
        int max,
        string siteId,
        string ownerId)
    {
        while (true)
        {
            var a = 1.0 - (LegacyRandomChoiceV111.ChooseBoundedInteger(
                rng, siteId, "map", ownerId, int.MaxValue).SelectedIndex
                / (double)int.MaxValue);
            var b = 1.0 - (LegacyRandomChoiceV111.ChooseBoundedInteger(
                rng, siteId, "map", ownerId, int.MaxValue).SelectedIndex
                / (double)int.MaxValue);
            var standardNormal = Math.Sqrt(-2.0 * Math.Log(a))
                * Math.Sin(2.0 * Math.PI * b);
            var result = (int)Math.Round(
                mean + standardDeviation * standardNormal);
            if (result >= min && result <= max)
            {
                return result;
            }
        }
    }

    private static bool CanAssign(PrototypeRoomType type, Point point)
    {
        if (point.Room is not null)
        {
            return false;
        }

        if (type is PrototypeRoomType.Elite or PrototypeRoomType.Rest
            && point.Floor <= 5)
        {
            return false;
        }

        if (type == PrototypeRoomType.Rest && point.Floor >= RoomRows - 2)
        {
            return false;
        }

        if (type is PrototypeRoomType.Rest or PrototypeRoomType.Elite
            or PrototypeRoomType.Treasure or PrototypeRoomType.Shop
            && point.Parents.Concat(point.Children).Any(other => other.Room == type))
        {
            return false;
        }

        if (type is PrototypeRoomType.Rest or PrototypeRoomType.Elite
            or PrototypeRoomType.Shop or PrototypeRoomType.Unknown)
        {
            if (point.Parents.SelectMany(parent => parent.Children)
                .Any(sibling => sibling != point && sibling.Room == type))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// An opt-in native-structure run. Does not alter the established six-floor
/// prototype environment used by existing JSONL consumers and benchmarks.
/// </summary>
public static class PrototypeNativeOvergrowthRunFactory
{
    public static RunState Create(string seed, int ascension = 0) =>
        CreateWithAudit(seed, ascension).State;

    public static (RunState State, NativeNeowOfferAuditV111 NeowOffers)
        CreateWithAudit(
            string seed,
            int ascension = 0,
            IRandomIndexProviderV111? neowProvider = null,
            IRandomIndexProviderV111? startRunProvider = null,
            bool generateNativeMap = true)
    {
        var engine = new PrototypeGameEngine();
        RunState state;
        if (startRunProvider is null)
        {
            state = engine.Step(
                PrototypeGameFactory.Create(seed, ascension),
                GameAction.Empty("start_run")).State;
        }
        else
        {
            using var providerScope = StochasticProviderScopeV111.Enter(
                indexProvider: startRunProvider,
                overrideExplicitProviders: true);
            state = engine.Step(
                PrototypeGameFactory.Create(seed, ascension),
                GameAction.Empty("start_run")).State;
        }
        var world = state.World
            ?? throw new InvalidOperationException("Run did not initialize.");

        // The legacy prototype boot generates its own map first. Reset only
        // the map substream; the source-shaped replacement map consumes the
        // separate act_1_map context stream and leaves prototype RNG alone.
        var rng = state.Stochastic.PrototypeRng;
        var clean = PrototypeRng.CreateBundle(seed);
        rng.ReplaceStream("map", clean.GetStream("map").Fork());

        var map = generateNativeMap
            ? PrototypeNativeOvergrowthMap.GenerateNative(seed, ascension)
            : world.Map with
            {
                GenerationProfileId =
                    PrototypeNativeOvergrowthMap.LegacyGenerationProfileId
            };
        // The A5 native curse belongs to run initialization rather
        // than a random reward. Our A0 profile retains the previously
        // captured Restlessness starter for the existing training gate;
        // at A5+ that slot carries native Ascender's Bane instead.
        var additionalStarterCard = new CardInstance(
            world.NextCardInstanceId,
            ascension >= 5
                ? "proto.curse.ascenders_bane"
                : "proto.common.restlessness",
            0,
            PrototypeJson.EmptyObject());

        // The native Neow event sets HP to zero before healing the
        // player. Weary Traveler (A2+) grants only 80% of max HP.
        // Tight Belt (A4+) removes one of the three native potion slots.
        var neowHp = ascension >= 2
            ? (int)(state.Player.MaxHp * 0.8m)
            : state.Player.MaxHp;
        var nativePotionSlots = ascension >= 4 ? 2 : 3;
        var neowOffers = NativeNeowEventV111.GenerateInitialOptions(
            seed, neowProvider);

        var result = state with
        {
            Player = state.Player with
            {
                Hp = neowHp,
                PotionSlots = new PotionInstance?[nativePotionSlots],
                Deck = state.Player.Deck.Append(additionalStarterCard).ToArray()
            },
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, rng),
            World = world.AppendEventId(
                PrototypeNativeOvergrowthEvents.NeowEventId) with
            {
                Map = map,
                NativeOvergrowthOpening = true,
                NextCardInstanceId = world.NextCardInstanceId + 1,
                UnknownRoomOdds = new PrototypeUnknownRoomOddsState(),
                Event = new EventState(
                    PrototypeNativeOvergrowthEvents.NeowEventId,
                    OfferedChoiceIds: neowOffers.OfferedChoiceIds,
                    NativeEventRng: neowOffers.NativeEventRng,
                    NativeEventRngProviderMode:
                        neowOffers.ProviderMode == "native-v111"
                            ? null : neowOffers.ProviderMode)
            },
            Phase = RunPhase.Event
        };
        return (result, neowOffers);
    }

    public static ReferenceNatureOvergrowthRunResultV111
        CreateWithReferenceNature(string seed, int ascension,
            ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(initialNatureState);
        initialNatureState.Validate();
        var provider = new ReferenceNatureOvergrowthBossIndexProviderV111(
            initialNatureState);
        var runAfterStart = CreateWithAudit(seed, ascension,
            startRunProvider: provider).State;
        var mapSession = ReferenceNatureMapGenerationSessionV111.Begin(
            seed, 1, ascension, provider.NatureState);
        while (!mapSession.Complete)
            mapSession = mapSession.ResolveNext();
        var map = mapSession.Map
            ?? throw new InvalidDataException(
                "ReferenceNature Overgrowth map generation did not complete.");
        var run = runAfterStart with
        {
            Stochastic = RunStochasticStateV111.WithReferenceNature(
                runAfterStart.Stochastic, mapSession.NatureState),
            World = runAfterStart.World! with { Map = map }
        };
        var mapLaws = mapSession.Decisions.Select(decision =>
            ChanceLawEngineV111.QueryNativeMapRequest(
                decision.ToRequest())).ToArray();
        var result = new ReferenceNatureOvergrowthRunResultV111(
            run, initialNatureState.Fork(), mapSession.NatureState,
            provider.ChanceLaws.Concat(mapLaws).ToArray(),
            provider.DecisionTranscript.Concat(
                mapSession.DecisionTranscript).ToArray())
        {
            MapGenerationAudit = mapSession.ToAudit()
        };
        result.Validate();
        return result;
    }
}

internal sealed class ReferenceNatureOvergrowthBossIndexProviderV111(
    ReferenceNatureStateV111 initialNatureState) : IRandomIndexProviderV111
{
    private ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        _stochastic = new(
            initialNatureState?.Fork()
                ?? throw new ArgumentNullException(nameof(initialNatureState)),
            initialNatureState?.Fork()
                ?? throw new ArgumentNullException(nameof(initialNatureState)),
            []);
    private readonly List<ChanceLawV111> _chanceLaws = [];

    public string Mode => ReferenceNatureV111.ProviderId;
    public ReferenceNatureStateV111 NatureState =>
        _stochastic.NatureState.Fork();
    public ChanceLawV111[] ChanceLaws => _chanceLaws
        .Select(law => law.Fork()).ToArray();
    public ReferenceNatureDecisionV111[] DecisionTranscript =>
        _stochastic.DecisionTranscript;

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.ValidateForLegacyProvider();
        if (request.SiteId != "run.encounter.overgrowth_boss.index_draw" ||
            request.StreamId != "combat")
        {
            if (request.SiteId.StartsWith("map.legacy.",
                    StringComparison.Ordinal) && request.StreamId == "map")
                return new LegacyPrototypeRandomIndexProviderV111()
                    .Resolve(request, streams);
            throw new NotSupportedException(
                $"ReferenceNature Overgrowth start does not support '{request.SiteId}'.");
        }

        var bossPool = PrototypeContent.OvergrowthBossEncounterPool;
        var expectedOwner = "run:act:1:boss_pool:" +
            CanonicalJson.Sha256(bossPool);
        if (_stochastic.DecisionTranscript.Length != 0 ||
            request.OwnerId != expectedOwner ||
            request.Ordinal != 0 || request.OperationKind != "choose_index" ||
            request.SupportCount != bossPool.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, bossPool.Length)) ||
            !streams.TryGetStream("combat", out var combatStream) ||
            combatStream.Codec != PrototypeRng.Codec)
            throw new InvalidDataException(
                "ReferenceNature Overgrowth boss request differs from its run-start boundary.");

        var law = ChanceLawEngineV111.QueryOvergrowthBossEncounter(0);
        if (law.SiteId != request.SiteId || law.OwnerId != request.OwnerId ||
            law.Ordinal != request.Ordinal ||
            law.OperationKind != request.OperationKind ||
            law.Outcomes.Length != request.SupportCount)
            throw new InvalidDataException(
                "ReferenceNature Overgrowth boss law differs from its source request.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, _stochastic.NatureState);
        _chanceLaws.Add(law.Fork());
        _stochastic = _stochastic.Advance(selection.NextState,
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex));
        var decision = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, selection.OutcomeIndex, Mode);
        decision.Validate();
        return decision;
    }
}

public sealed record ReferenceNatureOvergrowthRunResultV111(
    RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ChanceLawV111[] ChanceLaws,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private RunState? _run = Run?.Fork();
    private ChanceLawV111[]? _chanceLaws = ChanceLaws?.Select(law =>
        law.Fork()).ToArray();
    private ReferenceNatureMapGenerationAuditV111? _mapGenerationAudit;

    [System.Text.Json.Serialization.JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic { get; init; } = new(InitialNatureState, NatureState,
            DecisionTranscript);

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Stochastic.InitialNatureState;
        init => Stochastic = Stochastic with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Stochastic.NatureState;
        init => Stochastic = Stochastic with { NatureState = value };
    }

    public RunState Run
    {
        get => _run?.Fork()!;
        init => _run = value?.Fork();
    }

    public ChanceLawV111[] ChanceLaws
    {
        get => _chanceLaws?.Select(law => law.Fork()).ToArray()!;
        init => _chanceLaws = value?.Select(law => law.Fork()).ToArray();
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    public ReferenceNatureMapGenerationAuditV111? MapGenerationAudit
    {
        get => _mapGenerationAudit?.Fork();
        init => _mapGenerationAudit = value?.Fork();
    }

    public void Validate()
    {
        var run = Run;
        ArgumentNullException.ThrowIfNull(run);
        Stochastic.Validate();
        var stochastic = Stochastic;
        var world = run.World;
        var laws = ChanceLaws;
        var transcript = stochastic.DecisionTranscript;
        var mapAudit = MapGenerationAudit;
        var encounterPool = world?.Stochastic.ActOneEncounterPool;
        if (laws is null || transcript is null || laws.Length < 2 ||
            transcript.Length != laws.Length || mapAudit is null ||
            run.Stochastic.ReferenceNature is not { } savedCursor ||
            !savedCursor.SameCursor(stochastic.NatureState) ||
            world?.ActOneRegion != PrototypeActOneRegion.Overgrowth ||
            encounterPool is null ||
            encounterPool.Region != PrototypeActOneRegion.Overgrowth ||
            mapAudit.Seed != run.RunSeed || mapAudit.Act != 1 ||
            mapAudit.Ascension != run.Ascension || world.Map is null ||
            CanonicalJson.Sha256(world.Map) != mapAudit.MapFingerprint)
            throw new InvalidDataException(
                "Invalid ReferenceNature Overgrowth run result.");

        mapAudit.Validate();
        var law = laws[0];
        var decision = transcript[0];
        law.Validate();
        decision.Validate();
        var expectedLaw = ChanceLawEngineV111
            .QueryOvergrowthBossEncounter(0);
        if (CanonicalJson.Sha256(expectedLaw) != CanonicalJson.Sha256(law))
            throw new InvalidDataException(
                "Overgrowth boss Chance Law differs from its source query.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, stochastic.InitialNatureState);
        var expectedDecision = new ReferenceNatureDecisionV111(
            law.LawId, law.EventId, selection.OutcomeId,
            selection.OutcomeIndex);
        if (CanonicalJson.Sha256(expectedDecision) !=
                CanonicalJson.Sha256(decision) ||
            selection.OutcomeId !=
                "encounter:" + encounterPool.BossEncounterId ||
            !selection.NextState.SameCursor(
                mapAudit.Stochastic.InitialNatureState))
            throw new InvalidDataException(
                "Overgrowth boss result does not replay to its map-generation cursor.");

        var mapDecisions = mapAudit.Stochastic.DecisionTranscript;
        var mapRequests = mapAudit.Decisions;
        if (laws.Length != checked(mapDecisions.Length + 1) ||
            mapRequests.Length != mapDecisions.Length)
            throw new InvalidDataException(
                "Overgrowth boss and map transcript lengths differ.");
        for (var index = 0; index < mapDecisions.Length; index++)
        {
            var mapLaw = ChanceLawEngineV111.QueryNativeMapRequest(
                mapRequests[index].ToRequest());
            if (CanonicalJson.Sha256(mapLaw) !=
                    CanonicalJson.Sha256(laws[index + 1]) ||
                CanonicalJson.Sha256(mapDecisions[index]) !=
                    CanonicalJson.Sha256(transcript[index + 1]))
                throw new InvalidDataException(
                    $"Overgrowth map law or decision {index} differs from its replayable map prefix.");
        }
        if (!mapAudit.Stochastic.NatureState.SameCursor(
                stochastic.NatureState) ||
            stochastic.NatureState.SampleCount != checked(
                stochastic.InitialNatureState.SampleCount + laws.Length))
            throw new InvalidDataException(
                "Overgrowth boss and map results do not replay from their Nature cursor.");

        PrototypeStateInvariants.Validate(run);
        var initialCombat = PrototypeRng.CreateBundle(run.RunSeed)
            .GetStream("combat");
        var resultCombat = run.Stochastic.PrototypeRng.GetStream("combat");
        if (CanonicalJson.Sha256(initialCombat) !=
            CanonicalJson.Sha256(resultCombat))
            throw new InvalidDataException(
                "ReferenceNature Overgrowth creation advanced the legacy combat cursor.");
    }
}
