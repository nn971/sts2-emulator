namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// Resolve an acquisition-driven persistent deck choice identically
    /// whether the relic was obtained from a reward or purchased in a shop.
    /// Returning null means the final selection has been resolved.
    /// </summary>
    internal static (
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

        if (choice.Kind == PrototypePersistentDeckChoiceKind.Transform
            && PrototypeContent.Relic(choice.SourceRelicId)
                .AcquisitionDeckChoice?.TransformToCardId is not null
            && PrototypeContent.Card(selected.CardId).Type
                == PrototypeCardType.Quest)
        {
            throw new InvalidOperationException(
                $"Quest card {cardInstanceId} cannot be transformed by this relic.");
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
                PrototypeContent.Relic(choice.SourceRelicId)
                    .AcquisitionDeckChoice?.TransformToCardId is { } transformTo
                    ? TransformRelicDeckCard(
                        player, cardInstanceId,
                        choice.SourceRelicId, transformTo)
                    : TransformPersistentDeckCard(
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
        var minimumRemaining = Math.Max(
            0, choice.MinimumSelectionsRemaining - 1);
        return (
            player,
            remaining > 0 && candidates.Length > 0
                ? choice with
                {
                    RemainingSelections = remaining,
                    CandidateCardInstanceIds = candidates,
                    MinimumSelectionsRemaining = minimumRemaining
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

    private static PlayerState TransformRelicDeckCard(
        PlayerState player,
        long cardInstanceId,
        string relicId,
        string transformToCardId)
    {
        var spec = PrototypeContent.Relic(relicId).AcquisitionDeckChoice!;
        var original = player.Deck.Single(card =>
            card.InstanceId == cardInstanceId);
        if (PrototypeCardRules.IsEternal(original))
            throw new InvalidOperationException(
                "Eternal deck cards cannot be transformed.");

        var targetDefinition = PrototypeContent.Card(transformToCardId);
        var upgradeLevel = spec.PreserveUpgradeOnTransformation
            && original.UpgradeLevel > 0
            && targetDefinition.MaxUpgradeLevel > 0
                ? 1
                : 0;
        var transformed = new CardInstance(
            original.InstanceId,
            transformToCardId,
            upgradeLevel,
            PrototypeJson.EmptyObject());
        if (spec.PreserveEnchantmentOnTransformation
            && original.Enchantment is { } enchantment
            && CanSelectEventDeckCard(
                transformed,
                PrototypePersistentDeckChoiceKind.Enchant,
                null,
                enchantment.Kind,
                false))
        {
            transformed = transformed with
            {
                Enchantment = enchantment with { }
            };
        }

        return player with
        {
            Deck = player.Deck.Select(card =>
                card.InstanceId == cardInstanceId
                    ? transformed
                    : card).ToArray()
        };
    }
}
