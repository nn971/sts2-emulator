using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledDrawCostV111Tests
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
        var playable = ControlledPlayableCombatEngineV111.Begin(intents, seed);
        while (!playable.AtPlayerBoundary)
            playable = ControlledPlayableCombatEngineV111.Resolve(
                playable, playable.Fingerprint, 0);
        return playable;
    }

    private static ControlledPlayableCombatV111 BeginCost(
        string seed, bool snecko, bool slither)
    {
        var original = Prepared(seed);
        var run = original.Run.Fork();
        var world = run.World!;
        var combat = world.Combat!;
        var firstDrawn = combat.DrawPile[^1];
        var cards = combat.Cards.Select(card =>
            card.InstanceId == firstDrawn && slither
                ? card with
                {
                    Enchantment = new PrototypeCardEnchantment(
                        PrototypeCardEnchantmentKind.Slither)
                }
                : card).ToArray();
        var deck = run.Player.Deck.Select(card =>
            card.InstanceId == firstDrawn && slither
                ? card with
                {
                    Enchantment = new PrototypeCardEnchantment(
                        PrototypeCardEnchantmentKind.Slither)
                }
                : card).ToArray();
        var player = snecko
            ? run.Player with
            {
                Relics = run.Player.Relics.Append(new RelicInstance(
                    "proto.relic.snecko_eye", PrototypeJson.EmptyObject())).ToArray()
            }
            : run.Player;
        if (snecko)
        {
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
        }
        var state = original with
        {
            Run = run with
            {
                Player = player with { Deck = deck },
                World = world with { Combat = combat with { Cards = cards } }
            }
        };
        var action = new PrototypeAiEnvironment().Observe(state.Run)
            .LegalActions.Single(x => x.Kind == "end_turn");
        state = ControlledPlayableCombatEngineV111.Step(state, action.ActionId);
        for (int i = 0; i < 30 &&
            NatureBoundaryMetadataV111.Describe(state).Kind != "drawn_card_cost"; i++)
        {
            if (state.ActivePending is null)
                throw new InvalidOperationException(
                    "Hand draw ended without reaching its cost ticket.");
            state = ControlledPlayableCombatEngineV111.Resolve(
                state, state.Fingerprint, 0);
        }
        Assert.Equal("drawn_card_cost",
            NatureBoundaryMetadataV111.Describe(state).Kind);
        return state;
    }

    [Fact]
    public void SlitherRollOccursAfterPhysicalDrawBeforeCardDrawn()
    {
        var pending = BeginCost("nature-drawn-slither", snecko: false,
            slither: true);
        var cost = pending.Run.World!.Combat!.PendingChoice!.ControlledDrawCost!;
        var cardId = cost.CardInstanceId;
        Assert.Contains(cardId, pending.Run.World.Combat.Hand);
        Assert.DoesNotContain(cardId, pending.Run.World.Combat.DrawPile);
        Assert.Equal("slither", cost.Stage);
        Assert.Equal("combat.card_draw.slither_energy_cost",
            NatureBoundaryMetadataV111.Describe(pending).Pending!.SiteId);
        Assert.Equal(4, pending.ActivePending!.SupportCount);
        var before = pending.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(pending, "stale", 1));
        Assert.Throws<ArgumentException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(pending, before, 4));
        var finished = ControlledPlayableCombatEngineV111.Resolve(
            pending, before, 3);
        Assert.Equal(before, pending.Fingerprint);
        Assert.Equal(3, finished.Run.World!.Combat!.Cards.Single(c =>
            c.InstanceId == cardId).TemporaryEnergyCost!.Cost);
        Assert.Single(finished.DrawCostTranscript);
        Assert.Equal(pending.Run.DecisionIndex, finished.Run.DecisionIndex);
    }

    [Fact]
    public void SneckoThenSlitherUseTwoDifferentTicketsOnOneCard()
    {
        var first = BeginCost("nature-drawn-both", snecko: true,
            slither: true);
        var context = first.Run.World!.Combat!.PendingChoice!.ControlledDrawCost!;
        Assert.Equal("relic", context.Stage);
        Assert.Equal("combat.card_draw.relic_energy_cost",
            NatureBoundaryMetadataV111.Describe(first).Pending!.SiteId);
        var cardId = context.CardInstanceId;
        Assert.Equal(4, first.ActivePending!.SupportCount);
        var drawsBefore = first.Run.World!.Combat!.CounterState.CardsDrawnThisCombat;
        var next = ControlledPlayableCombatEngineV111.Resolve(
            first, first.Fingerprint, 1);
        // A second cost roll belongs to the same drawn card: its hooks
        // cannot run before both cost choices have been committed.
        Assert.Equal(drawsBefore,
            next.Run.World!.Combat!.CounterState.CardsDrawnThisCombat);
        Assert.Equal("slither",
            next.Run.World!.Combat!.PendingChoice!.ControlledDrawCost!.Stage);
        Assert.Equal(cardId,
            next.Run.World!.Combat!.PendingChoice!.ControlledDrawCost!.CardInstanceId);
        Assert.Equal("combat.card_draw.slither_energy_cost",
            NatureBoundaryMetadataV111.Describe(next).Pending!.SiteId);
        Assert.Equal(1, next.Run.World.Combat.Cards.Single(c =>
            c.InstanceId == cardId).TemporaryEnergyCost!.Cost);
        var final = ControlledPlayableCombatEngineV111.Resolve(
            next, next.Fingerprint, 3);
        Assert.Equal(3, final.Run.World!.Combat!.Cards.Single(c =>
            c.InstanceId == cardId).TemporaryEnergyCost!.Cost);
        Assert.True(final.Run.World!.Combat!.CounterState.CardsDrawnThisCombat
            >= drawsBefore + 1);
        Assert.Equal(2, final.DrawCostTranscript.Length);
        Assert.Equal(2, final.LiveTranscript
            .Count(x => x.Request.SiteId.StartsWith("combat.card_draw.")));
        Assert.Equal(next.Run.DecisionIndex, final.Run.DecisionIndex);
        Assert.Equal(cardId, first.Run.World!.Combat!.PendingChoice!
            .ControlledDrawCost!.CardInstanceId);
    }

    [Fact]
    public void ForgedCardIdentityAndStageFailBeforeMutation()
    {
        var pending = BeginCost("nature-drawn-forge", snecko: true,
            slither: true);
        var forged = pending.Fork();
        var old = forged.Run.World!.Combat!.PendingChoice!;
        forged = forged with
        {
            Run = forged.Run with
            {
                World = forged.Run.World! with
                {
                    Combat = forged.Run.World.Combat! with
                    {
                        PendingChoice = old with
                        {
                            ControlledDrawCost = old.ControlledDrawCost! with
                            {
                                CardInstanceId = -1
                            }
                        }
                    }
                }
            }
        };
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.VerifyStage(forged));
        Assert.Equal("relic",
            pending.Run.World!.Combat!.PendingChoice!.ControlledDrawCost!.Stage);
    }

    [Fact]
    public void FifteenWorkersReplayDrawnCostTicketsIndependently()
    {
        static string Run(int i)
        {
            var state = BeginCost("nature-draw-cost-15-" + i,
                snecko: true, slither: true);
            var ids = new List<string>();
            for (int n = 0; n < 2; n++)
            {
                var view = NatureBoundaryMetadataV111.Describe(state);
                ids.Add(view.Pending!.EventId);
                state = ControlledPlayableCombatEngineV111.Resolve(
                    state, state.Fingerprint, (i + n) % view.Pending.SupportCount);
            }
            return state.Fingerprint + ":" + string.Join("|", ids);
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Run(i));
        Assert.Equal(serial, parallel);
    }
}
