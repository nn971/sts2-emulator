namespace Sts2Emulator.Core;

/// <summary>
/// Maps a typed index decision to a mechanics outcome, checking that the
/// declared provider operation and support match the ordered outcomes.
/// </summary>
internal static class SemanticIndexOutcomeV111
{
    internal static T SelectByChooseIndex<T>(
        RandomIndexDecisionV111 decision,
        IReadOnlyList<T> orderedOutcomes,
        string? expectedProviderMode = null)
    {
        ArgumentNullException.ThrowIfNull(orderedOutcomes);
        ValidateDecision(decision, "choose_index", orderedOutcomes.Count,
            expectedProviderMode);
        return orderedOutcomes[decision.SelectedIndex];
    }

    internal static int SelectByBoundedOffset(
        RandomIndexDecisionV111 decision, int firstOutcome,
        int supportCount, string? expectedProviderMode = null)
    {
        ValidateDecision(decision, "bounded_integer", supportCount,
            expectedProviderMode);
        return checked(firstOutcome + decision.SelectedIndex);
    }

    private static void ValidateDecision(
        RandomIndexDecisionV111 decision, string expectedOperationKind,
        int supportCount, string? expectedProviderMode)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if ((expectedProviderMode is not null &&
             decision.ProviderMode != expectedProviderMode) ||
            decision.Request.OperationKind != expectedOperationKind ||
            decision.Request.SupportCount != supportCount ||
            decision.SelectedIndex < 0 ||
            decision.SelectedIndex >= supportCount)
            throw new InvalidDataException(
                "Index decision does not match semantic outcome support.");
    }
}
