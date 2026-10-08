namespace Sts2Emulator.Core;

/// <summary>
/// Native Neow acquisition options that suspend the selected event and
/// present a normal typed reward phase. Exact native reward RNG and
/// unlock-state restrictions are deliberately deferred.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static RewardState CreateDeferredEventReward(
        RunState state,
        PrototypeRunEffectKind kind,
        PlayerState player,
        string eventId)
    {
        var prefix = "Event:" + eventId;
        switch (kind)
        {
            case PrototypeRunEffectKind.OfferThreeRareCards:
            {
                var rarePool = PrototypeContent.RewardCardPool
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
                    AddCardAfterReward: "proto.native.neow.injury");
            }

            case PrototypeRunEffectKind.OfferThreeCardsAndPotion:
            {
                var cards = PickRewardCards(
                    RequireWorld(state).Act, 3, state.Rng);
                var potion = CanAcquirePotion(player)
                    ? PrototypeContent.PotionPool[
                        PrototypeRng.NextInt(state.Rng,
                            "reward", PrototypeContent.PotionPool.Length)]
                    : null;
                return new RewardState(
                    SourceRoom: prefix,
                    CardOptions: cards,
                    PotionOption: potion,
                    RelicOption: null,
                    CardResolved: false,
                    PotionResolved: potion is null,
                    RelicResolved: true,
                    EndsAct: false);
            }

            case PrototypeRunEffectKind.OfferCardBundles:
            {
                var commons = PrototypeContent.RewardCardPool
                    .Where(id => PrototypeContent.Card(id).Rarity ==
                        PrototypeCardRarity.Common)
                    .ToArray();
                var uncommons = PrototypeContent.RewardCardPool
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
                PrototypeRng.Shuffle(state.Rng, "reward", commons);
                PrototypeRng.Shuffle(state.Rng, "reward", uncommons);
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
                // The pinned native Neow's Bones samples from all
                // allowed Neow relics except Bones itself. Until the
                // remaining special acquisition hooks are implemented,
                // restrict this generator to already-functional
                // single-player Silent relic acquisition paths.
                string[] supportedNativeNames =
                [
                    "BoomingConch", "FishingRod", "GoldenPearl",
                    "LavaRock", "NutritiousOyster", "StoneHumidifier",
                    "NeowsTalisman", "Pomander", "PreciseScissors",
                    "NewLeaf", "SilverCrucible", "SilkenTress"
                ];
                var available = supportedNativeNames
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
                    // Injury is the sole ordinary standalone Curse
                    // implemented in the current Silent prototype.
                    AddCardAfterReward: "proto.native.neow.injury");
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
                var relicId = available[
                    PrototypeRng.NextInt(state.Rng, "reward", available.Length)];
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
}
