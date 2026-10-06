using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeMasterPlannerTests
{
    [Fact]
    public void MasterPlannerDefinitionMatchesPinnedV01110Semantics()
    {
        var card = PrototypeContent.Card("proto.silent.master_planner");

        Assert.Equal(2, card.Cost.AmountAt(0));
        Assert.Equal(1, card.Cost.AmountAt(1));
        Assert.Equal(PrototypeCardType.Power, card.Type);
        Assert.Equal(PrototypeCardRarity.Rare, card.Rarity);

        var apply = Assert.Single(card.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.ApplyPlayerPower,
            apply.Kind);
        Assert.Equal("proto.power.master_planner", apply.PowerId);

        var power = PrototypeContent.Power("proto.power.master_planner");
        var trigger = Assert.Single(power.Triggers);
        Assert.Equal(
            PrototypeCombatEventKind.CardPlayed,
            trigger.EventKind);
        Assert.Equal(
            PrototypeCardType.Skill,
            trigger.RequiredSourceCardType);

        var effect = Assert.Single(trigger.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.ModifyEventSourceCardKeyword,
            effect.Kind);
        Assert.NotNull(effect.EventSourceCardKeyword);
        Assert.Equal(
            PrototypeCardKeyword.Sly,
            effect.EventSourceCardKeyword!.Keyword);
        Assert.True(effect.EventSourceCardKeyword.Enabled);
        Assert.Equal(
            PrototypeCardKeywordOverrideExpiry.None,
            effect.EventSourceCardKeyword.Expiry);
    }

    [Fact]
    public void MasterPlannerMutatesPlayedSkillButNotAttack()
    {
        var masterPlanner = Card(1, "proto.silent.master_planner");
        var defend = Card(2, "proto.silent.defend");
        var strike = Card(3, "proto.silent.strike");

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 5,
            hand: [masterPlanner, defend, strike]);

        state = PlayCard(engine, state, masterPlanner.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Single(
            combat.PlayerPowers,
            item => item.PowerId == "proto.power.master_planner");

        // Power cards leave combat after resolving. Master Planner's
        // CardPlayed trigger is Skill-only, so its own play cannot be a
        // mutation target.
        Assert.DoesNotContain(
            combat.Cards,
            card => card.InstanceId == masterPlanner.InstanceId);

        state = PlayCard(engine, state, strike.InstanceId);
        combat = state.World!.Combat!;
        Assert.Null(combat.Cards.Single(
            card => card.InstanceId == strike.InstanceId)
            .KeywordOverrides);

        state = PlayCard(engine, state, defend.InstanceId);
        combat = state.World!.Combat!;

        var mutated = combat.Cards.Single(
            card => card.InstanceId == defend.InstanceId);
        var keyword = Assert.Single(mutated.KeywordOverrides!);
        Assert.Equal(PrototypeCardKeyword.Sly, keyword.Keyword);
        Assert.True(keyword.Enabled);
        Assert.Equal(
            PrototypeCardKeywordOverrideExpiry.None,
            keyword.Expiry);
        Assert.Contains(defend.InstanceId, combat.DiscardPile);
    }

    [Fact]
    public void MasterPlannerSlySurvivesTurnCleanup()
    {
        var defend = Card(1, "proto.silent.defend") with
        {
            KeywordOverrides =
            [
                new(
                    PrototypeCardKeyword.Sly,
                    true,
                    PrototypeCardKeywordOverrideExpiry.None)
            ]
        };
        var drawPile = Enumerable.Range(2, 5)
            .Select(id => Card(id, "proto.silent.strike"))
            .ToArray();

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 3,
            hand: [defend],
            drawPile: drawPile);

        state = EndTurn(engine, state);

        var combat = state.World!.Combat!;
        var card = combat.Cards.Single(
            item => item.InstanceId == defend.InstanceId);
        var keyword = Assert.Single(card.KeywordOverrides!);
        Assert.Equal(PrototypeCardKeyword.Sly, keyword.Keyword);
        Assert.Equal(
            PrototypeCardKeywordOverrideExpiry.None,
            keyword.Expiry);
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
        string cardId) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateState(
        int energy,
        CombatCardInstance[] hand,
        CombatCardInstance[]? drawPile = null)
    {
        drawPile ??= [];
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
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "master-planner-test",
            "master-planner-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("master-planner-test"),
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
