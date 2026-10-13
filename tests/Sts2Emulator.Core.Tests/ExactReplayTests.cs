using Sts2Emulator.Core;
using Sts2Emulator.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class ExactReplayTests
{
    [Fact]
    public void RecordedTransitionsReplayWithStateRngAndLegalActions()
    {
        var (header, transitions) = Record();
        var result = ExactReplay.Run(header, transitions, new PrototypeGameEngine());
        Assert.True(result.Matched);
        Assert.Equal(transitions.Length, result.TransitionsChecked);
        Assert.Null(result.Divergence);
    }

    [Fact]
    public void LegalActionDifferenceIsReportedAtTheFirstDecision()
    {
        var (header, transitions) = Record();
        transitions[0] = transitions[0] with { LegalActionsBefore = [] };
        var result = ExactReplay.Run(header, transitions, new PrototypeGameEngine());
        Assert.False(result.Matched);
        Assert.Equal(0, result.TransitionsChecked);
        Assert.Equal("legal-actions-before", result.Divergence!.Dimension);
    }

    [Fact]
    public void StateMismatchReportsAConcretePathWithoutHidingItBehindAHash()
    {
        var (header, transitions) = Record();
        var wrong = transitions[0].After.State;
        wrong = wrong with { Player = wrong.Player with { Hp = wrong.Player.Hp - 1 } };
        transitions[0] = transitions[0] with { After = new(CanonicalJson.Sha256(wrong), wrong) };
        var result = ExactReplay.Run(header, transitions, new PrototypeGameEngine());
        Assert.False(result.Matched);
        Assert.Equal("after-state", result.Divergence!.Dimension);
        Assert.Equal("$.player.hp", result.Divergence.Path);
    }

    [Fact]
    public void CorruptHashesBuildsDecisionIndexesAndMissingNativeActionsAreRejected()
    {
        var (header, transitions) = Record();
        Assert.Throws<InvalidDataException>(() => ExactReplay.Run(header with { GameBuild = "wrong" }, transitions, new PrototypeGameEngine()));
        Assert.Throws<InvalidDataException>(() => ExactReplay.Run(header,
            [transitions[0] with { DecisionIndex = 100 }], new PrototypeGameEngine()));
        Assert.Throws<InvalidDataException>(() => ExactReplay.Run(header,
            [transitions[0] with { After = transitions[0].After with { Hash = "corrupt" } }], new PrototypeGameEngine()));
        Assert.Throws<InvalidDataException>(() => ExactReplay.Run(header,
            [transitions[0] with { LegalActionsAfter = null }], new PrototypeGameEngine()));
        Assert.Throws<InvalidDataException>(() => ExactReplay.Run(header, [], new PrototypeGameEngine()));
    }

    private static (TraceHeader Header, TraceTransition[] Transitions) Record()
    {
        var engine = new PrototypeGameEngine();
        var state = V111RunFactory.Create("replay-fixture");
        var header = new TraceHeader("header", "0.1", state.GameBuild, "synthetic-test",
            "test", state.RunId, state.RunSeed, DateTimeOffset.UnixEpoch);
        var transitions = new List<TraceTransition>();
        for (var index = 0; index < 8 && state.Phase != RunPhase.Terminal; index++)
        {
            var actions = engine.GetLegalActions(state).ToArray();
            var action = actions[0];
            var after = engine.Step(state, action).State;
            transitions.Add(new("transition", after.DecisionIndex, state.Phase, action,
                new(CanonicalJson.Sha256(state), state), new(CanonicalJson.Sha256(after), after),
                state.Rng.Fork(), after.Rng.Fork(), [], actions, engine.GetLegalActions(after).ToArray()));
            state = after;
        }
        return (header, transitions.ToArray());
    }
}
