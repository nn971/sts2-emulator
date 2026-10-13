using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeMawlerTests
{
    [Fact]
    public void DefinitionMatchesPinnedAct1Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.mawler");

        Assert.Equal("Mawler", enemy.Name);
        Assert.Equal((72, 72), enemy.HpRangeAt(1, 0));
        Assert.Equal((76, 76), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.StateMachine,
            enemy.MovePolicy);

        Assert.Collection(
            enemy.Moves,
            rip =>
            {
                Assert.Equal("rip_and_tear", rip.Id);
                var damage = Assert.Single(rip.Effects);
                Assert.Equal(14, damage.AmountAt(1, 8));
                Assert.Equal(16, damage.AmountAt(1, 9));
            },
            roar =>
            {
                Assert.Equal("roar", roar.Id);
                var debuff = Assert.Single(roar.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyPlayerPower,
                    debuff.Kind);
                Assert.Equal(
                    "proto.power.vulnerable",
                    debuff.PowerId);
                Assert.Equal(3, debuff.Amount);
            },
            claw =>
            {
                Assert.Equal("claw", claw.Id);
                var damage = Assert.Single(claw.Effects);
                Assert.Equal(2, damage.Repetitions);
                Assert.Equal(4, damage.AmountAt(1, 8));
                Assert.Equal(5, damage.AmountAt(1, 9));
            });

        var ai = Assert.IsType<
            PrototypeEnemyAiDefinition>(enemy.Ai);
        Assert.Equal("claw_move", ai.InitialStateId);
        var random = Assert.Single(
            ai.States,
            state => state.Id == "rand");
        Assert.Equal(
            PrototypeEnemyAiStateKind.Random,
            random.Kind);

        Assert.Collection(
            random.Branches!,
            rip =>
            {
                Assert.Equal(
                    "rip_and_tear_move",
                    rip.TargetStateId);
                Assert.Equal(
                    PrototypeEnemyAiRepeatRule.CannotRepeat,
                    rip.RepeatRule);
            },
            roar =>
            {
                Assert.Equal("roar_move", roar.TargetStateId);
                Assert.Equal(
                    PrototypeEnemyAiRepeatRule.UseOnlyOnce,
                    roar.RepeatRule);
            },
            claw =>
            {
                Assert.Equal("claw_move", claw.TargetStateId);
                Assert.Equal(
                    PrototypeEnemyAiRepeatRule.CannotRepeat,
                    claw.RepeatRule);
            });
    }

    [Fact]
    public void ReferenceNormalEncounterIsSingletonMawler()
    {
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.mawler_normal");
        var spec = Assert.Single(
            encounter.ResolveEnemySpecs(
                PrototypeRng.CreateBundle(
                    "mawler-normal-formation")));

        Assert.Equal("proto.enemy.mawler", spec.EnemyId);
        Assert.Equal(0, spec.FormationPosition);
    }

    [Fact]
    public void ForcedRoarAppliesVulnerableAndThenRandomAttacksRespectIt()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            aiStateId: "roar_move");

        state = EndTurn(engine, state);

        Assert.Equal(500, state.Player.Hp);
        Assert.Equal(
            "roar",
            Assert.Single(
                state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(
            3,
            Assert.Single(
                state.World.Combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.vulnerable")
                .Stacks);

        state = EndTurn(engine, state);

        var enemyAfterAttack = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Contains(
            enemyAfterAttack.LastMoveId,
            new[] { "rip_and_tear", "claw" });
        Assert.Contains(
            500 - state.Player.Hp,
            new[] { 21, 12 });
        Assert.Equal(
            2,
            Assert.Single(
                state.World.Combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.vulnerable")
                .Stacks);

        var previous = enemyAfterAttack.LastMoveId;
        var hpBeforeThird = state.Player.Hp;

        state = EndTurn(engine, state);

        var enemyAfterThird = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.NotEqual(
            previous,
            enemyAfterThird.LastMoveId);
        Assert.Contains(
            hpBeforeThird - state.Player.Hp,
            new[] { 21, 12 });
        Assert.Equal(
            1,
            Assert.Single(
                state.World.Combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.vulnerable")
                .Stacks);
        Assert.Equal(
            1,
            enemyAfterThird.MoveUseCounts!["roar"]);
    }

    [Fact]
    public void NativeOpenerIsClaw()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        state = EndTurn(engine, state);

        Assert.Equal(492, state.Player.Hp);
        Assert.Equal(
            "claw",
            Assert.Single(
                state.World!.Combat!.Enemies).LastMoveId);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState CreateState(
        string? aiStateId = null)
    {
        var player = new PlayerState(
            500,
            500,
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
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.mawler",
                    72,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    AiStateId: aiStateId,
                    MoveUseCounts:
                        new Dictionary<string, int>(
                            StringComparer.Ordinal))
            ],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "mawler-test",
            "mawler-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("mawler-test"),
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
}
