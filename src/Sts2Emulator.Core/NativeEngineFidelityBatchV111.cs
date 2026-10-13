using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Runs each independently recorded native-expectation fixture through the
/// EXISTING actual PrototypeGameEngine.Step-based comparer. A directory batch
/// is merely a collection of isolated checkpoints: it never reconstructs the
/// original native run between files or synchronizes RNG streams.
/// </summary>
public static class NativeEngineFidelityBatchV111
{
    public const string Schema = "native-engine-fidelity-batch-v1";

    public sealed record Entry(
        string File,
        string? CardModel,
        string? NativeEnemyModel,
        int? DecisionId,
        string Status,
        bool ExecutedActualEngineStep,
        int FieldCount,
        int MatchingFieldCount,
        string? FirstMismatchPath,
        string[] Blockers,
        string[] UnverifiedPreconditions,
        int RecordedEmulatorEvents,
        string? EngineFailure,
        int RecordedRngProviderBoundaries = 0);

    public sealed record Report(
        string Schema,
        string Status,
        string NativeGameParity,
        string ExactRngParity,
        string NativeSourceHookOrderParity,
        string Directory,
        string SandboxRegion,
        int Ascension,
        int TotalFixtures,
        int ActuallyStepped,
        int Consistent,
        int Mismatched,
        int UnsupportedOrInvalid,
        Entry[] Entries);

    public static Report CompareDirectory(
        string directory, ActIdentity region = ActIdentity.Overgrowth,
        int ascension = 0)
    {
        if (!System.IO.Directory.Exists(directory))
            throw new DirectoryNotFoundException(directory);

        string[] files = System.IO.Directory.GetFiles(directory, "*.json",
            SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        var entries = new List<Entry>();
        foreach (string file in files)
        {
            NativeEngineFidelityReportV111 result;
            string? card = null;
            string? enemy = null;
            int? decision = null;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var root = document.RootElement;
                card = ReadString(root, "selected_card", "model_id");
                if (Get(root, "decision_id") is { ValueKind: JsonValueKind.Number } id &&
                    id.TryGetInt32(out int value))
                    decision = value;
                if (Get(root, "pre_state") is { } state &&
                    Get(state, "combat") is { } combat &&
                    Get(combat, "creatures") is { ValueKind: JsonValueKind.Array } creatures)
                {
                    foreach (var creature in creatures.EnumerateArray())
                    {
                        if (ReadString(creature, "id") != "creature:0")
                        {
                            enemy = ReadString(creature, "model_id");
                            break;
                        }
                    }
                }
                result = NativeEngineFidelityV111.Compare(root, region, ascension);
            }
            catch (Exception ex) when (ex is JsonException or IOException
                                      or UnauthorizedAccessException)
            {
                // Malformed individual fixture never hides the other
                // independently recorded comparisons in this directory.
                result = NativeEngineFidelityReportV111.Stop(
                    "INVALID_FIXTURE", null, ["Cannot read or parse fixture."],
                    error: ex.GetType().Name);
            }

            entries.Add(new Entry(
                Path.GetFileName(file), card, enemy, decision,
                result.Status, result.ExecutedActualEngineStep,
                result.CheckedFields.Length,
                result.CheckedFields.Count(c => c.Equal),
                result.FirstMismatchPath,
                result.Blockers,
                result.UnverifiedPreconditions,
                result.EmulatorEventJournal?.Length ?? 0,
                result.EngineFailure,
                result.EmulatorRandomBoundaries?.Length ?? 0));
        }

        int matched = entries.Count(e => e.Status == "CORE_ENGINE_CHECKPOINT_CONSISTENT");
        int mismatched = entries.Count(e => e.Status == "CORE_ENGINE_CHECKPOINT_MISMATCH");
        int actuallyStepped = entries.Count(e => e.ExecutedActualEngineStep);
        int unsupported = entries.Count - matched - mismatched;
        string status = entries.Count == 0 ? "NO_FIXTURES" :
            mismatched > 0 ? "CHECKPOINT_MISMATCHES_DETECTED" :
            unsupported > 0 ? "PARTIAL_COMPARISON_UNSUPPORTED" :
            "ALL_SUPPORTED_CHECKPOINTS_CONSISTENT";
        return new Report(
            Schema, status, "NOT_TESTED", "NOT_TESTED", "NOT_TESTED",
            Path.GetFullPath(directory), region.ToString(), ascension,
            entries.Count, actuallyStepped, matched, mismatched, unsupported,
            entries.ToArray());
    }

    private static JsonElement? Get(JsonElement obj, string field) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(field, out var prop) ? prop : null;

    private static string? ReadString(JsonElement obj, params string[] path)
    {
        JsonElement current = obj;
        foreach (string field in path)
        {
            if (Get(current, field) is not { } next) return null;
            current = next;
        }
        return current.ValueKind == JsonValueKind.String
            ? current.GetString() : null;
    }
}
