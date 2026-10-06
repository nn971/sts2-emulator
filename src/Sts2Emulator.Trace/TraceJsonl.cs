using System.Text.Json;

namespace Sts2Emulator.Trace;

public static class TraceJsonl
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public static string SerializeLine<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static async Task AppendLineAsync<T>(StreamWriter writer, T value, CancellationToken cancellationToken = default)
    {
        await writer.WriteLineAsync(SerializeLine(value).AsMemory(), cancellationToken);
    }
}
