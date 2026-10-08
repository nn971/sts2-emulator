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
