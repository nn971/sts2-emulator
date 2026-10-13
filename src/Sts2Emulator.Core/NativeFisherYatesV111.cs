namespace Sts2Emulator.Core;

/// <summary>
/// The source-pinned v0.111 Fisher-Yates swap loop. The caller must supply
/// the source-ordered card instance IDs: CardPileCmd.Shuffle uses stable
/// comparison-order sorting BEFORE invoking UnstableShuffle; initial
/// CardPile.RandomizeOrderInternal uses its existing input order.
/// Hooks such as ModifyShuffleOrder run AFTER this method and are verified
/// separately. This is a mechanics primitive, not full combat continuation.
/// </summary>
public static class NativeFisherYatesV111
{
    public const string SiteId = "combat.draw_pile.shuffle.swap";
    public const string StreamId = "run.shuffle";

    public static NativeFisherYatesResultV111 Execute(
        IReadOnlyList<long> orderedInput,
        RngBundle streams,
        IRandomIndexProviderV111 provider,
        string ownerId = "player:0",
        string siteId = SiteId)
    {
        ArgumentNullException.ThrowIfNull(orderedInput);
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        var result = orderedInput.ToArray();
        if (result.Distinct().Count() != result.Length)
            throw new ArgumentException(
                "Fisher-Yates instance identities must be pairwise distinct.");
        var decisions = new List<RandomIndexDecisionV111>();
        for (int end = result.Length - 1; end > 0; --end)
        {
            // Native ListExtensions.UnstableShuffle calls NextInt(end+1)
            // exactly once, including the final bound of two.
            var request = new RandomIndexRequestV111(
                siteId, StreamId, ownerId, decisions.Count,
                Array.Empty<int>(), OperationKind: "bounded_integer",
                BoundedCount: end + 1);
            var decision = provider.Resolve(request, streams);
            decision.Validate();
            (result[end], result[decision.SelectedIndex]) =
                (result[decision.SelectedIndex], result[end]);
            decisions.Add(decision);
        }
        return new NativeFisherYatesResultV111(
            orderedInput.ToArray(), result, decisions.ToArray(),
            "post_fisher_yates_pre_modify_shuffle_order");
    }
}

public sealed record NativeFisherYatesResultV111(
    long[] OrderedInput,
    long[] PreHookOutput,
    RandomIndexDecisionV111[] Choices,
    string OutputStage);
