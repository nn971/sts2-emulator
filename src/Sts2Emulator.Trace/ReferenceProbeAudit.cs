using System.Text.Json;

namespace Sts2Emulator.Trace;

public sealed record ReferenceProbeHistoryShape(
    string RuntimeType,
    int Count,
    int StateChangedCount,
    int StatePreservedCount,
    int DistinctStateHashes,
    string[] PayloadPaths);

public sealed record ReferenceProbeRngShape(
    string Scope,
    string RuntimeType,
    int Count,
    string[] PayloadPaths);

public sealed record ReferenceProbeTypeCatalog(
    string RuntimeType,
    string[] Properties,
    string[] Fields);

public sealed record ReferenceProbeAudit(
    int BoundaryCount,
    int HistoryBoundaryCount,
    int HistoryPayloadCount,
    int PlayerSnapshotCount,
    int PlayerCreatureSnapshotCount,
    int RunRngSnapshotCount,
    int PlayerRngSnapshotCount,
    IReadOnlyList<ReferenceProbeHistoryShape> HistoryEntryShapes,
    IReadOnlyList<ReferenceProbeRngShape> RngShapes,
    IReadOnlyList<ReferenceProbeTypeCatalog> TypeCatalogs);

public static class ReferenceProbeAuditor
{
    public static ReferenceProbeAudit Analyze(string path)
    {
        var history = new Dictionary<string, HistoryAccumulator>(StringComparer.Ordinal);
        var rng = new Dictionary<string, RngAccumulator>(StringComparer.Ordinal);
        var catalogs = new Dictionary<string, ReferenceProbeTypeCatalog>(StringComparer.Ordinal);

        var boundaryCount = 0;
        var historyBoundaryCount = 0;
        var historyPayloadCount = 0;
        var playerSnapshotCount = 0;
        var playerCreatureSnapshotCount = 0;
        var runRngSnapshotCount = 0;
        var playerRngSnapshotCount = 0;
        string? previousBoundaryStateHash = null;

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
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                var recordType = GetString(root, "type");

                if (StringComparer.Ordinal.Equals(recordType, "type_catalog"))
                {
                    CaptureTypeCatalog(root, catalogs);
                    continue;
                }

                if (!StringComparer.Ordinal.Equals(recordType, "boundary"))
                {
                    continue;
                }

                boundaryCount++;
                var stateHash = GetString(root, "state_hash");
                var changedSincePreviousBoundary =
                    previousBoundaryStateHash is not null
                    && stateHash is not null
                    && !StringComparer.Ordinal.Equals(
                        previousBoundaryStateHash,
                        stateHash);

                if (root.TryGetProperty("state", out var state)
                    && state.ValueKind == JsonValueKind.Object)
                {
                    if (state.TryGetProperty("run_rng", out var runRng)
                        && runRng.ValueKind is not JsonValueKind.Null
                        and not JsonValueKind.Undefined)
                    {
                        runRngSnapshotCount++;
                        CaptureRngShape("run", runRng, rng);
                    }

                    if (state.TryGetProperty("players", out var players)
                        && players.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var player in players.EnumerateArray())
                        {
                            if (player.ValueKind != JsonValueKind.Object)
                            {
                                continue;
                            }

                            playerSnapshotCount++;
                            if (player.TryGetProperty("creature", out var creature)
                                && creature.ValueKind == JsonValueKind.Object)
                            {
                                playerCreatureSnapshotCount++;
                            }

                            if (player.TryGetProperty("player_rng", out var playerRng)
                                && playerRng.ValueKind is not JsonValueKind.Null
                                and not JsonValueKind.Undefined)
                            {
                                playerRngSnapshotCount++;
                                CaptureRngShape("player", playerRng, rng);
                            }
                        }
                    }
                }

                var boundary = GetString(root, "boundary");
                if (StringComparer.Ordinal.Equals(
                        boundary,
                        "combat_history.Changed"))
                {
                    historyBoundaryCount++;
                    if (root.TryGetProperty("history_entry", out var historyEntry)
                        && historyEntry.ValueKind is not JsonValueKind.Null
                        and not JsonValueKind.Undefined)
                    {
                        historyPayloadCount++;
                        var runtimeType = RuntimeTypeOf(historyEntry);
                        if (!history.TryGetValue(runtimeType, out var accumulator))
                        {
                            accumulator = new HistoryAccumulator();
                            history.Add(runtimeType, accumulator);
                        }

                        accumulator.Count++;
                        if (changedSincePreviousBoundary)
                        {
                            accumulator.StateChangedCount++;
                        }
                        else
                        {
                            accumulator.StatePreservedCount++;
                        }

                        if (stateHash is not null)
                        {
                            accumulator.StateHashes.Add(stateHash);
                        }

                        CollectPayloadPaths(
                            historyEntry,
                            "$",
                            accumulator.PayloadPaths,
                            depth: 8);
                    }
                }

