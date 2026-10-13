namespace Sts2Emulator.Core;

/// <summary>Keywords granted by persistent card modifications are instance rules.</summary>
public static class PrototypeCardRules
{
    public static bool GainsBlock(PrototypeCardDefinition card) =>
        card.Effects.Any(effect =>
            effect.Kind is
                PrototypeCombatEffectKind.GainPlayerBlock
                or PrototypeCombatEffectKind.GainToricToughnessBlock
                or PrototypeCombatEffectKind.GainPlayerBlockFromEnemyStatusTotal
                or PrototypeCombatEffectKind.GainPlayerBlockAndApplyPowerFromActualGain
            || effect.GainBlockEqualToAttackDamage);

    public static bool IsEternal(CardInstance card) =>
        IsEternal(card.CardId, card.Enchantment);

    public static bool IsEternal(CombatCardInstance card) =>
        IsEternal(card.CardId, card.Enchantment);

    private static bool IsEternal(string cardId, PrototypeCardEnchantment? enchantment) =>
        PrototypeContent.Card(cardId).Eternal
        || enchantment?.Kind == PrototypeCardEnchantmentKind.TezcatarasEmber;
}
