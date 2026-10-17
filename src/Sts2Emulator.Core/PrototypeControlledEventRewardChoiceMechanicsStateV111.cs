namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics boundary and committed event-choice prefix for a suspended
/// post-reward event chance.
/// </summary>
public sealed record PrototypeControlledEventRewardChoiceMechanicsStateV111
{
    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public RunState PartialState
    {
        get => Continuation.Current;
        init => Continuation = Continuation with { Current = value };
    }

    public string EventId { get; init; }
    public string ChoiceId { get; init; }
    public int DecisionPrefixLength { get; init; }

    public PrototypeControlledEventRewardChoiceMechanicsStateV111(
        RunState PartialState,
        string EventId,
        string ChoiceId,
        int DecisionPrefixLength)
    {
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.ControlledEventRewardChoice,
            null, PartialState);
        this.EventId = EventId;
        this.ChoiceId = ChoiceId;
        this.DecisionPrefixLength = DecisionPrefixLength;
    }
}
