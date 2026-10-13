using Sts2Emulator.Core;

namespace Sts2ReferenceMod.Decision;

public enum ControlMode { Paused, Running, Stopping }
public enum ResolutionPhase { AtDecision, WaitingForAction, Executing, Resolving, Terminal, Error }

public sealed record Boundary(long DecisionId, long Revision, string ObservationHash, string StabilityKey);
public sealed record ActionSubmission(string RequestId, long DecisionId, long Revision, string ObservationHash, GameAction Action);
public sealed record SubmissionReceipt(bool Accepted, string Reason);
public sealed record ControllerSnapshot(ControlMode Mode, ResolutionPhase Phase, Boundary? Boundary, bool ActionInFlight);

/// <summary>
/// Host calls all mutating methods at semantic boundaries on its main thread.
/// This class performs no game calls; it authorizes at most one action per decision.
/// </summary>
public sealed class DecisionCoordinator
{
    private readonly object _sync = new();
    private readonly Dictionary<string, SubmissionReceipt> _receipts = new(StringComparer.Ordinal);
    private long _nextDecision;
    private long _revision;
    private Boundary? _boundary;
    private HashSet<string> _legalActions = new(StringComparer.Ordinal);
    private ActionSubmission? _pending;
    private bool _inFlight;
    private bool _pauseAtNextBoundary;
    private bool _stepInProgress;
    private int _stepBudget;

    public ControlMode Mode { get; private set; } = ControlMode.Paused;
    public ResolutionPhase Phase { get; private set; } = ResolutionPhase.AtDecision;

    public ControllerSnapshot Snapshot()
    {
        lock (_sync) return new ControllerSnapshot(Mode, Phase, _boundary, _inFlight);
    }

    public Boundary ObserveDecision(string stabilityKey, string observationHash, IReadOnlyList<GameAction> legalActions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stabilityKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(observationHash);
        lock (_sync)
        {
            if (_inFlight)
                throw new InvalidOperationException("Action must finish before the next decision.");
            // Duplicate stability callbacks must not emit another boundary.
            if (_boundary is not null && _boundary.StabilityKey == stabilityKey
                && _boundary.ObservationHash == observationHash && Phase == ResolutionPhase.WaitingForAction)
                return _boundary;
            _boundary = new Boundary(++_nextDecision, ++_revision, observationHash, stabilityKey);
            _legalActions = legalActions.Select(ActionKey).ToHashSet(StringComparer.Ordinal);
            _pending = null;
            Phase = ResolutionPhase.WaitingForAction;
            if (_stepInProgress || _pauseAtNextBoundary || Mode == ControlMode.Stopping)
            {
                Mode = ControlMode.Paused;
                _stepInProgress = false;
                _pauseAtNextBoundary = false;
                _stepBudget = 0;
            }
            return _boundary;
        }
    }

    public void Step()
    {
        lock (_sync)
        {
            if (Phase != ResolutionPhase.WaitingForAction || _inFlight)
                throw new InvalidOperationException("Step requires a stable decision boundary.");
            _stepBudget = 1;
            _stepInProgress = true;
            Mode = ControlMode.Running;
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            Mode = ControlMode.Running;
            _pauseAtNextBoundary = false;
            _stepInProgress = false;
            _stepBudget = 0;
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            if (_inFlight || Phase == ResolutionPhase.Executing || Phase == ResolutionPhase.Resolving)
                _pauseAtNextBoundary = true;
            else Mode = ControlMode.Paused;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            Mode = _inFlight ? ControlMode.Stopping : ControlMode.Paused;
            _pauseAtNextBoundary = true;
            _pending = null;
        }
    }

    public SubmissionReceipt Submit(ActionSubmission submission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.RequestId);
        lock (_sync)
        {
            if (_receipts.TryGetValue(submission.RequestId, out var duplicate))
                return duplicate;
            string error = _boundary is null ? "NO_BOUNDARY"
                : submission.DecisionId != _boundary.DecisionId ||
                  submission.Revision != _boundary.Revision ||
                  submission.ObservationHash != _boundary.ObservationHash ? "STALE_OBSERVATION"
                : _inFlight || _pending is not null ? "ACTION_IN_FLIGHT"
                : !_legalActions.Contains(ActionKey(submission.Action)) ? "ILLEGAL_ACTION"
                : Mode == ControlMode.Stopping ? "STOPPING"
                : "";
            var result = new SubmissionReceipt(error.Length == 0,
                error.Length == 0 ? "ACCEPTED" : error);
            _receipts[submission.RequestId] = result;
            if (result.Accepted) _pending = submission;
            return result;
        }
    }

    /// <summary>Call from the native main thread immediately before dispatching a queued action.</summary>
    public bool TryTakeAuthorizedAction(out GameAction? action)
    {
        lock (_sync)
        {
            action = null;
            if (Mode != ControlMode.Running || Phase != ResolutionPhase.WaitingForAction
                || _pending is null || _inFlight || (_stepInProgress && _stepBudget == 0))
                return false;
            action = _pending.Action;
            _pending = null;
            _inFlight = true;
            Phase = ResolutionPhase.Executing;
            if (_stepInProgress) _stepBudget--;
            return true;
        }
    }

    public void MarkResolving()
    {
        lock (_sync)
        {
            if (!_inFlight) throw new InvalidOperationException("No action is in flight.");
            Phase = ResolutionPhase.Resolving;
        }
    }

    public void MarkSettled()
    {
        lock (_sync)
        {
            if (!_inFlight) throw new InvalidOperationException("No action is in flight.");
            _inFlight = false;
            Phase = ResolutionPhase.AtDecision;
        }
    }

    public void MarkTerminal()
    {
        lock (_sync)
        {
            _inFlight = false;
            _pending = null;
            Mode = ControlMode.Paused;
            Phase = ResolutionPhase.Terminal;
        }
    }

    private static string ActionKey(GameAction action) =>
        action.Kind + ":" + CanonicalJson.Serialize(action.Payload);
}
