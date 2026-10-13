using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeGeneratedChoicePotionTests
{
    [Theory]
    [InlineData("proto.potion.attack", PrototypeCardType.Attack)]
    [InlineData("proto.potion.skill", PrototypeCardType.Skill)]
    [InlineData("proto.potion.power", PrototypeCardType.Power)]
    public void TypedCardPotionOffersThreeDistinctFreeGeneratedCards(
        string potionId,
        PrototypeCardType expectedType)
    {
        var state = CreateState(
            potionId,
            cards: [Card(1, "proto.silent.defend")],
            hand: [1]);
        var engine = new PrototypeGameEngine();

        state = UsePotion(engine, state);

        var combat = state.World!.Combat!;
        Assert.NotNull(combat.PendingChoice);
        Assert.Equal(
            PrototypeCardZone.ChoicePool,
            combat.PendingChoice!.Selection.SourceZone);
        Assert.Equal(3, combat.ChoiceCardIds.Length);
        Assert.Equal(
            3,
            combat.ChoiceCardIds.Distinct().Count());

        Assert.All(
            combat.ChoiceCardIds,
            id =>
            {
                var card = combat.Cards.Single(
                    item => item.InstanceId == id);
                var definition =
                    PrototypeContent.Card(card.CardId);
                Assert.True(card.IsTemporary);
                Assert.Equal(expectedType, definition.Type);
                Assert.True(definition.CanBeGeneratedInCombat);
                Assert.False(definition.MultiplayerOnly);
                Assert.NotNull(card.TemporaryEnergyCost);
                Assert.Equal(
                    0,
                    card.TemporaryEnergyCost!.Cost);
            });

        var legal = engine.GetLegalActions(state);
        Assert.Equal(4, legal.Count);
        Assert.Single(
            legal,
            action =>
                action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.Length == 0);
        Assert.Equal(
            3,
            legal.Count(action =>
                action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.Length == 1));

        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ChoosingGeneratedCardMovesOnlyThatCardToHand()
    {
        var state = CreateState(
            "proto.potion.attack",
            cards: [Card(1, "proto.silent.defend")],
            hand: [1]);
        var engine = new PrototypeGameEngine();
        state = UsePotion(engine, state);

        var before = state.World!.Combat!;
        var candidates = (long[])before.ChoiceCardIds.Clone();
        var choose = engine.GetLegalActions(state)
            .First(action =>
                action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.Length == 1);
        var chosenId = choose
            .ReadPayload<SelectCardsPayload>()
            .CardInstanceIds[0];

        state = engine.Step(state, choose).State;

        var combat = state.World!.Combat!;
        Assert.Empty(combat.ChoiceCardIds);
        Assert.Contains(chosenId, combat.Hand);
        Assert.All(
            candidates.Where(id => id != chosenId),
            id => Assert.DoesNotContain(
                combat.Cards,
                card => card.InstanceId == id));
        var chosen = combat.Cards.Single(
            card => card.InstanceId == chosenId);
        Assert.Equal(0, chosen.TemporaryEnergyCost!.Cost);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void SkippingGeneratedCardChoiceDeletesAllCandidates()
    {
        var state = CreateState(
            "proto.potion.skill",
            cards: [Card(1, "proto.silent.defend")],
            hand: [1]);
        var engine = new PrototypeGameEngine();
        state = UsePotion(engine, state);

        var candidates =
            (long[])state.World!.Combat!.ChoiceCardIds.Clone();
        var skip = engine.GetLegalActions(state)
            .Single(action =>
                action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.Length == 0);

        state = engine.Step(state, skip).State;

        var combat = state.World!.Combat!;
        Assert.Empty(combat.ChoiceCardIds);
        Assert.All(
            candidates,
            id => Assert.DoesNotContain(
                combat.Cards,
                card => card.InstanceId == id));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void LiquidMemoriesMovesDiscardCardToHandAndMakesItFree()
    {
        var state = CreateState(
            "proto.potion.liquid_memories",
            cards:
            [
                Card(1, "proto.silent.defend"),
                Card(2, "proto.silent.strike")
            ],
            hand: [1],
            discard: [2],
            energy: 0);
        var engine = new PrototypeGameEngine();

        state = UsePotion(engine, state);

        var combat = state.World!.Combat!;
        var pending = Assert.IsType<PendingCombatChoiceState>(
            combat.PendingChoice);
        Assert.Equal(
            PrototypeCardZone.DiscardPile,
            pending.Selection.SourceZone);
        Assert.Equal([2L], pending.CandidateCardInstanceIds);

        state = engine.Step(
            state,
            Assert.Single(engine.GetLegalActions(state))).State;

        combat = state.World!.Combat!;
        Assert.Contains(2L, combat.Hand);
        Assert.DoesNotContain(2L, combat.DiscardPile);
        var strike = combat.Cards.Single(
            card => card.InstanceId == 2);
        Assert.NotNull(strike.TemporaryEnergyCost);
        Assert.Equal(0, strike.TemporaryEnergyCost!.Cost);

        Assert.Contains(
            engine.GetLegalActions(state),
            action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 2);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void PotionUsedTriggerWaitsUntilGeneratedChoiceResolves()
    {
        var state = CreateState(
            "proto.potion.attack",
            cards: [Card(1, "proto.silent.defend")],
            hand: [1]);
        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Relics =
                [
                    new RelicInstance(
                        "proto.relic.reptile_trinket",
                        empty)
                ]
            },
            World = state.World! with
            {
                Combat = state.World!.Combat! with
                {
                    Relics =
                    [
                        new CombatRelicState(
                            0,
                            "proto.relic.reptile_trinket",
                            1,
                            [0])
                    ],
                    NextPowerApplicationOrder = 2
                }
            }
        };
        var engine = new PrototypeGameEngine();

        state = UsePotion(engine, state);

        Assert.NotNull(state.World!.Combat!.PendingChoice);
        Assert.Null(state.Player.PotionSlots[0]);
        Assert.DoesNotContain(
            state.World.Combat.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.temporary_strength");

        var choose = engine.GetLegalActions(state)
            .First(action =>
                action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.Length == 1);
        state = engine.Step(state, choose).State;

        Assert.Null(state.World!.Combat!.PendingChoice);
        Assert.Equal(
            3,
            Assert.Single(
                state.World.Combat.PlayerPowers,
                power =>
                    power.PowerId
                        == "proto.power.temporary_strength")
                .Stacks);
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState UsePotion(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = Assert.Single(
            engine.GetLegalActions(state),
            item => item.Kind == "use_potion");
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long id,
        string cardId) =>
        new(
            id,
            1000 + id,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateState(
        string potionId,
        CombatCardInstance[] cards,
        long[] hand,
        long[]? discard = null,
        int energy = 3)
    {
        discard ??= [];
        var empty = PrototypeJson.EmptyObject();
        var persistent = cards
            .Select(card =>
                new CardInstance(
                    card.PersistentCardInstanceId!.Value,
                    card.CardId,
                    card.UpgradeLevel,
                    empty))
            .ToArray();
        var potionSlots =
            new PotionInstance?[PrototypeContent.Rules.PotionSlots];
        potionSlots[0] =
            new PotionInstance(potionId, empty);
        var player = new PlayerState(
            70,
            70,
            0,
            persistent,
            [],
            potionSlots);
        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: 0,
            Hand: hand,
            DrawPile: [],
            DiscardPile: discard,
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    100,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId:
                cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1,
            Potions:
            [
                new CombatPotionState(
                    0,
                    potionId,
                    empty)
            ]);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "generated-choice-potion-test",
            "generated-choice-potion-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "generated-choice-potion-test"),
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
