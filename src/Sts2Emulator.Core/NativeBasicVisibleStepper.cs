using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sts2Emulator.Core;

/// <summary>
/// A limited independent visible-state transition from a native PRE-state.
/// Never reads the native observed POST-state. Missing private continuation
/// fields mean this is not a complete replay of Slay the Spire 2.
/// </summary>
public sealed record NativeVisibleSliceResult(
    string Status, string Reason, JsonElement? ProjectedPostState,
    string[] UnverifiedContinuation);

public static class NativeBasicVisibleStepper
{
    public static NativeVisibleSliceResult Step(NativeCombatSnapshot before, GameAction action)
    {
        var projection = NativeBasicCardStepper.Project(before, action);
        if (projection.Status != "RULE_PROJECTION_ONLY")
            return Unsupported(projection.Reason);

        JsonObject? root = JsonNode.Parse(before.SourceState.GetRawText()) as JsonObject;
        if (root is null || root["combat"] is not JsonObject combat ||
            root["player"] is not JsonObject player ||
            combat["hand"] is not JsonArray hand ||
            combat["discard_pile"] is not JsonArray discard ||
            combat["creatures"] is not JsonArray creatures)
            return Unsupported("Invalid canonical native snapshot structure");

        long selectedId = projection.CardInstanceId!.Value;
        int selectedIndex = -1;
        for (int i = 0; i < hand.Count; ++i)
            if (hand[i] is JsonObject c &&
                c["instance_id"] is JsonNode id &&
                id.GetValue<long>() == selectedId)
            {
                if (selectedIndex != -1)
                    return Unsupported("Repeated selected card instance in hand");
                selectedIndex = i;
            }
        if (selectedIndex == -1)
            return Unsupported("Selected card absent from hand");

        // Defend updates two independently observed projections of the hero's
        // block: player.block and the matching Silent creature block.
        JsonObject? hero = null;
        if (projection.PlayerBlock.HasValue)
        {
            foreach (var node in creatures)
                if (node is JsonObject creature &&
                    creature["model_id"]?.GetValue<string>() == "SILENT")
                {
                    if (hero is not null) return Unsupported("Ambiguous Silent creature");
                    hero = creature;
                }
            if (hero is null || hero["block"] is null ||
                hero["block"]!.GetValue<int>() != before.Block)
                return Unsupported("Player/creature block view disagreement");
        }

        JsonObject? target = null;
        if (projection.TargetId is string targetId)
        {
            foreach (var node in creatures)
                if (node is JsonObject creature &&
                    creature["id"]?.GetValue<string>() == targetId)
                {
                    if (target is not null) return Unsupported("Ambiguous target");
                    target = creature;
                }
            if (target is null) return Unsupported("Target not found in combat creatures");
        }

        // Mutate a deep copy, never the imported pre-state and never the native
        // post-state. Preserve all untouched visible JSON fields and card order.
        combat["energy"] = projection.Energy!.Value;
        if (target is not null)
        {
            target["hp"] = projection.TargetHp!.Value;
            target["block"] = projection.TargetBlock!.Value;
        }
        if (hero is not null)
        {
            player["block"] = projection.PlayerBlock!.Value;
            hero["block"] = projection.PlayerBlock.Value;
        }
        var playedCard = hand[selectedIndex]!.DeepClone();
        hand.RemoveAt(selectedIndex);
        discard.Add(playedCard);

        return new NativeVisibleSliceResult(
            "PROVISIONAL_VISIBLE_STEP",
            "Independent basic-card mutation of only recorded fields; " +
            "not a complete emulator combat continuation",
            JsonSerializer.SerializeToElement(root),
            projection.UnverifiedContinuation);
    }

    private static NativeVisibleSliceResult Unsupported(string reason) =>
        new("UNSUPPORTED", reason, null, []);
}

/// <summary>
/// Full recorded-JSON equality against independent restricted state mutation.
/// Unobserved relic, RNG, and pending-effect state remains unaudited.
/// The comparison is not a full emulator-fidelity verdict.
/// </summary>
public sealed record NativeVisibleSliceComparison(
    string Status, string EmulatorParity, string Reason,
    string? FirstDifferencePath, JsonElement? ProjectedPostState,
    string[] UnverifiedContinuation);

public static class NativeVisibleSliceComparer
{
    public static NativeVisibleSliceComparison Check(JsonElement fixture)
    {
        if (!TryProperty(fixture, "status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() != "RECORDED_NATIVE_EXPECTATION" ||
            !TryProperty(fixture, "pre_state", out var before) ||
            !TryProperty(fixture, "post_state", out var post) ||
            !TryProperty(fixture, "selected_action", out var selected) ||
            !TryProperty(selected, "kind", out var kind) ||
            kind.ValueKind != JsonValueKind.String ||
            !TryProperty(selected, "payload", out var payload) ||
            before.ValueKind != JsonValueKind.Object ||
            post.ValueKind != JsonValueKind.Object)
            return Unsupported("Complete native expectation with pre, post and selected action required");

        var imported = NativeCombatSnapshotImporter.Import(before);
        if (imported.Snapshot is null)
            return Unsupported("Native pre-state structurally unsupported: " +
                               string.Join(", ", imported.Diagnostics.Concat(imported.MissingFields)));

        var action = new GameAction(kind.GetString()!, payload.Clone());
        var prediction = NativeBasicVisibleStepper.Step(imported.Snapshot, action);
        if (prediction.ProjectedPostState is null)
            return Unsupported(prediction.Reason);

        var projected = JsonNode.Parse(prediction.ProjectedPostState.Value.GetRawText());
        var native = JsonNode.Parse(post.GetRawText());
        string? difference = FirstDifference(projected, native, "$");
        return new NativeVisibleSliceComparison(
            difference is null ? "VISIBLE_SLICE_CONSISTENT" : "VISIBLE_SLICE_MISMATCH",
            "NOT_TESTED",
            "Full recorded-JSON comparison of restricted independent visible-state step. " +
            "Unrecorded effects/continuation remain unaudited; not full emulator parity.",
            difference, prediction.ProjectedPostState, prediction.UnverifiedContinuation);
    }

    private static NativeVisibleSliceComparison Unsupported(string reason) =>
        new("UNSUPPORTED", "NOT_TESTED", reason, null, null, []);

    private static bool TryProperty(JsonElement source, string key, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(key, out value))
            return true;
        value = default;
        return false;
    }

    private static string? FirstDifference(JsonNode? expected, JsonNode? native, string path)
    {
        if (JsonNode.DeepEquals(expected, native)) return null;
        if (expected is JsonObject e && native is JsonObject n)
        {
            foreach (string key in e.Select(p => p.Key)
                         .Union(n.Select(p => p.Key), StringComparer.Ordinal)
                         .OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!e.ContainsKey(key) || !n.ContainsKey(key))
                    return path + "." + key;
                string? difference = FirstDifference(e[key], n[key], path + "." + key);
                if (difference is not null) return difference;
            }
        }
        else if (expected is JsonArray a && native is JsonArray b)
        {
            if (a.Count != b.Count) return path + ".length";
            for (int i = 0; i < a.Count; i++)
            {
                string? difference = FirstDifference(a[i], b[i], path + "[" + i + "]");
                if (difference is not null) return difference;
            }
        }
        return path;
    }
}