                if (stateHash is not null)
                {
                    previousBoundaryStateHash = stateHash;
                }
            }
        }

        return new ReferenceProbeAudit(
            boundaryCount,
            historyBoundaryCount,
            historyPayloadCount,
            playerSnapshotCount,
            playerCreatureSnapshotCount,
            runRngSnapshotCount,
            playerRngSnapshotCount,
            history
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new ReferenceProbeHistoryShape(
                    item.Key,
                    item.Value.Count,
                    item.Value.StateChangedCount,
                    item.Value.StatePreservedCount,
                    item.Value.StateHashes.Count,
                    item.Value.PayloadPaths
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray()))
                .ToArray(),
            rng
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new ReferenceProbeRngShape(
                    item.Value.Scope,
                    item.Value.RuntimeType,
                    item.Value.Count,
                    item.Value.PayloadPaths
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray()))
                .ToArray(),
            catalogs.Values
                .OrderBy(item => item.RuntimeType, StringComparer.Ordinal)
                .ToArray());
    }

    private static void CaptureRngShape(
        string scope,
        JsonElement element,
        Dictionary<string, RngAccumulator> accumulatorByKey)
    {
        var runtimeType = RuntimeTypeOf(element);
        var key = $"{scope}\u001f{runtimeType}";
        if (!accumulatorByKey.TryGetValue(key, out var accumulator))
        {
            accumulator = new RngAccumulator(scope, runtimeType);
            accumulatorByKey.Add(key, accumulator);
        }

        accumulator.Count++;
        CollectPayloadPaths(element, "$", accumulator.PayloadPaths, depth: 8);
    }

    private static void CaptureTypeCatalog(
        JsonElement root,
        Dictionary<string, ReferenceProbeTypeCatalog> catalogs)
    {
        var runtimeType = GetString(root, "runtime_type");
        if (runtimeType is null)
        {
            return;
        }

        catalogs[runtimeType] = new ReferenceProbeTypeCatalog(
            runtimeType,
            ReadMemberNames(root, "properties"),
            ReadMemberNames(root, "fields"));
    }

    private static string[] ReadMemberNames(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var members)
            || members.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return members
            .EnumerateArray()
            .Select(member => GetString(member, "name"))
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RuntimeTypeOf(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String)
        {
            return type.GetString() ?? "<null>";
        }

        return $"<{element.ValueKind}>";
    }

    private static string? GetString(
        JsonElement element,
        string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static void CollectPayloadPaths(
        JsonElement element,
        string path,
        HashSet<string> paths,
        int depth)
    {
        if (depth <= 0)
        {
            paths.Add(path + ".<depth-limit>");
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var properties = element.EnumerateObject().ToArray();
                if (properties.Length == 0)
                {
                    paths.Add(path + ".{}");
                    return;
                }

                foreach (var property in properties)
                {
                    if (StringComparer.Ordinal.Equals(property.Name, "type"))
                    {
                        continue;
                    }

                    CollectPayloadPaths(
                        property.Value,
                        path + "." + property.Name,
                        paths,
                        depth - 1);
                }

                break;
            }

            case JsonValueKind.Array:
            {
                var items = element.EnumerateArray().Take(32).ToArray();
                if (items.Length == 0)
                {
                    paths.Add(path + "[]");
                    return;
                }

                foreach (var item in items)
                {
                    CollectPayloadPaths(
                        item,
                        path + "[]",
                        paths,
                        depth - 1);
                }

                break;
            }

            default:
                paths.Add(path);
                break;
        }
    }

    private sealed class HistoryAccumulator
    {
        public int Count { get; set; }

        public int StateChangedCount { get; set; }

        public int StatePreservedCount { get; set; }

        public HashSet<string> StateHashes { get; } =
            new(StringComparer.Ordinal);

        public HashSet<string> PayloadPaths { get; } =
            new(StringComparer.Ordinal);
    }

    private sealed class RngAccumulator(
        string scope,
        string runtimeType)
    {
        public string Scope { get; } = scope;

        public string RuntimeType { get; } = runtimeType;

        public int Count { get; set; }

        public HashSet<string> PayloadPaths { get; } =
            new(StringComparer.Ordinal);
    }
}
