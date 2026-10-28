using System.Diagnostics;

namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in, single-threaded H8 nested stage profiler. The JSONL server installs
/// the sink on its request thread; normal combat decisions have no stopwatch,
/// GC counter or hash cost when no sink is installed. Operations are inclusive:
/// the child time of a nested scope must not be summed with its parent.
/// Provider choices, hidden state, and mechanics are never changed.
/// </summary>
public static class H8HotPathProfilerV1
{
    [ThreadStatic] private static Action<string, long, long>? _sink;

    public static bool Active => _sink is not null;

    /// <summary>Single owning JSONL thread; null turns profiling off.</summary>
    public static void SetSink(Action<string, long, long>? sink) =>
        _sink = sink;

    public static Stage Begin(string operation)
    {
        var sink = _sink;
        if (sink is null)
            return default;
        return new Stage(sink, operation, Stopwatch.GetTimestamp(),
            GC.GetAllocatedBytesForCurrentThread());
    }

    public readonly struct Stage : IDisposable
    {
        private readonly Action<string, long, long>? _record;
        private readonly string? _name;
        private readonly long _ticks;
        private readonly long _allocated;

        internal Stage(Action<string, long, long> record, string name,
            long ticks, long allocated)
        {
            _record = record;
            _name = name;
            _ticks = ticks;
            _allocated = allocated;
        }

        public void Dispose()
        {
            if (_record is null)
                return;
            _record(_name!, Stopwatch.GetTimestamp() - _ticks,
                Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - _allocated));
        }
    }
}
