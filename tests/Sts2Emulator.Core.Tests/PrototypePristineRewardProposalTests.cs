using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePristineRewardProposalTests
{
    private static Dictionary<string, string> Fixture() => new(StringComparer.Ordinal)
    {
        ["map"] = "000000000000000b",
        ["combat"] = "000000000000001d",
        ["combat_targets"] = "0000000000000005",
        ["reward"] = "0000000000000007",
        ["shop"] = "0000000000000009",
        ["event"] = "000000000000000d"
    };

    private static (RunState Before, RunState After, string ActionId)
        FindFirstReward()
    {
        var environment = new PrototypeAiEnvironment();
        var state = PrototypeFactorizedRunFactory.Create(Fixture());
        for (var i = 0; i < 120; i++)
        {
            var legal = environment.Observe(state).LegalActions;
            var chosen = state.Phase switch
            {
                RunPhase.RunStart => legal.Single(a => a.Kind == "start_run"),
                RunPhase.MapChoice => legal.First(a => a.Kind == "choose_map_node"),
                RunPhase.Combat => legal.FirstOrDefault(a => a.Kind == "play_card")
                    ?? legal.FirstOrDefault(a => a.Kind == "end_turn")
                    ?? legal[0],
                _ => legal[0]
            };
            var after = environment.Step(state, chosen.ActionId).State;
            if (state.Phase == RunPhase.Combat && after.Phase == RunPhase.Reward)
            {
                return (state, after, chosen.ActionId);
            }
            state = after;
        }
        throw new InvalidOperationException("Fixture did not reach a first reward.");
    }

    [Fact]
    public void FirstRewardIndependentStreamBranchesPreserveOtherCursors()
    {
        var (before, after, chosen) = FindFirstReward();
        var untouched = before.Rng.Streams.Single(s => s.StreamId == "reward");
        Assert.Equal(0L, untouched.CallCount);
        var originalHash = CanonicalJson.Sha256(before);

        var replay = PrototypePristineRewardProposal.Branch(
            before, chosen, "0000000000000007");
        Assert.Equal(CanonicalJson.Sha256(after), CanonicalJson.Sha256(replay));
        Assert.Equal(originalHash, CanonicalJson.Sha256(before));

        var newReward = PrototypePristineRewardProposal.Branch(
            before, chosen, "0000000000000008");
        Assert.Equal(RunPhase.Reward, newReward.Phase);
        Assert.Equal(
            after.Rng.Streams.Where(s => s.StreamId != "reward")
                .Select(s => (s.StreamId, CanonicalJson.Sha256(s))),
            newReward.Rng.Streams.Where(s => s.StreamId != "reward")
                .Select(s => (s.StreamId, CanonicalJson.Sha256(s))));
        Assert.Equal(originalHash, CanonicalJson.Sha256(before));

        Assert.Throws<InvalidOperationException>(() =>
            PrototypePristineRewardProposal.Branch(
                after, chosen, "0000000000000009"));
        Assert.Throws<ArgumentException>(() =>
            PrototypePristineRewardProposal.Branch(before, chosen, "invalid"));
        Assert.Throws<InvalidOperationException>(() =>
            PrototypePristineRewardProposal.Branch(
                before, "not-a-legal-action", "0000000000000009"));
    }

    [Fact]
    public void PristineRewardRequiresExperimentalPriorAndRewardTransition()
    {
        var (before, _, chosen) = FindFirstReward();
        var ordinary = before with { RunSeed = "ordinary-one-seed-prototype" };
        Assert.Throws<InvalidOperationException>(() =>
            PrototypePristineRewardProposal.Branch(
                ordinary, chosen, "0000000000000001"));

        var consumed = before.Fork();
        var index = Array.FindIndex(consumed.Rng.Streams, s => s.StreamId == "reward");
        consumed.Rng.Streams[index] = consumed.Rng.Streams[index] with { CallCount = 1 };
        Assert.Throws<InvalidOperationException>(() =>
            PrototypePristineRewardProposal.Branch(
                consumed, chosen, "0000000000000001"));

        var root = PrototypeFactorizedRunFactory.Create(Fixture());
        var firstAction = new PrototypeAiEnvironment().Observe(root).LegalActions[0];
        Assert.Throws<InvalidOperationException>(() =>
            PrototypePristineRewardProposal.Branch(
                root, firstAction.ActionId, "0000000000000001"));
    }
}
