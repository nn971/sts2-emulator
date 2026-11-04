using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeEnemyEffectPowerCatalogTests
{
    [Fact]
    public void LegacyEnemyStatusDefinitionsAreProjectionsOfCanonicalPowers()
    {
        Assert.Equal(
            new[] { "proto.status.poison", "proto.status.vulnerable", "proto.status.weak" },
            PrototypeContent.EnemyStatusPowerIds.Keys.Order(StringComparer.Ordinal));

        foreach (var (statusId, powerId) in PrototypeContent.EnemyStatusPowerIds)
        {
            var power = PrototypeContent.Power(powerId);
            var legacy = PrototypeContent.Status(statusId);
            Assert.True(power.IsDebuff);
            Assert.Equal(new PrototypeStatusDefinition(
                statusId, IsDebuff: power.IsDebuff,
                TriggerStage: power.EnemyEffectTriggerStage,
                TriggerKind: power.EnemyEffectTriggerKind,
                DecayOnTrigger: power.EnemyEffectDecayOnTrigger,
                DecayStage: power.EnemyEffectDecayStage,
                DecayAtStage: power.EnemyEffectDecayAtStage,
                OutgoingDamageNumerator: power.EnemyOutgoingDamageNumerator,
                OutgoingDamageDenominator: power.EnemyOutgoingDamageDenominator,
                IncomingAttackDamageNumerator: power.EnemyIncomingAttackDamageNumerator,
                IncomingAttackDamageDenominator: power.EnemyIncomingAttackDamageDenominator),
                legacy);
        }
    }

    [Fact]
    public void PoisonDamageAndEnemyWeakVulnerableTimingMatchTheLegacyContract()
    {
        var poison = PrototypeContent.Power("proto.power.poison");
        Assert.Equal(PrototypeTurnStage.EnemyTurnStart, poison.EnemyEffectTriggerStage);
        Assert.Equal(PrototypeStatusTriggerKind.DamageSelfByStacks,
            poison.EnemyEffectTriggerKind);
        Assert.Equal(1, poison.EnemyEffectDecayOnTrigger);

        var weak = PrototypeContent.Power("proto.power.weak");
        Assert.Equal(PrototypeTurnStage.EnemyTurnEnd, weak.EnemyEffectDecayStage);
        Assert.Equal(1, weak.EnemyEffectDecayAtStage);
        Assert.Equal(3, weak.EnemyOutgoingDamageNumerator);
        Assert.Equal(4, weak.EnemyOutgoingDamageDenominator);
        Assert.True(weak.DecrementAtPlayerTurnEnd);
        Assert.Equal(3, weak.PlayerAttackDamageNumerator);
        Assert.Equal(4, weak.PlayerAttackDamageDenominator);

        var vulnerable = PrototypeContent.Power("proto.power.vulnerable");
        Assert.Equal(PrototypeTurnStage.EnemyTurnEnd, vulnerable.EnemyEffectDecayStage);
        Assert.Equal(1, vulnerable.EnemyEffectDecayAtStage);
        Assert.Equal(3, vulnerable.EnemyIncomingAttackDamageNumerator);
        Assert.Equal(2, vulnerable.EnemyIncomingAttackDamageDenominator);
        Assert.True(vulnerable.DecrementAtPlayerTurnEnd);
        Assert.Equal(3, vulnerable.PlayerIncomingAttackDamageNumerator);
        Assert.Equal(2, vulnerable.PlayerIncomingAttackDamageDenominator);
    }

    [Fact]
    public void EnemyEffectStacksReadsExactlyOneCanonicalStorageShape()
    {
        var legacy = new EnemyCombatState(
            1, "proto.enemy.test", 30, 0, 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["proto.status.weak"] = 2
            });
        var canonical = legacy with
        {
            Statuses = new Dictionary<string, int>(StringComparer.Ordinal),
            Powers =
            [
                new PrototypePowerInstanceState("proto.power.weak", 2, 7)
            ]
        };

        Assert.Equal(2, legacy.EnemyEffectStacks("proto.status.weak"));
        Assert.Equal(2, canonical.EnemyEffectStacks("proto.status.weak"));
        Assert.Equal(0, canonical.EnemyEffectStacks("proto.status.poison"));
        Assert.Equal(0, canonical.EnemyEffectStacks("unknown.status"));

        // Legacy card status and a separate canonical relic power may both
        // be present, and both applications must contribute their stacks.
        var combined = legacy with { Powers = canonical.Powers };
        Assert.Equal(4, combined.EnemyEffectStacks("proto.status.weak"));
        // Staged migration preserves distinct power instances and fork identity.
        var fork = canonical.Fork();
        Assert.NotSame(canonical.Powers, fork.Powers);
        Assert.Equal(2, fork.EnemyEffectStacks("proto.status.weak"));
    }

    [Fact]
    public void EnemyWeakDamageIsEqualUnderLegacyAndCanonicalPowerStorage()
    {
        var legacy = new EnemyCombatState(
            11, "proto.enemy.crawler", 40, 0, 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["proto.status.weak"] = 3
            });
        var canonical = legacy with
        {
            Statuses = new Dictionary<string, int>(StringComparer.Ordinal),
            Powers = [new PrototypePowerInstanceState("proto.power.weak", 3, 2)]
        };
        var mixed = legacy with { Powers = canonical.Powers };
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: [], DrawPile: [], DiscardPile: [], ExhaustPile: [],
            Enemies: [legacy], NextCardInstanceId: 1, Cards: [],
            PlayerPowers: [], NextPowerApplicationOrder: 3);
        var attack = new PrototypeEnemyEffectSpec(
            PrototypeEnemyEffectKind.DamagePlayer, Amount: 20);
        var nonAttack = attack with { IsAttack = false };

        foreach (var enemy in new[] { legacy, canonical, mixed })
        {
            Assert.Equal(15, PrototypeGameEngine.EnemyHitDamage(
                enemy, combat, attack, act: 1, ascension: 0));
            Assert.Equal(20, PrototypeGameEngine.EnemyHitDamage(
                enemy, combat, nonAttack, act: 1, ascension: 0));
        }
    }

    private static CombatState EffectCombat(EnemyCombatState enemy) =>
        new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: [], DrawPile: [], DiscardPile: [], ExhaustPile: [],
            Enemies: [enemy], NextCardInstanceId: 1, Cards: [],
            PlayerPowers: [], NextPowerApplicationOrder: 100)
        {
            CanonicalEnemyEffectPowers = true
        };

    private static EnemyCombatState EffectEnemy(int hp = 30) =>
        new(11, "proto.enemy.crawler", hp, 0, 0,
            new Dictionary<string, int>(StringComparer.Ordinal));

    [Fact]
    public void OptInApplicationUsesPowerInstancesAndPreservesLegacyDefault()
    {
        var optIn = EffectCombat(EffectEnemy());
        var weak = PrototypeGameEngine.ApplyEnemyStatusForSourceRegression(
            optIn, 11, "proto.status.weak", 2);
        var target = Assert.Single(weak.Enemies);
        Assert.Empty(target.Statuses);
        Assert.Equal(2, target.EnemyEffectStacks("proto.status.weak"));
        var power = Assert.Single(target.PowerStates);
        Assert.Equal("proto.power.weak", power.PowerId);
        Assert.Equal(100, power.ApplicationOrder);
        Assert.Equal(101, weak.NextPowerApplicationOrder);

        weak = PrototypeGameEngine.ApplyEnemyStatusForSourceRegression(
            weak, 11, "proto.status.weak", 3);
        Assert.Equal(5, Assert.Single(weak.Enemies)
            .EnemyEffectStacks("proto.status.weak"));
        Assert.Equal(101, weak.NextPowerApplicationOrder);

        var legacy = optIn with { CanonicalEnemyEffectPowers = false };
        legacy = PrototypeGameEngine.ApplyEnemyStatusForSourceRegression(
            legacy, 11, "proto.status.weak", 2);
        Assert.Equal(2, Assert.Single(legacy.Enemies)
            .Statuses["proto.status.weak"]);
        Assert.Empty(Assert.Single(legacy.Enemies).PowerStates);
    }

    [Fact]
    public void CanonicalPoisonTurnsAndImmediateTriggerPreserveStackDecay()
    {
        var combat = EffectCombat(EffectEnemy());
        combat = PrototypeGameEngine.ApplyEnemyStatusForSourceRegression(
            combat, 11, "proto.status.poison", 3);
        var rng = PrototypeRng.CreateBundle("p1-poison-life-cycle");
        combat = PrototypeGameEngine.ResolveEnemyStatusStageForSourceRegression(
            combat, PrototypeTurnStage.EnemyTurnStart, rng);
        var afterFirst = Assert.Single(combat.Enemies);
        Assert.Equal(27, afterFirst.Hp);
        Assert.Equal(2, afterFirst.EnemyEffectStacks("proto.status.poison"));
        Assert.Empty(afterFirst.Statuses);

        combat = PrototypeGameEngine.TriggerEnemyStatusForSourceRegression(
            combat, 11, "proto.status.poison", rng);
        var afterTrigger = Assert.Single(combat.Enemies);
        Assert.Equal(25, afterTrigger.Hp);
        Assert.Equal(1, afterTrigger.EnemyEffectStacks("proto.status.poison"));

        combat = PrototypeGameEngine.ResolveEnemyStatusStageForSourceRegression(
            combat, PrototypeTurnStage.EnemyTurnStart, rng);
        var expired = Assert.Single(combat.Enemies);
        Assert.Equal(24, expired.Hp);
        Assert.Equal(0, expired.EnemyEffectStacks("proto.status.poison"));
        Assert.Empty(expired.PowerStates);
    }

    [Fact]
    public void EnemyWeakAndVulnerableExpireAtEnemySideTurnEnd()
    {
        var combat = EffectCombat(EffectEnemy());
        foreach (var id in new[] { "proto.status.weak", "proto.status.vulnerable" })
            combat = PrototypeGameEngine.ApplyEnemyStatusForSourceRegression(
                combat, 11, id, 2);

        var rng = PrototypeRng.CreateBundle("p1-weak-vulnerable-decay");
        combat = PrototypeGameEngine.ResolveEnemyStatusStageForSourceRegression(
            combat, PrototypeTurnStage.EnemyTurnStart, rng);
        Assert.Equal(2, Assert.Single(combat.Enemies)
            .EnemyEffectStacks("proto.status.weak"));
        combat = PrototypeGameEngine.ResolveEnemyStatusStageForSourceRegression(
            combat, PrototypeTurnStage.EnemyTurnEnd, rng);
        Assert.Equal(1, Assert.Single(combat.Enemies)
            .EnemyEffectStacks("proto.status.weak"));
        Assert.Equal(1, Assert.Single(combat.Enemies)
            .EnemyEffectStacks("proto.status.vulnerable"));

        combat = PrototypeGameEngine.ResolveEnemyStatusStageForSourceRegression(
            combat, PrototypeTurnStage.EnemyTurnEnd, rng);
        Assert.Empty(Assert.Single(combat.Enemies).PowerStates);
    }

    [Fact]
    public void CanonicalPoisonMultiplierAndArtifactDebuffBlocking()
    {
        var artifact = EffectEnemy() with
        {
            Powers = [new PrototypePowerInstanceState("proto.power.artifact", 1, 1)]
        };
        var combat = EffectCombat(artifact);
        combat = PrototypeGameEngine.ApplyEnemyStatusForSourceRegression(
            combat, 11, "proto.status.poison", 3);
        Assert.Equal(0, Assert.Single(combat.Enemies)
            .EnemyEffectStacks("proto.status.poison"));
        Assert.Empty(Assert.Single(combat.Enemies).PowerStates);

        combat = PrototypeGameEngine.ApplyEnemyStatusForSourceRegression(
            combat, 11, "proto.status.poison", 3);
        combat = PrototypeGameEngine.MultiplyEnemyStatusForSourceRegression(
            combat, 11, "proto.status.poison", 2);
        Assert.Equal(6, Assert.Single(combat.Enemies)
            .EnemyEffectStacks("proto.status.poison"));
        combat = PrototypeGameEngine.MultiplyEnemyStatusForSourceRegression(
            combat, 11, "proto.status.poison", 0);
        Assert.Equal(0, Assert.Single(combat.Enemies)
            .EnemyEffectStacks("proto.status.poison"));
    }
}
