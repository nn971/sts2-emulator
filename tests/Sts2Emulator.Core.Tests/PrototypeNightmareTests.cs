using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNightmareTests
{
    [Fact]
    public void NightmareDefinitionMatchesPinnedV01110Semantics()
    {
        var card = PrototypeContent.Card("proto.silent.nightmare");

        Assert.Equal(3, card.Cost.AmountAt(0));
        Assert.Equal(2, card.Cost.AmountAt(1));
        Assert.Equal(PrototypeCardType.Skill, card.Type);
        Assert.Equal(PrototypeCardRarity.Rare, card.Rarity);
        Assert.Equal(PrototypeCardTarget.None, card.Target);
        Assert.True(card.ExhaustOnUse);

        var effect = Assert.Single(card.Effects);
        Assert.Equal(PrototypeCombatEffectKind.ChooseCards, effect.Kind);
        Assert.NotNull(effect.Selection);
        Assert.Equal(PrototypeCardZone.Hand, effect.Selection!.SourceZone);
        Assert.Equal(1, effect.Selection.MinSelections);
        Assert.Equal(1, effect.Selection.MaxSelections);
        Assert.Equal(
            PrototypeCardSelectionResolutionKind.Preserve,
            effect.Selection.Resolution);
        Assert.NotNull(effect.SelectedCardPower);
        Assert.Equal(
            "proto.power.nightmare",
            effect.SelectedCardPower!.PowerId);
        Assert.Equal(3, effect.SelectedCardPower.AmountAt(0));

        var power = PrototypeContent.Power("proto.power.nightmare");
        Assert.True(power.IsInstanced);
        Assert.True(power.RequiresCardPayload);

        var trigger = Assert.Single(power.Triggers);
        Assert.Equal(
            PrototypeCombatEventKind.BeforeHandDraw,
            trigger.EventKind);
        Assert.True(trigger.RemoveSourcePowerAfterTrigger);
        var triggerEffect = Assert.Single(trigger.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.CreateCardsInHandFromPowerCardPayload,
            triggerEffect.Kind);
        Assert.Equal(1, triggerEffect.AmountPerPowerStack);
    }

    [Fact]
    public void NightmareSnapshotsSelectedCardAndCopiesItBeforeHandDraw()
    {
        var selectedState = JsonSerializer.SerializeToElement(
            new { marker = 7 });
        var nightmare = Card(1, "proto.silent.nightmare");
        var selected = Card(2, "proto.silent.defend", upgradeLevel: 1) with
        {
            State = selectedState,
            CombatEnergyCostDelta = -1
        };
        var drawPile = Enumerable.Range(3, 5)
            .Select(id => Card(id, "proto.silent.strike"))
            .ToArray();

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 3,
            hand: [nightmare, selected],
            drawPile: drawPile);

        state = PlayCard(engine, state, nightmare.InstanceId);
        state = SelectOnly(engine, state, selected.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Contains(selected.InstanceId, combat.Hand);
        Assert.Contains(nightmare.InstanceId, combat.ExhaustPile);

        var power = Assert.Single(
            combat.PlayerPowers,
            item => item.PowerId == "proto.power.nightmare");
        Assert.Equal(3, power.Stacks);
        Assert.NotNull(power.CardPayload);
        Assert.Equal(selected.CardId, power.CardPayload!.CardId);
        Assert.Equal(1, power.CardPayload.UpgradeLevel);
        Assert.Equal(-1, power.CardPayload.CombatEnergyCostDelta);
        Assert.Equal(
            7,
            power.CardPayload.State.GetProperty("marker").GetInt32());

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;

        Assert.Equal(2, combat.Turn);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            item => item.PowerId == "proto.power.nightmare");

        var generated = combat.Hand
            .Select(instanceId => combat.Cards.Single(
                card => card.InstanceId == instanceId))
            .Where(card =>
                card.IsTemporary
                && card.CardId == "proto.silent.defend")
            .ToArray();

        Assert.Equal(3, generated.Length);
        Assert.All(generated, card =>
        {
            Assert.Equal(1, card.UpgradeLevel);
            Assert.Equal(-1, card.CombatEnergyCostDelta);
            Assert.Equal(
                7,
                card.State.GetProperty("marker").GetInt32());
        });

        // Native NightmarePower runs in BeforeHandDraw. Three stored copies
        // therefore coexist with the ordinary five-card draw.
        Assert.Equal(8, combat.Hand.Length);
    }

    [Fact]
    public void BurstNightmareKeepsIndependentInstancedPayloads()
    {
        var nightmare = Card(1, "proto.silent.nightmare");
        var defend = Card(2, "proto.silent.defend");
        var strike = Card(3, "proto.silent.strike");

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 3,
            hand: [nightmare, defend, strike],
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.burst",
                    1,
                    1)
            ]);

        state = PlayCard(engine, state, nightmare.InstanceId);
        state = SelectOnly(engine, state, defend.InstanceId);

        var afterFirstChoice = state.World!.Combat!;
        var firstPower = Assert.Single(
            afterFirstChoice.PlayerPowers,
            item => item.PowerId == "proto.power.nightmare");
        Assert.Equal(
            defend.CardId,
            firstPower.CardPayload!.CardId);
        Assert.NotNull(afterFirstChoice.PendingChoice);

        state = SelectOnly(engine, state, strike.InstanceId);
        var combat = state.World!.Combat!;
        var nightmarePowers = combat.PlayerPowers
            .Where(item => item.PowerId == "proto.power.nightmare")
            .OrderBy(item => item.ApplicationOrder)
            .ToArray();

        Assert.Equal(2, nightmarePowers.Length);
        Assert.Equal(
            [defend.CardId, strike.CardId],
            nightmarePowers
                .Select(item => item.CardPayload!.CardId)
                .ToArray());
        Assert.Equal(
            2,
            nightmarePowers
                .Select(item => item.ApplicationOrder)
                .Distinct()
                .Count());
        Assert.Contains(nightmare.InstanceId, combat.ExhaustPile);
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static RunState SelectOnly(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "select_cards"
                && item.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([cardInstanceId]));
        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long instanceId,
        string cardId,
        int upgradeLevel = 0) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            upgradeLevel,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateState(
        int energy,
        CombatCardInstance[] hand,
        CombatCardInstance[]? drawPile = null,
        PrototypePowerInstanceState[]? powers = null)
    {
        drawPile ??= [];
        powers ??= [];
        var cards = hand.Concat(drawPile).ToArray();
        var empty = PrototypeJson.EmptyObject();

        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                card.State.Clone())).ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: 0,
            Hand: hand.Select(card => card.InstanceId).ToArray(),
            DrawPile: drawPile.Select(card => card.InstanceId).ToArray(),
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: powers,
            NextPowerApplicationOrder: powers.Length == 0
                ? 1
                : powers.Max(power => power.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "nightmare-test",
            "nightmare-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("nightmare-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
