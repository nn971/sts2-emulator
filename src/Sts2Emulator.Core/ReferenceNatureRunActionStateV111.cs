namespace Sts2Emulator.Core;

/// <summary>
/// ReferenceNature's initial/current sampler cursor and typed decision
/// transcript, shared by sessions with or without a controlled provider
/// prefix.
/// </summary>
public sealed record ReferenceNatureStochasticStateV111<TDecision>
    where TDecision : class
{
    private TDecision[]? _decisionTranscript = [];

    public ReferenceNatureStateV111 InitialNatureState { get; init; }
    public ReferenceNatureStateV111 NatureState { get; init; }

    public TDecision[] DecisionTranscript
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_decisionTranscript)!;
        init => _decisionTranscript = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }

    public ReferenceNatureStochasticStateV111(
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureStateV111 natureState,
        TDecision[] decisionTranscript)
    {
        InitialNatureState = initialNatureState ??
            throw new ArgumentNullException(nameof(initialNatureState));
        NatureState = natureState ??
            throw new ArgumentNullException(nameof(natureState));
        DecisionTranscript = decisionTranscript;
    }

    public ReferenceNatureStochasticStateV111<TDecision> Fork() => this with
    {
        InitialNatureState = InitialNatureState.Fork(),
        NatureState = NatureState.Fork(),
        DecisionTranscript = DecisionTranscript
    };

    public ReferenceNatureStochasticStateV111<TDecision> Advance(
        ReferenceNatureStateV111 natureState, TDecision decision)
    {
        ArgumentNullException.ThrowIfNull(natureState);
        ArgumentNullException.ThrowIfNull(decision);
        return this with
        {
            NatureState = natureState,
            DecisionTranscript = [.. DecisionTranscript, decision]
        };
    }

    public ReferenceNatureStochasticStateV111<TDecision> Append(
        ReferenceNatureStochasticStateV111<TDecision> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        continuation.Validate();
        if (!NatureState.SameCursor(continuation.InitialNatureState))
            throw new InvalidDataException(
                "ReferenceNature stochastic continuation does not start at the current cursor.");
        return this with
        {
            NatureState = continuation.NatureState,
            DecisionTranscript = [.. DecisionTranscript,
                .. continuation.DecisionTranscript]
        };
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(InitialNatureState);
        ArgumentNullException.ThrowIfNull(NatureState);
        var transcript = DecisionTranscript;
        if (transcript is null)
            throw new InvalidDataException(
                "ReferenceNature stochastic transcript is missing.");
        InitialNatureState.Validate();
        NatureState.Validate();
        if (NatureState.SampleCount != checked(
                InitialNatureState.SampleCount + transcript.Length))
            throw new InvalidDataException(
                "ReferenceNature transcript does not match its sample count.");
        foreach (var decision in transcript)
            if (decision is null)
                throw new InvalidDataException(
                    "ReferenceNature stochastic transcript contains a null decision.");
    }
}

/// <summary>
/// Source run checkpoint, selected action, and current run for ReferenceNature
/// sessions that replay an ordinary run action. The common continuation owns
/// both run snapshots.
/// </summary>
public sealed record ReferenceNatureRunActionMechanicsStateV111
{
    private GameAction _action = null!;

    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public RunState Origin
    {
        get => Continuation.Origin!;
        init => Continuation = Continuation with { Origin = value };
    }

    public RunState Run
    {
        get => Continuation.Current;
        init => Continuation = Continuation with { Current = value };
    }

    public GameAction Action
    {
        get => CloneAction(_action);
        init => _action = CloneAction(value);
    }

    public ReferenceNatureRunActionMechanicsStateV111(
        RunState Origin, GameAction Action, RunState Run)
    {
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunAction,
            Origin, Run);
        this.Action = Action;
    }

    public ReferenceNatureRunActionMechanicsStateV111 Fork() => this with
    {
        Continuation = Continuation.Fork(),
        Action = Action
    };

    private static GameAction CloneAction(GameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action with { Payload = action.Payload.Clone() };
    }
}

