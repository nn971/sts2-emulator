using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeCardKeywordOverrideTests
{
    [Fact]
    public void HandTrickDefinitionUsesFilteredTemporarySlyOverride()
    {
        var card = PrototypeContent.Card("proto.silent.hand_trick");

        Assert.Equal(1, card.Cost.AmountAt(0));
        Assert.Equal(PrototypeCardType.Skill, card.Type);
        Assert.Equal(PrototypeCardRarity.Uncommon, card.Rarity);

        Assert.Collection(
            card.Effects,
            block =>
            {
                Assert.Equal(
                    PrototypeCombatEffectKind.GainPlayerBlock,
                    block.Kind);
                Assert.Equal(7, block.AmountAt(0, 0));
                Assert.Equal(10, block.AmountAt(1, 0));
            },
            choose =>
            {
                Assert.Equal(
                    PrototypeCombatEffectKind.ChooseCards,
                    choose.Kind);
                Assert.NotNull(choose.Selection);
                Assert.Equal(
                    PrototypeCardZone.Hand,
                    choose.Selection!.SourceZone);
                Assert.Equal(
                    PrototypeCardSelectionResolutionKind.Preserve,
                    choose.Selection.Resolution);
                Assert.Equal(
                    PrototypeCardType.Skill,
                    choose.Selection.RequiredCardType);
                Assert.NotNull(choose.SelectedCardKeyword);
                Assert.Equal(
                    PrototypeCardKeyword.Sly,
                    choose.SelectedCardKeyword!.Keyword);
                Assert.True(choose.SelectedCardKeyword.Enabled);
                Assert.Equal(
                    PrototypeCardKeywordOverrideExpiry.EndOfTurn,
                    choose.SelectedCardKeyword.Expiry);
            });
    }

    [Fact]
    public void HandTrickOnlyOffersSkillsAndAddsSlyForThisTurn()
    {
        var handTrick = Card(1, "proto.silent.hand_trick");
        var defend = Card(2, "proto.silent.defend");
        var strike = Card(3, "proto.silent.strike");
        var engine = new PrototypeGameEngine();
        var state = CreateState(energy: 3, hand: [handTrick, defend, strike]);

        state = PlayCard(engine, state, handTrick.InstanceId);

        var choices = engine.GetLegalActions(state)
            .Where(action => action.Kind == "select_cards")
            .Select(action =>
                action.ReadPayload<SelectCardsPayload>().CardInstanceIds)
            .ToArray();

        Assert.Single(choices);
        Assert.Equal([defend.InstanceId], choices[0]);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "select_cards")).State;

        var combat = state.World!.Combat!;
        var selected = combat.Cards.Single(
            card => card.InstanceId == defend.InstanceId);
        var keyword = Assert.Single(selected.KeywordOverrides!);
        Assert.Equal(PrototypeCardKeyword.Sly, keyword.Keyword);
        Assert.True(keyword.Enabled);
        Assert.Equal(
            PrototypeCardKeywordOverrideExpiry.EndOfTurn,
            keyword.Expiry);
        Assert.Equal(7, combat.PlayerBlock);
    }

    [Fact]
    public void ExpertiseDrawsCardsAndAddsRetainForThisTurn()
    {
        var expertise = Card(1, "proto.silent.expertise");
        var drawPile = new[]
        {
            Card(2, "proto.silent.strike"),
            Card(3, "proto.silent.defend"),
            Card(4, "proto.silent.neutralize")
        };
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 3,
            hand: [expertise],
            drawPile: drawPile);

        state = PlayCard(engine, state, expertise.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(
            [4L, 3L],
            combat.Hand);
        foreach (var cardInstanceId in combat.Hand)
        {
            var card = combat.Cards.Single(
                item => item.InstanceId == cardInstanceId);
            var keyword = Assert.Single(card.KeywordOverrides!);
            Assert.Equal(PrototypeCardKeyword.Retain, keyword.Keyword);
            Assert.True(keyword.Enabled);
            Assert.Equal(
                PrototypeCardKeywordOverrideExpiry.EndOfTurn,
                keyword.Expiry);
        }

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;

        Assert.Contains(4L, combat.Hand);
        Assert.Contains(3L, combat.Hand);
        Assert.Null(combat.Cards.Single(item => item.InstanceId == 4L)
            .KeywordOverrides);
        Assert.Null(combat.Cards.Single(item => item.InstanceId == 3L)
            .KeywordOverrides);
    }

    [Fact]
    public void TemporarySlyParticipatesInDiscardAutoplay()
    {
        var survivor = Card(1, "proto.silent.survivor");
        var defend = Card(2, "proto.silent.defend") with
        {
            KeywordOverrides =
            [
                new(
                    PrototypeCardKeyword.Sly,
                    true,
                    PrototypeCardKeywordOverrideExpiry.EndOfTurn)
            ]
        };

        var engine = new PrototypeGameEngine();
        var state = CreateState(energy: 3, hand: [survivor, defend]);

        state = PlayCard(engine, state, survivor.InstanceId);
        state = SelectOnly(engine, state, defend.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(13, combat.PlayerBlock);
        Assert.Contains(defend.InstanceId, combat.DiscardPile);
        Assert.DoesNotContain(defend.InstanceId, combat.Hand);
    }

    [Fact]
    public void EndOfTurnKeywordOverridesExpireAfterDiscardSemantics()
    {
        var defend = Card(1, "proto.silent.defend") with
        {
            KeywordOverrides =
            [
                new(
                    PrototypeCardKeyword.Retain,
                    true,
                    PrototypeCardKeywordOverrideExpiry.EndOfTurn)
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
        Assert.Contains(defend.InstanceId, combat.Hand);
        var retained = combat.Cards.Single(
            card => card.InstanceId == defend.InstanceId);
        Assert.Null(retained.KeywordOverrides);
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
            "keyword-override-test",
            "keyword-override-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("keyword-override-test"),
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
