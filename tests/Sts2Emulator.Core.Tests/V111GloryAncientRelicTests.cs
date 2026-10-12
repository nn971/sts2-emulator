using Sts2Emulator.Core;
using System.Reflection;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryAncientRelicTests
{
    [Fact]
    public void BeautifulBraceletEnchantsFourEligibleCardsUsingRunNiche()
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = new[]
        {
            new CardInstance(1, "proto.silent.strike", 0, empty),
            new CardInstance(2, "proto.silent.defend", 0, empty),
            new CardInstance(3, "proto.silent.neutralize", 0, empty),
            new CardInstance(4, "proto.silent.survivor", 0, empty),
            new CardInstance(5, "proto.silent.footwork", 0, empty),
            new CardInstance(6, "proto.status.dazed", 0, empty),
            new CardInstance(7, "proto.silent.strike", 0, empty,
                new(PrototypeCardEnchantmentKind.Glam))
        };
        var player = new PlayerState(60, 60, 0, deck, [], [null, null]);
        Assert.True(NativeGloryAncientsV111.IsBeautifulBraceletEligible(player));

        var rng = NativeRngV111.CreateBundle("beautiful-bracelet-acquisition");
        var expectedRng = rng.Fork();
        var expected = deck.Take(5).ToArray();
        NativeRngV111.Shuffle(expectedRng, "run.niche", expected);
        var expectedIds = expected.Take(4).Select(card => card.InstanceId)
            .ToHashSet();
        var acquired = NativeGloryAncientsV111
            .ApplyBeautifulBraceletAcquisition(player, rng, rng);

        Assert.All(acquired.Deck.Where(card => expectedIds.Contains(card.InstanceId)),
            card => Assert.Equal(new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.Swift, 2), card.Enchantment));
        Assert.Equal(4, acquired.Deck.Count(card =>
            card.Enchantment?.Kind == PrototypeCardEnchantmentKind.Swift));
        Assert.Equal(deck.Single(card => card.InstanceId == 6),
            acquired.Deck.Single(card => card.InstanceId == 6));
        Assert.Equal(deck.Single(card => card.InstanceId == 7),
            acquired.Deck.Single(card => card.InstanceId == 7));
        Assert.Equal(NativeRngV111.ToSnapshot(expectedRng.Streams.Single(
                stream => stream.StreamId == "run.niche")),
            NativeRngV111.ToSnapshot(rng.Streams.Single(
                stream => stream.StreamId == "run.niche")));
        Assert.False(NativeGloryAncientsV111.IsBeautifulBraceletEligible(
            player with { Deck = deck[..3] }));
    }

    [Fact]
    public void RewardAcquisitionPersistsBeautifulBraceletsNativeRunCursor()
    {
        var initial = PrototypeNativeUnderdocksRunFactory.Create(
            "beautiful-bracelet-reward-acquisition");
        var empty = PrototypeJson.EmptyObject();
        var state = initial with
        {
            Phase = RunPhase.Reward,
            Player = initial.Player with
            {
                Deck =
                [
                    new(1, "proto.silent.strike", 0, empty),
                    new(2, "proto.silent.defend", 0, empty),
                    new(3, "proto.silent.neutralize", 0, empty),
                    new(4, "proto.silent.survivor", 0, empty),
                    new(5, "proto.silent.footwork", 0, empty)
                ]
            },
            NativeRunRng = NativeRngV111.CreateBundle(
                "beautiful-bracelet-reward-acquisition"),
            World = initial.World! with
            {
                Floor = 1,
                Map = new MapState([]),
                ActiveRoom = PrototypeRoomType.Combat,
                Combat = null,
                Reward = new RewardState("Combat", [], null,
                    NativeGloryAncientsV111.BeautifulBraceletId,
                    CardResolved: true, PotionResolved: true,
                    RelicResolved: false, EndsAct: false)
            }
        };
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "take_reward_relic");

        state = engine.Step(state, action).State;

        Assert.Equal(4, state.Player.Deck.Count(card =>
            card.Enchantment is
                { Kind: PrototypeCardEnchantmentKind.Swift, Amount: 2 }));
        Assert.Equal(4, NativeRngV111.ToSnapshot(state.NativeRunRng!
            .Streams.Single(stream => stream.StreamId == "run.niche")).Counter);
        Assert.Null(state.World!.Reward!.PendingDeckChoice);
    }

    [Fact]
    public void GlitterEnchantsCombatRewardChoicesAndPersistsOnSelection()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("glory-glitter-reward");
        state = engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
        state = state with
        {
            Player = state.Player with
            {
                Relics = [..state.Player.Relics,
                    Relic(NativeGloryAncientsV111.GlitterId)]
            }
        };
        var combatNode = state.World!.Map.AvailableNodes().First(node =>
            node.RoomType == PrototypeRoomType.Combat);
        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(combatNode.NodeId))).State;
        var combat = state.World!.Combat!;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    ExtraCardRewardsEarned = 1,
                    Enemies = combat.Enemies.Select(enemy =>
                        enemy with { Hp = 0, Block = 0 }).ToArray()
                }
            }
        };
        var enterReward = typeof(PrototypeGameEngine).GetMethod(
            "EnterCombatReward", BindingFlags.NonPublic | BindingFlags.Static)!;
        state = (RunState)enterReward.Invoke(null, [state])!;

        var reward = state.World!.Reward!;
        Assert.Equal(reward.CardOptions.Length,
            reward.CardOptionEnchantments!.Length);
        Assert.Single(reward.ExtraCardOptions!);
        Assert.Equal(reward.ExtraCardOptions![0].Length,
            reward.ExtraCardOptionEnchantments![0].Length);
        Assert.All(reward.CardOptions.Select((cardId, index) =>
                (cardId, enchantment: reward.CardOptionEnchantments[index])),
            option => Assert.Equal(
                PrototypeGameEngine.CanSelectEventDeckCard(
                    new CardInstance(0, option.cardId, 0,
                        PrototypeJson.EmptyObject()),
                    PrototypePersistentDeckChoiceKind.Enchant, null,
                    PrototypeCardEnchantmentKind.Glam, false),
                option.enchantment is
                    { Kind: PrototypeCardEnchantmentKind.Glam }));

        var restored = RunSnapshot.Load(RunSnapshot.Save(state));
        var take = engine.GetLegalActions(restored).First(action =>
            action.Kind == "take_reward_card");
        var selectedId = restored.World!.Reward!.CardOptions[
            take.ReadPayload<ChooseCardPayload>().Index];
        var selectedEnchantment = restored.World.Reward
            .CurrentCardOptionEnchantments[
                take.ReadPayload<ChooseCardPayload>().Index];
        restored = engine.Step(restored, take).State;
        var extraTake = engine.GetLegalActions(restored).First(action =>
            action.Kind == "take_reward_card");
        var extraIndex = extraTake.ReadPayload<ChooseCardPayload>().Index;
        var extraSelectedId = restored.World!.Reward!.ExtraCardOptions![0][
            extraIndex];
        var extraEnchantment = restored.World.Reward
            .CurrentCardOptionEnchantments[extraIndex];
        restored = engine.Step(restored, extraTake).State;

        var acquired = Assert.Single(restored.Player.Deck,
            card => card.CardId == selectedId);
        Assert.Equal(selectedEnchantment, acquired.Enchantment);
        var extraAcquired = Assert.Single(restored.Player.Deck,
            card => card.CardId == extraSelectedId);
        Assert.Equal(extraEnchantment, extraAcquired.Enchantment);
    }

    [Fact]
    public void BlessedAntlerAddsEnergyAndThreeDazedBeforeOpeningDraw()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("glory-blessed-antler");
        state = engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Deck =
                [
                    new(1, "proto.silent.strike", 0, empty),
                    new(2, "proto.silent.defend", 0, empty),
                    new(3, "proto.silent.strike", 0, empty),
                    new(4, "proto.silent.defend", 0, empty),
                    new(5, "proto.silent.strike", 0, empty)
                ],
                Relics = [..state.Player.Relics,
                    Relic(NativeGloryAncientsV111.BlessedAntlerId)]
            },
            World = state.World! with
            {
                NextCardInstanceId = 6,
                Map = new MapState(
                    [new MapNodeState("antler-combat", 1, 1,
                        PrototypeRoomType.Combat, [])],
                    EntryNodeIds: ["antler-combat"])
            }
        };

        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload("antler-combat"))).State;

        var combat = state.World!.Combat!;
        Assert.Equal(4, combat.Energy);
        var dazed = combat.Cards.Where(card =>
            card.CardId == "proto.status.dazed").ToArray();
        Assert.Equal(3, dazed.Length);
        Assert.All(dazed, card => Assert.True(card.IsTemporary));
        var zones = combat.Hand.Concat(combat.DrawPile)
            .Concat(combat.DiscardPile).Concat(combat.ExhaustPile)
            .ToArray();
        Assert.All(dazed, card => Assert.Contains(card.InstanceId, zones));
        Assert.Equal(zones.Length, zones.Distinct().Count());
    }

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
    public void JewelryBoxAddsInnateApotheosisAndItUpgradesOtherCombatCards()
    {
        var initial = PrototypeNativeUnderdocksRunFactory.Create(
            "glory-jewelry-box-acquisition");
        var state = initial with
        {
            Phase = RunPhase.Reward,
            World = initial.World! with
            {
                Floor = 1,
                Map = new MapState(
                    [new MapNodeState("apotheosis-combat", 1, 1,
                        PrototypeRoomType.Combat, [])],
                    EntryNodeIds: ["apotheosis-combat"]),
                ActiveRoom = PrototypeRoomType.Combat,
                Combat = null,
                Shop = null,
                Event = null,
                Reward = new RewardState("Combat", [], null,
                    NativeGloryAncientsV111.JewelryBoxId,
                    CardResolved: true, PotionResolved: true,
                    RelicResolved: false, EndsAct: false)
            }
        };
        var engine = new PrototypeGameEngine();
        var acquire = engine.GetLegalActions(state).Single(action =>
            action.Kind == "take_reward_relic");
        state = engine.Step(state, acquire).State;

        var apotheosis = Assert.Single(state.Player.Deck,
            card => card.CardId
                == NativeGloryAncientsV111.ApotheosisCardId);
        var apotheosisDefinition = PrototypeContent.Card(apotheosis.CardId);
        Assert.True(apotheosisDefinition.Innate);
        Assert.True(apotheosisDefinition.ExhaustOnUse);
        Assert.Equal(2, apotheosisDefinition.Cost.AmountAt(0));
        Assert.Equal(1, apotheosisDefinition.Cost.AmountAt(1));
        state = engine.Step(state, engine.GetLegalActions(state).Single(
            action => action.Kind == "leave_reward")).State;

        state = state with
        {
            Player = state.Player with
            {
                Deck =
                [
                    apotheosis with { InstanceId = 1 },
                    new(2, "proto.silent.strike", 0,
                        PrototypeJson.EmptyObject()),
                    new(3, "proto.silent.footwork", 0,
                        PrototypeJson.EmptyObject()),
                    new(4, "proto.status.dazed", 0,
                        PrototypeJson.EmptyObject())
                ]
            },
            World = state.World! with
            {
                NextCardInstanceId = 5,
                Map = new MapState(
                    [new MapNodeState("apotheosis-combat", 1, 1,
                        PrototypeRoomType.Combat, [])],
                    EntryNodeIds: ["apotheosis-combat"])
            }
        };
        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload("apotheosis-combat"))).State;
        var combat = state.World!.Combat!;
        var combatApotheosis = Assert.Single(combat.Cards, card =>
            card.CardId == NativeGloryAncientsV111.ApotheosisCardId);
        combat = combat with
        {
            Hand = [combatApotheosis.InstanceId],
            DrawPile = combat.Cards
                .Where(card => card.InstanceId
                    != combatApotheosis.InstanceId)
                .Select(card => card.InstanceId)
                .ToArray(),
            DiscardPile = [],
            ExhaustPile = []
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };

        var play = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == combatApotheosis.InstanceId);
        state = engine.Step(state, play).State;
        combat = state.World!.Combat!;

        Assert.Equal(1, combat.Cards.Single(card => card.CardId
            == "proto.silent.strike").UpgradeLevel);
        Assert.Equal(1, combat.Cards.Single(card => card.CardId
            == "proto.silent.footwork").UpgradeLevel);
        Assert.Equal(0, combat.Cards.Single(card => card.CardId
            == "proto.status.dazed").UpgradeLevel);
        Assert.Equal(0, combat.Cards.Single(card => card.CardId
            == NativeGloryAncientsV111.ApotheosisCardId).UpgradeLevel);
        Assert.Contains(combat.ExhaustPile, id =>
            id == combatApotheosis.InstanceId);
    }

    [Fact]
    public void BrilliantScarfMakesTheFifthManualCardPlayFree()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("glory-brilliant-scarf");
        state = engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Deck =
                [
                    new(1, "proto.silent.strike", 0, empty),
                    new(2, "proto.silent.strike", 0, empty),
                    new(3, "proto.silent.strike", 0, empty),
                    new(4, "proto.silent.strike", 0, empty),
                    new(5, "proto.silent.strike", 0, empty)
                ],
                Relics = [..state.Player.Relics,
                    new RelicInstance(
                        NativeGloryAncientsV111.BrilliantScarfId, empty)]
            },
            World = state.World! with
            {
                NextCardInstanceId = 6,
                Map = new MapState(
                    [new MapNodeState("scarf-combat", 1, 1,
                        PrototypeRoomType.Combat, [])],
                    EntryNodeIds: ["scarf-combat"])
            }
        };
        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload("scarf-combat"))).State;
        var combat = state.World!.Combat!;
        var cardInstanceId = combat.Hand[0];
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Energy = 0,
                    Counters = combat.CounterState with
                    {
                        CardsPlayedThisTurn = 3,
                        ManualCardsPlayedThisTurn = 3
                    }
                }
            }
        };

        Assert.DoesNotContain(engine.GetLegalActions(state), action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == cardInstanceId);

        combat = state.World!.Combat!;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Counters = combat.CounterState with
                    {
                        CardsPlayedThisTurn = 4,
                        ManualCardsPlayedThisTurn = 4
                    }
                }
            }
        };
        var fifthPlay = engine.GetLegalActions(state).First(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == cardInstanceId);

        state = engine.Step(state, fifthPlay).State;

        Assert.Equal(0, state.World!.Combat!.Energy);
        Assert.Equal(5,
            state.World.Combat.CounterState.ManualCardsPlayedThisTurn);
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
