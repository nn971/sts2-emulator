namespace Sts2Emulator.Core;

/// <summary>
/// Driftwood rerolls a whole generated card-reward group once. Generation
/// metadata lives with the reward so snapshots and forks retain the exact
/// generator and its one-use state.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static bool CanRerollRewardCardGroup(
        PlayerState player, RewardState reward, int groupIndex)
    {
        if (!player.ActiveRelics.Any(relic =>
                relic.RelicId == PrototypeNativeOrobasRelics.DriftwoodId)
            || reward.CardBundles is not null
            || reward.CardRewardGenerators is not { } generators
            || (uint)groupIndex >= (uint)generators.Length
            || generators[groupIndex] is not { Rerolled: false })
            return false;

        return groupIndex == 0
            ? !reward.CardResolved
            : !reward.IsExtraCardGroupResolved(groupIndex - 1);
    }

    internal static GameAction? CreateDriftwoodRerollAction(
        PlayerState player, RewardState reward, int groupIndex) =>
        CanRerollRewardCardGroup(player, reward, groupIndex)
            ? GameAction.Create("reroll_reward_card",
                new RerollRewardCardPayload(groupIndex))
            : null;

    private static RunState RerollRewardCardGroup(
        RunState state, int groupIndex)
    {
        var world = RequireWorld(state);
        var reward = world.Reward
            ?? throw new InvalidOperationException(
                "Card reward reroll has no reward state.");
        if (!CanRerollRewardCardGroup(state.Player, reward, groupIndex))
            throw new InvalidOperationException(
                "Driftwood cannot reroll this card reward group.");

        var generators = (PrototypeCardRewardGenerator?[])
            reward.CardRewardGenerators!.Clone();
        var generator = generators[groupIndex]!;
        var includeColorless = state.Player.ActiveRelics.Any(relic =>
            relic.RelicId == PrototypeNativeSharedRelics.DingyRugId);
        var oldOptions = reward.CardOptionsForGroup(groupIndex);
        var lastingCandy = reward.LastingCandyRewardActive
            && (generator.Kind is PrototypeCardRewardGeneratorKind.Encounter
                or PrototypeCardRewardGeneratorKind.RewardPool);
        var generatedCount = oldOptions.Length - (lastingCandy ? 1 : 0);
        var generatedFlags = (bool[]?)null;
        switch (generator.Kind)
        {
            case PrototypeCardRewardGeneratorKind.RewardPool:
                oldOptions = PickRewardCards(
                    generator.Act, generatedCount, state.Rng,
                    includeColorless);
                break;
            case PrototypeCardRewardGeneratorKind.ColorlessRewardPool:
                oldOptions = PickRewardCardsFromPool(
                    generator.Act, oldOptions.Length, state.Rng,
                    PrototypeColorlessCards.ImplementedRewardPool);
                break;
            case PrototypeCardRewardGeneratorKind.RarePool:
            {
                var rarePool = PrototypeContent.NativeSilentRewardCardPool
                    .Where(id => PrototypeContent.Card(id).Rarity
                        == PrototypeCardRarity.Rare)
                    .ToArray();
                if (rarePool.Length < oldOptions.Length)
                    throw new InvalidOperationException(
                        "Rare card reward pool is smaller than its offer.");
                oldOptions = PickDistinct(rarePool, oldOptions.Length,
                    state.Rng, "reward");
                break;
            }
            case PrototypeCardRewardGeneratorKind.DistinctRewardPool:
                oldOptions = PickDistinct(
                    PrototypeContent.NativeSilentRewardCardPool,
                    oldOptions.Length, state.Rng, "reward");
                break;
            case PrototypeCardRewardGeneratorKind.RarityPool:
            {
                var rarity = generator.Rarity
                    ?? throw new InvalidOperationException(
                        "Rarity card reward generator has no rarity.");
                var pool = PrototypeContent.NativeSilentRewardCardPool
                    .Where(id =>
                    {
                        var card = PrototypeContent.Card(id);
                        return card.Rarity == rarity
                            && (generator.CardType is null
                                || card.Type == generator.CardType.Value);
                    })
                    .ToArray();
                if (pool.Length < oldOptions.Length)
                    throw new InvalidOperationException(
                        "Filtered rarity card reward pool is smaller than its offer.");
                oldOptions = PickDistinct(pool, oldOptions.Length,
                    state.Rng, "reward");
                break;
            }
            case PrototypeCardRewardGeneratorKind.Encounter:
            {
                if (generator.Room is not (PrototypeRoomType.Combat
                    or PrototypeRoomType.Elite or PrototypeRoomType.Boss))
                    throw new InvalidOperationException(
                        "Encounter card reward generator has no combat room type.");
                var generated = PrototypeNativeCardRarityOdds
                    .GenerateEncounterCards(generatedCount,
                        state.Ascension, generator.Room.Value,
                        world.CardRarityOffsetBasisPoints,
                        state.Rng, generator.Act,
                        includeColorlessCardRewards: includeColorless);
                oldOptions = generated.Cards;
                generatedFlags = generated.UpgradeFlags;
                world = world with
                {
                    CardRarityOffsetBasisPoints =
                        generated.NextOffsetBasisPoints
                };
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (lastingCandy)
        {
            var addition = PickPowerOption(
                oldOptions, generator, state.Ascension,
                world.CardRarityOffsetBasisPoints, state.Rng,
                includeColorless);
            if (addition is not null)
            {
                oldOptions = oldOptions.Append(addition.Value.CardId).ToArray();
                if (generatedFlags is not null
                    || addition.Value.Upgraded)
                {
                    generatedFlags = (generatedFlags
                            ?? new bool[oldOptions.Length - 1])
                        .Append(addition.Value.Upgraded).ToArray();
                }
                world = world with
                {
                    CardRarityOffsetBasisPoints =
                        addition.Value.NextCardRarityOffsetBasisPoints
                };
            }
        }

        generators[groupIndex] = generator with { Rerolled = true };
        if (groupIndex == 0)
        {
            reward = reward with
            {
                CardOptions = oldOptions,
                CardOptionUpgradeFlags = generatedFlags
                    ?? (reward.CardOptionUpgradeFlags is null
                        ? null : new bool[oldOptions.Length]),
                CardOptionEnchantments =
                    reward.CardOptionEnchantments is null
                        ? null
                        : ApplyGlitterToCardRewardOptions(oldOptions),
                CardRewardGenerators = generators
            };
        }
        else
        {
            var extraOptions = reward.ExtraCardOptions!
                .Select(group => (string[])group.Clone()).ToArray();
            extraOptions[groupIndex - 1] = oldOptions;
            bool[][]? extraFlags = reward.ExtraCardOptionUpgradeFlags is null
                ? generatedFlags is null ? null
                    : Enumerable.Range(0, extraOptions.Length)
                        .Select(index => index == groupIndex - 1
                            ? generatedFlags
                            : new bool[extraOptions[index].Length])
                        .ToArray()
                : reward.ExtraCardOptionUpgradeFlags
                    .Select(flags => (bool[])flags.Clone()).ToArray();
            if (extraFlags is not null && generatedFlags is not null)
                extraFlags[groupIndex - 1] = generatedFlags;
            else if (extraFlags is not null)
                extraFlags[groupIndex - 1] = new bool[oldOptions.Length];
            var extraEnchantments =
                reward.ExtraCardOptionEnchantments is null
                    ? null
                    : reward.ExtraCardOptionEnchantments
                        .Select(group =>
                            (PrototypeCardEnchantment?[])group.Clone())
                        .ToArray();
            if (extraEnchantments is not null)
            {
                extraEnchantments[groupIndex - 1] =
                    ApplyGlitterToCardRewardOptions(oldOptions);
            }
            reward = reward with
            {
                ExtraCardOptions = extraOptions,
                ExtraCardOptionUpgradeFlags = extraFlags,
                ExtraCardOptionEnchantments = extraEnchantments,
                CardRewardGenerators = generators
            };
        }

        reward = ApplyLavaLampRewardModifier(state.Player, reward);
        var wingCharmResult = ApplyWingCharmToCardRewardOptions(
            state, reward.CardOptions,
            reward.ExtraCardOptions ?? Array.Empty<string[]>(),
            reward.CardOptionEnchantments,
            reward.ExtraCardOptionEnchantments);
        state = wingCharmResult.State;
        reward = reward with
        {
            CardOptionEnchantments =
                wingCharmResult.CardOptionEnchantments,
            ExtraCardOptionEnchantments =
                wingCharmResult.ExtraCardOptionEnchantments
        };
        return state with { World = world with { Reward = reward } };
    }
}
