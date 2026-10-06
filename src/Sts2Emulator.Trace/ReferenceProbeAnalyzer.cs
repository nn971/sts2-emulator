using System.Text.Json;

namespace Sts2Emulator.Trace;

public sealed record ReferenceProbeSummary(
    int Lines,
    int ValidRecords,
    int InvalidRecords,
    string? Schema,
    string? BuildFingerprint,
    int CombatCount,
    int TypeCatalogCount,
    IReadOnlyDictionary<string, int> RecordTypes,
    IReadOnlyDictionary<string, int> Boundaries,
    IReadOnlyDictionary<string, int> Diagnostics);

public static class ReferenceProbeAnalyzer
{
    public static ReferenceProbeSummary Analyze(string path)
    {
        var recordTypes = new Dictionary<string, int>(StringComparer.Ordinal);
        var boundaries = new Dictionary<string, int>(StringComparer.Ordinal);
        var diagnostics = new Dictionary<string, int>(StringComparer.Ordinal);

        var lines = 0;
        var valid = 0;
        var invalid = 0;
        var combatCount = 0;
        var typeCatalogCount = 0;
        string? schema = null;
        string? buildFingerprint = null;

        foreach (var line in File.ReadLines(path))
        {
            lines++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                valid++;

                if (root.TryGetProperty("schema", out var schemaElement)
                    && schemaElement.ValueKind == JsonValueKind.String)
                {
                    schema ??= schemaElement.GetString();
                }

                if (root.TryGetProperty("build_fingerprint", out var fingerprintElement)
                    && fingerprintElement.ValueKind == JsonValueKind.String)
                {
                    buildFingerprint ??= fingerprintElement.GetString();
                }

                var type = root.TryGetProperty("type", out var typeElement)
                    && typeElement.ValueKind == JsonValueKind.String
                        ? typeElement.GetString() ?? "<null>"
                        : "<missing>";
                recordTypes[type] = recordTypes.GetValueOrDefault(type) + 1;

                if (StringComparer.Ordinal.Equals(type, "type_catalog"))
                {
                    typeCatalogCount++;
                }

                if (StringComparer.Ordinal.Equals(type, "diagnostic"))
                {
                    var code = root.TryGetProperty("code", out var codeElement)
                        && codeElement.ValueKind == JsonValueKind.String
                            ? codeElement.GetString() ?? "<null>"
                            : "<missing>";
                    diagnostics[code] = diagnostics.GetValueOrDefault(code) + 1;
                }

                if (!StringComparer.Ordinal.Equals(type, "boundary"))
                {
                    continue;
                }

                var boundary = root.TryGetProperty("boundary", out var boundaryElement)
                    && boundaryElement.ValueKind == JsonValueKind.String
                        ? boundaryElement.GetString() ?? "<null>"
                        : "<missing>";
                boundaries[boundary] = boundaries.GetValueOrDefault(boundary) + 1;

                if (boundary.EndsWith(".CombatSetUp", StringComparison.Ordinal))
                {
                    combatCount++;
                }
            }
            catch (JsonException)
            {
                invalid++;
            }
        }

        return new ReferenceProbeSummary(
            lines,
            valid,
            invalid,
            schema,
            buildFingerprint,
            combatCount,
            typeCatalogCount,
            new SortedDictionary<string, int>(recordTypes, StringComparer.Ordinal),
            new SortedDictionary<string, int>(boundaries, StringComparer.Ordinal),
            new SortedDictionary<string, int>(diagnostics, StringComparer.Ordinal));
    }
}
