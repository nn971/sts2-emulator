using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Explicit, independently identified shuffle from a pinned v0.111 source
/// sidecar, mapped to persistent IDs in the *emulator's own pre-state*.
/// The mapping and source-ordered list are verified by the caller; the
/// sequenced choices are immutable data and never read native post-state
/// as a substitute for the engine's computed state.
/// </summary>
public sealed record NativeShuffleStepExpectationV111(
    string Kind,
    long[] OrderedSourceInput,
    long[] NativePreHookOutput,
    long[] NativePostHookOutput,
    int[] SourceSelectedIndices,
    string EvidenceOrigin = "native_source_shuffle",
    string CardIdentityStatus = "INDEPENDENTLY_MAPPED",
    string SiteId = NativeFisherYatesV111.SiteId)
{
    public void Validate()
    {
        if (Kind != "recycle" ||
            EvidenceOrigin != "native_source_shuffle" ||
            CardIdentityStatus != "INDEPENDENTLY_MAPPED" ||
            SiteId != NativeFisherYatesV111.SiteId ||
            OrderedSourceInput is null ||
            NativePreHookOutput is null ||
            NativePostHookOutput is null ||
            SourceSelectedIndices is null ||
            OrderedSourceInput.Length != NativePreHookOutput.Length ||
            OrderedSourceInput.Length != NativePostHookOutput.Length ||
            SourceSelectedIndices.Length !=
                Math.Max(0, OrderedSourceInput.Length - 1))
            throw new ArgumentException("Source shuffle evidence must have verified identity and stage lengths.");
        long[] ordered = OrderedSourceInput;
        if (ordered.Distinct().Count() != ordered.Length ||
            !NativePreHookOutput.Order().SequenceEqual(ordered.Order()) ||
            !NativePostHookOutput.Order().SequenceEqual(ordered.Order()))
            throw new ArgumentException("Source shuffle card identity multiset is invalid.");
        long[] actual = (long[])ordered.Clone();
        for (int i = 0; i < SourceSelectedIndices.Length; ++i)
        {
            int bound = actual.Length - i;
            int selected = SourceSelectedIndices[i];
            if (selected < 0 || selected >= bound)
                throw new ArgumentException("Source shuffle selected index is out of bounds.");
            (actual[bound - 1], actual[selected]) =
                (actual[selected], actual[bound - 1]);
        }
        if (!actual.SequenceEqual(NativePreHookOutput))
            throw new ArgumentException("Source RNG selections disagree with the claimed pre-hook shuffle order.");
    }

    public NativeShuffleStepExpectationV111 Fork() => this with
    {
        OrderedSourceInput = (long[])OrderedSourceInput.Clone(),
        NativePreHookOutput = (long[])NativePreHookOutput.Clone(),
        NativePostHookOutput = (long[])NativePostHookOutput.Clone(),
        SourceSelectedIndices = (int[])SourceSelectedIndices.Clone()
    };
}

public sealed record NativeShuffleStepObservationV111(
    int Sequence, long[] EngineEligibleInput, long[] SourceOrderedInput,
    long[] EnginePreHookOrder, long[] EnginePostHookOrder,
    string Status, string? FirstMismatchStage);

/// <summary>
/// Opt-in diagnostic context around the ACTUAL PrototypeGameEngine.Step.
/// The original engine's shuffle sites invoke this boundary only when a
/// replay scope exists. Without it, the legacy training shuffle is unchanged.
/// Native outputs are solely assertions, never copied into the engine's
/// post-hook state. Scripted choices do not advance fake/native RNG cursors.
/// </summary>
public sealed class NativeShuffleStepReplayV111 : IDisposable
{
    private static readonly AsyncLocal<NativeShuffleStepReplayV111?> Active = new();
    private readonly NativeShuffleStepReplayV111? _parent;
    private readonly NativeShuffleStepExpectationV111[] _script;
    private readonly List<NativeShuffleStepObservationV111> _events = [];
    private readonly RngBundle _replayStreams;
    private int _cursor;
    private bool _disposed;
    private bool _pendingPostHook;
    private bool _eligibilityProbe;
    private readonly List<long[]> _probedEligibleInputs = [];

    private NativeShuffleStepReplayV111(
        IEnumerable<NativeShuffleStepExpectationV111> script, RngBundle nativeStreams)
    {
        _script = script.Select(x => { x.Validate(); return x.Fork(); }).ToArray();
        _replayStreams = nativeStreams.Fork();
        _parent = Active.Value;
        Active.Value = this;
    }

