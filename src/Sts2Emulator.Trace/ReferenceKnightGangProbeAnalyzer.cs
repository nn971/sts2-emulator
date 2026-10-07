using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Trace;

public sealed record ReferenceKnightGangEnemySnapshot(
    long? CombatId,
    string? Role,
    string? NativeIdentity,
    int? CurrentHp,
    int? MaxHp,
    int? Block,
    string[] Powers,
    string? CurrentMove,
    string? NextMove,
    string? Intent,
    long? MoveIndex,
    string? LastMove);

public sealed record ReferenceKnightGangCardSnapshot(
    int HandCount,
    int DrawCount,
    int DiscardCount,
    int ExhaustCount,
    int PlayCount,
    int UpgradedCardCount,
    int HexedCardCount,
    int AfflictedCardCount);

public sealed record ReferenceKnightGangCheckpoint(
    long Sequence,
    string Boundary,
    long? RoundNumber,
    string? CurrentSide,
    int? Ascension,
    int? PlayerHp,
    int? PlayerMaxHp,
    int? PlayerBlock,
    ReferenceKnightGangEnemySnapshot[] Enemies,
    ReferenceKnightGangCardSnapshot Cards,
    string? MonsterAiRngFingerprint,
    string[] MonsterAiRngEvidence);

public sealed record ReferenceKnightGangCombatAudit(
    int CombatIndex,
    long FirstSequence,
    long LastSequence,
    int? Ascension,
    string[] NativeEnemyIdentities,
    int CheckpointCount,
    int CheckpointsWithMoveEvidence,
    int CheckpointsWithIntentEvidence,
    int CheckpointsWithMonsterAiRng,
    string[] StaticMismatches,
    ReferenceKnightGangCheckpoint[] Checkpoints);

public sealed record ReferenceKnightGangProbeAudit(
    string? Schema,
    string? BuildFingerprint,
    int MatchingCombatCount,
    int MatchingCheckpointCount,
    ReferenceKnightGangCombatAudit[] Combats,
    string[] Diagnostics);

