using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticPilotCombatV111Tests
{
    private static ControlledPlayableCombatV111 Opening(string seed)
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
        var opening = ControlledPlayableCombatEngineV111.Begin(intents, seed);
        while (!opening.AtPlayerBoundary)
            opening = ControlledPlayableCombatEngineV111.Resolve(
                opening, opening.Fingerprint, 0);
        return opening;
    }

    [Fact]
    public void SemanticProtocolReusesExactlyPrecommittedOpeningOrder()
    {
        var legacy = Opening("semantic-p0-precommitted-opening");
        var original = CanonicalJson.Sha256(legacy);
        var state = SemanticPilotCombatEngineV111.Begin(legacy);
        Assert.True(state.AtPlayerBoundary);
        Assert.Equal(SemanticPilotCombatV111.SchemaId, state.Mode);
        Assert.Equal(legacy.Run.World!.Combat!.DrawPile,
            state.Run.World!.Combat!.DrawPile);
        Assert.Equal(0, state.Run.World.Combat.SemanticDeck!.ShuffleEpoch);
        Assert.Empty(state.Run.World.Combat.SemanticDeck.Transcript!);
        Assert.Empty(state.Run.World.Combat.SemanticDeck.Pools);
        Assert.Equal(legacy.Run.World.Combat.DrawPile.Reverse(),
            state.Run.World.Combat.SemanticDeck.Slots.Select(
                x => x.KnownCardInstanceId!.Value));
        Assert.Null(state.DrawPending);
        Assert.Null(state.IntentPending);
        Assert.False(state.CombatTerminal);
        Assert.Equal(original, CanonicalJson.Sha256(legacy));
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void ActualEndTurnAndNatureIntentsReachRealFairPlayerDecision()
    {
        var state = SemanticPilotCombatEngineV111.Begin(
            Opening("semantic-p0-real-action-to-action"));
        var before = state.Fingerprint;
        var actions = new PrototypeAiEnvironment().Observe(state.Run).LegalActions;
        var endTurn = Assert.Single(actions, a => a.Kind == "end_turn");
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, endTurn.ActionId);
        Assert.NotEqual(before, state.Fingerprint);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.Resolve(state, before, 0));
        int intents = 0, draws = 0;
        int step = 0;
        while (!state.AtPlayerBoundary)
        {
            Assert.True(++step < 120, "Expected original automatic turn to finish.");
            if (state.IntentPending is { } intent)
            {
                Assert.True(intent.SupportCount > 0);
                intents++;
                state = SemanticPilotCombatEngineV111.Resolve(
                    state, state.Fingerprint, 0);
            }
            else if (state.DrawPending is { } draw)
            {
                Assert.NotEmpty(draw.CandidateCardInstanceIds);
                var candidate = draw.CandidateCardInstanceIds[0];
                Assert.Contains(candidate,
                    state.Run.World!.Combat!.Cards.Select(c => c.InstanceId));
                draws++;
                state = SemanticPilotCombatEngineV111.Resolve(
                    state, state.Fingerprint, 0);
            }
            else
                throw new InvalidOperationException(
                    "Unsupported source event lacked a semantic chance boundary.");
            Assert.Equal(state.Fingerprint,
                SemanticPilotCombatEngineV111.Restore(
                    SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
        }
        Assert.True(intents > 0);
        Assert.True(state.Run.World!.Combat!.IsPlayerTurn);
        Assert.NotEmpty(new PrototypeAiEnvironment()
            .Observe(state.Run).LegalActions);
        // Source opening is fully committed, so first turn's known
        // physical cards never get duplicate Nature identity tickets.
        Assert.Equal(0, draws);
    }

    [Fact]
    public void BasicSilentActionKeepsCommittedHiddenDeckUntouched()
    {
        var state = SemanticPilotCombatEngineV111.Begin(
            Opening("semantic-p0-basic-player-action"));
        var old = state.Run.World!.Combat!.SemanticDeck!.Fingerprint;
        var run = state.Run;
        var hand = run.World!.Combat!.Hand.ToHashSet();
        var basic = run.World.Combat.Cards.Where(c =>
            hand.Contains(c.InstanceId) &&
            c.CardId is "proto.silent.strike" or "proto.silent.defend")
            .Select(c => c.InstanceId).ToHashSet();
        var action = new PrototypeAiEnvironment().Observe(run)
            .LegalActions.First(a => a.Kind == "play_card" &&
                a.Payload.Deserialize<PlayCardPayload>() is { } card &&
                basic.Contains(card.CardInstanceId));
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, action.ActionId);
        Assert.True(state.AtPlayerBoundary);
        Assert.Equal(old,
            state.Run.World!.Combat!.SemanticDeck!.Fingerprint);
        Assert.Single(state.History);
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void FifteenWorkersRemainDeterministicAfterRealSemanticIntentTurns()
    {
        static string Rollout(int worker)
        {
            var state = SemanticPilotCombatEngineV111.Begin(
                Opening("semantic-p0-worker-" + worker));
            var action = Assert.Single(new PrototypeAiEnvironment()
                .Observe(state.Run).LegalActions, a => a.Kind == "end_turn");
            state = SemanticPilotCombatEngineV111.Step(
                state, state.Fingerprint, action.ActionId);
            var steps = 0;
            while (!state.AtPlayerBoundary)
            {
                Assert.True(++steps < 120);
                var support = state.IntentPending?.SupportCount ??
                    state.DrawPending?.CandidateCardInstanceIds.Length ??
                    throw new InvalidOperationException("Unexpected non-two-site chance.");
                state = SemanticPilotCombatEngineV111.Resolve(
                    state, state.Fingerprint, worker % support);
            }
            Assert.Equal(state.Fingerprint,
                SemanticPilotCombatEngineV111.Restore(
                    SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
            return state.Fingerprint + ":" + string.Join(",",
                state.History.Select(x => x.Kind + ":" +
                    (x.ActionId ?? x.SelectedIndex?.ToString())));
        }
        var single = Enumerable.Range(0, 15).Select(Rollout).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Rollout(i));
        Assert.Equal(single, parallel);
    }
}
