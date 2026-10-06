using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeFullRunTests
{
    [Fact]
    public void PrototypeStartsAsSilentWholeRun()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("seed-1");

        var action = Assert.Single(engine.GetLegalActions(state));
        state = engine.Step(state, action).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.NotNull(state.World);
        Assert.Equal("silent", state.World.CharacterId);
        Assert.Equal(1, state.World.Act);
        Assert.Equal(0, state.World.Floor);
        Assert.NotEmpty(state.World.Map.Options);
        Assert.Equal(12, state.Player.Deck.Length);
    }

    [Fact]
    public void LegalActionPolicyAlwaysReachesATerminalRun()
    {
        var state = DriveToTerminal("whole-run-smoke");

        Assert.Equal(RunPhase.Terminal, state.Phase);
        Assert.NotNull(state.World?.TerminalOutcome);
        Assert.Contains(state.World.TerminalOutcome, new[] { "victory", "defeat" });
        Assert.True(state.DecisionIndex < 5_000);
    }

    [Fact]
    public void SameSeedAndPolicyProduceIdenticalTerminalState()
    {
        var left = DriveToTerminal("deterministic-seed");
        var right = DriveToTerminal("deterministic-seed");

        Assert.Equal(CanonicalJson.Sha256(left), CanonicalJson.Sha256(right));
    }

    private static RunState DriveToTerminal(string seed)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create(seed);

        for (var step = 0; step < 5_000 && state.Phase != RunPhase.Terminal; step++)
        {
            var legal = engine.GetLegalActions(state);
            Assert.NotEmpty(legal);
            var action = ChooseAction(state, legal);
            state = engine.Step(state, action).State;
        }

        return state;
    }

    private static GameAction ChooseAction(
        RunState state,
        IReadOnlyList<GameAction> legal)
    {
        if (state.Phase == RunPhase.Rest)
        {
            return legal.FirstOrDefault(action => action.Kind == "rest_heal")
                ?? legal[0];
        }

        if (state.Phase == RunPhase.Shop)
        {
            return legal.FirstOrDefault(action => action.Kind == "buy_card")
                ?? legal.First(action => action.Kind == "leave_shop");
        }

        if (state.Phase == RunPhase.Combat)
        {
            return legal.FirstOrDefault(action => action.Kind == "use_potion")
                ?? legal.FirstOrDefault(action => action.Kind == "play_card")
                ?? legal.First(action => action.Kind == "end_turn");
        }

        return legal[0];
    }
}