/// <summary>
/// Extracts only evidence actually present in a passive native probe. The analyzer never
/// substitutes emulator state for a missing native field.
/// </summary>
public static class ReferenceKnightGangProbeAnalyzer
{
    public static ReferenceKnightGangProbeAudit Analyze(string path)
    {
        string? schema = null;
        string? fingerprint = null;
        var diagnostics = new HashSet<string>(StringComparer.Ordinal);
        var combats = new List<List<ReferenceKnightGangCheckpoint>>();
        List<ReferenceKnightGangCheckpoint>? current = null;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                diagnostics.Add("invalid_json_record_ignored");
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                schema ??= GetString(root, "schema");
                fingerprint ??= GetString(root, "build_fingerprint");

                if (!StringComparer.Ordinal.Equals(
                        GetString(root, "type"),
                        "boundary")
                    || !root.TryGetProperty("sequence", out var sequenceElement)
                    || !sequenceElement.TryGetInt64(out var sequence)
                    || !root.TryGetProperty("state", out var state)
                    || state.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!LooksLikeKnightGang(state))
                {
                    continue;
                }

                var boundary = GetString(root, "boundary") ?? "<missing>";
                if (current is null
                    || boundary.EndsWith(".CombatSetUp", StringComparison.Ordinal))
                {
                    current = [];
                    combats.Add(current);
                }

                current.Add(BuildCheckpoint(sequence, boundary, state));

                if (boundary.EndsWith(".CombatEnded", StringComparison.Ordinal)
                    || boundary.EndsWith(".CombatWon", StringComparison.Ordinal))
                {
                    current = null;
                }
            }
        }

        if (combats.Count == 0)
        {
            diagnostics.Add("knight_gang_not_observed");
        }

        var audits = combats
            .Where(combat => combat.Count > 0)
            .Select((combat, index) => BuildCombatAudit(index + 1, combat))
            .ToArray();

        if (audits.Any(combat => combat.CheckpointsWithMoveEvidence == 0))
        {
            diagnostics.Add("enemy_move_evidence_missing");
        }

        if (audits.Any(combat => combat.CheckpointsWithIntentEvidence == 0))
        {
            diagnostics.Add("enemy_intent_evidence_missing");
        }

        if (audits.Any(combat => combat.CheckpointsWithMonsterAiRng == 0))
        {
            diagnostics.Add("monster_ai_rng_evidence_missing");
        }

        return new ReferenceKnightGangProbeAudit(
            schema,
            fingerprint,
            audits.Length,
            audits.Sum(combat => combat.CheckpointCount),
            audits,
            diagnostics.Order(StringComparer.Ordinal).ToArray());
    }

    private static ReferenceKnightGangCombatAudit BuildCombatAudit(
        int combatIndex,
        List<ReferenceKnightGangCheckpoint> checkpoints)
    {
        var first = checkpoints[0];
        var ascension = checkpoints
            .Select(checkpoint => checkpoint.Ascension)
            .FirstOrDefault(value => value is not null);
        var identities = checkpoints
            .SelectMany(checkpoint => checkpoint.Enemies)
            .Select(enemy => enemy.NativeIdentity)
            .Where(identity => !string.IsNullOrWhiteSpace(identity))
            .Select(identity => identity!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var staticMismatches = CompareStaticEnemyValues(first, ascension);

        return new ReferenceKnightGangCombatAudit(
            combatIndex,
            first.Sequence,
            checkpoints[^1].Sequence,
            ascension,
            identities,
            checkpoints.Count,
            checkpoints.Count(checkpoint =>
                checkpoint.Enemies.Any(enemy =>
                    enemy.CurrentMove is not null
                    || enemy.NextMove is not null
                    || enemy.LastMove is not null
                    || enemy.MoveIndex is not null)),
            checkpoints.Count(checkpoint =>
                checkpoint.Enemies.Any(enemy => enemy.Intent is not null)),
            checkpoints.Count(checkpoint =>
                checkpoint.MonsterAiRngFingerprint is not null),
            staticMismatches,
            checkpoints.ToArray());
    }

    private static string[] CompareStaticEnemyValues(
        ReferenceKnightGangCheckpoint first,
        int? ascension)
    {
        if (ascension is null)
        {
            return ["native_ascension_missing_static_hp_comparison_skipped"];
        }

        var mismatches = new List<string>();
        foreach (var enemy in first.Enemies)
        {
            if (enemy.Role is null || enemy.MaxHp is null)
            {
                continue;
            }

            var prototypeId = enemy.Role switch
            {
                "flail" => "proto.enemy.flail_knight",
                "spectral" => "proto.enemy.spectral_knight",
                "magi" => "proto.enemy.magi_knight",
                _ => null
            };
            if (prototypeId is null)
            {
                continue;
            }

            var expected = PrototypeContent.Enemy(prototypeId)
                .HpAt(3, ascension.Value);
            if (enemy.MaxHp.Value != expected)
            {
                mismatches.Add(
                    $"{enemy.Role}_max_hp native={enemy.MaxHp.Value} emulator={expected}");
            }
        }

        return mismatches.ToArray();
    }

    private static ReferenceKnightGangCheckpoint BuildCheckpoint(
        long sequence,
        string boundary,
        JsonElement state)
    {
        var combat = GetObject(state, "combat");
        var run = GetObject(state, "run");
        var player = FirstArrayItem(state, "players");
        var creature = GetObject(player, "creature");
        var enemies = ArrayItems(state, "enemies")
            .Select(BuildEnemy)
            .ToArray();

        var rngEvidence = new List<string>();
        if (state.TryGetProperty("run_rng", out var runRng))
        {
            CollectMonsterAiRng(
                runRng,
                "$.run_rng",
                rngEvidence,
                depth: 10);
        }

        rngEvidence.Sort(StringComparer.Ordinal);
        var rngFingerprint = rngEvidence.Count == 0
            ? null
            : Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        string.Join("\n", rngEvidence))))
                .ToLowerInvariant();

        return new ReferenceKnightGangCheckpoint(
            sequence,
            boundary,
            GetInt64(combat, "round_number"),
            GetScalarText(combat, "current_side"),
            GetInt32(run, "ascension_level"),
            GetInt32(creature, "current_hp")
                ?? GetInt32(player, "current_hp"),
            GetInt32(creature, "max_hp")
                ?? GetInt32(player, "max_hp"),
            GetInt32(creature, "block"),
            enemies,
            BuildCardSnapshot(player),
            rngFingerprint,
            rngEvidence.ToArray());
    }

    private static ReferenceKnightGangEnemySnapshot BuildEnemy(
        JsonElement enemy)
    {
        var identity = IdentityFromEnemy(enemy);
        return new ReferenceKnightGangEnemySnapshot(
            GetInt64(enemy, "combat_id"),
            RoleFromIdentity(identity),
            identity,
            GetInt32(enemy, "current_hp"),
            GetInt32(enemy, "max_hp"),
            GetInt32(enemy, "block"),
            ReadPowerIdentities(enemy),
            FirstIdentity(enemy, "current_move", "move"),
            FirstIdentity(enemy, "next_move"),
            FirstIdentity(enemy, "current_intent", "intent"),
            GetInt64(enemy, "move_index"),
            FirstIdentity(enemy, "last_move"));
    }

    private static ReferenceKnightGangCardSnapshot BuildCardSnapshot(
        JsonElement player)
    {
        var all = new List<JsonElement>();
        var hand = PileItems(player, "hand");
        var draw = PileItems(player, "draw_pile");
        var discard = PileItems(player, "discard_pile");
        var exhaust = PileItems(player, "exhaust_pile");
        var play = PileItems(player, "play_pile");
        all.AddRange(hand);
        all.AddRange(draw);
        all.AddRange(discard);
        all.AddRange(exhaust);
        all.AddRange(play);

        return new ReferenceKnightGangCardSnapshot(
            hand.Length,
            draw.Length,
            discard.Length,
            exhaust.Length,
            play.Length,
            all.Count(card =>
                GetInt32(card, "upgrade_level") is > 0
                || GetBool(card, "is_upgraded") == true),
            all.Count(card => ContainsToken(card, "hexed")),
            all.Count(card =>
                HasNonNullProperty(card, "affliction")
                || ContainsToken(card, "affliction")));
    }

    private static bool LooksLikeKnightGang(JsonElement state)
    {
        var encounter = GetObject(GetObject(state, "combat"), "encounter");
        if (ContainsToken(encounter, "knight_gang")
            || ContainsToken(encounter, "knight gang"))
        {
            return true;
        }

        var roles = ArrayItems(state, "enemies")
            .Select(IdentityFromEnemy)
            .Select(RoleFromIdentity)
            .Where(role => role is not null)
            .ToHashSet(StringComparer.Ordinal);

        return roles.Contains("flail")
            && roles.Contains("spectral")
            && roles.Contains("magi");
    }

    private static string? IdentityFromEnemy(JsonElement enemy)
    {
        foreach (var name in new[] { "model_detail", "model" })
        {
            if (!enemy.TryGetProperty(name, out var model))
            {
                continue;
            }

            var identity = FindIdentityString(model);
            if (identity is not null)
            {
                return identity;
            }
        }

        return FindIdentityString(enemy);
    }

    private static string? RoleFromIdentity(string? identity)
    {
        if (identity is null)
        {
            return null;
        }

        var normalized = identity
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        if (normalized.Contains("flailknight", StringComparison.Ordinal))
        {
            return "flail";
        }

        if (normalized.Contains("spectralknight", StringComparison.Ordinal))
        {
            return "spectral";
        }

        if (normalized.Contains("magiknight", StringComparison.Ordinal)
            || normalized.Contains("magiknight", StringComparison.Ordinal))
        {
            return "magi";
        }

        return null;
    }

    private static string[] ReadPowerIdentities(JsonElement enemy)
    {
        if (!enemy.TryGetProperty("powers", out var powers))
        {
            return [];
        }

        var values = new HashSet<string>(StringComparer.Ordinal);
        CollectIdentityStrings(powers, values, depth: 7);
        return values
            .Where(value =>
                value.Contains("power", StringComparison.OrdinalIgnoreCase)
                || value.Contains("hex", StringComparison.OrdinalIgnoreCase)
                || value.Contains("dampen", StringComparison.OrdinalIgnoreCase)
                || value.Contains("strength", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? FirstIdentity(
        JsonElement element,
        params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (!element.TryGetProperty(name, out var property)
                || property.ValueKind is JsonValueKind.Null
                    or JsonValueKind.Undefined)
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            var identity = FindIdentityString(property);
            if (identity is not null)
            {
                return identity;
            }

            return property.GetRawText();
        }

        return null;
    }

    private static string? FindIdentityString(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var preferred in new[]
        {
            "entry", "id", "name", "title", "type", "runtime_type"
        })
        {
            if (!element.TryGetProperty(preferred, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            var nested = FindIdentityString(property);
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            var nested = FindIdentityString(property.Value);
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
            }
        }

        return null;
    }

    private static void CollectIdentityStrings(
        JsonElement element,
        HashSet<string> values,
        int depth)
    {
        if (depth <= 0)
        {
            return;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectIdentityStrings(item, values, depth - 1);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                CollectIdentityStrings(property.Value, values, depth - 1);
            }
        }
    }

    private static void CollectMonsterAiRng(
        JsonElement element,
        string path,
        List<string> evidence,
        int depth)
    {
        if (depth <= 0)
        {
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var childPath = path + "." + property.Name;
                if (IsMonsterAiName(property.Name))
                {
                    evidence.Add(
                        childPath + "=" + property.Value.GetRawText());
                }

                CollectMonsterAiRng(
                    property.Value,
                    childPath,
                    evidence,
                    depth - 1);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                CollectMonsterAiRng(
                    item,
                    path + "[" + index++ + "]",
                    evidence,
                    depth - 1);
            }
        }
    }

    private static bool IsMonsterAiName(string name)
    {
        var normalized = name
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized.Contains("monsterai", StringComparison.Ordinal)
            || (normalized.Contains("monster", StringComparison.Ordinal)
                && normalized.Contains("rng", StringComparison.Ordinal));
    }

    private static bool ContainsToken(
        JsonElement element,
        string token) =>
        element.ValueKind is not
            JsonValueKind.Undefined and not JsonValueKind.Null
        && element.GetRawText().Contains(
            token,
            StringComparison.OrdinalIgnoreCase);

    private static bool HasNonNullProperty(
        JsonElement element,
        string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var property)
        && property.ValueKind is not
            JsonValueKind.Null and not JsonValueKind.Undefined;

    private static JsonElement[] PileItems(
        JsonElement player,
        string pileName)
    {
        var pile = GetObject(player, pileName);
        if (!pile.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray()
            .Select(item => item.Clone())
            .ToArray();
    }

    private static JsonElement[] ArrayItems(
        JsonElement element,
        string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(item => item.Clone())
            .ToArray();
    }

    private static JsonElement FirstArrayItem(
        JsonElement element,
        string name)
    {
        foreach (var item in ArrayItems(element, name))
        {
            return item;
        }

        return default;
    }

    private static JsonElement GetObject(
        JsonElement element,
        string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.Object
            ? property
            : default;

    private static string? GetString(
        JsonElement element,
        string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? GetScalarText(
        JsonElement element,
        string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => FindIdentityString(property)
        };
    }

    private static int? GetInt32(
        JsonElement element,
        string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var property)
        && property.TryGetInt32(out var value)
            ? value
            : null;

    private static long? GetInt64(
        JsonElement element,
        string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var property)
        && property.TryGetInt64(out var value)
            ? value
            : null;

    private static bool? GetBool(
        JsonElement element,
        string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }
}
