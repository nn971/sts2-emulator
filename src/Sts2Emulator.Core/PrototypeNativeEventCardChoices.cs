namespace Sts2Emulator.Core;

/// <summary>
/// Eligibility and persistence for event-selected enchants and fixed
/// transformations. Native generic transform operations continue to use
/// the ordinary random reward card pool.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static bool CanSelectRelicDeckCard(
        CardInstance card,
        PrototypeRelicDeckChoiceSpec spec) =>
        (spec.RequiredCardType is null
            || PrototypeContent.Card(card.CardId).Type
                == spec.RequiredCardType.Value)
        && (spec.TransformToCardId is null
            || PrototypeContent.Card(card.CardId).Type
                != PrototypeCardType.Quest)
        && CanSelectEventDeckCard(card, spec.Kind, null,
            spec.EnchantmentKind, false);

    internal static bool CanSelectEventDeckCard(
        CardInstance card,
        PrototypePersistentDeckChoiceKind kind,
        string? transformToCardId,
        PrototypeCardEnchantmentKind? enchantmentKind,
        bool basicCardsOnly)
    {
        var definition = PrototypeContent.Card(card.CardId);
        if (basicCardsOnly && definition.Rarity != PrototypeCardRarity.Basic)
        {
            return false;
        }

        return kind switch
        {
            PrototypePersistentDeckChoiceKind.Remove =>
                !PrototypeCardRules.IsEternal(card),
            PrototypePersistentDeckChoiceKind.Transform =>
                !PrototypeCardRules.IsEternal(card),
            PrototypePersistentDeckChoiceKind.Upgrade =>
                card.UpgradeLevel < definition.MaxUpgradeLevel,
            PrototypePersistentDeckChoiceKind.Enchant =>
                enchantmentKind is not null
                && card.Enchantment is null
                && !definition.Unplayable
                && definition.Type is not
                    PrototypeCardType.Status
                    and not PrototypeCardType.Curse
                    and not PrototypeCardType.Quest
                && (enchantmentKind
                    != PrototypeCardEnchantmentKind.Instinct
                    || definition.Type == PrototypeCardType.Attack)
                && (enchantmentKind
                    != PrototypeCardEnchantmentKind.Imbued
                    || definition.Type == PrototypeCardType.Skill)
                && (enchantmentKind
                    != PrototypeCardEnchantmentKind.Nimble
                    || definition.Effects.Any(effect => effect.Kind is
                        PrototypeCombatEffectKind.GainPlayerBlock
                        or PrototypeCombatEffectKind.MultiplyPlayerBlock
                        or PrototypeCombatEffectKind.GainPlayerBlockAndApplyPowerFromActualGain
                        or PrototypeCombatEffectKind.GainToricToughnessBlock))
                // Souls Power can only remove a LOCAL Exhaust keyword.
                // Ordinary ExhaustOnUse is the canonical local keyword
                // in the typed card registry; upgraded cards that have
                // LoseExhaustOnUpgrade no longer qualify.
                && (enchantmentKind
                    != PrototypeCardEnchantmentKind.SoulsPower
                    || definition.ExhaustOnUse
                    && !(card.UpgradeLevel > 0
                        && definition.LoseExhaustOnUpgrade))
                && (enchantmentKind
                    != PrototypeCardEnchantmentKind.Slither
                    || definition.Cost.Kind
                        == PrototypeCardCostKind.Fixed
                    && definition.Cost.Amount >= 0)
                // Source: Spiral.CanEnchant requires basic Strike/Defend
                // tags. Other basic cards (e.g., Neutralize/Survivor)
                // are deliberately excluded.
                && (enchantmentKind
                    != PrototypeCardEnchantmentKind.Goopy
                    || definition.Tags?.Contains("Defend", StringComparer.Ordinal) == true)
                && (enchantmentKind
                    != PrototypeCardEnchantmentKind.Spiral
                    || definition.Rarity == PrototypeCardRarity.Basic
                    && (definition.Tags?.Contains(
                            "Strike", StringComparer.Ordinal) == true
                        || definition.Tags?.Contains(
                            "Defend", StringComparer.Ordinal) == true)),
            _ => false
        };
    }

    private static PlayerState ApplyFixedEventTransformation(
        PlayerState player, long instanceId, string resultId)
    {
        _ = PrototypeContent.Card(resultId);
        var original = player.Deck.Single(card => card.InstanceId == instanceId);
        if (PrototypeCardRules.IsEternal(original))
            throw new InvalidOperationException("Eternal deck cards cannot be transformed.");
        return player with
        {
            Deck = player.Deck.Select(card =>
                card.InstanceId == instanceId
                    ? new CardInstance(
                        card.InstanceId,
                        resultId,
                        0,
                        PrototypeJson.EmptyObject())
                    : card).ToArray()
        };
    }

    private static PlayerState ApplyEventEnchantment(
        PlayerState player, long instanceId,
        PrototypeCardEnchantmentKind kind, int amount = 1) =>
        player with
        {
            Deck = player.Deck.Select(card =>
                card.InstanceId == instanceId
                    ? card with
                    {
                        Enchantment = new PrototypeCardEnchantment(kind, amount)
                    }
                    : card).ToArray()
        };
}
