namespace Sts2Emulator.Core;

/// <summary>
/// Source run checkpoint and selected action for a ReferenceNature
/// run-action session. The common continuation owns both run snapshots.
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
/// ReferenceNature run-action session.
/// </summary>
public sealed record ReferenceNatureRunActionStochasticStateV111
{
    private ReferenceNatureDecisionV111[]? _decisionTranscript = [];
    private ControlledStochasticContinuationStateV111?
        _nativeCombatEntryContinuation;
    private ControlledStochasticDecisionV111[]?
        _nativeCombatEntryProviderDecisions = [];

    public ReferenceNatureStateV111 InitialNatureState { get; init; }
    public ReferenceNatureStateV111 NatureState { get; init; }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Clone(_decisionTranscript)!;
        init => _decisionTranscript = Clone(value);
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
        ArgumentNullException.ThrowIfNull(InitialNatureState);
        ArgumentNullException.ThrowIfNull(NatureState);
        this.InitialNatureState = InitialNatureState.Fork();
        this.NatureState = NatureState.Fork();
        this.DecisionTranscript = DecisionTranscript;
    }

    public ReferenceNatureRunActionStochasticStateV111 Fork() => this with
    {
        InitialNatureState = InitialNatureState.Fork(),
        NatureState = NatureState.Fork(),
        DecisionTranscript = DecisionTranscript,
        NativeCombatEntryContinuation = NativeCombatEntryContinuation,
        NativeCombatEntryProviderDecisions =
            NativeCombatEntryProviderDecisions
    };

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(InitialNatureState);
        ArgumentNullException.ThrowIfNull(NatureState);
        var transcript = DecisionTranscript;
        var providerDecisions = NativeCombatEntryProviderDecisions;
        if (transcript is null || providerDecisions is null)
            throw new InvalidDataException(
                "ReferenceNature run-action stochastic transcript is missing.");
        InitialNatureState.Validate();
        NatureState.Validate();
        foreach (var decision in transcript)
            (decision ?? throw new InvalidDataException(
                "ReferenceNature run-action transcript contains a null decision."))
                .Validate();
        NativeCombatEntryContinuation?.Validate();
        foreach (var decision in providerDecisions)
            (decision ?? throw new InvalidDataException(
                "Native combat-entry provider transcript contains a null decision."))
                .Validate();
    }

    private static ReferenceNatureDecisionV111[]? Clone(
        ReferenceNatureDecisionV111[]? transcript) => transcript is null
        ? null
        : (ReferenceNatureDecisionV111[])transcript.Clone();

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
    private TLawDecision[]? _decisionTranscript = [];
    private ControlledStochasticRequestV111? _pendingRequest;
    private string[]? _pendingCandidateLabels;

    public ReferenceNatureStateV111 InitialNatureState { get; init; }
    public ReferenceNatureStateV111 NatureState { get; init; }

    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(_providerDecisions)!;
        init => _providerDecisions = ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(value);
    }

    public TLawDecision[] DecisionTranscript
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_decisionTranscript)!;
        init => _decisionTranscript = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
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
        InitialNatureState = initialNatureState ??
            throw new ArgumentNullException(nameof(initialNatureState));
        NatureState = natureState ??
            throw new ArgumentNullException(nameof(natureState));
        ProviderDecisions = providerDecisions;
        DecisionTranscript = decisionTranscript;
        PendingRequest = pendingRequest;
        PendingCandidateLabels = pendingCandidateLabels;
    }

    public ReferenceNatureControlledActionStochasticStateV111<TLawDecision>
        Fork() => this with
        {
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            ProviderDecisions = ProviderDecisions,
            DecisionTranscript = DecisionTranscript,
            PendingRequest = PendingRequest,
            PendingCandidateLabels = PendingCandidateLabels
        };

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(InitialNatureState);
        ArgumentNullException.ThrowIfNull(NatureState);
        var providerDecisions = ProviderDecisions;
        var decisionTranscript = DecisionTranscript;
        if (providerDecisions is null || decisionTranscript is null ||
            providerDecisions.Length != decisionTranscript.Length)
            throw new InvalidDataException(
                "ReferenceNature controlled-action prefixes are inconsistent.");

        InitialNatureState.Validate();
        NatureState.Validate();
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
