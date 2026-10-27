using System.Diagnostics;

namespace Sts2Emulator.Cli;

/// <summary>
/// Per-process, opt-in profiling for the semantic JSONL boundary.
/// It never mutates simulation state or changes the wire format of gameplay.
/// All requests in PrototypeAiJsonlServer are handled on one thread.
/// </summary>
internal sealed class SemanticOperationProfiler
{
    private readonly Dictionary<string, Counter> _counters =
        new(StringComparer.Ordinal);

    private sealed class Counter
    {
        public long Count;
        public long Ticks;
        public long AllocatedBytes;
        public long MaxTicks;
    }

    public bool Enabled { get; private set; }

    public void Reset()
    {
        _counters.Clear();
        Enabled = true;
    }

    public static long Timestamp() => Stopwatch.GetTimestamp();

    public static long Allocated() => GC.GetAllocatedBytesForCurrentThread();

    public void Record(string operation, long started, long allocatedBefore)
    {
        if (!Enabled || started == 0)
            return;
        var elapsed = Stopwatch.GetTimestamp() - started;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        if (!_counters.TryGetValue(operation, out var counter))
            _counters.Add(operation, counter = new Counter());
        counter.Count++;
        counter.Ticks += elapsed;
        counter.AllocatedBytes += Math.Max(0L, allocated);
        counter.MaxTicks = Math.Max(counter.MaxTicks, elapsed);
    }

    public object Snapshot() => new
    {
        schemaId = "sts2-semantic-jsonl-performance-v1",
        enabled = Enabled,
        processId = Environment.ProcessId,
        stopwatchFrequency = Stopwatch.Frequency,
        operations = _counters.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                pair => pair.Key,
                pair => new
                {
                    count = pair.Value.Count,
                    totalSeconds = (double)pair.Value.Ticks / Stopwatch.Frequency,
                    maxSeconds = (double)pair.Value.MaxTicks / Stopwatch.Frequency,
                    allocatedBytes = pair.Value.AllocatedBytes
                },
                StringComparer.Ordinal)
    };
}
