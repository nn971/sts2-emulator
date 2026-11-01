using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in native-standard-map recycle shuffle during one combat Step.
/// Source v0.111 CardPileCmd.Shuffle calls List.StableShuffle:
///   source-model/upgrade sort, then UnstableShuffle(run.shuffle).
/// CardPile draws from the front, whereas the prototype draws from the back.
/// The ordinary prototype training RNG/shuffle is unaffected.
///
/// This scope owns a fork of the source run RNG. The caller merges ONLY the
/// resulting run.shuffle stream into the next immutable RunState after Step.
/// No native post-state or observed shuffle selection is imported.
/// </summary>
public sealed class NativeSourceRecycleShuffleV111 : IDisposable
{
    private static readonly AsyncLocal<NativeSourceRecycleShuffleV111?> Active = new();

    private readonly NativeSourceRecycleShuffleV111? _parent;
    private readonly RngBundle _nativeRunRng;
    private bool _disposed;

    private NativeSourceRecycleShuffleV111(RngBundle runRng)
    {
        _nativeRunRng = runRng.Fork();
        _parent = Active.Value;
        if (_parent is not null)
            throw new InvalidOperationException(
                "Nested native recycle RNG scopes are unsupported.");
        Active.Value = this;
    }

    public static NativeSourceRecycleShuffleV111 Begin(RngBundle runRng)
    {
        ArgumentNullException.ThrowIfNull(runRng);
        return new NativeSourceRecycleShuffleV111(runRng);
    }

    public int Recycles { get; private set; }

    public RngStreamState SourceShuffleStream => _nativeRunRng.Streams.Single(
        stream => stream.StreamId == "run.shuffle");

    public static long[]? TryRecycle(CombatState combat, long[] eligible)
    {
        var scope = Active.Value;
        if (scope is null) return null;
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(eligible);
        if (eligible.Distinct().Count() != eligible.Length)
            throw new InvalidDataException("Duplicate card IDs in native recycle input.");
        var cards = combat.Cards.ToDictionary(card => card.InstanceId);
        if (eligible.Any(id => !cards.ContainsKey(id)))
            throw new InvalidDataException("Native recycle includes an unknown combat card.");

        // The native CardModel.CompareTo delegates to ModelId.CompareTo,
        // then compares upgrade level. For the Silent source starter IDs,
        // Strike/Defend require the character suffix used by native models.
        // List.Sort (as in v111 StableShuffle) preserves the source's
        // runtime tie behavior for repeated equal CardModel identities.
        var sorted = eligible.ToList();
        sorted.Sort((left, right) =>
        {
            var a = cards[left];
            var b = cards[right];
            var model = string.CompareOrdinal(
                SourceModelEntry(a.CardId), SourceModelEntry(b.CardId));
            return model != 0 ? model :
                a.UpgradeLevel.CompareTo(b.UpgradeLevel);
        });
        NativeRngV111.Shuffle(scope._nativeRunRng, "run.shuffle", sorted);
        // Source post-shuffle order is front/top-first. Prototype arrays
        // are bottom-first; invert once to preserve the same draw order.
        sorted.Reverse();
        scope.Recycles++;
        return sorted.ToArray();
    }

    private static string SourceModelEntry(string emulatorCardId)
    {
        if (string.IsNullOrWhiteSpace(emulatorCardId))
            throw new InvalidDataException("Native source card model ID is missing.");
        var entry = emulatorCardId.Split('.').Last().ToUpperInvariant();
        return entry switch
        {
            "STRIKE" => "STRIKE_SILENT",
            "DEFEND" => "DEFEND_SILENT",
            _ => entry
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (!ReferenceEquals(Active.Value, this))
            throw new InvalidOperationException(
                "Native recycle RNG scope disposed out of order.");
        Active.Value = _parent;
        _disposed = true;
    }
}
