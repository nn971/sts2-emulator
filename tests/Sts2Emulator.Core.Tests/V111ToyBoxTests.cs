using System.Reflection;
using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111ToyBoxTests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState Offers()
    {
        var state = NativeAncientRewardsV111.CreateTezcataraRewardFixture(
            "toy-box-five-offers", NativeAncientRewardsV111.ToyBoxId);
        var action = Assert.Single(Engine.GetLegalActions(state));
        var parentHash = CanonicalJson.Sha256(state);
        var result = Engine.Step(state, action).State;
        Assert.Equal(parentHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        return result;
    }

    private static RunState Lifecycle(params string[] waxIds)
    {
        var offered = Offers();
        var state = Engine.Step(offered, GameAction.Empty("leave_reward")).State;
        state = state with { Player = state.Player with
        {
            Relics = state.Player.Relics.Concat(waxIds.Select(id =>
                new RelicInstance(id, PrototypeJson.EmptyObject(), IsWax: true))).ToArray(),
            Deck = [new(1, "proto.silent.strike", 0, PrototypeJson.EmptyObject())]
        }};
        return state with { World = PrototypeNativeRelicGrabBag.PruneOwned(state.World!, state.Player) };
    }

    private static RunState Combat(RunState state)
    {
        var method = typeof(PrototypeGameEngine).GetMethod("StartCombat", BindingFlags.Static | BindingFlags.NonPublic)!;
        state = state with
        {
            Player = state.Player with { Hp = 1000, MaxHp = 1000 },
            World = state.World! with
            {
                Map = new MapState([], GenerationProfileId: PrototypeNativeLaterActMap.HiveProfileId),
                Floor = 1, ActiveRoom = PrototypeRoomType.Combat, Event = null, Reward = null, Shop = null
            }
        };
        return (RunState)method.Invoke(null, [state, PrototypeRoomType.Combat,
            PrototypeContent.Encounter("proto.native.hive.encounter.tunneler_weak")])!;
    }

    private static RunState Win(RunState state)
    {
        var combat = state.World!.Combat!;
        state = state with { World = state.World with { Combat = combat with
        {
            Enemies = combat.Enemies.Select(enemy => enemy with { Hp = 1 }).ToArray()
        }}};
        var action = Engine.GetLegalActions(state).First(action => action.Kind == "play_card");
        var result = Engine.Step(state, action).State;
        Assert.Equal(RunPhase.Reward, result.Phase);
        Assert.Equal(CanonicalJson.Sha256(result), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        return result;
    }

    [Fact]
    public void FiveWaxOffersAreReservedAndAcquiredInChosenOrder()
    {
        var state = Offers();
        var reward = state.World!.Reward!;
        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.Equal(5, reward.ExtraRelicRewardIds!.Length);
        Assert.Equal(5, reward.ExtraRelicRewardIds.Distinct().Count());
        Assert.All(reward.ExtraRelicWaxFlags!, Assert.True);
        Assert.DoesNotContain(reward.ExtraRelicRewardIds, id => state.World.RelicBag!.Remaining.Contains(id));
        Assert.DoesNotContain(state.Player.Relics, relic => relic.IsWax);
        Assert.Equal(0, NativeAncientRewardsV111.ToyBoxCombatsSeen(state.Player.Relics
            .Single(relic => relic.RelicId == NativeAncientRewardsV111.ToyBoxId)));
        PrototypeStateInvariants.Validate(state);
        var observedOffers = new PrototypeAiEnvironment().Observe(state).Observation.Reward!.PendingExtraRelicRewards!;
        Assert.Equal(5, observedOffers.Length);
        Assert.All(observedOffers, offer => Assert.True(offer.IsWax));
        var originalHash = CanonicalJson.Sha256(state);
        var copy = state.Fork();
        copy.World!.Reward!.ExtraRelicWaxFlags![0] = false;
        Assert.Equal(originalHash, CanonicalJson.Sha256(state));

        var order = new[] { 3, 1, 4, 2, 0 };
        foreach (var index in order)
        {
            var pending = state.World!.Reward!;
            var action = Engine.GetLegalActions(state).Single(action =>
                action.Kind == (index == pending.FirstPendingExtraRelicGroupIndex
                    ? "take_reward_relic" : "take_reward_extra_relic")
                && action.ReadPayload<ChooseRelicPayload>().Index ==
                    (index == pending.FirstPendingExtraRelicGroupIndex ? 0 : index));
            var result = Engine.Step(state, action).State;
            Assert.Equal(CanonicalJson.Sha256(result), CanonicalJson.Sha256(
                Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
            Assert.True(result.Player.Relics.Last().IsWax);
            Assert.Equal(reward.ExtraRelicRewardIds[index], result.Player.Relics.Last().RelicId);
            state = result;
            PrototypeStateInvariants.Validate(state);
        }
        Assert.Equal(order.Select(index => reward.ExtraRelicRewardIds[index]),
            state.Player.Relics.Where(relic => relic.IsWax).Select(relic => relic.RelicId));
        state = Engine.Step(state, GameAction.Empty("leave_reward")).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Empty(state.World!.CompletedRooms);
    }

    [Fact]
    public void MeltingFollowsInventoryOrderEveryThirdCombatAndStopsAtFifteen()
    {
        string[] ids = ["proto.relic.lantern", "proto.relic.vajra", "proto.relic.anchor",
            "proto.relic.blood_vial", "proto.relic.oddly_smooth_stone"];
        var state = Lifecycle(ids);
        for (var count = 1; count <= 16; count++)
        {
            var combat = Combat(state);
            var openingEnergy = combat.World!.Combat!.Energy;
            Assert.Equal(count <= 3 ? 4 : 3, openingEnergy);
            PrototypeStateInvariants.Validate(combat);
            var parentHash = CanonicalJson.Sha256(combat);
            state = Win(combat);
            Assert.Equal(parentHash, CanonicalJson.Sha256(combat));
            Assert.Equal(Math.Min(15, count), NativeAncientRewardsV111.ToyBoxCombatsSeen(
                state.Player.Relics.Single(relic => relic.RelicId == NativeAncientRewardsV111.ToyBoxId)));
            var wax = state.Player.Relics.Where(relic => relic.IsWax).ToArray();
            Assert.Equal(5, wax.Length);
            Assert.Equal(Math.Min(5, count / 3), wax.Count(relic => relic.IsMelted));
            Assert.Equal(ids.Take(Math.Min(5, count / 3)), wax.Where(relic => relic.IsMelted).Select(relic => relic.RelicId));
            PrototypeStateInvariants.Validate(state);
        }
    }

    [Fact]
    public void ThirdCombatMeltPrecedesPotionRewardGeneration()
    {
        var state = Lifecycle("proto.relic.white_beast_statue");
        var configuration = RunConfiguration.Create() with
        { FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId };
        var configured = Engine.Step(V111RunFactory.Create("toy-box-native-reward", configuration),
            GameAction.Empty("start_run")).State;
        while (configured.Phase is RunPhase.Event or RunPhase.Reward)
            configured = Engine.Step(configured, Engine.GetLegalActions(configured).First()).State;
        configured = Engine.Step(configured with { Phase = RunPhase.ActTransition },
            GameAction.Empty("continue_act")).State;
        state = configured with { Player = state.Player with { PotionSlots = configured.Player.PotionSlots } };
        state = state with
        {
            Player = state.Player with { Relics = state.Player.Relics.Select(relic =>
                relic.RelicId == NativeAncientRewardsV111.ToyBoxId
                    ? relic with { PersistentState = JsonSerializer.SerializeToElement(new { CombatsSeen = 2 }) }
                    : relic).ToArray() },
            World = PrototypeNativeRelicGrabBag.PruneOwned(state.World!, state.Player) with
                { PotionRewardOddsThousandths = 0 }
        };
        var result = Win(Combat(state));
        Assert.True(result.Player.Relics.Single(relic => relic.RelicId == "proto.relic.white_beast_statue").IsMelted);
        Assert.Null(result.World!.Reward!.PotionOption);
        Assert.True(result.World.PotionRewardOddsThousandths > 0);
    }

    [Fact]
    public void MeltedPassiveAndAcquisitionModifiersAreInactiveButOwnershipRemains()
    {
        var state = Lifecycle();
        var melted = new RelicInstance("proto.relic.membership_card", PrototypeJson.EmptyObject(), true, true);
        state = state with { Player = state.Player with { Relics = state.Player.Relics.Append(melted).ToArray() } };
        var price = typeof(PrototypeGameEngine).GetMethod("ApplyShopPriceModifiers", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Equal(100, price.Invoke(null, [state.Player, 100]));
        Assert.Contains(melted, state.Player.Relics);
        Assert.DoesNotContain(melted, state.Player.ActiveRelics);
        var observation = new PrototypeAiEnvironment().Observe(state).Observation;
        var visible = observation.Relics.Single(relic => relic.RelicId == melted.RelicId);
        Assert.True(visible.IsWax);
        Assert.True(visible.IsMelted);
    }

    [Fact]
    public void DefeatCountsOnceAndMeltsTheNextWaxRelic()
    {
        var state = Lifecycle("proto.relic.lantern");
        state = state with { Player = state.Player with { Relics = state.Player.Relics.Select(relic =>
            relic.RelicId == NativeAncientRewardsV111.ToyBoxId
                ? relic with { PersistentState = JsonSerializer.SerializeToElement(new { CombatsSeen = 2 }) }
                : relic).ToArray() } };
        state = Combat(state);
        state = state with { Player = state.Player with { Hp = 1 } };
        for (var i = 0; i < 5 && state.Phase == RunPhase.Combat; i++)
            state = Engine.Step(state, Engine.GetLegalActions(state).Single(action => action.Kind == "end_turn")).State;
        Assert.Equal(RunPhase.Terminal, state.Phase);
        Assert.Equal(3, NativeAncientRewardsV111.ToyBoxCombatsSeen(state.Player.Relics
            .Single(relic => relic.RelicId == NativeAncientRewardsV111.ToyBoxId)));
        Assert.True(state.Player.Relics.Single(relic => relic.IsWax).IsMelted);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void SkippedWaxRewardsStillAdvanceToyBoxWithoutInventingRelics()
    {
        var state = Lifecycle();
        for (var i = 1; i <= 3; i++)
            state = Win(Combat(state));
        Assert.DoesNotContain(state.Player.Relics, relic => relic.IsWax);
        Assert.Equal(3, NativeAncientRewardsV111.ToyBoxCombatsSeen(state.Player.Relics
            .Single(relic => relic.RelicId == NativeAncientRewardsV111.ToyBoxId)));
    }

    [Fact]
    public void WaxAndCounterCorruptionCannotLoad()
    {
        var state = Offers();
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(state with
        { World = state.World! with { Reward = state.World.Reward! with { ExtraRelicWaxFlags = [true] } } }));
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(state with
        { Player = state.Player with { Relics = state.Player.Relics.Append(new RelicInstance(
            "proto.relic.vajra", PrototypeJson.EmptyObject(), IsMelted: true)).ToArray() } }));
        Assert.Throws<InvalidOperationException>(() => NativeAncientRewardsV111.ToyBoxCombatsSeen(
            new RelicInstance(NativeAncientRewardsV111.ToyBoxId,
                JsonSerializer.SerializeToElement(new { CombatsSeen = 16 }))));
    }
}
