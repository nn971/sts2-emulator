using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryAncientRelicTests
{
    [Fact]
    public void SignetRingGrants888GoldOnlyForTheNewlyAcquiredInstance()
    {
        var initial = PrototypeNativeUnderdocksRunFactory.Create(
            "glory-signet-ring-acquisition");
        var state = initial with
        {
            Phase = RunPhase.Reward,
            Player = initial.Player with
            {
                Gold = 123,
                Relics = [Relic(NativeGloryAncientsV111.SignetRingId)]
            },
            World = initial.World! with
            {
                Floor = 1,
                Map = new MapState([]),
                ActiveRoom = PrototypeRoomType.Combat,
                Combat = null,
                Shop = null,
                Event = null,
                Reward = new RewardState("Combat", [], null,
                    NativeGloryAncientsV111.SignetRingId,
                    CardResolved: true, PotionResolved: true,
                    RelicResolved: false, EndsAct: false)
            }
        };
        var originalHash = CanonicalJson.Sha256(state);
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "take_reward_relic");

        var acquired = engine.Step(state, action).State;

        Assert.Equal(1011, acquired.Player.Gold);
        Assert.Equal(2, acquired.Player.Relics.Length);
        Assert.Equal(originalHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(acquired),
            CanonicalJson.Sha256(engine.Step(
                RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
    }

    [Fact]
    public void SignetRingIsRegisteredAsASeparateAncientRelic()
    {
        Assert.Equal("Signet Ring",
            PrototypeContent.Relic(NativeGloryAncientsV111.SignetRingId).Name);
    }

    [Fact]
    public void LoomingFruitAdds31CurrentAndMaximumHpWhenAcquired()
    {
        var initial = PrototypeNativeUnderdocksRunFactory.Create(
            "glory-looming-fruit-acquisition");
        var state = initial with
        {
            Phase = RunPhase.Reward,
            Player = initial.Player with
            {
                Hp = 40,
                MaxHp = 60
            },
            World = initial.World! with
            {
                Floor = 1,
                Map = new MapState([]),
                ActiveRoom = PrototypeRoomType.Combat,
                Combat = null,
                Shop = null,
                Event = null,
                Reward = new RewardState("Combat", [], null,
                    NativeGloryAncientsV111.LoomingFruitId,
                    CardResolved: true, PotionResolved: true,
                    RelicResolved: false, EndsAct: false)
            }
        };
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "take_reward_relic");

        var acquired = engine.Step(state, action).State;

        Assert.Equal(71, acquired.Player.Hp);
        Assert.Equal(91, acquired.Player.MaxHp);
        Assert.Contains(acquired.Player.Relics, relic =>
            relic.RelicId == NativeGloryAncientsV111.LoomingFruitId);
    }

    [Fact]
    public void DiamondDiademGrantsOpeningBlockAndCarriesItThroughOneTurn()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("glory-diamond-diadem");
        state = engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Relics = [..state.Player.Relics,
                    new RelicInstance(NativeGloryAncientsV111.DiamondDiademId,
                        empty)]
            },
            World = state.World! with
            {
                Map = new MapState(
                    [new MapNodeState("diadem-combat", 1, 1,
                        PrototypeRoomType.Combat, [])],
                    EntryNodeIds: ["diadem-combat"])
            }
        };

        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload("diadem-combat"))).State;
        Assert.Equal(20, state.World!.Combat!.PlayerBlock);
        Assert.Contains(state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.blur");

        state = engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;

        Assert.Equal(2, state.World!.Combat!.Turn);
        Assert.True(state.World.Combat.PlayerBlock > 0,
            "Blur should preserve leftover Diadem Block through the next turn start.");
        Assert.DoesNotContain(state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.blur");
    }

    [Fact]
    public void SaiGrants7BlockAtEachPlayerSideTurnStart()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("glory-sai");
        state = engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
        state = state with
        {
            Player = state.Player with
            {
                Relics = [..state.Player.Relics,
                    new RelicInstance(NativeGloryAncientsV111.SaiId,
                        PrototypeJson.EmptyObject())]
            },
            World = state.World! with
            {
                Map = new MapState(
                    [new MapNodeState("sai-combat", 1, 1,
                        PrototypeRoomType.Combat, [])],
                    EntryNodeIds: ["sai-combat"])
            }
        };

        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload("sai-combat"))).State;
        Assert.Equal(7, state.World!.Combat!.PlayerBlock);

        state = engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;

        Assert.Equal(2, state.World!.Combat!.Turn);
        Assert.Equal(7, state.World.Combat.PlayerBlock);
    }

    [Fact]
    public void SpikedGauntletsAddsEnergyAndRaisesOnlyPowerCardCosts()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("glory-spiked-gauntlets");
        state = engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Deck =
                [
                    new(1, "proto.silent.footwork", 0, empty),
                    new(2, "proto.silent.strike", 0, empty),
                    new(3, "proto.silent.strike", 0, empty),
                    new(4, "proto.silent.strike", 0, empty),
                    new(5, "proto.silent.strike", 0, empty)
                ],
                Relics = [..state.Player.Relics,
                    new RelicInstance(
                        NativeGloryAncientsV111.SpikedGauntletsId, empty)]
            },
            World = state.World! with
            {
                NextCardInstanceId = 6,
                Map = new MapState(
                    [new MapNodeState("gauntlets-combat", 1, 1,
                        PrototypeRoomType.Combat, [])],
                    EntryNodeIds: ["gauntlets-combat"])
            }
        };

        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload("gauntlets-combat"))).State;
        Assert.Equal(4, state.World!.Combat!.Energy);
        var footwork = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);

        state = engine.Step(state, footwork).State;

        Assert.Equal(2, state.World!.Combat!.Energy);
        var strike = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2);
        state = engine.Step(state, strike).State;
        Assert.Equal(1, state.World!.Combat!.Energy);
    }

    [Fact]
    public void CrossbowAddsAFreeCharacterAttackToTheOpeningHand()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("glory-crossbow");
        state = engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Deck =
                [
                    new(1, "proto.silent.footwork", 0, empty),
                    new(2, "proto.silent.strike", 0, empty),
                    new(3, "proto.silent.strike", 0, empty),
                    new(4, "proto.silent.strike", 0, empty),
                    new(5, "proto.silent.strike", 0, empty)
                ],
                Relics = [..state.Player.Relics,
                    new RelicInstance(NativeGloryAncientsV111.CrossbowId,
                        empty)]
            },
            World = state.World! with
            {
                NextCardInstanceId = 6,
                Map = new MapState(
                    [new MapNodeState("crossbow-combat", 1, 1,
                        PrototypeRoomType.Combat, [])],
                    EntryNodeIds: ["crossbow-combat"])
            }
        };

        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload("crossbow-combat"))).State;

        var combat = state.World!.Combat!;
        var generated = Assert.Single(combat.Cards, card =>
            card.InstanceId > 5);
        Assert.Equal(PrototypeCardType.Attack,
            PrototypeContent.Card(generated.CardId).Type);
        Assert.Contains(generated.InstanceId, combat.Hand);
        Assert.Equal(0, generated.TemporaryEnergyCost?.Cost);
        Assert.Equal(PrototypeTemporaryCardCostExpiry.EndOfTurn,
            generated.TemporaryEnergyCost?.Expiry);
    }

    private static RelicInstance Relic(string id) =>
        new(id, PrototypeJson.EmptyObject());
}
