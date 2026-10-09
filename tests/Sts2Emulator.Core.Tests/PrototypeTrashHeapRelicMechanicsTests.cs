using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeTrashHeapRelicMechanicsTests
{
    private const string MawBank = "proto.native.trash_heap.maw_bank";
    private const string DreamCatcher = "proto.native.trash_heap.dream_catcher";
    private const string TheBoot = "proto.native.trash_heap.the_boot";

    [Fact]
    public void MawBankGainsTwelveOnRoomEntryAndExpiresOnPositiveShopPurchase()
    {
        var engine = new PrototypeGameEngine();
        var root = BuildMapRun("maw-bank-room",
            [PrototypeRoomType.Shop, PrototypeRoomType.Rest,
             PrototypeRoomType.Rest], relics: [Relic(MawBank)]);
        root = root with
        {
            Player = root.Player with { Gold = 500 }
        };

        var entered = EnterNext(engine, root);
        Assert.Equal(RunPhase.Shop, entered.Phase);
        Assert.Equal(512, entered.Player.Gold);
        Assert.Equal(12, PrototypeContent.Relic(MawBank)
            .GoldOnRoomEntryUntilPurchase);

        var removal = engine.GetLegalActions(entered)
            .First(item => item.Kind == "remove_card");
        var purchased = engine.Step(entered, removal).State;
        Assert.Equal(512 - entered.World!.Shop!.RemovalPrice,
            purchased.Player.Gold);
        Assert.True(purchased.Player.Relics[0].PersistentState
            .GetProperty("Purchased").GetBoolean());
        Assert.Equal(CanonicalJson.Sha256(purchased),
            CanonicalJson.Sha256(purchased.Fork()));
        var left = engine.Step(
            purchased, GameAction.Empty("leave_shop")).State;
        var next = EnterNext(engine, left);
        Assert.Equal(RunPhase.Rest, next.Phase);
        Assert.Equal(purchased.Player.Gold, next.Player.Gold);
    }

    [Fact]
    public void MawBankDoesNotDeactivateWhenShopIsLeftWithoutBuying()
    {
        var engine = new PrototypeGameEngine();
        var state = BuildMapRun("maw-bank-leave",
            [PrototypeRoomType.Shop, PrototypeRoomType.Rest,
             PrototypeRoomType.Rest], relics: [Relic(MawBank)]);
        state = EnterNext(engine, state);
        Assert.Equal(112, state.Player.Gold);
        state = engine.Step(state, GameAction.Empty("leave_shop")).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        state = EnterNext(engine, state);
        Assert.Equal(RunPhase.Rest, state.Phase);
        Assert.Equal(124, state.Player.Gold);
    }

    [Fact]
    public void DreamCatcherOffersOrdinaryThreeCardsAfterRestHealOnly()
    {
        var engine = new PrototypeGameEngine();
        var root = BuildMapRun("dream-catcher",
            [PrototypeRoomType.Rest, PrototypeRoomType.Rest,
             PrototypeRoomType.Rest], relics: [Relic(DreamCatcher)]);
        root = root with
        {
            Player = root.Player with { Hp = root.Player.MaxHp - 20 }
        };
        var rest = EnterNext(engine, root);
        var heal = engine.Step(rest, GameAction.Empty("rest_heal")).State;
        Assert.Equal(RunPhase.Reward, heal.Phase);
        Assert.Equal("Rest", heal.World!.Reward!.SourceRoom);
        Assert.Equal(3, heal.World.Reward.CardOptions.Length);
        Assert.True(heal.World.Reward.PotionResolved);
        Assert.True(heal.World.Reward.RelicResolved);
        Assert.True(heal.Player.Hp > rest.Player.Hp);

        var options = heal.World.Reward.CardOptions;
        Assert.All(options, id =>
            Assert.Contains(id, PrototypeContent.RewardCardPool));
        var choice = engine.GetLegalActions(heal).First(item =>
            item.Kind == "take_reward_card");
        var chosen = engine.Step(heal, choice).State;
        Assert.Contains(options[0], chosen.Player.Deck.Select(card => card.CardId));
        var leave = engine.GetLegalActions(chosen).Single(item =>
            item.Kind == "leave_reward");
        var done = engine.Step(chosen, leave).State;
        Assert.Equal(RunPhase.MapChoice, done.Phase);

        var nextRest = EnterNext(engine, done);
        var train = engine.Step(
            nextRest, GameAction.Empty("rest_train")).State;
        Assert.Equal(RunPhase.MapChoice, train.Phase);
        Assert.Null(train.World!.Reward);
    }

    [Theory]
    [InlineData(0, 94)]  // 6 HP damage remains 6
    [InlineData(3, 95)]  // 3 HP loss is raised to 5
    [InlineData(6, 100)] // entire attack blocked
    [InlineData(5, 95)]  // 1 HP loss raised to 5
    public void BootRaisesOnlyPositivePoweredAttackHpLoss(
        int initialBlock, int expectedHp)
    {
        var engine = new PrototypeGameEngine();
        var card = new CombatCardInstance(
            1, 1, "proto.silent.strike", 0, false,
            PrototypeJson.EmptyObject());
        var state = BuildCombatRun(
            "boot-" + initialBlock,
            card,
            initialBlock,
            [Relic(TheBoot)]);
        var action = engine.GetLegalActions(state).First(a =>
            a.Kind == "play_card");
        state = engine.Step(state, action).State;
        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(expectedHp, enemy.Hp);
        Assert.Equal(Math.Max(0, initialBlock - 6), enemy.Block);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void BootDoesNotTriggerOnNonAttackDamageOrEnemyDamageCaps()
    {
        Assert.Equal(5, PrototypeContent.Relic(TheBoot)
            .MinPoweredAttackHpLoss);
        Assert.Equal(3, PrototypeContent.Relic(DreamCatcher)
            .RestHealCardRewardCount);
        Assert.Equal(0, PrototypeContent.Relic(
            "proto.native.trash_heap.hand_drill")
            .MinPoweredAttackHpLoss);
        Assert.DoesNotContain(MawBank, PrototypeContent.RelicPool);
        Assert.DoesNotContain(DreamCatcher, PrototypeContent.RelicPool);
        Assert.DoesNotContain(TheBoot, PrototypeContent.RelicPool);
    }

    private static RelicInstance Relic(string id) =>
        new(id, PrototypeJson.EmptyObject());

    private static RunState BuildMapRun(
        string seed, PrototypeRoomType[] rooms,
        RelicInstance[] relics)
    {
        var initial = PrototypeNativeUnderdocksRunFactory.Create(seed);
        var nodes = rooms.Select((room, index) =>
            new MapNodeState("relic-room-" + index, 1,
                index + 1, room,
                index < rooms.Length - 1
                    ? ["relic-room-" + (index + 1)]
                    : [])).ToArray();
        return initial with
        {
            Phase = RunPhase.MapChoice,
            Player = initial.Player with
            {
                Relics = relics, Gold = 100
            },
            World = initial.World! with
            {
                Floor = 0,
                ActiveRoom = null,
                Map = new MapState(nodes, null, [nodes[0].NodeId],
                    PrototypeNativeUnderdocks.GenerationProfileId),
                Combat = null, Reward = null, Shop = null, Event = null,
                CompletedRoomHistory = []
            }
        };
    }

    private static RunState BuildCombatRun(
        string seed, CombatCardInstance card, int block,
        RelicInstance[] relics)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = new[] { new CardInstance(
            1, card.CardId, 0, empty) };
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: [1], DrawPile: [], DiscardPile: [], ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(1,
                    "proto.enemy.crawler", 100, block, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards: [card], PlayerPowers: [],
            NextPowerApplicationOrder: 2,
            Relics: relics.Select((relic, index) =>
                new CombatRelicState(
                    index, relic.RelicId, index + 1,
                    new int[(PrototypeContent.Relic(relic.RelicId)
                        .Triggers ?? []).Length]))
                .ToArray());
        return new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(70, 70, 100, deck, relics,
                new PotionInstance?[PrototypeContent.Rules.PotionSlots]),
            PrototypeRng.CreateBundle(seed), empty,
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 1, 1, 2,
                PrototypeRoomType.Combat, new MapState([]),
                combat, null, null, null, null));
    }

    private static RunState EnterNext(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, Assert.Single(
            engine.GetLegalActions(state))).State;
}
