using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticEndTurnV111Tests
{
    private static (RunState PlayerRun, string NativeEncounterEntry) Prepared(
        string seed, bool snecko = false)
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
        var run = state.Run.Fork();
        var world = run.World!;
        var combat = world.Combat!;
        // The initial controlled shuffle already committed a native-shaped
        // order. Exhaust the remaining pile BEFORE the player ends turn:
        // the next automatic hand draw now has a genuinely fresh source
        // discard recycle, without double counting any old shuffle tickets.
        run = run with
        {
            Player = snecko ? run.Player with
            {
                Relics = run.Player.Relics.Append(
                    new RelicInstance("proto.relic.snecko_eye",
                        PrototypeJson.EmptyObject())).ToArray()
            } : run.Player,
            World = world with
            {
                Combat = combat with
                {
                    Relics = snecko
                        ? (combat.Relics ?? []).Append(new CombatRelicState(
                            (combat.Relics ?? []).Length,
                            "proto.relic.snecko_eye",
                            combat.NextPowerApplicationOrder,
                            new int[(PrototypeContent.Relic(
                                "proto.relic.snecko_eye").Triggers ?? []).Length],
                            false)).ToArray()
                        : combat.Relics,
                    NextPowerApplicationOrder = snecko
                        ? combat.NextPowerApplicationOrder + 1
                        : combat.NextPowerApplicationOrder,
                    DrawPile = [],
                    DiscardPile = combat.DiscardPile.Concat(
                        combat.DrawPile).ToArray()
                }
            }
        };
        return (run, state.Origin.Formation.NativeEncounterEntry);
    }

    private static SemanticEndTurnV111 Finish(
        SemanticEndTurnV111 state, int seedOffset = 0)
    {
        int steps = 0;
        while (!state.Complete)
        {
            if (++steps > 100)
                throw new InvalidOperationException(
                    "Expected source-shaped end turn to finish within 100 Nature choices.");
            var bound = state.IntentPending?.SupportCount ??
                state.DrawPending?.CandidateCardInstanceIds.Length ??
                state.CostPending?.SupportCount ??
                throw new InvalidDataException("No pending Nature chance choice.");
            state = SemanticEndTurnEngineV111.Resolve(
                state, state.Fingerprint, (seedOffset + steps) % bound);
        }
        return state;
    }

    [Fact]
    public void GenuinePlayerEndTurnCommitsIntentThenRecyclesAndReachesNextPlayer()
    {
        var (source, entry) = Prepared("semantic-endturn-real");
        var original = CanonicalJson.Sha256(source);
        var frame = new PrototypeAiEnvironment().Observe(source);
        Assert.Contains(frame.LegalActions, a => a.Kind == "end_turn");
        var state = SemanticEndTurnEngineV111.BeginFromPlayerAction(source, entry);
        Assert.NotNull(state.PlayerActionSource);
        Assert.False(state.AtPlayerBoundary);
        Assert.Equal(PrototypeAutomaticStepKind.AdvanceTurn,
            PrototypeContent.Rules.EndTurnPipeline[
                state.Origin.World!.Combat!.AutomaticPipelineContinuation!.NextStepIndex].Kind);
        var first = state.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            SemanticEndTurnEngineV111.Resolve(state, "stale", 0));
        var seenIntents = 0;
        var seenCardChoices = 0;
        while (!state.Complete)
        {
            int bound;
            if (state.IntentPending is { } intent)
            {
                seenIntents++;
                bound = intent.SupportCount;
            }
            else if (state.DrawPending is { } draw)
            {
                seenCardChoices++;
                bound = draw.CandidateCardInstanceIds.Length;
            }
            else
                bound = state.CostPending!.SupportCount;
            state = SemanticEndTurnEngineV111.Resolve(
                state, state.Fingerprint, 0);
            Assert.True(bound > 0);
        }
        Assert.True(state.AtPlayerBoundary);
        Assert.True(seenIntents > 0);
        Assert.True(seenCardChoices > 0);
        Assert.NotNull(state.HandDraw);
        Assert.True(state.HandDraw!.AtBoundary);
        Assert.Equal(1, state.HandDraw.Run.World!.Combat!.SemanticDeck!.ShuffleEpoch);
        Assert.Null(state.Run.World!.Combat!.AutomaticPipelineContinuation);
        Assert.True(state.Run.World.Combat.IsPlayerTurn);
        Assert.True(state.Run.World.Combat.Turn > source.World!.Combat!.Turn);
        Assert.NotEmpty(new PrototypeAiEnvironment().Observe(state.Run).LegalActions);
        Assert.Equal(original, CanonicalJson.Sha256(source));
        Assert.Equal(state.Fingerprint,
            SemanticEndTurnEngineV111.Restore(
                SemanticEndTurnEngineV111.Save(state)).Fingerprint);
        Assert.NotEqual(first, state.Fingerprint);
    }

    [Fact]
    public void ReconstructsMidIntentMidDrawAndFinalPlayerDecisionFromActualAction()
    {
        var (source, entry) = Prepared("semantic-endturn-replay", snecko: true);
        var state = SemanticEndTurnEngineV111.BeginFromPlayerAction(source, entry);
        Assert.Equal(state.Fingerprint,
            SemanticEndTurnEngineV111.Restore(
                SemanticEndTurnEngineV111.Save(state)).Fingerprint);
        int steps = 0, cardChoices = 0, costs = 0;
        while (!state.Complete)
        {
            bool card = state.DrawPending is not null;
            bool cost = state.CostPending is not null;
            var bound = state.IntentPending?.SupportCount ??
                state.DrawPending?.CandidateCardInstanceIds.Length ??
                state.CostPending!.SupportCount;
            state = SemanticEndTurnEngineV111.Resolve(
                state, state.Fingerprint, ++steps % bound);
            if (card) cardChoices++;
            if (cost) costs++;
            Assert.Equal(state.Fingerprint,
                SemanticEndTurnEngineV111.Restore(
                    SemanticEndTurnEngineV111.Save(state)).Fingerprint);
        }
        Assert.True(state.AtPlayerBoundary);
        Assert.True(cardChoices >= PrototypeContent.Rules.HandSize);
        Assert.True(costs > 0);
        Assert.Equal(costs, state.HandDraw!.CostTranscript.Length);
        Assert.Equal(cardChoices, state.HandDraw.Transcript.Length);
        Assert.Equal(state.HandDraw.RequestedCount,
            state.HandDraw.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
    }

    [Fact]
    public void PrecommittedNonemptyDeckIsRejectedBeforeDoubleCountingShuffle()
    {
        var (source, entry) = Prepared("semantic-endturn-double-count");
        var combat = source.World!.Combat!;
        var moved = combat.DiscardPile[0];
        var modified = source with
        {
            World = source.World with
            {
                Combat = combat with
                {
                    DrawPile = [moved],
                    DiscardPile = combat.DiscardPile.Skip(1).ToArray()
                }
            }
        };
        var fingerprint = CanonicalJson.Sha256(modified);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticEndTurnEngineV111.BeginFromPlayerAction(modified, entry));
        Assert.Equal(fingerprint, CanonicalJson.Sha256(modified));
    }

    [Fact]
    public void RejectsStaleAndOutOfSupportChoicesWithoutChangingParent()
    {
        var (source, entry) = Prepared("semantic-endturn-branches");
        var parent = SemanticEndTurnEngineV111.BeginFromPlayerAction(source, entry);
        var fingerprint = parent.Fingerprint;
        int bound = parent.IntentPending?.SupportCount ??
            parent.DrawPending?.CandidateCardInstanceIds.Length ??
            parent.CostPending!.SupportCount;
        Assert.ThrowsAny<Exception>(() =>
            SemanticEndTurnEngineV111.Resolve(parent, fingerprint, bound));
        Assert.Throws<InvalidOperationException>(() =>
            SemanticEndTurnEngineV111.Resolve(parent, "stale", 0));
        Assert.Equal(fingerprint, parent.Fingerprint);
        var first = SemanticEndTurnEngineV111.Resolve(parent, fingerprint, 0);
        Assert.Equal(fingerprint, parent.Fingerprint);
        Assert.NotEqual(fingerprint, first.Fingerprint);
        Assert.Equal(first.Fingerprint,
            SemanticEndTurnEngineV111.Restore(
                SemanticEndTurnEngineV111.Save(first)).Fingerprint);
        var changed = first.Fork();
        var changedTranscript = changed.IntentTranscript;
        changedTranscript[0] = changedTranscript[0] with
        {
            SelectedIndex = 999
        };
        changed = changed with { IntentTranscript = changedTranscript };
        Assert.ThrowsAny<Exception>(() =>
            SemanticEndTurnEngineV111.Restore(
                System.Text.Json.JsonSerializer.Serialize(changed)));
    }

    [Fact]
    public void FifteenWorkersRunFullEndTurnAndReplayInIndependentOrders()
    {
        static string Run(int worker)
        {
            var (source, entry) = Prepared("semantic-endturn-15-" + worker);
            var state = Finish(
                SemanticEndTurnEngineV111.BeginFromPlayerAction(source, entry),
                worker);
            Assert.True(state.AtPlayerBoundary);
            Assert.Equal(state.Fingerprint,
                SemanticEndTurnEngineV111.Restore(
                    SemanticEndTurnEngineV111.Save(state)).Fingerprint);
            return state.Fingerprint + ":" +
                string.Join(",", state.IntentTranscript.Select(
                    x => x.SelectedIndex)) + ":" +
                string.Join(",", state.HandDraw!.Transcript.Select(
                    x => x.SelectedCardInstanceId));
        }
        var sequential = Enumerable.Range(0, 15).Select(Run).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Run(i));
        Assert.Equal(sequential, parallel);
    }
}
