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
            Assert.Equal(15, PrototypeCombat.EnemyHitDamage(
                enemy, combat, attack, act: 1, ascension: 0));
            Assert.Equal(20, PrototypeCombat.EnemyHitDamage(
                enemy, combat, nonAttack, act: 1, ascension: 0));
        }
    }
}
