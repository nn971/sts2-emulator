using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Boundary-scoped provider selection shared by typed random adapter calls
/// that receive providers explicitly and calls that use default providers.
/// A replay boundary can replace those paths for one synchronous mechanics
/// continuation without changing the mechanics call graph.
/// </summary>
internal static class StochasticProviderScopeV111
{
    private sealed class Frame(
        IRandomIndexProviderV111? indexProvider,
        IRandomUnitFloatProviderV111? unitFloatProvider,
        IRandomRangedFloatProviderV111? rangedFloatProvider,
        IRandomRangedDoubleProviderV111? rangedDoubleProvider,
        IRandomGaussianIntProviderV111? gaussianIntProvider,
        IRandomGaussianDoubleProviderV111? gaussianDoubleProvider,
        bool overrideExplicitProviders,
        Func<ControlledStochasticRequestV111, bool>? requestFilter,
        Frame? parent)
    {
        public IRandomIndexProviderV111? IndexProvider { get; } = indexProvider;
        public IRandomUnitFloatProviderV111? UnitFloatProvider { get; } =
            unitFloatProvider;
        public IRandomRangedFloatProviderV111? RangedFloatProvider { get; } =
            rangedFloatProvider;
        public IRandomRangedDoubleProviderV111? RangedDoubleProvider { get; } =
            rangedDoubleProvider;
        public IRandomGaussianIntProviderV111? GaussianIntProvider { get; } =
            gaussianIntProvider;
        public IRandomGaussianDoubleProviderV111? GaussianDoubleProvider { get; } =
            gaussianDoubleProvider;
        public bool OverrideExplicitProviders { get; } =
            overrideExplicitProviders;
        public Func<ControlledStochasticRequestV111, bool>? RequestFilter { get; } =
            requestFilter;
        public Frame? Parent { get; } = parent;
        public object OrdinalSync { get; } = new();
        public Dictionary<string, ulong> NextOrdinals { get; } =
            new(StringComparer.Ordinal);
    }

