using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeConditionedCombatEntryTests
{
    private static (RunState MapState, PrototypeAiEnvironment Environment, string ActionId)
        CombatEntry(string seed)
    {
        var environment = new PrototypeAiEnvironment();
        var state = environment.Reset(seed);
        state = environment.Step(
            state, Assert.Single(environment.Observe(state).LegalActions).ActionId).State;
        var map = environment.Observe(state);
        var action = map.LegalActions.Single(candidate =>
            candidate.Kind == "choose_map_node"
            && state.World!.Map.AvailableNodes()
                .Single(node =>
                    node.NodeId == System.Text.Json.JsonSerializer.Deserialize<
                        ChooseMapNodePayload>(candidate.Payload)!.NodeId)
                .RoomType == PrototypeRoomType.Combat);
        return (state, environment, action.ActionId);
    }

    private static RunState Candidate(
        RunState mapState, PrototypeAiEnvironment environment,
        string actionId, string independentSeed, int trial)
    {
        var state = mapState.Fork();
        var synthetic = PrototypeRng.CreateBundle(
            $"local-combat-condition-v1:{independentSeed}:{trial}");
        var combat = synthetic.Streams.Single(stream => stream.StreamId == "combat");
        var index = Array.FindIndex(
            state.Rng.Streams, stream => stream.StreamId == "combat");
        Assert.True(index >= 0);
        state.Rng.Streams[index] = combat;
        return environment.Step(state, actionId).State;
    }

    [Fact]
    public void ReplayReproducesEntirePostEntryStateIncludingCombatRngCursor()
    {
        var (state, environment, actionId) = CombatEntry("local-stream-exact");
        var originalHash = CanonicalJson.Sha256(state);
        var seed = "0000000000000000000000000000002a";
        var expected = Candidate(state, environment, actionId, seed, 0);
        var frame = environment.Observe(expected);
        var conditioned = PrototypeConditionedCombatEntry.Sample(
            state, actionId, frame.ObservationHash,
            frame.LegalActions.Select(a => a.ActionId).ToArray(), seed, 1);

        Assert.Equal(1, conditioned.Candidates);
        Assert.Equal(
            CanonicalJson.Sha256(expected),
            CanonicalJson.Sha256(conditioned.State));
        Assert.Equal(originalHash, CanonicalJson.Sha256(state));
        Assert.Equal(
            CanonicalJson.Sha256(expected.Rng),
            CanonicalJson.Sha256(conditioned.State.Rng));
        Assert.Equal(frame.ObservationHash,
            environment.Observe(conditioned.State).ObservationHash);

        // A later turn consumes the accepted stream cursor, so subsequent
        // game transitions also agree, unlike a post-combat order-only splice.
        var endTurn = environment.Observe(expected).LegalActions
            .Single(a => a.Kind == "end_turn").ActionId;
        var nextExpected = environment.Step(expected, endTurn).State;
        var nextConditioned = environment.Step(conditioned.State, endTurn).State;
        Assert.Equal(
            CanonicalJson.Sha256(nextExpected),
            CanonicalJson.Sha256(nextConditioned));
    }

    [Fact]
    public void ExhaustionInvalidInputAndNonCombatActionFailClosed()
    {
        var (state, environment, actionId) = CombatEntry("local-stream-reject");
        var before = CanonicalJson.Sha256(state);
        var seed = "0000000000000000000000000000002a";
        var candidate = Candidate(state, environment, actionId, seed, 0);
        var legal = environment.Observe(candidate).LegalActions
            .Select(a => a.ActionId).ToArray();

        Assert.Throws<InvalidOperationException>(() =>
            PrototypeConditionedCombatEntry.Sample(
                state, actionId, "impossible-observation", legal, seed, 3));
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeConditionedCombatEntry.Sample(
                state, actionId, environment.Observe(candidate).ObservationHash,
                new[] { "not-a-legal-action" }, seed, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeConditionedCombatEntry.Sample(
                state, actionId, "valid-hash", legal, seed, 0));
        Assert.Throws<ArgumentException>(() =>
            PrototypeConditionedCombatEntry.Sample(
                state, actionId, "valid-hash", legal, "not-a-128-bit-seed", 1));
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeConditionedCombatEntry.Sample(
                state, "unknown-map-choice", "hash", legal, seed, 1));
        Assert.Equal(before, CanonicalJson.Sha256(state));
    }
}
