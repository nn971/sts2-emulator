using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksAbyssalBathsTests
{
    private const string EventId =
        "proto.native.underdocks.abyssal_baths";

    [Fact]
    public void RepeatedImmersionsIncreaseMaxHpAndDamageThenExit()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindBaths(engine);
        Assert.Equal(new[] { "abstain", "immerse" },
            ChoiceIds(engine, initial).OrderBy(x => x).ToArray());

        var first = Select(engine, initial, "immerse");
        Assert.Equal(RunPhase.Event, first.Phase);
        Assert.Equal(initial.Player.MaxHp + 2, first.Player.MaxHp);
        Assert.Equal(initial.Player.Hp - 1, first.Player.Hp);
        Assert.Equal(1, first.World!.Event!.NativePageIndex);
        Assert.Equal(new[] { "exit", "linger" },
            ChoiceIds(engine, first).OrderBy(x => x).ToArray());

        var second = Select(engine, first, "linger");
        Assert.Equal(initial.Player.MaxHp + 4, second.Player.MaxHp);
        Assert.Equal(initial.Player.Hp - 3, second.Player.Hp);
        Assert.Equal(2, second.World!.Event!.NativePageIndex);

        var third = Select(engine, second, "linger");
        Assert.Equal(initial.Player.MaxHp + 6, third.Player.MaxHp);
        Assert.Equal(initial.Player.Hp - 6, third.Player.Hp);
        Assert.Equal(3, third.World!.Event!.NativePageIndex);
        Assert.Equal(CanonicalJson.Sha256(third),
            CanonicalJson.Sha256(third.Fork()));

        var exit = Select(engine, third, "exit");
        Assert.Equal(RunPhase.MapChoice, exit.Phase);
        Assert.Null(exit.World!.Event);
        Assert.Single(exit.World.CompletedRooms);
        Assert.Equal(third.Player.MaxHp, exit.Player.MaxHp);
        Assert.Equal(third.Player.Hp, exit.Player.Hp);
    }

    [Fact]
    public void AbstainingHealsTenAndCompletesTheEvent()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindBaths(engine);
        var wounded = initial with
        {
            Player = initial.Player with
            {
                Hp = initial.Player.MaxHp - 20
            }
        };

        var result = Select(engine, wounded, "abstain");
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(wounded.Player.Hp + 10, result.Player.Hp);
        Assert.Equal(wounded.Player.MaxHp, result.Player.MaxHp);
        Assert.Null(result.World!.Event);
    }

    [Fact]
    public void LingerDamageContinuesBeyondNinthPageAndUsesNoRng()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindBaths(engine);
        var state = initial with
        {
            Player = initial.Player with { Hp = 400, MaxHp = 400 }
        };
        var rngHash = CanonicalJson.Sha256(state.Rng);
        for (var i = 0; i < 12; i++)
        {
            var previous = state;
            state = Select(engine, state, i == 0 ? "immerse" : "linger");
            Assert.Equal(400 + 2 * (i + 1), state.Player.MaxHp);
            Assert.Equal(previous.Player.Hp - (i + 1),
                state.Player.Hp);
            Assert.Equal(i + 1, state.World!.Event!.NativePageIndex);
            Assert.Equal(rngHash, CanonicalJson.Sha256(state.Rng));
        }
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void LethalImmersionIsLegalAndEndsTheRun()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindBaths(engine);
        var vulnerable = initial with
        {
            Player = initial.Player with { Hp = 1 }
        };
        Assert.Contains("immerse", ChoiceIds(engine, vulnerable));
        var result = Select(engine, vulnerable, "immerse");
        Assert.Equal(RunPhase.Terminal, result.Phase);
        Assert.Equal(0, result.Player.Hp);
        Assert.Equal("defeat", result.World!.TerminalOutcome);
    }

    private static string[] ChoiceIds(
        PrototypeGameEngine engine, RunState state) =>
        engine.GetLegalActions(state)
            .Where(action => action.Kind == "event_choice")
            .Select(action =>
                action.Payload.GetProperty("ChoiceId").GetString()!)
            .ToArray();

    private static RunState Select(
        PrototypeGameEngine engine, RunState state, string choice)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "event_choice"
            && item.Payload.GetProperty("ChoiceId").GetString() == choice);
        return engine.Step(state, action).State;
    }

    private static RunState FindBaths(PrototypeGameEngine engine)
    {
        for (var i = 0; i < 128; i++)
        {
            var state = StartUnderdocksEvent(engine, "baths-seed-" + i);
            if (state.World!.Event!.EventId == EventId)
            {
                return state;
            }
        }

        throw new InvalidOperationException(
            "Could not find native Abyssal Baths in event pool.");
    }

    private static RunState StartUnderdocksEvent(
        PrototypeGameEngine engine, string seed)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(seed);
        var node = new MapNodeState(
            "test-underdocks-event", 1, 1,
            PrototypeRoomType.Event, ["next-room"]);
        var next = new MapNodeState(
            "next-room", 1, 2, PrototypeRoomType.Combat, []);
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World! with
            {
                Floor = 0,
                ActiveRoom = null,
                Map = new MapState(
                    [node, next], CurrentNodeId: null,
                    EntryNodeIds: [node.NodeId],
                    GenerationProfileId:
                        PrototypeNativeUnderdocks.GenerationProfileId),
                Event = null,
                Combat = null,
                Reward = null
            }
        };
        return engine.Step(state, Assert.Single(
            engine.GetLegalActions(state))).State;
    }
}
