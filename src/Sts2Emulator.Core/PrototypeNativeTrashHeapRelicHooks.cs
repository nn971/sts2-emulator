using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped generic relic modifiers shared across room, merchant,
/// rest-site and combat transitions. No new items are added to the ordinary
/// relic generation pool by these helpers.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private sealed record PurchaseExhaustedState(bool Purchased);

    private static bool IsPurchaseExhausted(RelicInstance relic) =>
        relic.PersistentState.ValueKind == JsonValueKind.Object
        && relic.PersistentState.TryGetProperty(
            nameof(PurchaseExhaustedState.Purchased), out var purchased)
        && purchased.ValueKind == JsonValueKind.True;

    private static PlayerState ApplyRelicRoomEntryEffects(
        PlayerState player,
        PrototypeRoomType room,
        RngBundle? rng = null,
        RunWorldState? world = null,
        RngBundle? nativeRunRng = null)
    {
        if (room == PrototypeRoomType.Rest)
        {
            player = PrototypeNativeFakeMerchant
                .MarkTeaSetsAfterRestSite(player);
        }

        var bonus = player.ActiveRelics.Sum(relic =>
        {
            var definition = PrototypeContent.Relic(relic.RelicId);
            return !IsPurchaseExhausted(relic)
                ? definition.GoldOnRoomEntryUntilPurchase : 0;
        });
        player = bonus > 0
            ? ApplyGoldGain(
                player, bonus, rng, world, nativeRunRng: nativeRunRng)
            : player;
        if (room != PrototypeRoomType.Rest)
            return player;

        var healPerFiveCards = player.ActiveRelics.Sum(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .RestSiteHealPerFiveCards);
        var heal = player.Deck.Length / 5 * healPerFiveCards;
        return heal > 0
            ? player with { Hp = Math.Min(player.MaxHp, player.Hp + heal) }
            : player;
    }

    private static PlayerState ApplyFirstPositiveShopPurchase(
        PlayerState player, int goldSpent)
    {
        if (goldSpent <= 0)
        {
            return player;
        }

        return player with
        {
            Relics = player.Relics.Select(relic =>
            {
                var definition = PrototypeContent.Relic(relic.RelicId);
                return definition.GoldOnRoomEntryUntilPurchase > 0
                    && !relic.IsMelted
                    && !IsPurchaseExhausted(relic)
                    ? relic with
                    {
                        PersistentState = JsonSerializer.SerializeToElement(
                            new PurchaseExhaustedState(true))
                    }
                    : relic;
            }).ToArray()
        };
    }

    private static int MinimumPoweredAttackHpLoss(PlayerState player) =>
        player.ActiveRelics
            .Select(relic => PrototypeContent.Relic(relic.RelicId)
                .MinPoweredAttackHpLoss)
            .DefaultIfEmpty(0).Max();

    private static RunState EnterRestHealRelicReward(RunState state)
    {
        var world = RequireWorld(state);
        var native = UsesNativeRegionalSystems(world);
        var cardGroups = new List<string[]>();
        var cardUpgradeGroups = new List<bool[]>();
        var potionOptions = new List<string>();
        foreach (var relic in state.Player.ActiveRelics)
        {
            var definition = PrototypeContent.Relic(relic.RelicId);
            if (definition.RestHealCardRewardCount > 0)
            {
                if (native)
                {
                    var next =
                        PrototypeNativeCardRarityOdds.GenerateEncounterCards(
                            definition.RestHealCardRewardCount,
                            state.Ascension, PrototypeRoomType.Combat,
                            world.CardRarityOffsetBasisPoints, state.Rng,
                            act: world.Act);
                    cardGroups.Add(next.Cards);
                    cardUpgradeGroups.Add(next.UpgradeFlags);
                    world = world with
                    {
                        CardRarityOffsetBasisPoints =
                            next.NextOffsetBasisPoints
                    };
                }
                else
                {
                    cardGroups.Add(PickRewardCards(
                        world.Act,
                        definition.RestHealCardRewardCount,
                        state.Rng));
                }
            }

            if (definition.RestHealPotionRewardCount > 0)
            {
                potionOptions.AddRange(
                    PrototypeNativePotionShop.PickWeightedDistinct(
                        state.Rng, "reward",
                        definition.RestHealPotionRewardCount));
            }
        }
        if (cardGroups.Count == 0 && potionOptions.Count == 0)
            return CompleteRoomToMap(state);

        var reward = new RewardState(
            SourceRoom: "Rest",
            CardOptions: cardGroups.Count == 0 ? [] : cardGroups[0],
            PotionOption: null,
            RelicOption: null,
            CardResolved: cardGroups.Count == 0,
            PotionResolved: true,
            RelicResolved: true,
            EndsAct: false,
            ExtraCardOptions: cardGroups.Skip(1).ToArray(),
            CardOptionUpgradeFlags:
                native && cardGroups.Count > 0
                    ? cardUpgradeGroups[0] : null,
            ExtraCardOptionUpgradeFlags:
                native ? cardUpgradeGroups.Skip(1).ToArray() : null,
            IndependentSelection: native || potionOptions.Count > 0,
            ExtraCardGroupsResolved: native
                ? new bool[Math.Max(0, cardGroups.Count - 1)] : null,
            ExtraRelicGroupsResolved: native ? [] : null,
            ExtraPotionOptions: potionOptions.Count > 0
                ? potionOptions.ToArray() : null,
            ExtraPotionGroupsResolved: potionOptions.Count > 0
                ? new bool[potionOptions.Count] : null,
            CardRewardGenerators: cardGroups.Select(_ =>
                (PrototypeCardRewardGenerator?)new(
                    native
                        ? PrototypeCardRewardGeneratorKind.Encounter
                        : PrototypeCardRewardGeneratorKind.RewardPool,
                    world.Act,
                    native ? PrototypeRoomType.Combat : null)).ToArray());

        return state with
        {
            World = world with { Reward = reward },
            Phase = RunPhase.Reward
        };
    }
}
