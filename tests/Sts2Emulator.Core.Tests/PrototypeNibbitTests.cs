using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNibbitTests
{
    [Fact]
    public void DefinitionMatchesPinnedAct1Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.nibbit");

        Assert.Equal("Nibbit", enemy.Name);
        Assert.Equal((42, 46), enemy.HpRangeAt(1, 0));
        Assert.Equal((44, 48), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.StateMachine,
            enemy.MovePolicy);

        Assert.Collection(
            enemy.Moves,
            butt =>
            {
                Assert.Equal("butt", butt.Id);
                var damage = Assert.Single(butt.Effects);
                Assert.Equal(12, damage.AmountAt(1, 8));
                Assert.Equal(13, damage.AmountAt(1, 9));
            },
            slice =>
            {
                Assert.Equal("slice", slice.Id);
                Assert.Collection(
                    slice.Effects,
                    damage =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.DamagePlayer,
                            damage.Kind);
                        Assert.Equal(6, damage.AmountAt(1, 8));
                        Assert.Equal(7, damage.AmountAt(1, 9));
                    },
                    block =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.GainBlock,
                            block.Kind);
                        Assert.Equal(6, block.AmountAt(1, 8));
                    });
            },
            hiss =>
            {
                Assert.Equal("hiss", hiss.Id);
                var strength = Assert.Single(hiss.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyEnemyPower,
                    strength.Kind);
                Assert.Equal(
                    "proto.power.strength",
                    strength.PowerId);
                Assert.Equal(2, strength.AmountAt(1, 8));
                Assert.Equal(3, strength.AmountAt(1, 9));
            });

        var ai = Assert.IsType<
            PrototypeEnemyAiDefinition>(enemy.Ai);
        var initial = Assert.Single(
            ai.States,
            state => state.Id == "init");
        Assert.Collection(
            initial.ConditionalBranches!,
            alone =>
            {
                Assert.Equal("butt", alone.TargetStateId);
                Assert.Equal(
                    PrototypeEnemyAiConditionKind.IsAlone,
                    alone.Condition);
            },
            back =>
            {
                Assert.Equal("hiss", back.TargetStateId);
                Assert.Equal(
                    PrototypeEnemyAiConditionKind.IsNotFront,
                    back.Condition);
            },
            front =>
            {
                Assert.Equal("slice", front.TargetStateId);
                Assert.Equal(
                    PrototypeEnemyAiConditionKind.IsFront,
                    front.Condition);
            });
    }

    [Fact]
    public void ReferenceEncounterFormationsMatchSoloAndPairedNibbits()
    {
        var weak = PrototypeContent.Encounter(
            "proto.encounter.nibbits_weak");
        var normal = PrototypeContent.Encounter(
            "proto.encounter.nibbits_normal");

        var rng = PrototypeRng.CreateBundle(
            "nibbit-formation-test");

        var weakSpecs = weak.ResolveEnemySpecs(rng);
        var weakEnemy = Assert.Single(weakSpecs);
        Assert.Equal("proto.enemy.nibbit", weakEnemy.EnemyId);
        Assert.Equal(0, weakEnemy.FormationPosition);

        var normalSpecs = normal.ResolveEnemySpecs(rng);
        Assert.Collection(
            normalSpecs,
            front =>
            {
                Assert.Equal("proto.enemy.nibbit", front.EnemyId);
                Assert.Equal(0, front.FormationPosition);
            },
            back =>
            {
                Assert.Equal("proto.enemy.nibbit", back.EnemyId);
                Assert.Equal(1, back.FormationPosition);
            });
    }

    [Fact]
    public void LoneNibbitStartsButtThenSliceThenHiss()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            ascension: 0,
            Enemy(1, 0));

        state = EndTurn(engine, state);
        Assert.Equal(88, state.Player.Hp);
        AssertEnemy(state, 1, "butt", block: 0, strength: 0);

        state = EndTurn(engine, state);
        Assert.Equal(82, state.Player.Hp);
        AssertEnemy(state, 1, "slice", block: 5, strength: 0);

        state = EndTurn(engine, state);
        Assert.Equal(82, state.Player.Hp);
        AssertEnemy(state, 1, "hiss", block: 0, strength: 2);

        state = EndTurn(engine, state);
        Assert.Equal(68, state.Player.Hp);
        AssertEnemy(state, 1, "butt", block: 0, strength: 2);
    }

    [Fact]
    public void PairedNibbitsUseFrontSliceAndBackHissOpeners()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            ascension: 0,
            Enemy(1, 0),
            Enemy(2, 1));

        state = EndTurn(engine, state);

        Assert.Equal(94, state.Player.Hp);
        AssertEnemy(state, 1, "slice", block: 5, strength: 0);
        AssertEnemy(state, 2, "hiss", block: 0, strength: 2);

        state = EndTurn(engine, state);

        Assert.Equal(80, state.Player.Hp);
        AssertEnemy(state, 1, "hiss", block: 0, strength: 2);
        AssertEnemy(state, 2, "butt", block: 0, strength: 2);
    }

    [Fact]
    public void A9ScalesSliceBlockHissStrengthAndAttacks()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            ascension: 9,
            Enemy(1, 0, hp: 48));

        state = EndTurn(engine, state);
        Assert.Equal(87, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal(80, state.Player.Hp);
        AssertEnemy(state, 1, "slice", block: 6, strength: 0);

        state = EndTurn(engine, state);
        AssertEnemy(state, 1, "hiss", block: 0, strength: 3);

        state = EndTurn(engine, state);
        Assert.Equal(64, state.Player.Hp);
    }

    private static EnemyCombatState Enemy(
        int instanceId,
        int formationPosition,
        int hp = 46) =>
        new(
            instanceId,
            "proto.enemy.nibbit",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal),
            FormationPosition: formationPosition);

    private static RunState CreateState(
        int ascension,
        params EnemyCombatState[] enemies)
    {
        var player = new PlayerState(
            100,
            100,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
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
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "nibbit-test",
            "nibbit-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "nibbit-test"),
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

    private static void AssertEnemy(
        RunState state,
        int instanceId,
        string moveId,
        int block,
        int strength)
    {
        var enemy = state.World!.Combat!.Enemies
            .Single(item => item.InstanceId == instanceId);
        Assert.Equal(moveId, enemy.LastMoveId);
        Assert.Equal(block, enemy.Block);
        var strengthPower = enemy.PowerStates
            .SingleOrDefault(power =>
                power.PowerId == "proto.power.strength");
        Assert.Equal(
            strength,
            strengthPower?.Stacks ?? 0);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }
}
