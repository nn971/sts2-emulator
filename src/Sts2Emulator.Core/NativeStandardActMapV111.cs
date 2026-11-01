namespace Sts2Emulator.Core;

/// <summary>
/// Experimental singleplayer v111 StandardActMap translation. Owns the
/// content-local "act_1_map" RNG, never the persistent run/player streams.
/// Source: StandardActMap, MapPathPruning, MapPostProcessing,
/// Overgrowth/Underdocks.GetMapPointTypes at STS2-source@706ef1c9.
/// Native game parity remains pending an observed full map/cursor comparison.
/// </summary>
public static class NativeStandardActMapV111
{
    public const string ProfileId = "v111-experimental-native-standard-act1-map-v1";
    public const int MapWidth = 7;
    public const int RoomRows = 15;
    private const int GridRows = 16;

    public sealed record Generation(
        MapState Map, NativeRngSnapshot AfterQuotas,
        NativeRngSnapshot AfterPaths, NativeRngSnapshot AfterTypes,
        NativeRngSnapshot AfterPruning, NativeRngSnapshot FinalRng,
        int RestQuota, int UnknownQuota, int EliteQuota);

    // Exact native MapPointType integer identities (segment keys depend on them).
    private enum Type
    {
        Unassigned, Unknown, Shop, Treasure, RestSite, Monster, Elite, Boss, Ancient
    }

    private sealed class Point(int column, int row)
    {
        public int Col = column;
        public readonly int Row = row;
        public Type Kind = Type.Unassigned;
        public bool Modifiable = true;
        public readonly HashSet<Point> Children = [];
        public readonly HashSet<Point> Parents = [];
        public string Id => $"1:{Row}:{Col}";

        public void Connect(Point child)
        {
            Children.Add(child);
            child.Parents.Add(this);
        }

        public void Disconnect(Point child)
        {
            Children.Remove(child);
            child.Parents.Remove(this);
        }
    }

    public static Generation Generate(string runSeed, int ascension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runSeed);
        if (ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        return new Builder(runSeed, ascension).Generate();
    }

    private sealed class Builder
    {
        private readonly RngBundle _rng;
        private readonly string _streamId;
        private readonly Point?[,] _grid = new Point[MapWidth, GridRows];
        private readonly HashSet<Point> _starts = [];
        private readonly Point _ancient = new(3, 0);
        private readonly Point _boss = new(3, GridRows);
        private readonly int _eliteQuota;
        private int _restQuota;
        private int _unknownQuota;

        public Builder(string seed, int ascension)
        {
            var stream = NativeRngV111Contexts.ForActMap(seed, 1);
            _streamId = stream.StreamId;
            _rng = new RngBundle([stream]);
            _eliteQuota = ascension >= 1 ? 8 : 5;
        }

        private NativeRngSnapshot Snapshot() =>
            NativeRngV111.ToSnapshot(_rng.Streams.Single());

        private int NextInt(int max) => NativeRngV111.NextInt(_rng, _streamId, max);

        private void Shuffle<T>(IList<T> values) =>
            NativeRngV111.Shuffle(_rng, _streamId, values);

        // Native MapPoint.CompareTo sorts by (column,row), not floor first.
        private void StableShuffle(List<Point> points)
        {
            points.Sort((a, b) =>
            {
                var result = a.Col.CompareTo(b.Col);
                return result != 0 ? result : a.Row.CompareTo(b.Row);
            });
            Shuffle(points);
        }

        private Point Get(int col, int row)
        {
            if (col is < 0 or >= MapWidth || row is < 1 or >= GridRows)
                throw new ArgumentOutOfRangeException(nameof(row));
            return _grid[col, row] ??= new Point(col, row);
        }

        private IEnumerable<Point> AllPoints()
        {
            for (var col = 0; col < MapWidth; col++)
                for (var row = 0; row < GridRows; row++)
                    if (_grid[col, row] is { } point)
                        yield return point;
        }

        private IEnumerable<Point> Row(int row)
        {
            for (var col = 0; col < MapWidth; col++)
                if (_grid[col, row] is { } point)
                    yield return point;
        }

