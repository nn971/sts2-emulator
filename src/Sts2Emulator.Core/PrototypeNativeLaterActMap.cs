namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped single-player Hive and Glory map geometry for the pinned
/// v0.111.0 reference. Geometry and placement restrictions are derived from
/// StandardActMap, Hive, Glory and MapPointTypeCounts. The opted-in native
/// map stream uses the exact v111 seed and sampling codec; path traversal,
/// pruning, and room placement still need call-order parity.
/// </summary>
public static class PrototypeNativeLaterActMap
{
    public const string HiveProfileId =
        "native-hive-map-structure-v0.111.0-v1";
    public const string GloryProfileId =
        "native-glory-map-structure-v0.111.0-v1";

    public const int MapWidth = 7;
    public const int HiveRoomRows = 14;
    public const int GloryRoomRows = 13;
    public const string PathStartSiteId =
        "map.later_act.path_start_column.index_draw";
    public const string PathDeltaShuffleSiteId =
        "map.later_act.path_delta.shuffle_swap_index";
    public const string GloryRestCountSiteId =
        "map.later_act.glory_rest_count.index_draw";
    public const string RoomAssignmentShuffleSiteId =
        "map.later_act.room_assignment.shuffle_swap_index";
    public const string HiveRestCountGaussianSiteId =
        "map.later_act.hive_rest_count.gaussian_int";
    public const string UnknownCountGaussianSiteId =
        "map.later_act.unknown_count.gaussian_int";

    public static string ProfileId(int act) => act switch
    {
        2 => HiveProfileId,
        3 => GloryProfileId,
        _ => throw new ArgumentOutOfRangeException(nameof(act),
            "Later-act native map geometry supports Hive and Glory only.")
    };

    public static int RoomRows(int act) => act switch
    {
        2 => HiveRoomRows,
        3 => GloryRoomRows,
        _ => throw new ArgumentOutOfRangeException(nameof(act))
    };

    private sealed class Point(int act, int floor, int column)
    {
        public int Act { get; } = act;
        public int Floor { get; } = floor;
        public int Column { get; } = column;
        public PrototypeRoomType? Room { get; set; }
        public HashSet<Point> Children { get; } = [];
        public HashSet<Point> Parents { get; } = [];
        public string Id => $"{Act}:{Floor}:{Column}";
    }

