using System.Runtime.CompilerServices;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Reference-identity mapping from the pinned game's mutable Rng objects to
/// their persistent run/player streams. Scoped to a single observed combat.
/// Nothing here reads or advances RNG. Ephemeral context RNGs remain explicitly
/// unknown until their creation/owner hooks are source-audited.
/// </summary>
public sealed class NativeRngSourceRegistryV111
{
    private sealed record Entry(string StreamId, string OwnerId, long CallCount);
    private readonly Dictionary<object, Entry> _entries =
        new(ReferenceEqualityComparer.Instance);

    public void Register(object rng, string streamId, string ownerId)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (_entries.TryGetValue(rng, out var existing))
        {
            if (existing.StreamId != streamId || existing.OwnerId != ownerId)
                throw new InvalidOperationException(
                    "One native RNG instance has conflicting stream owners.");
            return;
        }
        _entries.Add(rng, new Entry(streamId, ownerId, 0));
    }

    public (string StreamId, string OwnerId, long CallOrdinal) Next(object rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (!_entries.TryGetValue(rng, out var e))
            return ("UNATTRIBUTED", "UNATTRIBUTED", 0);
        _entries[rng] = e with { CallCount = checked(e.CallCount + 1) };
        return (e.StreamId, e.OwnerId, e.CallCount);
    }

    public int RegisteredOwners => _entries.Count;
    public void Clear() => _entries.Clear();
}
