using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticCardSelectionV111Tests
{
    private static (RunState Run, string Encounter) PlayerWithSemanticDeck(
        string seed)
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
        run = run with
        {
            Player = run.Player with { Hp = 999, MaxHp = 999 },
            World = world with
            {
                Combat = combat with
                {
                    DrawPile = [],
                    DiscardPile = combat.DiscardPile.Concat(
                        combat.DrawPile).ToArray()
                }
            }
        };
        var first = SemanticEndTurnEngineV111.BeginFromPlayerAction(
            run, playable.Origin.Formation.NativeEncounterEntry);
        for (int i = 0; !first.Complete; i++)
        {
            if (i > 100)
                throw new InvalidOperationException("No player boundary reached.");
            var support = first.IntentPending?.SupportCount ??
                first.DrawPending?.CandidateCardInstanceIds.Length ??
                first.CostPending?.SupportCount ??
                throw new InvalidDataException("No Nature decision.");
            first = SemanticEndTurnEngineV111.Resolve(
                first, first.Fingerprint, i % support);
        }
        Assert.True(first.AtPlayerBoundary);
        return (first.Run, playable.Origin.Formation.NativeEncounterEntry);
    }

    private static RunState PendingSelection(
        RunState source, PrototypeCardZone from,
        PrototypeCardSelectionResolutionKind resolution, long[] ids)
    {
        var combat = source.World!.Combat!;
        var spec = new PrototypeCardSelectionSpec(
            from, ids.Length, ids.Length, resolution);
        var pending = new PendingCombatChoiceState(
            "select_cards", null, PrototypeCardZone.DiscardPile,
            spec, ids, [], [], MoveSourceCardOnCompletion: false);
        return source with
        {
            World = source.World with
            {
                Combat = combat with { PendingChoice = pending }
            }
        };
    }

    private static string SelectAction(RunState source, long[] ids)
    {
        var actions = new PrototypeAiEnvironment().Observe(source).LegalActions;
        return actions.Single(a =>
            a.Kind == "select_cards" &&
            a.Payload.Deserialize<SelectCardsPayload>() is { } selected &&
            selected.CardInstanceIds.SequenceEqual(ids)).ActionId;
    }

    [Fact]
    public void RealHandSelectionCommitsTwoCardsToKnownTopInSourceOrder()
    {
        var (run, _) = PlayerWithSemanticDeck("semantic-select-top-two");
        var combat = run.World!.Combat!;
        Assert.True(combat.Hand.Length >= 2);
        var selected = combat.Hand.Take(2).ToArray();
        var oldDeck = combat.SemanticDeck!;
        var source = PendingSelection(run, PrototypeCardZone.Hand,
            PrototypeCardSelectionResolutionKind.MoveToDrawTop, selected);
        var choice = SemanticCardSelectionEngineV111.Step(
            source, SelectAction(source, selected));

        Assert.True(choice.Run.World!.Combat!.IsPlayerTurn);
        Assert.Null(choice.Run.World.Combat.PendingChoice);
        Assert.Equal(selected[1], choice.Run.World.Combat.SemanticDeck!
            .Slots[0].KnownCardInstanceId);
        Assert.Equal(selected[0], choice.Run.World.Combat.SemanticDeck
            .Slots[1].KnownCardInstanceId);
        Assert.Equal(selected.Reverse(),
            choice.Run.World.Combat.SemanticDeck.Slots.Take(2)
                .Select(x => x.KnownCardInstanceId!.Value));
        Assert.Equal(combat.DrawPile.Concat(selected),
            choice.Run.World.Combat.DrawPile);
        Assert.Equal(oldDeck.Transcript!.Length,
            choice.Run.World.Combat.SemanticDeck.Transcript!.Length);
        Assert.Equal(oldDeck.ShuffleEpoch,
            choice.Run.World.Combat.SemanticDeck.ShuffleEpoch);
        Assert.Equal(choice.Fingerprint,
            SemanticCardSelectionEngineV111.Restore(
                SemanticCardSelectionEngineV111.Save(choice)).Fingerprint);
        Assert.Equal(oldDeck.Fingerprint, combat.SemanticDeck!.Fingerprint);
        SemanticPlayerActionEngineV111.ValidateBoundary(choice.Run);
    }

    [Fact]
    public void KnownTopFromSourceSelectionDrawsWithoutNatureIdentityReroll()
    {
        var (run, encounter) = PlayerWithSemanticDeck(
            "semantic-select-top-next-turn");
        long known = run.World!.Combat!.Hand[0];
        var source = PendingSelection(run, PrototypeCardZone.Hand,
            PrototypeCardSelectionResolutionKind.MoveToDrawTop, [known]);
        var choice = SemanticCardSelectionEngineV111.Step(
            source, SelectAction(source, [known]));
        var next = SemanticEndTurnEngineV111.BeginFromPlayerAction(
            choice.Run, encounter);
        for (int n = 0; next.HandDraw is null; n++)
        {
            Assert.True(n < 30, "Expected a hand-draw Nature stage.");
            Assert.NotNull(next.IntentPending);
            next = SemanticEndTurnEngineV111.Resolve(
                next, next.Fingerprint, 0);
        }
        // The original automatic DrawCards consumed the source-selected
        // top card before asking for the next unresolved card.
        Assert.Contains(known, next.HandDraw!.Run.World!.Combat!.Hand);
        Assert.DoesNotContain(known,
            next.HandDraw.Pending?.CandidateCardInstanceIds ?? []);
        while (!next.Complete)
        {
            int support = next.IntentPending?.SupportCount ??
                next.DrawPending?.CandidateCardInstanceIds.Length ??
                next.CostPending?.SupportCount ??
                throw new InvalidDataException("Missing next chance stage.");
            next = SemanticEndTurnEngineV111.Resolve(
                next, next.Fingerprint, 0 % support);
        }
        Assert.Equal(next.Fingerprint, SemanticEndTurnEngineV111.Restore(
            SemanticEndTurnEngineV111.Save(next)).Fingerprint);
    }

    [Fact]
    public void RevealedCardRemovedFromDrawByOriginalSelectionRetainsOtherConstraints()
    {
        var (run, _) = PlayerWithSemanticDeck(
            "semantic-select-revealed-removal");
        var combat = run.World!.Combat!;
        var deck = SemanticDeckEngineV111.Require(
            combat.SemanticDeck!, 1, "peek");
        Assert.NotNull(deck.Pending);
        deck = SemanticDeckEngineV111.Resolve(
            deck, deck.Fingerprint, 0);
        var known = deck.Slots[1].KnownCardInstanceId!.Value;
        run = run with
        {
            World = run.World with
            {
                Combat = combat with { SemanticDeck = deck }
            }
        };
        var source = PendingSelection(run, PrototypeCardZone.DrawPile,
            PrototypeCardSelectionResolutionKind.MoveToHand, [known]);
        var choice = SemanticCardSelectionEngineV111.Step(
            source, SelectAction(source, [known]));
        Assert.Contains(known, choice.Run.World!.Combat!.Hand);
        Assert.DoesNotContain(known, choice.Run.World.Combat.DrawPile);
        Assert.Equal(deck.Count - 1,
            choice.Run.World.Combat.SemanticDeck!.Count);
        Assert.Equal(deck.Transcript!.Length,
            choice.Run.World.Combat.SemanticDeck.Transcript!.Length);
        Assert.Equal(deck.Fingerprint, deck.Fingerprint);
        Assert.Equal(choice.Fingerprint,
            SemanticCardSelectionEngineV111.Restore(
                SemanticCardSelectionEngineV111.Save(choice)).Fingerprint);
    }

    [Fact]
    public void UnknownPositionRemovalFailsAtomicallyAndBadStateCannotReplay()
    {
        var (run, _) = PlayerWithSemanticDeck(
            "semantic-select-unknown-position");
        var combat = run.World!.Combat!;
        var selectedAnchor = combat.Hand[0];
        var originalDeck = combat.SemanticDeck!;
        var unresolvedPool = originalDeck.Pools.First(pool =>
            pool.RemainingCardInstanceIds.Length >= 2);
        var poolPositions = originalDeck.Slots
            .Select((slot, index) => (slot, index))
            .Where(item => item.slot.PoolId == unresolvedPool.PoolId)
            .Select(item => item.index).ToArray();
        var splitDeck = SemanticDeckEngineV111.InsertKnown(
            originalDeck, poolPositions[poolPositions.Length / 2],
            selectedAnchor);
        combat = SemanticDrawPileAdapterV111.ApplyCardZoneMove(
            combat, PrototypeCardZone.Hand, PrototypeCardZone.DrawPile,
            [selectedAnchor], destinationIndex: combat.DrawPile.Length,
            publiclyKnown: false);
        combat = combat with
        {
            SemanticDeck = splitDeck
        };
        run = run with
        {
            World = run.World! with { Combat = combat }
        };
        long unknown = unresolvedPool.RemainingCardInstanceIds[0];
        var source = PendingSelection(run, PrototypeCardZone.DrawPile,
            PrototypeCardSelectionResolutionKind.MoveToHand, [unknown]);
        var sourceFingerprint = CanonicalJson.Sha256(source);
        Assert.Throws<NotSupportedException>(() =>
            SemanticCardSelectionEngineV111.Step(
                source, SelectAction(source, [unknown])));
        Assert.Equal(sourceFingerprint, CanonicalJson.Sha256(source));
        var handCard = combat.Hand[0];
        var legal = PendingSelection(run, PrototypeCardZone.Hand,
            PrototypeCardSelectionResolutionKind.MoveToDrawTop, [handCard]);
        var ok = SemanticCardSelectionEngineV111.Step(
            legal, SelectAction(legal, [handCard]));
        var modified = ok.Fork();
        var modifiedRun = modified.Run;
        var modifiedWorld = modifiedRun.World!;
        var modifiedCombat = modifiedWorld.Combat!;
        var modifiedDeck = modifiedCombat.SemanticDeck!;
        var modifiedSlots = modifiedDeck.Slots;
        modifiedSlots[0] = SemanticDeckSlotV111.Known(-9000);
        modified = modified with
        {
            Run = modifiedRun with
            {
                World = modifiedWorld with
                {
                    Combat = modifiedCombat with
                    {
                        SemanticDeck = modifiedDeck with
                        {
                            Slots = modifiedSlots
                        }
                    }
                }
            }
        };
        Assert.Throws<InvalidDataException>(() =>
            SemanticCardSelectionEngineV111.Restore(
                JsonSerializer.Serialize(modified)));
    }

    [Fact]
    public void FifteenWorkersPreserveSourceChoiceAndNextHandDrawDeterministically()
    {
        static string Rollout(int worker)
        {
            var (run, encounter) = PlayerWithSemanticDeck(
                "semantic-select-worker-" + worker);
            var known = run.World!.Combat!.Hand[0];
            var pending = PendingSelection(run, PrototypeCardZone.Hand,
                PrototypeCardSelectionResolutionKind.MoveToDrawTop, [known]);
            var result = SemanticCardSelectionEngineV111.Step(
                pending, SelectAction(pending, [known]));
            Assert.Equal(result.Fingerprint,
                SemanticCardSelectionEngineV111.Restore(
                    SemanticCardSelectionEngineV111.Save(result)).Fingerprint);
            var next = SemanticEndTurnEngineV111.BeginFromPlayerAction(
                result.Run, encounter);
            int i = 0;
            while (!next.Complete)
            {
                Assert.True(++i < 120);
                var support = next.IntentPending?.SupportCount ??
                    next.DrawPending?.CandidateCardInstanceIds.Length ??
                    next.CostPending?.SupportCount ??
                    throw new InvalidDataException("Missing choice.");
                next = SemanticEndTurnEngineV111.Resolve(
                    next, next.Fingerprint, worker % support);
            }
            Assert.Contains(known, next.Run.World!.Combat!.Hand);
            Assert.Equal(next.Fingerprint,
                SemanticEndTurnEngineV111.Restore(
                    SemanticEndTurnEngineV111.Save(next)).Fingerprint);
            return next.Fingerprint + ":" + result.Fingerprint;
        }
        var serial = Enumerable.Range(0, 15).Select(Rollout).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Rollout(i));
        Assert.Equal(serial, parallel);
    }
}
