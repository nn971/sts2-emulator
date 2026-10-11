namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped single-player Hive and Glory map geometry for the pinned
/// v0.111.0 reference. Geometry and placement restrictions are derived from
/// StandardActMap, Hive, Glory and MapPointTypeCounts. The generator deliberately
/// uses PrototypeRng rather than claiming native map RNG byte-for-byte parity.
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
    /// Opt-in map content inventory, not a live Hive/Glory run.
    /// Unimplemented regional rewards and encounter bags are gated
    /// at room entry. Uses prototype map RNG until native ownership
    /// and exact path/pruning call order are independently validated.
    /// </summary>
    public static MapState Generate(RngBundle rng, int act, int ascension = 0)
    {
        var profile = ProfileId(act);
        if (ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        var roomRows = RoomRows(act);
        var bossFloor = roomRows + 1;
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
            var start = PrototypeRng.NextInt(rng, "map", MapWidth);
            if (path == 1)
            {
                while (starts.Contains(start))
                {
                    start = PrototypeRng.NextInt(rng, "map", MapWidth);
                }
            }
            starts.Add(start);

            var current = Get(1, start);
            for (var row = 1; row < roomRows; row++)
            {
                var deltas = new[] { -1, 0, 1 };
                PrototypeRng.Shuffle(rng, "map", deltas);
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
        }

        // Hive.GetMapPointTypes: truncated rounded Gaussian
        // rest 6..7; Glory: NextInt(5,7) gives 5 or 6.
        // Both have StandardRandomUnknownCount minus one (9..13).
        // Geometry/quota compatibility is not RNG stream parity.
        var restCount = act == 2
            ? SampleTruncatedGaussian(rng, 6, 1, 6, 7)
            : 5 + PrototypeRng.NextInt(rng, "map", 2);
        var unknownCount =
            SampleTruncatedGaussian(rng, 12, 1, 10, 14) - 1;
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
                PrototypeRng.Shuffle(rng, "map", order);
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
        int mean,
        int standardDeviation,
        int min,
        int max)
    {
        while (true)
        {
            var a = 1.0 - (PrototypeRng.NextInt(
                rng, "map", int.MaxValue) / (double)int.MaxValue);
            var b = 1.0 - (PrototypeRng.NextInt(
                rng, "map", int.MaxValue) / (double)int.MaxValue);
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

