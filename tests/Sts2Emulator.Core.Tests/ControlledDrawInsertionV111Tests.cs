using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledDrawInsertionV111Tests
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
        var state = ControlledPlayableCombatEngineV111.Begin(intents, seed);
        while (!state.AtPlayerBoundary)
            state = ControlledPlayableCombatEngineV111.Resolve(
                state, state.Fingerprint, 0);
        return state;
    }

    private static ControlledPlayableCombatV111 Start(
        string seed, PrototypeQueuedOperation operation)
    {
        var state = Prepared(seed);
        RunState run;
        using (ControlledChanceGuardV111.Enter())
            run = PrototypeGameEngine.BeginControlledDrawInsertionFixture(
                state.Run.Fork(), operation);
        return state with { Run = run };
    }

    [Fact]
    public void FixedCardInsertionPreservesSourceCardIdsAndBindsToChosenPosition()
    {
        var source = Prepared("nature-draw-insert-fixed");
        var originalDraw = source.Run.World!.Combat!.DrawPile;
        var state = Start("nature-draw-insert-fixed",
            new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
                2, CardId: "proto.silent.shiv"));
        var oldNextId = state.Run.World!.Combat!.NextCardInstanceId;
        var startHp = state.Run.Player.Hp;
        var startIndex = state.Run.DecisionIndex;
        for (var n = 0; n < 2; n++)
        {
            var view = NatureBoundaryMetadataV111.Describe(state);
            Assert.Equal("draw_insertion", view.Kind);
            Assert.Equal("combat.draw_pile.random_insert_index",
                view.Pending!.SiteId);
            Assert.Equal("prototype.combat", view.Pending.StreamId);
            Assert.Equal("bounded_integer", view.Pending.OperationKind);
            Assert.Equal(state.Run.World.Combat!.DrawPile.Length + 1,
                view.Pending.SupportCount);
            Assert.Null(view.Pending.CandidateCardIds);
            Assert.Equal("UNVERIFIED", view.Pending.ProbabilityStatus);
            var original = state.Fingerprint;
            Assert.Throws<InvalidOperationException>(() =>
                ControlledPlayableCombatEngineV111.Resolve(
                    state, "stale", 0));
            Assert.Throws<ArgumentException>(() =>
                ControlledPlayableCombatEngineV111.Resolve(
                    state, original, view.Pending.SupportCount));
            var index = n == 0 ? 0 : view.Pending.SupportCount - 1;
            state = ControlledPlayableCombatEngineV111.Resolve(
                state, original, index);
            Assert.Equal(oldNextId + n + 1,
                state.Run.World!.Combat!.NextCardInstanceId);
            Assert.Equal(startHp, state.Run.Player.Hp);
            Assert.Equal(startIndex, state.Run.DecisionIndex);
        }
        Assert.True(state.AtPlayerBoundary);
        Assert.Equal(oldNextId, state.Run.World!.Combat!.DrawPile[0]);
        Assert.Equal(oldNextId + 1, state.Run.World.Combat.DrawPile[^1]);
        Assert.Equal(2, state.DrawInsertionTranscript.Length);
        Assert.Equal(2, state.LiveTranscript.Length);
        Assert.Equal(originalDraw.Length + 2,
            state.Run.World.Combat.DrawPile.Length);
        Assert.Equal(originalDraw, state.Run.World.Combat.DrawPile.Skip(1)
            .SkipLast(1));
        Assert.Equal("proto.silent.shiv",
            state.Run.World.Combat.Cards.Last().CardId);
    }

    [Fact]
    public void RandomAttackGenerationHasTwoDistinctNatureStagesPerCard()
    {
        var state = Start("nature-random-attack-insert",
            new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile, 2));
        var before = state.Run.World!.Combat!;
        var view = NatureBoundaryMetadataV111.Describe(state);
        Assert.Equal("draw_insertion", view.Kind);
        Assert.Equal("combat.random_draw_attack.card_model", view.Pending!.SiteId);
        Assert.Equal("choose_index", view.Pending.OperationKind);
        Assert.True(view.Pending.SupportCount > 1);
        Assert.Equal(view.Pending.SupportCount, view.Pending.CandidateCardIds!.Length);
        var selectedModel = view.Pending.CandidateCardIds[1];
        var originalFingerprint = state.Fingerprint;
        var selected = ControlledPlayableCombatEngineV111.Resolve(
            state, originalFingerprint, 1);
        Assert.Equal(before.Cards.Length, selected.Run.World!.Combat!.Cards.Length);
        Assert.Equal(before.NextCardInstanceId,
            selected.Run.World.Combat.NextCardInstanceId);
        Assert.Equal(before.DrawPile, selected.Run.World.Combat.DrawPile);
        var position = NatureBoundaryMetadataV111.Describe(selected);
        Assert.Equal("combat.draw_pile.random_insert_index",
            position.Pending!.SiteId);
        Assert.Equal("bounded_integer", position.Pending.OperationKind);
        Assert.Equal(before.DrawPile.Length + 1, position.Pending.SupportCount);
        state = ControlledPlayableCombatEngineV111.Resolve(
            selected, selected.Fingerprint, before.DrawPile.Length);
        Assert.Equal(selectedModel, state.Run.World!.Combat!.Cards.Last().CardId);
        Assert.Equal(0, state.Run.World.Combat.Cards.Last().TemporaryEnergyCost!.Cost);
        Assert.Equal("combat.random_draw_attack.card_model",
            NatureBoundaryMetadataV111.Describe(state).Pending!.SiteId);
        for (var i = 0; i < 2; i++)
            state = ControlledPlayableCombatEngineV111.Resolve(
                state, state.Fingerprint, 0);
        Assert.True(state.AtPlayerBoundary);
        Assert.Equal(4, state.DrawInsertionTranscript.Length);
        Assert.Equal(4, state.LiveTranscript.Length);
        Assert.Equal(before.Cards.Length + 2, state.Run.World!.Combat!.Cards.Length);
        Assert.Throws<InvalidOperationException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(
                selected, originalFingerprint, 0));
    }

    [Fact]
    public void TamperedDrawPileAndCardPoolAreRejected()
    {
        var fixedStage = Start("nature-insert-tampered",
            new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
                1, CardId: "proto.silent.shiv"));
        var mutated = fixedStage.Fork();
        var mutatedChoice = mutated.Run.World!.Combat!.PendingChoice!;
        var insertion = mutatedChoice.ControlledDrawInsertion!;
        var eligibleDrawPile = insertion.EligibleDrawPile;
        eligibleDrawPile[0] = -9;
        mutated = ControlledPlayableTestSupport.WithPendingChoice(
            mutated, mutatedChoice with
            {
                ControlledDrawInsertion = insertion with
                {
                    EligibleDrawPile = eligibleDrawPile
                }
            });
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.VerifyStage(mutated));
        var modelStage = Start("nature-model-tampered",
            new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile, 1));
        var corrupted = modelStage.Fork();
        var corruptedChoice = corrupted.Run.World!.Combat!.PendingChoice!;
        var corruptedInsertion = corruptedChoice.ControlledDrawInsertion!;
        var candidateIds = corruptedInsertion.CandidateCardIds;
        candidateIds[0] = "missing.card";
        corrupted = ControlledPlayableTestSupport.WithPendingChoice(
            corrupted, corruptedChoice with
            {
                ControlledDrawInsertion = corruptedInsertion with
                {
                    CandidateCardIds = candidateIds
                }
            });
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.VerifyStage(corrupted));
    }

    [Fact]
    public void FifteenWorkersHaveScheduleIndependentDrawInsertion()
    {
        static string Run(int worker)
        {
            var state = Start("nature-insertion-workers-" + worker,
                new PrototypeQueuedOperation(
                    PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile,
                    2));
            var ids = new List<string>();
            while (!state.AtPlayerBoundary)
            {
                var view = NatureBoundaryMetadataV111.Describe(state);
                ids.Add(view.Pending!.EventId);
                state = ControlledPlayableCombatEngineV111.Resolve(
                    state, state.Fingerprint,
                    worker % view.Pending.SupportCount);
            }
            return state.Fingerprint + ":" + string.Join("|", ids);
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Run(i));
        Assert.Equal(serial, parallel);
    }
}