    /// <summary>
    /// RNG adapter for the existing source-shaped map algorithm. The legacy
    /// entry point retains its original six-stream behavior; the native path
    /// consumes exclusively the independent act-local context stream.
    /// </summary>
    private sealed class MapRandom(
        RngBundle bundle, string streamId, bool native, string ownerId)
    {
        private readonly NativeRandomIndexProviderV111 _nativeProvider = new();
        private readonly NativeRandomGaussianIntProviderV111
            _nativeGaussianProvider = new();
        private readonly Dictionary<string, int> _ordinals = new(
            StringComparer.Ordinal);

        private int NextOrdinal(string siteId)
        {
            var ordinal = _ordinals.GetValueOrDefault(siteId);
            _ordinals[siteId] = ordinal + 1;
            return ordinal;
        }

        public int NextInt(int maxExclusive, string siteId)
        {
            if (siteId is not (PathStartSiteId or GloryRestCountSiteId))
                throw new NotSupportedException(
                    $"Later-act map index adapter does not support site '{siteId}'.");
            if (!native)
                return LegacyRandomChoiceV111.ChooseBoundedInteger(
                    bundle,
                    siteId,
                    streamId,
                    ownerId + ":" + siteId,
                    maxExclusive,
                    NextOrdinal(siteId)).SelectedIndex;
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var decision = StochasticProviderScopeV111.ResolveIndex(
                _nativeProvider,
                new RandomIndexRequestV111(
                    siteId, streamId, ownerId, NextOrdinal(siteId),
                    Array.Empty<int>(), OperationKind: "bounded_integer",
                    BoundedCount: maxExclusive), bundle);
            return decision.SelectedIndex;
        }

        public void Shuffle<T>(T[] values, string siteId)
        {
            if (siteId is not (PathDeltaShuffleSiteId or
                    RoomAssignmentShuffleSiteId))
                throw new NotSupportedException(
                    $"Later-act map shuffle adapter does not support site '{siteId}'.");
            if (!native)
            {
                var legacyOrdinal = _ordinals.GetValueOrDefault(siteId);
                var legacyChoices = LegacyFisherYatesV111.ShuffleInPlace(
                    values,
                    bundle,
                    streamId,
                    siteId,
                    ownerId + ":" + siteId,
                    legacyOrdinal);
                _ordinals[siteId] = legacyOrdinal + legacyChoices.Length;
                return;
            }
            var ordinal = _ordinals.GetValueOrDefault(siteId);
            var choices = NativeFisherYatesV111.ShuffleInPlace(
                values, bundle, _nativeProvider, streamId, ownerId,
                siteId, ordinal);
            _ordinals[siteId] = ordinal + choices.Length;
        }

        public int NextGaussianInt(
            int mean, int standardDeviation, int min, int max, string siteId)
        {
            if (siteId is not (HiveRestCountGaussianSiteId or
                    UnknownCountGaussianSiteId))
                throw new NotSupportedException(
                    $"Later-act map Gaussian adapter does not support site '{siteId}'.");
            if (!native)
                return SampleTruncatedGaussian(
                    bundle, streamId, ownerId, siteId,
                    mean, standardDeviation, min, max,
                    NextOrdinal(siteId));
            var decision = StochasticProviderScopeV111.ResolveGaussianInt(
                _nativeGaussianProvider,
                new RandomGaussianIntRequestV111(
                    siteId, streamId, ownerId, NextOrdinal(siteId),
                    mean, standardDeviation, min, max), bundle);
            return decision.SelectedValue;
        }
    }

