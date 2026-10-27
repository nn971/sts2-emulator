using System.Diagnostics;

namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in, current-thread-only timing and managed allocation measurements for
/// the nested semantic draw-resolve path. No simulation state or RNG is touched.
/// The probe remains inert when the CLI has not explicitly installed a sink.
/// </summary>
public static class SemanticDrawStageProfilerV111
{
    [ThreadStatic]
    private static Action<string, long, long>? _sink;

    private sealed class Scope : IDisposable
    {
        private readonly Action<string, long, long>? _previous;
        private bool _disposed;

        public Scope(Action<string, long, long>? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed) return;
            _sink = _previous;
            _disposed = true;
        }
    }

    /// <summary>Install a profiler for one synchronous draw operation only.</summary>
    public static IDisposable Begin(Action<string, long, long>? sink)
    {
        var previous = _sink;
        _sink = sink;
        return new Scope(previous);
    }

    public static T Measure<T>(string operation, Func<T> evaluate)
    {
        var sink = _sink;
        if (sink is null) return evaluate();
        var started = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            return evaluate();
        }
        finally
        {
            sink(operation, started, allocated);
        }
    }

    public static void Measure(string operation, Action execute)
    {
        var sink = _sink;
        if (sink is null)
        {
            execute();
            return;
        }
        var started = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            execute();
        }
        finally
        {
            sink(operation, started, allocated);
        }
    }
}
