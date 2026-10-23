namespace Sts2Emulator.Core;

/// <summary>
/// Atomic combat-level ownership of provider-private RNG and unresolved deck
/// state. Compatibility properties on <see cref="CombatState"/> preserve the
/// current snapshot wire representation during the migration. Native RNG
/// sidecars are copied on assignment and exposed through defensive public
/// projections.
/// </summary>
public sealed record CombatStochasticStateV111
{
    private NativeCombatRngV111? _nativeRng;

    [System.Text.Json.Serialization.JsonConstructor]
    public CombatStochasticStateV111(
        NativeCombatRngV111? NativeRng = null,
        SemanticDeckStateV111? SemanticDeck = null)
    {
        this.NativeRng = NativeRng;
        this.SemanticDeck = SemanticDeck;
    }

    public NativeCombatRngV111? NativeRng
    {
        get => _nativeRng?.Fork();
        init => _nativeRng = value?.Fork();
    }

    public SemanticDeckStateV111? SemanticDeck { get; init; }

    public void Deconstruct(
        out NativeCombatRngV111? NativeRng,
        out SemanticDeckStateV111? SemanticDeck)
    {
        NativeRng = this.NativeRng;
        SemanticDeck = this.SemanticDeck;
    }

    public CombatStochasticStateV111 Fork() => this with
    {
        NativeRng = _nativeRng?.Fork(),
        SemanticDeck = SemanticDeck?.Fork()
    };

    public void Validate(CombatState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        NativeCombatRngEntryV111.Validate(owner, _nativeRng);
        if (SemanticDeck is not { } semanticDeck) return;

        SemanticDrawPileAdapterV111.ValidateSynchronized(
            owner, semanticDeck);
        if (semanticDeck.Mode == SemanticDeckStateV111.NativeMode &&
            _nativeRng is null)
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
