using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticAutomaticHandDrawV111Tests
{
    private static RunState SavedHandDrawCheckpoint(
        string seed, bool snecko = false, bool slither = false,
        int? reducedDraw = null)
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
        // The existing playable fixture supplies genuine cards/enemies.
        // A saved DrawPlayerHand stage cursor represents an already
        // completed preceding automatic-pipeline prefix.
        var run = ControlledPlayableCombatEngineV111.Begin(intents, seed).Run;
        var world = run.World!;
        var combat = world.Combat!;
        long slitherCard = combat.DrawPile[0];
        var pile = reducedDraw is null
            ? combat.DrawPile : combat.DrawPile.Take(reducedDraw.Value).ToArray();
        var rest = reducedDraw is null ? combat.DiscardPile
            : combat.DrawPile.Skip(reducedDraw.Value).ToArray();
        var player = run.Player;
        if (snecko)
        {
            const string sneckoEyeId = "proto.relic.snecko_eye";
            var nextOrder = combat.RelicStates
                .Select(relic => relic.ApplicationOrder)
                .Concat(combat.PlayerPowers.Select(power => power.ApplicationOrder))
                .Concat(combat.Enemies.SelectMany(enemy => enemy.PowerStates)
                    .Select(power => power.ApplicationOrder))
                .DefaultIfEmpty(0).Max() + 1;
            var definition = PrototypeContent.Relic(sneckoEyeId);
            player = player with
            {
                Relics = player.Relics.Append(new RelicInstance(
                    sneckoEyeId, PrototypeJson.EmptyObject())).ToArray()
            };
            combat = combat with
            {
                Relics = combat.RelicStates.Append(new CombatRelicState(
                    combat.RelicStates.Length, sneckoEyeId, nextOrder,
                    new int[(definition.Triggers ?? []).Length])).ToArray(),
                NextPowerApplicationOrder = Math.Max(
                    combat.NextPowerApplicationOrder, nextOrder + 1)
            };
        }
        if (slither)
        {
            player = player with
            {
                Deck = player.Deck.Select(card =>
                    card.InstanceId == slitherCard
                        ? card with
                        {
                            Enchantment = new PrototypeCardEnchantment(
                                PrototypeCardEnchantmentKind.Slither)
                        }
                        : card).ToArray()
            };
        }
        combat = combat with
        {
            DrawPile = pile,
            DiscardPile = rest,
            Cards = combat.Cards.Select(c =>
                slither && c.InstanceId == slitherCard
                    ? c with
                    {
                        Enchantment = new PrototypeCardEnchantment(
                            PrototypeCardEnchantmentKind.Slither)
                    } : c).ToArray(),
            AutomaticPipelineContinuation =
                new PrototypeAutomaticPipelineContinuationState(
                    PrototypeGameEngine.SemanticHandDrawStepIndex)
        };
        combat = SemanticDrawPileAdapterV111.InitializeSemanticDrawPile(
            combat, SemanticDeckEngineV111.BeginNature(pile));
        return run with
        {
            Player = player,
            World = world with
            {
                Combat = combat
            }
        };
    }

    [Fact]
    public void RealBeforeHandDrawRunsOnceThenSemanticCardSelectionsResumeStage()
    {
        var source = SavedHandDrawCheckpoint("nature-auto-hand");
        var fingerprint = CanonicalJson.Sha256(source);
        var state = SemanticCombatDrawEngineV111.BeginAutomaticHandDraw(source);
        Assert.True(state.AutomaticHandDraw);
        Assert.True(state.FromHandDraw);
        Assert.Equal(PrototypeContent.Rules.HandSize, state.RequestedCount);
        Assert.Equal(state.RequestedCount, state.RemainingCount);
        Assert.Equal(0, state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
        Assert.NotNull(state.Pending);
        Assert.Null(state.CostPending);
        var initialBoundary = state.Pending!.EventId;
        var partial = SemanticCombatDrawEngineV111.Restore(
            SemanticCombatDrawEngineV111.Save(state));
        Assert.Equal(initialBoundary, partial.Pending!.EventId);
        Assert.Equal(state.Fingerprint, partial.Fingerprint);

        int drawCount = 0;
        while (!state.AtBoundary)
        {
            Assert.NotNull(state.Pending);
            Assert.Null(state.CostPending);
            state = SemanticCombatDrawEngineV111.Resolve(
                state, state.Fingerprint, 0);
            drawCount++;
            Assert.Equal(drawCount,
                state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
            Assert.Equal(drawCount, state.Run.World.Combat.Hand.Length);
            if (!state.AtBoundary)
            {
                Assert.Equal(PrototypeGameEngine.SemanticHandDrawStepIndex,
                    state.Run.World.Combat.AutomaticPipelineContinuation!.NextStepIndex);
                Assert.Equal(state.RemainingCount,
                    state.Run.World.Combat.AutomaticPipelineContinuation!
                        .ControlledSemanticDrawRemaining);
            }
        }
        Assert.Equal(state.RequestedCount, drawCount);
        Assert.Equal(state.RequestedCount, state.Transcript.Length);
        Assert.Equal(PrototypeGameEngine.SemanticHandDrawStepIndex + 1,
            state.Run.World!.Combat!.AutomaticPipelineContinuation!.NextStepIndex);
        Assert.Null(state.Run.World.Combat.AutomaticPipelineContinuation!
            .ControlledSemanticDrawRemaining);
        Assert.Equal(fingerprint, CanonicalJson.Sha256(source));
        Assert.Equal(state.Fingerprint, SemanticCombatDrawEngineV111.Restore(
            SemanticCombatDrawEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void SneckoAndSlitherCostTicketsSuspendInsideActualHandDrawStage()
    {
        var source = SavedHandDrawCheckpoint(
            "nature-auto-cost", snecko: true, slither: true);
        var state = SemanticCombatDrawEngineV111.BeginAutomaticHandDraw(source);
        var slitherId = state.Pending!.CandidateCardInstanceIds[0];
        var originalRng = CanonicalJson.Sha256(source.Rng);
        state = SemanticCombatDrawEngineV111.Resolve(
            state, state.Fingerprint, 0);
        Assert.Equal("combat.card_draw.relic_energy_cost", state.CostPending!.SiteId);
        Assert.Null(state.Pending);
        Assert.Equal(0, state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
        Assert.Equal(state.Fingerprint, SemanticCombatDrawEngineV111.Restore(
            SemanticCombatDrawEngineV111.Save(state)).Fingerprint);
        state = SemanticCombatDrawEngineV111.Resolve(
            state, state.Fingerprint, 1);
        Assert.Equal("combat.card_draw.slither_energy_cost", state.CostPending!.SiteId);
        Assert.Equal(1, state.Run.World!.Combat!.Cards
            .Single(c => c.InstanceId == slitherId).TemporaryEnergyCost!.Cost);
        Assert.Equal(0, state.Run.World.Combat.CounterState.CardsDrawnThisCombat);
        state = SemanticCombatDrawEngineV111.Resolve(
            state, state.Fingerprint, 3);
        Assert.Equal(3, state.Run.World!.Combat!.Cards
            .Single(c => c.InstanceId == slitherId).TemporaryEnergyCost!.Cost);
        Assert.Equal(1, state.Run.World.Combat.CounterState.CardsDrawnThisCombat);
        Assert.Single(state.Transcript);
        Assert.Equal(2, state.CostTranscript.Length);
        Assert.NotNull(state.Pending);
        while (!state.AtBoundary)
        {
            if (state.CostPending is not null)
                state = SemanticCombatDrawEngineV111.Resolve(
                    state, state.Fingerprint, 0);
            else
                state = SemanticCombatDrawEngineV111.Resolve(
                    state, state.Fingerprint, 0);
        }
        // Snecko Eye also increases the number of cards drawn per hand.
        Assert.Equal(state.RequestedCount,
            state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
        Assert.Equal(PrototypeGameEngine.SemanticHandDrawStepIndex + 1,
            state.Run.World.Combat.AutomaticPipelineContinuation!.NextStepIndex);
        Assert.Equal(originalRng, CanonicalJson.Sha256(state.Run.Rng));
        Assert.Equal(state.Fingerprint, SemanticCombatDrawEngineV111.Restore(
            SemanticCombatDrawEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void RealHandDrawRecyclesWithDeferredEpochAndNoRepeatedBeforeHandDraw()
    {
        var source = SavedHandDrawCheckpoint(
            "nature-auto-recycle", reducedDraw: 2);
        var state = SemanticCombatDrawEngineV111.BeginAutomaticHandDraw(source);
        Assert.Equal(0, state.Run.World!.Combat!.SemanticDeck!.ShuffleEpoch);
        for (int i = 0; i < 2; i++)
            state = SemanticCombatDrawEngineV111.Resolve(
                state, state.Fingerprint, 0);
        Assert.Equal(1, state.Run.World!.Combat!.SemanticDeck!.ShuffleEpoch);
        Assert.NotNull(state.Pending);
        while (!state.AtBoundary)
            state = SemanticCombatDrawEngineV111.Resolve(
                state, state.Fingerprint, 0);
        Assert.Equal(PrototypeContent.Rules.HandSize,
            state.Run.World!.Combat!.Hand.Length);
        Assert.Equal(PrototypeContent.Rules.HandSize,
            state.Run.World.Combat.CounterState.CardsDrawnThisCombat);
        Assert.Equal(state.Fingerprint, SemanticCombatDrawEngineV111.Restore(
            SemanticCombatDrawEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void ForgedAutomaticPipelineCursorAndParentFingerprintFailClosed()
    {
        var source = SavedHandDrawCheckpoint("nature-auto-invalid");
        var first = SemanticCombatDrawEngineV111.BeginAutomaticHandDraw(source);
        var fp = first.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            SemanticCombatDrawEngineV111.Resolve(first, "stale", 0));
        var altered = first.Fork();
        var combat = altered.Run.World!.Combat!;
        altered = altered with
        {
            Run = altered.Run with
            {
                World = altered.Run.World! with
                {
                    Combat = combat with
                    {
                        AutomaticPipelineContinuation =
                            combat.AutomaticPipelineContinuation! with
                            {
                                NextStepIndex = -1
                            }
                    }
                }
            }
        };
        Assert.Throws<InvalidDataException>(() =>
            SemanticCombatDrawEngineV111.Validate(altered));
        Assert.Equal(fp, first.Fingerprint);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticCombatDrawEngineV111.BeginAutomaticHandDraw(
                source with
                {
                    World = source.World! with
                    {
                        Combat = source.World.Combat! with
                        {
                            AutomaticPipelineContinuation = null
                        }
                    }
                }));
    }

    [Fact]
    public void FifteenWorkersReproduceRealAutomaticHandDrawChoices()
    {
        static string Rollout(int worker)
        {
            var state = SemanticCombatDrawEngineV111.BeginAutomaticHandDraw(
                SavedHandDrawCheckpoint("nature-auto-15-" + worker));
            var events = new List<string>();
            while (!state.AtBoundary)
            {
                events.Add(state.Pending?.EventId ?? state.CostEventId!);
                state = SemanticCombatDrawEngineV111.Resolve(
                    state, state.Fingerprint,
                    worker % (state.Pending?.CandidateCardInstanceIds.Length ??
                        state.CostPending!.SupportCount));
            }
            Assert.Equal(state.RequestedCount,
                state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
            Assert.Equal(state.Fingerprint, SemanticCombatDrawEngineV111.Restore(
                SemanticCombatDrawEngineV111.Save(state)).Fingerprint);
            return state.Fingerprint + ":" + string.Join("|", events);
        }
        var sequential = Enumerable.Range(0, 15).Select(Rollout).ToArray();
        var concurrent = new string[15];
        Parallel.For(0, 15, w => concurrent[w] = Rollout(w));
        Assert.Equal(sequential, concurrent);
    }
}
