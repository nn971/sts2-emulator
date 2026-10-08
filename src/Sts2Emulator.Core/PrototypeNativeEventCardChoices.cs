namespace Sts2Emulator.Core;

/// <summary>
/// Eligibility and persistence for event-selected enchants and fixed
/// transformations. Native generic transform operations continue to use
/// the ordinary random reward card pool.
/// </summary>
public sealed partial class PrototypeGameEngine
{
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
            PrototypePersistentDeckChoiceKind.Remove
                or PrototypePersistentDeckChoiceKind.Transform =>
                    !definition.Eternal,
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
                    != PrototypeCardEnchantmentKind.Slither
                    || definition.Cost.Kind
                        == PrototypeCardCostKind.Fixed
                    && definition.Cost.Amount >= 0),
            _ => false
        };
    }

    private static PlayerState ApplyFixedEventTransformation(
        PlayerState player, long instanceId, string resultId)
    {
        _ = PrototypeContent.Card(resultId);
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
        PrototypeCardEnchantmentKind kind) =>
        player with
        {
            Deck = player.Deck.Select(card =>
                card.InstanceId == instanceId
                    ? card with
                    {
                        Enchantment = new PrototypeCardEnchantment(kind, 1)
                    }
                    : card).ToArray()
        };
}
