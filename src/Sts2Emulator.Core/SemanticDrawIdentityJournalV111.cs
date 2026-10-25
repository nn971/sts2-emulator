namespace Sts2Emulator.Core;

/// <summary>
/// Captures physical card identities consumed by the mechanics draw kernel.
/// This is replay diagnostics, not a Native RNG boundary: a Native provider
/// may have committed the order earlier, while Nature may resolve each card
/// when DrawCards asks for it.
/// </summary>
internal sealed class SemanticDrawIdentityJournalV111 : IDisposable
{
    private static readonly AsyncLocal<SemanticDrawIdentityJournalV111?> Active = new();
    private readonly SemanticDrawIdentityJournalV111? _previous;
    private readonly List<long> _cardInstanceIds = [];
    private bool _disposed;

    private SemanticDrawIdentityJournalV111()
    {
        _previous = Active.Value;
        Active.Value = this;
    }

    public long[] CardInstanceIds => _cardInstanceIds.ToArray();

    public static SemanticDrawIdentityJournalV111 Begin() => new();

    public static void Record(long cardInstanceId)
    {
        var current = Active.Value;
        if (current is null)
            return;
        if (cardInstanceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(cardInstanceId));
        current._cardInstanceIds.Add(cardInstanceId);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (!ReferenceEquals(Active.Value, this))
            throw new InvalidOperationException(
                "Semantic draw identity scopes must close in LIFO order.");
        Active.Value = _previous;
        _disposed = true;
    }
}
