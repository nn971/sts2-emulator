namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// The cost currently displayed for a revealed combat card. Reuse the
    /// same resolver as card affordability and energy spending, so powers,
    /// relics, afflictions, enchantments and temporary overrides agree.
    /// X-cost cards retain their distinct printed kind; their energy spent
    /// depends on the energy available at play time.
    /// </summary>
    internal static (PrototypeCardCostKind Kind, int? EffectiveEnergyCost)
        PublicCardEnergyCost(CombatState combat, CombatCardInstance card)
    {
        var definition = PrototypeContent.Card(card.CardId);
        return (
            definition.Cost.Kind,
            definition.Cost.Kind == PrototypeCardCostKind.Fixed
                ? ResolveFixedCardEnergyCost(combat, card, definition)
                : null);
    }
}
