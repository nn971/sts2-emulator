using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledGeneratedCardV111Tests
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

    private static ControlledPlayableCombatV111 WithOrobicAcid(
        ControlledPlayableCombatV111 state)
    {
        var slots = (PotionInstance?[])state.Run.Player.PotionSlots.Clone();
        Assert.NotEmpty(slots);
        slots[0] = new PotionInstance(
            "proto.potion.orobic_acid", PrototypeJson.EmptyObject());
        var combat = state.Run.World!.Combat!;
        return state with
        {
            Run = state.Run with
            {
                Player = state.Run.Player with { PotionSlots = slots },
                World = state.Run.World with
                {
                    Combat = combat with
                    {
                        Potions = [new CombatPotionState(
                            0, "proto.potion.orobic_acid",
                            PrototypeJson.EmptyObject())]
                    }
                }
            }
        };
    }

    private static ControlledPlayableCombatV111 BeginGeneration(string seed)
    {
        var state = WithOrobicAcid(Prepared(seed));
        var action = new PrototypeAiEnvironment().Observe(state.Run)
            .LegalActions.Single(x => x.Kind == "use_potion");
        return ControlledPlayableCombatEngineV111.Step(state, action.ActionId);
    }

    [Fact]
    public void OrobicAcidExposesThreeLiveGeneratedCardsWithoutReplayingPriorEffects()
    {
        var before = WithOrobicAcid(Prepared("nature-card-generation"));
        var action = new PrototypeAiEnvironment().Observe(before.Run)
            .LegalActions.Single(x => x.Kind == "use_potion");
        var priorHash = before.Fingerprint;
        var pending = ControlledPlayableCombatEngineV111.Step(before, action.ActionId);
        Assert.Equal(priorHash, before.Fingerprint);
        Assert.Equal(before.Run.DecisionIndex + 1, pending.Run.DecisionIndex);
        Assert.Null(pending.Run.Player.PotionSlots[0]);
        var priorHp = pending.Run.Player.Hp;
        var originalCards = pending.Run.World!.Combat!.Cards.Length;
        var transcript = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var view = NatureBoundaryMetadataV111.Describe(pending);
            Assert.Equal("generated_card", view.Kind);
            Assert.NotNull(view.Pending);
            Assert.Equal("combat.generated_card.select", view.Pending!.SiteId);
            Assert.Equal("prototype.combat", view.Pending.StreamId);
            Assert.Equal("choose_index", view.Pending.OperationKind);
            Assert.Equal("UNVERIFIED", view.Pending.ProbabilityStatus);
            Assert.NotNull(view.Pending.CandidateCardIds);
            Assert.True(view.Pending.CandidateCardIds!.Length > 3);
            Assert.Equal(view.Pending.CandidateCardIds.Length, view.Pending.SupportCount);
            Assert.Equal(Enumerable.Range(0, view.Pending.SupportCount),
                view.Pending.OrderedCandidates);
            transcript.Add(view.Pending.EventId);
            Assert.Throws<InvalidOperationException>(() =>
                ControlledPlayableCombatEngineV111.Resolve(pending, "stale", 0));
            Assert.Throws<ArgumentException>(() =>
                ControlledPlayableCombatEngineV111.Resolve(pending,
                    pending.Fingerprint, view.Pending.SupportCount));
            var predecessor = pending.Fingerprint;
            pending = ControlledPlayableCombatEngineV111.Resolve(
                pending, predecessor, i);
            Assert.NotEqual(predecessor, pending.Fingerprint);
        }
        Assert.True(pending.AtPlayerBoundary);
        Assert.Equal(before.Run.DecisionIndex + 1, pending.Run.DecisionIndex);
        Assert.Equal(priorHp, pending.Run.Player.Hp);
        Assert.Equal(originalCards + 3, pending.Run.World!.Combat!.Cards.Length);
        Assert.Equal(3, pending.GeneratedCardTranscript.Length);
        Assert.Equal(3, pending.LiveTranscript.Length);
        Assert.NotEmpty(new PrototypeAiEnvironment().Observe(pending.Run).LegalActions);
        Assert.Equal(priorHash, before.Fingerprint);
        var traced = NatureBoundaryMetadataV111.Describe(pending).Transcript;
        Assert.Equal(transcript, traced.TakeLast(3).Select(x => x.EventId));
    }

    [Fact]
    public void DistinctEligibleGenerationChoicesForkAndKeepSourceUnchanged()
    {
        var pending = BeginGeneration("nature-gen-forks");
        var view = NatureBoundaryMetadataV111.Describe(pending);
        int upper = view.Pending!.SupportCount - 1;
        Assert.True(upper >= 1);
        var original = pending.Fingerprint;
        var left = ControlledPlayableCombatEngineV111.Resolve(pending, original, 0);
        var right = ControlledPlayableCombatEngineV111.Resolve(pending, original, upper);
        Assert.NotEqual(left.Fingerprint, right.Fingerprint);
        Assert.Equal(original, pending.Fingerprint);
        Assert.Single(left.GeneratedCardTranscript);
        Assert.Single(right.GeneratedCardTranscript);
        Assert.NotEqual(left.Run.World!.Combat!.Cards.Last().CardId,
            right.Run.World!.Combat!.Cards.Last().CardId);
    }

    [Fact]
    public void ForgedGeneratedCardPoolFailsStructuralValidation()
    {
        var pending = BeginGeneration("nature-gen-tamper");
        var forged = pending.Fork();
        forged.Run.World!.Combat!.PendingChoice!.ControlledGeneration!
            .CandidateCardIds[0] = "proto.card.not_a_real_card";
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.VerifyStage(forged));
        Assert.Equal("generated_card",
            NatureBoundaryMetadataV111.Describe(pending).Kind);
    }

    [Fact]
    public void FifteenWorkersHaveIndependentGeneratedCardSupportAndEventIds()
    {
        static string Run(int worker)
        {
            var s = BeginGeneration("nature-gen-15-" + worker);
            var events = new List<string>();
            while (!s.AtPlayerBoundary)
            {
                var view = NatureBoundaryMetadataV111.Describe(s);
                Assert.Equal("generated_card", view.Kind);
                events.Add(view.Pending!.EventId);
                s = ControlledPlayableCombatEngineV111.Resolve(
                    s, s.Fingerprint, worker % view.Pending.SupportCount);
            }
            return s.Fingerprint + ":" + string.Join("|", events);
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, worker => parallel[worker] = Run(worker));
        Assert.Equal(serial, parallel);
    }
}
