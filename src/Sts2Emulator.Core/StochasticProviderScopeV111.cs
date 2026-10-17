using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Boundary-scoped provider selection shared by native adapter calls that
/// receive providers explicitly and calls that use the default provider.
/// A Nature or replay boundary can replace both paths for one synchronous
/// mechanics continuation without changing the mechanics call graph.
/// </summary>
internal static class StochasticProviderScopeV111
{
    private sealed class Frame(
        IRandomIndexProviderV111? indexProvider,
        IRandomUnitFloatProviderV111? unitFloatProvider,
        bool overrideExplicitProviders,
        Frame? parent)
    {
        public IRandomIndexProviderV111? IndexProvider { get; } = indexProvider;
        public IRandomUnitFloatProviderV111? UnitFloatProvider { get; } =
            unitFloatProvider;
        public bool OverrideExplicitProviders { get; } =
            overrideExplicitProviders;
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
        bool overrideExplicitProviders = true)
    {
        if (indexProvider is null && unitFloatProvider is null)
            throw new ArgumentException(
                "A stochastic provider scope must replace at least one provider.");
        var frame = new Frame(
            indexProvider, unitFloatProvider, overrideExplicitProviders,
            Current.Value);
        Current.Value = frame;
        return new ScopeLease(frame);
    }

    public static IRandomIndexProviderV111 SelectIndexProvider(
        IRandomIndexProviderV111? explicitProvider)
    {
        IRandomIndexProviderV111? fallback = null;
        for (var frame = Current.Value; frame is not null;
             frame = frame.Parent)
        {
            if (frame.IndexProvider is not { } scopedProvider)
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
        if (frame is not null)
        {
            EnsureOrdinal(frame, streams, request.StreamId);
        }
        var decision = SelectIndexProvider(explicitProvider)
            .Resolve(request, streams);
        if (frame is not null)
            AdvanceOrdinal(frame, streams, request.StreamId,
                GetStreamCallCount(streams, request.StreamId));
        return decision;
    }

    public static IRandomUnitFloatProviderV111 SelectUnitFloatProvider(
        IRandomUnitFloatProviderV111? explicitProvider)
    {
        IRandomUnitFloatProviderV111? fallback = null;
        for (var frame = Current.Value; frame is not null;
             frame = frame.Parent)
        {
            if (frame.UnitFloatProvider is not { } scopedProvider)
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
        if (frame is not null)
        {
            EnsureOrdinal(frame, streams, request.StreamId);
        }
        var decision = SelectUnitFloatProvider(explicitProvider)
            .Resolve(request, streams);
        if (frame is not null)
            AdvanceOrdinal(frame, streams, request.StreamId,
                GetStreamCallCount(streams, request.StreamId));
        return decision;
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
        var matches = streams.Streams.Where(stream =>
            stream.StreamId == streamId).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Scoped native random stream ownership is ambiguous.");
        return matches[0].CallCount ?? 0;
    }
}
