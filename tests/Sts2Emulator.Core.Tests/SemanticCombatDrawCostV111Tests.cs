using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticCombatDrawCostV111Tests
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
            intents = ControlledIntentsV111.Resolve(
                intents, intents.Fingerprint, 0);
        return ControlledPlayableCombatEngineV111.Begin(intents, seed).Run;
    }

    private static SemanticCombatDrawV111 Start(
        string seed, bool relic, bool slither, int count = 1,
        bool fromHandDraw = true)
    {
        var run = SourceCombat(seed);
        var world = run.World!;
        var combat = world.Combat!;
        // The first deferred card support candidate has a known instance ID
        // but its *position* remains Nature-private and unresolved.
        var firstId = combat.DrawPile[0];
        var cards = combat.Cards.Select(card =>
            slither && card.InstanceId == firstId
                ? card with
                {
                    Enchantment = new PrototypeCardEnchantment(
                        PrototypeCardEnchantmentKind.Slither)
                } : card).ToArray();
        var deck = run.Player.Deck.Select(card =>
            slither && card.InstanceId == firstId
                ? card with
                {
                    Enchantment = new PrototypeCardEnchantment(
                        PrototypeCardEnchantmentKind.Slither)
                } : card).ToArray();
        var player = run.Player;
        if (relic)
        {
            const string relicId = "proto.relic.snecko_eye";
            var nextOrder = combat.RelicStates
                .Select(item => item.ApplicationOrder)
                .Concat(combat.PlayerPowers.Select(power => power.ApplicationOrder))
                .Concat(combat.Enemies.SelectMany(enemy => enemy.PowerStates)
                    .Select(power => power.ApplicationOrder))
                .DefaultIfEmpty(0).Max() + 1;
            var definition = PrototypeContent.Relic(relicId);
            player = player with
            {
                Relics = player.Relics.Append(new RelicInstance(
                    relicId, PrototypeJson.EmptyObject())).ToArray()
            };
            combat = combat with
            {
                Relics = combat.RelicStates.Append(new CombatRelicState(
                    combat.RelicStates.Length, relicId, nextOrder,
                    new int[(definition.Triggers ?? []).Length])).ToArray(),
                NextPowerApplicationOrder = Math.Max(
                    combat.NextPowerApplicationOrder, nextOrder + 1)
            };
        }
        run = run with
        {
            Player = player with { Deck = deck },
            World = world with { Combat = combat with { Cards = cards } }
        };
        return SemanticCombatDrawEngineV111.Begin(run, count, fromHandDraw);
    }

    [Fact]
    public void SneckoThenSlitherRetainsSelectedCardAndDelaysCardDrawnUntilBothCosts()
    {
        var first = Start("semantic-cost-both", relic: true, slither: true);
        var oldRng = CanonicalJson.Sha256(first.Run.Rng);
        var cardId = first.Pending!.CandidateCardInstanceIds[0];
        var selected = SemanticCombatDrawEngineV111.Resolve(
            first, first.Fingerprint, 0);
        Assert.Equal("relic", selected.Run.World!.Combat!
            .PendingChoice!.ControlledDrawCost!.Stage);
        Assert.Equal("combat.card_draw.relic_energy_cost",
            selected.CostPending!.SiteId);
        Assert.Equal(4, selected.CostPending.SupportCount);
        Assert.Contains(cardId, selected.Run.World.Combat.Hand);
        Assert.DoesNotContain(cardId, selected.Run.World.Combat.DrawPile);
        Assert.Equal(0, selected.Run.World.Combat.CounterState.CardsDrawnThisCombat);
        Assert.Null(selected.Pending);
        Assert.Single(selected.Transcript);
        Assert.Empty(selected.CostTranscript);
        Assert.Equal(selected.Fingerprint,
            SemanticCombatDrawEngineV111.Restore(
                SemanticCombatDrawEngineV111.Save(selected)).Fingerprint);

        var snecko = SemanticCombatDrawEngineV111.Resolve(
            selected, selected.Fingerprint, 1);
        Assert.Equal("slither", snecko.Run.World!.Combat!.PendingChoice!
            .ControlledDrawCost!.Stage);
        Assert.Equal("combat.card_draw.slither_energy_cost",
            snecko.CostPending!.SiteId);
        Assert.Equal(1, snecko.Run.World.Combat.Cards
            .Single(c => c.InstanceId == cardId).TemporaryEnergyCost!.Cost);
        Assert.Equal(0, snecko.Run.World.Combat.CounterState.CardsDrawnThisCombat);
        Assert.Single(snecko.CostTranscript);
        Assert.Equal(snecko.Fingerprint,
            SemanticCombatDrawEngineV111.Restore(
                SemanticCombatDrawEngineV111.Save(snecko)).Fingerprint);

        var slither = SemanticCombatDrawEngineV111.Resolve(
            snecko, snecko.Fingerprint, 3);
        Assert.True(slither.AtBoundary);
        Assert.Equal(3, slither.Run.World!.Combat!.Cards
            .Single(c => c.InstanceId == cardId).TemporaryEnergyCost!.Cost);
        Assert.Equal(1, slither.Run.World.Combat.CounterState.CardsDrawnThisCombat);
        Assert.Single(slither.Run.World.Combat.Hand);
        Assert.Equal(cardId, slither.Run.World.Combat.Hand[0]);
        Assert.Equal(2, slither.CostTranscript.Length);
        Assert.Equal(new[]
        {
            "combat.card_draw.relic_energy_cost",
            "combat.card_draw.slither_energy_cost"
        }, slither.CostTranscript.Select(x => x.Request.SiteId).ToArray());
        Assert.Equal(oldRng, CanonicalJson.Sha256(slither.Run.Rng));
        Assert.Equal(slither.Fingerprint,
            SemanticCombatDrawEngineV111.Restore(
                SemanticCombatDrawEngineV111.Save(slither)).Fingerprint);
        Assert.Equal(first.Fingerprint,
            SemanticCombatDrawEngineV111.Restore(
                SemanticCombatDrawEngineV111.Save(first)).Fingerprint);
    }

    [Fact]
    public void SlitherOnlyAndEffectDrawDoNotRequireAnAutomaticPipelineCursor()
    {
        var state = Start("semantic-cost-slither-effect",
            relic: false, slither: true, fromHandDraw: false);
        Assert.True(state.FromHandDraw == false);
        state = SemanticCombatDrawEngineV111.Resolve(
            state, state.Fingerprint, 0);
        Assert.Equal("slither", state.Run.World!.Combat!
            .PendingChoice!.ControlledDrawCost!.Stage);
        Assert.Equal("combat.card_draw.slither_energy_cost",
            state.CostPending!.SiteId);
        Assert.Equal(4, state.CostPending.SupportCount);
        Assert.Equal(0,
            state.Run.World.Combat.CounterState.CardsDrawnThisCombat);
        state = SemanticCombatDrawEngineV111.Resolve(
            state, state.Fingerprint, 2);
        Assert.True(state.AtBoundary);
        Assert.Equal(2, state.Run.World!.Combat!.Cards.Single(c =>
            c.InstanceId == state.Run.World.Combat.Hand[0])
            .TemporaryEnergyCost!.Cost);
        Assert.Single(state.CostTranscript);
        Assert.Equal(1, state.Run.World.Combat.CounterState.CardsDrawnThisCombat);
        Assert.Equal(state.Fingerprint,
            SemanticCombatDrawEngineV111.Restore(
                SemanticCombatDrawEngineV111.Save(state)).Fingerprint);
    }

    [Fact]
    public void ForgedCardAndOutOfRangeCostFailBeforeMutatingParent()
    {
        var state = Start("semantic-cost-invalid",
            relic: true, slither: true);
        state = SemanticCombatDrawEngineV111.Resolve(
            state, state.Fingerprint, 0);
        var unchanged = state.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            SemanticCombatDrawEngineV111.Resolve(state, "stale", 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SemanticCombatDrawEngineV111.Resolve(state, unchanged, 4));
        var forged = state.Fork();
        var combat = forged.Run.World!.Combat!;
        var pending = combat.PendingChoice!;
        forged = forged with
        {
            Run = forged.Run with
            {
                World = forged.Run.World with
                {
                    Combat = combat with
                    {
                        PendingChoice = pending with
                        {
                            ControlledDrawCost =
                                pending.ControlledDrawCost! with
                                {
                                    CardInstanceId = -1000
                                }
                        }
                    }
                }
            }
        };
        Assert.Throws<InvalidDataException>(() =>
            SemanticCombatDrawEngineV111.Validate(forged));
        Assert.Equal(unchanged, state.Fingerprint);
        var resolved = SemanticCombatDrawEngineV111.Resolve(
            state, unchanged, 0);
        Assert.Equal(0,
            resolved.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
        Assert.Equal(unchanged, state.Fingerprint);
    }

    [Fact]
    public void FifteenWorkersReplayTwoCostsAndOneCardInOrder()
    {
        static string Run(int worker)
        {
            var state = Start("semantic-cost-workers-" + worker,
                relic: true, slither: true);
            var seen = new List<string>();
            seen.Add(state.Pending!.EventId);
            state = SemanticCombatDrawEngineV111.Resolve(
                state, state.Fingerprint, 0);
            for (int stage = 0; stage < 2; stage++)
            {
                seen.Add(state.CostEventId!);
                state = SemanticCombatDrawEngineV111.Resolve(
                    state, state.Fingerprint, (worker + stage) % 4);
                Assert.Equal(state.Fingerprint,
                    SemanticCombatDrawEngineV111.Restore(
                        SemanticCombatDrawEngineV111.Save(state)).Fingerprint);
            }
            Assert.True(state.AtBoundary);
            Assert.Equal(1, state.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
            Assert.Single(state.Transcript);
            Assert.Equal(2, state.CostTranscript.Length);
            return state.Fingerprint + ":" + string.Join("|", seen);
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Run(i));
        Assert.Equal(serial, parallel);
    }
}
