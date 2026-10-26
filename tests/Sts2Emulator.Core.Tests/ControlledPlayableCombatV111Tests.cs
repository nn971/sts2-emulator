using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledPlayableCombatV111Tests
{
    private static ControlledIntentStateV111 Completed(string seed)
    {
        var pre = PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
            seed, 2, PrototypeNativeHiveNormals.MytesNormalId,
            "MytesNormal", 21, mapColumn: 3, mapRow: 5);
        var hp = ControlledInitialHpV111.Begin(
            pre.Combat, pre.Formation, pre.NativeStreams);
        while (!hp.Complete)
            hp = ControlledInitialHpV111.Resolve(hp, hp.Fingerprint, 0);
        var intents = ControlledIntentsV111.BeginAfterControlledHp(hp);
        while (!intents.Complete)
            intents = ControlledIntentsV111.Resolve(
                intents, intents.Fingerprint, 0);
        return intents;
    }

    private static ControlledPlayableCombatV111 CompleteShuffle(
        ControlledPlayableCombatV111 pending)
    {
        while (!pending.AtPlayerBoundary)
            pending = ControlledPlayableCombatEngineV111.Resolve(
                pending, pending.Fingerprint, 0);
        return pending;
    }

    [Fact]
    public void ControlledHpIntentsFlowIntoActualPlayableCombatWithFairActions()
    {
        const string seed = "nature-143-playable";
        var source = Completed(seed);
        var first = ControlledPlayableCombatEngineV111.Begin(source, seed);
        Assert.NotNull(first.Pending);
        Assert.Equal(ControlledPlayableCombatEngineV111.ShuffleSite,
            first.Pending!.SiteId);
        Assert.Equal("run.shuffle", first.Pending.StreamId);
        Assert.Equal("bounded_integer", first.Pending.OperationKind);
        Assert.Null(first.Run.World!.Combat!.NativeRng);
        Assert.Equal(source.Combat.Enemies.Select(x =>
                (x.Hp, x.MaxHp, x.PlannedMoveIndex, x.PlannedNextAiStateId)),
            first.Run.World.Combat.Enemies.Select(x =>
                (x.Hp, x.MaxHp, x.PlannedMoveIndex, x.PlannedNextAiStateId)));
        var before = first.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(first, "stale", 0));
        Assert.Throws<ArgumentException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(
                first, before, first.Pending.SupportCount));
        Assert.Equal(before, first.Fingerprint);

        var player = CompleteShuffle(first);
        Assert.True(player.AtPlayerBoundary);
        Assert.Equal(RunPhase.Combat, player.Run.Phase);
        Assert.Equal(5, player.Run.World!.Combat!.Hand.Length);
        Assert.Equal(player.Run.Player.Deck.Length,
            player.Run.World.Combat.Cards.Length);
        var environment = new PrototypeAiEnvironment();
        var frame = environment.Observe(player.Run);
        Assert.NotEmpty(frame.LegalActions);
        Assert.Equal(PrototypeAiEnvironment.SchemaId, frame.SchemaId);
        Assert.Equal(player.Run.Player.Hp, frame.Observation.Hp);
        Assert.Equal(player.Run.World.Combat.Hand.Length,
            frame.Observation.Combat!.Hand.Length);
        var trace = NatureBoundaryMetadataV111.Describe(player);
        Assert.Equal(source.PriorHpTranscript!.Length + source.Transcript.Length +
            first.Run.World.Combat.DrawPile.Length - 1, trace.Transcript.Length);
        Assert.Null(trace.Pending);
    }

    [Fact]
    public void DeterministicPlayerActionsPreserveSourceIntentsAndRandomActionsFailClosed()
    {
        const string seed = "nature-143-action";
        var entry = CompleteShuffle(
            ControlledPlayableCombatEngineV111.Begin(Completed(seed), seed));
        var frame = new PrototypeAiEnvironment().Observe(entry.Run);
        var candidate = frame.LegalActions.FirstOrDefault(x =>
            x.Kind == "play_card");
        Assert.NotNull(candidate);
        var before = entry.Fingerprint;
        var next = ControlledPlayableCombatEngineV111.Step(
            entry, candidate!.ActionId);
        Assert.Equal(before, entry.Fingerprint);
        Assert.Equal(entry.PlayerActions.Length + 1, next.PlayerActions.Length);
        Assert.Equal(entry.Run.DecisionIndex + 1, next.Run.DecisionIndex);
        Assert.NotEqual(before, next.Fingerprint);
        var restored = ControlledPlayableCombatEngineV111.Restore(
            ControlledPlayableCombatEngineV111.Save(next));
        Assert.Equal(next.Fingerprint, restored.Fingerprint);

        // End-turn is allowed when every invoked mechanic is deterministic.
        // Fail-closed behavior is tested at the two RNG roots, which any
        // unmigrated random action would eventually reach.
        using (ControlledChanceGuardV111.Enter())
        {
            var proto = Assert.Throws<NotSupportedException>(() =>
                PrototypeRng.NextInt(entry.Run.Rng, "combat", 2));
            Assert.Contains("UNSUPPORTED_CONTROLLED_CHANCE", proto.Message);
            Assert.Contains("site=prototype_rng.combat", proto.Message);
            var native = Assert.Throws<NotSupportedException>(() =>
                NativeRngV111.NextInt(NativeRngV111.CreateBundle(seed), "run.niche", 2));
            Assert.Contains("site=native_rng.run.niche", native.Message);
        }
        Assert.Equal(before, entry.Fingerprint);
    }

    [Fact]
    public void SaveRestoreRejectsTamperingAndForksRemainIndependent()
    {
        const string seed = "nature-143-snapshot";
        var start = ControlledPlayableCombatEngineV111.Begin(Completed(seed), seed);
        var original = start.Fingerprint;
        var altered = start.Fork();
        var alteredRun = altered.Run;
        var alteredWorld = alteredRun.World!;
        var alteredCombat = alteredWorld.Combat!;
        var alteredDrawPile = alteredCombat.DrawPile;
        alteredDrawPile[0] = 123456789;
        altered = altered with
        {
            Run = alteredRun with
            {
                World = alteredWorld with
                {
                    Combat = alteredCombat with
                    {
                        DrawPile = alteredDrawPile
                    }
                }
            }
        };
        Assert.Throws<InvalidOperationException>(() =>
            ControlledPlayableCombatEngineV111.Restore(JsonSerializer.Serialize(altered)));
        Assert.Equal(original, start.Fingerprint);
        var a = ControlledPlayableCombatEngineV111.Resolve(
            start, start.Fingerprint, 0);
        var b = ControlledPlayableCombatEngineV111.Resolve(
            start, start.Fingerprint, start.Pending!.SupportCount - 1);
        Assert.NotEqual(a.Fingerprint, b.Fingerprint);
        Assert.Equal(a.Fingerprint,
            ControlledPlayableCombatEngineV111.Restore(
                ControlledPlayableCombatEngineV111.Save(a)).Fingerprint);
        Assert.Equal(original, start.Fingerprint);
    }

    [Fact]
    public void FifteenIndependentControlledEntrypointsAreScheduleDeterministic()
    {
        static string Run(int i)
        {
            string seed = "nature-playable-worker-" + i;
            var state = CompleteShuffle(
                ControlledPlayableCombatEngineV111.Begin(Completed(seed), seed));
            return state.Fingerprint;
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var concurrent = new string[15];
        Parallel.For(0, 15, i => concurrent[i] = Run(i));
        Assert.Equal(serial, concurrent);
    }
}

internal static class ControlledPlayableTestSupport
{
    public static ControlledPlayableCombatV111 WithPendingChoice(
        ControlledPlayableCombatV111 state,
        PendingCombatChoiceState pendingChoice)
    {
        var run = state.Run;
        var world = run.World!;
        var combat = world.Combat!;
        return state with
        {
            Run = run with
            {
                World = world with
                {
                    Combat = combat with { PendingChoice = pendingChoice }
                }
            }
        };
    }
}
