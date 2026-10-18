namespace Sts2Emulator.Core;

/// <summary>
/// Declares which continuation boundaries a DrawCards caller can resume.
/// Named profiles keep source, automatic-turn, semantic, and nested-effect
/// adapters from independently assembling overlapping suspension switches.
/// </summary>
internal readonly record struct PrototypeDrawCardsSuspensionPolicyV111(
    bool ControlledRecycle,
    bool ControlledDrawCost,
    bool SemanticDraw,
    bool AfterShuffleInsertion,
    bool CardDrawnChoice)
{
    public static PrototypeDrawCardsSuspensionPolicyV111 None => new();

    public static PrototypeDrawCardsSuspensionPolicyV111 AutomaticHandDraw(
        CombatState combat)
    {
        bool controlled = ControlledChanceGuardV111.Active;
        bool nativeOwned =
            ControlledChanceGuardV111.EngineOwnedNativeProviderActive;
        bool natureDeck = combat.SemanticDeck is
            { Mode: SemanticDeckStateV111.NatureMode };
        return new(
            ControlledRecycle: controlled,
            ControlledDrawCost: controlled,
            SemanticDraw: controlled && combat.SemanticDeck is not null,
            AfterShuffleInsertion: controlled && natureDeck,
            CardDrawnChoice: controlled && !nativeOwned &&
                (combat.SemanticDeck is null || natureDeck));
    }

    public static PrototypeDrawCardsSuspensionPolicyV111
        ControlledLegacyAutomaticHandDraw(
            CombatState combat,
            bool? cardDrawnChoice = null)
    {
        ArgumentNullException.ThrowIfNull(combat);
        return new(
            ControlledRecycle: true,
            ControlledDrawCost: true,
            SemanticDraw: false,
            AfterShuffleInsertion: false,
            CardDrawnChoice: cardDrawnChoice ??
                (combat.SemanticDeck is null &&
                 !ControlledChanceGuardV111.EngineOwnedNativeProviderActive));
    }

    public static PrototypeDrawCardsSuspensionPolicyV111 SemanticDrawCards(
        bool effectDraw = false) => new(
            ControlledRecycle: false,
            ControlledDrawCost: effectDraw,
            SemanticDraw: true,
            AfterShuffleInsertion: true,
            CardDrawnChoice: true);

    public static PrototypeDrawCardsSuspensionPolicyV111 ControlledEffectDraw(
        CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        bool semanticDraw = ControlledChanceGuardV111.Active &&
            combat.SemanticDeck is
                { Mode: SemanticDeckStateV111.NatureMode };
        return new(
            ControlledRecycle: ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
                combat.SemanticDeck is null,
            ControlledDrawCost: semanticDraw,
            SemanticDraw: semanticDraw,
            AfterShuffleInsertion: semanticDraw,
            CardDrawnChoice: ControlledChanceGuardV111.Active &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive);
    }
}
