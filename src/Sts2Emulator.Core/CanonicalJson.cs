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
    public static string Serialize<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Sha256<T>(T value)
    {
        if (!SemanticDrawStageProfilerV111.Active)
            return Sha256Core(value);
        // Profiling only: aggregate canonical JSON hashing by statically
        // declared source type, including serialization and UTF-8 copies.
        // This retains the exact same hash and canonical ordering.
        return SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_hash_" + typeof(T).Name,
            () => Sha256Core(value));
    }

    private static string Sha256Core<T>(T value)
    {
        var utf8 = Encoding.UTF8.GetBytes(Serialize(value));
        return Convert.ToHexString(SHA256.HashData(utf8)).ToLowerInvariant();
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
