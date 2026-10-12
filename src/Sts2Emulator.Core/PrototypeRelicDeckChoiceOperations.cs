namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// Resolve an acquisition-driven persistent deck choice identically
    /// whether the relic was obtained from a reward or purchased in a shop.
    /// Returning null means the final selection has been resolved.
    /// </summary>
    private static (
        PlayerState Player,
        PrototypePendingDeckChoiceState? Remaining
    ) ResolveRelicDeckChoice(
        PlayerState player,
        PrototypePendingDeckChoiceState choice,
        long cardInstanceId,
        RngBundle rng)
    {
        if (!choice.CandidateCardInstanceIds.Contains(cardInstanceId))
        {
            throw new InvalidOperationException(
                $"Card instance {cardInstanceId} is not eligible for the relic deck choice.");
        }

        var selected = player.Deck.FirstOrDefault(card =>
            card.InstanceId == cardInstanceId)
            ?? throw new InvalidOperationException(
                $"Card instance {cardInstanceId} is missing.");

        if (choice.Kind is PrototypePersistentDeckChoiceKind.Remove
                or PrototypePersistentDeckChoiceKind.Transform
            && PrototypeCardRules.IsEternal(selected))
        {
            throw new InvalidOperationException(
                $"Eternal card {cardInstanceId} cannot be changed by this relic.");
        }

        if (choice.Kind == PrototypePersistentDeckChoiceKind.Remove
            && PrototypeContent.Relic(choice.SourceRelicId).AcquisitionDeckChoice?.RequiresUpgradable == true
            && selected.UpgradeLevel >= PrototypeContent.Card(selected.CardId).MaxUpgradeLevel)
            throw new InvalidOperationException("Selected card is not upgradable.");

        if (choice.Kind == PrototypePersistentDeckChoiceKind.Remove)
            player = NativePaelRewardsV111.RememberToothRemoval(player, choice.SourceRelicId, selected);

        player = choice.Kind switch
        {
            PrototypePersistentDeckChoiceKind.Enchant =>
                ApplyRelicDeckEnchantment(player, choice.SourceRelicId, selected, rng),
            PrototypePersistentDeckChoiceKind.Remove =>
                player with
                {
                    Deck = player.Deck
                        .Where(card => card.InstanceId != cardInstanceId)
                        .ToArray()
                },
            PrototypePersistentDeckChoiceKind.Transform =>
                TransformPersistentDeckCard(
                    player,
                    cardInstanceId,
                    choice.UpgradeTransformedCards,
                    rng),
            PrototypePersistentDeckChoiceKind.Upgrade =>
                player with
                {
                    Deck = player.Deck
                        .Select(card =>
                            card.InstanceId == cardInstanceId
                                ? card with
                                {
                                    UpgradeLevel = card.UpgradeLevel + 1
                                }
                                : card)
                        .ToArray()
                },
            _ => throw new ArgumentOutOfRangeException()
        };

        var candidates = choice.CandidateCardInstanceIds
            .Where(id => id != cardInstanceId)
            .Where(id => player.Deck.Any(card => card.InstanceId == id))
            .ToArray();
        var remaining = choice.RemainingSelections - 1;
        return (
            player,
            remaining > 0 && candidates.Length > 0
                ? choice with
                {
                    RemainingSelections = remaining,
                    CandidateCardInstanceIds = candidates
                }
                : null);
    }

    private static PlayerState ApplyRelicDeckEnchantment(PlayerState player, string relicId, CardInstance card, RngBundle rng)
    {
        var spec = PrototypeContent.Relic(relicId).AcquisitionDeckChoice!;
        if (!CanSelectRelicDeckCard(card, spec)
            || spec.EnchantmentAmount <= 0)
            throw new InvalidOperationException("Card is no longer eligible for the relic enchantment.");
        return ApplyEventEnchantment(player, card.InstanceId, spec.EnchantmentKind!.Value, spec.EnchantmentAmount);
    }
}
