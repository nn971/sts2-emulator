using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Stops the real native combat-entry pipeline immediately before its first
/// enemy-intent draw. The semantic route builder persists this source state,
/// then resumes the ordinary opening hooks after Nature or native selection.
/// </summary>
internal sealed class SemanticInitialIntentCaptureV111 : IDisposable
{
    private static readonly AsyncLocal<SemanticInitialIntentCaptureV111?> Active =
        new();
    private readonly SemanticInitialIntentCaptureV111? _previous;
    private bool _disposed;

    private SemanticInitialIntentCaptureV111()
    {
        _previous = Active.Value;
        Active.Value = this;
    }

    public RunState? State { get; private set; }

    public static SemanticInitialIntentCaptureV111 Begin() => new();

    internal static void CaptureAndSuspend(RunState state)
    {
        var active = Active.Value;
        if (active is null) return;
        if (active.State is not null)
            throw new InvalidOperationException(
                "The semantic route captured more than one initial intent boundary.");
        active.State = state.Fork();
        throw new SemanticInitialIntentBoundaryException();
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (!ReferenceEquals(Active.Value, this))
            throw new InvalidOperationException(
                "Semantic initial-intent scopes must close in LIFO order.");
        Active.Value = _previous;
        _disposed = true;
    }
}

internal sealed class SemanticInitialIntentBoundaryException : Exception { }
