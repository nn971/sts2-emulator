using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledRandomCostV111Tests
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
        string seed, PrototypeCombatEffectKind kind, int amount)
    {
        var state = Prepared(seed);
        using (ControlledChanceGuardV111.Enter())
            return state with
            {
                Run = PrototypeGameEngine.BeginControlledRandomCostFixture(
                    state.Run.Fork(), new PrototypeQueuedOperation(kind, amount))
            };
    }

    [Fact]
    public void SingleHandCardSelectionIsNatureOwnedAndBranchesWithoutRerun()
    {
        var state = Start("nature-cost-single",
            PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed, 0);
        var view = NatureBoundaryMetadataV111.Describe(state);
        Assert.Equal("random_card_cost", view.Kind);
        Assert.Equal("combat.random_hand_card.select", view.Pending!.SiteId);
        Assert.Equal("choose_index", view.Pending.OperationKind);
        Assert.Equal("prototype.combat", view.Pending.StreamId);
        Assert.Equal("UNVERIFIED", view.Pending.ProbabilityStatus);
        Assert.True(view.Pending.SupportCount > 1);
        var before = state.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(state, "stale", 0));
        Assert.Throws<ArgumentException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(
                state, before, view.Pending.SupportCount));
        var a = ControlledPlayableCombatEngineV111.Resolve(state, before, 0);
        var b = ControlledPlayableCombatEngineV111.Resolve(
            state, before, view.Pending.SupportCount - 1);
        Assert.Equal(before, state.Fingerprint);
        Assert.True(a.AtPlayerBoundary);
        Assert.True(b.AtPlayerBoundary);
        Assert.NotEqual(a.Fingerprint, b.Fingerprint);
        Assert.Single(a.RandomCostTranscript);
        Assert.Single(b.LiveTranscript);
        var first = state.Run.World!.Combat!.Hand[0];
        Assert.Equal(0, a.Run.World!.Combat!.Cards
            .Single(x => x.InstanceId == first).TemporaryEnergyCost!.Cost);
        Assert.Equal(state.Run.DecisionIndex, a.Run.DecisionIndex);
    }

    [Fact]
    public void AllHandCostsSuspendSequentiallyAndKeepEarlierChoices()
    {
        var state = Start("nature-cost-all",
            PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed, 4);
        var combat = state.Run.World!.Combat!;
        var eligible = combat.PendingChoice!.ControlledRandomCost!
            .EligibleCardInstanceIds.ToArray();
        Assert.True(eligible.Length >= 2);
        var fingerprints = new HashSet<string>();
        for (int i = 0; i < eligible.Length; i++)
        {
            Assert.True(fingerprints.Add(state.Fingerprint));
            var view = NatureBoundaryMetadataV111.Describe(state);
            Assert.Equal("combat.random_hand_card.energy_cost", view.Pending!.SiteId);
            Assert.Equal("bounded_integer", view.Pending.OperationKind);
            Assert.Equal(4, view.Pending.SupportCount);
            var predecessor = state.Fingerprint;
            state = ControlledPlayableCombatEngineV111.Resolve(
                state, predecessor, i % 4);
            var card = state.Run.World!.Combat!.Cards
                .Single(c => c.InstanceId == eligible[i]);
            Assert.Equal(i % 4, card.TemporaryEnergyCost!.Cost);
        }
        Assert.True(state.AtPlayerBoundary);
        Assert.Equal(eligible.Length, state.RandomCostTranscript.Length);
        Assert.Equal(eligible.Length, state.LiveTranscript.Length);
        Assert.Equal(combat.Hand, state.Run.World!.Combat!.Hand);
    }

    [Fact]
    public void ForgedHandSupportAndCursorFailClosed()
    {
        var state = Start("nature-cost-tamper",
            PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed, 3);
        var altered = state.Fork();
        altered.Run.World!.Combat!.PendingChoice!.ControlledRandomCost!
            .EligibleCardInstanceIds[0] = -99;
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.VerifyStage(altered));
        var badCursor = state.Fork();
        var pending = badCursor.Run.World!.Combat!.PendingChoice!;
        var changed = pending with
        {
            ControlledRandomCost = pending.ControlledRandomCost! with
            {
                NextCardIndex = pending.ControlledRandomCost!.EligibleCardInstanceIds.Length
            }
        };
        badCursor = badCursor with
        {
            Run = badCursor.Run with
            {
                World = badCursor.Run.World! with
                {
                    Combat = badCursor.Run.World!.Combat! with
                    {
                        PendingChoice = changed
                    }
                }
            }
        };
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.VerifyStage(badCursor));
    }

    [Fact]
    public void FifteenWorkersAgreeAcrossScheduling()
    {
        static string Run(int worker)
        {
            var state = Start("nature-cost-15-" + worker,
                PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed, 4);
            var events = new List<string>();
            while (!state.AtPlayerBoundary)
            {
                var view = NatureBoundaryMetadataV111.Describe(state);
                events.Add(view.Pending!.EventId);
                state = ControlledPlayableCombatEngineV111.Resolve(
                    state, state.Fingerprint, worker % view.Pending.SupportCount);
            }
            return state.Fingerprint + ":" + string.Join("|", events);
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var concurrent = new string[15];
        Parallel.For(0, 15, i => concurrent[i] = Run(i));
        Assert.Equal(serial, concurrent);
    }
}
