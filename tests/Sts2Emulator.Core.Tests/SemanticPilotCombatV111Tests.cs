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
    public void WeightedIntentSupportMatchesSourceTicketAndSemanticSelection()
    {
        var state = SemanticPilotCombatEngineV111.Begin(
            Opening("semantic-p0-eligible-weights"));
        var end = Assert.Single(new PrototypeAiEnvironment()
            .Observe(state.Run).LegalActions, a => a.Kind == "end_turn");
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, end.ActionId);
        var intents = state.PendingEndTurn!.Intents!;
        var pending = Assert.IsType<RandomIndexRequestV111>(intents.Pending);
        var support = Assert.IsType<SemanticIntentSupportV111>(
            ControlledTurnIntentEngineV111.DescribePendingSemanticIntent(intents));
        Assert.Equal(pending.OwnerId, support.OwnerId);
        Assert.Equal(pending.SupportCount, support.TotalRawWeight);
        Assert.NotEmpty(support.Candidates);
        Assert.Equal(support.TotalRawWeight,
            support.Candidates.Sum(candidate => candidate.RawWeight));
        var nextTicket = 0;
        foreach (var candidate in support.Candidates)
        {
            Assert.True(candidate.RawWeight > 0);
            Assert.Equal(nextTicket, candidate.TicketStart);
            Assert.False(string.IsNullOrWhiteSpace(candidate.IntentId));
            Assert.False(string.IsNullOrWhiteSpace(candidate.MoveId));
            nextTicket += candidate.RawWeight;
        }
        var last = support.Candidates[^1];
        var bySemanticId = SemanticPilotCombatEngineV111.ResolveIntent(
            state, state.Fingerprint, last.IntentId);
        var bySourceTicket = SemanticPilotCombatEngineV111.Resolve(
            state, state.Fingerprint, last.TicketStart);
        Assert.Equal(bySourceTicket.Fingerprint, bySemanticId.Fingerprint);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.ResolveIntent(
                state, state.Fingerprint, "forged-intent-id"));
        Assert.Equal(bySemanticId.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(bySemanticId)).Fingerprint);
    }

    private static SemanticSeededEntryV111 Seeded(string seed) =>
        new(seed, 2, PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", 20, 0, 1, 3, 5);

    [Fact]
    public void SeededSemanticEntryRequiresOnlyTwoExternalChanceSitesAndReplays()
    {
        var entry = Seeded("semantic-p0-seeded-opening");
        var state = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        Assert.True(state.AtPlayerBoundary);
        Assert.Equal(entry, state.SeededEntry);
        Assert.Null(state.IntentPending);
        Assert.Null(state.DrawPending);
        Assert.Equal(0, state.Run.World!.Combat!.SemanticDeck!.ShuffleEpoch);
        Assert.Empty(state.Run.World.Combat.SemanticDeck.Transcript!);
        Assert.Empty(state.Run.World.Combat.SemanticDeck.Pools);
        Assert.Equal(state.Run.World.Combat.DrawPile.Reverse(),
            state.Run.World.Combat.SemanticDeck.Slots.Select(
                x => x.KnownCardInstanceId!.Value));
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);

        var action = Assert.Single(new PrototypeAiEnvironment()
            .Observe(state.Run).LegalActions, a => a.Kind == "end_turn");
        state = SemanticPilotCombatEngineV111.Step(
            state, state.Fingerprint, action.ActionId);
        int intents = 0;
        for (var count = 0; !state.AtPlayerBoundary && !state.CombatTerminal; count++)
        {
            Assert.True(count < 120);
            Assert.True(state.IntentPending is not null ||
                state.DrawPending is not null);
            if (state.IntentPending is not null) intents++;
            state = SemanticPilotCombatEngineV111.Resolve(
                state, state.Fingerprint, 0);
        }
        Assert.True(intents > 0);
        Assert.True(state.AtPlayerBoundary || state.CombatTerminal);
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void SeededOpeningProvenanceRejectsTamperedReplayAndMatchesFifteenWorkers()
    {
        var entry = Seeded("semantic-p0-seeded-multiworker");
        var initial = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        var forged = initial with
        {
            SeededEntry = entry with { TotalFloor = entry.TotalFloor + 1 }
        };
        Assert.Throws<InvalidDataException>(() =>
            SemanticPilotCombatEngineV111.Restore(
                System.Text.Json.JsonSerializer.Serialize(forged)));
        var single = Enumerable.Range(0, 15).Select(_ =>
            SemanticPilotCombatEngineV111.BeginSeeded(entry).Fingerprint).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] =
            SemanticPilotCombatEngineV111.BeginSeeded(entry).Fingerprint);
        Assert.Equal(single, parallel);
        Assert.All(parallel, value => Assert.Equal(initial.Fingerprint, value));
    }

    [Fact]
    public void SeededCombatEndTurnsCanReachActualDefeatWithoutPhantomIntent()
    {
        var state = SemanticPilotCombatEngineV111.BeginSeeded(
            Seeded("semantic-p0-real-terminal-defeat"));
        Assert.True(state.AtPlayerBoundary);
        int turns = 0;
        int intentChoices = 0;
        while (!state.CombatTerminal && turns < 40)
        {
            Assert.True(state.AtPlayerBoundary);
            var end = Assert.Single(new PrototypeAiEnvironment()
                .Observe(state.Run).LegalActions,
                action => action.Kind == "end_turn");
            state = SemanticPilotCombatEngineV111.Step(
                state, state.Fingerprint, end.ActionId);
            turns++;
            for (int choices = 0; !state.AtPlayerBoundary && !state.CombatTerminal;
                 choices++)
            {
                Assert.True(choices < 100, "Unfinished real turn continuation.");
                if (state.IntentPending is not null)
                {
                    var support = ControlledTurnIntentEngineV111
                        .DescribePendingSemanticIntent(
                            state.PendingEndTurn!.Intents!)!;
                    state = SemanticPilotCombatEngineV111.ResolveIntent(
                        state, state.Fingerprint, support.Candidates[0].IntentId);
                    intentChoices++;
                }
                else if (state.DrawPending is not null)
                    state = SemanticPilotCombatEngineV111.Resolve(
                        state, state.Fingerprint, 0);
                else
                    throw new InvalidDataException("Missing supported two-site ticket.");
            }
        }
        Assert.True(intentChoices > 0);
        Assert.True(state.CombatTerminal, "Expected an actual terminal fight.");
        Assert.Equal("defeat", state.CombatOutcome);
        Assert.True(state.Run.Player.Hp <= 0);
        Assert.Null(state.PendingEndTurn);
        Assert.Null(state.IntentPending);
        Assert.Null(state.DrawPending);
        Assert.Equal(state.Fingerprint,
            SemanticPilotCombatEngineV111.Restore(
                SemanticPilotCombatEngineV111.Save(state)).Fingerprint);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticPilotCombatEngineV111.Step(
                state, state.Fingerprint, "end_turn"));
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
