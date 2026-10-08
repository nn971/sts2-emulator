using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePotionInteractionTests
{
    [Fact]
    public void DuplicatorReplaysNextAttackAndConsumesPower()
    {
        var state = CreateState(
            cards: [Card(1, "proto.silent.strike")],
            hand: [1],
            enemies: [Enemy(1, 20)],
            potionId: "proto.potion.duplicator");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);
        Assert.Contains(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.duplication");

        state = PlayOnlyCard(engine, state);

        Assert.Equal(
            8,
            state.World!.Combat!.Enemies
                .Single(enemy => enemy.InstanceId == 1).Hp);
        Assert.DoesNotContain(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.duplication");
        Assert.Equal(
            2,
            state.World.Combat.CounterState.CardsPlayedThisTurn);
    }

    [Fact]
    public void DuplicatorAlsoReplaysSkills()
    {
        var state = CreateState(
            cards: [Card(1, "proto.silent.defend")],
            hand: [1],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.duplicator");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);
        state = PlayOnlyCard(engine, state);

        Assert.Equal(10, state.World!.Combat!.PlayerBlock);
        Assert.DoesNotContain(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.duplication");
    }

    [Fact]
    public void DuplicatorCalculatedGambleExhaustsSourceOnlyOnce()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.calculated_gamble"),
                Card(2, "proto.silent.strike")
            ],
            hand: [1, 2],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.duplicator");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);
        var gamble = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1);
        state = engine.Step(state, gamble).State;

        var combat = state.World!.Combat!;
        Assert.Equal(
            1,
            combat.ExhaustPile.Count(id => id == 1));
        Assert.Equal(
            2,
            combat.CounterState.CardsPlayedThisTurn);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void FlexPotionGrantsTemporaryStrengthUntilTurnEnd()
    {
        var state = CreateState(
            cards: [Card(1, "proto.silent.strike")],
            hand: [1],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.flex",
            playerBlock: 999);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);
        var temporaryStrength = Assert.Single(
            state.World!.Combat!.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.temporary_strength");
        Assert.Equal(5, temporaryStrength.Stacks);

        state = PlayOnlyCard(engine, state);
        Assert.Equal(
            89,
            state.World!.Combat!.Enemies
                .Single(enemy => enemy.InstanceId == 1).Hp);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.temporary_strength");
    }

    [Fact]
    public void CureAllGainsEnergyAndDrawsTwoCards()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend"),
                Card(3, "proto.silent.backflip")
            ],
            hand: [1],
            drawPile: [2, 3],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.cure_all",
            energy: 0);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var combat = state.World!.Combat!;
        Assert.Equal(1, combat.Energy);
        Assert.Empty(combat.DrawPile);
        Assert.Equal(3, combat.Hand.Length);
        Assert.Contains(2, combat.Hand);
        Assert.Contains(3, combat.Hand);
    }

    [Fact]
    public void FortifierTriplesCurrentBlock()
    {
        var state = CreateState(
            cards: [Card(1, "proto.silent.strike")],
            hand: [1],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.fortifier",
            playerBlock: 7);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        Assert.Equal(
            21,
            state.World!.Combat!.PlayerBlock);
    }

    [Fact]
    public void StableSerumRetainsHandForExactlyTwoCleanups()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend")
            ],
            hand: [1, 2],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.stable_serum",
            playerBlock: 999);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);
        Assert.Equal(
            2,
            Assert.Single(
                state.World!.Combat!.PlayerPowers,
                power =>
                    power.PowerId
                        == "proto.power.retain_hand")
                .Stacks);

        for (var cleanup = 1; cleanup <= 2; cleanup++)
        {
            state = engine.Step(
                state,
                engine.GetLegalActions(state)
                    .Single(action =>
                        action.Kind == "end_turn")).State;

            Assert.Contains(1, state.World!.Combat!.Hand);
            Assert.Contains(2, state.World.Combat.Hand);
            if (cleanup == 1)
            {
                Assert.Equal(
                    1,
                    Assert.Single(
                        state.World.Combat.PlayerPowers,
                        power =>
                            power.PowerId
                                == "proto.power.retain_hand")
                        .Stacks);
            }
        }

        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.retain_hand");
    }

    [Fact]
    public void TouchOfInsanityMakesChosenCardFreeForCombat()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend")
            ],
            hand: [1, 2],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.touch_of_insanity");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);
        var choice = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([1L]));
        state = engine.Step(state, choice).State;

        var combat = state.World!.Combat!;
        var strike = combat.Cards.Single(
            card => card.InstanceId == 1);
        Assert.NotNull(strike.TemporaryEnergyCost);
        Assert.Equal(0, strike.TemporaryEnergyCost!.Cost);
        Assert.Equal(
            PrototypeTemporaryCardCostExpiry.None,
            strike.TemporaryEnergyCost.Expiry);

        var play = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1);
        state = engine.Step(state, play).State;

        strike = state.World!.Combat!.Cards.Single(
            card => card.InstanceId == 1);
        Assert.NotNull(strike.TemporaryEnergyCost);
        Assert.Equal(0, strike.TemporaryEnergyCost!.Cost);
    }

    [Fact]
    public void SneckoOilDrawsThenRandomizesWholeHandButSkipsXCost()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.skewer"),
                Card(3, "proto.silent.defend"),
                Card(4, "proto.silent.backflip")
            ],
            hand: [1, 2],
            drawPile: [3, 4],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.snecko_oil");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var combat = state.World!.Combat!;
        Assert.Empty(combat.DrawPile);
        Assert.Equal(4, combat.Hand.Length);

        foreach (var instanceId in new long[] { 1, 3, 4 })
        {
            var card = combat.Cards.Single(
                item => item.InstanceId == instanceId);
            Assert.NotNull(card.TemporaryEnergyCost);
            Assert.InRange(
                card.TemporaryEnergyCost!.Cost,
                0,
                3);
            Assert.Equal(
                PrototypeTemporaryCardCostExpiry.EndOfTurn
                    | PrototypeTemporaryCardCostExpiry.WhenPlayed,
                card.TemporaryEnergyCost.Expiry);
        }

        Assert.Null(
            combat.Cards.Single(
                    card => card.InstanceId == 2)
                .TemporaryEnergyCost);
    }

    [Fact]
    public void ReptileTrinketTriggersAfterPotionIsConsumed()
    {
        var state = CreateState(
            cards: [Card(1, "proto.silent.strike")],
            hand: [1],
            enemies: [Enemy(1, 20)],
            potionId: "proto.potion.energy",
            relicId: "proto.relic.reptile_trinket");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        Assert.Null(state.Player.PotionSlots[0]);
        Assert.DoesNotContain(
            state.World!.Combat!.PotionStates,
            potion => potion.Slot == 0);
        var strength = Assert.Single(
            state.World.Combat.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.temporary_strength");
        Assert.Equal(3, strength.Stacks);

        state = PlayOnlyCard(engine, state);
        Assert.Equal(
            11,
            state.World!.Combat!.Enemies
                .Single(enemy => enemy.InstanceId == 1).Hp);
    }

    private static RunState UseOnlyPotion(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = Assert.Single(
            engine.GetLegalActions(state),
            item => item.Kind == "use_potion");
        return engine.Step(state, action).State;
    }

    private static RunState PlayOnlyCard(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = Assert.Single(
            engine.GetLegalActions(state),
            item => item.Kind == "play_card");
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

    private static EnemyCombatState Enemy(
        int id,
        int hp) =>
        new(
            id,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal));

    private static RunState CreateState(
        CombatCardInstance[] cards,
        long[] hand,
        EnemyCombatState[] enemies,
        string potionId,
        string? relicId = null,
        int playerBlock = 0,
        long[]? drawPile = null,
        int energy = 3)
    {
        var empty = PrototypeJson.EmptyObject();
        var potionSlots =
            new PotionInstance?[PrototypeContent.Rules.PotionSlots];
        potionSlots[0] =
            new PotionInstance(potionId, empty);

        RelicInstance[] relics = [];
        CombatRelicState[] combatRelics = [];
        if (relicId is not null)
        {
            relics =
            [
                new RelicInstance(relicId, empty)
            ];
            var relicDefinition =
                PrototypeContent.Relic(relicId);
            combatRelics =
            [
                new CombatRelicState(
                    0,
                    relicId,
                    1,
                    new int[
                        (relicDefinition.Triggers
                            ?? Array.Empty<PrototypeRelicTriggerSpec>())
                        .Length])
            ];
        }

        var persistent = cards
            .Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                empty))
            .ToArray();
        var player = new PlayerState(
            70,
            70,
            0,
            persistent,
            relics,
            potionSlots);
        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: playerBlock,
            Hand: hand,
            DrawPile: drawPile ?? [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId:
                cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder:
                combatRelics.Length + 1,
            Relics: combatRelics,
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
            "potion-interaction-test",
            "potion-interaction-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "potion-interaction-test"),
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
