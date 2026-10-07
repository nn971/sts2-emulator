using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeInkletTests
{
    [Fact]
    public void DefinitionMatchesPinnedActOneData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.inklet");

        Assert.Equal("Inklet", enemy.Name);
        Assert.Equal((11, 17), enemy.HpRangeAt(1, 0));
        Assert.Equal((12, 18), enemy.HpRangeAt(1, 8));

        Assert.Collection(
            enemy.Moves,
            jab =>
            {
                Assert.Equal("jab", jab.Id);
                var damage = Assert.Single(jab.Effects);
                Assert.Equal(3, damage.AmountAt(1, 8));
                Assert.Equal(4, damage.AmountAt(1, 9));
            },
            whirlwind =>
            {
                Assert.Equal("whirlwind", whirlwind.Id);
                var damage = Assert.Single(whirlwind.Effects);
                Assert.Equal(3, damage.RepetitionsAt(9));
                Assert.Equal(2, damage.AmountAt(1, 8));
                Assert.Equal(3, damage.AmountAt(1, 9));
            },
            gaze =>
            {
                Assert.Equal("piercing_gaze", gaze.Id);
                var damage = Assert.Single(gaze.Effects);
                Assert.Equal(10, damage.AmountAt(1, 8));
                Assert.Equal(11, damage.AmountAt(1, 9));
            });

        var slippery = Assert.Single(
            enemy.StartingPowers!);
        Assert.Equal("proto.power.slippery", slippery.PowerId);
        Assert.Equal(1, slippery.Stacks);

        var power = PrototypeContent.Power(
            "proto.power.slippery");
        Assert.Equal(1, power.EnemyHpLossCapPerTrigger);
        Assert.True(power.ConsumeOnEnemyHpLoss);
    }

    [Fact]
    public void EncounterUsesThreeSlottedInklets()
    {
        var specs = PrototypeContent.Encounter(
                "proto.encounter.inklets_normal")
            .ResolveEnemySpecs(
                PrototypeRng.CreateBundle(
                    "inklets-formation"));

        Assert.Collection(
            specs,
            left =>
            {
                Assert.Equal("proto.enemy.inklet", left.EnemyId);
                Assert.Equal(0, left.FormationPosition);
                Assert.Equal("left", left.SlotName);
            },
            middle =>
            {
                Assert.Equal("proto.enemy.inklet", middle.EnemyId);
                Assert.Equal(1, middle.FormationPosition);
                Assert.Equal("middle", middle.SlotName);
            },
            right =>
            {
                Assert.Equal("proto.enemy.inklet", right.EnemyId);
                Assert.Equal(2, right.FormationPosition);
                Assert.Equal("right", right.SlotName);
            });
    }

    [Fact]
    public void OuterInkletsOpenJabAndMiddleOpensWhirlwind()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateThreeInkletState();

        state = EndTurn(engine, state);

        Assert.Equal(988, state.Player.Hp);
        Assert.Collection(
            state.World!.Combat!.Enemies,
            left => Assert.Equal("jab", left.LastMoveId),
            middle => Assert.Equal("whirlwind", middle.LastMoveId),
            right => Assert.Equal("jab", right.LastMoveId));
    }

    [Fact]
    public void AfterJabBothRandomFollowupsAreReachableAndReturnToJab()
    {
        var seen = new HashSet<string>(
            StringComparer.Ordinal);

        for (var seed = 0; seed < 64; seed++)
        {
            var engine = new PrototypeGameEngine();
            var state = CreateSingleInkletState(
                $"inklet-rand-{seed}",
                aiStateId: "rand",
                lastMoveId: "jab");

            state = EndTurn(engine, state);
            var first = Assert.Single(
                state.World!.Combat!.Enemies).LastMoveId!;
            Assert.Contains(
                first,
                new[] { "piercing_gaze", "whirlwind" });
            seen.Add(first);

            state = EndTurn(engine, state);
            Assert.Equal(
                "jab",
                Assert.Single(
                    state.World!.Combat!.Enemies)
                    .LastMoveId);
        }

        Assert.Equal(
            ["piercing_gaze", "whirlwind"],
            seen.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SlipperyDoesNotConsumeWhenBlockPreventsHpLoss()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateSlipperyDamageState(
            enemyBlock: 6);

        state = PlayStrike(engine, state, 1);

        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(17, enemy.Hp);
        Assert.Equal(0, enemy.Block);
        Assert.Equal(
            1,
            Assert.Single(
                enemy.PowerStates,
                power => power.PowerId
                    == "proto.power.slippery")
                .Stacks);

        state = PlayStrike(engine, state, 2);

        enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(16, enemy.Hp);
        Assert.DoesNotContain(
            enemy.PowerStates,
            power => power.PowerId
                == "proto.power.slippery");
    }

    [Fact]
    public void SlipperyCapsRetaliationHpLossAndConsumes()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateSingleInkletState(
            "inklet-retaliation",
            aiStateId: "jab_move",
            slipperyStacks: 1,
            playerPowers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.thorns",
                    5,
                    1)
            ]);

        state = EndTurn(engine, state);

        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(16, enemy.Hp);
        Assert.DoesNotContain(
            enemy.PowerStates,
            power => power.PowerId
                == "proto.power.slippery");
    }

    [Fact]
    public void A9ScalesAllInkletAttacks()
    {
        var engine = new PrototypeGameEngine();

        var jab = CreateSingleInkletState(
            "inklet-a9-jab",
            ascension: 9,
            aiStateId: "jab_move");
        jab = EndTurn(engine, jab);
        Assert.Equal(996, jab.Player.Hp);

        var whirlwind = CreateSingleInkletState(
            "inklet-a9-whirlwind",
            ascension: 9,
            aiStateId: "whirlwind_move");
        whirlwind = EndTurn(engine, whirlwind);
        Assert.Equal(991, whirlwind.Player.Hp);

        var gaze = CreateSingleInkletState(
            "inklet-a9-gaze",
            ascension: 9,
            aiStateId: "piercing_gaze_move");
        gaze = EndTurn(engine, gaze);
        Assert.Equal(989, gaze.Player.Hp);
    }

    private static RunState PlayStrike(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
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

    private static EnemyCombatState Inklet(
        int instanceId,
        int position,
        string slotName,
        string? aiStateId = null,
        string? lastMoveId = null,
        int slipperyStacks = 0) =>
        new(
            instanceId,
            "proto.enemy.inklet",
            17,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal),
            Powers: slipperyStacks > 0
                ? [
                    new PrototypePowerInstanceState(
                        "proto.power.slippery",
                        slipperyStacks,
                        instanceId)
                  ]
                : null,
            LastMoveId: lastMoveId,
            AiStateId: aiStateId,
            MoveUseCounts:
                new Dictionary<string, int>(
                    StringComparer.Ordinal),
            FormationPosition: position,
            SlotName: slotName);

    private static RunState CreateThreeInkletState()
    {
        var combat = EmptyCombat(
            [
                Inklet(1, 0, "left"),
                Inklet(2, 1, "middle"),
                Inklet(3, 2, "right")
            ]);

        return State(
            "inklets-openers",
            combat);
    }

    private static RunState CreateSingleInkletState(
        string seed,
        int ascension = 0,
        string? aiStateId = null,
        string? lastMoveId = null,
        int slipperyStacks = 0,
        PrototypePowerInstanceState[]? playerPowers = null)
    {
        var combat = EmptyCombat(
            [
                Inklet(
                    1,
                    0,
                    "left",
                    aiStateId,
                    lastMoveId,
                    slipperyStacks)
            ],
            playerPowers: playerPowers);

        return State(seed, combat, ascension);
    }

    private static RunState CreateSlipperyDamageState(
        int enemyBlock)
    {
        var strike1 = new CombatCardInstance(
            1,
            null,
            "proto.silent.strike",
            0,
            true,
            PrototypeJson.EmptyObject());
        var strike2 = new CombatCardInstance(
            2,
            null,
            "proto.silent.strike",
            0,
            true,
            PrototypeJson.EmptyObject());

        var enemy = Inklet(
            1,
            0,
            "left",
            slipperyStacks: 1) with
        {
            Block = enemyBlock
        };

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: [enemy],
            NextCardInstanceId: 3,
            Cards: [strike1, strike2],
            PlayerPowers: [],
            NextPowerApplicationOrder: 2);

        return State(
            "inklet-slippery-damage",
            combat);
    }

    private static CombatState EmptyCombat(
        EnemyCombatState[] enemies,
        PrototypePowerInstanceState[]? playerPowers = null) =>
        new(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers:
                playerPowers ?? [],
            NextPowerApplicationOrder: 10);

    private static RunState State(
        string seed,
        CombatState combat,
        int ascension = 0) =>
        new(
            "prototype-unbound",
            "prototype-0.1",
            seed,
            seed,
            0,
            RunPhase.Combat,
            new PlayerState(
                1000,
                1000,
                0,
                [],
                [],
                new PotionInstance?[
                    PrototypeContent.Rules.PotionSlots]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
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
                null),
            Ascension: ascension);
}
