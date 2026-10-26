using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledPoolShuffleV111Tests
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

    private static ControlledPlayableCombatV111 WithPotion(
        ControlledPlayableCombatV111 state, string potion)
    {
        var slots = (PotionInstance?[])state.Run.Player.PotionSlots.Clone();
        slots[0] = new PotionInstance(potion, PrototypeJson.EmptyObject());
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
                            0, potion, PrototypeJson.EmptyObject())]
                    }
                }
            }
        };
    }

    private static ControlledPlayableCombatV111 BeginPotion(
        string seed, string potion)
    {
        var state = WithPotion(Prepared(seed), potion);
        var action = new PrototypeAiEnvironment().Observe(state.Run)
            .LegalActions.Single(x => x.Kind == "use_potion");
        return ControlledPlayableCombatEngineV111.Step(state, action.ActionId);
    }

    private static ControlledPlayableCombatV111 Drain(
        ControlledPlayableCombatV111 state, int index = 0)
    {
        for (int i = 0; i < 500 && state.Run.World?.Combat?.PendingChoice?
                 .ControlledPoolShuffle is not null; i++)
        {
            var view = NatureBoundaryMetadataV111.Describe(state);
            Assert.Equal("generation_pool_shuffle", view.Kind);
            Assert.NotNull(view.Pending);
            state = ControlledPlayableCombatEngineV111.Resolve(
                state, state.Fingerprint, index % view.Pending!.SupportCount);
        }
        Assert.Null(state.Run.World?.Combat?.PendingChoice?
            .ControlledPoolShuffle);
        return state;
    }

    [Fact]
    public void AttackPotionShufflesFullPoolBeforeOfferingOnlyThreeChoices()
    {
        var pending = BeginPotion("nature-pool-attack-potion", "proto.potion.attack");
        var source = pending.Run.World!.Combat!;
        var stage = source.PendingChoice!.ControlledPoolShuffle!;
        var initial = (string[])stage.OriginalCardIds.Clone();
        Assert.True(initial.Length > 3);
        var initialHash = pending.Fingerprint;
        var first = NatureBoundaryMetadataV111.Describe(pending);
        Assert.Equal("generation_pool_shuffle", first.Kind);
        Assert.Equal("combat.generation.full_pool_shuffle.swap_index",
            first.Pending!.SiteId);
        Assert.Equal("prototype.combat", first.Pending.StreamId);
        Assert.Equal("bounded_integer", first.Pending.OperationKind);
        Assert.Equal(initial.Length, first.Pending.SupportCount);
        Assert.Equal(initial, first.Pending.CandidateCardIds);
        Assert.Equal("UNVERIFIED", first.Pending.ProbabilityStatus);
        Assert.Empty(source.ChoiceCardIds);
        Assert.Throws<InvalidOperationException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(pending, "stale", 0));
        Assert.Throws<ArgumentException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(
                pending, pending.Fingerprint, first.Pending.SupportCount));
        var order = (string[])initial.Clone();
        int cursor = initial.Length - 1;
        while (pending.Run.World?.Combat?.PendingChoice?
                   .ControlledPoolShuffle is not null)
        {
            var view = NatureBoundaryMetadataV111.Describe(pending);
            Assert.Equal(cursor + 1, view.Pending!.SupportCount);
            var priorEvent = view.Pending.EventId;
            var next = ControlledPlayableCombatEngineV111.Resolve(
                pending, pending.Fingerprint, 0);
            (order[cursor], order[0]) = (order[0], order[cursor]);
            cursor--;
            Assert.Equal(priorEvent,
                NatureBoundaryMetadataV111.Describe(next).Transcript[^1].EventId);
            pending = next;
        }
        Assert.Equal(0, cursor);
        Assert.Equal(initial.Length - 1, pending.PoolShuffleTranscript.Length);
        Assert.Throws<InvalidOperationException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(pending, initialHash, 0));
        Assert.Equal(order.Take(3),
            pending.Run.World!.Combat!.ChoiceCardIds.Select(id =>
                pending.Run.World.Combat.Cards.Single(c => c.InstanceId == id).CardId));
    }

    [Fact]
    public void ColorlessPotionPreservesChoiceSelectionAndTemporaryCost()
    {
        var pending = BeginPotion("nature-pool-colorless", "proto.potion.colorless");
        var pool = pending.Run.World!.Combat!.PendingChoice!.ControlledPoolShuffle!;
        var ordered = (string[])pool.OriginalCardIds.Clone();
        pending = Drain(pending, 0);
        var combat = pending.Run.World!.Combat!;
        Assert.Equal(3, combat.ChoiceCardIds.Length);
        var expected = (string[])ordered.Clone();
        for (var i = expected.Length - 1; i > 0; i--)
            (expected[i], expected[0]) = (expected[0], expected[i]);
        Assert.Equal(expected.Take(3), combat.ChoiceCardIds.Select(id =>
            combat.Cards.Single(card => card.InstanceId == id).CardId));
        Assert.All(combat.ChoiceCardIds, id =>
            Assert.Equal(0, combat.Cards.Single(card => card.InstanceId == id)
                .TemporaryEnergyCost!.Cost));
        var action = new PrototypeAiEnvironment().Observe(pending.Run)
            .LegalActions.First(x => x.Kind == "select_cards");
        var after = ControlledPlayableCombatEngineV111.Step(pending, action.ActionId);
        Assert.Null(after.Run.World!.Combat!.PendingChoice);
        Assert.Empty(after.Run.World.Combat.ChoiceCardIds);
    }

    [Fact]
    public void JackOfAllTradesResumesTheActualColorlessCardPlay()
    {
        var before = Prepared("nature-pool-jack");
        var combat = before.Run.World!.Combat!;
        var sourceId = combat.Hand[0];
        var cardId = "proto.colorless.jack_of_all_trades";
        before = before with
        {
            Run = before.Run with
            {
                Player = before.Run.Player with
                {
                    Deck = before.Run.Player.Deck.Select(card =>
                        card.InstanceId == sourceId
                            ? card with { CardId = cardId }
                            : card).ToArray()
                },
                World = before.Run.World with
                {
                    Combat = combat with
                    {
                        Cards = combat.Cards.Select(card =>
                            card.InstanceId == sourceId
                                ? card with { CardId = cardId } : card).ToArray()
                    }
                }
            }
        };
        var play = new PrototypeAiEnvironment().Observe(before.Run)
            .LegalActions.Single(action => action.Kind == "play_card" &&
                action.Payload.GetProperty("CardInstanceId").GetInt64() == sourceId);
        var pending = ControlledPlayableCombatEngineV111.Step(
            before, play.ActionId);
        var stage = pending.Run.World!.Combat!.PendingChoice!
            .ControlledPoolShuffle!;
        Assert.DoesNotContain(cardId, stage.OriginalCardIds);
        Assert.True(stage.OriginalCardIds.Length > 1);
        var sourceCount = pending.Run.World.Combat.Cards.Length;
        var after = Drain(pending);
        Assert.Equal(sourceCount + 1, after.Run.World!.Combat!.Cards.Length);
        Assert.Contains(after.Run.World.Combat.Cards.Last().CardId,
            stage.OriginalCardIds);
        Assert.Equal(before.Run.DecisionIndex + 1, after.Run.DecisionIndex);
        Assert.NotEmpty(new PrototypeAiEnvironment().Observe(after.Run).LegalActions);
    }

    [Fact]
    public void DistinctCommonCardsUseWholePoolEvenIfOnlyOneIsCreated()
    {
        var state = Prepared("nature-pool-common");
        RunState run;
        using (ControlledChanceGuardV111.Enter())
            run = PrototypeGameEngine.BeginControlledPoolShuffleFixture(
                state.Run.Fork(),
                new PrototypeQueuedOperation(
                    PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand,
                    1));
        state = state with { Run = run };
        var pool = state.Run.World!.Combat!.PendingChoice!.ControlledPoolShuffle!;
        var candidates = (string[])pool.OriginalCardIds.Clone();
        var before = state.Run.World.Combat.Hand.Length;
        Assert.True(candidates.Length > 1);
        state = Drain(state);
        Assert.Equal(candidates.Length - 1, state.PoolShuffleTranscript.Length);
        Assert.Equal(before + 1, state.Run.World!.Combat!.Hand.Length);
        // First entry moves to the last slot, then the next prefix
        // entries are swapped down in source Fisher-Yates order.
        var expected = (string[])candidates.Clone();
        for (var i = expected.Length - 1; i > 0; i--)
            (expected[i], expected[0]) = (expected[0], expected[i]);
        Assert.Equal(expected[0], state.Run.World.Combat.Cards.Last().CardId);
    }

    [Fact]
    public void MalformedPoolSupportAndMidShufflePermutationAreRejected()
    {
        var state = BeginPotion("nature-pool-malformed", "proto.potion.attack");
        var corrupted = state.Fork();
        var corruptedChoice = corrupted.Run.World!.Combat!.PendingChoice!;
        var corruptedShuffle = corruptedChoice.ControlledPoolShuffle!;
        var corruptedOriginal = corruptedShuffle.OriginalCardIds;
        corruptedOriginal[0] = "forged.card.model";
        corrupted = ControlledPlayableTestSupport.WithPendingChoice(
            corrupted, corruptedChoice with
            {
                ControlledPoolShuffle = corruptedShuffle with
                {
                    OriginalCardIds = corruptedOriginal
                }
            });
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.VerifyStage(corrupted));
        var changed = state.Fork();
        var changedChoice = changed.Run.World!.Combat!.PendingChoice!;
        var changedShuffle = changedChoice.ControlledPoolShuffle!;
        var changedCurrent = changedShuffle.CurrentCardIds;
        changedCurrent[0] = "missing";
        changed = ControlledPlayableTestSupport.WithPendingChoice(
            changed, changedChoice with
            {
                ControlledPoolShuffle = changedShuffle with
                {
                    CurrentCardIds = changedCurrent
                }
            });
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.VerifyStage(changed));
        // Even a valid permutation of the same source cards is forged
        // unless the recorded index choices reconstruct it exactly.
        var progressed = ControlledPlayableCombatEngineV111.Resolve(
            state, state.Fingerprint, 0);
        var forgedPrefix = progressed.Fork();
        var partialChoice = forgedPrefix.Run.World!.Combat!.PendingChoice!;
        var partialShuffle = partialChoice.ControlledPoolShuffle!;
        var partial = partialShuffle.CurrentCardIds;
        (partial[0], partial[1]) = (partial[1], partial[0]);
        forgedPrefix = ControlledPlayableTestSupport.WithPendingChoice(
            forgedPrefix, partialChoice with
            {
                ControlledPoolShuffle = partialShuffle with
                {
                    CurrentCardIds = partial
                }
            });
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.VerifyStage(forgedPrefix));
        var view = NatureBoundaryMetadataV111.Describe(state);
        Assert.Equal(view.Pending!.EventId,
            NatureBoundaryMetadataV111.Describe(state.Fork()).Pending!.EventId);
    }

    [Fact]
    public void FifteenWorkersDeterministicallyAdvanceFullPool()
    {
        static string Run(int worker)
        {
            var state = BeginPotion(
                "nature-pool-worker-" + worker, "proto.potion.attack");
            var ids = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var view = NatureBoundaryMetadataV111.Describe(state);
                ids.Add(view.Pending!.EventId);
                state = ControlledPlayableCombatEngineV111.Resolve(
                    state, state.Fingerprint, worker % view.Pending.SupportCount);
            }
            return state.Fingerprint + ":" + string.Join("|", ids);
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Run(i));
        Assert.Equal(serial, parallel);
    }
}
