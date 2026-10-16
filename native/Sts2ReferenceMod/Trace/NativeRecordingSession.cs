using System.Text.Json;
using Sts2Emulator.Core;
using Sts2Emulator.Trace;
using Sts2ReferenceMod.Capture;
using Sts2ReferenceMod.Decision;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Passive instrumentation facade. Native hook patches should call these methods from
/// the main thread. Hook discovery and a genuine native adapter are separately gated.
/// </summary>
public sealed class NativeRecordingSession : IAsyncDisposable
{
    private readonly AsyncTraceWriter _writer;
    private readonly EventJournal _journal;
    private readonly DecisionCoordinator _coordinator = new();
    private NativeDecisionRecord? _current;
    private readonly List<NativeDecisionRecord> _uiDecisions = new();
    private bool _actionRecorded;
    private bool _terminal;
    private bool _incomplete;

    public NativeRecordingSession(string outputPath, NativeTraceHeader header, int queueCapacity = 8192)
    {
        if (header.TraceSchema != NativeTraceVersion.Current)
            throw new ArgumentException("Trace schema must equal " + NativeTraceVersion.Current, nameof(header));
        if (header.CaptureMode != "passive")
            throw new ArgumentException("Controlled playback is not connected to native hooks.", nameof(header));
        _writer = new AsyncTraceWriter(outputPath, queueCapacity);
        _journal = new EventJournal();
        Enqueue(header);
    }

    public EventJournal Journal => _journal;
    /// <summary>Bounded in-memory viewer window; the durable JSONL has the full trace.</summary>
    public IReadOnlyList<NativeDecisionRecord> RecordedDecisions => _uiDecisions;
    public DecisionCoordinator Controller => _coordinator;
    public bool IsIncomplete => _incomplete;

    public NativeDecisionRecord? OnStableDecision(INativeCaptureAdapter adapter)
    {
        if (_terminal || !adapter.IsAtStablePlayerChoice) return null;
        JsonElement state = adapter.CaptureCanonicalState().Clone();
        var actions = adapter.GetCanonicalLegalActions().ToArray();
        var boundary = _coordinator.ObserveDecision(adapter.SemanticBoundaryKey,
            adapter.ObservationHash, actions);
        if (_current is not null && _current.DecisionId == boundary.DecisionId)
            return _current;

        string hash = CanonicalJson.Sha256(state);
        if (_current is not null)
        {
            Enqueue(new NativeTransitionRecord("transition", _current.DecisionId,
                _current.StateHash, hash, _current.EventSeq + 1, _journal.LastSequence,
                _actionRecorded && !_incomplete ? "COMPLETE" : "INCOMPLETE_CAPTURE"));
            if (!_actionRecorded) _incomplete = true;
        }

        var decision = new NativeDecisionRecord("decision", boundary.DecisionId, adapter.Phase,
            state, hash, actions, adapter.ObservationHash, boundary.Revision,
            adapter.GetCoverage(), _journal.LastSequence);
        _current = decision;
        _uiDecisions.Add(decision);
        if (_uiDecisions.Count > 250) _uiDecisions.RemoveAt(0);
        _actionRecorded = false;
        Enqueue(decision);
        return decision;
    }

    public void OnHumanAction(GameAction action, bool accepted = true, string? reason = null)
    {
        if (_terminal || _current is null || _actionRecorded)
        {
            _incomplete = true;
            throw new InvalidOperationException("Human action without a unique decision boundary.");
        }
        _actionRecorded = true;
        string submitted = action.Kind + ":" + CanonicalJson.Serialize(action.Payload);
        bool menuMember = _current.LegalActions.Any(legal =>
            legal.Kind + ":" + CanonicalJson.Serialize(legal.Payload) == submitted);
        if (!menuMember || !accepted) _incomplete = true;
        Enqueue(new NativeActionRecord("action", _current.DecisionId, action, "human",
            accepted && menuMember, !menuMember ? "ACTION_ABSENT_FROM_LEGAL_MENU" : reason));
    }

    public NativeEventRecord OnSemanticEffect(
        string kind, JsonElement payload, string origin,
        string? actorId = null, string? targetId = null, string visibility = "player")
    {
        if (_terminal || _current is null)
        {
            _incomplete = true;
            throw new InvalidOperationException("Semantic event without a current decision.");
        }
        var evt = _journal.Append(_current.DecisionId, kind, payload, origin,
            actorId, targetId, visibility);
        Enqueue(evt);
        return evt;
    }

    public void OnTerminal(string status, string reason, JsonElement? terminalState = null)
    {
        if (_terminal) return;
        _terminal = true;
        if (_current is not null)
        {
            bool complete = terminalState.HasValue && _actionRecorded && !_incomplete;
            Enqueue(new NativeTransitionRecord("transition", _current.DecisionId,
                _current.StateHash, complete ? CanonicalJson.Sha256(terminalState.GetValueOrDefault()) : null,
                _current.EventSeq + 1, _journal.LastSequence,
                complete ? "COMPLETE" : "INCOMPLETE_CAPTURE"));
            if (!complete) _incomplete = true;
        }
        _coordinator.MarkTerminal();
        Enqueue(new NativeTerminalRecord("terminal", _current?.DecisionId,
            _incomplete ? "INCOMPLETE_CAPTURE" : status, reason, terminalState?.Clone()));
    }

    private void Enqueue(object record)
    {
        if (!_writer.TryEnqueue(record)) _incomplete = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_terminal) OnTerminal("INCOMPLETE_CAPTURE", "Recording stopped without terminal checkpoint");
        await _writer.CompleteAsync().ConfigureAwait(false);
    }
}
