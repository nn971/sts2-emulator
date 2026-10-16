using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>One field-level observation compared against a provisional basic-card rule.</summary>
public sealed record NativeRuleFieldComparison(
    string Field, object? Predicted, object? Observed, bool Agrees);

/// <summary>
/// Comparison against a restricted independent card-rule projection.
/// "parity" is ALWAYS NOT_TESTED; neither a rule-level consistency check nor
/// a native before/after observation is a verified emulator continuation.
/// </summary>
public sealed record NativeBasicRuleComparison(
    string Status, string EmulatorParity, string Reason,
    NativeBasicCardProjection? Projection,
    NativeRuleFieldComparison[] Fields);

public static class NativeBasicRuleComparer
{
    public static NativeBasicRuleComparison Check(JsonElement fixture)
    {
        if (!TryProperty(fixture, "status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() != "RECORDED_NATIVE_EXPECTATION")
            return Unsupported("Native recorded-expectation fixture required");
        if (!TryProperty(fixture, "pre_state", out var pre) ||
            !TryProperty(fixture, "post_state", out var post) ||
            !TryProperty(fixture, "selected_action", out var selected) ||
            pre.ValueKind != JsonValueKind.Object ||
            post.ValueKind != JsonValueKind.Object ||
            selected.ValueKind != JsonValueKind.Object)
            return Unsupported("Native pre/post/action unavailable");
        if (!TryProperty(selected, "kind", out var kind) ||
            kind.ValueKind != JsonValueKind.String ||
            !TryProperty(selected, "payload", out var payload))
            return Unsupported("Invalid selected action");

        var imported = NativeCombatSnapshotImporter.Import(pre);
        if (imported.Snapshot is null)
            return Unsupported("Native pre-state cannot be structurally imported: " +
                               string.Join(", ", imported.Diagnostics.Concat(imported.MissingFields)));
        var action = new GameAction(kind.GetString()!, payload.Clone());
        NativeBasicCardProjection projected = NativeBasicCardStepper.Project(imported.Snapshot, action);
        if (projected.Status != "RULE_PROJECTION_ONLY")
            return new NativeBasicRuleComparison("UNSUPPORTED", "NOT_TESTED",
                projected.Reason, projected, []);

        var fields = new List<NativeRuleFieldComparison>();
        void Compare(string path, object predicted, object? observed)
        {
            fields.Add(new NativeRuleFieldComparison(
                path, predicted, observed, observed is not null && predicted.Equals(observed)));
        }

        if (!TryProperty(post, "combat", out var combat) ||
            !TryProperty(post, "player", out var player))
            return Unsupported("Native post-state combat/player missing");
        Compare("$.combat.energy", projected.Energy!.Value, GetInt(combat, "energy"));
        if (projected.PlayerBlock is int block)
            Compare("$.player.block", block, GetInt(player, "block"));

        if (projected.TargetId is string id)
        {
            if (!TryProperty(combat, "creatures", out var enemies) ||
                enemies.ValueKind != JsonValueKind.Array)
                return Unsupported("Native post-state creatures missing");
            JsonElement? target = null;
            foreach (var creature in enemies.EnumerateArray())
                if (TryProperty(creature, "id", out var creatureId) &&
                    creatureId.ValueKind == JsonValueKind.String &&
                    creatureId.GetString() == id)
                {
                    target = creature;
                    break;
                }
            Compare("$.combat.creatures[target].hp", projected.TargetHp!.Value,
                target is null ? null : GetInt(target.Value, "hp"));
            Compare("$.combat.creatures[target].block", projected.TargetBlock!.Value,
                target is null ? null : GetInt(target.Value, "block"));
        }

        Compare("$.combat.hand.length", projected.HandCount!.Value, GetCount(combat, "hand"));
        Compare("$.combat.discard_pile.length", projected.DiscardCount!.Value,
            GetCount(combat, "discard_pile"));
        // Card instance must leave hand and be present exactly once in discard.
        long cardId = projected.CardInstanceId!.Value;
        Compare("$.combat.hand[card_instance].absent", true,
            ContainsCard(combat, "hand", cardId) is bool inHand ? !inHand : null);
        Compare("$.combat.discard_pile[card_instance].present", true,
            ContainsCard(combat, "discard_pile", cardId));

        bool missing = fields.Any(x => x.Observed is null);
        bool differs = fields.Any(x => !x.Agrees);
        return new NativeBasicRuleComparison(
            missing ? "INSUFFICIENT_NATIVE_POST_STATE" :
                differs ? "RULE_PROJECTION_MISMATCH" : "RULE_PROJECTION_CONSISTENT",
            "NOT_TESTED",
            "Compared independently calculated basic-card rules to native observed " +
            "checkpoint fields only. Hidden continuation/relic/enchantment effects " +
            "and real deterministic emulator stepping remain unverified.",
            projected, fields.ToArray());
    }

    private static NativeBasicRuleComparison Unsupported(string reason) =>
        new("UNSUPPORTED", "NOT_TESTED", reason, null, []);
    private static bool TryProperty(JsonElement value, string name, out JsonElement result)
    {
        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty(name, out result)) return true;
        result = default;
        return false;
    }
    private static int? GetInt(JsonElement value, string name) =>
        TryProperty(value, name, out var prop) &&
        prop.ValueKind == JsonValueKind.Number &&
        prop.TryGetInt32(out int result) ? result : null;
    private static int? GetCount(JsonElement value, string name) =>
        TryProperty(value, name, out var prop) &&
        prop.ValueKind == JsonValueKind.Array ? prop.GetArrayLength() : null;
    private static bool? ContainsCard(JsonElement value, string name, long cardId)
    {
        if (!TryProperty(value, name, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return null;
        return prop.EnumerateArray().Any(c =>
            TryProperty(c, "instance_id", out var id) &&
            id.ValueKind == JsonValueKind.Number &&
            id.TryGetInt64(out long v) && v == cardId);
    }
}
