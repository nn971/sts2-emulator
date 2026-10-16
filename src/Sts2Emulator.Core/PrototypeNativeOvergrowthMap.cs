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
            if (!native)
                return LegacyRandomChoiceV111.ChooseBoundedInteger(
                    streams, siteId, streamId, ownerId, maxExclusive)
                    .SelectedIndex;
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return _nativeIndex.Resolve(new RandomIndexRequestV111(
                siteId, streamId, ownerId, NextOrdinal(siteId), [],
                OperationKind: "bounded_integer",
                BoundedCount: maxExclusive), streams).SelectedIndex;
        }

        public void Shuffle<T>(T[] values, string siteId, string ownerId)
        {
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
            if (!native)
                return SampleTruncatedGaussian(
                    streams, mean, standardDeviation, min, max,
                    siteId, ownerId);
            return _nativeGaussian.Resolve(new RandomGaussianIntRequestV111(
                siteId, streamId, ownerId, NextOrdinal(siteId),
                mean, standardDeviation, min, max), streams).SelectedValue;
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
        return (map, NativeRngV111.ToSnapshot(bundle.Streams[0]));
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
    public static RunState Create(string seed, int ascension = 0)
    {
        var engine = new PrototypeGameEngine();
        var state = engine.Step(
            PrototypeGameFactory.Create(seed, ascension),
            GameAction.Empty("start_run")).State;
        var world = state.World
            ?? throw new InvalidOperationException("Run did not initialize.");

        // The legacy prototype boot generates its own map first. Reset only
        // the map substream; the source-shaped replacement map consumes the
        // separate act_1_map context stream and leaves prototype RNG alone.
        var rng = state.Rng.Fork();
        var clean = PrototypeRng.CreateBundle(seed);
        var index = Array.FindIndex(rng.Streams, stream =>
            stream.StreamId == "map");
        var cleanIndex = Array.FindIndex(clean.Streams, stream =>
            stream.StreamId == "map");
        rng.Streams[index] = clean.Streams[cleanIndex].Fork();

        var map = PrototypeNativeOvergrowthMap.GenerateNative(seed, ascension);
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

        return state with
        {
            Player = state.Player with
            {
                Hp = neowHp,
                PotionSlots = new PotionInstance?[nativePotionSlots],
                Deck = state.Player.Deck.Append(additionalStarterCard).ToArray()
            },
            Rng = rng,
            World = world with
            {
                Map = map,
                NativeOvergrowthOpening = true,
                NextCardInstanceId = world.NextCardInstanceId + 1,
                UnknownRoomOdds = new PrototypeUnknownRoomOddsState(),
                Event = new EventState(
                    PrototypeNativeOvergrowthEvents.NeowEventId,
                    OfferedChoiceIds:
                        PrototypeNativeOvergrowthEvents.GenerateNeowOfferedChoiceIds(rng)),
                EventHistory =
                [
                    PrototypeNativeOvergrowthEvents.NeowEventId
                ]
            },
            Phase = RunPhase.Event
        };
    }
}
