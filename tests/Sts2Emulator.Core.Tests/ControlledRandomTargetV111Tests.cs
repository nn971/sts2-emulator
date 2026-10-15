using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledRandomTargetV111Tests
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
            intents = ControlledIntentsV111.Resolve(intents, intents.Fingerprint, 0);
        var playable = ControlledPlayableCombatEngineV111.Begin(intents, seed);
        while (!playable.AtPlayerBoundary)
            playable = ControlledPlayableCombatEngineV111.Resolve(
                playable, playable.Fingerprint, 0);
        return playable;
    }

    private static ControlledPlayableCombatV111 WithFirstCard(
        ControlledPlayableCombatV111 state, string cardId,
        bool sly = false)
    {
        var combat = state.Run.World!.Combat!;
        long first = combat.Hand[0];
        var cards = combat.Cards.Select(card =>
            card.InstanceId == first ? card with
            {
                CardId = cardId,
                KeywordOverrides = sly
                    ? [new PrototypeCardKeywordOverride(
                        PrototypeCardKeyword.Sly, true,
                        PrototypeCardKeywordOverrideExpiry.None)]
                    : card.KeywordOverrides
            } : card).ToArray();
        return state with
        {
            Run = state.Run with
            {
                World = state.Run.World with
                {
                    Combat = combat with { Cards = cards }
                }
            }
        };
    }

    [Fact]
    public void RicochetSuspendsInRealCardEffectAndUsesNatureTarget()
    {
        var state = WithFirstCard(Prepared("target-ricochet"), "proto.silent.ricochet");
        var environment = new PrototypeAiEnvironment();
        var handId = state.Run.World!.Combat!.Hand[0];
        var action = environment.Observe(state.Run).LegalActions
            .First(a => a.Kind == "play_card" &&
                a.Payload.TryGetProperty("CardInstanceId", out var id) &&
                id.GetInt64() == handId);
        var root = state.Fingerprint;
        var pending = ControlledPlayableCombatEngineV111.Step(state, action.ActionId);
        var view = NatureBoundaryMetadataV111.Describe(pending);
        Assert.Equal("random_target", view.Kind);
        Assert.Equal("combat.effect.random_enemy", view.Pending!.SiteId);
        Assert.Equal("run.combat_targets", view.Pending.StreamId);
        Assert.True(view.Pending.SupportCount > 1);
        Assert.Equal(state.Run.DecisionIndex + 1, pending.Run.DecisionIndex);
        Assert.Equal(root, state.Fingerprint);
        Assert.Null(view.Pending.ReferenceLawId);
        Assert.Equal("UNVERIFIED", view.Pending.ProbabilityStatus);
        Assert.Throws<InvalidOperationException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(pending, "stale", 0));
        Assert.Throws<ArgumentException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(
                pending, pending.Fingerprint, view.Pending.SupportCount));
        var initialEvent = view.Pending.EventId;
        var first = ControlledPlayableCombatEngineV111.Resolve(
            pending, pending.Fingerprint, 0);
        Assert.Equal(initialEvent,
            NatureBoundaryMetadataV111.Describe(first).Transcript[^1].EventId);
        Assert.Equal(pending.Run.DecisionIndex, first.Run.DecisionIndex);
        for (int n = 0; n < 20 && !first.AtPlayerBoundary; n++)
            first = ControlledPlayableCombatEngineV111.Resolve(
                first, first.Fingerprint, 0);
        Assert.True(first.AtPlayerBoundary);
        Assert.True(first.TargetTranscript.Length >= 1);
        Assert.Equal(state.Run.World.Combat.Enemies.Length,
            first.Run.World!.Combat!.Enemies.Length);
    }

    [Fact]
    public void SlyDiscardAutoPlayHasControlledChoiceAndRecordsSelectedTarget()
    {
        var source = WithFirstCard(Prepared("target-sly"), "proto.silent.strike", true);
        long cardId = source.Run.World!.Combat!.Hand[0];
        var end = new PrototypeAiEnvironment().Observe(source.Run).LegalActions
            .Single(a => a.Kind == "end_turn");
        var pending = ControlledPlayableCombatEngineV111.Step(source, end.ActionId);
        var view = NatureBoundaryMetadataV111.Describe(pending);
        Assert.Equal("random_target", view.Kind);
        Assert.Equal("combat.sly_autoplay.random_enemy", view.Pending!.SiteId);
        Assert.Equal("card:" + cardId, view.Pending.OwnerId);
        Assert.True(view.Pending.SupportCount > 1);
        var original = pending.Fingerprint;
        var left = ControlledPlayableCombatEngineV111.Resolve(
            pending, original, 0);
        var right = ControlledPlayableCombatEngineV111.Resolve(
            pending, original, view.Pending.SupportCount - 1);
        Assert.NotEqual(left.Fingerprint, right.Fingerprint);
        Assert.Equal(original, pending.Fingerprint);
        Assert.Single(left.Run.World!.Combat!.AutoPlayTargetRolls!);
        Assert.Single(right.Run.World!.Combat!.AutoPlayTargetRolls!);
        Assert.Equal(cardId, left.Run.World.Combat.AutoPlayTargetRolls![0].CardInstanceId);
        Assert.NotEqual(
            left.Run.World!.Combat!.AutoPlayTargetRolls![0].TargetEnemyInstanceId,
            right.Run.World!.Combat!.AutoPlayTargetRolls![0].TargetEnemyInstanceId);
        Assert.Equal(source.Run.DecisionIndex + 1, left.Run.DecisionIndex);
    }

    [Fact]
    public void SavedTargetChoiceRejectsForgedSupportWithoutPublishingChanges()
    {
        var source = WithFirstCard(Prepared("target-snapshot"), "proto.silent.ricochet");
        var action = new PrototypeAiEnvironment().Observe(source.Run)
            .LegalActions.First(x => x.Kind == "play_card");
        var pending = ControlledPlayableCombatEngineV111.Step(source, action.ActionId);
        Assert.NotNull(pending.TargetPending);
        var save = JsonSerializer.Serialize(pending);
        var copied = JsonSerializer.Deserialize<ControlledPlayableCombatV111>(save)!;
        var target = copied.Run.World!.Combat!.PendingChoice!.ControlledTarget!;
        target.EligibleEnemyInstanceIds[0] = int.MaxValue;
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.VerifyStage(copied));
        Assert.NotNull(NatureBoundaryMetadataV111.Describe(pending).Pending);
    }

    [Fact]
    public void FifteenWorkerRandomEffectChoicesAreScheduleIndependent()
    {
        static string Run(int n)
        {
            var s = WithFirstCard(Prepared("target-workers-" + n),
                "proto.silent.ricochet");
            var act = new PrototypeAiEnvironment().Observe(s.Run)
                .LegalActions.First(x => x.Kind == "play_card");
            s = ControlledPlayableCombatEngineV111.Step(s, act.ActionId);
            for (int k = 0; k < 20 && !s.AtPlayerBoundary; k++)
                s = ControlledPlayableCombatEngineV111.Resolve(
                    s, s.Fingerprint, n % s.ActivePending!.SupportCount);
            return s.Fingerprint;
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, n => parallel[n] = Run(n));
        Assert.Equal(serial, parallel);
    }
}
