using System.Threading.Channels;

namespace Sts2Emulator.Trace;

/// <summary>
/// Single ordered disk writer. Producers never block the native game thread.
/// A full queue fails closed; the caller marks the capture incomplete.
/// </summary>
public sealed class AsyncTraceWriter : IAsyncDisposable
{
    private readonly Channel<object> _queue;
    private readonly Task _pump;
    private readonly string _path;
    private bool _completed;
    private bool _overflow;

    public AsyncTraceWriter(string path, int capacity = 8192)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _path = Path.GetFullPath(path);
        _queue = Channel.CreateBounded<object>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait
        });
        _pump = Task.Run(WriteLoopAsync);
    }

    public bool TryEnqueue(object record)
    {
        if (_completed || !_queue.Writer.TryWrite(record))
        {
            _overflow = true;
            return false;
        }
        return true;
    }

    public async Task CompleteAsync()
    {
        if (_completed) return;
        _completed = true;
        _queue.Writer.TryComplete();
        try
        {
            await _pump.ConfigureAwait(false);
            if (_overflow)
                await File.WriteAllTextAsync(_path + ".incomplete",
                    "TRACE_QUEUE_OVERFLOW: semantic records could not be persisted.").ConfigureAwait(false);
        }
        catch (Exception e)
        {
            await File.WriteAllTextAsync(_path + ".incomplete",
                "TRACE_WRITE_FAILED: " + e).ConfigureAwait(false);
            throw;
        }
    }

    private async Task WriteLoopAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var file = new FileStream(_path, FileMode.CreateNew, FileAccess.Write,
            FileShare.Read, bufferSize: 65536, useAsync: true);
        await using var writer = new StreamWriter(file) { AutoFlush = true };
        await foreach (var record in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
            await TraceJsonl.AppendLineAsync(writer, record).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => new(CompleteAsync());
}
