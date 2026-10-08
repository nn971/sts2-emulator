using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeDenseVegetationTests
{
    private static RunState EnterDenseVegetation(
        string seed, int currentHp = 30)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed);
        state = state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with
                {
                    OfferedChoiceIds =
                    [
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "GoldenPearl"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "BoomingConch"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "CursedPearl")
                    ]
                }
            }
        };
        var goldenPearl = engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice" &&
            a.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                    "GoldenPearl"));
        state = engine.Step(state, goldenPearl).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);

        var map = state.World!.Map;
        var unknown = map.Nodes.First(n =>
            n.RoomType == PrototypeRoomType.Unknown);
        var parent = map.Nodes.First(n =>
            n.NextNodeIds?.Contains(unknown.NodeId,
                StringComparer.Ordinal) == true);
        var visits = Enumerable.Range(1, parent.Floor)
            .Select(floor =>
            {
                var node = floor == parent.Floor
                    ? parent
                    : map.Nodes.First(n => n.Floor == floor);
                return new PrototypeCompletedRoomRecord(
                    1, floor, node.NodeId, node.RoomType);
            }).ToArray();
        state = state with
        {
            Player = state.Player with { Hp = currentHp },
            Phase = RunPhase.Event,
            World = state.World with
            {
                Floor = unknown.Floor,
                Map = map with { CurrentNodeId = unknown.NodeId },
                ActiveRoom = PrototypeRoomType.Event,
                CompletedRoomHistory = visits,
                Event = new EventState(
                    PrototypeNativeDenseVegetation.EventId,
                    NativeEventGold: 80),
                EventHistory = state.World.EventIds.Append(
                    PrototypeNativeDenseVegetation.EventId).ToArray()
            }
        };
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    [Fact]
    public void TrudgeDealsEightDamageAndGrantsRolledGold()
    {
        var state = EnterDenseVegetation(
            "dense-vegetation-trudge", currentHp: 30);
        var engine = new PrototypeGameEngine();
        var gold = state.Player.Gold;
        var floor = state.World!.Floor;
        var action = engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId == "trudge");
        state = engine.Step(state, action).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(22, state.Player.Hp);
        Assert.Equal(gold + 80, state.Player.Gold);
        Assert.Equal(floor, state.World!.Floor);
        Assert.Null(state.World.Event);
        Assert.Null(state.World.Combat);
        Assert.Null(state.World.Reward);
        Assert.Contains(state.World.CompletedRooms, room =>
            room.RoomType == PrototypeRoomType.Event
            && room.Floor == floor);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void RestThenFightFourWrigglersWithNoCombatRewards()
    {
        var state = EnterDenseVegetation(
            "dense-vegetation-rest-and-fight", currentHp: 20);
        var engine = new PrototypeGameEngine();
        var beforeGold = state.Player.Gold;
        var beforeEncounterCount = state.World!.EncounterIds.Length;
        var beforeWeakCount = state.World.ActOneEncounterPool!
            .OrdinaryCombatsStarted;
        var rest = engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId == "rest");

        state = engine.Step(state, rest).State;
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(41, state.Player.Hp);
        Assert.Equal(1, state.World!.Event!.NativePageIndex);
        var choices = engine.GetLegalActions(state);
        var fight = Assert.Single(choices);
        Assert.Equal("fight",
            fight.ReadPayload<EventChoicePayload>().ChoiceId);
        Assert.Null(state.World.Combat);
        PrototypeStateInvariants.Validate(state);

        state = engine.Step(state, fight).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal(PrototypeRoomType.Event, state.World!.ActiveRoom);
        Assert.Equal(2, state.World.Event!.NativePageIndex);
        var combat = state.World.Combat!;
        Assert.Equal(4, combat.Enemies.Length);
        Assert.All(combat.Enemies, e => Assert.Equal(
            "proto.enemy.wriggler", e.EnemyId));
        Assert.Equal(beforeEncounterCount + 1,
            state.World.EncounterIds.Length);
        Assert.Equal(PrototypeNativeDenseVegetation.EncounterId,
            state.World.EncounterIds[^1]);
        Assert.Equal(beforeWeakCount,
            state.World.ActOneEncounterPool!.OrdinaryCombatsStarted);
        PrototypeStateInvariants.Validate(state);

        var neutralize = combat.Cards.First(c =>
            c.CardId == "proto.silent.neutralize");
        var others = combat.Cards.Select(c => c.InstanceId)
            .Where(id => id != neutralize.InstanceId).ToArray();
        combat = combat with
        {
            Hand = [neutralize.InstanceId],
            DrawPile = others,
            DiscardPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3,
            Enemies = combat.Enemies.Select((enemy, index) =>
                enemy with { Hp = index == 0 ? 1 : 0, Block = 0 }).ToArray()
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };
        var attack = engine.GetLegalActions(state).Single(a =>
            a.Kind == "play_card"
            && a.ReadPayload<PlayCardPayload>().CardInstanceId
                == neutralize.InstanceId
            && a.ReadPayload<PlayCardPayload>().TargetEnemyId
                == combat.Enemies[0].InstanceId);
        state = engine.Step(state, attack).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Null(state.World!.Event);
        Assert.Null(state.World.Combat);
        Assert.Null(state.World.Reward);
        Assert.Equal(beforeGold, state.Player.Gold);
        Assert.Contains(state.World.CompletedRooms, room =>
            room.RoomType == PrototypeRoomType.Event);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void LethalTrudgeEndsRunWithoutGrantingGold()
    {
        var state = EnterDenseVegetation(
            "dense-vegetation-lethal-trudge", currentHp: 8);
        var gold = state.Player.Gold;
        var engine = new PrototypeGameEngine();
        var trudge = engine.GetLegalActions(state).Single(a =>
            a.ReadPayload<EventChoicePayload>().ChoiceId == "trudge");
        state = engine.Step(state, trudge).State;
        Assert.Equal(RunPhase.Terminal, state.Phase);
        Assert.Equal("defeat", state.World!.TerminalOutcome);
        Assert.Equal(0, state.Player.Hp);
        Assert.Equal(gold, state.Player.Gold);
        PrototypeStateInvariants.Validate(state);
    }
}
