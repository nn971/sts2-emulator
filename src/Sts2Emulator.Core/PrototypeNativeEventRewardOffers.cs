namespace Sts2Emulator.Core;

/// <summary>
/// Native Neow acquisition options that suspend the selected event and
/// present a normal typed reward phase. Exact native reward RNG and
/// unlock-state restrictions are deliberately deferred.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static RewardState CreateDeferredEventReward(
        RunState state, PrototypeRunEffectKind kind, PlayerState player,
        string eventId, RngBundle? nativeRunRng)
    {
        var world = RequireWorld(state);
        if (kind == PrototypeRunEffectKind.OfferWaxRelics)
            throw new InvalidOperationException("Wax reward construction requires retained bag updates.");
        return CreateDeferredEventRewardCore(
            state, kind, player, eventId, ref world,
            nativeRunRng);
    }

    private static RewardState CreateDeferredEventReward(
        RunState state,
        PrototypeRunEffectKind kind,
        ref PlayerState player,
        string eventId,
        ref RunWorldState world,
        RngBundle? nativeRunRng)
    {
        var reward = CreateDeferredEventRewardCore(
            state, kind, player, eventId, ref world, nativeRunRng);
        if (kind is not (PrototypeRunEffectKind.OfferThreeRareCards
            or PrototypeRunEffectKind.OfferThreeCardsAndPotion
            or PrototypeRunEffectKind.OfferFiveClassCards
            or PrototypeRunEffectKind.OfferThreeColorlessCards))
        {
            return reward;
        }

        var primaryGroupCount = reward.CardOptions.Length > 0 ? 1 : 0;
        var extraGroupCount = reward.ExtraCardOptions?.Length ?? 0;
        var groupCount = primaryGroupCount + extraGroupCount;
        if (groupCount == 0)
        {
            return reward;
        }

        var upgraded = ApplySilverCrucibleToCardRewardGeneration(
            player, groupCount);
        player = upgraded.Player;
        var groupIndex = 0;
        var primaryUpgraded = primaryGroupCount > 0
            && upgraded.UpgradeGroups[groupIndex++];
        var extraUpgrades = extraGroupCount > 0
            ? upgraded.UpgradeGroups.Skip(groupIndex).ToArray()
            : reward.ExtraCardOptionsUpgraded;
        return reward with
        {
            CardOptionsUpgraded = primaryUpgraded,
            ExtraCardOptionsUpgraded = extraUpgrades
        };
    }

    private static RewardState CreateDeferredEventRewardCore(
        RunState state,
        PrototypeRunEffectKind kind,
        PlayerState player,
        string eventId,
        ref RunWorldState world,
        RngBundle? nativeRunRng = null)
    {
        var prefix = "Event:" + eventId;
        switch (kind)
        {
            case PrototypeRunEffectKind.OfferWaxRelics:
            {
                return CreateWaxRelicReward(
                    state, player, prefix, ref world, nativeRunRng);
            }
            case PrototypeRunEffectKind.OfferThreeRareCards:
            {
                var rarePool = PrototypeContent.NativeSilentRewardCardPool
                    .Where(id => PrototypeContent.Card(id).Rarity
                        == PrototypeCardRarity.Rare)
                    .ToArray();
                var cards = PickDistinct(
                    rarePool, 3, state.Rng, "reward");
                return new RewardState(
                    SourceRoom: prefix,
                    CardOptions: cards,
                    PotionOption: null,
                    RelicOption: null,
                    CardResolved: false,
                    PotionResolved: true,
                    RelicResolved: true,
                    EndsAct: false,
                    AddCardAfterReward: "proto.native.neow.injury",
                    CardRewardGenerators:
                    [new PrototypeCardRewardGenerator(
                        PrototypeCardRewardGeneratorKind.RarePool,
                        world.Act)]);
            }

            case PrototypeRunEffectKind.OfferThreeCardsAndPotion:
            {
                var cards = PickRewardCards(
                    RequireWorld(state).Act, 3, state.Rng);
                var potion = CanAcquirePotion(player)
                    ? PrototypeContent.PotionPool[
                        LegacyRandomChoiceV111.ChooseIndexRange(
                            state.Rng,
                            "event.reward.offer_potion.model.index_draw",
                            "reward",
                            prefix + ":potion_pool:"
                                + CanonicalJson.Sha256(
                                    PrototypeContent.PotionPool),
                            PrototypeContent.PotionPool.Length).SelectedIndex]
                    : null;
                return new RewardState(
                    SourceRoom: prefix,
                    CardOptions: cards,
                    PotionOption: potion,
                    RelicOption: null,
                    CardResolved: false,
                    PotionResolved: potion is null,
                    RelicResolved: true,
                    EndsAct: false,
                    CardRewardGenerators:
                    [new PrototypeCardRewardGenerator(
                        PrototypeCardRewardGeneratorKind.RewardPool,
                        world.Act)]);
            }

            case PrototypeRunEffectKind.OfferFiveClassCards:
            {
                var cards = PickRewardCards(world.Act, 5, state.Rng);
                if (cards.Length != 5)
                {
                    throw new InvalidOperationException(
                        "Brain Leech requires five distinct class cards.");
                }

                return new RewardState(
                    SourceRoom: prefix,
                    CardOptions: cards,
                    PotionOption: null,
                    RelicOption: null,
                    CardResolved: false,
                    PotionResolved: true,
                    RelicResolved: true,
                    EndsAct: false,
                    CardRewardGenerators:
                    [new PrototypeCardRewardGenerator(
                        PrototypeCardRewardGeneratorKind.RewardPool,
                        world.Act)]);
            }

            case PrototypeRunEffectKind.OfferThreeColorlessCards:
            {
                var pool = PrototypeColorlessCards
                    .ImplementedCombatGenerationPool;
                if (pool.Length < 3)
                {
                    throw new InvalidOperationException(
                        "Brain Leech requires three implemented colorless cards.");
                }

                return new RewardState(
                    SourceRoom: prefix,
                    CardOptions: PickDistinct(
                        pool, 3, state.Rng, "reward"),
                    PotionOption: null,
                    RelicOption: null,
                    CardResolved: false,
                    PotionResolved: true,
                    RelicResolved: true,
                    EndsAct: false);
            }

            case PrototypeRunEffectKind.OfferTwoColorlessCards:
            {
                var pool = PrototypeColorlessCards.ImplementedRewardPool;
                if (pool.Length < 2)
                {
                    throw new InvalidOperationException(
                        "Lead Paperweight requires two implemented reward-eligible Colorless cards.");
                }

                return new RewardState(
                    SourceRoom: prefix,
                    CardOptions: PickDistinct(
                        pool, 2, state.Rng, "reward"),
                    PotionOption: null,
                    RelicOption: null,
                    CardResolved: false,
                    PotionResolved: true,
                    RelicResolved: true,
                    EndsAct: false);
            }

            case PrototypeRunEffectKind.OfferCardBundles:
            {
                var commons = PrototypeContent.NativeSilentRewardCardPool
                    .Where(id => PrototypeContent.Card(id).Rarity ==
                        PrototypeCardRarity.Common)
                    .ToArray();
                var uncommons = PrototypeContent.NativeSilentRewardCardPool
                    .Where(id => PrototypeContent.Card(id).Rarity ==
                        PrototypeCardRarity.Uncommon)
                    .ToArray();
                if (commons.Length < 4 || uncommons.Length < 2)
                {
                    throw new InvalidOperationException(
                        "Scroll Boxes requires four Commons and two Uncommons.");
                }

                // The native single-player Silent version has no
                // Defect-specific Claw bundle exception.
                _ = LegacyFisherYatesV111.ShuffleInPlace(
                    commons,
                    state.Rng,
                    "reward",
                    "event.reward.card_bundle.shuffle_swap_index",
                    prefix + ":common_bundle");
                _ = LegacyFisherYatesV111.ShuffleInPlace(
                    uncommons,
                    state.Rng,
                    "reward",
                    "event.reward.card_bundle.shuffle_swap_index",
                    prefix + ":uncommon_bundle");
                return new RewardState(
                    SourceRoom: prefix,
                    CardOptions: [],
                    PotionOption: null,
                    RelicOption: null,
                    CardResolved: false,
                    PotionResolved: true,
                    RelicResolved: true,
                    EndsAct: false,
                    CardBundles:
                    [
                        [commons[0], commons[1], uncommons[0]],
                        [commons[2], commons[3], uncommons[1]]
                    ]);
            }

            case PrototypeRunEffectKind.OfferNeowsBonesRelics:
            {
                // Pickup continuations are handled after every Neow relic
                // reward. Kaleidoscope's foreign card pools and multiplayer
                // Massive Scroll remain outside this Silent profile.
                var available = PrototypeNativeOvergrowthEvents
                    .NeowBonesSupportedRelicNames
                    .Select(PrototypeNativeOvergrowthEvents.NeowRelicId)
                    .Where(id => !player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(relic.RelicId, id)))
                    .ToArray();
                var chosen = PickDistinct(
                    available, Math.Min(2, available.Length),
                    state.Rng, "reward");
                if (chosen.Length != 2)
                {
                    throw new InvalidOperationException(
                        "Neow's Bones requires two eligible Neow relics.");
                }

                return new RewardState(
                    SourceRoom: prefix,
                    CardOptions: [],
                    PotionOption: null,
                    RelicOption: null,
                    CardResolved: true,
                    PotionResolved: true,
                    RelicResolved: true,
                    EndsAct: false,
                    ExtraRelicRewardIds: chosen,
                    // Draw one source-pool Curse only after both relics
                    // and their acquisition continuations are resolved.
                    AddRandomCurseAfterReward: true)
                {
                    ExtraRelicGroupsSkippable = false
                };
            }

            case PrototypeRunEffectKind.OfferRandomRelic:
            {
                var available = PrototypeContent.RelicPool
                    .Where(id => !player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(relic.RelicId, id)))
                    .ToArray();
                if (available.Length == 0)
                {
                    throw new InvalidOperationException(
                        "No unowned relics available for a custom relic reward.");
                }
                var ownerId = prefix + ":unowned_relic_pool:"
                    + CanonicalJson.Sha256(available);
                var relicIndex = nativeRunRng is { } nativeRewardRng
                    ? NativePlayerRngV111.ChooseIndex(
                        nativeRewardRng,
                        "relic.unowned_random_selection.index_draw",
                        NativePlayerRngV111.RewardsStreamId,
                        ownerId,
                        available.Length)
                    : LegacyRandomChoiceV111.ChooseIndexRange(
                        state.Rng,
                        "relic.unowned_random_selection.index_draw",
                        "reward",
                        ownerId,
                        available.Length).SelectedIndex;
                var relicId = available[relicIndex];
                return new RewardState(
                    SourceRoom: prefix,
                    CardOptions: [],
                    PotionOption: null,
                    RelicOption: relicId,
                    CardResolved: true,
                    PotionResolved: true,
                    RelicResolved: false,
                    EndsAct: false);
            }

            default:
                throw new InvalidOperationException(
                    $"Unsupported deferred event reward kind '{kind}'.");
        }
    }

    private static RewardState CreateCallingBellRelicReward(
        PlayerState player,
        string eventId,
        RngBundle rng,
        ref RunWorldState world,
        RngBundle? nativeRunRng)
    {
        var relics = new List<string>(3);
        foreach (var rarity in new[]
                 {
                     PrototypeRelicRarity.Common,
                     PrototypeRelicRarity.Uncommon,
                     PrototypeRelicRarity.Rare
                 })
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                world, player, rng, merchant: false,
                forcedRarity: rarity,
                nativeRunRng: nativeRunRng);
            world = draw.World;
            if (draw.Id is { } relicId)
                relics.Add(relicId);
        }

        return new RewardState(
            SourceRoom: "Event:" + eventId,
            CardOptions: [],
            PotionOption: null,
            RelicOption: null,
            CardResolved: true,
            PotionResolved: true,
            RelicResolved: true,
            EndsAct: false,
            ExtraRelicRewardIds: relics.ToArray(),
            IndependentSelection: true,
            ExtraRelicGroupsResolved: new bool[relics.Count]);
    }
}
