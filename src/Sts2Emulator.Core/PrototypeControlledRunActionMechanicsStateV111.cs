namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint for replaying a supported run action up to its next
/// unresolved chance boundary.
/// </summary>
public sealed record PrototypeControlledRunActionMechanicsStateV111
{
    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public RunState PartialState
    {
        get => Continuation.Current;
        init => Continuation = Continuation with { Current = value };
    }

    public string ActionId { get; init; }
    public string? MapNodeId { get; init; }

    public PrototypeControlledRunActionMechanicsStateV111(
        RunState PartialState,
        string ActionId,
        string? MapNodeId = null)
    {
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.ControlledRunAction,
            null, PartialState);
        this.ActionId = ActionId;
        this.MapNodeId = MapNodeId;
    }
}
