using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Passive, main-frame-polled task-reference observer. Game callbacks only
/// enqueue task references; no observer continuation is attached to native
/// tasks and no native task is awaited, scheduled, or canceled.
/// </summary>
public sealed class NativeAsyncTaskTracker
{
    private sealed record Registration(Task Task, string Family, string Source);
    private sealed record Entry(long Id, Task Task, string Family, string Source);
    private readonly ConcurrentQueue<Registration> _incoming = new();
    private readonly Dictionary<Task, Entry> _seen =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<long, Entry> _pending = [];
    private readonly NativeActionEnvelopeRecorder _recorder;
    private long _nextId;
    private int _registered;
    private int _completed;
    private int _overflow;
    private volatile bool _closed;

    public const int MaxTaskRegistrations = 32768;
    public const string Coverage = "NAMED_ASYNC_ENTRYPOINTS_ONLY";

    public NativeAsyncTaskTracker(NativeActionEnvelopeRecorder recorder) =>
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));

    public void Register(Task? task, string family, string source)
    {
        if (_closed || task is null) return;
        if (family is not (
                "GAME_ACTION_BODY" or "GAME_ACTION_STEP" or
                "HOOK_TASK_BODY" or "HOOK_PAUSE_WAIT" or
                "HOOK_COMPLETION_BARRIER" or "TURN_LOOP"))
            throw new ArgumentOutOfRangeException(nameof(family));
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Missing native source method", nameof(source));
        _incoming.Enqueue(new(task, family, source));
    }

    private static string TaskStatus(Task task) =>
        !task.IsCompleted ? "PENDING" :
        task.IsFaulted ? "FAULTED" :
        task.IsCanceled ? "CANCELED" : "RAN_TO_COMPLETION";

    /// <summary>Must be invoked on the native game frame thread.</summary>
    public void Poll()
    {
        if (_closed) return;
        while (_incoming.TryDequeue(out var item))
        {
            if (_seen.ContainsKey(item.Task)) continue;
            if (_seen.Count >= MaxTaskRegistrations)
            {
                _overflow++;
                continue;
            }
            var entry = new Entry(++_nextId, item.Task, item.Family, item.Source);
            _seen.Add(item.Task, entry);
            _registered++;
            _recorder.NativeAsyncTaskLifecycle(entry.Id, entry.Family,
                entry.Source, "registered", TaskStatus(entry.Task));
            _pending.Add(entry.Id, entry);
        }

        // Completion means "first observed completed at this game frame";
        // no timestamp of the underlying task's actual completion is claimed.
        foreach (var entry in _pending.Values.ToArray())
        {
            if (!entry.Task.IsCompleted) continue;
            _recorder.NativeAsyncTaskLifecycle(entry.Id, entry.Family,
                entry.Source, "completed", TaskStatus(entry.Task));
            _pending.Remove(entry.Id);
            _completed++;
        }
    }

    public void Checkpoint(int? turn, string site)
    {
        if (_closed) return;
        Poll();
        var byFamily = _pending.Values.GroupBy(x => x.Family)
            .ToDictionary(g => g.Key, g => g.Count(),
                StringComparer.Ordinal);
        var transientPending = _pending.Values.Count(
            x => x.Family != "TURN_LOOP");
        _recorder.NativeAsyncTaskCheckpoint(turn, site,
            _pending.Count, transientPending, byFamily, _registered, _completed,
            _overflow, _pending.Keys.OrderBy(id => id).ToArray());
    }

    public void EndCombat()
    {
        if (_closed) return;
        Checkpoint(null, "COMBAT_TEARDOWN");
        _closed = true;
        while (_incoming.TryDequeue(out _)) { }
        _pending.Clear();
        _seen.Clear();
    }
}
