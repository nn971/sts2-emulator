namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 CardRarityOdds, represented in integer basis points.
/// Each encounter card advances the persistent rarity offset; shop cards
/// consult that same offset without changing it. Legacy operations retain
/// their discrete prototype draws; configured reward and merchant generation
/// use the native player reward/shop streams.
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

    private static (int RareBase, int UncommonBase) Thresholds(
        int ascension, PrototypeNativeCardOddsSource source)
    {
        if (ascension < 0)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        var scarcity = ascension >= ScarcityAscension;
        return source switch
        {
            PrototypeNativeCardOddsSource.RegularEncounter =>
                (scarcity ? 149 : 300, 3700),
            PrototypeNativeCardOddsSource.EliteEncounter =>
                (scarcity ? 500 : 1000, 4000),
            PrototypeNativeCardOddsSource.BossEncounter =>
                (10000, 0),
            PrototypeNativeCardOddsSource.Shop =>
                (scarcity ? 450 : 900, 3700),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
    }

    /// <summary>
    /// Nominal basis-point partition retained for source-audit reporting.
    /// Chance Law weights for the Native float path count the exact rounded
    /// 53-bit grid preimages at the source's float thresholds.
    /// </summary>
    public static (long Rare, long Uncommon, long Common)
        MerchantRarityWeights(int offsetBasisPoints, int ascension)
    {
        if (offsetBasisPoints is < InitialOffsetBasisPoints or
            > MaximumOffsetBasisPoints)
            throw new ArgumentOutOfRangeException(nameof(offsetBasisPoints));
        var (rareBase, uncommonBase) = Thresholds(
            ascension, PrototypeNativeCardOddsSource.Shop);
        var rareThreshold = checked(rareBase + offsetBasisPoints);
        if (rareThreshold >= 10000)
            return (10000, 0, 0);
        var rare = Math.Clamp(rareThreshold, 0, 10000);
        var uncommonEnd = Math.Clamp(
            checked(rareThreshold + uncommonBase), 0, 10000);
        var uncommon = Math.Max(0, uncommonEnd - rare);
        var common = checked(10000 - rare - uncommon);
        if (uncommon < 0 || common < 0)
            throw new InvalidOperationException(
                "Native merchant rarity thresholds do not partition the unit interval.");
        return (rare, uncommon, common);
    }

    /// <summary>Return the pinned source thresholds in integer basis points
    /// for a valid persistent offset and card source.</summary>
    public static (int Rare, int UncommonEnd) NativeFloatThresholds(
        int offsetBasisPoints, int ascension,
        PrototypeNativeCardOddsSource source)
    {
        if (offsetBasisPoints is < InitialOffsetBasisPoints or
            > MaximumOffsetBasisPoints || offsetBasisPoints % 50 != 0)
            throw new ArgumentOutOfRangeException(nameof(offsetBasisPoints));
        var (rareBase, uncommonBase) = Thresholds(ascension, source);
        var rare = checked(rareBase + (source ==
            PrototypeNativeCardOddsSource.BossEncounter
                ? 0 : offsetBasisPoints));
        return (rare, checked(rare + uncommonBase));
    }

    internal static (int Rare, int UncommonEnd)
        MerchantFloatThresholds(int offsetBasisPoints, int ascension) =>
        NativeFloatThresholds(offsetBasisPoints, ascension,
            PrototypeNativeCardOddsSource.Shop);

    private static int RareThreshold(
        int offsetBasisPoints, PrototypeNativeCardOddsSource source,
        int ascension) =>
        Thresholds(ascension, source).RareBase
        + (source == PrototypeNativeCardOddsSource.BossEncounter
            ? 0 : offsetBasisPoints);

    internal static PrototypeCardRarity ResolveRarity(
        float roll, int offsetBasisPoints,
        PrototypeNativeCardOddsSource source, int ascension)
    {
        var (rareBase, uncommonBase) = Thresholds(ascension, source);
        var rareThreshold = rareBase
            + (source == PrototypeNativeCardOddsSource.BossEncounter
                ? 0 : offsetBasisPoints);
        if (rareThreshold >= 10000
            || roll < rareThreshold / 10000f)
            return PrototypeCardRarity.Rare;
        return roll < (rareThreshold + uncommonBase) / 10000f
            ? PrototypeCardRarity.Uncommon
            : PrototypeCardRarity.Common;
    }

    internal static int NextOffsetBasisPoints(
        PrototypeCardRarity rarity, int currentOffsetBasisPoints,
        int ascension, PrototypeNativeCardOddsSource source) =>
        source == PrototypeNativeCardOddsSource.Shop
            ? currentOffsetBasisPoints
            : rarity == PrototypeCardRarity.Rare
                ? InitialOffsetBasisPoints
                : Math.Min(MaximumOffsetBasisPoints,
                    currentOffsetBasisPoints + GrowthBasisPoints(ascension));

    internal static float MerchantRarityRepresentative(
        string outcomeId, int offsetBasisPoints, int ascension)
        => NativeRarityRepresentative(outcomeId, offsetBasisPoints,
            ascension, PrototypeNativeCardOddsSource.Shop);

    internal static float NativeRarityRepresentative(
        string outcomeId, int offsetBasisPoints, int ascension,
        PrototypeNativeCardOddsSource source)
    {
        var (rareThreshold, uncommonEnd) = NativeFloatThresholds(
            offsetBasisPoints, ascension, source);
        var rareEnd = rareThreshold / 10000f;
        var uncommonEndFloat = uncommonEnd / 10000f;
        var roll = outcomeId switch
        {
            "rarity:rare" when rareThreshold > 0 => 0f,
            "rarity:uncommon" when uncommonEnd > rareThreshold =>
                (rareEnd + uncommonEndFloat) / 2f,
            "rarity:common" when uncommonEnd < 10000 =>
                (uncommonEndFloat + 1f) / 2f,
            _ => throw new ArgumentException(
                "Outcome is not a positive Native card rarity.",
                nameof(outcomeId))
        };
        if (!float.IsFinite(roll) || roll < 0 || roll > 1 ||
            ResolveRarity(roll, offsetBasisPoints,
                source, ascension).ToString()
                .ToLowerInvariant() != outcomeId[7..])
            throw new InvalidOperationException(
                "No representative source float maps to the selected card rarity.");
        return roll;
    }

    public static (PrototypeCardRarity Rarity, int NextOffsetBasisPoints)
        Roll(int currentOffsetBasisPoints, int ascension,
            PrototypeNativeCardOddsSource source, RngBundle rng)
    {
        var (_, uncommonBase) = Thresholds(ascension, source);

        // Native CardRarityOdds owns Rewards RNG even for merchant
        // rarity; the resulting merchant card selection uses Shops.
        var roll = LegacyRandomChoiceV111.ChooseBoundedInteger(
            rng,
            "card_rarity.roll.ticket",
            "reward",
            $"source:{source}:offset:{currentOffsetBasisPoints}",
            10000).SelectedIndex;
        var rareThreshold = RareThreshold(
            currentOffsetBasisPoints, source, ascension);
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

    private static (PrototypeCardRarity Rarity, int NextOffsetBasisPoints)
        RollNative(int currentOffsetBasisPoints, int ascension,
            PrototypeNativeCardOddsSource source, RngBundle nativeRunRng,
            string ownerId)
    {
        var decision = NativePlayerRngV111.NextUnitFloatDecision(
            nativeRunRng,
            SemanticCardOddsNativeAuditV111.RaritySiteId,
            NativePlayerRngV111.RewardsStreamId,
            ownerId);
        var roll = decision.SelectedValue;
        var rarity = ResolveRarity(
            roll, currentOffsetBasisPoints, source, ascension);
        var nextOffset = NextOffsetBasisPoints(
            rarity, currentOffsetBasisPoints, ascension, source);
        if (decision.ProviderMode == "native-v111" &&
            RandomBoundaryJournalV111.HasActiveJournal)
            RandomBoundaryJournalV111.RecordCardOddsOutcome(
                SemanticCardOddsNativeAuditV111.CreateRarity(
                    decision, source, currentOffsetBasisPoints, ascension));
        return (rarity, nextOffset);
    }

    internal static int EncounterCardUpgradeThresholdBasisPoints(
        string cardId, int act, int ascension)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (ascension < 0)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        var definition = PrototypeContent.Card(cardId);
        return definition.Rarity == PrototypeCardRarity.Rare ||
            definition.MaxUpgradeLevel <= 0
                ? 0
                : (int)Math.Min(10000L,
                    (long)(act - 1) * (ascension >= ScarcityAscension
                        ? 2500L : 1250L));
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

    /// <summary>
    /// Pinned CardFactory.RollForUpgrade's zero-base reward odds:
    /// (act-1) * 12.5% below Scarcity, or (act-1) * 25% at A7+.
    /// Rare and non-upgradable cards remain unupgraded, but still
    /// consume an RNG draw. The discrete sampler treats zero as zero;
    /// native NextFloat() <= 0 can extremely rarely upgrade on zero.
    /// </summary>
    public static bool RollEncounterCardUpgrade(
        string cardId, int act, int ascension, RngBundle rng,
        RngBundle? nativeRunRng = null)
    {
        var threshold = EncounterCardUpgradeThresholdBasisPoints(
            cardId, act, ascension);
        var ownerId = $"card:{cardId}:act:{act}:ascension:{ascension}";
        if (nativeRunRng is not null)
        {
            var decision = NativePlayerRngV111.NextUnitFloatDecision(
                nativeRunRng,
                SemanticCardOddsNativeAuditV111.UpgradeSiteId,
                NativePlayerRngV111.RewardsStreamId,
                ownerId);
            var roll = decision.SelectedValue;
            if (decision.ProviderMode == "native-v111" &&
                RandomBoundaryJournalV111.HasActiveJournal)
                RandomBoundaryJournalV111.RecordCardOddsOutcome(
                    SemanticCardOddsNativeAuditV111.CreateUpgrade(
                        decision, cardId, act, ascension));
            return roll <= threshold / 10000f;
        }

        var ticket = LegacyRandomChoiceV111.ChooseBoundedInteger(
            rng, "card_rarity.upgrade_roll.ticket", "reward",
            ownerId, 10000).SelectedIndex;
        return ticket < threshold;
    }

    public static (string[] Cards, int NextOffsetBasisPoints,
        bool[] UpgradeFlags)
        GenerateEncounterCards(
            int count, int ascension, PrototypeRoomType room,
            int currentOffsetBasisPoints, RngBundle rng,
            int act = 1, RngBundle? nativeRunRng = null)
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
        var pool = PrototypeContent.NativeSilentRewardCardPool.ToList();
        var cards = new List<string>(count);
        var upgrades = new List<bool>(count);
        var offset = currentOffsetBasisPoints;
        for (var i = 0; i < count; i++)
        {
            var ownerId =
                $"act:{act}:room:{room}:pick:{i}:offset:{offset}";
            var (rolled, updated) = nativeRunRng is null
                ? Roll(offset, ascension, source, rng)
                : RollNative(offset, ascension, source,
                    nativeRunRng, ownerId);
            offset = updated;
            var rarity = NextAvailableRarity(
                rolled,
                pool.Select(id => PrototypeContent.Card(id).Rarity)
                    .Distinct().ToArray());
            var candidates = pool.Where(id =>
                PrototypeContent.Card(id).Rarity == rarity).ToArray();
            var selectionOwner =
                $"act:{act}:room:{room}:pick:{i}:rarity:{rarity}:pool:"
                    + CanonicalJson.Sha256(candidates);
            int selectedIndex;
            if (nativeRunRng is null)
            {
                selectedIndex = LegacyRandomChoiceV111.ChooseIndexRange(
                    rng, StochasticSiteRegistryV111.NativeRewardCardModelSiteId,
                    "reward", selectionOwner, candidates.Length).SelectedIndex;
            }
            else
            {
                var decision = NativePlayerRngV111.ChooseIndexDecision(
                    nativeRunRng,
                    StochasticSiteRegistryV111.NativeRewardCardModelSiteId,
                    NativePlayerRngV111.RewardsStreamId,
                    selectionOwner,
                    candidates.Length);
                selectedIndex = decision.SelectedIndex;
                if (decision.ProviderMode == "native-v111" &&
                    RandomBoundaryJournalV111.HasActiveJournal)
                    RandomBoundaryJournalV111
                        .RecordGeneratedCardModelOutcome(
                            SemanticGeneratedCardNativeAuditV111
                                .CreateNativeReward(decision, candidates));
            }
            var selected = candidates[selectedIndex];
            cards.Add(selected);
            pool.Remove(selected);
            upgrades.Add(RollEncounterCardUpgrade(
                selected, act, ascension, rng, nativeRunRng));
        }
        return (cards.ToArray(), offset, upgrades.ToArray());
    }

    public static string[] GenerateMerchantCards(
        int count, int ascension, PrototypeCardType type,
        int currentOffsetBasisPoints, RngBundle rng,
        RngBundle? nativeRunRng = null)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        // Merchant cards are individually generated from the same
        // eligible character pool; duplicate models are legal.
        var pool = PrototypeContent.NativeSilentRewardCardPool
            .Where(id => PrototypeContent.Card(id).Type == type)
            .ToArray();
        var cards = new string[count];
        for (var i = 0; i < count; i++)
        {
            PrototypeCardRarity rolled;
            if (nativeRunRng is null)
            {
                var (legacyRarity, unchanged) = Roll(
                    currentOffsetBasisPoints, ascension,
                    PrototypeNativeCardOddsSource.Shop, rng);
                if (unchanged != currentOffsetBasisPoints)
                    throw new InvalidOperationException(
                        "Merchant generation changed persistent rarity odds.");
                rolled = legacyRarity;
            }
            else
            {
                var ownerId = $"type:{type}:offer:{i}:offset:{currentOffsetBasisPoints}";
                var roll = NativePlayerRngV111.NextUnitFloat(
                    nativeRunRng,
                    "card_rarity.roll.unit_float",
                    NativePlayerRngV111.RewardsStreamId,
                    ownerId);
                rolled = ResolveRarity(
                    roll, currentOffsetBasisPoints,
                    PrototypeNativeCardOddsSource.Shop, ascension);
            }

            var rarity = NextAvailableRarity(rolled,
                pool.Select(id => PrototypeContent.Card(id).Rarity)
                    .Distinct().ToArray());
            var candidates = pool.Where(id =>
                PrototypeContent.Card(id).Rarity == rarity).ToArray();
            var selectionOwner =
                $"type:{type}:offer:{i}:rarity:{rarity}:pool:"
                    + CanonicalJson.Sha256(candidates);
            var selectedIndex = nativeRunRng is null
                ? LegacyRandomChoiceV111.ChooseIndexRange(
                    rng,
                    "shop.merchant.card_model.index_draw",
                    "shop",
                    selectionOwner,
                    candidates.Length).SelectedIndex
                : NativePlayerRngV111.ChooseIndex(
                    nativeRunRng,
                    "shop.merchant.card_model.index_draw",
                    NativePlayerRngV111.ShopsStreamId,
                    selectionOwner,
                    candidates.Length);
            cards[i] = candidates[selectedIndex];
            // CardFactory.CreateForMerchant consumes a Shops upgrade
            // roll even though its negative base chance prevents upgrades.
            var upgradeOwner = $"type:{type}:offer:{i}:card:{cards[i]}";
            if (nativeRunRng is null)
            {
                _ = LegacyRandomChoiceV111.ChooseBoundedInteger(
                    rng,
                    "shop.merchant.card_upgrade_roll.ticket",
                    "shop",
                    upgradeOwner,
                    10000);
            }
            else
            {
                _ = NativePlayerRngV111.NextUnitFloat(
                    nativeRunRng,
                    "shop.merchant.card_upgrade_roll.unit_float",
                    NativePlayerRngV111.ShopsStreamId,
                    upgradeOwner);
            }
        }
        return cards;
    }
}
