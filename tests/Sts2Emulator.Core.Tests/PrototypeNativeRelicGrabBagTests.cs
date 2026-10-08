using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeRelicGrabBagTests
{

    [Theory]
    [InlineData("proto.relic.anchor", 175)]
    [InlineData("proto.relic.blood_vial", 175)]
    [InlineData("proto.relic.nunchaku", 225)]
    [InlineData("proto.relic.lucky_fysh", 225)]
    [InlineData("proto.relic.old_coin", 275)]
    [InlineData("proto.relic.kunai", 275)]
    [InlineData("proto.relic.lees_waffle", 200)]
    [InlineData("proto.relic.membership_card", 200)]
    public void NativeMerchantUsesSourceRarityBaseCost(
        string relicId, int expectedCost)
    {
        Assert.Equal(expectedCost,
            PrototypeNativeRelicGrabBag.MerchantBaseCost(relicId));
    }

    [Fact]
    public void NativeMerchantPriceRollsStayWithinRarityBounds()
    {
        var rng = PrototypeRng.CreateBundle("native-merchant-price");
        foreach (var id in new[]
        {
            "proto.relic.anchor", "proto.relic.nunchaku",
            "proto.relic.kunai", "proto.relic.membership_card"
        })
        {
            var baseCost = PrototypeNativeRelicGrabBag.MerchantBaseCost(id);
            var minimum = (int)Math.Round(baseCost * 0.85);
            var maximum = (int)Math.Round(baseCost * 1.15);
            for (var i = 0; i < 128; i++)
            {
                Assert.InRange(
                    PrototypeNativeRelicGrabBag.MerchantPrice(id, rng),
                    minimum, maximum);
            }
        }

        Assert.Throws<InvalidOperationException>(() =>
            PrototypeNativeRelicGrabBag.MerchantBaseCost(
                "proto.relic.darkstone_periapt"));
    }

    [Fact]
    public void NativeBagContainsOnlyPinnedSourceClassifiedRelics()
    {
        var initial = PrototypeNativeOvergrowthRunFactory.Create(
            "native-relic-catalog");
        var bag = PrototypeNativeRelicGrabBag.Create(
            initial.Player, initial.Rng.Fork());
        Assert.Equal(10, bag.Common.Length);
        Assert.Equal(11, bag.Uncommon.Length);
        Assert.Equal(15, bag.Rare.Length);
        Assert.Equal(4, bag.Shop.Length);
        Assert.Equal(40, bag.Remaining.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain("proto.relic.darkstone_periapt", bag.Remaining);
        Assert.DoesNotContain("proto.relic.forgotten_soul", bag.Remaining);
        Assert.DoesNotContain("proto.relic.ink_bottle", bag.Remaining);
        Assert.All(bag.Shop, id =>
        {
            Assert.True(PrototypeNativeRelicGrabBag.TryGetRarity(id, out var rarity));
            Assert.Equal(PrototypeRelicRarity.Shop, rarity);
        });
    }

    [Fact]
    public void RewardsPullFromFrontAndShopsPullFromBackWithoutReplacement()
    {
        var initial = PrototypeNativeOvergrowthRunFactory.Create(
            "native-relic-ends");
        var bag = new PrototypeRelicBagState(
            ["proto.relic.anchor", "proto.relic.vajra",
             "proto.relic.blood_vial"], [], [], []);
        var (afterReward, first) = PrototypeNativeRelicGrabBag.Pull(
            bag, PrototypeRelicRarity.Common,
            merchant: false, initial.Player);
        Assert.Equal("proto.relic.anchor", first);
        Assert.Equal(3, bag.Common.Length);
        Assert.Equal(new[] { "proto.relic.vajra", "proto.relic.blood_vial" },
            afterReward.Common);
        var (afterShop, last) = PrototypeNativeRelicGrabBag.Pull(
            afterReward, PrototypeRelicRarity.Common,
            merchant: true, initial.Player);
        Assert.Equal("proto.relic.blood_vial", last);
        Assert.Equal(new[] { "proto.relic.vajra" }, afterShop.Common);
        var (exhausted, final) = PrototypeNativeRelicGrabBag.Pull(
            afterShop, PrototypeRelicRarity.Common,
            merchant: false, initial.Player);
        Assert.Equal("proto.relic.vajra", final);
        Assert.Empty(exhausted.Remaining);
        Assert.Null(PrototypeNativeRelicGrabBag.Pull(
            exhausted, PrototypeRelicRarity.Common,
            merchant: true, initial.Player).Id);
    }

    [Fact]
    public void ShopOnlyRarityFallsBackAndForbiddenShopRelicsAreSkipped()
    {
        var player = PrototypeNativeOvergrowthRunFactory.Create(
            "native-relic-fallback").Player;
        var bag = new PrototypeRelicBagState(
            ["proto.relic.anchor"],
            ["proto.relic.lucky_fysh", "proto.relic.tingsha"],
            ["proto.relic.old_coin", "proto.relic.kunai"],
            []);
        var (updated, id) = PrototypeNativeRelicGrabBag.Pull(
            bag, PrototypeRelicRarity.Shop, merchant: true, player);
        Assert.Equal("proto.relic.anchor", id);
        Assert.Empty(updated.Common);

        var (_, uncommon) = PrototypeNativeRelicGrabBag.Pull(
            updated, PrototypeRelicRarity.Uncommon,
            merchant: true, player);
        Assert.Equal("proto.relic.tingsha", uncommon);

        var rareOnly = bag with { Common = [], Uncommon = [] };
        var (_, rare) = PrototypeNativeRelicGrabBag.Pull(
            rareOnly, PrototypeRelicRarity.Rare,
            merchant: true, player);
        Assert.Equal("proto.relic.kunai", rare);
    }

    [Fact]
    public void NativeShopReservesItsRelicsAtGenerationAndForksDeeply()
    {
        var engine = new PrototypeGameEngine();
        var initial = PrototypeNativeOvergrowthRunFactory.Create(
            "native-relic-shop-integration");
        var map = initial.World!.Map;
        var node = map.Nodes.First(item =>
            item.RoomType == PrototypeRoomType.Shop);
        var parent = map.Nodes.First(item =>
            item.NextNodeIds?.Contains(node.NodeId,
                StringComparer.Ordinal) == true);
        var history = Enumerable.Range(1, parent.Floor)
            .Select(floor =>
            {
                var selected = floor == parent.Floor
                    ? parent
                    : map.Nodes.First(item => item.Floor == floor);
                return new PrototypeCompletedRoomRecord(
                    1, floor, selected.NodeId, selected.RoomType);
            }).ToArray();
        var ready = initial with
        {
            Phase = RunPhase.MapChoice,
            World = initial.World with
            {
                Floor = parent.Floor,
                ActiveRoom = null,
                CompletedRoomHistory = history,
                Map = map with { CurrentNodeId = parent.NodeId }
            }
        };
        PrototypeStateInvariants.Validate(ready);
        var state = engine.Step(ready, GameAction.Create(
            "choose_map_node", new ChooseMapNodePayload(node.NodeId))).State;
        Assert.Equal(RunPhase.Shop, state.Phase);
        var potionOffers = state.World!.Shop!.PotionOffers;
        Assert.Equal(3, potionOffers.Length);
        foreach (var offer in potionOffers)
        {
            var cost = PrototypeNativePotionShop.MerchantBaseCost(
                offer.ItemId);
            Assert.InRange(offer.Price,
                (int)Math.Round(cost * 0.95),
                (int)Math.Round(cost * 1.05));
            Assert.Equal(offer.Price, offer.UndiscountedPrice);
        }

        var offers = state.World.Shop.RelicOffers;
        Assert.Equal(3, offers.Length);
        Assert.Equal(3, offers.Select(offer => offer.ItemId)
            .Distinct(StringComparer.Ordinal).Count());
        var bag = Assert.IsType<PrototypeRelicBagState>(state.World.RelicBag);
        Assert.All(offers, offer =>
            Assert.DoesNotContain(offer.ItemId, bag.Remaining));
        Assert.Contains(offers,
            offer => PrototypeNativeRelicGrabBag.TryGetRarity(
                offer.ItemId, out var rarity)
                && rarity == PrototypeRelicRarity.Shop);
        foreach (var offer in offers)
        {
            var baseCost =
                PrototypeNativeRelicGrabBag.MerchantBaseCost(offer.ItemId);
            Assert.InRange(offer.Price,
                (int)Math.Round(baseCost * 0.85),
                (int)Math.Round(baseCost * 1.15));
            Assert.Equal(offer.Price, offer.UndiscountedPrice);
        }
        PrototypeStateInvariants.Validate(state);

        var fork = state.Fork();
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        if (bag.Common.Length > 0)
        {
            fork.World!.RelicBag!.Common[0] = "changed-only-in-fork";
            Assert.NotEqual("changed-only-in-fork", bag.Common[0]);
        }
    }

    [Fact]
    public void NativeRelicDrawsRemoveReservedItemsAcrossRewardSources()
    {
        var initial = PrototypeNativeOvergrowthRunFactory.Create(
            "native-relic-cross-source");
        var world = initial.World!;
        var rng = initial.Rng.Fork();
        var first = PrototypeNativeRelicGrabBag.Draw(
            world, initial.Player, rng, merchant: true,
            forcedRarity: PrototypeRelicRarity.Common);
        var second = PrototypeNativeRelicGrabBag.Draw(
            first.World, initial.Player, rng, merchant: false,
            forcedRarity: PrototypeRelicRarity.Common);
        Assert.NotNull(first.Id);
        Assert.NotNull(second.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.DoesNotContain(first.Id!, second.World.RelicBag!.Remaining);
        Assert.DoesNotContain(second.Id!, second.World.RelicBag.Remaining);
        Assert.Equal(2, first.World.RelicBag!.Remaining.Length -
            second.World.RelicBag.Remaining.Length + 1);
        PrototypeStateInvariants.Validate(initial with { World = second.World });
    }
}
