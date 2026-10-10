using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeFlyconidTests
{
    [Fact]
    public void DefinitionMatchesPinnedActOneData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.flyconid");

        Assert.Equal("Flyconid", enemy.Name);
        Assert.Equal((47, 49), enemy.HpRangeAt(1, 0));
        Assert.Equal((51, 53), enemy.HpRangeAt(1, 8));

        Assert.Collection(
            enemy.Moves,
            vulnerable =>
            {
                Assert.Equal(
                    "vulnerable_spores",
                    vulnerable.Id);
                var debuff = Assert.Single(vulnerable.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyPlayerPower,
                    debuff.Kind);
                Assert.Equal(
                    "proto.power.vulnerable",
                    debuff.PowerId);
                Assert.Equal(2, debuff.Amount);
            },
            frail =>
            {
                Assert.Equal("frail_spores", frail.Id);
                Assert.Collection(
                    frail.Effects,
                    damage =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.DamagePlayer,
                            damage.Kind);
                        Assert.Equal(8, damage.AmountAt(1, 8));
                        Assert.Equal(9, damage.AmountAt(1, 9));
                    },
                    debuff =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.ApplyPlayerPower,
                            debuff.Kind);
                        Assert.Equal("proto.power.frail", debuff.PowerId);
                        Assert.Equal(2, debuff.Amount);
                    });
            },
            smash =>
            {
                Assert.Equal("smash", smash.Id);
                var damage = Assert.Single(smash.Effects);
                Assert.Equal(11, damage.AmountAt(1, 8));
                Assert.Equal(12, damage.AmountAt(1, 9));
            });

        var ai = Assert.IsType<
            PrototypeEnemyAiDefinition>(enemy.Ai);
        Assert.Equal("initial", ai.InitialStateId);

        var initial = Assert.Single(
            ai.States,
            state => state.Id == "initial");
        Assert.Collection(
            initial.Branches!,
            frail =>
            {
                Assert.Equal(
                    "frail_spores_move",
                    frail.TargetStateId);
                Assert.Equal(2, frail.Weight);
            },
            smash =>
            {
                Assert.Equal(
                    "smash_move",
                    smash.TargetStateId);
                Assert.Equal(1, smash.Weight);
            });

        var random = Assert.Single(
            ai.States,
            state => state.Id == "rand");
        var randomBranches = Assert.IsType<
            PrototypeEnemyAiBranch[]>(random.Branches);
        Assert.Equal(
            [3, 2, 1],
            randomBranches.Select(branch => branch.Weight));
        Assert.All(
            randomBranches,
            branch => Assert.Equal(
                PrototypeEnemyAiRepeatRule.CannotRepeat,
                branch.RepeatRule));
    }

    [Fact]
    public void OpenerIsFrailOrSmashButNeverVulnerable()
    {
        var seen = new HashSet<string>(
            StringComparer.Ordinal);

        for (var seed = 0; seed < 96; seed++)
        {
            var engine = new PrototypeGameEngine();
            var state = CreateState(
                $"flyconid-opener-{seed}");

            state = EndTurn(engine, state);
            var move = Assert.Single(
                state.World!.Combat!.Enemies).LastMoveId!;

            Assert.NotEqual("vulnerable_spores", move);
            seen.Add(move);
        }

        Assert.Equal(
            ["frail_spores", "smash"],
            seen.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("vulnerable_spores")]
    [InlineData("frail_spores")]
    [InlineData("smash")]
    public void RandomStateCannotRepeatPreviousMove(
        string previousMove)
    {
        for (var seed = 0; seed < 32; seed++)
        {
            var engine = new PrototypeGameEngine();
            var state = CreateState(
                $"flyconid-no-repeat-{previousMove}-{seed}",
                aiStateId: "rand",
                lastMoveId: previousMove,
                consecutiveMoveUses: 1);

            state = EndTurn(engine, state);

            Assert.NotEqual(
                previousMove,
                Assert.Single(
                    state.World!.Combat!.Enemies)
                    .LastMoveId);
        }
    }

    [Fact]
    public void FrailSporesAndVulnerableSporesApplyTimedDebuffs()
    {
        var engine = new PrototypeGameEngine();

        var frail = CreateState(
            "flyconid-frail",
            aiStateId: "frail_spores_move");
        frail = EndTurn(engine, frail);
        Assert.Equal(992, frail.Player.Hp);
        Assert.Equal(
            2,
            Assert.Single(
                frail.World!.Combat!.PlayerPowers,
                power => power.PowerId
                    == "proto.power.frail")
                .Stacks);

        var vulnerable = CreateState(
            "flyconid-vulnerable",
            aiStateId: "vulnerable_spores_move");
        vulnerable = EndTurn(engine, vulnerable);
        Assert.Equal(1000, vulnerable.Player.Hp);
        Assert.Equal(
            2,
            Assert.Single(
                vulnerable.World!.Combat!.PlayerPowers,
                power => power.PowerId
                    == "proto.power.vulnerable")
                .Stacks);
    }

    [Fact]
    public void ShroomAndSlimeUsesRandomMediumSlimeThenFlyconid()
    {
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.flyconid_normal");
        var seenMediums = new HashSet<string>(
            StringComparer.Ordinal);

        for (var seed = 0; seed < 64; seed++)
        {
            var specs = encounter.ResolveEnemySpecs(
                PrototypeRng.CreateBundle(
                    $"flyconid-formation-{seed}"));

            Assert.Collection(
                specs,
                slime =>
                {
                    Assert.Contains(
                        slime.EnemyId,
                        new[]
                        {
                            "proto.enemy.leaf_slime_m",
                            "proto.enemy.twig_slime_m"
                        });
                    Assert.Equal(0, slime.FormationPosition);
                    seenMediums.Add(slime.EnemyId);
                },
                flyconid =>
                {
                    Assert.Equal(
                        "proto.enemy.flyconid",
                        flyconid.EnemyId);
                    Assert.Equal(1, flyconid.FormationPosition);
                });
        }

        Assert.Equal(2, seenMediums.Count);
    }

    [Fact]
    public void OvergrowthFloraIsJaxfruitThenFlyconid()
    {
        var specs = PrototypeContent.Encounter(
                "proto.encounter.snapping_jaxfruit_normal")
            .ResolveEnemySpecs(
                PrototypeRng.CreateBundle(
                    "overgrowth-flora"));

        Assert.Collection(
            specs,
            jaxfruit =>
            {
                Assert.Equal(
                    "proto.enemy.snapping_jaxfruit",
                    jaxfruit.EnemyId);
                Assert.Equal(0, jaxfruit.FormationPosition);
            },
            flyconid =>
            {
                Assert.Equal(
                    "proto.enemy.flyconid",
                    flyconid.EnemyId);
                Assert.Equal(1, flyconid.FormationPosition);
            });
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
        string seed,
        string? aiStateId = null,
        string? lastMoveId = null,
        int consecutiveMoveUses = 0)
    {
        var player = new PlayerState(
            1000,
            1000,
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
                    "proto.enemy.flyconid",
                    49,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    LastMoveId: lastMoveId,
                    ConsecutiveMoveUses:
                        consecutiveMoveUses,
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
            seed,
            seed,
            0,
            RunPhase.Combat,
            player,
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
                null));
    }
}
