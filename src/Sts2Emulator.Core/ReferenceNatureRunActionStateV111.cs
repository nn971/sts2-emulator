namespace Sts2Emulator.Core;

/// <summary>
/// Source run checkpoint and selected map action for a ReferenceNature
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
