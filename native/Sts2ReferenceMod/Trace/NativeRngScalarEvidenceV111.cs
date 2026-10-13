using Sts2Emulator.Core;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Observed ORIGINAL return from the pinned game's Rng.NextDouble() or
/// Rng.NextBool(). No random method is invoked when recording this evidence.
/// Source-hook runtime behavior still requires a game-local DLL test.
/// </summary>
public sealed record NativeRngScalarEvidenceV111(
    string SiteId, string StreamId, string OwnerId, long CallOrdinal,
    string OperationKind, double? SelectedDouble, bool? SelectedBool,
    NativeRngSnapshot CursorBefore, NativeRngSnapshot CursorAfter,
    string Origin = "source_primitive_hook",
    string SourceVersion = "v0.111.0")
{
    public void Validate()
    {
        bool valueValid = OperationKind switch
        {
            "unit_double" => SelectedDouble is >= 0.0 and < 1.0
                && !SelectedBool.HasValue,
            "boolean" => SelectedBool.HasValue && !SelectedDouble.HasValue,
            _ => false
        };
        if (!valueValid ||
            Origin != "source_primitive_hook" ||
            SourceVersion != "v0.111.0" ||
            string.IsNullOrWhiteSpace(SiteId) ||
            string.IsNullOrWhiteSpace(StreamId) ||
            string.IsNullOrWhiteSpace(OwnerId) ||
            CallOrdinal < 0 || CursorBefore.Counter < 0 ||
            CursorAfter.Counter != CursorBefore.Counter + 1)
            throw new ArgumentException("Invalid native scalar source-call evidence.");
    }

    public bool Attributed =>
        SiteId != "UNATTRIBUTED" &&
        StreamId != "UNATTRIBUTED" &&
        OwnerId != "UNATTRIBUTED";
}
