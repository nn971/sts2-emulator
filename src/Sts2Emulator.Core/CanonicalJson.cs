using System.Buffers;
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
    // RunState and SemanticPilotCombatV111 contain mutable nested arrays.
    // Only memoize during a synchronous read-only scope. Reference identity
    // avoids expensive structural record equality; never cache across steps.
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
        public readonly Dictionary<SemanticPilotCombatV111, string> PilotHashes =
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

    /// <summary>
    /// Read-only semantic status/projection scope. Shares the same nested
    /// reference-identity dictionary as the existing RunState validation
    /// lease, but also reuses exact SemanticPilotCombatV111 fingerprints.
    /// Dispose before advancing mechanics or changing any mutable array.
    /// </summary>
    public static IDisposable BeginReadOnlySemanticSnapshotHashReuse() =>
        BeginReadOnlyRunStateHashReuse();

    public static string Serialize<T>(T value)
    {
        using var bytes = WriteCanonicalUtf8(value);
        return Encoding.UTF8.GetString(bytes.WrittenSpan);
    }

    public static string Sha256<T>(T value)
    {
        // Generic serialization depends on static type. Only the exact
        // RunState and SemanticPilotCombatV111 generic calls participate.
        // No scopes survive a mechanical transition or another thread.
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
        if (typeof(T) == typeof(SemanticPilotCombatV111) &&
            value is SemanticPilotCombatV111 pilot &&
            _readOnlyRunHashes is { } pilotScope)
        {
            if (pilotScope.PilotHashes.TryGetValue(pilot, out var cached))
            {
                if (VerifyReadOnlyReuse &&
                    !StringComparer.Ordinal.Equals(cached, Sha256Core(value)))
                    throw new InvalidDataException(
                        "Semantic pilot changed during read-only hashing.");
                if (!SemanticDrawStageProfilerV111.Active)
                    return cached;
                return SemanticDrawStageProfilerV111.Measure(
                    "sub.semantic_draw_hash_SemanticPilotCombatV111_cache_hit",
                    () => cached);
            }
            var hash = Sha256Measured(value);
            pilotScope.PilotHashes.Add(pilot, hash);
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
        // Stream canonical UTF-8 to a reusable pooled buffer. SHA-256 reads the
        // exact written span; neither a UTF-16 roundtrip nor a final byte[]
        // copy is needed. The pool lease never escapes one invocation.
        using var bytes = WriteCanonicalUtf8(value);
        return Convert.ToHexString(SHA256.HashData(bytes.WrittenSpan))
            .ToLowerInvariant();
    }

    private static PooledCanonicalBuffer WriteCanonicalUtf8<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        var bytes = new PooledCanonicalBuffer();
        try
        {
            using (var writer = new Utf8JsonWriter(
                bytes, new JsonWriterOptions { Indented = false }))
            {
                WriteCanonical(writer, element);
                writer.Flush();
            }
            return bytes;
        }
        catch
        {
            bytes.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Pooled write buffer for the canonical UTF-8 writer. Leases are owned by
    /// a single serialization operation (including nested ones); no shared
    /// mutable scratch data or cross-boundary fingerprints are retained.
    /// </summary>
    private sealed class PooledCanonicalBuffer : IBufferWriter<byte>, IDisposable
    {
        private byte[] _buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        private int _written;
        private bool _disposed;

        public ReadOnlySpan<byte> WrittenSpan
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _buffer.AsSpan(0, _written);
            }
        }

        public void Advance(int count)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (count < 0 || count > _buffer.Length - _written)
                throw new ArgumentOutOfRangeException(nameof(count));
            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(_written);
        }

        private void EnsureCapacity(int sizeHint)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            if (sizeHint == 0) sizeHint = 1;
            if (sizeHint <= _buffer.Length - _written) return;

            var required = checked(_written + sizeHint);
            var replacement = ArrayPool<byte>.Shared.Rent(
                Math.Max(checked(_buffer.Length * 2), required));
            _buffer.AsSpan(0, _written).CopyTo(replacement);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = replacement;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var old = _buffer;
            _buffer = Array.Empty<byte>();
            _written = 0;
            ArrayPool<byte>.Shared.Return(old);
        }
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
