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
    // RunState contains mutable nested arrays. Its fingerprint may be cached
    // only during a provably read-only operation; never beyond validation or
    // across a transition. Identity comparison deliberately ignores the
    // record's structural equality.
    [ThreadStatic]
    private static RunStateHashScope? _readOnlyRunHashes;

    // Diagnostic-only recomputation checks in CI, never in production
    // throughput benchmarks. Environment is fixed at process startup.
    private static readonly bool VerifyReadOnlyReuse =
        Environment.GetEnvironmentVariable(
            "STS2_H7_VERIFY_RUN_HASH_REUSE") == "1";

    private sealed class RunStateHashScope
    {
        public readonly Dictionary<RunState, string> Hashes =
            new(ReferenceEqualityComparer.Instance);
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
    /// Memoize identical RunState references only inside a synchronous,
    /// read-only validation pass. Nested validators share the current scope;
    /// disposal clears it even when validation throws. State transitions and
    /// JSONL request handling must remain outside the scope.
    /// </summary>
    public static IDisposable BeginReadOnlyRunStateHashReuse()
    {
        var previous = _readOnlyRunHashes;
        _readOnlyRunHashes = previous ?? new RunStateHashScope();
        return new ReadOnlyHashLease(previous);
    }

    public static string Serialize<T>(T value)
    {
        return Encoding.UTF8.GetString(SerializeCanonicalUtf8(value));
    }

    public static string Sha256<T>(T value)
    {
        // Generic serialization can depend on static type. Restrict reuse to
        // exact RunState-typed calls, with a reference-identity key, inside
        // a read-only validation scope. The cache is never shared across a
        // new decision boundary or another thread.
        if (typeof(T) == typeof(RunState) &&
            value is RunState run &&
            _readOnlyRunHashes is { } scope)
        {
            if (scope.Hashes.TryGetValue(run, out var cached))
            {
                if (VerifyReadOnlyReuse &&
                    !StringComparer.Ordinal.Equals(
                        cached, Sha256Core(value)))
                    throw new InvalidDataException(
                        "RunState was modified during read-only validation.");
                if (!SemanticDrawStageProfilerV111.Active)
                    return cached;
                return SemanticDrawStageProfilerV111.Measure(
                    "sub.semantic_draw_hash_RunState_cache_hit",
                    () => cached);
            }
            var hash = Sha256Measured(value);
            scope.Hashes.Add(run, hash);
            return hash;
        }
        return Sha256Measured(value);
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
        // Hash canonical UTF-8 directly: converting it to a UTF-16 string
        // and back allocates two redundant full-state copies on every hash.
        var utf8 = SerializeCanonicalUtf8(value);
        return Convert.ToHexString(SHA256.HashData(utf8)).ToLowerInvariant();
    }

    private static byte[] SerializeCanonicalUtf8<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, element);
        }
        return stream.ToArray();
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
