using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticPersistentPlayerActionV111Tests
{
    private static (RunState Run, string EncounterEntry) BeginPlayer(string seed)
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
        var run = playable.Run.Fork();
        var world = run.World!;
        var combat = world.Combat!;
        return (run with
        {
            Player = run.Player with { Hp = 999, MaxHp = 999 },
            World = world with
            {
                Combat = combat with
                {
                    DrawPile = [],
                    DiscardPile = combat.DiscardPile.Concat(combat.DrawPile).ToArray()
                }
            }
        }, playable.Origin.Formation.NativeEncounterEntry);
    }

    private static SemanticEndTurnV111 CompleteTurn(
        SemanticEndTurnV111 state, int offset = 0)
    {
        for (var steps = 0; !state.Complete; steps++)
        {
            if (steps >= 120)
                throw new InvalidOperationException(
                    "End-turn should reach a player decision within 120 choices.");
            int support = state.IntentPending?.SupportCount ??
                state.DrawPending?.CandidateCardInstanceIds.Length ??
                state.CostPending?.SupportCount ??
                throw new InvalidOperationException("No chance event.");
            state = SemanticEndTurnEngineV111.Resolve(
                state, state.Fingerprint, (offset + steps) % support);
        }
        Assert.True(state.AtPlayerBoundary);
        return state;
    }

    private static string ChooseBasicCardAction(RunState run)
    {
        var combat = run.World!.Combat!;
        var cards = combat.Cards
            .Where(c => combat.Hand.Contains(c.InstanceId) &&
                (c.CardId == "proto.silent.defend" ||
                 c.CardId == "proto.silent.strike"))
            .Select(c => c.InstanceId).ToHashSet();
        var actions = new PrototypeAiEnvironment().Observe(run).LegalActions;
        var action = actions.FirstOrDefault(a =>
            a.Kind == "play_card" &&
            a.Payload.Deserialize<PlayCardPayload>() is { } play &&
            cards.Contains(play.CardInstanceId));
        return action?.ActionId ??
            throw new InvalidOperationException(
                "Prepared Silent hand lacks a playable Strike/Defend. " +
                "energy=" + combat.Energy +
                "; hand=" + string.Join(",", combat.Cards.Where(c =>
                    combat.Hand.Contains(c.InstanceId)).Select(c => c.CardId)) +
                "; legal=" + string.Join(",", actions.Select(x => x.Kind)));
    }

    private static SemanticEndTurnV111 FirstEndTurn(
        string seed, out string entry)
    {
        var (source, e) = BeginPlayer(seed);
        entry = e;
        return CompleteTurn(
            SemanticEndTurnEngineV111.BeginFromPlayerAction(source, e));
    }

    [Fact]
    public void BasicRealPlayerActionDoesNotResampleExistingHiddenDeck()
    {
        var first = FirstEndTurn("semantic-persistent-safe-play", out _);
        var run = first.Run;
        var originalDeck = run.World!.Combat!.SemanticDeck!;
        Assert.True(originalDeck.Count > 0);
        Assert.NotEmpty(originalDeck.Transcript!);
        var deckFingerprint = originalDeck.Fingerprint;
        var publicBefore = SemanticDeckEngineV111.Observe(originalDeck);
        var actionId = ChooseBasicCardAction(run);
        var playerAction = SemanticPlayerActionEngineV111.Step(run, actionId);

        Assert.True(playerAction.AtPlayerBoundary);
        Assert.Equal(deckFingerprint,
            playerAction.Run.World!.Combat!.SemanticDeck!.Fingerprint);
        Assert.Equal(run.World!.Combat!.DrawPile,
            playerAction.Run.World.Combat.DrawPile);
        Assert.Equal(CanonicalJson.Sha256(publicBefore),
            CanonicalJson.Sha256(SemanticDeckEngineV111.Observe(
                playerAction.Run.World.Combat.SemanticDeck!)));
        Assert.Equal(playerAction.Fingerprint,
            SemanticPlayerActionEngineV111.Restore(
                SemanticPlayerActionEngineV111.Save(playerAction)).Fingerprint);
        Assert.NotEqual(CanonicalJson.Sha256(playerAction.Run),
            CanonicalJson.Sha256(run));
        Assert.Equal(deckFingerprint, run.World.Combat.SemanticDeck!.Fingerprint);
    }

    [Fact]
    public void SecondRealEndTurnReusesPriorUnknownPoolWithoutDuplicateShuffle()
    {
        var first = FirstEndTurn("semantic-persistent-two-turns", out var entry);
        var originalDeck = first.Run.World!.Combat!.SemanticDeck!;
        Assert.True(originalDeck.Count > 0);
        var originalTranscriptCount = originalDeck.Transcript!.Length;
        var poolRemaining = SemanticDeckEngineV111.AllCardInstanceIds(originalDeck);
        var action = SemanticPlayerActionEngineV111.Step(
            first.Run, ChooseBasicCardAction(first.Run));
        var second = SemanticEndTurnEngineV111.BeginFromPlayerAction(
            action.Run, entry);
        Assert.Equal(originalTranscriptCount,
            second.Origin.World!.Combat!.SemanticDeck!.Transcript!.Length);
        Assert.Equal(originalDeck.Fingerprint,
            second.Origin.World.Combat.SemanticDeck!.Fingerprint);
        Assert.Equal(poolRemaining.Order(),
            SemanticDeckEngineV111.AllCardInstanceIds(
                second.Origin.World.Combat.SemanticDeck).Order());

        var checkpoint = SemanticEndTurnEngineV111.Restore(
            SemanticEndTurnEngineV111.Save(second));
        Assert.Equal(second.Fingerprint, checkpoint.Fingerprint);
        second = CompleteTurn(second);
        Assert.True(second.AtPlayerBoundary);
        Assert.True(second.Run.World!.Combat!.Turn >
            first.Run.World!.Combat!.Turn);
        Assert.NotNull(second.HandDraw);
        Assert.True(second.HandDraw!.Transcript.Length > 0);
        // The second segment reports its own new card decisions, while the
        // provider accumulates a full multi-turn chance history.
        var provider = second.Run.World.Combat.SemanticDeck!;
        Assert.Equal(originalTranscriptCount + second.HandDraw.Transcript.Length,
            provider.Transcript!.Length);
        Assert.Equal(second.Fingerprint,
            SemanticEndTurnEngineV111.Restore(
                SemanticEndTurnEngineV111.Save(second)).Fingerprint);
        Assert.Equal(originalDeck.Fingerprint,
            first.Run.World.Combat.SemanticDeck!.Fingerprint);
    }

    [Fact]
    public void KnownTopConstraintSurvivesPlayerActionAndSecondTurnWithoutReroll()
    {
        var first = FirstEndTurn("semantic-persistent-known-top", out var entry);
        var run = first.Run.Fork();
        var world = run.World!;
        var combat = world.Combat!;
        Assert.True(combat.Hand.Length >= 2);
        // Condition a source-consistent fixed-top placement by moving one
        // existing physical hand card into the draw pile. The separate
        // action must still have a playable basic card in hand.
        long known = combat.Hand[^1];
        var insertedCombat = SemanticDrawPileAdapterV111.ApplyCardZoneMove(
            combat, PrototypeCardZone.Hand, PrototypeCardZone.DrawPile,
            [known], destinationIndex: combat.DrawPile.Length,
            publiclyKnown: true);
        run = run with
        {
            World = world with { Combat = insertedCombat }
        };
        SemanticPlayerActionEngineV111.ValidateBoundary(run);
        var action = SemanticPlayerActionEngineV111.Step(
            run, ChooseBasicCardAction(run));
        Assert.Equal(known, action.Run.World!.Combat!
            .SemanticDeck!.Slots[0].KnownCardInstanceId);
        var next = SemanticEndTurnEngineV111.BeginFromPlayerAction(
            action.Run, entry);
        int steps = 0;
        while (next.HandDraw is null && !next.Complete)
        {
            Assert.NotNull(next.IntentPending);
            next = SemanticEndTurnEngineV111.Resolve(
                next, next.Fingerprint, 0);
            if (++steps > 30)
                throw new InvalidOperationException("No semantic hand stage.");
        }
        Assert.NotNull(next.HandDraw);
        // Known top card is consumed by original DrawCards immediately,
        // *before* the next Nature unknown-card ticket.
        Assert.Contains(known, next.HandDraw!.Run.World!.Combat!.Hand);
        Assert.DoesNotContain(known,
            next.HandDraw.Pending?.CandidateCardInstanceIds ?? []);
        next = CompleteTurn(next);
        Assert.Equal(next.Fingerprint,
            SemanticEndTurnEngineV111.Restore(
                SemanticEndTurnEngineV111.Save(next)).Fingerprint);
    }

    [Fact]
    public void TamperedZoneOrHiddenPoolAndUnsupportedActionFailBeforePublish()
    {
        var first = FirstEndTurn("semantic-persistent-tamper", out _);
        var run = first.Run;
        var old = CanonicalJson.Sha256(run);
        var actionId = ChooseBasicCardAction(run);
        Assert.Throws<NotSupportedException>(() =>
            SemanticPlayerActionEngineV111.Step(
                run, new PrototypeAiEnvironment().Observe(run)
                    .LegalActions.Single(a => a.Kind == "end_turn").ActionId));
        Assert.Equal(old, CanonicalJson.Sha256(run));
        Assert.Throws<InvalidOperationException>(() =>
            _ = run with
            {
                World = run.World! with
                {
                    Combat = run.World.Combat! with
                    {
                        DrawPile = run.World.Combat.DrawPile
                            .Append(-1000).ToArray()
                    }
                }
            });
        Assert.Equal(old, CanonicalJson.Sha256(run));
        var modified = SemanticPlayerActionEngineV111.Step(run, actionId);
        var forged = modified.Fork();
        var forgedRun = forged.Run;
        var forgedWorld = forgedRun.World!;
        var forgedCombat = forgedWorld.Combat!;
        var forgedDeck = forgedCombat.SemanticDeck!;
        var forgedSlots = forgedDeck.Slots;
        forgedSlots[0] = SemanticDeckSlotV111.Known(-1234);
        forged = forged with
        {
            Run = forgedRun with
            {
                World = forgedWorld with
                {
                    Combat = forgedCombat with
                    {
                        SemanticDeck = forgedDeck with
                        {
                            Slots = forgedSlots
                        }
                    }
                }
            }
        };
        Assert.Throws<InvalidDataException>(() =>
            SemanticPlayerActionEngineV111.Restore(
                JsonSerializer.Serialize(forged)));
        Assert.Equal(old, CanonicalJson.Sha256(run));
    }

    [Fact]
    public void FifteenWorkersKeepSecondTurnChanceAndActionReplayIsolated()
    {
        static string Run(int worker)
        {
            var first = FirstEndTurn(
                "semantic-persistent-worker-" + worker, out var entry);
            var action = SemanticPlayerActionEngineV111.Step(
                first.Run, ChooseBasicCardAction(first.Run));
            Assert.Equal(action.Fingerprint,
                SemanticPlayerActionEngineV111.Restore(
                    SemanticPlayerActionEngineV111.Save(action)).Fingerprint);
            var second = CompleteTurn(
                SemanticEndTurnEngineV111.BeginFromPlayerAction(
                    action.Run, entry), worker);
            Assert.Equal(second.Fingerprint,
                SemanticEndTurnEngineV111.Restore(
                    SemanticEndTurnEngineV111.Save(second)).Fingerprint);
            return second.Fingerprint + ":" +
                second.Run.World!.Combat!.SemanticDeck!.Transcript!.Length;
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Run(i));
        Assert.Equal(serial, parallel);
    }
}
