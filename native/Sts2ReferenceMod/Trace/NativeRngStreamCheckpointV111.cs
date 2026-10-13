using Sts2Emulator.Core;

namespace Sts2ReferenceMod.Trace;

/// <summary>Read-only inventory of the 12 run and 3 player RNGs as observed
/// at one native combat/action callback. A checkpoint is not a source call.
/// </summary>
public sealed record NativeRngStreamCursorV111(
    string StreamId, string OwnerId, NativeRngSnapshot Cursor);

public sealed record NativeRngStreamCheckpointV111(
    string Phase, NativeRngStreamCursorV111[] Streams)
{
    public void Validate()
    {
        if (Phase is not ("combat_attach" or "before_action" or
                           "after_action" or "combat_end")
            || Streams is null || Streams.Length != 15
            || Streams.Count(s => s.StreamId.StartsWith("run.", StringComparison.Ordinal)) != 12
            || Streams.Count(s => s.StreamId.StartsWith("player.", StringComparison.Ordinal)) != 3
            || Streams.Any(s => string.IsNullOrWhiteSpace(s.StreamId)
                || string.IsNullOrWhiteSpace(s.OwnerId)
                || s.Cursor is null || s.Cursor.Counter < 0)
            || Streams.Select(s => s.StreamId)
                .Distinct(StringComparer.Ordinal).Count() != Streams.Length)
            throw new ArgumentException("Expected a unique 12+3 native RNG stream checkpoint.");
    }
}
