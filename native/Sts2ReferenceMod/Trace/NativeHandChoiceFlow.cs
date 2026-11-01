using System.Threading;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Captures the exact PlayerChoiceSynchronizer.ReserveChoiceId return value
/// in the async execution context of CardSelectCmd.FromHand. No game state,
/// RNG, or synchronization behavior is modified. The scope is restored when
/// the hooked async method returns its Task; any already-captured continuation
/// retains its execution-context token.
/// </summary>
public static class NativeHandChoiceFlow
{
    private static readonly AsyncLocal<Scope?> Ambient = new();

    public readonly record struct Binding(
        ulong? OwnerId, uint? ChoiceId, string Status);

    public sealed class Scope : IDisposable
    {
        private readonly Scope? _parent;
        private uint? _choiceId;
        private bool _ambiguous;

        internal Scope(ulong ownerId, Scope? parent)
        {
            OwnerId = ownerId;
            _parent = parent;
        }

        public ulong OwnerId { get; }

        internal void Reserve(ulong playerId, uint choiceId)
        {
            if (playerId != OwnerId || _choiceId.HasValue)
            {
                _ambiguous = true;
                return;
            }
            _choiceId = choiceId;
        }

        public Binding Snapshot() =>
            _ambiguous
                ? new(OwnerId, null, "AMBIGUOUS_SOURCE_RESERVATION")
                : _choiceId.HasValue
                    ? new(OwnerId, _choiceId, "SOURCE_ASYNC_FLOW_RESERVED_ID")
                    : new(OwnerId, null, "CHOICE_ID_NOT_YET_OBSERVED");

        public void Dispose()
        {
            if (ReferenceEquals(Ambient.Value, this))
                Ambient.Value = _parent;
        }
    }

    public static Scope BeginFromHand(ulong ownerId)
    {
        var scope = new Scope(ownerId, Ambient.Value);
        Ambient.Value = scope;
        return scope;
    }

    public static void Reserved(ulong ownerId, uint choiceId) =>
        Ambient.Value?.Reserve(ownerId, choiceId);

    public static Binding Current() =>
        Ambient.Value?.Snapshot()
        ?? new(null, null, "UNVERIFIED");
}
