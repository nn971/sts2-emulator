namespace Sts2Emulator.Core;

/// <summary>
/// Maps a Native primitive index to the ordered semantic outcome support
/// shared by live continuations and Native audit records.
/// </summary>
internal static class NativeSemanticOutcomeV111
{
    internal static T SelectByChooseIndex<T>(
        RandomIndexDecisionV111 decision,
        IReadOnlyList<T> orderedOutcomes) =>
        Select(decision, orderedOutcomes, "choose_index");

    internal static T SelectByBoundedInteger<T>(
        RandomIndexDecisionV111 decision,
        IReadOnlyList<T> orderedOutcomes) =>
        Select(decision, orderedOutcomes, "bounded_integer");

    private static T Select<T>(
        RandomIndexDecisionV111 decision,
        IReadOnlyList<T> orderedOutcomes, string expectedOperationKind)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(orderedOutcomes);
        decision.Validate();
        if (decision.ProviderMode != "native-v111" ||
            decision.Request.OperationKind != expectedOperationKind ||
            decision.Request.SupportCount != orderedOutcomes.Count ||
            decision.SelectedIndex < 0 ||
            decision.SelectedIndex >= orderedOutcomes.Count)
            throw new InvalidDataException(
                "Native provider decision does not match semantic outcome support.");
        return orderedOutcomes[decision.SelectedIndex];
    }
}
