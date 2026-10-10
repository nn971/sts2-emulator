using System.Text.Json;

namespace Sts2Emulator.Core;

public sealed record V111InventoryItem(
    string Kind, string Id, string Scope, string? Pool, string? Act,
    string? Source, string? SourceSha256, string[]? Variants,
    string[]? Branches, bool BranchInventoryComplete = false,
    string[]? Encounters = null, string[]? Events = null, string[]? Ancients = null);

public sealed record V111Inventory(
    string Schema, string GameVersion, string GameCommit, string BuildFingerprint,
    string CorpusCommit, string SourceCommit,
    Dictionary<string, string> CorpusBlobs, V111InventoryItem[] Items);

public sealed record V111CoverageItem(
    string Kind, string NativeId, string Scope, string[] EngineIds,
    bool Catalogued, bool Registered, string Implementation, string Integration, string Validation,
    string[] MissingVariants, string[] MissingBranches, string[] Evidence);

public sealed record V111CoverageSummary(
    string Kind, int Eligible, int ScopeReviewRequired, int Excluded,
    int Catalogued, int Implemented, int Integrated, int Validated);

public sealed record V111CoverageReport(
    string Schema, string GameBuild, string InventoryHash,
    string CorpusCommit, string SourceCommit,
    V111CoverageSummary[] Summary, V111CoverageItem[] Items,
    bool ReleaseReady);