        public Generation Generate()
        {
            // ActModel.GetMapPointTypes executes in the StandardActMap ctor
            // BEFORE GenerateMap; two rejection-sampled Gaussians.
            _restQuota = NativeRngV111.NextGaussianInt(_rng, _streamId, 7, 1, 6, 7);
            _unknownQuota = NativeRngV111.NextGaussianInt(_rng, _streamId, 12, 1, 10, 14);
            var quotas = Snapshot();
            GeneratePaths();
            var paths = Snapshot();
            AssignTypes();
            var types = Snapshot();
            PruneAndRepair();
            var pruned = Snapshot();
            CenterGrid();
            SpreadNodes();
            StraightenPaths();
            var nodes = AllPoints().Append(_boss)
                .OrderBy(point => point.Row).ThenBy(point => point.Col)
                .Select(point => new MapNodeState(
                    point.Id, 1, point.Row, RoomType(point.Kind),
                    point.Children.OrderBy(child => child.Col)
                        .Select(child => child.Id).ToArray()))
                .ToArray();
            return new Generation(
                new MapState(
                    Nodes: nodes,
                    EntryNodeIds: nodes.Where(node => node.Floor == 1)
                        .Select(node => node.NodeId).ToArray(),
                    GenerationProfileId: ProfileId),
                quotas, paths, types, pruned, Snapshot(),
                _restQuota, _unknownQuota, _eliteQuota);
        }

        private static PrototypeRoomType RoomType(Type type) => type switch
        {
            Type.Monster => PrototypeRoomType.Combat,
            Type.Unknown => PrototypeRoomType.Unknown,
            Type.Shop => PrototypeRoomType.Shop,
            Type.Treasure => PrototypeRoomType.Treasure,
            Type.RestSite => PrototypeRoomType.Rest,
            Type.Elite => PrototypeRoomType.Elite,
            Type.Boss => PrototypeRoomType.Boss,
            _ => throw new InvalidDataException($"Unassigned native map point {type}.")
        };

        private void GeneratePaths()
        {
            for (var index = 0; index < 7; index++)
            {
                var start = Get(NextInt(MapWidth), 1);
                if (index == 1)
                    while (_starts.Contains(start))
                        start = Get(NextInt(MapWidth), 1);
                _starts.Add(start);
                var current = start;
                while (current.Row < GridRows - 1)
                {
                    var nextCol = ChooseNextColumn(current);
                    var child = Get(nextCol, current.Row + 1);
                    current.Connect(child);
                    current = child;
                }
            }
            foreach (var point in Row(GridRows - 1))
                point.Connect(_boss);
            foreach (var point in Row(1))
                _ancient.Connect(point);
        }

        private int ChooseNextColumn(Point current)
        {
            // StandardActMap.GenerateNextCoord: stable shuffle [-1,0,1].
            var offsets = new[] { -1, 0, 1 };
            Shuffle(offsets);
            foreach (var offset in offsets)
            {
                var col = Math.Clamp(current.Col + offset, 0, MapWidth - 1);
                var movement = col - current.Col;
                if (movement == 0)
                    return col;
                var neighbor = _grid[col, current.Row];
                if (neighbor is null || !neighbor.Children.Any(child =>
                    child.Col - neighbor.Col == -movement))
                    return col;
            }
            throw new InvalidOperationException("Source map path has no noncrossing step.");
        }

        private void AssignTypes()
        {
            foreach (var point in Row(GridRows - 1))
                SetFixed(point, Type.RestSite);
            foreach (var point in Row(GridRows - 7))
                SetFixed(point, Type.Treasure);
            foreach (var point in Row(1))
                SetFixed(point, Type.Monster);
            var pending = new Queue<Type>(
                Enumerable.Repeat(Type.RestSite, _restQuota)
                    .Concat(Enumerable.Repeat(Type.Shop, 3))
                    .Concat(Enumerable.Repeat(Type.Elite, _eliteQuota))
                    .Concat(Enumerable.Repeat(Type.Unknown, _unknownQuota)));
            for (var attempt = 0; attempt < 3 && pending.Count > 0; attempt++)
            {
                var points = AllPoints()
                    .Where(point => point.Kind == Type.Unassigned).ToList();
                StableShuffle(points);
                foreach (var point in points)
                {
                    if (pending.Count == 0)
                        break;
                    point.Kind = NextValid(pending, point);
                }
            }
            foreach (var point in AllPoints())
                if (point.Kind == Type.Unassigned)
                    point.Kind = Type.Monster;
            _boss.Kind = Type.Boss;
            _ancient.Kind = Type.Ancient;
        }

        private static void SetFixed(Point point, Type kind)
        {
            point.Kind = kind;
            point.Modifiable = false;
        }

        private Type NextValid(Queue<Type> pending, Point point)
        {
            // Note: loop bound is the live Queue.Count, matching v111.
            for (var i = 0; i < pending.Count; i++)
            {
                var type = pending.Dequeue();
                if (Valid(type, point))
                    return type;
                pending.Enqueue(type);
            }
            return Type.Unassigned;
        }

