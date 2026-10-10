namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped single-player Act 1 Overgrowth map generator for the pinned
/// v0.111.0 reference. Geometry and placement restrictions are derived from
/// StandardActMap, Overgrowth and MapPointTypeCounts. The generator deliberately
/// uses PrototypeRng rather than claiming native map RNG byte-for-byte parity.
/// </summary>
public static class PrototypeNativeOvergrowthMap
{
    public const string GenerationProfileId =
        "native-overgrowth-map-structure-v0.111.0-v1";

    public const int MapWidth = 7;
    public const int RoomRows = 15;
    public const int BossFloor = 16;
    public const int FirstTreasureFloor = 9;
    public const int PreBossRestFloor = 15;

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

    public static MapState Generate(RngBundle rng, int ascension = 0) =>
        GenerateCore(rng, ascension, 1, RoomRows, GenerationProfileId);

    /// <summary>
    /// Source-shaped later-act geometry. Reuses the pinned StandardActMap
    /// connectivity/placement model but not native RNG stream/call ordering.
    /// Act 2 has 14 room rows; Act 3 has 13.
    /// </summary>
    internal static MapState GenerateLaterAct(
        RngBundle rng, int ascension, int act)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(nameof(act));
        return GenerateCore(rng, ascension, act,
            act == 2 ? 14 : 13,
            act == 2
                ? PrototypeNativeLaterActRouting.HiveMapProfile
                : PrototypeNativeLaterActRouting.GloryMapProfile);
    }

    private static MapState GenerateCore(RngBundle rng, int ascension,
        int act, int roomRows, string profileId)
    {
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

        var boss = Get(roomRows + 1, 3);
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
            else if (point.Floor == FirstTreasureFloor)
            {
                point.Room = PrototypeRoomType.Treasure;
            }
            else if (point.Floor == roomRows)
            {
                point.Room = PrototypeRoomType.Rest;
            }
            else if (point.Floor == roomRows + 1)
            {
                point.Room = PrototypeRoomType.Boss;
            }
        }

        // Native Overgrowth requests two truncated, rounded Gaussian
        // samples: rest (mean 7, sigma 1, clamp [6,7]) and unknown
        // (mean 12, sigma 1, clamp [10,14]). Use the same rejection
        // algorithm; output draws remain non-native until the map RNG
        // codec and source call-order are translated.
        var restCount = act switch
        {
            1 => SampleTruncatedGaussian(rng, 7, 1, 6, 7),
            2 => SampleTruncatedGaussian(rng, 6, 1, 6, 7),
            3 => 5 + PrototypeRng.NextInt(rng, "map", 2),
            _ => throw new ArgumentOutOfRangeException(nameof(act))
        };
        var unknownCount = SampleTruncatedGaussian(rng, 12, 1, 10, 14)
            - (act == 1 ? 0 : 1);
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
            GenerationProfileId: profileId);
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
        // the map substream so opting into native geometry is independent of
        // the number of RNG calls consumed by the legacy map generator.
        var rng = state.Rng.Fork();
        var clean = PrototypeRng.CreateBundle(seed);
        var index = Array.FindIndex(rng.Streams, stream =>
            stream.StreamId == "map");
        var cleanIndex = Array.FindIndex(clean.Streams, stream =>
            stream.StreamId == "map");
        rng.Streams[index] = clean.Streams[cleanIndex].Fork();

        var map = PrototypeNativeOvergrowthMap.Generate(rng, ascension);
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
