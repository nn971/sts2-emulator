namespace Sts2Emulator.Core;

public sealed record PrototypeCapabilityManifest(
    string RulesetId,
    string AiSchemaId,
    string CharacterId,
    string[] CardIds,
    string[] PotionIds,
    string[] RelicIds,
    string[] PowerIds,
    string[] EnemyIds,
    string[] EventIds,
    string[] EncounterIds,
    string[] CombatEffectKinds,
    string[] CombatEventKinds,
    string[] RunPhases,
    V111CoverageSummary[]? V111Coverage = null);

public static class PrototypeCapabilities
{
    public static PrototypeCapabilityManifest Create() =>
        new(
            RulesetId: PrototypeContent.RulesetId,
            AiSchemaId: PrototypeAiEnvironment.SchemaId,
            CharacterId: PrototypeContent.CharacterId,
            CardIds: PrototypeContent.Cards.Keys.Order(StringComparer.Ordinal).ToArray(),
            PotionIds: PrototypeContent.Potions.Keys.Order(StringComparer.Ordinal).ToArray(),
            RelicIds: PrototypeContent.Relics.Keys.Order(StringComparer.Ordinal).ToArray(),
            PowerIds: PrototypeContent.Powers.Keys.Order(StringComparer.Ordinal).ToArray(),
            EnemyIds: PrototypeContent.Enemies.Keys.Order(StringComparer.Ordinal).ToArray(),
            EventIds: PrototypeContent.Events.Keys.Order(StringComparer.Ordinal).ToArray(),
            EncounterIds: PrototypeContent.Encounters
                .Select(encounter => encounter.Id)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            CombatEffectKinds: Enum.GetNames<PrototypeCombatEffectKind>()
                .Order(StringComparer.Ordinal)
                .ToArray(),
            CombatEventKinds: Enum.GetNames<PrototypeCombatEventKind>()
                .Order(StringComparer.Ordinal)
                .ToArray(),
            RunPhases: Enum.GetNames<RunPhase>()
                .Order(StringComparer.Ordinal)
                .ToArray(),
            V111Coverage: Sts2Emulator.Core.V111Coverage.Create().Summary);
}