        private static bool Valid(Type kind, Point point)
        {
            if (point.Row < 6 && kind is Type.RestSite or Type.Elite)
                return false;
            if (point.Row >= GridRows - 3 && kind == Type.RestSite)
                return false;
            if (kind is Type.Elite or Type.RestSite or Type.Treasure or Type.Shop)
                if (point.Parents.Concat(point.Children).Any(p => p.Kind == kind))
                    return false;
            if (kind is Type.RestSite or Type.Monster or Type.Unknown
                or Type.Elite or Type.Shop)
                if (point.Parents.SelectMany(p => p.Children)
                    .Any(sibling => sibling != point && sibling.Kind == kind))
                    return false;
            return true;
        }

        private void PruneAndRepair()
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                PruneDuplicates();
                var changed = Repair(Type.Shop, 3);
                changed |= Repair(Type.Elite, _eliteQuota);
                changed |= Repair(Type.RestSite, _restQuota);
                changed |= Repair(Type.Unknown, _unknownQuota);
                if (!changed)
                    break;
            }
        }

        private bool Repair(Type type, int target)
        {
            var missing = target - AllPoints().Count(p => p.Kind == type);
            if (missing <= 0)
                return false;
            var points = AllPoints()
                .Where(p => p.Kind == Type.Monster && p.Modifiable).ToList();
            StableShuffle(points);
            var changed = false;
            foreach (var point in points)
            {
                if (missing == 0)
                    break;
                if (!Valid(type, point))
                    continue;
                point.Kind = type;
                missing--;
                changed = true;
            }
            return changed;
        }

        private static List<List<Point>> Paths(Point current)
        {
            if (current.Kind == Type.Boss)
                return [[current]];
            var result = new List<List<Point>>();
            foreach (var child in current.Children)
                foreach (var path in Paths(child))
                {
                    var extended = new List<Point> { current };
                    extended.AddRange(path);
                    result.Add(extended);
                }
            return result;
        }

        private List<List<Point[]>> DuplicateSegments()
        {
            var segments = new SortedDictionary<string, List<Point[]>>(
                StringComparer.Ordinal);
            foreach (var path in Paths(_ancient))
                for (var start = 0; start < path.Count - 1; start++)
                {
                    var startPoint = path[start];
                    if (startPoint.Children.Count <= 1 && startPoint.Row != 0)
                        continue;
                    for (var length = 2; length < path.Count - start; length++)
                    {
                        var end = path[start + length];
                        if (end.Parents.Count < 2)
                            continue;
                        var candidate = path.Skip(start).Take(length + 1).ToArray();
                        var key = SegmentKey(candidate);
                        if (!segments.TryGetValue(key, out var seen))
                            segments[key] = [candidate];
                        else if (!seen.Any(existing => Overlaps(existing, candidate)))
                            seen.Add(candidate);
                    }
                }
            return segments.Values.Where(seen => seen.Count > 1).ToList();
        }

        private static string SegmentKey(Point[] segment)
        {
            var first = segment[0];
            var last = segment[^1];
            var prefix = first.Row == 0
                ? $"0-{last.Col},{last.Row}-"
                : $"{first.Col},{first.Row}-{last.Col},{last.Row}-";
            return prefix + string.Join(",", segment.Select(p => (int)p.Kind));
        }

        private static bool Overlaps(Point[] first, Point[] second)
        {
            if (first.Length < 3 || second.Length < 3)
                return false;
            for (var i = 1; i < first.Length - 1; i++)
                if (first[i] == second[i])
                    return true;
            return false;
        }

        private void PruneDuplicates()
        {
            var iterations = 0;
            var groups = DuplicateSegments();
            while (PruneGroups(groups))
            {
                if (++iterations > 50)
                    throw new InvalidOperationException(
                        "Unable to prune duplicate map segments after 50 iterations.");
                groups = DuplicateSegments();
            }
        }

        private bool PruneGroups(List<List<Point[]>> groups)
        {
            foreach (var group in groups)
            {
                Shuffle(group);
                if (PruneAllButLast(group) != 0)
                    return true;
                if (group.Any(BreakRelationship))
                    return true;
            }
            return false;
        }

        private int PruneAllButLast(IReadOnlyList<Point[]> group)
        {
            var count = 0;
            foreach (var segment in group)
            {
                if (count == group.Count - 1)
                    return count;
                if (PruneSegment(segment))
                    count++;
            }
            return count;
        }

        private bool InMap(Point point) => point.Kind is Type.Ancient or Type.Boss
            || (point.Row < GridRows && _grid[point.Col, point.Row] is not null);

        private bool Removed(Point point) => point.Row < GridRows
            && _grid[point.Col, point.Row] is null;

        private bool PruneSegment(Point[] segment)
        {
            var changed = false;
            for (var i = 0; i < segment.Length - 1; i++)
            {
                var point = segment[i];
                if (!InMap(point))
                    return true;
                if (point.Children.Count > 1 || point.Parents.Count > 1
                    || point.Parents.Any(p => p.Children.Count == 1 && !Removed(p)))
                    continue;
                var suffix = segment.Skip(i).ToArray();
                if (suffix.Any(p => p.Children.Count > 1 && p.Parents.Count == 1))
                    continue;
                if (segment[^1].Parents.Count == 1)
                    return false;
                if (point.Children.Where(c => !segment.Contains(c))
                    .Any(c => c.Parents.Count == 1))
                    continue;
                Remove(point);
                changed = true;
            }
            return changed;
        }

        private void Remove(Point point)
        {
            if (point.Row >= GridRows)
                throw new InvalidOperationException("Cannot remove the boss.");
            _grid[point.Col, point.Row] = null;
            _starts.Remove(point);
            foreach (var child in point.Children.ToArray())
                point.Disconnect(child);
            foreach (var parent in point.Parents.ToArray())
                parent.Disconnect(point);
        }

        private static bool BreakRelationship(Point[] segment)
        {
            var changed = false;
            for (var i = 0; i < segment.Length - 1; i++)
            {
                var point = segment[i];
                if (point.Children.Count < 2)
                    continue;
                var child = segment[i + 1];
                if (child.Parents.Count != 1)
                {
                    point.Disconnect(child);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ColumnEmpty(int col)
        {
            for (var row = 0; row < GridRows; row++)
                if (_grid[col, row] is not null)
                    return false;
            return true;
        }

        private void CenterGrid()
        {
            var leftEmpty = ColumnEmpty(0) && ColumnEmpty(1);
            var rightEmpty = ColumnEmpty(MapWidth - 1) && ColumnEmpty(MapWidth - 2);
            var shift = leftEmpty && !rightEmpty ? -1
                : !leftEmpty && rightEmpty ? 1 : 0;
            if (shift == 0)
                return;
            for (var row = 0; row < GridRows; row++)
                for (var step = 0; step < MapWidth; step++)
                {
                    var col = shift > 0 ? MapWidth - 1 - step : step;
                    var point = _grid[col, row];
                    _grid[col, row] = null;
                    var dest = col + shift;
                    if (dest < 0 || dest >= MapWidth)
                        continue;
                    _grid[dest, row] = point;
                    if (point is not null)
                        point.Col = dest;
                }
        }

        private static HashSet<int> Allowed(Point point)
        {
            var columns = new HashSet<int>(Enumerable.Range(0, MapWidth));
            foreach (var neighbor in point.Parents.Concat(point.Children))
                columns.IntersectWith(Enumerable.Range(
                    Math.Max(0, neighbor.Col - 1),
                    Math.Min(MapWidth - 1, neighbor.Col + 1)
                        - Math.Max(0, neighbor.Col - 1) + 1));
            return columns;
        }

        private static int Gap(int col, IReadOnlyList<Point> row, Point point)
        {
            var result = int.MaxValue;
            foreach (var other in row)
                if (other != point)
                    result = Math.Min(result, Math.Abs(col - other.Col));
            return result;
        }

        private void SpreadNodes()
        {
            for (var row = 0; row < GridRows; row++)
            {
                var nodes = Row(row).ToList();
                bool changed;
                do
                {
                    changed = false;
                    foreach (var point in nodes)
                    {
                        var current = point.Col;
                        var best = current;
                        var maxGap = Gap(current, nodes, point);
                        foreach (var candidate in Allowed(point))
                        {
                            if (candidate == current || _grid[candidate, row] is not null
                                && _grid[candidate, row] != point)
                                continue;
                            var gap = Gap(candidate, nodes, point);
                            if (gap <= maxGap)
                                continue;
                            maxGap = gap;
                            best = candidate;
                        }
                        if (best == current)
                            continue;
                        _grid[current, row] = null;
                        _grid[best, row] = point;
                        point.Col = best;
                        changed = true;
                    }
                } while (changed);
            }
        }

        private void StraightenPaths()
        {
            for (var row = 0; row < GridRows; row++)
                for (var col = 0; col < MapWidth; col++)
                {
                    var point = _grid[col, row];
                    if (point is null || point.Parents.Count != 1
                        || point.Children.Count != 1)
                        continue;
                    var parent = point.Parents.First();
                    var child = point.Children.First();
                    var offset = point.Col < child.Col && point.Col < parent.Col ? 1
                        : point.Col > child.Col && point.Col > parent.Col ? -1 : 0;
                    var dest = col + offset;
                    if (offset == 0 || dest < 0 || dest >= MapWidth
                        || _grid[dest, row] is not null)
                        continue;
                    _grid[col, row] = null;
                    _grid[dest, row] = point;
                    point.Col = dest;
                }
        }
    }
}