/// <summary>
/// Nature cursor and typed controlled-provider prefix owned by the existing
/// ReferenceNature run-action session. The serialized provider-prefix member
/// retains its combat-entry name for snapshot compatibility.
/// </summary>
public sealed record ReferenceNatureRunActionStochasticStateV111
{
    private ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        _cursor = null!;
    private ControlledStochasticContinuationStateV111?
        _nativeCombatEntryContinuation;
    private ControlledStochasticDecisionV111[]?
        _nativeCombatEntryProviderDecisions = [];

    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Cursor
    {
        get => _cursor;
        init => _cursor = value ?? throw new ArgumentNullException(nameof(Cursor));
    }

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Cursor.InitialNatureState;
        init => Cursor = Cursor with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Cursor.NatureState;
        init => Cursor = Cursor with { NatureState = value };
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Cursor.DecisionTranscript;
        init => Cursor = Cursor with { DecisionTranscript = value };
    }

    public ControlledStochasticContinuationStateV111?
        NativeCombatEntryContinuation
    {
        get => _nativeCombatEntryContinuation?.Fork();
        init => _nativeCombatEntryContinuation = value?.Fork();
    }

    public ControlledStochasticDecisionV111[]
        NativeCombatEntryProviderDecisions
    {
        get => CloneProviderDecisions(
            _nativeCombatEntryProviderDecisions)!;
        init => _nativeCombatEntryProviderDecisions =
            CloneProviderDecisions(value);
    }

    public ReferenceNatureRunActionStochasticStateV111(
        ReferenceNatureStateV111 InitialNatureState,
        ReferenceNatureStateV111 NatureState,
        ReferenceNatureDecisionV111[] DecisionTranscript)
    {
        Cursor = new ReferenceNatureStochasticStateV111<
            ReferenceNatureDecisionV111>(InitialNatureState, NatureState,
            DecisionTranscript);
    }

    public ReferenceNatureRunActionStochasticStateV111 Fork() => this with
    {
        Cursor = Cursor.Fork(),
        NativeCombatEntryContinuation = NativeCombatEntryContinuation,
        NativeCombatEntryProviderDecisions =
            NativeCombatEntryProviderDecisions
    };

    public void Validate()
    {
        Cursor.Validate();
        var transcript = DecisionTranscript;
        var providerDecisions = NativeCombatEntryProviderDecisions;
        if (providerDecisions is null)
            throw new InvalidDataException(
                "ReferenceNature run-action typed provider transcript is missing.");
        foreach (var decision in transcript)
            decision.Validate();
        NativeCombatEntryContinuation?.Validate();
        foreach (var decision in providerDecisions)
            (decision ?? throw new InvalidDataException(
                "Native typed run-action provider transcript contains a null decision."))
                .Validate();
    }

    internal ReferenceNatureRunActionStochasticStateV111 Advance(
        ReferenceNatureStateV111 natureState,
        ReferenceNatureDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(natureState);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        return this with
        {
            Cursor = Cursor.Advance(natureState, decision)
        };
    }

    private static ControlledStochasticDecisionV111[]?
        CloneProviderDecisions(
            ControlledStochasticDecisionV111[]? decisions) => decisions?
                .Select(decision => decision?.Fork()!).ToArray();
}

/// <summary>
/// Shared stochastic prefix for ReferenceNature sessions that replay one
/// source action from a saved run boundary.
/// </summary>
public sealed record ReferenceNatureControlledActionStochasticStateV111<TLawDecision>
    where TLawDecision : class
{
    private ControlledStochasticDecisionV111[]? _providerDecisions = [];
    private ControlledStochasticRequestV111? _pendingRequest;
    private string[]? _pendingCandidateLabels;

    private ReferenceNatureStochasticStateV111<TLawDecision> _cursor = null!;

    public ReferenceNatureStochasticStateV111<TLawDecision> Cursor
    {
        get => _cursor;
        init => _cursor = value ?? throw new ArgumentNullException(nameof(Cursor));
    }

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Cursor.InitialNatureState;
        init => Cursor = Cursor with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Cursor.NatureState;
        init => Cursor = Cursor with { NatureState = value };
    }

    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(_providerDecisions)!;
        init => _providerDecisions = ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(value);
    }

    public TLawDecision[] DecisionTranscript
    {
        get => Cursor.DecisionTranscript;
        init => Cursor = Cursor with { DecisionTranscript = value };
    }

    public ControlledStochasticRequestV111? PendingRequest
    {
        get => ReferenceNatureSessionValueCopiesV111.CopyRequest(
            _pendingRequest);
        init => _pendingRequest = ReferenceNatureSessionValueCopiesV111
            .CopyRequest(value);
    }

    public string[]? PendingCandidateLabels
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_pendingCandidateLabels);
        init => _pendingCandidateLabels = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }

    public ReferenceNatureControlledActionStochasticStateV111(
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureStateV111 natureState,
        ControlledStochasticDecisionV111[] providerDecisions,
        TLawDecision[] decisionTranscript,
        ControlledStochasticRequestV111? pendingRequest,
        string[]? pendingCandidateLabels)
    {
        Cursor = new ReferenceNatureStochasticStateV111<TLawDecision>(
            initialNatureState, natureState, decisionTranscript);
        ProviderDecisions = providerDecisions;
        PendingRequest = pendingRequest;
        PendingCandidateLabels = pendingCandidateLabels;
    }

    public ReferenceNatureControlledActionStochasticStateV111<TLawDecision>
        Fork() => this with
        {
            Cursor = Cursor.Fork(),
            ProviderDecisions = ProviderDecisions,
            PendingRequest = PendingRequest,
            PendingCandidateLabels = PendingCandidateLabels
        };

    public void Validate()
    {
        Cursor.Validate();
        var providerDecisions = ProviderDecisions;
        var decisionTranscript = DecisionTranscript;
        if (providerDecisions is null ||
            providerDecisions.Length != decisionTranscript.Length)
            throw new InvalidDataException(
                "ReferenceNature controlled-action prefixes are inconsistent.");

        foreach (var decision in providerDecisions)
            (decision ?? throw new InvalidDataException(
                "ReferenceNature controlled-action provider prefix contains a null decision."))
                .Validate();

        var request = PendingRequest;
        var labels = PendingCandidateLabels;
        if (request is null)
        {
            if (labels is not null)
                throw new InvalidDataException(
                    "Completed ReferenceNature controlled action has pending candidate labels.");
            return;
        }

        request.Validate();
        if (labels is not null &&
            (request.IndexRequest is null ||
             labels.Length != request.IndexRequest.SupportCount ||
             labels.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException(
                "ReferenceNature controlled-action candidate labels are invalid.");
    }
}
