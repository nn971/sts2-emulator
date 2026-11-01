using System.Text.Json;
using Sts2Emulator.Trace;
using Sts2ReferenceMod.Trace;

namespace Sts2ReferenceMod.Transport;

/// <summary>
/// Independent, append-only public and privileged native event channels.
/// Producers only append to bounded memory and a nonblocking disk queue;
/// HTTP clients and slow disk writers never run on the Godot game thread.
/// </summary>
public sealed class NativeLiveEventHub : IAsyncDisposable
{
    public const string Schema = "sts2-native-live-event-v1";
    public const string PublicChannel = "public";
    public const string FidelityChannel = "fidelity";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly object _sync = new();
    private readonly AsyncTraceWriter _publicWriter;
    private readonly AsyncTraceWriter _fidelityWriter;
    private readonly List<NativeLiveEvent> _publicEvents = [];
    private readonly List<NativeLiveEvent> _fidelityEvents = [];
    private readonly int _capacity;
    private long _publicSequence;
    private long _fidelitySequence;
    private long _sessionEpoch;
    private string? _runId;
    private string? _failure;

    public NativeLiveEventHub(string journalPrefix, int capacity = 200_000,
        int diskQueueCapacity = 8192)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPrefix);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _publicWriter = new AsyncTraceWriter(journalPrefix + ".public.jsonl",
            diskQueueCapacity);
        _fidelityWriter = new AsyncTraceWriter(journalPrefix + ".fidelity.jsonl",
            diskQueueCapacity);
    }

    public string? Failure
    {
        get { lock (_sync) return _failure; }
    }

    public long LastSequence(string channel)
    {
        lock (_sync)
            return channel switch
            {
                PublicChannel => _publicSequence,
                FidelityChannel => _fidelitySequence,
                _ => throw new ArgumentOutOfRangeException(nameof(channel))
            };
    }

    /// <summary>Called at each native combat attach; a new epoch invalidates
    /// prior decision identities without discarding reconnect history.</summary>
    public bool OpenSession(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (_sync)
        {
            if (_failure is not null) return false;
            _runId = runId;
            ++_sessionEpoch;
            var payload = JsonSerializer.SerializeToElement(new
            {
                status = "NATIVE_COMBAT_ATTACHED",
                source_settlement = "UNVERIFIED"
            });
            bool ok = AppendLocked(PublicChannel, "session_started", null,
                null, payload);
            return AppendLocked(FidelityChannel, "session_started", null,
                null, payload) && ok;
        }
    }

    /// <summary>Only the already whitelisted agent boundary can enter the
    /// public event channel. There is no generic public JsonElement writer.</summary>
    public bool PublishPublicBoundary(NativeAgentVisibleBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        lock (_sync)
        {
            if (_runId is null || boundary.SessionId != _runId) return false;
            return AppendLocked(PublicChannel, "decision", boundary.DecisionId,
                null, JsonSerializer.SerializeToElement(boundary, JsonOptions));
        }
    }

    public bool PublishPublicStatus(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        lock (_sync)
            return _runId is not null && AppendLocked(PublicChannel,
                "readiness", null, null,
                JsonSerializer.SerializeToElement(new { status }));
    }

    /// <summary>Fidelity channel alone may contain native action states,
    /// RNG evidence and executor action identities.</summary>
    public bool PublishFidelityRecord(NativeActionEnvelopeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_sync)
            return _runId is not null && AppendLocked(FidelityChannel,
                record.Type, null, record.Ordinal,
                JsonSerializer.SerializeToElement(record, JsonOptions));
    }

    public IReadOnlyList<NativeLiveEvent> ReadAfter(string channel, long cursor,
        int limit = 512)
    {
        if (cursor < 0 || limit is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(cursor));
        lock (_sync)
        {
            var (events, last) = channel switch
            {
                PublicChannel => (_publicEvents, _publicSequence),
                FidelityChannel => (_fidelityEvents, _fidelitySequence),
                _ => throw new ArgumentOutOfRangeException(nameof(channel))
            };
            if (cursor > last)
                throw new InvalidOperationException("FUTURE_EVENT_CURSOR");
            if (_failure is not null)
                throw new InvalidOperationException(_failure);
            return events.Where(e => e.Sequence > cursor).Take(limit).ToArray();
        }
    }

    private bool AppendLocked(string channel, string kind, long? decisionId,
        long? sourceOrdinal, JsonElement payload)
    {
        if (_failure is not null) return false;
        var events = channel == PublicChannel ? _publicEvents : _fidelityEvents;
        var writer = channel == PublicChannel ? _publicWriter : _fidelityWriter;
        if (events.Count >= _capacity)
        {
            _failure = "LIVE_EVENT_JOURNAL_CAPACITY_EXCEEDED";
            return false;
        }
        long next = (channel == PublicChannel ? _publicSequence
            : _fidelitySequence) + 1;
        var evt = new NativeLiveEvent(Schema, channel, _runId!,
            _sessionEpoch, next, kind, decisionId, sourceOrdinal,
            CanonicalJson.Sha256(payload), payload.Clone());
        if (!writer.TryEnqueue(evt))
        {
            _failure = "LIVE_EVENT_DISK_QUEUE_OVERFLOW";
            return false;
        }
        events.Add(evt);
        if (channel == PublicChannel) _publicSequence = next;
        else _fidelitySequence = next;
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await _publicWriter.DisposeAsync().ConfigureAwait(false);
        await _fidelityWriter.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>Per-channel sequences are continuous. A client can reconnect
/// with the last acknowledged sequence without learning privileged traffic.</summary>
public sealed record NativeLiveEvent(
    string Schema, string Channel, string RunId, long SessionEpoch,
    long Sequence, string Kind, long? DecisionId, long? SourceOrdinal,
    string ContentFingerprint, JsonElement Payload);
