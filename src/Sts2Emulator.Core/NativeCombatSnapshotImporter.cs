using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>Immutable, structural projection of the native 0.2 combat snapshot.</summary>
public sealed record NativeCardView(
    long InstanceId, string ModelId, int UpgradeLevel, int Cost, string TargetType);

public sealed record NativeCreatureView(
    string InstanceId, string ModelId, int Hp, int MaxHp, int Block, bool Alive,
    JsonElement Powers);

public sealed record NativeCombatSnapshot(
    int Hp, int MaxHp, int Block, int Energy, int MaxEnergy, int Turn,
    NativeCreatureView[] Creatures,
    NativeCardView[] Hand,
    NativeCardView[] DrawPile,
    NativeCardView[] DiscardPile,
    NativeCardView[] ExhaustPile,
    JsonElement PlayerPowers,
    JsonElement SourceState);

public sealed record NativeCombatImportResult(
    string Status, NativeCombatSnapshot? Snapshot,
    string[] MissingFields, string[] Diagnostics,
    string[] VerifiedFields, string[] MissingContinuationFields,
    bool ReadyForDeterministicStep);

/// <summary>
/// Strict, read-only native 0.2 combat state parser. The imported projection
/// can be inspected and diffed without native assemblies. Deterministic stepping
/// remains explicitly gated until continuation state and action semantics have
/// been validated independently against a pinned game build.
/// </summary>
public static class NativeCombatSnapshotImporter
{
    public static readonly string[] RequiredVisibleFields =
    [
        "$.player.hp", "$.player.max_hp", "$.player.block", "$.player.powers",
        "$.combat.energy", "$.combat.max_energy", "$.combat.turn",
        "$.combat.creatures", "$.combat.hand", "$.combat.draw_pile",
        "$.combat.discard_pile", "$.combat.exhaust_pile"
    ];

    public static readonly string[] RequiredContinuationFields =
    [
        "$.combat.pending_effects", "$.combat.move_history",
        "$.combat.hidden_card_state", "$.combat.relic_continuation",
        "$.combat.potion_continuation", "$.combat.rng_state"
    ];

    public static NativeCombatImportResult Import(
        JsonElement state, IReadOnlyCollection<string>? declaredVerifiedFields = null)
    {
        var diagnostics = new List<string>();
        var missing = new List<string>();
        HashSet<string> verified = declaredVerifiedFields is null
            ? new(StringComparer.Ordinal)
            : new(declaredVerifiedFields, StringComparer.Ordinal);

        // A present field is only structurally parsed. It becomes verified
        // after an independent native and emulator adapter audit.
        foreach (string path in RequiredVisibleFields)
            if (!PathPresent(state, path))
                missing.Add(path);

        if (missing.Count > 0)
            return new NativeCombatImportResult("UNSUPPORTED", null, missing.ToArray(),
                ["Required canonical scalar/zone fields are absent"],
                RequiredVisibleFields.Where(verified.Contains).ToArray(),
                RequiredContinuationFields, false);

        try
        {
            JsonElement player = state.GetProperty("player");
            JsonElement combat = state.GetProperty("combat");

            int hp = ReadInt(player, "hp");
            int maxHp = ReadInt(player, "max_hp");
            int block = ReadInt(player, "block");
            int energy = ReadInt(combat, "energy");
            int maxEnergy = ReadInt(combat, "max_energy");
            int turn = ReadInt(combat, "turn");
            if (hp < 0 || maxHp < 1 || hp > maxHp || block < 0 ||
                energy < 0 || maxEnergy < 0 || turn < 0)
                diagnostics.Add("INVALID_SCALAR_RANGE");

            var creatures = ReadCreatures(combat.GetProperty("creatures"), diagnostics);
            var hand = ReadCards(combat.GetProperty("hand"), "hand", diagnostics);
            var draw = ReadCards(combat.GetProperty("draw_pile"), "draw_pile", diagnostics);
            var discard = ReadCards(combat.GetProperty("discard_pile"), "discard_pile", diagnostics);
            var exhaust = ReadCards(combat.GetProperty("exhaust_pile"), "exhaust_pile", diagnostics);
            var allIds = hand.Concat(draw).Concat(discard).Concat(exhaust)
                .Select(card => card.InstanceId).ToArray();
            if (allIds.Distinct().Count() != allIds.Length)
                diagnostics.Add("DUPLICATE_CARD_INSTANCE_ACROSS_ZONES");
            if (creatures.Select(creature => creature.InstanceId).Distinct(StringComparer.Ordinal).Count()
                != creatures.Length)
                diagnostics.Add("DUPLICATE_CREATURE_INSTANCE");

            if (diagnostics.Count > 0)
                return new NativeCombatImportResult("UNSUPPORTED", null, [],
                    diagnostics.ToArray(), RequiredVisibleFields.Where(verified.Contains).ToArray(),
                    RequiredContinuationFields, false);

            var snapshot = new NativeCombatSnapshot(
                hp, maxHp, block, energy, maxEnergy, turn,
                creatures, hand, draw, discard, exhaust,
                player.GetProperty("powers").Clone(), state.Clone());
            string[] continuationMissing = RequiredContinuationFields
                .Where(path => !PathPresent(state, path) || !verified.Contains(path))
                .ToArray();
            string[] unverified = RequiredVisibleFields.Where(path => !verified.Contains(path)).ToArray();

            return new NativeCombatImportResult("PARTIAL", snapshot, [],
                unverified.Length > 0
                    ? ["Visible state structurally imported; native field coverage remains unaudited"]
                    : ["Visible state structurally imported; deterministic continuation remains unverified"],
                RequiredVisibleFields.Where(verified.Contains).ToArray(),
                continuationMissing, false);
        }
        catch (Exception e) when (e is JsonException or FormatException
                                  or OverflowException or InvalidOperationException
                                  or KeyNotFoundException)
        {
            return new NativeCombatImportResult("UNSUPPORTED", null, [],
                ["Malformed canonical native snapshot: " + e.GetType().Name],
                RequiredVisibleFields.Where(verified.Contains).ToArray(),
                RequiredContinuationFields, false);
        }
    }

