using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PublicEnemyIntentCatalogV111Tests
{
    [Fact]
    public void CatalogCopiesSourceMoveRulesAndExactNativeIntegerMasses()
    {
        var catalog = PublicEnemyIntentCatalogV111.Create();
        Assert.Equal(PublicEnemyIntentCatalogV111.SchemaId, catalog.Schema);
        Assert.Equal(ChanceLawV111.NativeSourceGridSize,
            catalog.ProbabilityDenominator);
        Assert.NotEmpty(catalog.Enemies);
        Assert.NotNull(catalog.DamagePowers);
        Assert.NotNull(catalog.DamageRelics);
        Assert.NotNull(catalog.EnemyStatusPowerIds);
        Assert.Equal(PrototypeContent.Powers.Count, catalog.DamagePowers!.Length);
        Assert.Equal(PrototypeContent.EnemyStatusPowerIds,
            catalog.EnemyStatusPowerIds);
        Assert.All(catalog.Enemies, rule =>
        {
            var source = PrototypeContent.Enemy(rule.EnemyId);
            Assert.Equal(source.Moves.Select(move => move.Id), rule.MoveIds);
            Assert.Equal(source.Moves.Select(move => move.MaxConsecutiveUses),
                rule.MaxConsecutiveUses);
            Assert.Equal(source.MovePolicy.ToString(), rule.MovePolicy);
            Assert.Equal(source.Moves.Length, rule.MoveDamage?.Length);
            for (var j = 0; j < source.Moves.Length; j++)
            {
                var move = source.Moves[j];
                var damage = Assert.IsType<PublicEnemyMoveDamageV111>(
                    rule.MoveDamage![j]);
                Assert.Equal(move.Id, damage.MoveId);
                Assert.Equal(move.Effects.Count(effect =>
                    effect.Kind == PrototypeEnemyEffectKind.DamagePlayer),
                    damage.Effects.Length);
                var sourceDamage = move.Effects.Where(effect =>
                    effect.Kind == PrototypeEnemyEffectKind.DamagePlayer).ToArray();
                for (var k = 0; k < sourceDamage.Length; k++)
                {
                    Assert.Equal(sourceDamage[k].Amount, damage.Effects[k].Amount);
                    Assert.Equal(sourceDamage[k].Repetitions, damage.Effects[k].Repetitions);
                    Assert.Equal(sourceDamage[k].IsAttack, damage.Effects[k].IsAttack);
                }
            }
            if (source.Ai is null)
            {
                Assert.Null(rule.Ai);
            }
            else
            {
                var publicAi = Assert.IsType<PublicEnemyAiDefinitionV111>(rule.Ai);
                Assert.Equal(source.Ai.InitialStateId, publicAi.InitialStateId);
                Assert.Equal(source.Ai.States.Length, publicAi.States.Length);
                for (var i = 0; i < source.Ai.States.Length; i++)
                {
                    var expected = source.Ai.States[i];
                    var actual = publicAi.States[i];
                    Assert.Equal(expected.Id, actual.Id);
                    Assert.Equal(expected.Kind.ToString(), actual.Kind);
                    Assert.Equal(expected.MoveIndex, actual.MoveIndex);
                    Assert.Equal(expected.NextStateId, actual.NextStateId);
                    Assert.Equal(expected.Branches?.Length ?? 0,
                        actual.Branches.Length);
                    Assert.Equal(expected.ConditionalBranches?.Length ?? 0,
                        actual.ConditionalBranches.Length);
                }
            }
        });
        foreach (var item in catalog.NativeNextIntWeights)
        {
            var count = int.Parse(item.Key);
            Assert.Equal(count, item.Value.Length);
            Assert.All(item.Value, weight => Assert.True(weight > 0));
            Assert.Equal(catalog.ProbabilityDenominator, item.Value.Sum());
            Assert.Equal(
                ChanceLawEngineV111.NativeNextIntGridWeights(count), item.Value);
        }
    }

    [Fact]
    public void CatalogDoesNotDependOnSeedOrEnemyPrivateState()
    {
        var original = CanonicalJson.Serialize(PublicEnemyIntentCatalogV111.Create());
        var again = CanonicalJson.Serialize(PublicEnemyIntentCatalogV111.Create());
        Assert.Equal(original, again);
        using var json = JsonDocument.Parse(original);
        var root = json.RootElement;
        Assert.True(root.TryGetProperty("enemies", out _));
        Assert.False(root.TryGetProperty("rng", out _));
        Assert.False(root.TryGetProperty("ai_state_id", out _));
        Assert.False(root.TryGetProperty("move_use_counts", out _));
    }
}