    public static NativeShuffleStepReplayV111 Begin(
        IEnumerable<NativeShuffleStepExpectationV111> script,
        RngBundle nativeStreams)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(nativeStreams);
        return new NativeShuffleStepReplayV111(script, nativeStreams);
    }

    /// <summary>
    /// Diagnostic-only eligible-card census for constructing SYNTHETIC
    /// engine tests. It is NOT independently observed native source evidence
    /// and cannot be passed off as native shuffle parity.
    /// </summary>
    public IReadOnlyList<long[]> ProbedEligibleInputs => _probedEligibleInputs;
    public IReadOnlyList<NativeShuffleStepObservationV111> Observations => _events;
    public int Remaining => _script.Length - _cursor;

    public static NativeShuffleStepReplayV111 BeginEngineEligibilityProbe()
    {
        var scope = new NativeShuffleStepReplayV111(
            [], PrototypeRng.CreateBundle("diagnostic-source-census"));
        scope._eligibilityProbe = true;
        return scope;
    }
    public string Status => _events.FirstOrDefault(x =>
        x.Status != "SOURCE_SHUFFLE_STAGE_CONSISTENT")?.Status ??
        (Remaining > 0 || _pendingPostHook ? "REPLAY_INCOMPLETE"
            : "SOURCE_SHUFFLE_STAGES_CONSISTENT");

    /// <summary>
    /// Perform the exact source-shaped swap on source-ordered card IDs inside
    /// a real Step, after checking the available engine card IDs match the
    /// independently supplied native-to-emulator mapping.
    /// </summary>
    public static long[] ShuffleRecycle(
        long[] engineEligibleInput, RngBundle engineRng)
    {
        var scope = Active.Value;
        if (scope is null)
        {
            var output = (long[])engineEligibleInput.Clone();
            PrototypeRng.Shuffle(engineRng, "combat", output);
            return output;
        }
        if (scope._eligibilityProbe)
        {
            scope._probedEligibleInputs.Add((long[])engineEligibleInput.Clone());
            var unchanged = (long[])engineEligibleInput.Clone();
            PrototypeRng.Shuffle(engineRng, "combat", unchanged);
            return unchanged;
        }
        if (scope._pendingPostHook)
            throw new InvalidOperationException("Nested source shuffle before preceding post-hook verification.");
        if (scope._cursor >= scope._script.Length)
            throw new InvalidOperationException("Unexpected source shuffle in actual engine Step.");
        var expected = scope._script[scope._cursor];
        long[] input = (long[])engineEligibleInput.Clone();
        if (!input.Order().SequenceEqual(expected.OrderedSourceInput.Order()))
            throw new InvalidOperationException(
                "SOURCE_SHUFFLE_ELIGIBLE_CARD_IDENTITIES_MISMATCH: engine=[" +
                string.Join(",", input) + "], source=[" +
                string.Join(",", expected.OrderedSourceInput) + "].");

        // Native replay uses the source's explicitly verified ordering.
        // The input never comes from an observed *after* state.
        var scripted = expected.SourceSelectedIndices.Select((index, i) =>
            new RandomIndexDecisionV111(
                new RandomIndexRequestV111(
                    NativeFisherYatesV111.SiteId,
                    NativeFisherYatesV111.StreamId,
                    "player:0", i, [], OperationKind: "bounded_integer",
                    BoundedCount: input.Length - i),
                index, "source-transcript-v111")).ToArray();
        var provider = new ScriptedRandomIndexProviderV111(scripted);
        var computed = NativeFisherYatesV111.Execute(
            expected.OrderedSourceInput, scope._replayStreams, provider,
            siteId: NativeFisherYatesV111.SiteId);
        provider.AssertComplete();
        int sequence = ++scope._cursor;
        scope._pendingPostHook = true;
        scope._events.Add(new NativeShuffleStepObservationV111(
            sequence, input, (long[])expected.OrderedSourceInput.Clone(),
            computed.PreHookOutput, [],
            computed.PreHookOutput.SequenceEqual(expected.NativePreHookOutput)
                ? "AWAITING_POST_HOOK" : "SOURCE_SHUFFLE_PRE_HOOK_MISMATCH",
            computed.PreHookOutput.SequenceEqual(expected.NativePreHookOutput)
                ? null : "pre_hook_order"));
        return computed.PreHookOutput;
    }

    /// <summary>
    /// Observe the ORIGINAL engine ApplyPerfectFitShuffleOrder output,
    /// compare it to the independently captured native after-hook order,
    /// then return the unmodified result back to the engine.
    /// </summary>
    public static long[] CheckPostHook(long[] actualEngineOrder)
    {
        var scope = Active.Value;
        if (scope is null || scope._eligibilityProbe) return actualEngineOrder;
        if (!scope._pendingPostHook || scope._events.Count == 0)
            throw new InvalidOperationException("Source shuffle post-hook callback without pending source shuffle.");
        int index = scope._events.Count - 1;
        var prior = scope._events[index];
        var expected = scope._script[prior.Sequence - 1];
        string? mismatch = prior.FirstMismatchStage;
        if (mismatch is null &&
            !actualEngineOrder.SequenceEqual(expected.NativePostHookOutput))
            mismatch = "post_hook_order";
        string status = mismatch is null
            ? "SOURCE_SHUFFLE_STAGE_CONSISTENT"
            : mismatch == "pre_hook_order"
                ? "SOURCE_SHUFFLE_PRE_HOOK_MISMATCH"
                : "SOURCE_SHUFFLE_POST_HOOK_MISMATCH";
        scope._events[index] = prior with
        {
            EnginePostHookOrder = (long[])actualEngineOrder.Clone(),
            Status = status,
            FirstMismatchStage = mismatch
        };
        scope._pendingPostHook = false;
        return actualEngineOrder;
    }

    public void AssertComplete()
    {
        if (_pendingPostHook || _cursor != _script.Length)
            throw new InvalidOperationException(
                "Native shuffle Step transcript not completely consumed.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (!ReferenceEquals(Active.Value, this))
            throw new InvalidOperationException("Native shuffle replay scope disposed out of order.");
        Active.Value = _parent;
        _disposed = true;
    }
}
