namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint for replaying a supported run action up to its next
/// unresolved chance boundary.
/// </summary>
public sealed record PrototypeControlledRunActionMechanicsStateV111(
    RunState PartialState,
    string ActionId,
    string? MapNodeId = null);
