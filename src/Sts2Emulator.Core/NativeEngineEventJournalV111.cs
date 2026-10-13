using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// One observation of the actual v111 engine execution path. Event names are
/// emulator-internal execution stages, not claims about native history-hook
/// semantics. Sequence is local to a single engine step.
/// </summary>
public sealed record NativeEngineJournalEntryV111(
    int Sequence,
    string Stage,
    string Kind,
    long? SourceCardInstanceId = null,
    int? TargetEnemyInstanceId = null,
    int? Amount = null);

/// <summary>
/// Strictly opt-in, synchronous observation of the real emulator pipeline.
/// An AsyncLocal scope keeps concurrent independent step tests separated and
/// permits nested scopes. No RunState mutation or RNG reads are performed.
/// </summary>
public sealed class NativeEngineEventJournalV111 : IDisposable
{
    private static readonly AsyncLocal<NativeEngineEventJournalV111?> Active = new();
    private readonly NativeEngineEventJournalV111? _previous;
    private readonly List<NativeEngineJournalEntryV111> _entries = [];
    private bool _disposed;

    private NativeEngineEventJournalV111(NativeEngineEventJournalV111? previous)
    {
        _previous = previous;
    }

    public IReadOnlyList<NativeEngineJournalEntryV111> Entries => _entries;

    public static NativeEngineEventJournalV111 Begin()
    {
        var journal = new NativeEngineEventJournalV111(Active.Value);
        Active.Value = journal;
        return journal;
    }

    internal static void Record(
        string stage,
        string kind,
        long? sourceCardInstanceId = null,
        int? targetEnemyInstanceId = null,
        int? amount = null)
    {
        var journal = Active.Value;
        if (journal is null)
            return;
        journal._entries.Add(new NativeEngineJournalEntryV111(
            journal._entries.Count + 1,
            stage,
            kind,
            sourceCardInstanceId,
            targetEnemyInstanceId,
            amount));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (Active.Value != this)
            throw new InvalidOperationException(
                "Engine fidelity journals must be disposed in reverse scope order.");
        Active.Value = _previous;
        _disposed = true;
    }
}
