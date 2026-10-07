using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeExpandedPotionTests
{
    [Fact]
    public void FruitJuiceIsUsableOnMapAndRaisesCurrentAndMaxHp()
    {
        var state = NonCombatState(
            hp: 40,
            maxHp: 70,
            potionId: "proto.potion.fruit_juice");
        var engine = new PrototypeGameEngine();

        var use = Assert.Single(
            engine.GetLegalActions(state),
            action => action.Kind == "use_potion");
        state = engine.Step(state, use).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(45, state.Player.Hp);
        Assert.Equal(75, state.Player.MaxHp);
        Assert.Null(state.Player.PotionSlots[0]);
    }

    [Fact]
    public void BloodPotionHealsTwentyPercentOfMaxHpOutsideCombat()
    {
        var state = NonCombatState(
            hp: 20,
            maxHp: 70,
            potionId: "proto.potion.blood");
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state),
                action => action.Kind == "use_potion")).State;

        Assert.Equal(34, state.Player.Hp);
        Assert.Null(state.Player.PotionSlots[0]);
    }

    [Fact]
    public void EntropicBrewConsumesItselfAndFillsOpenPotionSlots()
    {
        var state = NonCombatState(
            hp: 70,
            maxHp: 70,
            potionId: "proto.potion.entropic_brew");
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state),
                action => action.Kind == "use_potion")).State;

        Assert.All(
            state.Player.PotionSlots,
            potion => Assert.NotNull(potion));
    }

    [Fact]
    public void BlessingOfTheForgeUpgradesAllUpgradableCardsInHand()
    {
        var state = CombatStateWith(
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend"),
                Card(3, "proto.status.dazed")
            ],
            hand: [1, 2, 3],
            potionId: "proto.potion.blessing_of_the_forge");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var combat = state.World!.Combat!;
        Assert.Equal(
            1,
            combat.Cards.Single(card => card.InstanceId == 1)
                .UpgradeLevel);
        Assert.Equal(
            1,
            combat.Cards.Single(card => card.InstanceId == 2)
                .UpgradeLevel);
        Assert.Equal(
            0,
            combat.Cards.Single(card => card.InstanceId == 3)
                .UpgradeLevel);
    }

    [Fact]
    public void SpeedPotionUsesTemporaryDexterityPower()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.defend")],
            hand: [1],
            potionId: "proto.potion.speed",
            playerBlock: 999);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);
        var power = Assert.Single(
            state.World!.Combat!.PlayerPowers,
            item => item.PowerId == "proto.power.temporary_dexterity");
        Assert.Equal(5, power.Stacks);

        var defend = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");
        state = engine.Step(state, defend).State;
        Assert.Equal(1009, state.World!.Combat!.PlayerBlock);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;
        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            item => item.PowerId == "proto.power.temporary_dexterity");
    }

    [Fact]
    public void VulnerablePotionUsesExistingVulnerableDamageSemantics()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.strike")],
            hand: [1],
            potionId: "proto.potion.vulnerable",
            enemyHp: 20);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);
        var enemy = state.World!.Combat!.Enemies.Single();
        Assert.Equal(
            3,
            enemy.Statuses.GetValueOrDefault("proto.status.vulnerable"));

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "play_card")).State;

        Assert.Equal(11, state.World!.Combat!.Enemies.Single().Hp);
    }

    [Theory]
    [InlineData("proto.potion.liquid_bronze", "proto.power.thorns", 3)]
    [InlineData("proto.potion.ghost_in_a_jar", "proto.power.intangible", 1)]
    public void DefensivePotionsReuseExistingPowerMechanics(
        string potionId,
        string powerId,
        int stacks)
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.defend")],
            hand: [1],
            potionId: potionId);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var power = Assert.Single(
            state.World!.Combat!.PlayerPowers,
            item => item.PowerId == powerId);
        Assert.Equal(stacks, power.Stacks);
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

    private static RunState NonCombatState(
        int hp,
        int maxHp,
        string potionId)
    {
        var empty = PrototypeJson.EmptyObject();
        var slots =
            new PotionInstance?[PrototypeContent.Rules.PotionSlots];
        slots[0] = new PotionInstance(potionId, empty);
        var player = new PlayerState(
            hp,
            maxHp,
            0,
            [],
            [],
            slots);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "expanded-potion-noncombat",
            "expanded-potion-noncombat",
            0,
            RunPhase.MapChoice,
            player,
            PrototypeRng.CreateBundle(
                "expanded-potion-noncombat"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                1,
                null,
                new MapState(
                    [
                        new MapNodeState(
                            "next",
                            1,
                            1,
                            PrototypeRoomType.Combat,
                            [])
                    ],
                    EntryNodeIds: ["next"]),
                null,
                null,
                null,
                null,
                null));
    }

    private static RunState CombatStateWith(
        CombatCardInstance[] cards,
        long[] hand,
        string potionId,
        int playerBlock = 0,
        int enemyHp = 100)
    {
        var empty = PrototypeJson.EmptyObject();
        var persistent = cards
            .Select(card =>
                new CardInstance(
                    card.PersistentCardInstanceId!.Value,
                    card.CardId,
                    card.UpgradeLevel,
                    empty))
            .ToArray();
        var slots =
            new PotionInstance?[PrototypeContent.Rules.PotionSlots];
        slots[0] = new PotionInstance(potionId, empty);
        var player = new PlayerState(
            70,
            70,
            0,
            persistent,
            [],
            slots);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: playerBlock,
            Hand: hand,
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    enemyHp,
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
            "expanded-potion-combat",
            "expanded-potion-combat",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "expanded-potion-combat"),
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
