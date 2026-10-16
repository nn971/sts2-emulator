using Sts2Emulator.Core;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Immutable evidence from a real v111 RNG primitive *source call*. Only a
/// native source hook may construct a production observation. The recorder
/// never invokes RNG; this DTO is also useful for synthetic protocol tests.
/// A frame-to-frame cursor delta is NOT a source-call observation.
/// </summary>
public sealed record NativeRngBoundaryEvidenceV111(
    string SiteId,
    string StreamId,
    string OwnerId,
    long CallOrdinal,
    int BoundedCount,
    int SelectedIndex,
    NativeRngSnapshot CursorBefore,
    NativeRngSnapshot CursorAfter,
    string Origin = "source_primitive_hook",
    string SourceVersion = "v0.111.0")
{
    public void Validate()
    {
        if (Origin != "source_primitive_hook" ||
            SourceVersion != "v0.111.0" ||
            string.IsNullOrWhiteSpace(SiteId) ||
            string.IsNullOrWhiteSpace(StreamId) ||
            string.IsNullOrWhiteSpace(OwnerId) ||
            CallOrdinal < 0 || BoundedCount <= 0 ||
            SelectedIndex < 0 || SelectedIndex >= BoundedCount ||
            CursorBefore.Counter < 0 ||
            CursorAfter.Counter != CursorBefore.Counter + 1)
            throw new ArgumentException(
                "Native RNG boundary must come from one exact source primitive call.");
    }

    public bool Attributed =>
        SiteId != "UNATTRIBUTED" &&
        StreamId != "UNATTRIBUTED" &&
        OwnerId != "UNATTRIBUTED";
}
