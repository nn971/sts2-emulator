using System;
using System.Linq;

namespace Sts2Emulator.Core;

/// <summary>
/// The source-pinned v0.111 Fisher-Yates swap loop. The caller must supply
/// the source-ordered card instance IDs: CardPileCmd.Shuffle uses stable
/// comparison-order sorting BEFORE invoking UnstableShuffle; initial
/// CardPile.RandomizeOrderInternal uses its existing input order.
/// Hooks such as ModifyShuffleOrder run AFTER this method and are verified
/// separately. SiteIds are restricted to registered Native shuffle
/// boundaries. This is a mechanics primitive, not full combat continuation.
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
        var decisions = ShuffleInPlace(
            result, streams, provider, StreamId, ownerId, siteId);
        return new NativeFisherYatesResultV111(
            orderedInput.ToArray(), result, decisions,
            "post_fisher_yates_pre_modify_shuffle_order");
    }

    /// <summary>
    /// Apply the same source-pinned descending Fisher-Yates loop to an
    /// arbitrary ordered pool and named stream through the shared provider.
    /// </summary>
    public static RandomIndexDecisionV111[] ShuffleInPlace<T>(
        IList<T> values,
        RngBundle streams,
        IRandomIndexProviderV111 provider,
        string streamId,
        string ownerId,
        string siteId,
        int ordinalOffset = 0,
        Func<T, string>? controlledCandidateLabel = null,
        bool allowRegisteredLegacyShuffle = false)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        // The shared Fisher-Yates mechanics also serve legacy prototype
        // shuffles. Keep their caller-bound sites separate from the Native
        // allowlist: authorization here is only for the legacy wrapper.
        var registeredLegacyShuffle = allowRegisteredLegacyShuffle
            && siteId.EndsWith(".shuffle_swap_index", StringComparison.Ordinal)
            && StochasticSiteRegistryV111.TryGetLegacyCallerBoundIndexQuerySite(
                siteId, out var legacySite)
            && legacySite.StreamId == streamId
            && legacySite.OperationKind == "bounded_integer";
        if (!registeredLegacyShuffle)
            ValidateSiteId(siteId,
                StochasticSiteRegistryV111.NativeFisherYatesSiteIds,
                "shuffle");
        if (ordinalOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinalOffset));
        var decisions = new List<RandomIndexDecisionV111>();
        var controlledEventReplay =
            ControlledEventChoiceReplayV111.IsActive
            && ControlledEventChoiceReplayV111.IsEventChoiceSite(siteId);
        for (int end = values.Count - 1; end > 0; --end)
        {
            // Native ListExtensions.UnstableShuffle calls NextInt(end+1)
            // exactly once, including the final bound of two.
            var request = new RandomIndexRequestV111(
                siteId, streamId, ownerId,
                // Controlled event replay adds the consumed transcript
                // prefix; using the local swap count here would count it twice.
                controlledEventReplay
                    ? ordinalOffset
                    : ordinalOffset + decisions.Count,
                Array.Empty<int>(), OperationKind: "bounded_integer",
                BoundedCount: end + 1);
            RandomIndexDecisionV111 decision;
            if (controlledCandidateLabel is not null
                && controlledEventReplay)
            {
                var candidateLabels = values.Take(end + 1)
                    .Select(controlledCandidateLabel).ToArray();
                if (candidateLabels.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidOperationException(
                        "Controlled shuffle candidate labels must be nonempty.");
                using var candidateScope =
                    ControlledEventChoiceReplayV111.DescribeCandidates(
                        candidateLabels);
                decision = StochasticProviderScopeV111.ResolveIndex(
                    provider, request, streams);
            }
            else
            {
                decision = StochasticProviderScopeV111.ResolveIndex(
                    provider, request, streams);
            }
            decision.Validate();
            (values[end], values[decision.SelectedIndex]) =
                (values[decision.SelectedIndex], values[end]);
            decisions.Add(decision);
        }
        return decisions.ToArray();
    }

    private static void ValidateSiteId(
        string siteId, string[] supportedSiteIds, string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentNullException.ThrowIfNull(supportedSiteIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (!supportedSiteIds.Contains(siteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"Native Fisher-Yates does not support {operation} site " +
                $"'{siteId}'.");
    }
}

public sealed record NativeFisherYatesResultV111(
    long[] OrderedInput,
    long[] PreHookOutput,
    RandomIndexDecisionV111[] Choices,
    string OutputStage);
