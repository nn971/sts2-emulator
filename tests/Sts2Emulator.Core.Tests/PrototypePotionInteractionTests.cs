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
    public void GamblersBrewUsesLinearSequentialChoiceAndDrawsDiscardCount()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend"),
                Card(3, "proto.silent.backflip"),
                Card(4, "proto.silent.neutralize"),
                Card(5, "proto.silent.survivor")
            ],
            hand: [1, 2, 3],
            drawPile: [4, 5],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.gamblers_brew");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var legal = engine.GetLegalActions(state);
        Assert.Equal(4, legal.Count);
        Assert.Equal(
            3,
            legal.Count(action =>
                action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.Length == 1));
        Assert.Single(
            legal,
            action =>
                action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.Length == 0);

        var chooseOne = legal.Single(action =>
            action.ReadPayload<SelectCardsPayload>()
                .CardInstanceIds.SequenceEqual([1L]));
        state = engine.Step(state, chooseOne).State;
        Assert.Contains(1L, state.World!.Combat!.Hand);

        legal = engine.GetLegalActions(state);
        Assert.Equal(3, legal.Count);
        var chooseTwo = legal.Single(action =>
            action.ReadPayload<SelectCardsPayload>()
                .CardInstanceIds.SequenceEqual([2L]));
        state = engine.Step(state, chooseTwo).State;

        legal = engine.GetLegalActions(state);
        Assert.Equal(2, legal.Count);
        var finish = legal.Single(action =>
            action.ReadPayload<SelectCardsPayload>()
                .CardInstanceIds.Length == 0);
        state = engine.Step(state, finish).State;

        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Contains(1L, combat.DiscardPile);
        Assert.Contains(2L, combat.DiscardPile);
        Assert.DoesNotContain(1L, combat.Hand);
        Assert.DoesNotContain(2L, combat.Hand);
        Assert.Contains(3L, combat.Hand);
        Assert.Contains(4L, combat.Hand);
        Assert.Contains(5L, combat.Hand);
        Assert.Empty(combat.DrawPile);
        Assert.Equal(
            2,
            combat.CounterState.CardsDiscardedThisTurn);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void LethalUpgradedDaggerThrowCompletesDiscardBeforeCombatWon()
    {
        // Reproduces native self-play failure neural-train-86-1 at floor 13:
        // Dagger Throw+ hits the final 12-HP Mawler, draws and demands one
        // discard, while Meat on the Bone triggers on CombatWon at low HP.
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.dagger_throw") with { UpgradeLevel = 1 },
                Card(2, "proto.silent.strike"),
                Card(3, "proto.silent.defend")
            ],
            hand: [1, 2],
            drawPile: [3],
            enemies: [Enemy(1, 12)],
            potionId: "proto.potion.energy",
            relicId: "proto.relic.meat_on_the_bone");
        state = state with { Player = state.Player with { Hp = 21 } };
        var engine = new PrototypeGameEngine();

        var play = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, play).State;

        Assert.Equal(RunPhase.Combat, state.Phase);
        var combat = state.World!.Combat!;
        Assert.Equal(0, combat.Enemies.Single().Hp);
        Assert.NotNull(combat.PendingChoice);
        Assert.Equal("select_cards", combat.PendingChoice.ChoiceId);
        PrototypeStateInvariants.Validate(state);

        var select = engine.GetLegalActions(state).First(action =>
            action.Kind == "select_cards");
        state = engine.Step(state, select).State;

        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.Equal(33, state.Player.Hp); // Meat on the Bone, exactly once.
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void DistilledChaosStagesAndAutoPlaysTopThreeCards()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.defend"),
                Card(2, "proto.silent.strike"),
                Card(3, "proto.silent.defend"),
                Card(4, "proto.silent.strike")
            ],
            hand: [1],
            drawPile: [2, 3, 4],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.distilled_chaos");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Empty(combat.PlayCardIds);
        Assert.Empty(combat.DrawPile);
        Assert.Equal(88, combat.Enemies.Single().Hp);
        Assert.Equal(5, combat.PlayerBlock);
        Assert.Equal(3, combat.CounterState.CardsPlayedThisTurn);
        Assert.All(
            new long[] { 2, 3, 4 },
            id => Assert.Contains(id, combat.DiscardPile));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void DistilledChaosSkipsUnplayableDowsingWithoutAbortingAutoplay()
    {
        // Native Dowsing is a persistent unplayable quest card with
        // MechanicsImplemented=false: it is not a missing combat effect.
        // The potion must still finish its two subsequent normal plays.
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.defend"),
                Card(2, "proto.native.neow.dowsing"),
                Card(3, "proto.silent.strike"),
                Card(4, "proto.silent.defend")
            ],
            hand: [1],
            drawPile: [2, 3, 4],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.distilled_chaos");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Empty(combat.PlayCardIds);
        Assert.Empty(combat.DrawPile);
        Assert.Equal(94, combat.Enemies.Single().Hp);
        Assert.Equal(5, combat.PlayerBlock);
        Assert.Equal(2, combat.CounterState.CardsPlayedThisTurn);
        Assert.All(
            new long[] { 2, 3, 4 },
            id => Assert.Contains(id, combat.DiscardPile));
        Assert.Contains(
            state.Player.Deck,
            card => card.CardId == "proto.native.neow.dowsing");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void DistilledChaosSkipsUnplayableQuestEggAsWell()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.native.event.byrdonis_egg"),
                Card(3, "proto.native.neow.dowsing"),
                Card(4, "proto.silent.strike")
            ],
            hand: [1],
            drawPile: [2, 3, 4],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.distilled_chaos");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var combat = state.World!.Combat!;
        Assert.Empty(combat.PlayCardIds);
        Assert.Equal(94, combat.Enemies.Single().Hp);
        Assert.Equal(1, combat.CounterState.CardsPlayedThisTurn);
        Assert.All(
            new long[] { 2, 3, 4 },
            id => Assert.Contains(id, combat.DiscardPile));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void DistilledChaosPreservesOuterAutoplayAcrossNestedChoice()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.defend"),
                Card(2, "proto.silent.strike"),
                Card(3, "proto.silent.survivor"),
                Card(4, "proto.silent.strike")
            ],
            hand: [1],
            drawPile: [2, 3, 4],
            enemies: [Enemy(1, 100)],
            potionId: "proto.potion.distilled_chaos");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var combat = state.World!.Combat!;
        Assert.NotNull(combat.PendingChoice);
        Assert.Contains(2L, combat.PlayCardIds);
        Assert.DoesNotContain(3L, combat.PlayCardIds);

        var discard = Assert.Single(
            engine.GetLegalActions(state));
        Assert.Equal(
            [1L],
            discard.ReadPayload<SelectCardsPayload>()
                .CardInstanceIds);
        state = engine.Step(state, discard).State;

        combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Empty(combat.PlayCardIds);
        Assert.Equal(88, combat.Enemies.Single().Hp);
        Assert.Equal(8, combat.PlayerBlock);
        Assert.Equal(3, combat.CounterState.CardsPlayedThisTurn);
        Assert.All(
            new long[] { 1, 2, 3, 4 },
            id => Assert.Contains(id, combat.DiscardPile));
        PrototypeStateInvariants.Validate(state);
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
