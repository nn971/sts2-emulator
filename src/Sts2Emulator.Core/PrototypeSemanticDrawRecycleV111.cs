namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// Commits the legacy non-semantic recycle used by ordinary draws and
    /// top autoplay. The combat zone owner is the only source of the piles;
    /// callers do not supply parallel snapshots.
    /// </summary>
    private static CombatState CommitLegacyDrawPileRecycle(
        CombatState combat, RngBundle rng)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(rng);
        if (combat.Stochastic.SemanticDeck is not null ||
            combat.DrawPile.Length != 0 || combat.DiscardPile.Length == 0 ||
            combat.DiscardPile.Distinct().Count() != combat.DiscardPile.Length)
            throw new InvalidDataException(
                "Legacy recycle requires an exhausted synchronized draw pile.");

        var recycled = (long[])combat.DiscardPile.Clone();
        var shuffled = NativeShuffleStepReplayV111.ShuffleRecycle(
            recycled, rng);
        var postHook = NativeShuffleStepReplayV111.CheckPostHook(
            ApplyPerfectFitShuffleOrder(combat, shuffled));
        return SemanticDrawPileAdapterV111.CommitLegacyRecycle(
            combat, postHook);
    }

    /// <summary>
    /// Applies one source-time Native shuffle to an exhausted exact semantic
    /// deck. The same mechanics path serves ordinary draws and top autoplay;
    /// callers continue their own post-shuffle effects afterward.
    /// </summary>
    private static CombatState CommitNativeSemanticRecycle(
        CombatState combat, string unsupportedSite)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentException.ThrowIfNullOrWhiteSpace(unsupportedSite);
        SemanticDrawPileAdapterV111.ValidateSynchronized(
            combat, requireIdle: true);
        if (combat.Stochastic.SemanticDeck is not { } semantic ||
            semantic.Mode != SemanticDeckStateV111.NativeMode ||
            semantic.Count != 0 ||
            combat.DrawPile.Length != 0 || combat.DiscardPile.Length == 0 ||
            combat.DiscardPile.Distinct().Count() != combat.DiscardPile.Length)
            throw new InvalidDataException(
                "Native recycle requires an exhausted synchronized draw pile.");

        var recycled = (long[])combat.DiscardPile.Clone();
        var native = combat.Stochastic.NativeRng ??
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_NATIVE site=" + unsupportedSite +
                " operation=missing_native_stream_owner");
        var nativeStreams = native.RunStreams.Fork();
        var shuffle = NativeFisherYatesV111.Execute(
            recycled, nativeStreams,
            new NativeRandomIndexProviderV111(),
            ownerId: "player:0",
            siteId: ControlledRecycleShuffleEngineV111.SiteId);
        if (RandomBoundaryJournalV111.HasActiveJournal)
            SemanticPoolShuffleChoiceV111.RecordNativeAudit(
                NativePoolShuffleLabels(combat, shuffle.OrderedInput),
                NativePoolShuffleLabels(combat, shuffle.PreHookOutput),
                shuffle.Choices, shuffle.OrderedInput.Length,
                SemanticPoolShuffleChoiceV111.NativeRecycleOperation, null);
        var postHook = NativeShuffleStepReplayV111.CheckPostHook(
            ApplyPerfectFitShuffleOrder(combat, shuffle.PreHookOutput));
        var advanced = combat with
        {
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                combat.Stochastic,
                native with { RunStreams = nativeStreams })
        };
        return SemanticDrawPileAdapterV111.CommitNativeRecycle(
            advanced, postHook);
    }

    /// <summary>
    /// Commits a Nature recycle using the current discard membership as the
    /// source for Perfect Fit's known-top constraints.
    /// </summary>
    private static CombatState CommitNatureDrawPileRecycle(
        CombatState combat, IReadOnlyList<long> orderedDrawPile)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(orderedDrawPile);
        var perfectFit = combat.DiscardPile.Where(id =>
            RequireCombatCard(combat, id).Enchantment?.Kind ==
            PrototypeCardEnchantmentKind.PerfectFit).ToArray();
        return SemanticDrawPileAdapterV111.CommitNatureRecycle(
            combat, orderedDrawPile, perfectFit);
    }
}
