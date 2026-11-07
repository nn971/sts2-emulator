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
        Assert.All(catalog.Enemies, rule =>
        {
            var source = PrototypeContent.Enemy(rule.EnemyId);
            Assert.Equal(source.Moves.Select(move => move.Id), rule.MoveIds);
            Assert.Equal(source.Moves.Select(move => move.MaxConsecutiveUses),
                rule.MaxConsecutiveUses);
            Assert.Equal(source.MovePolicy.ToString(), rule.MovePolicy);
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
