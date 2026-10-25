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
    int? Amount = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Detail = null);

/// <summary>
/// Strictly opt-in, synchronous observation of the real emulator pipeline.
/// An AsyncLocal scope keeps concurrent independent captures separated;
/// nested captures also receive events recorded inside their scope. No
/// RunState mutation or RNG reads are performed.
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

    internal static bool IsActive => Active.Value is not null;

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
        int? amount = null,
        string? detail = null)
    {
        for (var journal = Active.Value; journal is not null;
            journal = journal._previous)
        {
            if (journal._disposed)
                continue;
            journal._entries.Add(new NativeEngineJournalEntryV111(
                journal._entries.Count + 1,
                stage,
                kind,
                sourceCardInstanceId,
                targetEnemyInstanceId,
                amount,
                detail));
        }
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
