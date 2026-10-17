using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in adapter for controlled stochastic event options. Ordinary engine
/// steps retain the legacy random profile; callers use this adapter to begin
/// or resume a saved event-choice continuation.
/// </summary>
public static class ControlledEventChoiceEngineV111
{
    public const string SchemaId = "sts2-v111-controlled-event-choice-v1";

    public static RunState Begin(RunState source, string choiceId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        if (source.Phase != RunPhase.Event
            || source.World?.Event is not { ControlledChoice: null } eventState
            || !PrototypeGameEngine.IsControlledEventChoice(
                eventState.EventId, choiceId))
            throw new InvalidOperationException(
                "Controlled event choice requires a supported option on the current event page.");
        return Step(source, GameAction.Create(
            "event_choice", new EventChoicePayload(choiceId)));
    }

    public static RunState Resolve(RunState source, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Phase != RunPhase.Event
            || source.World?.Event?.ControlledChoice is null)
            throw new InvalidOperationException(
                "No controlled event outcome is pending.");
        return Step(source, GameAction.Create(
            "choose_controlled_event_outcome",
            new ChooseControlledEventOutcomePayload(selectedIndex)));
    }

    private static RunState Step(RunState source, GameAction action)
    {
        using (ControlledChanceGuardV111.Enter())
        {
            var result = new PrototypeGameEngine().Step(source, action).State;
            PrototypeStateInvariants.Validate(result);
            return result;
        }
    }
}

/// <summary>
/// Completed controlled outcomes for one event option. This remains attached
/// to the run after its mechanics commit, so snapshots retain decision
/// provenance without retaining a replay checkpoint.
/// </summary>
public sealed record ControlledEventChoiceTraceV111(
    string EventId,
    string ChoiceId,
    RandomIndexDecisionV111[] Decisions,
    string[][] CandidateLabels,
    string ProbabilityStatus = "UNVERIFIED")
{
    public ControlledEventChoiceTraceV111 Fork() => this with
    {
        Decisions = Decisions.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray(),
        CandidateLabels = CandidateLabels.Select(labels =>
            (string[])labels.Clone()).ToArray()
    };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EventId)
            || string.IsNullOrWhiteSpace(ChoiceId)
            || Decisions is null || CandidateLabels is null
            || Decisions.Length == 0
            || Decisions.Length != CandidateLabels.Length
            || !PrototypeGameEngine.IsControlledEventChoice(EventId, ChoiceId)
            || ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Malformed controlled event-choice transcript.");
        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || !ControlledEventChoiceReplayV111.IsEventChoiceSite(
                    decision.Request.SiteId)
                || CandidateLabels[index].Length !=
                    decision.Request.SupportCount
                || CandidateLabels[index].Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled event-choice transcript contains an invalid outcome.");
        }
    }
}

/// <summary>
/// Saved replay point for a guarded event option that needs controlled
/// random outcomes. The checkpoint is the run immediately before the option;
/// replaying from it prevents partially applied effects from being committed.
/// </summary>
public sealed record PrototypeControlledEventChoiceContinuationV111(
    RunState Checkpoint,
    string EventId,
    string ChoiceId,
    RandomIndexDecisionV111[] Decisions,
    RandomIndexRequestV111 PendingRequest,
    string[]? PendingCandidateLabels = null,
    string[][]? DecisionCandidateLabels = null)
{
    public PrototypeControlledEventChoiceContinuationV111 Fork() => this with
    {
        Checkpoint = Checkpoint.Fork(),
        Decisions = Decisions.Select(CloneDecision).ToArray(),
        PendingRequest = CloneRequest(PendingRequest),
        PendingCandidateLabels = PendingCandidateLabels is null
            ? null : (string[])PendingCandidateLabels.Clone(),
        DecisionCandidateLabels = DecisionCandidateLabels?.Select(labels =>
            (string[])labels.Clone()).ToArray()
    };

    public void Validate()
    {
        if (Checkpoint is null || Decisions is null
            || DecisionCandidateLabels is null
            || DecisionCandidateLabels.Length != Decisions.Length
            || string.IsNullOrWhiteSpace(EventId)
            || string.IsNullOrWhiteSpace(ChoiceId)
            || PendingRequest is null)
            throw new InvalidDataException(
                "Malformed controlled event-choice continuation.");

        var checkpointEvent = Checkpoint.World?.Event;
        if (Checkpoint.Phase != RunPhase.Event
            || Checkpoint.World is null
            || checkpointEvent is null
            || checkpointEvent.EventId != EventId
            || checkpointEvent.ChosenChoiceId is not null
            || checkpointEvent.ControlledChoice is not null
            || !PrototypeGameEngine.IsControlledEventChoice(EventId, ChoiceId))
            throw new InvalidDataException(
                "Controlled event-choice checkpoint is outside its event option boundary.");

        ValidateRequest(PendingRequest);
        if (PendingCandidateLabels is not null
            && (PendingCandidateLabels.Length != PendingRequest.SupportCount
                || PendingCandidateLabels.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException(
                "Controlled event-choice candidate labels do not match their ordered support.");
        if (PendingCandidateLabels is null)
            throw new InvalidDataException(
                "Controlled event-choice continuation is missing candidate labels.");
        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null)
                throw new InvalidDataException(
                    "Controlled event-choice prefix contains a non-Nature outcome.");
            ValidateRequest(decision.Request);
            if (DecisionCandidateLabels[index] is null
                || DecisionCandidateLabels[index].Length !=
                    decision.Request.SupportCount
                || DecisionCandidateLabels[index]
                    .Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled event-choice transcript labels do not match their ordered support.");
        }
    }

    internal static RandomIndexRequestV111 CloneRequest(
        RandomIndexRequestV111 request) => request with
    {
        OrderedCandidates = (int[])request.OrderedCandidates.Clone()
    };

    internal static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 decision) => decision with
    {
        Request = CloneRequest(decision.Request)
    };

    private static void ValidateRequest(RandomIndexRequestV111 request)
    {
        request.Validate();
        if (!ControlledEventChoiceReplayV111.IsEventChoiceSite(request.SiteId))
            throw new InvalidDataException(
                "Controlled event-choice continuation contains an unrelated chance site.");
    }
}

