using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Bootstrap canonical JSON representation for trace/state fingerprints.
/// Object keys are sorted ordinally; array order is preserved.
/// This is a protocol utility, not a claim that JSON is the eventual hot-path state layout.
/// </summary>
public static class CanonicalJson
{
    // Simulation states contain mutable nested arrays. Reuse fingerprints
    // only during synchronous read-only validation, never across mutations,
    // decisions, requests, or replay boundaries. Keys use reference identity
    // and the exact static generic type used for serialization.
    [ThreadStatic]
    private static RunStateHashScope? _readOnlyRunHashes;

    // Diagnostic-only recomputation checks in CI, never in production
    // throughput benchmarks. Environment is fixed at process startup.
    private static readonly bool VerifyReadOnlyReuse =
        Environment.GetEnvironmentVariable(
            "STS2_H7_VERIFY_RUN_HASH_REUSE") == "1";

    private sealed class RunStateHashScope
    {
        public readonly Dictionary<RunState, string> RunHashes =
            new(ReferenceEqualityComparer.Instance);
        public Dictionary<SemanticPilotCombatV111, string>? PilotHashes;
        public Dictionary<SemanticEndTurnV111, string>? EndTurnHashes;
        public Dictionary<SemanticCombatDrawV111, string>? CombatDrawHashes;
    }

    private sealed class ReadOnlyHashLease : IDisposable
    {
        private readonly RunStateHashScope? _previous;
        private bool _disposed;

        public ReadOnlyHashLease(RunStateHashScope? previous) =>
            _previous = previous;

        public void Dispose()
        {
            if (_disposed) return;
            _readOnlyRunHashes = _previous;
            _disposed = true;
        }
    }

    /// <summary>
    /// Memoize identical typed simulation-state references only inside a
    /// synchronous read-only validation pass. Nested validators share the
    /// current scope; disposal clears it even when validation throws.
    /// No state transition or JSONL request may span a scope.
    /// </summary>
    public static IDisposable BeginReadOnlyRunStateHashReuse()
    {
        var previous = _readOnlyRunHashes;
        _readOnlyRunHashes = previous ?? new RunStateHashScope();
        return new ReadOnlyHashLease(previous);
    }

    public static string Serialize<T>(T value)
    {
        using var stream = new MemoryStream();
        WriteCanonicalUtf8(value, stream);
        return Encoding.UTF8.GetString(
            stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }

    private static void WriteCanonicalUtf8<T>(T value, MemoryStream stream)
    {
        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        using var writer = new Utf8JsonWriter(
            stream, new JsonWriterOptions { Indented = false });
        WriteCanonical(writer, element);
    }

    public static string Sha256<T>(T value)
    {
        // JsonSerializer's output can depend on T. Each permitted reference
        // type has its own cache, gated by the exact static generic type.
        // No cached digest survives the outermost read-only validation scope.
        if (_readOnlyRunHashes is { } scope)
        {
            if (typeof(T) == typeof(RunState) && value is RunState run)
                return Sha256ReadOnly(value, run, scope.RunHashes);
            if (typeof(T) == typeof(SemanticPilotCombatV111) &&
                value is SemanticPilotCombatV111 pilot)
                return Sha256ReadOnly(value, pilot, scope.PilotHashes ??=
                    new(ReferenceEqualityComparer.Instance));
            if (typeof(T) == typeof(SemanticEndTurnV111) &&
                value is SemanticEndTurnV111 endTurn)
                return Sha256ReadOnly(value, endTurn, scope.EndTurnHashes ??=
                    new(ReferenceEqualityComparer.Instance));
            if (typeof(T) == typeof(SemanticCombatDrawV111) &&
                value is SemanticCombatDrawV111 combatDraw)
                return Sha256ReadOnly(value, combatDraw, scope.CombatDrawHashes ??=
                    new(ReferenceEqualityComparer.Instance));
        }
        return Sha256Measured(value);
    }

    private static string Sha256ReadOnly<T, TState>(
        T value, TState reference, Dictionary<TState, string> hashes)
        where TState : class
    {
        if (hashes.TryGetValue(reference, out var cached))
        {
            if (VerifyReadOnlyReuse &&
                !StringComparer.Ordinal.Equals(cached, Sha256Core(value)))
                throw new InvalidDataException(
                    "State changed during read-only fingerprint validation.");
            if (!SemanticDrawStageProfilerV111.Active)
                return cached;
            return SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_hash_" + typeof(T).Name + "_cache_hit",
                () => cached);
        }
        var hash = Sha256Measured(value);
        hashes.Add(reference, hash);
        return hash;
    }

    private static string Sha256Measured<T>(T value)
    {
        if (!SemanticDrawStageProfilerV111.Active)
            return Sha256Core(value);
        return SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_hash_" + typeof(T).Name,
            () => Sha256Core(value));
    }

    private static string Sha256Core<T>(T value)
    {
        // Hash canonical bytes in place. The old implementation copied them
        // into a string, encoded that string back into a second byte array,
        // then hashed it. Keep the exact same canonical writer and SHA-256.
        using var stream = new MemoryStream();
        WriteCanonicalUtf8(value, stream);
        var bytes = stream.GetBuffer().AsSpan(
            0, checked((int)stream.Length));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray())
                {
                    WriteCanonical(writer, child);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                element.WriteTo(writer);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(element), element.ValueKind, "Unsupported JSON kind");
        }
    }
}
