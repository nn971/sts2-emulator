using System.Globalization;
using System.Threading;

namespace Sts2Emulator.Core;

internal sealed record NativeEventShuffleCaptureV111(
    string SiteId, string OwnerId, string[] OrderedCandidateIds,
    string? NativeStreamId = null,
    NativeRngSnapshot? NativeCursorBefore = null);

internal sealed record NativeEventUnitFloatCaptureV111(
    RandomUnitFloatRequestV111 Request, RunState PreDrawState);

/// <summary>Opt-in observation of Native pre-draw constraints: ordered
/// shuffle support or a typed unit-float request and its input state.</summary>
internal sealed class NativeEventRandomProbeV111 : IDisposable
{
    private static readonly AsyncLocal<NativeEventRandomProbeV111?> Active =
        new();
    private readonly NativeEventRandomProbeV111? _previous;
    private readonly List<NativeEventShuffleCaptureV111> _captures = [];
    private readonly List<NativeEventUnitFloatCaptureV111> _unitFloatCaptures = [];
    private bool _disposed;

    private NativeEventRandomProbeV111(
        NativeEventRandomProbeV111? previous) => _previous = previous;

    public IReadOnlyList<NativeEventShuffleCaptureV111> ShuffleCaptures =>
        _captures.Select(capture => capture with
        {
            OrderedCandidateIds =
                (string[])capture.OrderedCandidateIds.Clone()
        }).ToArray();

    public IReadOnlyList<NativeEventUnitFloatCaptureV111> UnitFloatCaptures =>
        _unitFloatCaptures.Select(capture => capture with
        {
            Request = capture.Request with { },
            PreDrawState = capture.PreDrawState.Fork()
        }).ToArray();

    public static NativeEventRandomProbeV111 Begin()
    {
        var probe = new NativeEventRandomProbeV111(Active.Value);
        Active.Value = probe;
        return probe;
    }

    public static void CaptureShuffle<T>(
        string siteId, string ownerId, IList<T> orderedValues)
        => CaptureShuffle(siteId, ownerId, orderedValues, null, null);

    public static void CaptureShuffle<T>(
        string siteId, string ownerId, IList<T> orderedValues,
        RngBundle? streams, string? nativeStreamId)
    {
        var probe = Active.Value;
        if (probe is null)
            return;
        if (probe._disposed)
            throw new InvalidOperationException(
                "Native event random probes cannot capture after disposal.");
        NativeRngSnapshot? cursor = null;
        if (nativeStreamId is not null)
        {
            if (streams is null)
                throw new ArgumentNullException(nameof(streams));
            cursor = NativeRngV111.ToSnapshot(
                streams.GetStream(nativeStreamId));
        }
        probe._captures.Add(new NativeEventShuffleCaptureV111(
            siteId, ownerId,
            orderedValues.Select(value => Convert.ToString(
                value, CultureInfo.InvariantCulture) ?? "").ToArray(),
            nativeStreamId, cursor));
    }

    public static void CaptureUnitFloat(
        RunState state, RandomUnitFloatRequestV111 request)
    {
        var probe = Active.Value;
        if (probe is null)
            return;
        if (probe._disposed)
            throw new InvalidOperationException(
                "Native event random probes cannot capture after disposal.");
        probe._unitFloatCaptures.Add(new NativeEventUnitFloatCaptureV111(
            request with { }, state.Fork()));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (!ReferenceEquals(Active.Value, this))
            throw new InvalidOperationException(
                "Native event random probes must be disposed in stack order.");
        Active.Value = _previous;
        _disposed = true;
    }
}
