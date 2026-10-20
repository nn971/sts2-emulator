namespace Sts2Emulator.Core;

/// <summary>
/// Shared defensive copies for arrays and typed requests carried by atomic
/// ReferenceNature session records and their snapshots.
/// </summary>
internal static class ReferenceNatureSessionValueCopiesV111
{
    internal static T[]? CopyArray<T>(T[]? values) => values is null
        ? null
        : (T[])values.Clone();

    internal static ControlledStochasticDecisionV111[]? CopyProviderDecisions(
        ControlledStochasticDecisionV111[]? values) => values is null
        ? null
        : values.Select(decision => decision.Fork()).ToArray();

    internal static ControlledStochasticRequestV111? CopyRequest(
        ControlledStochasticRequestV111? value) => value?.Fork();
}
