namespace Sts2Emulator.Core;

/// <summary>
/// Persistent, append-only continuation transcript for a live H8 session.
///
/// v111 must still expose the original materialized History array for explicit
/// snapshot/audit consumers. H8 replay owns a separate compact decision journal;
/// the legacy semantic-event ordinals nevertheless depend on counts by kind.
/// This immutable linked representation makes each normal append O(1) in
/// history length, and each ordinal lookup O(1) in history length. It never
/// clones existing continuation decisions during ordinary advancement.
///
/// Materializing an array is explicit and O(n); it is reserved for audit,
/// legacy snapshot, or a compatibility consumer accessing History directly.
/// No mutable list escapes and forks can share the immutable prefix safely.
/// </summary>
public sealed class H8HistoryTrailV1
{
    private readonly H8HistoryTrailV1? _parent;
    private readonly SemanticPilotContinuationV111? _entry;
    private readonly Dictionary<string, int> _counts;

    private H8HistoryTrailV1(
        H8HistoryTrailV1? parent,
        SemanticPilotContinuationV111? entry,
        int count,
        Dictionary<string, int> counts)
    {
        _parent = parent;
        _entry = entry;
        Count = count;
        _counts = counts;
    }

    public int Count { get; }

    public static H8HistoryTrailV1 FromHistory(
        SemanticPilotContinuationV111[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var trail = new H8HistoryTrailV1(
            null, null, 0, new Dictionary<string, int>(
                StringComparer.Ordinal));
        foreach (var entry in entries)
            trail = trail.Append(entry);
        return trail;
    }

    public int CountKind(string kind) =>
        _counts.TryGetValue(kind, out var value) ? value : 0;

    public H8HistoryTrailV1 Append(SemanticPilotContinuationV111 entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        // The number of distinct continuation kinds is small and fixed by
        // the source mechanics; copying its count map is independent of the
        // potentially thousands of decisions in a long fight.
        var counts = new Dictionary<string, int>(
            _counts, StringComparer.Ordinal);
        counts[entry.Kind] = CountKind(entry.Kind) + 1;
        return new H8HistoryTrailV1(this, entry, checked(Count + 1), counts);
    }

    public SemanticPilotContinuationV111[] Materialize()
    {
        var array = new SemanticPilotContinuationV111[Count];
        var node = this;
        for (var i = Count - 1; i >= 0; --i)
        {
            array[i] = node._entry ?? throw new InvalidDataException(
                "H8 history trail is missing a decision entry.");
            node = node._parent ?? throw new InvalidDataException(
                "H8 history trail has an invalid parent chain.");
        }
        return array;
    }
}
