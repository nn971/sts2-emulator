using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticCombatDrawV111Tests
{
    private static RunState SourceCombat(string seed)
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
        // Begin constructs the real native-shaped combat card instances,
        // with a complete unshuffled draw pile. No legacy swap tickets are
        // consumed: the new semantic Nature deck owns its random order.
        return ControlledPlayableCombatEngineV111.Begin(intents, seed).Run;
    }

    [Fact]
    public void RealDrawCardsStopsBeforeUnknownIdentityAndFiresEachDrawHookOnce()
    {
        var source = SourceCombat("semantic-real-three-draws");
        var initial = source.World!.Combat!;
        var state = SemanticCombatDrawEngineV111.Begin(source, 3);
        Assert.NotNull(state.Pending);
        Assert.Equal("combat.draw_pile.draw_card", state.Pending!.SiteId);
        Assert.Equal(SemanticDeckStateV111.SchemaId, state.Pending.SchemaId);
        Assert.Equal(initial.DrawPile.Length,
            state.Pending.CandidateCardInstanceIds.Length);
        Assert.Equal("UNVERIFIED", state.Pending.ProbabilityStatus);
        Assert.Equal(initial.CounterState.CardsDrawnThisCombat,
            state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
        Assert.Empty(state.Run.World!.Combat!.Hand);
        Assert.Equal(source.DecisionIndex, state.Run.DecisionIndex);
        var prior = state.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            SemanticCombatDrawEngineV111.Resolve(state, "stale", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SemanticCombatDrawEngineV111.Resolve(
                state, prior, state.Pending.CandidateCardInstanceIds.Length));
        var selected = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            var boundary = state.Pending!;
            var position = boundary.CandidateCardInstanceIds.Length - 1;
            selected.Add(boundary.CandidateCardInstanceIds[position]);
            state = SemanticCombatDrawEngineV111.Resolve(
                state, state.Fingerprint, position);
            Assert.Equal(initial.CounterState.CardsDrawnThisCombat + i + 1,
                state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
            Assert.Equal(i + 1, state.Run.World!.Combat!.Hand.Length);
            Assert.Equal(selected, state.Run.World!.Combat!.Hand);
            Assert.Equal(initial.DrawPile.Length - i - 1,
                state.Run.World!.Combat!.DrawPile.Length);
            Assert.Equal(i + 1, state.Transcript.Length);
        }
        Assert.True(state.AtBoundary);
        Assert.Null(state.Pending);
        Assert.Equal(source.DecisionIndex, state.Run.DecisionIndex);
        Assert.Empty(source.World!.Combat!.Hand);
        Assert.Equal(initial.DrawPile.Length, source.World!.Combat!.DrawPile.Length);
        Assert.Equal(3, state.Transcript.Select(x =>
            x.SelectedCardInstanceId).Distinct().Count());
        Assert.All(state.Transcript, x =>
            Assert.Equal("UNVERIFIED", x.ProbabilityStatus));
    }

    [Fact]
    public void InterruptedCombatDrawCanForkSaveAndReplayFromOriginalMechanics()
    {
        var source = SourceCombat("semantic-real-snapshot");
        var initial = SemanticCombatDrawEngineV111.Begin(source, 4);
        var saved = SemanticCombatDrawEngineV111.Save(initial);
        var restored = SemanticCombatDrawEngineV111.Restore(saved);
        Assert.Equal(initial.Fingerprint, restored.Fingerprint);
        Assert.Equal(initial.Pending!.EventId, restored.Pending!.EventId);
        var left = SemanticCombatDrawEngineV111.Resolve(initial,
            initial.Fingerprint, 0);
        var right = SemanticCombatDrawEngineV111.Resolve(initial,
            initial.Fingerprint, 1);
        Assert.NotEqual(left.Fingerprint, right.Fingerprint);
        Assert.Equal(initial.Fingerprint,
            SemanticCombatDrawEngineV111.Restore(saved).Fingerprint);
        var leftReplayed = SemanticCombatDrawEngineV111.Restore(
            SemanticCombatDrawEngineV111.Save(left));
        Assert.Equal(left.Fingerprint, leftReplayed.Fingerprint);
        Assert.Equal(left.Pending!.EventId, leftReplayed.Pending!.EventId);
        while (!left.AtBoundary)
        {
            left = SemanticCombatDrawEngineV111.Resolve(
                left, left.Fingerprint, 0);
            Assert.Equal(left.Fingerprint,
                SemanticCombatDrawEngineV111.Restore(
                    SemanticCombatDrawEngineV111.Save(left)).Fingerprint);
        }
        Assert.Equal(4, left.Run.World!.Combat!.Hand.Length);
        Assert.Equal(4, left.Transcript.Length);
        Assert.Equal(initial.Fingerprint, restored.Fingerprint);
    }

    [Fact]
    public void TamperedMembershipAndTranscriptFailWithoutSideEffects()
    {
        var original = SemanticCombatDrawEngineV111.Begin(
            SourceCombat("semantic-real-corrupt"), 2);
        var tampered = original.Fork();
        var tamperedRun = tampered.Run;
        var tamperedWorld = tamperedRun.World!;
        var tamperedCombat = tamperedWorld.Combat!;
        var deck = tamperedCombat.SemanticDeck!;
        var pending = deck.Pending!;
        var candidateIds = pending.CandidateCardInstanceIds;
        candidateIds[0] = -999;
        tampered = tampered with
        {
            Run = tamperedRun with
            {
                World = tamperedWorld with
                {
                    Combat = tamperedCombat with
                    {
                        SemanticDeck = deck with
                        {
                            Pending = pending with
                            {
                                CandidateCardInstanceIds = candidateIds
                            }
                        }
                    }
                }
            }
        };
        Assert.Throws<InvalidDataException>(() =>
            SemanticCombatDrawEngineV111.Restore(JsonSerializer.Serialize(tampered)));
        var changed = original.Fork();
        var changedCombat = changed.Run.World!.Combat!;
        var changedDrawPile = changedCombat.DrawPile;
        changedDrawPile[0] = -100;
        Assert.Throws<InvalidOperationException>(() =>
            _ = changedCombat with { DrawPile = changedDrawPile });
        Assert.Equal(original.Fingerprint, changed.Fingerprint);
        var child = SemanticCombatDrawEngineV111.Resolve(
            original, original.Fingerprint, 0);
        var modified = child with
        {
            RemainingCount = 0
        };
        Assert.Throws<InvalidDataException>(() =>
            SemanticCombatDrawEngineV111.Restore(JsonSerializer.Serialize(modified)));
        Assert.Equal(original.Fingerprint,
            SemanticCombatDrawEngineV111.Restore(
                SemanticCombatDrawEngineV111.Save(original)).Fingerprint);
    }

    [Fact]
    public void PerCardRealDrawEnumerationCoversOrderedDistinctInstances()
    {
        var source = SourceCombat("semantic-real-two-draw-enumeration");
        // Make the physical pile exactly three existing source instances,
        // discarding the remainder as a conditioned fixture.
        var combat = source.World!.Combat!;
        var chosen = combat.DrawPile.Take(3).ToArray();
        source = source with
        {
            World = source.World with
            {
                Combat = combat with
                {
                    DrawPile = chosen,
                    DiscardPile = combat.DrawPile.Skip(3).ToArray()
                }
            }
        };
        var root = SemanticCombatDrawEngineV111.Begin(source, 2);
        var pairs = new HashSet<string>();
        for (int i = 0; i < 3; i++)
        {
            var first = SemanticCombatDrawEngineV111.Resolve(
                root, root.Fingerprint, i);
            for (int j = 0; j < 2; j++)
            {
                var second = SemanticCombatDrawEngineV111.Resolve(
                    first, first.Fingerprint, j);
                Assert.True(second.AtBoundary);
                Assert.Equal(2, second.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
                Assert.Equal(2, second.Run.World!.Combat!.Hand.Length);
                pairs.Add(string.Join(",", second.Run.World!.Combat!.Hand));
            }
        }
        Assert.Equal(6, pairs.Count);
        Assert.All(pairs, text =>
        {
            var two = text.Split(',').Select(long.Parse).ToArray();
            Assert.Equal(2, two.Distinct().Count());
            Assert.All(two, id => Assert.Contains(id, chosen));
        });
    }

    [Fact]
    public void FifteenWorkersKeepSemanticRealDrawHooksAndEventIdsIndependent()
    {
        static string Run(int worker)
        {
            var state = SemanticCombatDrawEngineV111.Begin(
                SourceCombat("semantic-real-worker-" + worker), 4);
            var ids = new List<string>();
            while (!state.AtBoundary)
            {
                var pending = state.Pending!;
                ids.Add(pending.EventId);
                state = SemanticCombatDrawEngineV111.Resolve(
                    state, state.Fingerprint,
                    worker % pending.CandidateCardInstanceIds.Length);
            }
            Assert.Equal(4, state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
            return state.Fingerprint + ":" + string.Join("|", ids);
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var concurrent = new string[15];
        Parallel.For(0, 15, i => concurrent[i] = Run(i));
        Assert.Equal(serial, concurrent);
    }


    [Fact]
    public void RealDrawRecycleStartsFreshNatureEpochAfterEarlierCardDrawnHooks()
    {
        var source = SourceCombat("semantic-recycle-real-events");
        var combat = source.World!.Combat!;
        var original = combat.DrawPile.ToArray();
        source = source with
        {
            World = source.World with
            {
                Combat = combat with
                {
                    DrawPile = original.Take(2).ToArray(),
                    DiscardPile = original.Skip(2).ToArray()
                }
            }
        };
        var expectedRng = CanonicalJson.Sha256(source.Rng);
        var state = SemanticCombatDrawEngineV111.Begin(source, 4);
        var firstEpochIds = new HashSet<string>();
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(0, state.Run.World!.Combat!.SemanticDeck!.ShuffleEpoch);
            firstEpochIds.Add(state.Pending!.EventId);
            // The second draw has only one eligible card remaining.
            state = SemanticCombatDrawEngineV111.Resolve(
                state, state.Fingerprint, 0);
            Assert.Equal(i + 1,
                state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
        }

        var newCombat = state.Run.World!.Combat!;
        Assert.Equal(1, newCombat.SemanticDeck!.ShuffleEpoch);
        Assert.Empty(newCombat.DiscardPile);
        Assert.Equal(original.Length - 2, newCombat.DrawPile.Length);
        Assert.Equal(original.Length - 2,
            state.Pending!.CandidateCardInstanceIds.Length);
        Assert.DoesNotContain(state.Pending.EventId, firstEpochIds);
        Assert.Equal(2, newCombat.Hand.Length);
        Assert.Equal(2, state.Transcript.Length);
        for (int i = 2; i < 4; i++)
        {
            state = SemanticCombatDrawEngineV111.Resolve(
                state, state.Fingerprint, i % state.Pending!.CandidateCardInstanceIds.Length);
            Assert.Equal(i + 1,
                state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
        }
        Assert.True(state.AtBoundary);
        Assert.Equal(4, state.Run.World!.Combat!.Hand.Length);
        Assert.Equal(4, state.Run.World.Combat.Hand.Distinct().Count());
        Assert.Equal(expectedRng, CanonicalJson.Sha256(state.Run.Rng));
        Assert.Equal(state.Fingerprint,
            SemanticCombatDrawEngineV111.Restore(
                SemanticCombatDrawEngineV111.Save(state)).Fingerprint);
        Assert.Empty(source.World!.Combat!.Hand);
        Assert.Equal(2, source.World.Combat.DrawPile.Length);
    }

    [Fact]
    public void RealPerfectFitShufflePinsTheEnchantedCardBeforeRemainingUnknowns()
    {
        var source = SourceCombat("semantic-perfect-fit-recycle");
        var combat = source.World!.Combat!;
        var original = combat.DrawPile;
        var perfectId = original[3];
        var deck = source.Player.Deck.Select(card =>
            card.InstanceId == perfectId
                ? card with
                {
                    Enchantment = new PrototypeCardEnchantment(
                        PrototypeCardEnchantmentKind.PerfectFit)
                }
                : card).ToArray();
        source = source with
        {
            Player = source.Player with { Deck = deck },
            World = source.World with
            {
                Combat = combat with
                {
                    DrawPile = original.Take(2).ToArray(),
                    DiscardPile = original.Skip(2).ToArray(),
                    Cards = combat.Cards.Select(c => c.InstanceId == perfectId
                        ? c with
                        {
                            Enchantment = new PrototypeCardEnchantment(
                                PrototypeCardEnchantmentKind.PerfectFit)
                        }
                        : c).ToArray()
                }
            }
        };
        var state = SemanticCombatDrawEngineV111.Begin(source, 4);
        state = SemanticCombatDrawEngineV111.Resolve(
            state, state.Fingerprint, 0);
        state = SemanticCombatDrawEngineV111.Resolve(
            state, state.Fingerprint, 0);
        Assert.Equal(1, state.Run.World!.Combat!.SemanticDeck!.ShuffleEpoch);
        Assert.Equal(new long[] { perfectId },
            state.Pending!.CandidateCardInstanceIds);
        var relicDraw = SemanticCombatDrawEngineV111.Resolve(
            state, state.Fingerprint, 0);
        Assert.Equal(perfectId, relicDraw.Run.World!.Combat!.Hand[^1]);
        Assert.Equal(original.Length - 3,
            relicDraw.Pending!.CandidateCardInstanceIds.Length);
        Assert.DoesNotContain(perfectId,
            relicDraw.Pending.CandidateCardInstanceIds);
        var replay = SemanticCombatDrawEngineV111.Restore(
            SemanticCombatDrawEngineV111.Save(relicDraw));
        Assert.Equal(relicDraw.Fingerprint, replay.Fingerprint);
        var done = SemanticCombatDrawEngineV111.Resolve(
            relicDraw, relicDraw.Fingerprint, 0);
        Assert.True(done.AtBoundary);
        Assert.Equal(4, done.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
    }

    [Fact]
    public void FifteenWorkersHaveScheduleIndependentRealRecycleEpochs()
    {
        static string Rollout(int worker)
        {
            var source = SourceCombat("semantic-real-recycle-15-" + worker);
            var combat = source.World!.Combat!;
            var ids = combat.DrawPile;
            source = source with
            {
                World = source.World with
                {
                    Combat = combat with
                    {
                        DrawPile = ids.Take(2).ToArray(),
                        DiscardPile = ids.Skip(2).ToArray()
                    }
                }
            };
            var state = SemanticCombatDrawEngineV111.Begin(source, 5);
            var events = new List<string>();
            while (!state.AtBoundary)
            {
                var pending = state.Pending!;
                events.Add(pending.EventId);
                state = SemanticCombatDrawEngineV111.Resolve(
                    state, state.Fingerprint,
                    worker % pending.CandidateCardInstanceIds.Length);
            }
            Assert.Equal(5, events.Count);
            Assert.Equal(1, state.Run.World!.Combat!.SemanticDeck!.ShuffleEpoch);
            Assert.Equal(5, state.Run.World.Combat.CounterState.CardsDrawnThisCombat);
            Assert.Equal(state.Fingerprint,
                SemanticCombatDrawEngineV111.Restore(
                    SemanticCombatDrawEngineV111.Save(state)).Fingerprint);
            return state.Fingerprint + ":" + string.Join("|", events);
        }
        var serial = Enumerable.Range(0, 15).Select(Rollout).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, worker => parallel[worker] = Rollout(worker));
        Assert.Equal(serial, parallel);
    }

    [Fact]
    public void SemanticRecycleSuspendsAtStratagemWithoutMutatingSource()
    {
        var source = SourceCombat("semantic-recycle-stratagem");
        var combat = source.World!.Combat!;
        source = source with
        {
            World = source.World with
            {
                Combat = combat with
                {
                    DrawPile = [],
                    DiscardPile = combat.DrawPile.ToArray(),
                    PlayerPowers = combat.PlayerPowers.Append(
                        new PrototypePowerInstanceState(
                            "proto.power.stratagem", 1, 0)).ToArray()
                }
            }
        };
        var fingerprint = CanonicalJson.Sha256(source);
        var pending = SemanticCombatDrawEngineV111.Begin(source, 1);
        Assert.NotNull(pending.StratagemSelectionPending);
        Assert.Null(pending.Pending);
        Assert.Equal(fingerprint, CanonicalJson.Sha256(source));
    }

    [Fact]
    public void SneckoDrawCostNowResumesIntoTheNormalDrawnEvent()
    {
        var source = SourceCombat("semantic-real-snecko-cost");
        var combat = source.World!.Combat!;
        const string relicId = "proto.relic.snecko_eye";
        var nextOrder = combat.RelicStates
            .Select(relic => relic.ApplicationOrder)
            .Concat(combat.PlayerPowers.Select(power => power.ApplicationOrder))
            .Concat(combat.Enemies.SelectMany(enemy => enemy.PowerStates)
                .Select(power => power.ApplicationOrder))
            .DefaultIfEmpty(0).Max() + 1;
        var definition = PrototypeContent.Relic(relicId);
        combat = combat with
        {
            Relics = combat.RelicStates.Append(new CombatRelicState(
                combat.RelicStates.Length, relicId, nextOrder,
                new int[(definition.Triggers ?? []).Length])).ToArray(),
            NextPowerApplicationOrder = Math.Max(
                combat.NextPowerApplicationOrder, nextOrder + 1)
        };
        source = source with
        {
            Player = source.Player with
            {
                Relics = source.Player.Relics.Append(
                    new RelicInstance(relicId,
                        PrototypeJson.EmptyObject())).ToArray()
            },
            World = source.World with { Combat = combat }
        };
        var nativeBefore = CanonicalJson.Sha256(source.Rng);
        var state = SemanticCombatDrawEngineV111.Begin(source, 2);
        Assert.NotNull(state.Pending);
        Assert.Null(state.CostPending);
        var cardId = state.Pending!.CandidateCardInstanceIds[0];
        state = SemanticCombatDrawEngineV111.Resolve(
            state, state.Fingerprint, 0);
        Assert.Null(state.Pending);
        Assert.Equal("combat.card_draw.relic_energy_cost",
            state.CostPending!.SiteId);
        Assert.Equal(4, state.CostPending.SupportCount);
        Assert.Contains(cardId, state.Run.World!.Combat!.Hand);
        Assert.Equal(combat.CounterState.CardsDrawnThisCombat,
            state.Run.World.Combat.CounterState.CardsDrawnThisCombat);
        var pending = state.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            SemanticCombatDrawEngineV111.Resolve(state, "stale", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SemanticCombatDrawEngineV111.Resolve(state, pending, 4));
        state = SemanticCombatDrawEngineV111.Resolve(state, pending, 2);
        Assert.Equal(2, state.Run.World!.Combat!.Cards
            .Single(c => c.InstanceId == cardId).TemporaryEnergyCost!.Cost);
        Assert.Equal(combat.CounterState.CardsDrawnThisCombat + 1,
            state.Run.World.Combat.CounterState.CardsDrawnThisCombat);
        Assert.Single(state.CostTranscript);
        Assert.NotNull(state.Pending); // the second card is not yet chosen
        state = SemanticCombatDrawEngineV111.Resolve(state, state.Fingerprint, 0);
        Assert.NotNull(state.CostPending);
        state = SemanticCombatDrawEngineV111.Resolve(state, state.Fingerprint, 1);
        Assert.True(state.AtBoundary);
        Assert.Equal(2, state.Transcript.Length);
        Assert.Equal(2, state.CostTranscript.Length);
        Assert.Equal(nativeBefore, CanonicalJson.Sha256(state.Run.Rng));
        Assert.Equal(state.Fingerprint,
            SemanticCombatDrawEngineV111.Restore(
                SemanticCombatDrawEngineV111.Save(state)).Fingerprint);
        Assert.Empty(source.World!.Combat!.Hand);
    }

}
