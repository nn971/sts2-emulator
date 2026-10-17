namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// Applies one source-time Native shuffle to an exhausted exact semantic
    /// deck. The same mechanics path serves ordinary draws and top autoplay;
    /// callers continue their own post-shuffle effects afterward.
    /// </summary>
    private static CombatState CommitNativeSemanticRecycle(
        CombatState combat,
        SemanticDeckStateV111 semantic,
        IReadOnlyList<long> currentPhysicalDrawPile,
        IReadOnlyList<long> currentDiscardPile,
        string unsupportedSite)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(semantic);
        ArgumentNullException.ThrowIfNull(currentPhysicalDrawPile);
        ArgumentNullException.ThrowIfNull(currentDiscardPile);
        ArgumentException.ThrowIfNullOrWhiteSpace(unsupportedSite);

        var checkpoint = combat with
        {
            DrawPile = currentPhysicalDrawPile.ToArray(),
            DiscardPile = currentDiscardPile.ToArray(),
            SemanticDeck = semantic
        };
        SemanticDrawPileAdapterV111.ValidateSynchronized(
            checkpoint, requireIdle: true);
        if (semantic.Mode != SemanticDeckStateV111.NativeMode ||
            semantic.Count != 0 || currentPhysicalDrawPile.Count != 0 ||
            currentDiscardPile.Count == 0 ||
            !combat.DiscardPile.Order().SequenceEqual(
                currentDiscardPile.Order()))
            throw new InvalidDataException(
                "Native recycle requires an exhausted synchronized draw pile.");

        var native = checkpoint.NativeRng ??
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_NATIVE site=" + unsupportedSite +
                " operation=missing_native_stream_owner");
        var nativeStreams = native.RunStreams.Fork();
        var shuffle = NativeFisherYatesV111.Execute(
            currentDiscardPile.ToArray(), nativeStreams,
            new NativeRandomIndexProviderV111(),
            ownerId: "player:0",
            siteId: "combat.draw_pile_recycle.swap_index");
        var postHook = NativeShuffleStepReplayV111.CheckPostHook(
            ApplyPerfectFitShuffleOrder(checkpoint, shuffle.PreHookOutput));
        var nextDeck = SemanticDeckEngineV111.RecycleNative(
            semantic, currentDiscardPile, postHook.Reverse().ToArray());
        var advanced = checkpoint with
        {
            NativeRng = native with { RunStreams = nativeStreams }
        };
        return SemanticDrawPileAdapterV111.ApplyCommittedPhysicalMutation(
            advanced, postHook, nextDeck) with { DiscardPile = [] };
    }
}