    private sealed class ScopeLease(Frame frame) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            if (!ReferenceEquals(Current.Value, frame))
                throw new InvalidOperationException(
                    "Stochastic provider scopes must be disposed in stack order.");
            Current.Value = frame.Parent;
            _disposed = true;
        }
    }

    private static readonly AsyncLocal<Frame?> Current = new();

    public static bool HasProviderOverride =>
        HasOverridingProvider(Current.Value);

    public static IDisposable Enter(
        IRandomIndexProviderV111? indexProvider = null,
        IRandomUnitFloatProviderV111? unitFloatProvider = null,
        bool overrideExplicitProviders = true,
        IRandomRangedFloatProviderV111? rangedFloatProvider = null,
        IRandomRangedDoubleProviderV111? rangedDoubleProvider = null,
        IRandomGaussianIntProviderV111? gaussianIntProvider = null,
        IRandomGaussianDoubleProviderV111? gaussianDoubleProvider = null)
        => EnterCore(indexProvider, unitFloatProvider,
            rangedFloatProvider, rangedDoubleProvider,
            gaussianIntProvider, gaussianDoubleProvider,
            overrideExplicitProviders, null);

    /// <summary>
    /// Installs one provider across every typed random primitive while the
    /// ordinary mechanics call graph runs.
    /// </summary>
    public static IDisposable EnterBoundary(
        IRandomBoundaryProviderV111 provider,
        bool overrideExplicitProviders = true)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return Enter(provider, provider, overrideExplicitProviders,
            provider, provider, provider, provider);
    }

    public static IDisposable EnterFiltered(
        IRandomIndexProviderV111? indexProvider,
        IRandomUnitFloatProviderV111? unitFloatProvider,
        Func<ControlledStochasticRequestV111, bool> requestFilter,
        bool overrideExplicitProviders = true)
    {
        ArgumentNullException.ThrowIfNull(requestFilter);
        return EnterCore(indexProvider, unitFloatProvider,
            null, null, null, null,
            overrideExplicitProviders, requestFilter);
    }

    public static IDisposable EnterFilteredBoundary(
        IRandomBoundaryProviderV111 provider,
        Func<ControlledStochasticRequestV111, bool> requestFilter,
        bool overrideExplicitProviders = true)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(requestFilter);
        return EnterCore(provider, provider, provider, provider, provider,
            provider, overrideExplicitProviders, requestFilter);
    }

    private static IDisposable EnterCore(
        IRandomIndexProviderV111? indexProvider,
        IRandomUnitFloatProviderV111? unitFloatProvider,
        IRandomRangedFloatProviderV111? rangedFloatProvider,
        IRandomRangedDoubleProviderV111? rangedDoubleProvider,
        IRandomGaussianIntProviderV111? gaussianIntProvider,
        IRandomGaussianDoubleProviderV111? gaussianDoubleProvider,
        bool overrideExplicitProviders,
        Func<ControlledStochasticRequestV111, bool>? requestFilter)
    {
        if (indexProvider is null && unitFloatProvider is null &&
            rangedFloatProvider is null && rangedDoubleProvider is null &&
            gaussianIntProvider is null && gaussianDoubleProvider is null)
            throw new ArgumentException(
                "A stochastic provider scope must replace at least one provider.");
        var frame = new Frame(
            indexProvider, unitFloatProvider, rangedFloatProvider,
            rangedDoubleProvider, gaussianIntProvider, gaussianDoubleProvider,
            overrideExplicitProviders, requestFilter, Current.Value);
        Current.Value = frame;
        return new ScopeLease(frame);
    }

    public static IRandomIndexProviderV111 SelectIndexProvider(
        IRandomIndexProviderV111? explicitProvider)
        => SelectIndexProvider(explicitProvider, null);

    private static IRandomIndexProviderV111 SelectIndexProvider(
        IRandomIndexProviderV111? explicitProvider,
        ControlledStochasticRequestV111? request)
    {
        IRandomIndexProviderV111? fallback = null;
        for (var frame = Current.Value; frame is not null;
             frame = frame.Parent)
        {
            if (frame.IndexProvider is not { } scopedProvider)
                continue;
            if (frame.RequestFilter is { } filter &&
                (request is null || !filter(request)))
                continue;
            fallback ??= scopedProvider;
            if (frame.OverrideExplicitProviders)
                return scopedProvider;
        }
        return explicitProvider ?? fallback ??
            new NativeRandomIndexProviderV111();
    }

    public static RandomIndexDecisionV111 ResolveIndex(
        IRandomIndexProviderV111? explicitProvider,
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        var frame = Current.Value;
        var tracksIndexOrFloat = HasIndexOrUnitFloatProvider(frame);
        if (frame is not null && tracksIndexOrFloat)
        {
            EnsureOrdinal(frame, streams, request.StreamId);
        }
        ControlledStochasticChanceReplayV111.ClearLastResolvedDecision();
        var decision = SelectIndexProvider(
                explicitProvider,
                ControlledStochasticRequestV111.FromIndex(request))
            .Resolve(request, streams);
        if (frame is not null && tracksIndexOrFloat)
            AdvanceOrdinal(frame, streams, request.StreamId,
                GetStreamCallCount(streams, request.StreamId));
        return decision;
    }

    /// <summary>
    /// Resolves a request whose boundary owner supplies its logical ordinal
    /// directly rather than deriving it from a native stream cursor. The
    /// request still selects the scoped provider, including request filters.
    /// </summary>
    public static RandomIndexDecisionV111 ResolveIndexAtDeclaredOrdinal(
        IRandomIndexProviderV111? explicitProvider,
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        var provider = SelectIndexProvider(
            explicitProvider,
            ControlledStochasticRequestV111.FromIndex(request));
        if (provider.Mode == "native-v111")
            request.ValidateForNativeProvider();
        else if (provider.Mode == "legacy-prototype-v0")
            request.ValidateForLegacyProvider();
        else
            request.Validate();
        return provider.Resolve(request, streams);
    }

    public static IRandomUnitFloatProviderV111 SelectUnitFloatProvider(
        IRandomUnitFloatProviderV111? explicitProvider)
        => SelectUnitFloatProvider(explicitProvider, null);

    private static IRandomUnitFloatProviderV111 SelectUnitFloatProvider(
        IRandomUnitFloatProviderV111? explicitProvider,
        ControlledStochasticRequestV111? request)
    {
        IRandomUnitFloatProviderV111? fallback = null;
        for (var frame = Current.Value; frame is not null;
             frame = frame.Parent)
        {
            if (frame.UnitFloatProvider is not { } scopedProvider)
                continue;
            if (frame.RequestFilter is { } filter &&
                (request is null || !filter(request)))
                continue;
            fallback ??= scopedProvider;
            if (frame.OverrideExplicitProviders)
                return scopedProvider;
        }
        return explicitProvider ?? fallback ??
            new NativeRandomUnitFloatProviderV111();
    }

    public static RandomUnitFloatDecisionV111 ResolveUnitFloat(
        IRandomUnitFloatProviderV111? explicitProvider,
        RandomUnitFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        var frame = Current.Value;
        var tracksIndexOrFloat = HasIndexOrUnitFloatProvider(frame);
        if (frame is not null && tracksIndexOrFloat)
        {
            EnsureOrdinal(frame, streams, request.StreamId);
        }
        var decision = SelectUnitFloatProvider(
                explicitProvider,
                ControlledStochasticRequestV111.FromUnitFloat(request))
            .Resolve(request, streams);
        if (frame is not null && tracksIndexOrFloat)
            AdvanceOrdinal(frame, streams, request.StreamId,
                GetStreamCallCount(streams, request.StreamId));
        return decision;
    }

    public static RandomRangedFloatDecisionV111 ResolveRangedFloat(
        IRandomRangedFloatProviderV111? explicitProvider,
        RandomRangedFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        return SelectRangedFloatProvider(explicitProvider,
                ControlledStochasticRequestV111.FromRangedFloat(request))
            .Resolve(request, streams);
    }

    public static RandomRangedDoubleDecisionV111 ResolveRangedDouble(
        IRandomRangedDoubleProviderV111? explicitProvider,
        RandomRangedDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        return SelectRangedDoubleProvider(explicitProvider,
                ControlledStochasticRequestV111.FromRangedDouble(request))
            .Resolve(request, streams);
    }

    public static RandomGaussianIntDecisionV111 ResolveGaussianInt(
        IRandomGaussianIntProviderV111? explicitProvider,
        RandomGaussianIntRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        return SelectGaussianIntProvider(explicitProvider,
                ControlledStochasticRequestV111.FromGaussianInt(request))
            .Resolve(request, streams);
    }

    public static RandomGaussianDoubleDecisionV111 ResolveGaussianDouble(
        IRandomGaussianDoubleProviderV111? explicitProvider,
        RandomGaussianDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        return SelectGaussianDoubleProvider(explicitProvider,
                ControlledStochasticRequestV111.FromGaussianDouble(request))
            .Resolve(request, streams);
    }

    private static IRandomRangedFloatProviderV111 SelectRangedFloatProvider(
        IRandomRangedFloatProviderV111? explicitProvider,
        ControlledStochasticRequestV111 request)
    {
        IRandomRangedFloatProviderV111? fallback = null;
        for (var frame = Current.Value; frame is not null;
             frame = frame.Parent)
        {
            if (frame.RangedFloatProvider is not { } scopedProvider ||
                frame.RequestFilter is { } filter && !filter(request))
                continue;
            fallback ??= scopedProvider;
            if (frame.OverrideExplicitProviders)
                return scopedProvider;
        }
        return explicitProvider ?? fallback ??
            new NativeRandomRangedFloatProviderV111();
    }

    private static IRandomRangedDoubleProviderV111 SelectRangedDoubleProvider(
        IRandomRangedDoubleProviderV111? explicitProvider,
        ControlledStochasticRequestV111 request)
    {
        IRandomRangedDoubleProviderV111? fallback = null;
        for (var frame = Current.Value; frame is not null;
             frame = frame.Parent)
        {
            if (frame.RangedDoubleProvider is not { } scopedProvider ||
                frame.RequestFilter is { } filter && !filter(request))
                continue;
            fallback ??= scopedProvider;
            if (frame.OverrideExplicitProviders)
                return scopedProvider;
        }
        return explicitProvider ?? fallback ??
            new NativeRandomRangedDoubleProviderV111();
    }

    private static IRandomGaussianIntProviderV111 SelectGaussianIntProvider(
        IRandomGaussianIntProviderV111? explicitProvider,
        ControlledStochasticRequestV111 request)
    {
        IRandomGaussianIntProviderV111? fallback = null;
        for (var frame = Current.Value; frame is not null;
             frame = frame.Parent)
        {
            if (frame.GaussianIntProvider is not { } scopedProvider ||
                frame.RequestFilter is { } filter && !filter(request))
                continue;
            fallback ??= scopedProvider;
            if (frame.OverrideExplicitProviders)
                return scopedProvider;
        }
        return explicitProvider ?? fallback ??
            new NativeRandomGaussianIntProviderV111();
    }

    private static IRandomGaussianDoubleProviderV111
        SelectGaussianDoubleProvider(
            IRandomGaussianDoubleProviderV111? explicitProvider,
            ControlledStochasticRequestV111 request)
    {
        IRandomGaussianDoubleProviderV111? fallback = null;
        for (var frame = Current.Value; frame is not null;
             frame = frame.Parent)
        {
            if (frame.GaussianDoubleProvider is not { } scopedProvider ||
                frame.RequestFilter is { } filter && !filter(request))
                continue;
            fallback ??= scopedProvider;
            if (frame.OverrideExplicitProviders)
                return scopedProvider;
        }
        return explicitProvider ?? fallback ??
            new NativeRandomGaussianDoubleProviderV111();
    }

    public static int? GetNextOrdinal(
        RngBundle streams, string streamId)
    {
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        var frame = Current.Value;
        if (frame is null || !HasOverridingProvider(frame))
            return null;
        EnsureOrdinal(frame, streams, streamId);
        lock (frame.OrdinalSync)
        {
            var nextOrdinal = frame.NextOrdinals[streamId];
            if (nextOrdinal > int.MaxValue)
                throw new NotSupportedException(
                    "Scoped native provider ordinal exceeds the request range.");
            return checked((int)nextOrdinal);
        }
    }

    private static bool HasOverridingProvider(Frame? frame)
    {
        for (; frame is not null; frame = frame.Parent)
            if (frame.OverrideExplicitProviders &&
                (frame.IndexProvider is not null ||
                 frame.UnitFloatProvider is not null))
                return true;
        return false;
    }

    private static bool HasIndexOrUnitFloatProvider(Frame? frame)
    {
        for (; frame is not null; frame = frame.Parent)
            if (frame.IndexProvider is not null ||
                frame.UnitFloatProvider is not null)
                return true;
        return false;
    }

    private static void EnsureOrdinal(
        Frame frame, RngBundle streams, string streamId)
    {
        if (frame.Parent is { } parent)
            EnsureOrdinal(parent, streams, streamId);
        lock (frame.OrdinalSync)
        {
            if (frame.NextOrdinals.ContainsKey(streamId))
                return;
            var initialOrdinal = frame.Parent is { } parentFrame
                ? parentFrame.NextOrdinals[streamId]
                : GetStreamCallCount(streams, streamId);
            if (initialOrdinal > int.MaxValue)
                throw new NotSupportedException(
                    "Scoped native provider ordinal exceeds the request range.");
            frame.NextOrdinals.Add(streamId, initialOrdinal);
        }
    }

    private static void AdvanceOrdinal(
        Frame frame, RngBundle streams, string streamId, ulong cursorAfter)
    {
        for (var current = frame; current is not null;
             current = current.Parent)
        {
            EnsureOrdinal(current, streams, streamId);
            lock (current.OrdinalSync)
            {
                var nextOrdinal = checked(
                    current.NextOrdinals[streamId] + 1);
                current.NextOrdinals[streamId] = Math.Max(
                    nextOrdinal, cursorAfter);
            }
        }
    }

    private static ulong GetStreamCallCount(
        RngBundle streams, string streamId)
    {
        return streams.GetStream(streamId).CallCount ?? 0;
    }
}
