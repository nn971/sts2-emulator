namespace Sts2Emulator.Core;

/// <summary>
/// Atomic combat-level ownership of provider-private RNG and unresolved deck
/// state. Compatibility properties on <see cref="CombatState"/> preserve the
/// current snapshot wire representation during the migration.
/// </summary>
public sealed record CombatStochasticStateV111(
    NativeCombatRngV111? NativeRng = null,
    SemanticDeckStateV111? SemanticDeck = null)
{
    public CombatStochasticStateV111 Fork() => this with
    {
        NativeRng = NativeRng?.Fork(),
        SemanticDeck = SemanticDeck?.Fork()
    };

    public void Validate(CombatState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        NativeCombatRngEntryV111.Validate(owner, NativeRng);
        if (SemanticDeck is not { } semanticDeck) return;

        SemanticDrawPileAdapterV111.ValidateSynchronized(
            owner, semanticDeck);
        if (semanticDeck.Mode == SemanticDeckStateV111.NativeMode &&
            NativeRng is null)
            throw new InvalidOperationException(
                "Native exact semantic deck has no native combat RNG owner.");
    }

    internal static CombatStochasticStateV111 WithNativeRng(
        CombatStochasticStateV111? current, NativeCombatRngV111? nativeRng) =>
        (current ?? new CombatStochasticStateV111()) with
        {
            NativeRng = nativeRng
        };

    internal static CombatStochasticStateV111 WithSemanticDeck(
        CombatStochasticStateV111? current,
        SemanticDeckStateV111? semanticDeck) =>
        (current ?? new CombatStochasticStateV111()) with
        {
            SemanticDeck = semanticDeck
        };
}
