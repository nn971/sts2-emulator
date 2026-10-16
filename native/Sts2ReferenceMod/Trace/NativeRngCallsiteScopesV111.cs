namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Synchronous native source-level call-site scope. A primitive RNG patch
/// resolves a semantic identity only when the innermost audited source scope
/// has the correct native stream. Ordinary contexts remain UNATTRIBUTED.
/// No source method, random operation, or game state is invoked here.
/// </summary>
public static class NativeRngCallsiteScopesV111
{
    private sealed class Frame(
        string siteId, string streamId, string ownerId, Frame? parent)
    {
        public readonly string SiteId = siteId;
        public readonly string StreamId = streamId;
        public readonly string OwnerId = ownerId;
        public readonly Frame? Parent = parent;
        public long Ordinal;
    }

    [ThreadStatic]
    private static Frame? _active;

    private sealed class Scope(Frame current) : IDisposable
    {
        private bool _closed;

        public void Dispose()
        {
            if (_closed) return;
            if (!ReferenceEquals(_active, current))
                throw new InvalidOperationException(
                    "Native call-site scope was not closed in source call order.");
            _active = current.Parent;
            _closed = true;
        }
    }

    public static IDisposable Enter(
        string siteId, string streamId, string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        var frame = new Frame(siteId, streamId, ownerId, _active);
        _active = frame;
        return new Scope(frame);
    }

    public static (string SiteId, string OwnerId, long CallOrdinal)? NextFor(
        string streamId)
    {
        var frame = _active;
        if (frame is null || frame.StreamId != streamId)
            return null;
        long ordinal = frame.Ordinal++;
        return (frame.SiteId, frame.OwnerId, ordinal);
    }
}
