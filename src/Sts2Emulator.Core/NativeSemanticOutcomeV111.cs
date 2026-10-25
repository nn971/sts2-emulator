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
        SemanticIndexOutcomeV111.SelectByChooseIndex(
            decision, orderedOutcomes, "native-v111");

    internal static int SelectIndex(
        RandomIndexDecisionV111 decision, string operationKind,
        int supportCount) =>
        SemanticIndexOutcomeV111.SelectIndex(
            decision, operationKind, supportCount, "native-v111");

    internal static int SelectByBoundedOffset(
        RandomIndexDecisionV111 decision, int firstOutcome,
        int supportCount)
        => SemanticIndexOutcomeV111.SelectByBoundedOffset(
            decision, firstOutcome, supportCount, "native-v111");
}
