using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeNeowItemsAndCombatCompletionTests
{
    private static string OptionId(string name) =>
        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(name);

    private static RunState Pick(string name, string? seed = null)
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create(
            seed ?? "native-neow-extra-" + name);
        var world = state.World!;
        state = state with
        {
            World = world with
            {
                Event = world.Event! with
                {
                    OfferedChoiceIds =
                    [
                        OptionId(name),
                        OptionId(name == "GoldenPearl"
                            ? "ArcaneScroll" : "GoldenPearl"),
                        OptionId(name == "CursedPearl"
                            ? "DowsingRod" : "CursedPearl")
                    ]
                }
            }
        };
        var engine = new PrototypeGameEngine();
        var take = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId
                == OptionId(name));
        state = engine.Step(state, take).State;
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    [Fact]
    public void PhialHolsterGainsOneSlotAndTwoRandomPotions()
    {
        var state = Pick("PhialHolster");
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(3, state.Player.PotionSlots.Length);
        Assert.Equal(2, state.Player.PotionSlots.Count(p => p is not null));
        Assert.All(state.Player.PotionSlots.Where(p => p is not null),
            p => Assert.Contains(p!.PotionId, PrototypeContent.PotionPool));
        Assert.Contains(state.Player.Relics,
            relic => relic.RelicId == PrototypeNativeOvergrowthEvents.NeowRelicId(
                "PhialHolster"));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void LargeCapsuleAddsTwoUnownedRelicsAndStarterCards()
    {
        var state = Pick("LargeCapsule");
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        // Silent already owns its Ring of the Snake starter relic.
        Assert.Equal(4, state.Player.Relics.Length);
        Assert.Equal(4, state.Player.Relics
            .Select(relic => relic.RelicId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(15, state.Player.Deck.Length);
        Assert.Equal("proto.silent.strike", state.Player.Deck[^2].CardId);
        Assert.Equal("proto.silent.defend", state.Player.Deck[^1].CardId);
        Assert.Equal(14, state.Player.Deck[^2].InstanceId);
        Assert.Equal(15, state.Player.Deck[^1].InstanceId);
        Assert.Equal(16, state.World!.NextCardInstanceId);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void NeowsSacrificeGrantsAmbergrisAndGuilty()
    {
        var state = Pick("NeowsSacrifice");
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(14, state.Player.Deck.Length);
        Assert.Equal("proto.native.event.guilty",
            state.Player.Deck[^1].CardId);
        Assert.Equal("proto.native.neow.ambergris",
            state.Player.PotionSlots[0]?.PotionId);
        Assert.Equal(14, state.Player.Deck[^1].InstanceId);
        Assert.DoesNotContain("proto.native.neow.ambergris",
            PrototypeContent.PotionPool);
        Assert.Contains("proto.native.neow.ambergris",
            PrototypeContent.Potions.Keys);
        PrototypeStateInvariants.Validate(state);

        var engine = new PrototypeGameEngine();
        state = state with
        {
            Player = state.Player with { Hp = 20 }
        };
        var drink = engine.GetLegalActions(state).Single(action =>
            action.Kind == "use_potion"
            && action.ReadPayload<UsePotionPayload>().Slot == 0);
        state = engine.Step(state, drink).State;
        Assert.Equal(55, state.Player.Hp);
        Assert.Null(state.Player.PotionSlots[0]);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void GuiltyExpiresAfterFifthVictoryButNotEarlier()
    {
        var state = Pick("NeowsSacrifice", "native-guilty-victory");
        state = state with
        {
            Player = state.Player with
            {
                Deck = state.Player.Deck.Select(card =>
                    card.CardId == "proto.native.event.guilty"
                        ? card with
                        {
                            PersistentState = JsonSerializer.SerializeToElement(
                                new { CombatsSeen = 3 })
                        }
                        : card).ToArray()
            }
        };
        state = EnterFirstCombat(state);
        state = DefeatCombat(state);
        var guilty = Assert.Single(state.Player.Deck, card =>
            card.CardId == "proto.native.event.guilty");
        Assert.Equal(4,
            guilty.PersistentState.GetProperty("CombatsSeen").GetInt32());
        PrototypeStateInvariants.Validate(state);

        // Force a fifth encounter as an independent continuation state.
        // It still enters and wins through the public legal-action path.
        var nextRun = Pick("NeowsSacrifice", "native-guilty-fifth");
        nextRun = nextRun with
        {
            Player = nextRun.Player with
            {
                Deck = nextRun.Player.Deck.Select(card =>
                    card.CardId == "proto.native.event.guilty"
                        ? card with
                        {
                            PersistentState = JsonSerializer.SerializeToElement(
                                new { CombatsSeen = 4 })
                        }
                        : card).ToArray()
            }
        };
        nextRun = DefeatCombat(EnterFirstCombat(nextRun));
        Assert.DoesNotContain(nextRun.Player.Deck,
            card => card.CardId == "proto.native.event.guilty");
        Assert.Equal(13, nextRun.Player.Deck.Length);
        Assert.Equal(15, nextRun.World!.NextCardInstanceId);
        PrototypeStateInvariants.Validate(nextRun);
    }

    [Fact]
    public void FishingRodUpgradesOneCardAtItsThirdOrdinaryVictory()
    {
        var state = Pick("FishingRod");
        var rodId = PrototypeNativeOvergrowthEvents.NeowRelicId("FishingRod");
        state = state with
        {
            Player = state.Player with
            {
                Relics = state.Player.Relics.Select(relic =>
                    relic.RelicId == rodId
                        ? relic with
                        {
                            PersistentState = JsonSerializer.SerializeToElement(
                                new { CombatsSeen = 2 })
                        }
                        : relic).ToArray()
            }
        };
        Assert.All(state.Player.Deck,
            card => Assert.Equal(0, card.UpgradeLevel));

        state = DefeatCombat(EnterFirstCombat(state));
        var rod = Assert.Single(state.Player.Relics,
            relic => relic.RelicId == rodId);
        Assert.Equal(3,
            rod.PersistentState.GetProperty("CombatsSeen").GetInt32());
        Assert.Single(state.Player.Deck,
            card => card.UpgradeLevel == 1);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void BoomingConchOnlyBoostsTheOpeningEliteTurn()
    {
        var ordinary = EnterFirstCombat(Pick(
            "BoomingConch", "native-conch-normal"));
        Assert.Equal(3, ordinary.World!.Combat!.Energy);
        Assert.Equal(7, ordinary.World.Combat.Hand.Length);
        PrototypeStateInvariants.Validate(ordinary);

        var elite = EnterEliteCombat(Pick(
            "BoomingConch", "native-conch-elite"));
        Assert.Equal(PrototypeRoomType.Elite, elite.World!.ActiveRoom);
        Assert.Equal(4, elite.World.Combat!.Energy);
        Assert.Equal(9, elite.World.Combat.Hand.Length);
        PrototypeStateInvariants.Validate(elite);
    }

    [Fact]
    public void FishingRodDoesNotCountEliteVictory()
    {
        var state = Pick("FishingRod", "native-fishing-elite");
        var rodId = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "FishingRod");
        state = state with
        {
            Player = state.Player with
            {
                Relics = state.Player.Relics.Select(relic =>
                    relic.RelicId == rodId
                        ? relic with
                        {
                            PersistentState = JsonSerializer.SerializeToElement(
                                new { CombatsSeen = 2 })
                        }
                        : relic).ToArray()
            }
        };
        state = DefeatCombat(EnterEliteCombat(state));
        var rod = Assert.Single(state.Player.Relics,
            relic => relic.RelicId == rodId);
        Assert.Equal(2,
            rod.PersistentState.GetProperty("CombatsSeen").GetInt32());
        Assert.DoesNotContain(state.Player.Deck,
            card => card.UpgradeLevel > 0);
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState EnterEliteCombat(RunState state)
    {
        var engine = new PrototypeGameEngine();
        var world = state.World!;
        var map = world.Map;
        var elite = map.Nodes.First(node =>
            node.RoomType == PrototypeRoomType.Elite
            && node.Floor >= 3);
        var predecessor = map.Nodes.First(node =>
            node.NextNodeIds?.Contains(elite.NodeId,
                StringComparer.Ordinal) == true);
        var history = Enumerable.Range(1, predecessor.Floor)
            .Select(floor =>
            {
                var node = floor == predecessor.Floor
                    ? predecessor
                    : map.Nodes.First(item => item.Floor == floor);
                return new PrototypeCompletedRoomRecord(
                    1, floor, node.NodeId, node.RoomType);
            }).ToArray();

        state = state with
        {
            World = world with
            {
                Floor = predecessor.Floor,
                Map = map with { CurrentNodeId = predecessor.NodeId },
                Event = null,
                CompletedRoomHistory = history
            }
        };
        PrototypeStateInvariants.Validate(state);
        var action = engine.GetLegalActions(state).Single(candidate =>
            candidate.Kind == "choose_map_node"
            && candidate.ReadPayload<ChooseMapNodePayload>()
                .NodeId == elite.NodeId);
        state = engine.Step(state, action).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        return state;
    }

    private static RunState EnterFirstCombat(RunState state)
    {
        var engine = new PrototypeGameEngine();
        var world = state.World!;
        var node = world.Map.AvailableNodes().First(item =>
            item.RoomType == PrototypeRoomType.Combat);
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "choose_map_node"
            && item.ReadPayload<ChooseMapNodePayload>().NodeId == node.NodeId);
        state = engine.Step(state, action).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        return state;
    }

    private static RunState DefeatCombat(RunState state)
    {
        var engine = new PrototypeGameEngine();
        var world = state.World!;
        var combat = world.Combat!;
        var attacker = combat.Cards.First(card =>
            card.CardId == "proto.silent.neutralize");
        var rest = combat.Cards.Select(card => card.InstanceId)
            .Where(id => id != attacker.InstanceId).ToArray();
        combat = combat with
        {
            Hand = [attacker.InstanceId],
            DrawPile = rest,
            DiscardPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3,
            Enemies = combat.Enemies.Select((enemy, index) =>
                enemy with
                {
                    Hp = index == 0 ? 1 : 0,
                    Block = 0
                }).ToArray()
        };
        state = state with
        {
            World = world with { Combat = combat }
        };
        var attack = engine.GetLegalActions(state).First(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == attacker.InstanceId);
        state = engine.Step(state, attack).State;
        Assert.Equal(RunPhase.Reward, state.Phase);
        return state;
    }

    [Fact]
    public void SwordOfStoneTracksEliteVictoriesAndTransformsIntoJade()
    {
        const string stoneId = "proto.native.event.sword_of_stone";
        const string jadeId = "proto.native.event.sword_of_jade";

        RunState WithStone(string seed, int defeated)
        {
            var state = Pick("GoldenPearl", seed);
            return state with
            {
                Player = state.Player with
                {
                    Relics =
                    [
                        .. state.Player.Relics,
                        new RelicInstance(stoneId,
                            JsonSerializer.SerializeToElement(
                                new { ElitesDefeated = defeated }))
                    ]
                }
            };
        }

        var fourth = DefeatCombat(EnterEliteCombat(
            WithStone("stone-fourth-elite", 3)));
        var stone = Assert.Single(fourth.Player.Relics,
            relic => relic.RelicId == stoneId);
        Assert.Equal(4, stone.PersistentState
            .GetProperty("ElitesDefeated").GetInt32());
        Assert.DoesNotContain(fourth.Player.Relics,
            relic => relic.RelicId == jadeId);
        PrototypeStateInvariants.Validate(fourth);

        var fifth = DefeatCombat(EnterEliteCombat(
            WithStone("stone-fifth-elite", 4)));
        Assert.DoesNotContain(fifth.Player.Relics,
            relic => relic.RelicId == stoneId);
        Assert.Single(fifth.Player.Relics,
            relic => relic.RelicId == jadeId);
        PrototypeStateInvariants.Validate(fifth);

        var withJade = Pick("GoldenPearl", "jade-next-combat");
        withJade = withJade with
        {
            Player = withJade.Player with
            {
                Relics =
                [
                    .. withJade.Player.Relics,
                    new RelicInstance(jadeId, PrototypeJson.EmptyObject())
                ]
            }
        };
        var nextCombat = EnterFirstCombat(withJade);
        var strength = Assert.Single(
            nextCombat.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.strength");
        Assert.Equal(3, strength.Stacks);
        PrototypeStateInvariants.Validate(nextCombat);
    }

    [Fact]
    public void SwordOfStoneIgnoresOrdinaryVictories()
    {
        const string stoneId = "proto.native.event.sword_of_stone";
        var state = Pick("GoldenPearl", "stone-ordinary-victory");
        state = state with
        {
            Player = state.Player with
            {
                Relics =
                [
                    .. state.Player.Relics,
                    new RelicInstance(stoneId,
                        JsonSerializer.SerializeToElement(
                            new { ElitesDefeated = 4 }))
                ]
            }
        };
        state = DefeatCombat(EnterFirstCombat(state));
        var stone = Assert.Single(state.Player.Relics,
            relic => relic.RelicId == stoneId);
        Assert.Equal(4, stone.PersistentState
            .GetProperty("ElitesDefeated").GetInt32());
        Assert.DoesNotContain(state.Player.Relics,
            relic => relic.RelicId == "proto.native.event.sword_of_jade");
        PrototypeStateInvariants.Validate(state);
    }
}