    /// <summary>
    /// Historical prototype adapter. Retain for old fixtures and consumers.
    /// This entry point advances only the supplied prototype 'map' stream.
    /// </summary>
    public static MapState Generate(RngBundle rng, int act, int ascension = 0)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return GenerateCore(new MapRandom(
            rng, "map", native: false, ownerId: "map:prototype"),
            act, ascension);
    }

    /// <summary>
    /// Opt-in source-shaped Hive/Glory generator using the *native* map
    /// context RNG. Neither the caller's persistent 12+3 native streams
    /// nor its six legacy streams are consumed during map generation.
    /// </summary>
    public static MapState GenerateNative(string seed, int act, int ascension = 0) =>
        GenerateNativeWithAudit(seed, act, ascension).Map;

    /// <summary>
    /// Return the final act-local PRNG snapshot for exact-seed auditing.
    /// This measures the current emulator map algorithm's draws, not yet
    /// the exact native StandardActMap call sequence.
    /// </summary>
    public static (MapState Map, NativeRngSnapshot Rng)
        GenerateNativeWithAudit(string seed, int act, int ascension = 0)
    {
        _ = ProfileId(act);
        var stream = NativeRngV111Contexts.ForActMap(seed, act);
        var bundle = new RngBundle([stream]);
        var map = GenerateCore(
            new MapRandom(bundle, stream.StreamId, native: true,
                ownerId: $"map:act:{act}"),
            act, ascension);
        return (map, NativeRngV111.ToSnapshot(
            bundle.GetStream(stream.StreamId)));
    }

    private static MapState GenerateCore(
        MapRandom random, int act, int ascension)
    {
        var profile = ProfileId(act);
        if (ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        var roomRows = RoomRows(act);
        var bossFloor = roomRows + 1;
        var hasSecondBoss = act == 3 && ascension >= 10;
        var treasureFloor = bossFloor - 7;
        var points = new Dictionary<(int Floor, int Column), Point>();
        Point Get(int floor, int col)
        {
            var key = (floor, col);
            if (!points.TryGetValue(key, out var point))
            {
                point = new Point(act, floor, col);
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
            var start = random.NextInt(MapWidth, PathStartSiteId);
            if (path == 1)
            {
                while (starts.Contains(start))
                {
                    start = random.NextInt(MapWidth, PathStartSiteId);
                }
            }
            starts.Add(start);

            var current = Get(1, start);
            for (var row = 1; row < roomRows; row++)
            {
                var deltas = new[] { -1, 0, 1 };
                random.Shuffle(deltas, PathDeltaShuffleSiteId);
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

        var boss = Get(bossFloor, 3);
        foreach (var top in points.Values.Where(point =>
                     point.Floor == roomRows).ToArray())
        {
            Connect(top, boss);
        }
        if (hasSecondBoss)
            Connect(boss, Get(bossFloor + 1, 3));

        foreach (var point in points.Values)
        {
            if (point.Floor == 1)
            {
                point.Room = PrototypeRoomType.Combat;
            }
            else if (point.Floor == treasureFloor)
            {
                point.Room = PrototypeRoomType.Treasure;
            }
            else if (point.Floor == roomRows)
            {
                point.Room = PrototypeRoomType.Rest;
            }
            else if (point.Floor == bossFloor)
            {
                point.Room = PrototypeRoomType.Boss;
            }
            else if (hasSecondBoss && point.Floor == bossFloor + 1)
            {
                point.Room = PrototypeRoomType.Boss;
            }
        }

        // Hive.GetMapPointTypes: truncated rounded Gaussian
        // rest 6..7; Glory: NextInt(5,7) gives 5 or 6.
        // Both have StandardRandomUnknownCount minus one (9..13).
        // Geometry/quota compatibility is not RNG stream parity.
        var restCount = act == 2
            ? random.NextGaussianInt(
                6, 1, 6, 7, HiveRestCountGaussianSiteId)
            : 5 + random.NextInt(2, GloryRestCountSiteId);
        var unknownCount =
            random.NextGaussianInt(
                12, 1, 10, 14, UnknownCountGaussianSiteId) - 1;
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
                random.Shuffle(order, RoomAssignmentShuffleSiteId);
                foreach (var point in order)
                {
                    if (remaining == 0)
                    {
                        break;
                    }

                    if (!CanAssign(type, point, roomRows))
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
                act,
                point.Floor,
                point.Room!.Value,
                point.Children.OrderBy(child => child.Column)
                    .Select(child => child.Id).ToArray()))
            .ToArray();

        return new MapState(
            Nodes: nodes,
            EntryNodeIds: nodes.Where(node => node.Floor == 1)
                .Select(node => node.NodeId).ToArray(),
            GenerationProfileId: profile);
    }

    private static int SampleTruncatedGaussian(
        RngBundle rng,
        string streamId,
        string ownerId,
        string siteId,
        int mean,
        int standardDeviation,
        int min,
        int max,
        int sampleOrdinal)
    {
        while (true)
        {
            var componentOwner = ownerId + ":" + siteId + ":sample:"
                + sampleOrdinal;
            var a = 1.0 - (LegacyRandomChoiceV111.ChooseBoundedInteger(
                rng,
                siteId + ".component",
                streamId,
                componentOwner,
                int.MaxValue).SelectedIndex / (double)int.MaxValue);
            var b = 1.0 - (LegacyRandomChoiceV111.ChooseBoundedInteger(
                rng,
                siteId + ".component",
                streamId,
                componentOwner,
                int.MaxValue).SelectedIndex / (double)int.MaxValue);
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

    private static bool CanAssign(PrototypeRoomType type, Point point, int roomRows)
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

        if (type == PrototypeRoomType.Rest && point.Floor >= roomRows - 2)
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

