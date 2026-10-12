using System.Text.Json;

namespace Sts2Emulator.Trace;

/// <summary>
/// An append-only semantic journal. Every consumer maintains its own last-seen sequence.
/// Capacity exhaustion is explicit rather than silently discarding parity-critical events.
/// </summary>
public sealed class EventJournal
{
    private readonly object _sync = new();
    private readonly List<NativeEventRecord> _events = new();
    private readonly int _capacity;
    private long _sequence;

    public EventJournal(int capacity = 200_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public long LastSequence
    {
        get { lock (_sync) return _sequence; }
    }

    public NativeEventRecord Append(
        long decisionId, string kind, JsonElement payload, string origin,
        string? actorId = null, string? targetId = null,
        string visibility = "player", long? causalParent = null)
    {
        lock (_sync)
        {
            if (_events.Count >= _capacity)
                throw new InvalidOperationException("EVENT_JOURNAL_CAPACITY_EXCEEDED");
            var evt = new NativeEventRecord("event", ++_sequence, decisionId, kind,
                actorId, targetId, payload.Clone(), origin, visibility, causalParent);
            _events.Add(evt);
            return evt;
        }
    }

    public IReadOnlyList<NativeEventRecord> ReadAfter(long lastSeenSequence, int limit = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        lock (_sync)
        {
            return _events.Where(e => e.EventSeq > lastSeenSequence).Take(limit).ToArray();
        }
    }
}
