using System.Text.Json;
using Sts2Emulator.Core;
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
    private NativeLiveFidelityResult? _latestFidelityResult;

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

    public NativeLiveFidelityResult? LatestFidelityResult
    {
        get { lock (_sync) return _latestFidelityResult; }
    }

    /// <summary>Fidelity results are accepted only from a separately
    /// authenticated coordinator at an observed event cursor in the current
    /// source combat. They are diagnostic claims, never parity certificates.</summary>
    public string? AcceptFidelityResult(NativeLiveFidelityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (_sync)
        {
            if (result.Schema != NativeLiveFidelityResult.SchemaId
                || !NativeLiveFidelityResult.AllowedStatuses.Contains(result.Status)
                || result.Reason is null || result.Reason.Length > 300
                || result.PlayerCheckpointsMatched < 0
                || result.EnemyCheckpointsDeferred < 0
                || result.EmulatorActionsStepped < 0
                || result.FirstDifferencePath?.Length > 300
                || result.FirstDifferenceReason?.Length > 300)
                return "INVALID_FIDELITY_RESULT";
            if (_runId is null || result.RunId != _runId
                || result.SessionEpoch != _sessionEpoch)
                return "STALE_FIDELITY_SESSION";
            if (result.ThroughSequence < 1 || result.ThroughSequence > _fidelitySequence)
                return "UNOBSERVED_FIDELITY_CURSOR";
            if (_failure is not null)
                return _failure;
            if (_latestFidelityResult is { } prior)
            {
                if (result == prior) return null;
                if (NativeLiveFidelityResult.IsTerminalFailure(prior.Status))
                    return "FIDELITY_SESSION_BLOCKED";
                if (result.ThroughSequence < prior.ThroughSequence)
                    return "STALE_FIDELITY_RESULT";
                if (result.ThroughSequence == prior.ThroughSequence)
                {
                    if (result == prior) return null;
                    // A stream gap or corrupt event has no acknowledged
                    // successor cursor. Permit an irreversible fail-closed
                    // diagnostic at the last *verified* source sequence.
                    if (!NativeLiveFidelityResult.IsTerminalFailure(prior.Status)
                        && NativeLiveFidelityResult.IsTerminalFailure(result.Status))
                    {
                        _latestFidelityResult = result;
                        return null;
                    }
                    return "CONFLICTING_FIDELITY_RESULT";
                }
            }
            _latestFidelityResult = result;
            return null;
        }
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
            _latestFidelityResult = null;
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

/* The coordinator can report projected agreement and genuine observed
 * differences; native settlement remains uncertified in every result. */
public sealed record NativeLiveFidelityResult(
    string Schema, string RunId, long SessionEpoch, long ThroughSequence,
    string Status, string Reason, int PlayerCheckpointsMatched,
    int EnemyCheckpointsDeferred, int EmulatorActionsStepped,
    string? FirstDifferencePath = null, string? FirstDifferenceReason = null)
{
    public const string SchemaId = "sts2-native-fidelity-result-v1";

    public static bool IsTerminalFailure(string status) =>
        status.StartsWith("BLOCKED_", StringComparison.Ordinal)
        || status is "INCOMPLETE_CAPTURE"
            or "EMULATOR_COORDINATOR_ERROR"
            or "SOURCE_PLAYER_CHECKPOINT_DIFFERENCE_UNCERTIFIED";

    public static readonly IReadOnlySet<string> AllowedStatuses =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "AWAITING_SOURCE_PRELUDE", "AWAITING_FIRST_PLAYER_CHECKPOINT",
            "EMULATOR_BOOTSTRAPPED_UNCERTIFIED",
            "PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED",
            "SOURCE_PLAYER_CHECKPOINT_DIFFERENCE_UNCERTIFIED",
            "BLOCKED_NATIVE_PRELUDE_PAIRING", "BLOCKED_EMULATOR_BOOTSTRAP",
            "BLOCKED_SOURCE_CHRONOLOGY", "BLOCKED_EMULATOR_LEGAL_ACTION",
            "BLOCKED_EMULATOR_STEP", "BLOCKED_NESTED_SOURCE_CHOICE",
            "BLOCKED_SOURCE_AUDIT", "INCOMPLETE_CAPTURE",
            "BLOCKED_STREAM_INTEGRITY", "EMULATOR_COORDINATOR_ERROR",
            "SOURCE_TERMINAL_OBSERVED_UNCERTIFIED"
        };
}
