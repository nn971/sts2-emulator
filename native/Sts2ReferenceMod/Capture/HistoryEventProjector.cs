using System.Reflection;
using System.Text.Json;

namespace Sts2ReferenceMod.Capture;

public sealed record ProjectedHistoryEffect(
    string Kind,
    JsonElement Payload,
    string? ActorId,
    string? TargetId,
    string[] MissingFields);

/// <summary>
/// Restrictive scalar-only adapter for named combat-history entry types.
/// The native module supplies canonical creature IDs, while this projector
/// never serializes native object graphs. A missing property is explicit.
/// Native property-getter purity still requires a pinned-build audit.
/// </summary>
public static class HistoryEventProjector
{
    private static readonly BindingFlags Properties = BindingFlags.Public | BindingFlags.Instance;
    private static readonly IReadOnlyDictionary<string, (string Kind, string[] Fields)> Specifications =
        new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
        {
            ["DamageReceivedEntry"] = ("damage", ["Result.UnblockedDamage"]),
            ["BlockGainedEntry"] = ("block_gained", ["Amount"]),
            ["EnergySpentEntry"] = ("energy_spent", ["Amount"]),
            ["PowerReceivedEntry"] = ("power_received", ["Amount"]),
            ["CardPlayStartedEntry"] = ("card_play_started", ["CardPlay.Card.CombatCardIndex"]),
            ["CardPlayFinishedEntry"] = ("card_play_finished", ["CardPlay.Card.CombatCardIndex"]),
            ["PotionUsedEntry"] = ("potion_used", []),
            ["MonsterPerformedMoveEntry"] = ("monster_move", [])
        };

    public static ProjectedHistoryEffect Project(
        object nativeEntry, long ordinal,
        Func<object, string?> identifyCreature)
    {
        ArgumentNullException.ThrowIfNull(nativeEntry);
        ArgumentNullException.ThrowIfNull(identifyCreature);
        string type = nativeEntry.GetType().Name;
        var spec = Specifications.TryGetValue(type, out var declared)
            ? declared : ("native_history_entry", Array.Empty<string>());

        var missing = new List<string>();
        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["entry_type"] = type,
            ["history_index"] = ordinal
        };
        foreach (string field in spec.Item2)
        {
            if (!TryReadScalar(nativeEntry, field, out object? value))
                missing.Add(field);
            else
                data[ToSnakeCase(field.Replace('.', '_'))] = value;
        }

        string? target = TryReadObject(nativeEntry, "Receiver", out var receiver)
            && receiver is not null ? identifyCreature(receiver) : null;
        string? actor = TryReadObject(nativeEntry, "Dealer", out var dealer)
            && dealer is not null ? identifyCreature(dealer) : null;
        if (actor is null && TryReadObject(nativeEntry, "Actor", out var actorObj)
            && actorObj is not null)
            actor = identifyCreature(actorObj);
        return new ProjectedHistoryEffect(spec.Item1,
            JsonSerializer.SerializeToElement(data), actor, target, missing.ToArray());
    }

    private static string ToSnakeCase(string text)
    {
        var chars = new System.Text.StringBuilder(text.Length + 8);
        foreach (char c in text)
        {
            if (char.IsUpper(c))
            {
                if (chars.Length > 0 && chars[^1] != '_') chars.Append('_');
                chars.Append(char.ToLowerInvariant(c));
            }
            else chars.Append(c);
        }
        return chars.ToString();
    }

    private static bool TryReadScalar(object root, string path, out object? value)
    {
        value = null;
        if (!TryReadObject(root, path, out object? read) || read is null)
            return false;
        switch (read)
        {
            case string or bool or int or long or short or byte or uint or ulong
                or float or double or decimal:
                value = read;
                return true;
            default:
                return false;
        }
    }

    private static bool TryReadObject(object root, string path, out object? value)
    {
        value = root;
        foreach (string part in path.Split('.'))
        {
            if (value is null) return false;
            var prop = value.GetType().GetProperty(part, Properties);
            if (prop is null || prop.GetIndexParameters().Length > 0) return false;
            try { value = prop.GetValue(value); }
            catch (TargetInvocationException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
        return true;
    }
}
