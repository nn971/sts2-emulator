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

    private static PlayerState ApplyRelicRoomEntryGold(PlayerState player)
    {
        var bonus = player.Relics.Sum(relic =>
        {
            var definition = PrototypeContent.Relic(relic.RelicId);
            return !IsPurchaseExhausted(relic)
                ? definition.GoldOnRoomEntryUntilPurchase : 0;
        });
        return bonus > 0
            ? player with { Gold = checked(player.Gold + bonus) }
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
        player.Relics
            .Select(relic => PrototypeContent.Relic(relic.RelicId)
                .MinPoweredAttackHpLoss)
            .DefaultIfEmpty(0).Max();

    private static RunState EnterRestHealRelicReward(RunState state)
    {
        var world = RequireWorld(state);
        var rewardCounts = state.Player.Relics
            .Select(relic => PrototypeContent.Relic(relic.RelicId)
                .RestHealCardRewardCount)
            .Where(count => count > 0).ToArray();
        if (rewardCounts.Length == 0)
        {
            return CompleteRoomToMap(state);
        }

        // Native Dream Catcher adds one ordinary 3-choice CardReward
        // after the RestSite heal. No card reward is given on train or
        // smith actions. Each relic adds its own reward group.
        var groups = rewardCounts
            .Select(count => PickRewardCards(
                world.Act, count, state.Rng))
            .ToArray();
        var reward = new RewardState(
            SourceRoom: "Rest",
            CardOptions: groups[0],
            PotionOption: null,
            RelicOption: null,
            CardResolved: false,
            PotionResolved: true,
            RelicResolved: true,
            EndsAct: false,
            ExtraCardOptions: groups.Skip(1).ToArray());

        return state with
        {
            World = world with { Reward = reward },
            Phase = RunPhase.Reward
        };
    }
}
