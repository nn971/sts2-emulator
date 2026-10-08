namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 CardRarityOdds, represented in integer basis points.
/// Each encounter card advances the persistent rarity offset; shop cards
/// consult that same offset without changing it. The prototype consumes
/// discrete RNG draws, so this models probabilities and lifecycle rather
/// than native float bit sequences.
/// </summary>
public enum PrototypeNativeCardOddsSource
{
    RegularEncounter,
    EliteEncounter,
    BossEncounter,
    Shop
}

public static class PrototypeNativeCardRarityOdds
{
    public const int InitialOffsetBasisPoints = -500;
    public const int MaximumOffsetBasisPoints = 4000;
    public const int ScarcityAscension = 7;

    public static int GrowthBasisPoints(int ascension) =>
        ascension >= ScarcityAscension ? 50 : 100;

    public static (PrototypeCardRarity Rarity, int NextOffsetBasisPoints)
        Roll(int currentOffsetBasisPoints, int ascension,
            PrototypeNativeCardOddsSource source, RngBundle rng)
    {
        if (ascension < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ascension));
        }

        // Source CardRarityOdds: high-ascension Scarcity decreases the
        // base rare rates and slows the pity growth.
        var scarcity = ascension >= ScarcityAscension;
        var rareBase = source switch
        {
            PrototypeNativeCardOddsSource.RegularEncounter =>
                scarcity ? 149 : 300,
            PrototypeNativeCardOddsSource.EliteEncounter =>
                scarcity ? 500 : 1000,
            PrototypeNativeCardOddsSource.BossEncounter => 10000,
            PrototypeNativeCardOddsSource.Shop =>
                scarcity ? 450 : 900,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        var uncommonBase = source switch
        {
            PrototypeNativeCardOddsSource.RegularEncounter => 3700,
            PrototypeNativeCardOddsSource.EliteEncounter => 4000,
            PrototypeNativeCardOddsSource.BossEncounter => 0,
            PrototypeNativeCardOddsSource.Shop => 3700,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

        // Native CardRarityOdds owns Rewards RNG even for merchant
        // rarity; the resulting merchant card selection uses Shops.
        var roll = PrototypeRng.NextInt(rng, "reward", 10000);
        var rareThreshold = rareBase +
            (source == PrototypeNativeCardOddsSource.BossEncounter
                ? 0 : currentOffsetBasisPoints);
        var rarity = roll < rareThreshold
            ? PrototypeCardRarity.Rare
            : roll < rareThreshold + uncommonBase
                ? PrototypeCardRarity.Uncommon
                : PrototypeCardRarity.Common;

        var nextOffset = source == PrototypeNativeCardOddsSource.Shop
            ? currentOffsetBasisPoints
            : rarity == PrototypeCardRarity.Rare
                ? InitialOffsetBasisPoints
                : Math.Min(MaximumOffsetBasisPoints,
                    currentOffsetBasisPoints + GrowthBasisPoints(ascension));
        return (rarity, nextOffset);
    }

    /// <summary>
    /// CardFactory.GetNextAllowedRarity cycles Common, Uncommon, Rare,
    /// wrapping after Rare. The odds advance according to the original
    /// rolled rarity, before this fallback.
    /// </summary>
    public static PrototypeCardRarity NextAvailableRarity(
        PrototypeCardRarity rolled,
        IReadOnlyCollection<PrototypeCardRarity> available)
    {
        var next = rolled;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (available.Contains(next))
            {
                return next;
            }
            next = next switch
            {
                PrototypeCardRarity.Common => PrototypeCardRarity.Uncommon,
                PrototypeCardRarity.Uncommon => PrototypeCardRarity.Rare,
                PrototypeCardRarity.Rare => PrototypeCardRarity.Common,
                _ => throw new ArgumentOutOfRangeException(nameof(rolled))
            };
        }
        throw new InvalidOperationException(
            "No purchasable/reward card rarity remains in the restricted pool.");
    }

    public static (string[] Cards, int NextOffsetBasisPoints)
        GenerateEncounterCards(
            int count, int ascension, PrototypeRoomType room,
            int currentOffsetBasisPoints, RngBundle rng)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        var source = room switch
        {
            PrototypeRoomType.Combat =>
                PrototypeNativeCardOddsSource.RegularEncounter,
            PrototypeRoomType.Elite =>
                PrototypeNativeCardOddsSource.EliteEncounter,
            PrototypeRoomType.Boss =>
                PrototypeNativeCardOddsSource.BossEncounter,
            _ => throw new ArgumentOutOfRangeException(nameof(room))
        };
        var pool = PrototypeContent.RewardCardPool.ToList();
        var cards = new List<string>(count);
        var offset = currentOffsetBasisPoints;
        for (var i = 0; i < count; i++)
        {
            var (rolled, updated) = Roll(offset, ascension, source, rng);
            offset = updated;
            var rarity = NextAvailableRarity(
                rolled,
                pool.Select(id => PrototypeContent.Card(id).Rarity)
                    .Distinct().ToArray());
            var candidates = pool.Where(id =>
                PrototypeContent.Card(id).Rarity == rarity).ToArray();
            var selected = candidates[PrototypeRng.NextInt(
                rng, "reward", candidates.Length)];
            cards.Add(selected);
            pool.Remove(selected);
            // CardFactory.CreateForReward also performs a separate
            // Rewards upgrade roll per generated card. In Act 1 the
            // normal baseline upgrade chance is zero.
            _ = PrototypeRng.NextInt(rng, "reward", 10000);
        }
        return (cards.ToArray(), offset);
    }

    public static string[] GenerateMerchantCards(
        int count, int ascension, PrototypeCardType type,
        int currentOffsetBasisPoints, RngBundle rng)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        // Merchant cards are individually generated from the same
        // eligible character pool; duplicate models are legal.
        var pool = PrototypeContent.RewardCardPool
            .Where(id => PrototypeContent.Card(id).Type == type)
            .ToArray();
        var cards = new string[count];
        for (var i = 0; i < count; i++)
        {
            var (rolled, unchanged) = Roll(
                currentOffsetBasisPoints, ascension,
                PrototypeNativeCardOddsSource.Shop, rng);
            if (unchanged != currentOffsetBasisPoints)
            {
                throw new InvalidOperationException(
                    "Merchant generation changed persistent rarity odds.");
            }
            var rarity = NextAvailableRarity(rolled,
                pool.Select(id => PrototypeContent.Card(id).Rarity)
                    .Distinct().ToArray());
            var candidates = pool.Where(id =>
                PrototypeContent.Card(id).Rarity == rarity).ToArray();
            cards[i] = candidates[PrototypeRng.NextInt(
                rng, "shop", candidates.Length)];
            // CardFactory.CreateForMerchant consumes a Shops upgrade
            // roll even though its negative base chance prevents upgrades.
            _ = PrototypeRng.NextInt(rng, "shop", 10000);
        }
        return cards;
    }
}