internal sealed class ControlledEventChoiceChanceRequiredException(
    RandomIndexRequestV111 request,
    string[]? candidateLabels) : Exception(
        "A controlled event choice needs a random-choice outcome.")
{
    public RandomIndexRequestV111 Request { get; } =
        PrototypeControlledEventChoiceContinuationV111.CloneRequest(request);
    public string[]? CandidateLabels { get; } = candidateLabels is null
        ? null : (string[])candidateLabels.Clone();
}

/// <summary>
/// Replays an ordinary event option from its saved pre-option state, injecting
/// only its recorded controlled outcomes. The first unknown request suspends
/// that option with its exact ordered support.
/// </summary>
internal sealed class ControlledEventChoiceReplayV111 : IDisposable
{
    private static readonly AsyncLocal<ControlledEventChoiceReplayV111?> Active =
        new();
    private readonly ControlledEventChoiceReplayV111? _previous;
    private readonly RandomIndexDecisionV111[] _decisions;
    private string[]? _candidateLabels;
    private int _nextDecision;
    private bool _disposed;

    public static bool IsActive => Active.Value is not null;

    private ControlledEventChoiceReplayV111(
        RandomIndexDecisionV111[] decisions)
    {
        _previous = Active.Value;
        _decisions = decisions.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray();
        Active.Value = this;
    }

    public static ControlledEventChoiceReplayV111 Begin(
        RandomIndexDecisionV111[] decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return new ControlledEventChoiceReplayV111(decisions);
    }

    public static int Resolve(RandomIndexRequestV111 request)
    {
        var active = Active.Value ?? throw new InvalidOperationException(
            "Controlled event-choice replay is not active.");
        request.Validate();
        if (!IsEventChoiceSite(request.SiteId))
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=" + request.SiteId +
                " operation=" + request.OperationKind);

        var ordinal = checked(request.Ordinal + active._decisions
            .Take(active._nextDecision)
            .Count(decision => decision.Request.StreamId == request.StreamId));
        var normalizedRequest = request with { Ordinal = ordinal };
        if (active._candidateLabels is not null
            && active._candidateLabels.Length != normalizedRequest.SupportCount)
            throw new InvalidDataException(
                "Event-choice candidate labels do not match the random support.");
        if (active._nextDecision >= active._decisions.Length)
            throw new ControlledEventChoiceChanceRequiredException(
                normalizedRequest, active._candidateLabels);

        var decision = active._decisions[active._nextDecision++];
        decision.Validate();
        if (CanonicalJson.Sha256(decision.Request) !=
            CanonicalJson.Sha256(normalizedRequest))
            throw new InvalidDataException(
                "Event-choice replay diverged from its saved chance request.");
        return decision.SelectedIndex;
    }

    public void EnsureConsumed()
    {
        if (_nextDecision != _decisions.Length)
            throw new InvalidDataException(
                "Event-choice replay left saved controlled outcomes unused.");
    }

    public static IDisposable DescribeCandidates(string[]? candidateLabels)
    {
        var active = Active.Value;
        if (active is null)
            return EmptyScope.Instance;
        var previous = active._candidateLabels;
        active._candidateLabels = candidateLabels is null
            ? null : (string[])candidateLabels.Clone();
        return new CandidateLabelScope(active, previous);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (Active.Value != this)
            throw new InvalidOperationException(
                "Controlled event-choice replay scopes must be disposed in reverse order.");
        Active.Value = _previous;
        _disposed = true;
    }

    private sealed class CandidateLabelScope(
        ControlledEventChoiceReplayV111 owner,
        string[]? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            owner._candidateLabels = previous;
            _disposed = true;
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }

    public static bool IsEventChoiceSite(string siteId) => siteId is
        "event.random_upgrade.card_instance" or
        "event.reward.random_rare_card.model" or
        "event.reward.random_potion.model" or
        "event.potion.fill_empty_slots.model" or
        "potion.uncommon_model.index_draw" or
        "relic.unowned_random_selection.index_draw";
}
