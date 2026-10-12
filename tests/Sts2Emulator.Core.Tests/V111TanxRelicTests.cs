using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111TanxRelicTests
{
    [Fact]
    public void IronClubDrawsEveryFourthCardAndPersistsItsCounter()
    {
        var (engine, state) = StartCombat(
            NativeGloryAncientsV111.IronClubId, cardsPlayed: 0);
        var initialHandCount = state.World!.Combat!.Hand.Length;

        for (var cardNumber = 1; cardNumber <= 3; cardNumber++)
        {
            var cardId = state.World.Combat!.Hand[0];
            var action = engine.GetLegalActions(state).First(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>().CardInstanceId
                    == cardId);
            state = engine.Step(state, action).State;
            Assert.Equal(initialHandCount - cardNumber,
                state.World!.Combat!.Hand.Length);
        }

        var snapshotHash = CanonicalJson.Sha256(state);
        state = RunSnapshot.Load(RunSnapshot.Save(state));
        Assert.Equal(snapshotHash, CanonicalJson.Sha256(state));
        var playedCardId = state.World!.Combat!.Hand[0];
        var fourthCard = engine.GetLegalActions(state).First(item =>
            item.Kind == "play_card"
            && item.ReadPayload<PlayCardPayload>().CardInstanceId
                == playedCardId);
        state = engine.Step(state, fourthCard).State;

        Assert.Equal(initialHandCount - 3, state.World!.Combat!.Hand.Length);
        var relic = Assert.Single(state.Player.Relics, item =>
            item.RelicId == NativeGloryAncientsV111.IronClubId);
        Assert.Equal(4, relic.PersistentState.GetProperty("CardsPlayed")
            .GetInt32());
    }

    [Fact]
    public void IronClubRestoresItsCounterWhenTheNextCombatStarts()
    {
        var (engine, state) = StartCombat(
            NativeGloryAncientsV111.IronClubId, cardsPlayed: 3);
        var initialHandCount = state.World!.Combat!.Hand.Length;
        state = RunSnapshot.Load(RunSnapshot.Save(state));
        var cardId = state.World!.Combat!.Hand[0];
        var action = engine.GetLegalActions(state).First(item =>
            item.Kind == "play_card"
            && item.ReadPayload<PlayCardPayload>().CardInstanceId == cardId);

        state = engine.Step(state, action).State;

        Assert.Equal(initialHandCount, state.World!.Combat!.Hand.Length);
        var relic = Assert.Single(state.Player.Relics, item =>
            item.RelicId == NativeGloryAncientsV111.IronClubId);
        Assert.Equal(4, relic.PersistentState.GetProperty("CardsPlayed")
            .GetInt32());
    }

    [Fact]
    public void ThrowingAxeRepeatsOnlyTheFirstCardOfEachCombat()
    {
        var (engine, state) = StartCombat(
            NativeGloryAncientsV111.ThrowingAxeId);
        var firstCardId = state.World!.Combat!.Hand[0];
        var firstPlay = engine.GetLegalActions(state).First(item =>
            item.Kind == "play_card"
            && item.ReadPayload<PlayCardPayload>().CardInstanceId
                == firstCardId);

        state = engine.Step(state, firstPlay).State;

        Assert.Equal(2,
            state.World!.Combat!.CounterState.CardsPlayedThisCombat);
        var throwingAxe = Assert.Single(state.World.Combat.RelicStates,
            relic => relic.RelicId == NativeGloryAncientsV111.ThrowingAxeId);
        Assert.True(throwingAxe.UsedFirstCardPlayModifierThisCombat);

        var nextCardId = state.World.Combat.Hand[0];
        var secondPlay = engine.GetLegalActions(state).First(item =>
            item.Kind == "play_card"
            && item.ReadPayload<PlayCardPayload>().CardInstanceId
                == nextCardId);
        state = engine.Step(state, secondPlay).State;
        Assert.Equal(3,
            state.World!.Combat!.CounterState.CardsPlayedThisCombat);
    }

    [Fact]
    public void TanxsWhistleAcquisitionAddsOneWhistleCard()
    {
        var initial = PrototypeNativeUnderdocksRunFactory.Create(
            "tanx-whistle-acquisition");
        var state = initial with
        {
            Phase = RunPhase.Reward,
            World = initial.World! with
            {
                Floor = 1,
                Map = new MapState([]),
                ActiveRoom = PrototypeRoomType.Combat,
                Combat = null,
                Shop = null,
                Event = null,
                Reward = new RewardState("Combat", [], null,
                    NativeGloryAncientsV111.TanxsWhistleId,
                    CardResolved: true, PotionResolved: true,
                    RelicResolved: false, EndsAct: false)
            }
        };
        var engine = new PrototypeGameEngine();
        var acquire = engine.GetLegalActions(state).Single(action =>
            action.Kind == "take_reward_relic");

        state = engine.Step(state, acquire).State;

        var whistle = Assert.Single(state.Player.Deck, card =>
            card.CardId == NativeGloryAncientsV111.WhistleCardId);
        Assert.Equal(0, whistle.UpgradeLevel);
        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == NativeGloryAncientsV111.TanxsWhistleId);
    }

    [Theory]
    [InlineData(0, 33)]
    [InlineData(1, 44)]
    public void WhistleDealsItsDamageAndStunsTheLivingTarget(
        int upgradeLevel,
        int damage)
    {
        var (engine, state) = StartCombat(
            relicId: null,
            firstCardId: NativeGloryAncientsV111.WhistleCardId,
            firstCardUpgradeLevel: upgradeLevel);
        var combat = state.World!.Combat!;
        var target = combat.Enemies[0];
        var followUpMove = target.PlannedMoveIndex ?? target.MoveIndex;
        var followUpAiState = target.PlannedNextAiStateId
            ?? target.AiStateId;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Enemies = combat.Enemies.Select(enemy => enemy with
                    {
                        Hp = 100,
                        MaxHp = 100,
                        EnemyActionSkipsRemaining = 0,
                        SkipNextEnemyAction = false
                    }).ToArray()
                }
            }
        };
        var cardId = state.World.Combat!.Hand.Single(id => id == 1);
        var play = engine.GetLegalActions(state).First(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == cardId
            && action.ReadPayload<PlayCardPayload>().TargetEnemyId
                == target.InstanceId);

        state = engine.Step(state, play).State;

        var stunned = Assert.Single(state.World!.Combat!.Enemies,
            enemy => enemy.InstanceId == target.InstanceId);
        Assert.Equal(100 - damage, stunned.Hp);
        Assert.Equal(1, stunned.EnemyActionSkipsRemaining);
        Assert.Null(stunned.PlannedMoveIndex);
        Assert.Equal(followUpMove, stunned.StunFollowUpMoveIndex);
        Assert.Equal(followUpAiState, stunned.StunFollowUpAiStateId);
        Assert.Contains(1, state.World.Combat.ExhaustPile);

        var stunnedSnapshotHash = CanonicalJson.Sha256(state);
        state = RunSnapshot.Load(RunSnapshot.Save(state));
        Assert.Equal(stunnedSnapshotHash, CanonicalJson.Sha256(state));
        var endTurn = engine.GetLegalActions(state).Single(action =>
            action.Kind == "end_turn");
        state = engine.Step(state, endTurn).State;
        var resumed = Assert.Single(state.World!.Combat!.Enemies,
            enemy => enemy.InstanceId == target.InstanceId);
        Assert.Equal(0, resumed.EnemyActionSkipsRemaining);
        Assert.Equal(followUpMove, resumed.PlannedMoveIndex);
        Assert.Null(resumed.StunFollowUpMoveIndex);
    }

    private static (PrototypeGameEngine Engine, RunState State) StartCombat(
        string? relicId,
        int cardsPlayed = 0,
        string firstCardId = "proto.silent.shiv",
        int firstCardUpgradeLevel = 0)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create(
            $"tanx-iron-club-{cardsPlayed}");
        state = engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Deck = Enumerable.Range(1, 10)
                    .Select(id => new CardInstance(id,
                        id == 1 ? firstCardId : "proto.silent.shiv",
                        id == 1 ? firstCardUpgradeLevel : 0,
                        empty)).ToArray(),
                Relics = relicId is null
                    ? state.Player.Relics
                    : [..state.Player.Relics,
                        new RelicInstance(relicId,
                            relicId == NativeGloryAncientsV111.IronClubId
                                ? JsonSerializer.SerializeToElement(
                                    new { CardsPlayed = cardsPlayed })
                                : empty)]
            },
            World = state.World! with
            {
                NextCardInstanceId = 11
            }
        };

        var entry = state.World!.Map.Nodes.First(node =>
            node.Floor == 1
            && node.RoomType == PrototypeRoomType.Combat
            && state.World.Map.EntryNodeIds!.Contains(node.NodeId));
        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(entry.NodeId))).State;
        return (engine, state);
    }
}
