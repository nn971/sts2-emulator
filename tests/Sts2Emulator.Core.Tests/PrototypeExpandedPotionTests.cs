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
    public void CunningPotionCreatesThreeUpgradedShivsInHand()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.strike")],
            hand: [1],
            potionId: "proto.potion.cunning");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var combat = state.World!.Combat!;
        var shivs = combat.Cards.Where(card =>
            card.CardId == "proto.silent.shiv").ToArray();
        Assert.Equal(3, shivs.Length);
        Assert.All(shivs, shiv => Assert.Equal(1, shiv.UpgradeLevel));
        Assert.All(shivs, shiv => Assert.Contains(shiv.InstanceId, combat.Hand));
        Assert.Null(state.Player.PotionSlots[0]);
    }

    [Fact]
    public void FyshOilAppliesStrengthThenDexterity()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.strike")],
            hand: [1],
            potionId: "proto.potion.fysh_oil");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var powers = state.World!.Combat!.PlayerPowers;
        Assert.Equal(1, Assert.Single(powers,
            power => power.PowerId == "proto.power.strength").Stacks);
        Assert.Equal(1, Assert.Single(powers,
            power => power.PowerId == "proto.power.dexterity").Stacks);
        Assert.Null(state.Player.PotionSlots[0]);
    }

    [Fact]
    public void PotionOfBindingAppliesWeakAndVulnerableToEveryEnemy()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.strike")],
            hand: [1],
            potionId: "proto.potion.binding",
            additionalEnemies: 2);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        Assert.Equal(3, state.World!.Combat!.Enemies.Length);
        Assert.All(state.World.Combat.Enemies, enemy =>
        {
            Assert.Equal(1,
                enemy.Statuses.GetValueOrDefault("proto.status.weak"));
            Assert.Equal(1,
                enemy.Statuses.GetValueOrDefault("proto.status.vulnerable"));
        });
        Assert.Null(state.Player.PotionSlots[0]);
    }

    [Fact]
    public void ShipInABottleGivesBlockNowAndAfterTheNextReset()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.defend")],
            hand: [1],
            potionId: "proto.potion.ship_in_a_bottle");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        Assert.Equal(10, state.World!.Combat!.PlayerBlock);
        Assert.Equal(10, Assert.Single(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.block_next_turn").Stacks);
        Assert.Null(state.Player.PotionSlots[0]);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        Assert.Equal(2, state.World!.Combat!.Turn);
        Assert.Equal(10, state.World.Combat.PlayerBlock);
        Assert.DoesNotContain(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.block_next_turn");
    }

    [Fact]
    public void DropletOfPrecognitionLetsPlayerChooseOneDrawPileCard()
    {
        var state = CombatStateWith(
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend")
            ],
            hand: [1],
            potionId: "proto.potion.droplet_of_precognition",
            drawPile: [2]);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var pending = Assert.IsType<PendingCombatChoiceState>(
            state.World!.Combat!.PendingChoice);
        Assert.Equal(PrototypeCardZone.DrawPile,
            pending.Selection.SourceZone);
        Assert.Equal([2L], pending.CandidateCardInstanceIds);
        var choose = Assert.Single(
            engine.GetLegalActions(state),
            action => action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([2L]));

        state = engine.Step(state, choose).State;

        Assert.Equal(new long[] { 1, 2 }, state.World!.Combat!.Hand);
        Assert.Empty(state.World.Combat.DrawPile);
        Assert.Null(state.World.Combat.PendingChoice);
        Assert.Null(state.Player.PotionSlots[0]);
    }

    [Fact]
    public void ExplosiveAmpouleDealsTenDamageToEveryEnemy()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.strike")],
            hand: [1],
            potionId: "proto.potion.explosive_ampoule",
            enemyHp: 20,
            additionalEnemies: 1);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        Assert.Equal(new[] { 10, 10 },
            state.World!.Combat!.Enemies.Select(enemy => enemy.Hp));
        Assert.Null(state.Player.PotionSlots[0]);
    }

    [Fact]
    public void BottledPotentialReturnsHandShufflesAndDrawsFive()
    {
        var cards = Enumerable.Range(1, 6)
            .Select(id => Card(
                id,
                id % 2 == 0
                    ? "proto.silent.defend"
                    : "proto.silent.strike"))
            .ToArray();
        var state = CombatStateWith(
            cards,
            hand: [1, 2],
            potionId: "proto.potion.bottled_potential",
            drawPile: [3, 4, 5, 6]);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var combat = state.World!.Combat!;
        Assert.Equal(5, combat.Hand.Length);
        Assert.Single(combat.DrawPile);
        Assert.Equal(
            new long[] { 1, 2, 3, 4, 5, 6 }.Order(),
            combat.Hand.Concat(combat.DrawPile).Order());
        Assert.Null(state.Player.PotionSlots[0]);
    }

    [Fact]
    public void HeartOfIronAppliesSevenPlating()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.defend")],
            hand: [1],
            potionId: "proto.potion.heart_of_iron");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        Assert.Equal(7, Assert.Single(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.plating").Stacks);
        Assert.Null(state.Player.PotionSlots[0]);
    }

    [Fact]
    public void ShacklingPotionTemporarilyReducesEveryEnemyStrength()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.defend")],
            hand: [1],
            potionId: "proto.potion.shackling",
            enemyStrength: 5,
            additionalEnemies: 1);
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        Assert.All(state.World!.Combat!.Enemies, enemy =>
        {
            Assert.Equal(-2, Assert.Single(
                enemy.PowerStates,
                power => power.PowerId == "proto.power.strength").Stacks);
            Assert.Equal(7, Assert.Single(
                enemy.PowerStates,
                power => power.PowerId == "proto.power.shackling_potion")
                .Stacks);
        });
        Assert.Null(state.Player.PotionSlots[0]);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        Assert.All(state.World!.Combat!.Enemies, enemy =>
        {
            Assert.Equal(5, Assert.Single(
                enemy.PowerStates,
                power => power.PowerId == "proto.power.strength").Stacks);
            Assert.DoesNotContain(enemy.PowerStates,
                power => power.PowerId == "proto.power.shackling_potion");
        });
    }

    [Fact]
    public void MazalethsGiftGrantsStrengthAtPlayerTurnEnd()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.defend")],
            hand: [1],
            potionId: "proto.potion.mazaleths_gift");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.strength");
        Assert.Equal(1, Assert.Single(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.ritual").Stacks);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        Assert.Equal(1, Assert.Single(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.strength").Stacks);
    }

    [Fact]
    public void RadiantTinctureAddsImmediateEnergyAndThreeLaterEnergyResets()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.defend")],
            hand: [1],
            potionId: "proto.potion.radiant_tincture");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);
        Assert.Equal(4, state.World!.Combat!.Energy);
        Assert.Equal(3, Assert.Single(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.radiance").Stacks);

        for (var expectedStacks = 2; expectedStacks >= 0; expectedStacks--)
        {
            state = engine.Step(
                state,
                engine.GetLegalActions(state)
                    .Single(action => action.Kind == "end_turn")).State;
            Assert.Equal(4, state.World!.Combat!.Energy);
            if (expectedStacks == 0)
            {
                Assert.DoesNotContain(
                    state.World.Combat.PlayerPowers,
                    power => power.PowerId == "proto.power.radiance");
            }
            else
            {
                Assert.Equal(expectedStacks, Assert.Single(
                    state.World.Combat.PlayerPowers,
                    power => power.PowerId == "proto.power.radiance").Stacks);
            }
        }
    }

    [Fact]
    public void BeetleJuiceReducesPoweredEnemyDamageAndExpiresAfterFourTurns()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.defend")],
            hand: [1],
            potionId: "proto.potion.beetle_juice");
        var engine = new PrototypeGameEngine();

        state = UseOnlyPotion(engine, state);

        var combat = state.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        Assert.Equal(4, Assert.Single(
            enemy.PowerStates,
            power => power.PowerId == "proto.power.shrink").Stacks);
        Assert.Equal(7, PrototypeGameEngine.EnemyHitDamage(
            enemy,
            combat,
            new PrototypeEnemyEffectSpec(
                PrototypeEnemyEffectKind.DamagePlayer,
                10),
            act: 1,
            ascension: 0));

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        Assert.Equal(3, Assert.Single(
            state.World!.Combat!.Enemies.Single().PowerStates,
            power => power.PowerId == "proto.power.shrink").Stacks);
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

    [Fact]
    public void FairyInABottleIsAutomaticAndRevivesAfterLethalDamage()
    {
        var state = CombatStateWith(
            [Card(1, "proto.silent.defend")],
            hand: [1],
            potionId: "proto.potion.fairy_in_a_bottle",
            playerHp: 1);
        var engine = new PrototypeGameEngine();

        Assert.DoesNotContain(
            engine.GetLegalActions(state),
            action => action.Kind == "use_potion");

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action =>
                    action.Kind == "end_turn")).State;

        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal(21, state.Player.Hp);
        Assert.Null(state.Player.PotionSlots[0]);
        Assert.DoesNotContain(
            state.World!.Combat!.PotionStates,
            potion => potion.Slot == 0);
        PrototypeStateInvariants.Validate(state);
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
        int enemyHp = 100,
        int playerHp = 70,
        int additionalEnemies = 0,
        long[]? drawPile = null,
        int enemyStrength = 0)
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
            playerHp,
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
            DrawPile: drawPile ?? [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: Enumerable.Range(0, additionalEnemies + 1)
                .Select(index => new EnemyCombatState(
                    index + 1,
                    "proto.enemy.crawler",
                    enemyHp,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal),
                    Powers: enemyStrength == 0
                        ? []
                        : [new PrototypePowerInstanceState(
                            "proto.power.strength",
                            enemyStrength,
                            1)]))
                .ToArray(),
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
