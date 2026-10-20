namespace Sts2Emulator.Core;

/// <summary>
/// Source-ordered, finite Fisher-Yates tickets for a live draw-pile recycle.
/// Baseline order is the exact discard list at the suspended DrawCards call.
/// Gameplay hooks and previous card draws remain in the owning RunState.
/// </summary>
public sealed record ControlledRecycleShuffleV111(
    long[] Baseline,
    long[] Shuffled,
    int NextSwapIndex,
    int FirstOrdinal,
    RandomIndexDecisionV111[] Transcript,
    RandomIndexRequestV111? Pending)
{
    private long[] _baseline = CloneRequired(Baseline, nameof(Baseline));
    private long[] _shuffled = CloneRequired(Shuffled, nameof(Shuffled));
    private RandomIndexDecisionV111[] _transcript =
        CloneTranscript(Transcript);
    private RandomIndexRequestV111? _pending = CloneRequest(Pending);

    public long[] Baseline
    {
        get => (long[])_baseline.Clone();
        init => _baseline = CloneRequired(value, nameof(Baseline));
    }

    public long[] Shuffled
    {
        get => (long[])_shuffled.Clone();
        init => _shuffled = CloneRequired(value, nameof(Shuffled));
    }

    public RandomIndexDecisionV111[] Transcript
    {
        get => CloneTranscript(_transcript);
        init => _transcript = CloneTranscript(value);
    }

    public RandomIndexRequestV111? Pending
    {
        get => CloneRequest(_pending);
        init => _pending = CloneRequest(value);
    }

    public bool Complete => Pending is null;

    public ControlledRecycleShuffleV111 Fork() => this with
    {
        Baseline = _baseline,
        Shuffled = _shuffled,
        Transcript = _transcript,
        Pending = _pending
    };

    private static long[] CloneRequired(long[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (long[])values.Clone();
    }

    private static RandomIndexDecisionV111[] CloneTranscript(
        RandomIndexDecisionV111[] decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return decisions.Select(decision => decision with
        {
            Request = CloneRequest(decision.Request)!
        }).ToArray();
    }

    private static RandomIndexRequestV111? CloneRequest(
        RandomIndexRequestV111? request) =>
        request is null
            ? null
            : request with
            {
                OrderedCandidates =
                    (int[])request.OrderedCandidates.Clone()
            };
}

public static class ControlledRecycleShuffleEngineV111
{
    public const string SiteId = "combat.draw_pile_recycle.swap_index";

    public static ControlledRecycleShuffleV111 Begin(
        long[] discardPile, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(discardPile);
        if (discardPile.Length == 0 || discardPile.Distinct().Count() != discardPile.Length ||
            ordinal < 0)
            throw new ArgumentException(
                "Recycle requires a nonempty unique eligible discard pile.");
        var stage = new ControlledRecycleShuffleV111(
            (long[])discardPile.Clone(), (long[])discardPile.Clone(),
            discardPile.Length - 1, ordinal, [], null);
        return stage with { Pending = MakePending(stage) };
    }

    public static ControlledRecycleShuffleV111 Resolve(
        ControlledRecycleShuffleV111 stage, int selectedIndex, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(streams);
        Validate(stage);
        if (stage.Pending is null)
            throw new InvalidOperationException("Recycle has no pending swap.");
        var working = stage.Fork();
        var request = working.Pending ??
            throw new InvalidOperationException("Recycle has no pending swap.");
        return ControlledChanceContinuationExecutorV111
            .ResolveSelectedIndex<ControlledRecycleShuffleV111,
                ControlledRecycleShuffleV111>(
                working, request, selectedIndex, (checkpoint, replayedIndex) =>
                {
                    var values = (long[])checkpoint.Shuffled.Clone();
                    (values[checkpoint.NextSwapIndex], values[replayedIndex]) =
                        (values[replayedIndex],
                            values[checkpoint.NextSwapIndex]);
                    var decision = new RandomIndexDecisionV111(
                        request, replayedIndex, "controlled-v111");
                    decision.Validate();
                    var next = checkpoint with
                    {
                        Shuffled = values,
                        NextSwapIndex = checkpoint.NextSwapIndex - 1,
                        Transcript = checkpoint.Transcript
                            .Append(decision).ToArray(),
                        Pending = null
                    };
                    return next with { Pending = MakePending(next) };
                });
    }

    public static void Validate(ControlledRecycleShuffleV111 stage)
    {
        if (stage.Baseline is null || stage.Shuffled is null ||
            stage.Transcript is null || stage.Baseline.Length == 0 ||
            stage.FirstOrdinal < 0 ||
            stage.Shuffled.Length != stage.Baseline.Length ||
            stage.NextSwapIndex != stage.Baseline.Length - 1 - stage.Transcript.Length)
            throw new InvalidDataException("Malformed recycle continuation.");
        var replay = Begin(stage.Baseline, stage.FirstOrdinal);
        foreach (var decision in stage.Transcript)
        {
            decision.Validate();
            if (replay.Pending is null ||
                decision.ProviderMode != "controlled-v111" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null ||
                CanonicalJson.Sha256(replay.Pending) !=
                    CanonicalJson.Sha256(decision.Request))
                throw new InvalidDataException("Recycle transcript diverged from source eligibility.");
            // Pure swap reconstruction, without invoking the journaling provider.
            var cards = (long[])replay.Shuffled.Clone();
            (cards[replay.NextSwapIndex], cards[decision.SelectedIndex]) =
                (cards[decision.SelectedIndex], cards[replay.NextSwapIndex]);
            replay = replay with
            {
                Shuffled = cards,
                NextSwapIndex = replay.NextSwapIndex - 1,
                Transcript = replay.Transcript.Append(decision).ToArray()
            };
            replay = replay with { Pending = MakePending(replay) };
        }
        if (CanonicalJson.Sha256(replay) != CanonicalJson.Sha256(stage))
            throw new InvalidDataException("Recycle continuation is not source-reachable.");
    }

    private static RandomIndexRequestV111? MakePending(
        ControlledRecycleShuffleV111 state) =>
        state.NextSwapIndex < 1 ? null : new RandomIndexRequestV111(
            SiteId, "run.shuffle", "player:0",
            state.FirstOrdinal + state.Transcript.Length,
            [], OperationKind: "bounded_integer",
            BoundedCount: state.NextSwapIndex + 1);
}