/// <summary>
/// Coverage is derived from the pinned inventory and executable registries.
/// Registration and prototype tests never imply native parity. Variant/branch
/// evidence remains missing until an explicit native fixture is recorded.
/// </summary>
public static class V111Coverage
{
    public const string SchemaId = "sts2-v111-coverage-v1";
    public static V111Inventory LoadInventory()
    {
        using var stream = typeof(V111Coverage).Assembly.GetManifestResourceStream(
            "Sts2Emulator.Core.v111-inventory.json")
            ?? throw new InvalidOperationException("Pinned v111 inventory is missing.");
        var inventory = JsonSerializer.Deserialize<V111Inventory>(stream, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
        }) ?? throw new InvalidDataException("Invalid v111 inventory.");
        if (inventory.Schema != "sts2-v111-inventory-v1"
            || inventory.BuildFingerprint != V111Build.Fingerprint
            || inventory.GameVersion != V111Build.Version
            || inventory.GameCommit != V111Build.Commit
            || inventory.CorpusCommit != V111Build.CorpusCommit
            || inventory.SourceCommit != V111Build.SourceCommit
            || inventory.Items.Select(x => (x.Kind, x.Id)).Distinct().Count() != inventory.Items.Length)
        {
            throw new InvalidDataException("Inventory identity or unique keys do not match the v111 pin.");
        }
        return inventory;
    }

    public static V111CoverageReport Create()
    {
        var inventory = LoadInventory();
        var items = inventory.Items.Select(Describe).ToArray();
        var summary = items.GroupBy(x => x.Kind).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(group => new V111CoverageSummary(
                group.Key, group.Count(x => x.Scope == "single-player"),
                group.Count(x => x.Scope == "review-required"),
                group.Count(x => x.Scope == "multiplayer-only"),
                group.Count(x => x.Scope == "single-player" && x.Catalogued),
                group.Count(x => x.Scope == "single-player" && x.Implementation == "implemented"),
                group.Count(x => x.Scope == "single-player" && x.Integration == "integrated"),
                group.Count(x => x.Scope == "single-player" && x.Validation == "native-parity")))
            .ToArray();
        var releaseReady = items.All(item => item.Scope == "multiplayer-only"
            || item.Scope == "single-player" && item.Implementation == "implemented"
                && item.Integration == "integrated" && item.Validation == "native-parity"
                && item.MissingVariants.Length == 0 && item.MissingBranches.Length == 0);
        return new V111CoverageReport(SchemaId, V111Build.Id, CanonicalJson.Sha256(inventory),
            inventory.CorpusCommit, inventory.SourceCommit, summary, items,
            ReleaseReady: releaseReady);
    }

    private static V111CoverageItem Describe(V111InventoryItem item)
    {
        var normalized = Normalize(item.Id);
        var ids = item.Kind switch
        {
            "cards" => PrototypeContent.Cards.Values
                .Where(card => Matches(item, card.Id, card.Name)).Select(card => card.Id).ToArray(),
            "potions" => PrototypeContent.Potions.Values
                .Where(potion => Matches(item, potion.Id, potion.Name)).Select(potion => potion.Id).ToArray(),
            "relics" => PrototypeContent.Relics.Values
                .Where(relic => Matches(item, relic.Id, relic.Name)).Select(relic => relic.Id).ToArray(),
            "powers" => PrototypeContent.Powers.Values
                .Where(power => Matches(item, power.Id, power.Name)).Select(power => power.Id).ToArray(),
            "monsters" => PrototypeContent.Enemies.Values
                .Where(enemy => Matches(item, enemy.Id, enemy.Name)).Select(enemy => enemy.Id).ToArray(),
            "encounters" => PrototypeContent.Encounters
                .Where(encounter => Normalize(encounter.Id.Split('.').Last()) == normalized)
                .Select(encounter => encounter.Id).ToArray(),
            "events" => PrototypeContent.Events.Values
                .Where(evt => Matches(item, evt.Id, evt.Name)).Select(evt => evt.Id).ToArray(),
            "characters" => V111Characters.All.Where(character => Normalize(character.Id) == normalized)
                .Select(character => character.Id).ToArray(),
            "acts" => Enum.GetNames<ActIdentity>().Where(act => Normalize(act) == normalized),
            "enchantments" => Enum.GetNames<PrototypeCardEnchantmentKind>().Where(kind => Normalize(kind) == normalized),
            "afflictions" => Enum.GetNames<PrototypeCardAfflictionKind>().Where(kind => Normalize(kind) == normalized),
            _ => Array.Empty<string>()
        };
        var engineIds = ids.Order(StringComparer.Ordinal).ToArray();
        var implementation = engineIds.Length == 0 ? "missing" : "partial";
        var integration = "missing";
        if (item.Scope == "single-player" && item.Kind == "cards")
        {
            if (engineIds.Any(id => PrototypeContent.Card(id).MechanicsImplemented
                && !PrototypeContent.Card(id).MultiplayerOnly))
            {
                implementation = "implemented";
                // Ordinary acquisition is established by pool membership. Event
                // or generated cards need separate path evidence, not a count.
                if (engineIds.Any(id => PrototypeContent.RewardCardPool.Contains(id)
                    || PrototypeContent.StartingDeck.Contains(id)
                    || PrototypeColorlessCards.ImplementedShopPool.Contains(id)))
                    integration = "integrated";
            }
        }
        if (item.Scope == "single-player" && item.Kind == "encounters")
        {
            var pools = PrototypeContent.OvergrowthWeakEncounterPool
                .Concat(PrototypeContent.OvergrowthNormalEncounterPool)
                .Concat(PrototypeContent.OvergrowthEliteEncounterPool)
                .Concat(PrototypeContent.OvergrowthBossEncounterPool)
                .Concat(PrototypeNativeUnderdocks.SupportedWeakEncounterIds)
                .Concat(PrototypeNativeUnderdocks.SupportedNormalEncounterIds)
                .Concat(PrototypeNativeUnderdocks.SupportedEliteEncounterIds)
                .Concat(PrototypeNativeUnderdocks.NativeBossEncounterIds).ToHashSet(StringComparer.Ordinal);
            if (engineIds.Any(pools.Contains))
            {
                implementation = "implemented";
                integration = "integrated";
            }
        }
        if (item.Scope == "multiplayer-only")
        {
            implementation = "excluded";
            integration = "excluded";
        }
        return new V111CoverageItem(item.Kind, item.Id, item.Scope, engineIds,
            true, engineIds.Length > 0, implementation, integration, "unverified",
            item.Variants ?? [], item.Branches ?? [],
            item.Source is null ? [] : [$"{V111Build.SourceCommit}:{item.Source}#{item.SourceSha256}"]);
    }

    private static bool Matches(V111InventoryItem item, string engineId, string name)
    {
        var key = Normalize(item.Id);
        var last = Normalize(engineId.Split('.').Last());
        if (item.Kind == "cards" && (key.StartsWith("STRIKE", StringComparison.Ordinal)
            || key.StartsWith("DEFEND", StringComparison.Ordinal)))
        {
            return engineId.StartsWith($"proto.{item.Pool}.", StringComparison.Ordinal)
                && key == last + Normalize(item.Pool ?? "");
        }
        return key == last || key == Normalize(name);
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
