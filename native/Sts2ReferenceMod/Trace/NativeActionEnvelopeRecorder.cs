using System.Text.Json;
using Sts2Emulator.Core;
using Sts2Emulator.Trace;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Independent, non-intervening native action-executor timeline. This sidecar
/// retains callback and history evidence even when the strict decision trace
/// cannot identify a stable player input boundary. It does not claim hook
/// completeness or parity with the emulated effects.
/// </summary>
public sealed class NativeActionEnvelopeRecorder : IAsyncDisposable
{
    public const string Schema = "native-action-envelope-v1";
    private readonly AsyncTraceWriter _writer;
    private readonly Dictionary<object, long> _running =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<long> _activeOrder = [];
    // The native executor may invoke BeforeActionExecuted again on the same
    // action instance after resuming a player-choice suspension.
    private readonly HashSet<object> _pendingResumption =
        new(ReferenceEqualityComparer.Instance);
    private readonly string _gameBuild;
    private long _sequence;
    private long _nextActionId;
    private bool _incomplete;
    private bool _closed;
    private long _frameIndex;
    private string? _lastFrameStateHash;

    public NativeActionEnvelopeRecorder(
        string path, string gameBuild, string runId,
        int queueCapacity = 8192, bool nativeRngHookInstalled = false,
        bool nativeMonsterAiScopeInstalled = false)
    {
        _gameBuild = gameBuild;
        _writer = new AsyncTraceWriter(path, queueCapacity);
        Write(new NativeActionEnvelopeRecord(
            Type: "header", Ordinal: Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                schema = Schema, game_build = gameBuild, run_id = runId,
                started_at_utc = DateTimeOffset.UtcNow,
                source = "native_action_executor_and_combat_history",
                coverage = "callback_order_and_observed_snapshots_only",
                native_rng_hook_installed = nativeRngHookInstalled,
                native_monster_ai_scope_installed = nativeMonsterAiScopeInstalled,
                native_rng_capture_scope = nativeRngHookInstalled
                    ? "post_first_observed_combat_frame"
                    : "hook_disabled",
                rng_source_hook_complete = false,
                exact_rng_parity = "NOT_TESTED",
                native_hook_parity = "NOT_TESTED"
            })));
    }

    public bool IsIncomplete => _incomplete;

    /// <summary>
    /// At most one full snapshot per changed canonical state. Frames can
    /// observe UI-driven nested selections even when no executor action
    /// starts. Sampling never establishes sub-frame hook ordering.
    /// </summary>
    public void ObserveFrame(JsonElement state)
    {
        if (_closed) return;
        ++_frameIndex;
        string hash = CanonicalJson.Sha256(state);
        if (hash == _lastFrameStateHash) return;
        _lastFrameStateHash = hash;
        Write(new NativeActionEnvelopeRecord(
            "frame_state", Next(), State: Snapshot(state),
            Data: JsonSerializer.SerializeToElement(new
            {
                frame_index = _frameIndex,
                state_hash = hash,
                capture_basis = "rendered_frame_poll",
                semantic_hook_order_verified = false
            })));
    }

    public void HistoryBaseline(int existingEntryCount)
    {
        if (_closed) return;
        Write(new NativeActionEnvelopeRecord(
            "history_baseline", Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                existing_entry_count = existingEntryCount,
                note = "Entries before observer attachment have no recorded order"
            })));
    }

    public void ObservationFailure(string stage, string exceptionType)
    {
        if (_closed) return;
        _incomplete = true;
        Write(new NativeActionEnvelopeRecord(
            "observation_failure", Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                stage, exception_type = exceptionType
            })));
    }

    public void Start(
        object action, string type, JsonElement? state,
        GameAction? canonicalAction = null)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(action);
        if (_running.TryGetValue(action, out long existingId))
        {
            if (_pendingResumption.Remove(action))
            {
                if (state is null)
                    ObservationFailure("action_resume_snapshot", "STATE_UNAVAILABLE");
                Write(new NativeActionEnvelopeRecord(
                    "action_resumed", Next(), existingId,
                    NativeType: type, State: Snapshot(state),
                    Data: Data("native_executor_before_action_after_choice", type)));
                return;
            }
            _incomplete = true;
            Write(new NativeActionEnvelopeRecord(
                "anomaly", Next(), existingId,
                NativeType: type,
                Data: Data("duplicate_action_start_without_choice", type)));
            return;
        }
        if (state is null) ObservationFailure("action_start_snapshot", "STATE_UNAVAILABLE");
        long id = ++_nextActionId;
        long? parent = _activeOrder.Count == 0 ? null : _activeOrder[^1];
        _running.Add(action, id);
        _activeOrder.Add(id);
        Write(new NativeActionEnvelopeRecord(
            "action_started", Next(), id, parent, type,
            canonicalAction, Snapshot(state)));
    }

    public void Complete(object action, JsonElement? state)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(action);
        if (!_running.Remove(action, out long id))
        {
            _incomplete = true;
            Write(new NativeActionEnvelopeRecord(
                "anomaly", Next(), Data: Data("orphan_action_complete",
                    action.GetType().FullName ?? action.GetType().Name),
                State: Snapshot(state)));
            return;
        }
        _pendingResumption.Remove(action);
        if (state is null) ObservationFailure("action_complete_snapshot", "STATE_UNAVAILABLE");
        int index = _activeOrder.LastIndexOf(id);
        if (index >= 0) _activeOrder.RemoveAt(index);
        Write(new NativeActionEnvelopeRecord(
            "action_completed", Next(), id,
            NativeType: action.GetType().FullName ?? action.GetType().Name,
            State: Snapshot(state)));
    }

    public void ChoicePause(object action, JsonElement? state)
    {
        if (_closed) return;
        long? actionId = _running.TryGetValue(action, out long id) ? id : null;
        if (actionId.HasValue) _pendingResumption.Add(action);
        else _incomplete = true;
        Write(new NativeActionEnvelopeRecord(
            "nested_choice_pause", Next(), actionId,
            NativeType: action.GetType().FullName ?? action.GetType().Name,
            State: Snapshot(state),
            Data: Data("choice_kind_and_selected_option_not_yet_audited",
                "passive observation")));
    }

    public void StableBoundary(long decisionId, JsonElement state)
    {
        if (_closed) return;
        Write(new NativeActionEnvelopeRecord(
            "stable_decision", Next(), State: Snapshot(state),
            Data: JsonSerializer.SerializeToElement(new { decision_id = decisionId })));
    }

    public void HistoryEntry(
        long historyIndex, string kind, JsonElement payload,
        string? actor, string? target, string[] missingFields)
    {
        if (_closed) return;
        Write(new NativeActionEnvelopeRecord(
            "history_entry", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            NativeType: kind,
            Data: JsonSerializer.SerializeToElement(new
            {
                history_index = historyIndex,
                payload, actor_id = actor, target_id = target,
                missing_fields = missingFields,
                origin = "CombatHistory.Changed",
                semantic_hook_order_verified = false
            })));
    }

    /// <summary>
    /// Record a real native RNG source call without generating a number or
    /// peeking ahead. This method is intentionally NOT called from the
    /// existing frame/history observer: those callbacks cannot attribute
    /// a particular primitive RNG operation or exact hook order.
    /// </summary>
    public void NativeRngBoundary(NativeRngBoundaryEvidenceV111 evidence)
    {
        if (_closed) return;
        ArgumentNullException.ThrowIfNull(evidence);
        evidence.Validate();
        Write(new NativeActionEnvelopeRecord(
            "native_rng_boundary", Next(),
            _activeOrder.Count == 0 ? null : _activeOrder[^1],
            NativeType: "Rng.NextInt",
            Data: JsonSerializer.SerializeToElement(new
            {
                site_id = evidence.SiteId,
                stream_id = evidence.StreamId,
                owner_id = evidence.OwnerId,
                call_ordinal = evidence.CallOrdinal,
                operation_kind = "bounded_integer",
                bounded_count = evidence.BoundedCount,
                selected_index = evidence.SelectedIndex,
                cursor_before = evidence.CursorBefore,
                cursor_after = evidence.CursorAfter,
                source_version = evidence.SourceVersion,
                origin = evidence.Origin,
                attributed = evidence.Attributed,
                source_hook_observed = true
            })));
    }

    public void HistoryReset(long previouslyObserved, long newLength)
    {
        if (_closed) return;
        // The native game clears CombatHistory when an encounter ends.
        // Whether a reset is benign depends on the following callbacks;
        // the auditor only accepts a final reset directly before terminal.
        if (_activeOrder.Count > 0) _incomplete = true;
        Write(new NativeActionEnvelopeRecord(
            "history_reset", Next(),
            Data: JsonSerializer.SerializeToElement(new
            {
                previous_count = previouslyObserved,
                new_count = newLength
            })));
    }

    public void Finish(string reason, JsonElement? state)
    {
        if (_closed) return;
        var outstanding = _activeOrder.ToArray();
        if (outstanding.Length > 0) _incomplete = true;
        Write(new NativeActionEnvelopeRecord(
            "terminal", Next(), State: Snapshot(state),
            Data: JsonSerializer.SerializeToElement(new
            {
                reason, status = _incomplete ? "INCOMPLETE_CAPTURE" : "OBSERVED",
                open_actions = outstanding,
                action_count = _nextActionId,
                native_build = _gameBuild,
                native_game_parity = "NOT_TESTED"
            })));
        _closed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_closed) Finish("Recorder disposed before combat ended", null);
        await _writer.CompleteAsync().ConfigureAwait(false);
    }

    private void Write(NativeActionEnvelopeRecord record)
    {
        if (!_writer.TryEnqueue(record)) _incomplete = true;
    }

    private long Next() => ++_sequence;
    private static JsonElement? Snapshot(JsonElement? state) => state?.Clone();
    private static JsonElement Data(string reason, string detail) =>
        JsonSerializer.SerializeToElement(new { reason, detail });
}

public sealed record NativeActionEnvelopeRecord(
    string Type, long Ordinal, long? ActionId = null,
    long? ParentActionId = null, string? NativeType = null,
    GameAction? CanonicalAction = null, JsonElement? State = null,
    JsonElement? Data = null);
