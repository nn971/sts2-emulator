using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Language-neutral semantic action. Payload remains canonical JSON at the public boundary,
/// while implemented engines may deserialize it into typed phase-specific payload records.
/// </summary>
public sealed record GameAction(string Kind, JsonElement Payload)
{
    public static GameAction Empty(string kind)
    {
        using var document = JsonDocument.Parse("{}");
        return new GameAction(kind, document.RootElement.Clone());
    }

    public static GameAction Create<T>(string kind, T payload) =>
        new(kind, JsonSerializer.SerializeToElement(payload));

    public T ReadPayload<T>() =>
        Payload.Deserialize<T>()
        ?? throw new InvalidOperationException($"Action '{Kind}' has an invalid payload.");
}
