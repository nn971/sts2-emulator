using System.Globalization;
using System.Threading;

namespace Sts2Emulator.Core;

internal sealed record NativeEventShuffleCaptureV111(
    string SiteId, string OwnerId, string[] OrderedCandidateIds);

/// <summary>Opt-in observation of the source-ordered values immediately
/// before an event-local shuffle. Used only by read-only resolver probes.</summary>
internal sealed class NativeEventShuffleProbeV111 : IDisposable
{
    private static readonly AsyncLocal<NativeEventShuffleProbeV111?> Active =
        new();
    private readonly NativeEventShuffleProbeV111? _previous;
    private readonly List<NativeEventShuffleCaptureV111> _captures = [];
    private bool _disposed;

    private NativeEventShuffleProbeV111(
        NativeEventShuffleProbeV111? previous) => _previous = previous;

    public IReadOnlyList<NativeEventShuffleCaptureV111> Captures =>
        _captures.Select(capture => capture with
        {
            OrderedCandidateIds =
                (string[])capture.OrderedCandidateIds.Clone()
        }).ToArray();

    public static NativeEventShuffleProbeV111 Begin()
    {
        var probe = new NativeEventShuffleProbeV111(Active.Value);
        Active.Value = probe;
        return probe;
    }

    public static void Capture<T>(
        string siteId, string ownerId, IList<T> orderedValues)
    {
        var probe = Active.Value;
        if (probe is null)
            return;
        if (probe._disposed)
            throw new InvalidOperationException(
                "Native event shuffle probes cannot capture after disposal.");
        probe._captures.Add(new NativeEventShuffleCaptureV111(
            siteId, ownerId,
            orderedValues.Select(value => Convert.ToString(
                value, CultureInfo.InvariantCulture) ?? "").ToArray()));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (!ReferenceEquals(Active.Value, this))
            throw new InvalidOperationException(
                "Native event shuffle probes must be disposed in stack order.");
        Active.Value = _previous;
        _disposed = true;
    }
}
