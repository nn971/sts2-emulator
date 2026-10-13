using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Lasting Candy adds one Power to every encounter card reward on each
/// alternate combat reward, beginning with the second reward.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static (PlayerState Player, RewardState Reward,
        int CardRarityOffsetBasisPoints) ApplyLastingCandyCombatReward(
        PlayerState player, RewardState reward, int ascension,
        int cardRarityOffsetBasisPoints, RngBundle rng)
    {
        if (reward.CardRewardGenerators is not { } generators
            || generators.Length == 0)
        {
            return (player, reward, cardRarityOffsetBasisPoints);
        }

        var activeGroups = generators.Select((generator, index) =>
                (Generator: generator, Index: index))
            .Where(item => item.Generator?.Kind is
                PrototypeCardRewardGeneratorKind.Encounter
                or PrototypeCardRewardGeneratorKind.RewardPool)
            .Select(item => item.Index).ToArray();
        if (activeGroups.Length == 0)
            return (player, reward, cardRarityOffsetBasisPoints);

        var relics = (RelicInstance[])player.Relics.Clone();
        var modified = false;
        foreach (var relicIndex in Enumerable.Range(0, relics.Length))
        {
            var relic = relics[relicIndex];
            if (relic.IsMelted
                || relic.RelicId != PrototypeNativeSharedRelics.LastingCandyId)
            {
                continue;
            }

            var rewardsSeen = ReadCombatRewardsSeen(relic);
            if (rewardsSeen > 0 && rewardsSeen % 2 == 1)
            {
                foreach (var groupIndex in activeGroups)
                {
                    var options = reward.CardOptionsForGroup(groupIndex);
                    var generator = generators[groupIndex]!;
                    var addition = PickPowerOption(
                        options, generator, ascension,
                        cardRarityOffsetBasisPoints, rng,
                        includeColorless: player.ActiveRelics.Any(relic =>
                            relic.RelicId
                                == PrototypeNativeSharedRelics.DingyRugId));
                    if (addition is null)
                        continue;

                    cardRarityOffsetBasisPoints =
                        addition.Value.NextCardRarityOffsetBasisPoints;
                    reward = AppendOption(reward, groupIndex,
                        addition.Value.CardId, addition.Value.Upgraded);
                    modified = true;
                }
            }

            relics[relicIndex] = relic with
            {
                PersistentState = JsonSerializer.SerializeToElement(
                    new { CombatRewardsSeen = checked(rewardsSeen + 1) })
            };
        }

        player = player with { Relics = relics };
        return (player, reward with
        {
            LastingCandyRewardActive = modified
        }, cardRarityOffsetBasisPoints);
    }

    internal static int ReadCombatRewardsSeen(RelicInstance relic)
    {
        return relic.PersistentState.ValueKind == JsonValueKind.Object
            && relic.PersistentState.TryGetProperty(
                "CombatRewardsSeen", out var value)
            && value.TryGetInt32(out var count)
                ? Math.Max(0, count)
                : 0;
    }

    private static (string CardId, bool Upgraded,
        int NextCardRarityOffsetBasisPoints)?
        PickPowerOption(IReadOnlyList<string> existing,
            PrototypeCardRewardGenerator generator, int ascension,
            int cardRarityOffsetBasisPoints, RngBundle rng,
            bool includeColorless = false)
    {
        var powerPool = PrototypeContent.NativeSilentRewardCardPool
            .Concat(includeColorless
                ? PrototypeColorlessCards.ImplementedRewardPool
                : Array.Empty<string>())
            .Where(id => PrototypeContent.Card(id).Type
                == PrototypeCardType.Power).ToArray();
        var distinct = powerPool.Where(id =>
            !existing.Contains(id, StringComparer.Ordinal)).ToArray();
        var candidates = distinct.Length > 0 ? distinct : powerPool;
        if (candidates.Length == 0)
            return null;

        if (generator.Room is { } room)
        {
            var source = room switch
            {
                PrototypeRoomType.Combat =>
                    PrototypeNativeCardOddsSource.RegularEncounter,
                PrototypeRoomType.Elite =>
                    PrototypeNativeCardOddsSource.EliteEncounter,
                PrototypeRoomType.Boss =>
                    PrototypeNativeCardOddsSource.BossEncounter,
                _ => throw new InvalidOperationException(
                    "Lasting Candy needs a combat reward room.")
            };
            var rolledRarity = PrototypeNativeCardRarityOdds
                .RollWithBaseOdds(ascension, source, rng);
            var rarity = PrototypeNativeCardRarityOdds.NextAvailableRarity(
                rolledRarity,
                candidates.Select(id => PrototypeContent.Card(id).Rarity)
                    .Distinct().ToArray());
            candidates = candidates.Where(id =>
                PrototypeContent.Card(id).Rarity == rarity).ToArray();
            var cardId = candidates[PrototypeRng.NextInt(
                rng, "reward", candidates.Length)];
            var upgraded = PrototypeNativeCardRarityOdds
                .RollEncounterCardUpgrade(cardId, generator.Act,
                    ascension, rng);
            return (cardId, upgraded, cardRarityOffsetBasisPoints);
        }

        var selected = candidates[PrototypeRng.NextInt(
            rng, "reward", candidates.Length)];
        var isUpgraded = PrototypeNativeCardRarityOdds
            .RollEncounterCardUpgrade(selected, generator.Act,
                ascension, rng);
        return (selected, isUpgraded, cardRarityOffsetBasisPoints);
    }

    private static RewardState AppendOption(
        RewardState reward, int groupIndex, string cardId,
        bool upgraded)
    {
        var enchantment = ApplyGlitterToCardRewardOptions([cardId])[0];
        if (groupIndex == 0)
        {
            var options = reward.CardOptions;
            return reward with
            {
                CardOptions = options.Append(cardId).ToArray(),
                CardOptionUpgradeFlags = reward.CardOptionUpgradeFlags is null
                    ? upgraded
                        ? new bool[options.Length].Append(true).ToArray()
                        : null
                    : reward.CardOptionUpgradeFlags.Append(upgraded).ToArray(),
                CardOptionEnchantments = reward.CardOptionEnchantments is null
                    ? null
                    : reward.CardOptionEnchantments.Append(enchantment).ToArray()
            };
        }

        var extraIndex = groupIndex - 1;
        var extraOptions = reward.ExtraCardOptions!
            .Select(group => (string[])group.Clone()).ToArray();
        extraOptions[extraIndex] = extraOptions[extraIndex]
            .Append(cardId).ToArray();
        var extraFlags = reward.ExtraCardOptionUpgradeFlags is null
            ? upgraded
                ? reward.ExtraCardOptions!
                    .Select(group => new bool[group.Length]).ToArray()
                : null
            : reward.ExtraCardOptionUpgradeFlags
                .Select(flags => (bool[])flags.Clone()).ToArray();
        if (extraFlags is not null)
            extraFlags[extraIndex] = extraFlags[extraIndex]
                .Append(upgraded).ToArray();
        var extraEnchantments = reward.ExtraCardOptionEnchantments is null
            ? null
            : reward.ExtraCardOptionEnchantments
                .Select(group => (PrototypeCardEnchantment?[])group.Clone())
                .ToArray();
        if (extraEnchantments is not null)
        {
            extraEnchantments[extraIndex] = extraEnchantments[extraIndex]
                .Append(enchantment).ToArray();
        }

        return reward with
        {
            ExtraCardOptions = extraOptions,
            ExtraCardOptionUpgradeFlags = extraFlags,
            ExtraCardOptionEnchantments = extraEnchantments
        };
    }
}
