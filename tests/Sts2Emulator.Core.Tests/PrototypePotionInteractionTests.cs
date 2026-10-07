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
        int playerBlock = 0)
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
            Energy: 3,
            PlayerBlock: playerBlock,
            Hand: hand,
            DrawPile: [],
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
