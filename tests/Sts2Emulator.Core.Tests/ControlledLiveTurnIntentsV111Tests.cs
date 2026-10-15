using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledLiveTurnIntentsV111Tests
{
    private static ControlledPlayableCombatV111 Prepared(string seed)
    {
        var pre = PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
            seed, 2, PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", 20, mapColumn: 3, mapRow: 5);
        var hp = ControlledInitialHpV111.Begin(
            pre.Combat, pre.Formation, pre.NativeStreams);
        while (!hp.Complete)
            hp = ControlledInitialHpV111.Resolve(hp, hp.Fingerprint, 0);
        var intents = ControlledIntentsV111.BeginAfterControlledHp(hp);
        while (!intents.Complete)
            intents = ControlledIntentsV111.Resolve(
                intents, intents.Fingerprint, 0);
        var playable = ControlledPlayableCombatEngineV111.Begin(intents, seed);
        while (!playable.AtPlayerBoundary)
            playable = ControlledPlayableCombatEngineV111.Resolve(
                playable, playable.Fingerprint, 0);
        return playable;
    }

    [Fact]
    public void EndTurnSuspendsBeforeNextIntentWithoutRepeatingEnemyEffects()
    {
        var initial = Prepared("nature-143-live-turn");
        var actionId = new PrototypeAiEnvironment().Observe(initial.Run)
            .LegalActions.Single(x => x.Kind == "end_turn").ActionId;
        var rootHash = initial.Fingerprint;
        var pending = ControlledPlayableCombatEngineV111.Step(initial, actionId);
        Assert.NotNull(pending.TurnIntents);
        Assert.NotNull(pending.ActivePending);
        Assert.Equal(ControlledTurnIntentEngineV111.SiteId,
            pending.ActivePending!.SiteId);
        Assert.Equal("run.monster_ai", pending.ActivePending.StreamId);
        Assert.False(pending.AtPlayerBoundary);
        Assert.Equal(initial.Run.DecisionIndex + 1, pending.Run.DecisionIndex);
        Assert.Equal(1, pending.Run.World!.Combat!.Turn);
        Assert.Equal(rootHash, initial.Fingerprint);
        var postEnemyHp = pending.Run.Player.Hp;
        var postEnemyBlock = pending.Run.World.Combat.PlayerBlock;
        var original = pending.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(pending, "stale", 0));
        Assert.Throws<ArgumentException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(pending,
                original, pending.ActivePending.SupportCount));
        Assert.Equal(original, pending.Fingerprint);

        var choice = NatureBoundaryMetadataV111.Describe(pending);
        var restored = ControlledPlayableCombatEngineV111.Restore(
            ControlledPlayableCombatEngineV111.Save(pending));
        Assert.Equal(pending.Fingerprint, restored.Fingerprint);
        Assert.Equal(choice.Pending!.EventId,
            NatureBoundaryMetadataV111.Describe(restored).Pending!.EventId);

        var branch = ControlledPlayableCombatEngineV111.Resolve(
            pending, pending.Fingerprint, 0);
        Assert.Equal(postEnemyHp, branch.Run.Player.Hp);
        Assert.Equal(postEnemyBlock, branch.Run.World!.Combat!.PlayerBlock);
        Assert.Equal(pending.Run.DecisionIndex, branch.Run.DecisionIndex);
        Assert.Equal(choice.Pending.EventId,
            NatureBoundaryMetadataV111.Describe(branch).Transcript[^1].EventId);
        while (!branch.AtPlayerBoundary)
            branch = ControlledPlayableCombatEngineV111.Resolve(
                branch, branch.Fingerprint, 0);
        Assert.Equal(2, branch.Run.World!.Combat!.Turn);
        Assert.True(branch.Run.World.Combat.IsPlayerTurn);
        Assert.NotEmpty(new PrototypeAiEnvironment().Observe(branch.Run).LegalActions);
        Assert.Equal(branch.Fingerprint,
            ControlledPlayableCombatEngineV111.Restore(
                ControlledPlayableCombatEngineV111.Save(branch)).Fingerprint);
        Assert.Equal(rootHash, initial.Fingerprint);
    }

    [Fact]
    public void DifferentLegalTicketsForkIndependently()
    {
        var initial = Prepared("nature-143-live-branches");
        var endTurn = new PrototypeAiEnvironment().Observe(initial.Run)
            .LegalActions.Single(x => x.Kind == "end_turn").ActionId;
        var pending = ControlledPlayableCombatEngineV111.Step(initial, endTurn);
        Assert.NotNull(pending.ActivePending);
        Assert.True(pending.ActivePending!.SupportCount > 1);
        var a = ControlledPlayableCombatEngineV111.Resolve(
            pending, pending.Fingerprint, 0);
        var b = ControlledPlayableCombatEngineV111.Resolve(
            pending, pending.Fingerprint, pending.ActivePending.SupportCount - 1);
        Assert.NotEqual(a.Fingerprint, b.Fingerprint);
        Assert.Equal(a.Fingerprint,
            ControlledPlayableCombatEngineV111.Restore(
                ControlledPlayableCombatEngineV111.Save(a)).Fingerprint);
        Assert.Equal(b.Fingerprint,
            ControlledPlayableCombatEngineV111.Restore(
                ControlledPlayableCombatEngineV111.Save(b)).Fingerprint);
        Assert.Equal(pending.Fingerprint,
            ControlledPlayableCombatEngineV111.Restore(
                ControlledPlayableCombatEngineV111.Save(pending)).Fingerprint);
    }

    [Fact]
    public void FifteenWorkersRepeatTheSameTurnIntentEventSequence()
    {
        static string Run(int i)
        {
            var p = Prepared("nature-143-turn-worker-" + i);
            var end = new PrototypeAiEnvironment().Observe(p.Run)
                .LegalActions.Single(x => x.Kind == "end_turn").ActionId;
            p = ControlledPlayableCombatEngineV111.Step(p, end);
            var ids = new List<string>();
            while (!p.AtPlayerBoundary)
            {
                var view = NatureBoundaryMetadataV111.Describe(p);
                ids.Add(view.Pending!.EventId);
                p = ControlledPlayableCombatEngineV111.Resolve(
                    p, p.Fingerprint, i % view.Pending.SupportCount);
            }
            return p.Fingerprint + ":" + string.Join("|", ids);
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Run(i));
        Assert.Equal(serial, parallel);
    }
}
