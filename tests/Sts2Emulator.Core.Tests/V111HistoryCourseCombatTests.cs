using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HistoryCourseCombatTests
{
    private static readonly PrototypeGameEngine Engine = new();
    private const string Relic = PrototypeNativeHiveEvents.HistoryCourseId;
    private const string Strike = "proto.silent.strike";

    [Fact]
    public void NoAttackLastTurnMeansNoHistoryCourseAutoPlay()
    {
        var combat = Fight(withRelic: true);
        var startCards = combat.World!.Combat!.Cards.Length;
        var result = EndTurn(combat);
        Assert.Equal(RunPhase.Combat, result.Phase);
        Assert.Equal(2, result.World!.Combat!.Turn);
        Assert.Null(result.World.Combat.CounterState.LastOriginalAttackLastTurn);
        Assert.Equal(startCards, result.World.Combat.Cards.Length);
        Assert.DoesNotContain(result.World.Combat.Cards,
            c => c.IsHistoryCourseDupe);
    }

    [Fact]
    public void WithoutHistoryCourseAttacksDoNotChangeTheNormalTurnPipeline()
    {
        var combat = Fight(withRelic: false);
        var played = PlayForcedStrike(combat);
        Assert.Null(played.World!.Combat!.CounterState.LastOriginalAttackThisTurn);
        var next = EndTurn(played);
        Assert.Null(next.World!.Combat!.CounterState.LastOriginalAttackLastTurn);
        Assert.DoesNotContain(next.World.Combat.Cards,
            card => card.IsHistoryCourseDupe);
    }

    [Fact]
    public void HistoryCourseReplaysLastFinishedAttackAsFreeDupeNextTurn()
    {
        var combat = Fight(withRelic: true);
        var played = PlayForcedStrike(combat);
        var turnOne = played.World!.Combat!;
        var savedAttack = turnOne.CounterState.LastOriginalAttackThisTurn;
        Assert.NotNull(savedAttack);
        Assert.Equal(Strike, savedAttack.CardId);
        Assert.Null(turnOne.CounterState.LastOriginalAttackLastTurn);
        // Plating can absorb the replayed Attack entirely, so compare
        // HP + Block against a fork with History Course temporarily
        // suppressed rather than requiring HP damage.
        var withoutRelic = played.Fork();
        withoutRelic = withoutRelic with
        {
            Player = withoutRelic.Player with
            {
                Relics = withoutRelic.Player.Relics.Where(
                    relic => relic.RelicId != Relic).ToArray()
            },
            World = withoutRelic.World! with
            {
                Combat = withoutRelic.World.Combat! with
                {
                    Relics = withoutRelic.World.Combat.RelicStates.Where(
                        relic => relic.RelicId != Relic).ToArray()
                }
            }
        };
        var withoutReplay = EndTurn(withoutRelic);
        var enemyWithout = Assert.Single(withoutReplay.World!.Combat!.Enemies);

        var replayed = EndTurn(played);
        Assert.Equal(RunPhase.Combat, replayed.Phase);
        var turnTwo = replayed.World!.Combat!;
        Assert.Equal(2, turnTwo.Turn);
        Assert.Equal(Strike, turnTwo.CounterState.LastOriginalAttackLastTurn!.CardId);
        Assert.Null(turnTwo.CounterState.LastOriginalAttackThisTurn);
        Assert.Equal(1, turnTwo.CounterState.AttacksPlayedThisTurn);
        Assert.Equal(1, turnTwo.CounterState.AttacksPlayedLastTurn);
        Assert.Equal(3, turnTwo.Energy);
        var dupe = Assert.Single(turnTwo.Cards,
            card => card.IsHistoryCourseDupe);
        Assert.True(dupe.IsTemporary);
        Assert.Null(dupe.PersistentCardInstanceId);
        Assert.Contains(dupe.InstanceId,
            turnTwo.CounterState.PlayedCardIdsThisTurn!);
        Assert.Contains(dupe.InstanceId, turnTwo.DiscardPile);
        var enemyWith = Assert.Single(turnTwo.Enemies);
        Assert.True(enemyWith.Hp + enemyWith.Block
            < enemyWithout.Hp + enemyWithout.Block);
        Assert.Equal(CanonicalJson.Sha256(replayed),
            CanonicalJson.Sha256(EndTurn(played.Fork())));
    }

    [Fact]
    public void HistoryCourseDoesNotReplayItsOwnDuplicateOnFollowingTurn()
    {
        var afterAttack = PlayForcedStrike(Fight(withRelic: true));
        var secondTurn = EndTurn(afterAttack);
        Assert.Single(secondTurn.World!.Combat!.Cards,
            card => card.IsHistoryCourseDupe);
        var thirdTurn = EndTurn(secondTurn);
        Assert.Equal(RunPhase.Combat, thirdTurn.Phase);
        Assert.Equal(3, thirdTurn.World!.Combat!.Turn);
        Assert.Null(thirdTurn.World.Combat.CounterState.LastOriginalAttackLastTurn);
        Assert.Single(thirdTurn.World.Combat.Cards,
            card => card.IsHistoryCourseDupe);
        Assert.Equal(0,
            thirdTurn.World.Combat.CounterState.AttacksPlayedThisTurn);
        Assert.Equal(CanonicalJson.Sha256(thirdTurn),
            CanonicalJson.Sha256(thirdTurn.Fork()));
    }

    [Fact]
    public void UpgradedLastAttackSnapshotAndReplayAreForkOwned()
    {
        var fight = Fight(withRelic: true);
        var target = fight.World!.Combat!.Cards.First(
            card => card.CardId == Strike);
        var altered = fight.World.Combat with
        {
            Cards = fight.World.Combat.Cards.Select(c =>
                c.InstanceId == target.InstanceId
                    ? c with { UpgradeLevel = 1 } : c).ToArray()
        };
        fight = fight with
        {
            World = fight.World with { Combat = altered }
        };
        var played = PlayForcedStrike(fight);
        Assert.Equal(1, played.World!.Combat!.CounterState
            .LastOriginalAttackThisTurn!.UpgradeLevel);
        var checkpoint = played.Fork();
        var originalHash = CanonicalJson.Sha256(played);
        var next = EndTurn(checkpoint);
        Assert.Equal(1, Assert.Single(next.World!.Combat!.Cards,
            card => card.IsHistoryCourseDupe).UpgradeLevel);
        Assert.Equal(originalHash, CanonicalJson.Sha256(played));
        Assert.Equal(CanonicalJson.Sha256(next),
            CanonicalJson.Sha256(EndTurn(played)));
    }

    private static RunState Fight(bool withRelic)
    {
        var start = Engine.Step(
            PrototypeGameFactory.Create("v111-history-course-combat"),
            GameAction.Empty("start_run")).State;

        var relics = withRelic
            ? start.Player.Relics.Append(new RelicInstance(
                Relic, PrototypeJson.EmptyObject())).ToArray()
            : start.Player.Relics;

        // Reuse the pinned Lantern Key's forced Mysterious Knight
        // encounter to avoid depending on native map generation.
        var fixture = start with
        {
            Phase = RunPhase.Event,
            Player = start.Player with { Relics = relics },
            World = start.World! with
            {
                Act = 2, Floor = 1,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(
                    PrototypeNativeHiveEvents.LanternKeyEventId),
                Map = new MapState(
                [
                    new("event", 2, 1, PrototypeRoomType.Event, ["next"]),
                    new("next", 2, 2, PrototypeRoomType.Combat, [])
                ], "event", ["event"])
            }
        };

        fixture = Choose(fixture, "keep_key");
        fixture = Choose(fixture, "fight");
        Assert.Equal(RunPhase.Combat, fixture.Phase);
        Assert.Equal(withRelic,
            fixture.World!.Combat!.RelicStates.Any(x => x.RelicId == Relic));
        return fixture;
    }

    private static RunState PlayForcedStrike(RunState state)
    {
        var combat = state.World!.Combat!;
        var strike = combat.Cards.First(c => c.CardId == Strike);
        var staged = combat with
        {
            Hand = [strike.InstanceId],
            DrawPile = combat.Cards.Where(c =>
                c.InstanceId != strike.InstanceId)
                .Select(c => c.InstanceId).ToArray(),
            DiscardPile = [],
            ExhaustPile = []
        };
        state = state with
        {
            World = state.World with { Combat = staged }
        };
        var action = Engine.GetLegalActions(state).Single(a =>
            a.Kind == "play_card"
            && a.ReadPayload<PlayCardPayload>().CardInstanceId
                == strike.InstanceId);
        return Engine.Step(state, action).State;
    }

    private static RunState EndTurn(RunState state) =>
        Engine.Step(state, Engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;

    private static RunState Choose(RunState state, string id) =>
        Engine.Step(state, Engine.GetLegalActions(state).Single(
            a => a.Kind == "event_choice"
                && a.ReadPayload<EventChoicePayload>().ChoiceId == id)).State;
}
