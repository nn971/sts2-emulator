using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeRouteHistoryTests
{
    [Fact]
    public void CompletedCombatRewardsRecordOnceAndTrailLedgerCountsPerAct()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            1,
            1,
            PrototypeRoomType.Combat,
            RunPhase.Reward,
            [],
            ["proto.relic.trail_ledger"]);

        Assert.Empty(state.World!.CompletedRooms);
        Assert.Equal(10, state.Player.Gold);
        state = engine.Step(
            state,
            Assert.Single(engine.GetLegalActions(state))).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(10, state.Player.Gold);
        var first = Assert.Single(state.World!.CompletedRooms);
        Assert.Equal((1, 1, PrototypeRoomType.Combat),
            (first.Act, first.Floor, first.RoomType));

        var snapshot = new PrototypeAiEnvironment().Observe(state);
        Assert.Equal(state.World.CompletedRooms, snapshot.Observation.CompletedRooms);
        var fork = state.Fork();
        fork.World!.CompletedRoomHistory![0] = new PrototypeCompletedRoomRecord(
            1, 1, "tampered", PrototypeRoomType.Combat);
        Assert.Equal("f1", state.World.CompletedRooms[0].NodeId);

        state = state with
        {
            Phase = RunPhase.Reward,
            World = state.World with
            {
                Floor = 2,
                ActiveRoom = PrototypeRoomType.Combat,
                Map = state.World.Map with { CurrentNodeId = "f2" },
                Reward = ResolvedCombatReward()
            }
        };

        state = engine.Step(
            state,
            Assert.Single(engine.GetLegalActions(state))).State;
        Assert.Equal(30, state.Player.Gold);
        Assert.Equal(2, state.World!.CompletedRooms.Length);
        Assert.Equal("f2", state.World.CompletedRooms[1].NodeId);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void CompletedBossIsRecordedBeforeActTransitionAndHistorySurvives()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            1,
            6,
            PrototypeRoomType.Boss,
            RunPhase.Reward,
            [],
            []);
        state = state with
        {
            World = state.World! with
            {
                Reward = ResolvedCombatReward(endsAct: true),
                EncounterHistory =
                [
                    PrototypeContent.Encounters
                        .First(encounter => encounter.RoomType == PrototypeRoomType.Boss).Id
                ]
            }
        };

        state = engine.Step(
            state,
            Assert.Single(engine.GetLegalActions(state))).State;

        Assert.Equal(RunPhase.ActTransition, state.Phase);
        var boss = Assert.Single(state.World!.CompletedRooms);
        Assert.Equal(PrototypeRoomType.Boss, boss.RoomType);
        Assert.Equal(6, boss.Floor);

        state = engine.Step(
            state,
            Assert.Single(engine.GetLegalActions(state))).State;
        Assert.Equal(2, state.World!.Act);
        Assert.Equal(0, state.World.Floor);
        Assert.Equal(boss, Assert.Single(state.World.CompletedRooms));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void PathBrokerUnlocksEliteDividendOnlyAfterCompletedElite()
    {
        var engine = new PrototypeGameEngine();
        var history = new[]
        {
            new PrototypeCompletedRoomRecord(1, 1, "f1", PrototypeRoomType.Combat),
            new PrototypeCompletedRoomRecord(1, 2, "f2", PrototypeRoomType.Combat),
            new PrototypeCompletedRoomRecord(1, 3, "f3", PrototypeRoomType.Elite)
        };
        var state = CreateState(
            1, 4, PrototypeRoomType.Event, RunPhase.Event, history, []);
        var choices = ChoiceIds(engine, state);
        Assert.Contains("elite_contract", choices);
        Assert.DoesNotContain("combat_streak", choices);
        Assert.Contains("ordinary_trade", choices);

        state = SelectEvent(engine, state, "elite_contract");
        Assert.Equal(85, state.Player.Gold);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(PrototypeRoomType.Event,
            state.World!.CompletedRooms[^1].RoomType);
        Assert.Equal(4, state.World.CompletedRooms.Length);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void PathBrokerStreakIsConsecutiveAndEliteCountsResetByAct()
    {
        var engine = new PrototypeGameEngine();
        var history = new[]
        {
            new PrototypeCompletedRoomRecord(1, 3, "old-elite", PrototypeRoomType.Elite),
            new PrototypeCompletedRoomRecord(2, 1, "f1", PrototypeRoomType.Combat),
            new PrototypeCompletedRoomRecord(2, 2, "f2", PrototypeRoomType.Combat),
            new PrototypeCompletedRoomRecord(2, 3, "f3", PrototypeRoomType.Combat)
        };
        var state = CreateState(
            2, 4, PrototypeRoomType.Event, RunPhase.Event, history, []);
        var choices = ChoiceIds(engine, state);
        Assert.DoesNotContain("elite_contract", choices);
        Assert.Contains("combat_streak", choices);

        state = SelectEvent(engine, state, "combat_streak");
        Assert.Equal(75, state.Player.MaxHp);
        Assert.Equal(75, state.Player.Hp);
        Assert.Equal(10, state.Player.Gold);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void InvalidRouteChronologyIsRejected()
    {
        var invalid = new[]
        {
            new PrototypeCompletedRoomRecord(1, 2, "f2", PrototypeRoomType.Combat),
            new PrototypeCompletedRoomRecord(1, 1, "f1", PrototypeRoomType.Combat)
        };
        var state = CreateState(
            1, 4, PrototypeRoomType.Event, RunPhase.Event, invalid, []);

        Assert.Throws<InvalidOperationException>(
            () => PrototypeStateInvariants.Validate(state));
    }

    private static string[] ChoiceIds(
        PrototypeGameEngine engine,
        RunState state) =>
        engine.GetLegalActions(state)
            .Where(action => action.Kind == "event_choice")
            .Select(action => action.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();

    private static RunState SelectEvent(
        PrototypeGameEngine engine,
        RunState state,
        string choiceId) =>
        engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "event_choice"
                    && action.ReadPayload<EventChoicePayload>().ChoiceId == choiceId)
        ).State;

    private static RewardState ResolvedCombatReward(bool endsAct = false) =>
        new("Combat", [], null, null,
            CardResolved: true,
            PotionResolved: true,
            RelicResolved: true,
            EndsAct: endsAct);

    private static RunState CreateState(
        int act,
        int floor,
        PrototypeRoomType room,
        RunPhase phase,
        PrototypeCompletedRoomRecord[] history,
        string[] relics)
    {
        var nodes = new[]
        {
            new MapNodeState("f1", act, 1, PrototypeRoomType.Combat, ["f2"]),
            new MapNodeState("f2", act, 2, PrototypeRoomType.Combat, ["f3"]),
            new MapNodeState("f3", act, 3,
                history.Any(visit => visit.Act == act
                    && visit.Floor == 3
                    && visit.RoomType == PrototypeRoomType.Elite)
                        ? PrototypeRoomType.Elite : PrototypeRoomType.Combat,
                ["f4"]),
            new MapNodeState("f4", act, 4, PrototypeRoomType.Event, ["f5"]),
            new MapNodeState("f5", act, 5, PrototypeRoomType.Rest, ["f6"]),
            new MapNodeState("f6", act, 6, PrototypeRoomType.Boss, [])
        };
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70, 70, 10, [],
            relics.Select(id => new RelicInstance(id, empty)).ToArray(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);
        var world = new RunWorldState(
            PrototypeContent.RulesetId,
            PrototypeContent.CharacterId,
            act,
            floor,
            1,
            room,
            new MapState(nodes,
                CurrentNodeId: $"f{floor}",
                EntryNodeIds: ["f1"]),
            null,
            phase == RunPhase.Reward ? ResolvedCombatReward() : null,
            null,
            phase == RunPhase.Event ? new EventState("proto.event.path_broker") : null,
            null,
            EventHistory: phase == RunPhase.Event ? ["proto.event.path_broker"] : null,
            CompletedRoomHistory: history);
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "route-history-test",
            "route-history-test",
            0,
            phase,
            player,
            PrototypeRng.CreateBundle("route-history-test"),
            empty,
            world);
    }
}
