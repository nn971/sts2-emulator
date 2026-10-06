using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Language-neutral semantic action. Payload is canonical JSON so the trace protocol
/// can evolve before every action receives a dedicated CLR type.
/// </summary>
public sealed record GameAction(string Kind, JsonElement Payload)
{
    public static GameAction Empty(string kind)
    {
        using var document = JsonDocument.Parse("{}");
        return new GameAction(kind, document.RootElement.Clone());
    }
}