    private static int ReadInt(JsonElement obj, string name)
    {
        JsonElement prop = obj.GetProperty(name);
        if (prop.ValueKind != JsonValueKind.Number || !prop.TryGetInt32(out int value))
            throw new FormatException("Expected int: " + name);
        return value;
    }

    private static bool PathPresent(JsonElement state, string path)
    {
        JsonElement cursor = state;
        foreach (var segment in path[2..].Split('.'))
        {
            if (cursor.ValueKind != JsonValueKind.Object || !cursor.TryGetProperty(segment, out cursor))
                return false;
        }
        return true;
    }

    private static NativeCardView[] ReadCards(
        JsonElement zone, string name, List<string> diagnostics)
    {
        if (zone.ValueKind != JsonValueKind.Array) throw new FormatException(name);
        var cards = new List<NativeCardView>();
        foreach (var card in zone.EnumerateArray())
        {
            long id = card.GetProperty("instance_id").GetInt64();
            string model = card.GetProperty("model_id").GetString() ?? "";
            int upgrades = ReadInt(card, "upgrades");
            int cost = ReadInt(card, "cost");
            string target = card.GetProperty("target_type").GetString() ?? "";
            if (id < 0 || string.IsNullOrWhiteSpace(model) || upgrades < 0 ||
                string.IsNullOrWhiteSpace(target))
                diagnostics.Add("INVALID_CARD_IN_" + name.ToUpperInvariant());
            cards.Add(new NativeCardView(id, model, upgrades, cost, target));
        }
        return cards.ToArray();
    }

    private static NativeCreatureView[] ReadCreatures(
        JsonElement array, List<string> diagnostics)
    {
        if (array.ValueKind != JsonValueKind.Array) throw new FormatException("creatures");
        var creatures = new List<NativeCreatureView>();
        foreach (var creature in array.EnumerateArray())
        {
            string id = creature.GetProperty("id").GetString() ?? "";
            string model = creature.GetProperty("model_id").GetString() ?? "";
            int hp = ReadInt(creature, "hp");
            int maxHp = ReadInt(creature, "max_hp");
            int block = ReadInt(creature, "block");
            bool alive = creature.GetProperty("alive").GetBoolean();
            JsonElement powers = creature.GetProperty("powers").Clone();
            if (string.IsNullOrWhiteSpace(id) || id.Contains("UNASSIGNED", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(model) || hp < 0 || hp > maxHp || maxHp <= 0 || block < 0)
                diagnostics.Add("INVALID_CREATURE_ID_OR_SCALARS");
            creatures.Add(new NativeCreatureView(id, model, hp, maxHp, block, alive, powers));
        }
        return creatures.ToArray();
    }
}
