using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeCapabilityTests
{
    [Fact]
    public void ManifestMatchesCurrentPrototypeCatalogAndSchemas()
    {
        var manifest = PrototypeCapabilities.Create();

        Assert.Equal(PrototypeContent.RulesetId, manifest.RulesetId);
        Assert.Equal(PrototypeAiEnvironment.SchemaId, manifest.AiSchemaId);
        Assert.Equal(PrototypeContent.CharacterId, manifest.CharacterId);

        Assert.Equal(
            PrototypeContent.Cards.Keys.Order(StringComparer.Ordinal),
            manifest.CardIds);
        Assert.Equal(
            PrototypeContent.Potions.Keys.Order(StringComparer.Ordinal),
            manifest.PotionIds);
        Assert.Equal(
            PrototypeContent.Relics.Keys.Order(StringComparer.Ordinal),
            manifest.RelicIds);
        Assert.Equal(
            PrototypeContent.Enemies.Keys.Order(StringComparer.Ordinal),
            manifest.EnemyIds);
        Assert.Equal(
            PrototypeContent.Encounters.Select(item => item.Id).Order(StringComparer.Ordinal),
            manifest.EncounterIds);

        Assert.Equal(
            manifest.CardIds.Length,
            manifest.CardIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            manifest.EncounterIds.Length,
            manifest.EncounterIds.Distinct(StringComparer.Ordinal).Count());
    }
}
