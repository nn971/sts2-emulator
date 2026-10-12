using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111PaelRewardTests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState Acquire(string relicId, bool egg = false)
    {
        var state = NativePaelRewardsV111.CreateRewardFixture("pael-reward-" + relicId, relicId);
        if (egg)
            state = state with { Player = state.Player with { Relics =
                [new("proto.relic.toxic_egg", PrototypeJson.EmptyObject())] } };
        var action = Assert.Single(Engine.GetLegalActions(state));
        var acquired = Engine.Step(state, action).State;
        Assert.Equal(CanonicalJson.Sha256(acquired), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        Assert.Equal(RunPhase.MapChoice, acquired.Phase);
        Assert.Empty(acquired.World!.CompletedRooms);
        PrototypeStateInvariants.Validate(acquired);
        return acquired;
    }

    private static RunState Combat(RunState state)
    {
        state = state with
        {
            Player = state.Player with { Hp = 1000, MaxHp = 1000 },
            World = state.World! with
            {
                Map = new MapState([], GenerationProfileId: PrototypeNativeLaterActMap.HiveProfileId),
                Floor = 1, ActiveRoom = PrototypeRoomType.Combat, Event = null, Reward = null
            }
        };
        var method = typeof(PrototypeGameEngine).GetMethod("StartCombat", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (RunState)method.Invoke(null, [state, PrototypeRoomType.Combat,
            PrototypeContent.Encounter("proto.native.hive.encounter.tunneler_weak")])!;
    }

    private static RunState EndTurn(RunState state)
    {
        var action = Engine.GetLegalActions(state).Single(action => action.Kind == "end_turn");
        var next = Engine.Step(state, action).State;
        Assert.Equal(CanonicalJson.Sha256(next), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        PrototypeStateInvariants.Validate(next);
        return next;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HornAddsTwoSeparateCardsThroughSharedAcquisition(bool egg)
    {
        var fixture = NativePaelRewardsV111.CreateRewardFixture("pael-reward-" + NativePaelRewardsV111.HornId,
            NativePaelRewardsV111.HornId);
        var acquired = Acquire(NativePaelRewardsV111.HornId, egg);
        var cards = acquired.Player.Deck.Where(card => card.CardId == NativePaelRewardsV111.RelaxId).ToArray();
        Assert.Equal(2, cards.Length);
        Assert.Equal(new[] { fixture.World!.NextCardInstanceId, fixture.World.NextCardInstanceId + 1 },
            cards.Select(card => card.InstanceId));
        Assert.Equal(fixture.World.NextCardInstanceId + 2, acquired.World!.NextCardInstanceId);
        Assert.All(cards, card => Assert.Equal(egg ? 1 : 0, card.UpgradeLevel));
    }

    [Theory]
    [InlineData(0, 16, 2)]
    [InlineData(1, 18, 3)]
    public void RelaxExhaustsAndGrantsBlockThenOneTurnOfDrawAndEnergy(int upgrade, int block, int bonus)
    {
        var state = Combat(Acquire(NativePaelRewardsV111.HornId, egg: upgrade == 1));
        var combat = state.World!.Combat!;
        var card = combat.Cards.First(card => card.CardId == NativePaelRewardsV111.RelaxId);
        state = state with { World = state.World with { Combat = combat with
        {
            Hand = combat.Hand.Contains(card.InstanceId) ? combat.Hand : [..combat.Hand, card.InstanceId],
            DrawPile = combat.DrawPile.Where(id => id != card.InstanceId).ToArray()
        } } };
        var play = Engine.GetLegalActions(state).Single(action => action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == card.InstanceId);
        state = Engine.Step(state, play).State;
        Assert.Equal(block, state.World!.Combat!.PlayerBlock);
        Assert.Equal(0, state.World.Combat.Energy);
        Assert.Contains(card.InstanceId, state.World.Combat.ExhaustPile);
        state = EndTurn(state);
        Assert.Equal(3 + bonus, state.World!.Combat!.Energy);
        Assert.Equal(5 + bonus, state.World.Combat.Hand.Length);
        Assert.DoesNotContain(state.World.Combat.PlayerPowers, power =>
            power.PowerId is "proto.power.energy_next_turn" or "proto.power.draw_cards_next_turn");
        state = EndTurn(state);
        Assert.Equal(3, state.World!.Combat!.Energy);
        Assert.Equal(5, state.World.Combat.Hand.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FleshModifiesEnergyResetStartingAtTurnThreeAndResetsForNextCombat(bool iceCream)
    {
        var acquired = Acquire(NativePaelRewardsV111.FleshId);
        if (iceCream)
            acquired = acquired with { Player = acquired.Player with { Relics =
                [..acquired.Player.Relics, new("proto.relic.ice_cream", PrototypeJson.EmptyObject())] } };
        var state = Combat(acquired);
        Assert.Equal(3, state.World!.Combat!.Energy);
        state = EndTurn(state);
        Assert.Equal(iceCream ? 6 : 3, state.World!.Combat!.Energy);
        state = EndTurn(state);
        Assert.Equal(iceCream ? 10 : 4, state.World!.Combat!.Energy);
        state = EndTurn(state);
        Assert.Equal(iceCream ? 14 : 4, state.World!.Combat!.Energy);
        Assert.Equal(3, Combat(acquired).World!.Combat!.Energy);
    }

    [Fact]
    public void BloodAddsOneCardToOpeningAndEveryLaterHandDraw()
    {
        var state = Combat(Acquire(NativePaelRewardsV111.BloodId));
        // Silent's starting relic adds two more cards on turn one.
        Assert.Equal(8, state.World!.Combat!.Hand.Length);
        for (var turn = 2; turn <= 4; turn++)
        {
            state = EndTurn(state);
            Assert.Equal(turn, state.World!.Combat!.Turn);
            Assert.Equal(6, state.World.Combat.Hand.Length);
        }
    }

    [Fact]
    public void FiddleAddsTwoOpeningCardsAndBlocksExtraPlayerTurnDraws()
    {
        var state = Acquire(NativePaelRewardsV111.FleshId);
        var nextId = state.World!.NextCardInstanceId;
        state = state with
        {
            Player = state.Player with
            {
                Relics = [..state.Player.Relics,
                    new(NativeGloryAncientsV111.FiddleId,
                        PrototypeJson.EmptyObject())],
                Deck = [..state.Player.Deck,
                    new(nextId, "proto.silent.backflip", 0,
                        PrototypeJson.EmptyObject())]
            },
            World = state.World with { NextCardInstanceId = nextId + 1 }
        };

        state = Combat(state);
        var combat = state.World!.Combat!;
        Assert.Equal(9, combat.Hand.Length);

        var backflip = Assert.Single(combat.Cards,
            card => card.CardId == "proto.silent.backflip");
        var drawPileBefore = combat.Cards
            .Where(card => card.InstanceId != backflip.InstanceId)
            .Select(card => card.InstanceId).ToArray();
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Hand = [backflip.InstanceId],
                    DrawPile = combat.Cards
                        .Where(card => card.InstanceId != backflip.InstanceId)
                        .Select(card => card.InstanceId).ToArray(),
                    DiscardPile = [],
                    ExhaustPile = [],
                    Energy = 3
                }
            }
        };

        var play = Engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == backflip.InstanceId);
        state = Engine.Step(state, play).State;

        combat = state.World!.Combat!;
        Assert.Equal(drawPileBefore.Order().ToArray(),
            combat.DrawPile.Order().ToArray());
        Assert.Empty(combat.Hand);
        Assert.Contains(backflip.InstanceId, combat.DiscardPile);
    }

    [Fact]
    public void BloodSoakedRoseAddsEternalEnthralledAndGatesOtherManualPlays()
    {
        var initial = PrototypeNativeLaterActFixture.Create(
            "blood-soaked-rose-acquisition", 2);
        var state = initial with
        {
            Phase = RunPhase.Reward,
            World = initial.World! with
            {
                Floor = 1,
                Map = new MapState([], GenerationProfileId:
                    PrototypeNativeLaterActMap.HiveProfileId),
                ActiveRoom = PrototypeRoomType.Combat,
                Combat = null,
                Event = null,
                Reward = new RewardState("Combat", [], null,
                    NativeGloryAncientsV111.BloodSoakedRoseId,
                    CardResolved: true, PotionResolved: true,
                    RelicResolved: false, EndsAct: false)
            }
        };
        var acquireRose = Engine.GetLegalActions(state).Single(
            action => action.Kind == "take_reward_relic");
        var acquired = Engine.Step(state, acquireRose).State;
        var replayedAcquisition = Engine.Step(
            RunSnapshot.Load(RunSnapshot.Save(state)), acquireRose).State;
        Assert.Equal(CanonicalJson.Sha256(acquired),
            CanonicalJson.Sha256(replayedAcquisition));
        state = RunSnapshot.Load(RunSnapshot.Save(acquired));
        var enthralled = Assert.Single(state.Player.Deck,
            card => card.CardId == NativeGloryAncientsV111.EnthralledCardId);
        Assert.True(PrototypeCardRules.IsEternal(enthralled));

        state = Combat(state);
        var combat = state.World!.Combat!;
        Assert.Equal(4, combat.Energy);
        var curse = Assert.Single(combat.Cards,
            card => card.CardId == NativeGloryAncientsV111.EnthralledCardId);
        var strike = combat.Cards.First(card =>
            card.CardId == "proto.silent.strike");
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Hand = [curse.InstanceId, strike.InstanceId],
                    DrawPile = combat.Cards.Where(card =>
                            card.InstanceId != curse.InstanceId
                            && card.InstanceId != strike.InstanceId)
                        .Select(card => card.InstanceId).ToArray(),
                    DiscardPile = [],
                    ExhaustPile = [],
                    Energy = 4
                }
            }
        };

        GameAction Play(long cardId) => Engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId
                    == cardId);

        Assert.DoesNotContain(Engine.GetLegalActions(state), action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == strike.InstanceId);
        var playCurse = Play(curse.InstanceId);
        var playedCurse = Engine.Step(state, playCurse).State;
        Assert.Equal(CanonicalJson.Sha256(playedCurse),
            CanonicalJson.Sha256(Engine.Step(
                RunSnapshot.Load(RunSnapshot.Save(state)), playCurse).State));
        state = playedCurse;
        Assert.Contains(curse.InstanceId, state.World!.Combat!.DiscardPile);
        Assert.Contains(Engine.GetLegalActions(state), action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == strike.InstanceId);
    }

    [Fact]
    public void WhisperingEarringPaysForPlayableOpeningCardsInHandOrder()
    {
        var empty = PrototypeJson.EmptyObject();
        var state = PrototypeNativeLaterActFixture.Create(
            "whispering-earring-first-turn", 2);
        state = state with
        {
            Player = state.Player with
            {
                Deck = Enumerable.Range(1, 5)
                    .Select(id => new CardInstance(id,
                        "proto.silent.strike", 0, empty)).ToArray(),
                Relics = [..state.Player.Relics,
                    new(NativeGloryAncientsV111.WhisperingEarringId,
                        empty)]
            },
            World = state.World! with { NextCardInstanceId = 6 }
        };

        var baselineState = state.Fork() with
        {
            Player = state.Player with
            {
                Relics = state.Player.Relics.Where(relic =>
                    relic.RelicId
                        != NativeGloryAncientsV111.WhisperingEarringId)
                    .ToArray()
            }
        };
        var baseline = Combat(baselineState).World!.Combat!;

        state = Combat(state.Fork());

        var combat = state.World!.Combat!;
        Assert.Equal(1, combat.Turn);
        Assert.Equal(0, combat.Energy);
        Assert.Single(combat.Hand);
        Assert.Equal(4, combat.DiscardPile.Length);
        Assert.Equal(4, combat.CounterState.CardsPlayedThisTurn);
        Assert.Equal(0, combat.CounterState.ManualCardsPlayedThisTurn);
        Assert.Equal(24, baseline.Enemies.Sum(enemy => enemy.Hp)
            - combat.Enemies.Sum(enemy => enemy.Hp));

        state = EndTurn(state);
        combat = state.World!.Combat!;
        Assert.Equal(2, combat.Turn);
        Assert.Equal(4, combat.Energy);
        Assert.Equal(5, combat.Hand.Length);
        Assert.Equal(0, combat.CounterState.CardsPlayedThisTurn);
    }

    [Fact]
    public void WhisperingEarringHonorsEnthralledDuringItsCanPlayScan()
    {
        var empty = PrototypeJson.EmptyObject();
        var state = PrototypeNativeLaterActFixture.Create(
            "whispering-earring-enthralled", 2);
        state = state with
        {
            Player = state.Player with
            {
                Deck =
                [
                    new(1, NativeGloryAncientsV111.EnthralledCardId, 0,
                        empty),
                    .. Enumerable.Range(2, 4).Select(id => new CardInstance(
                        id, "proto.silent.strike", 0, empty))
                ],
                Relics = [..state.Player.Relics,
                    new(NativeGloryAncientsV111.WhisperingEarringId,
                        empty)]
            },
            World = state.World! with { NextCardInstanceId = 6 }
        };

        state = Combat(state);

        var combat = state.World!.Combat!;
        Assert.Equal(3, combat.CounterState.CardsPlayedThisTurn);
        Assert.Equal(0, combat.CounterState.ManualCardsPlayedThisTurn);
        Assert.Equal(0, combat.Energy);
        Assert.Contains(combat.Cards.Single(card => card.CardId
            == NativeGloryAncientsV111.EnthralledCardId).InstanceId,
            combat.DiscardPile);
    }

    [Theory]
    [InlineData(0, false, 3)]
    [InlineData(1, false, 5)]
    [InlineData(2, true, 7)]
    public void TearsSamplesLeftoverEnergyAndOverwritesItsFlagEachTurn(int energy, bool iceCream, int expected)
    {
        var acquired = Acquire(NativePaelRewardsV111.TearsId);
        if (iceCream)
            acquired = acquired with { Player = acquired.Player with { Relics =
                [..acquired.Player.Relics, new("proto.relic.ice_cream", PrototypeJson.EmptyObject())] } };
        var state = Combat(acquired);
        Assert.Equal(3, state.World!.Combat!.Energy);
        Assert.False(state.World.Combat.RelicStates.Single(relic => relic.RelicId == NativePaelRewardsV111.TearsId).HadLeftoverEnergy);
        state = state with { World = state.World with { Combat = state.World.Combat with { Energy = energy } } };
        state = EndTurn(state);
        Assert.Equal(expected, state.World!.Combat!.Energy);
        Assert.Equal(energy > 0, state.World.Combat.RelicStates.Single(relic => relic.RelicId == NativePaelRewardsV111.TearsId).HadLeftoverEnergy);
        state = state with { World = state.World with { Combat = state.World.Combat with { Energy = 0 } } };
        state = EndTurn(state);
        Assert.Equal(3, state.World!.Combat!.Energy);
        Assert.False(state.World.Combat.RelicStates.Single(relic => relic.RelicId == NativePaelRewardsV111.TearsId).HadLeftoverEnergy);
        Assert.Equal(3, Combat(acquired).World!.Combat!.Energy);
    }

    [Fact]
    public void TearsBonusIsNotReplayedWhenMittensSuspendsTheFollowingTurn()
    {
        var acquired = Acquire(NativePaelRewardsV111.TearsId);
        acquired = acquired with { Player = acquired.Player with { Relics =
            [..acquired.Player.Relics, new(NativeAncientRewardsV111.ToastyMittensId, PrototypeJson.EmptyObject())] } };
        var state = Combat(acquired);
        state = Engine.Step(state, Engine.GetLegalActions(state).First()).State;
        state = EndTurn(state);
        Assert.NotNull(state.World!.Combat!.PendingChoice);
        Assert.Equal(5, state.World.Combat.Energy);
        var choose = Engine.GetLegalActions(state).First();
        var next = Engine.Step(state, choose).State;
        Assert.Equal(CanonicalJson.Sha256(next), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), choose).State));
        Assert.Equal(5, next.World!.Combat!.Energy);
        Assert.Null(next.World.Combat.PendingChoice);
        PrototypeStateInvariants.Validate(next);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 8)]
    public void ClawDefendsExhaustAndTheirGoopyGrowthPersistsIntoNextCombat(int upgrade, int baseBlock)
    {
        var acquired = Acquire(NativePaelRewardsV111.ClawId);
        var defend = acquired.Player.Deck.First(card => card.Enchantment?.Kind == PrototypeCardEnchantmentKind.Goopy);
        acquired = acquired with { Player = acquired.Player with { Deck = acquired.Player.Deck.Select(card =>
            card.InstanceId == defend.InstanceId ? card with { UpgradeLevel = upgrade } : card).ToArray() } };
        var state = Combat(acquired);
        for (var playNumber = 1; playNumber <= 2; playNumber++)
        {
            var combat = state.World!.Combat!;
            state = state with { World = state.World with { Combat = combat with
            {
                Hand = combat.Hand.Contains(defend.InstanceId) ? combat.Hand : [..combat.Hand, defend.InstanceId],
                DrawPile = combat.DrawPile.Where(id => id != defend.InstanceId).ToArray(),
                ExhaustPile = combat.ExhaustPile.Where(id => id != defend.InstanceId).ToArray(),
                PlayerBlock = 0, Energy = 3
            } } };
            var play = Engine.GetLegalActions(state).Single(action => action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == defend.InstanceId);
            var next = Engine.Step(state, play).State;
            Assert.Equal(CanonicalJson.Sha256(next), CanonicalJson.Sha256(
                Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), play).State));
            state = next;
            Assert.Equal(baseBlock + playNumber - 1, state.World!.Combat!.PlayerBlock);
            Assert.Contains(defend.InstanceId, state.World.Combat.ExhaustPile);
            Assert.Equal(playNumber + 1, state.Player.Deck.Single(card => card.InstanceId == defend.InstanceId).Enchantment!.Amount);
            Assert.Equal(playNumber + 1, state.World.Combat.Cards.Single(card => card.InstanceId == defend.InstanceId).Enchantment!.Amount);
            PrototypeStateInvariants.Validate(state);
        }
        var nextCombat = Combat(state);
        Assert.Equal(3, nextCombat.World!.Combat!.Cards.Single(card => card.InstanceId == defend.InstanceId).Enchantment!.Amount);
    }

    [Fact]
    public void ClawPreservesExistingEnchantmentsAndOnlyEnchantsDefendTags()
    {
        var state = NativePaelRewardsV111.CreateRewardFixture("claw-eligibility", NativePaelRewardsV111.ClawId);
        var defend = state.Player.Deck.First(card => PrototypeContent.Card(card.CardId).Tags?.Contains("Defend") == true);
        state = state with { Player = state.Player with { Deck = state.Player.Deck.Select(card =>
            card.InstanceId == defend.InstanceId ? card with { Enchantment = new(PrototypeCardEnchantmentKind.Nimble, 2) } : card).ToArray() } };
        var acquired = Engine.Step(state, Assert.Single(Engine.GetLegalActions(state))).State;
        Assert.Equal(new PrototypeCardEnchantment(PrototypeCardEnchantmentKind.Nimble, 2),
            acquired.Player.Deck.Single(card => card.InstanceId == defend.InstanceId).Enchantment);
        foreach (var card in acquired.Player.Deck.Where(card => card.InstanceId != defend.InstanceId))
            if (PrototypeContent.Card(card.CardId).Tags?.Contains("Defend") == true)
                Assert.Equal(new PrototypeCardEnchantment(PrototypeCardEnchantmentKind.Goopy, 1), card.Enchantment);
            else
                Assert.Null(card.Enchantment);
        PrototypeStateInvariants.Validate(acquired);
    }
}
