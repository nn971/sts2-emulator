using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeToadpoleTests
{
    [Fact]
    public void DefinitionMatchesPinnedAct1Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.toadpole");

        Assert.Equal("Toadpole", enemy.Name);
        Assert.Equal((21, 25), enemy.HpRangeAt(1, 0));
        Assert.Equal((22, 26), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.StateMachine,
            enemy.MovePolicy);

        Assert.Collection(
            enemy.Moves,
            spit =>
            {
                Assert.Equal("spike_spit", spit.Id);
                var damage = Assert.Single(spit.Effects);
                Assert.Equal(3, damage.Repetitions);
                Assert.Equal(3, damage.AmountAt(1, 8));
                Assert.Equal(4, damage.AmountAt(1, 9));
            },
            whirl =>
            {
                Assert.Equal("whirl", whirl.Id);
                var damage = Assert.Single(whirl.Effects);
                Assert.Equal(7, damage.AmountAt(1, 8));
                Assert.Equal(8, damage.AmountAt(1, 9));
            },
            spiken =>
            {
                Assert.Equal("spiken", spiken.Id);
                var thorns = Assert.Single(spiken.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyEnemyPower,
                    thorns.Kind);
                Assert.Equal("proto.power.thorns", thorns.PowerId);
                Assert.Equal(2, thorns.Amount);
            });

        var ai = Assert.IsType<
            PrototypeEnemyAiDefinition>(enemy.Ai);
        Assert.Equal("init", ai.InitialStateId);
        var initial = Assert.Single(
            ai.States,
            state => state.Id == "init");
        Assert.Equal(
            PrototypeEnemyAiStateKind.Conditional,
            initial.Kind);
        Assert.Collection(
            initial.ConditionalBranches!,
            back =>
            {
                Assert.Equal("whirl_move", back.TargetStateId);
                Assert.Equal(
                    PrototypeEnemyAiConditionKind.IsNotFront,
                    back.Condition);
            },
            front =>
            {
                Assert.Equal("spiken_move", front.TargetStateId);
                Assert.Equal(
                    PrototypeEnemyAiConditionKind.IsFront,
                    front.Condition);
            });
    }

    [Fact]
    public void FrontAndBackToadpolesChooseDifferentNativeOpeners()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            Enemy(1, 0),
            Enemy(2, 1));

        state = EndTurn(engine, state);

        Assert.Equal(93, state.Player.Hp);
        Assert.Equal(
            "spiken",
            state.World!.Combat!.Enemies[0].LastMoveId);
        Assert.Equal(
            "whirl",
            state.World.Combat.Enemies[1].LastMoveId);
        Assert.Equal(
            2,
            Assert.Single(
                state.World.Combat.Enemies[0].PowerStates,
                power => power.PowerId
                    == "proto.power.thorns")
                .Stacks);

        state = EndTurn(engine, state);

        Assert.Equal(84, state.Player.Hp);
        Assert.Equal(
            "spike_spit",
            state.World!.Combat!.Enemies[0].LastMoveId);
        Assert.Equal(
            "spiken",
            state.World.Combat.Enemies[1].LastMoveId);
    }

    [Fact]
    public void BackToadpoleBecomesFrontWhenEarlierFormationSlotIsDead()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            Enemy(1, 0, hp: 0),
            Enemy(2, 1));

        state = EndTurn(engine, state);

        Assert.Equal(100, state.Player.Hp);
        Assert.Null(
            state.World!.Combat!.Enemies[0].LastMoveId);
        Assert.Equal(
            "spiken",
            state.World.Combat.Enemies[1].LastMoveId);
    }

    private static EnemyCombatState Enemy(
        int instanceId,
        int formationPosition,
        int hp = 25) =>
        new(
            instanceId,
            "proto.enemy.toadpole",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal),
            FormationPosition: formationPosition);

    private static RunState CreateState(
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
            "toadpole-test",
            "toadpole-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "toadpole-test"),
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
                null));
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
