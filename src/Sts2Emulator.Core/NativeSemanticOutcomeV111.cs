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

    internal static int SelectByBoundedOffset(
        RandomIndexDecisionV111 decision, int firstOutcome,
        int supportCount)
    {
        ValidateDecision(decision, "bounded_integer", supportCount);
        return checked(firstOutcome + decision.SelectedIndex);
    }

    private static T Select<T>(
        RandomIndexDecisionV111 decision,
        IReadOnlyList<T> orderedOutcomes, string expectedOperationKind)
    {
        ArgumentNullException.ThrowIfNull(orderedOutcomes);
        ValidateDecision(decision, expectedOperationKind,
            orderedOutcomes.Count);
        return orderedOutcomes[decision.SelectedIndex];
    }

    private static void ValidateDecision(
        RandomIndexDecisionV111 decision, string expectedOperationKind,
        int supportCount)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (decision.ProviderMode != "native-v111" ||
            decision.Request.OperationKind != expectedOperationKind ||
            decision.Request.SupportCount != supportCount ||
            decision.SelectedIndex < 0 ||
            decision.SelectedIndex >= supportCount)
            throw new InvalidDataException(
                "Native provider decision does not match semantic outcome support.");
    }
}
