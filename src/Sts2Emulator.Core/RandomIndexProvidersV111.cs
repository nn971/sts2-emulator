using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Source-authentic, ordered finite index selection. Candidate indices (not
/// candidate values) are the RNG outcomes: duplicate values remain distinguishable.
/// This first D1 slice supports ordered finite pools only, not continuous draws.
/// </summary>
public sealed record RandomIndexRequestV111(
    string SiteId, string StreamId, string OwnerId,
    int Ordinal, int[] OrderedCandidates,
    string SourceVersion = "v0.111.0",
    string OperationKind = "choose_index",
    int? BoundedCount = null)
{
    // A bounded integer ticket is compact even for very large ranges.
    [JsonIgnore]
    public int SupportCount => OperationKind == "bounded_integer"
        ? BoundedCount ?? 0 : OrderedCandidates.Length;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SiteId)
            || string.IsNullOrWhiteSpace(StreamId)
            || string.IsNullOrWhiteSpace(OwnerId)
            || SourceVersion != "v0.111.0"
            || OperationKind is not ("choose_index" or "bounded_integer")
            || Ordinal < 0 || OrderedCandidates is null
            || (OperationKind == "choose_index" &&
                (OrderedCandidates.Length == 0 || BoundedCount is not null))
            || (OperationKind == "bounded_integer" &&
                (OrderedCandidates.Length != 0 || BoundedCount is null or <= 0)))
            throw new ArgumentException("Invalid source-pinned random-choice request.");
    }
}

/// <summary>
/// Explicit outcome provenance. The transcript contains no claim about the
/// original conditional probability of the outcome.
/// </summary>
public sealed record RandomIndexDecisionV111(
    RandomIndexRequestV111 Request,
    int SelectedIndex,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore = null,
    NativeRngSnapshot? NativeCursorAfter = null,
    string ProbabilityStatus = "UNVERIFIED")
{
    [JsonIgnore]
    public int SelectedValue => Request.OperationKind == "bounded_integer"
        ? SelectedIndex : Request.OrderedCandidates[SelectedIndex];

    public void Validate()
    {
        Request.Validate();
        if (SelectedIndex < 0 || SelectedIndex >= Request.SupportCount ||
            string.IsNullOrWhiteSpace(ProviderMode) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new ArgumentException("Illegal random-choice outcome or probability label.");
    }
}

/// <summary>
/// Injected outcome selection only. The mechanics own the call order and
/// eligibility; providers never execute game hooks or modify non-RNG state.
/// </summary>
public interface IRandomIndexProviderV111
{
    string Mode { get; }
    RandomIndexDecisionV111 Resolve(RandomIndexRequestV111 request, RngBundle streams);
}

/// <summary>
/// Uses the original v111 native NextInt algorithm and advances precisely the
/// named stream, including a call with a singleton candidate pool.
/// </summary>
public sealed class NativeRandomIndexProviderV111 : IRandomIndexProviderV111
{
    public string Mode => "native-v111";

    public RandomIndexDecisionV111 Resolve(RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var matches = streams.Streams.Where(s => s.StreamId == request.StreamId).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("Native random stream ownership is ambiguous.");
        var before = NativeRngV111.ToSnapshot(matches[0]);
        int index = NativeRngV111.NextInt(streams, request.StreamId, request.SupportCount);
        var after = NativeRngV111.ToSnapshot(streams.Streams.Single(s =>
            s.StreamId == request.StreamId));
        var decision = new RandomIndexDecisionV111(
            request with { OrderedCandidates = (int[])request.OrderedCandidates.Clone() },
            index, Mode, before, after);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}

/// <summary>
/// Typed adapter for the retained legacy six-stream prototype profile. It
/// preserves PrototypeRng's modulo sampler and stream state; it does not
/// claim native-v111 RNG parity. Use the provider mode in transcripts to keep
/// these outcomes distinct from native-v111 and counterfactual Nature.
/// </summary>
public sealed class LegacyPrototypeRandomIndexProviderV111
    : IRandomIndexProviderV111
{
    public string Mode => "legacy-prototype-v0";

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var matches = streams.Streams
            .Where(stream => stream.StreamId == request.StreamId).ToArray();
        if (matches.Length != 1 ||
            matches[0].Codec != PrototypeRng.Codec ||
            matches[0].StateBytes.Length != sizeof(ulong))
            throw new InvalidOperationException(
                "Legacy prototype random stream ownership or codec is invalid.");

        var selectedIndex = PrototypeRng.NextInt(
            streams, request.StreamId, request.SupportCount);
        var decision = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates =
                    (int[])request.OrderedCandidates.Clone()
            },
            selectedIndex, Mode);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}

/// <summary>
/// Exact replay of a *specified outcome transcript*. Does not touch native
/// cursors: replayed Nature choices are counterfactual, not seeded-native RNG.
/// The caller must explicitly assert all choices were consumed.
/// </summary>
public sealed class ScriptedRandomIndexProviderV111 : IRandomIndexProviderV111
{
    private readonly RandomIndexDecisionV111[] _script;
    private int _cursor;
    public string Mode => "scripted-v111";
    public int Remaining => _script.Length - _cursor;

    public ScriptedRandomIndexProviderV111(IEnumerable<RandomIndexDecisionV111> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = script.Select(x =>
        {
            x.Validate();
            return x with { Request = x.Request with
                { OrderedCandidates = (int[])x.Request.OrderedCandidates.Clone() } };
        }).ToArray();
    }

    public RandomIndexDecisionV111 Resolve(RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        if (_cursor >= _script.Length)
            throw new InvalidOperationException("Scripted Nature transcript exhausted.");
        var expected = _script[_cursor];
        if (expected.Request.SiteId != request.SiteId ||
            expected.Request.StreamId != request.StreamId ||
            expected.Request.OwnerId != request.OwnerId ||
            expected.Request.Ordinal != request.Ordinal ||
            expected.Request.SourceVersion != request.SourceVersion ||
            expected.Request.OperationKind != request.OperationKind ||
            expected.Request.BoundedCount != request.BoundedCount ||
            !expected.Request.OrderedCandidates.SequenceEqual(request.OrderedCandidates))
            throw new InvalidOperationException(
                "Scripted Nature site/owner/ordinal/eligibility mismatch at choice " + _cursor);
        var result = new RandomIndexDecisionV111(
            request with { OrderedCandidates = (int[])request.OrderedCandidates.Clone() },
            expected.SelectedIndex, Mode);
        result.Validate();
        _cursor++;
        RandomBoundaryJournalV111.Record(result);
        return result;
    }

    public void AssertComplete()
    {
        if (Remaining != 0)
            throw new InvalidOperationException(
                "Unconsumed scripted Nature outcomes: " + Remaining);
    }
}

/// <summary>
/// Stateless checked Nature selection at one pending site, without an RNG draw.
/// This provider is a building block, NOT the serializable D3 continuation
/// controller; it must not be used to claim a native-seeded combat state.
/// </summary>
public sealed class ControlledRandomIndexProviderV111(int selectedIndex)
    : IRandomIndexProviderV111
{
    public string Mode => "controlled-v111";
    public RandomIndexDecisionV111 Resolve(RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var outcome = new RandomIndexDecisionV111(
            request with { OrderedCandidates = (int[])request.OrderedCandidates.Clone() },
            selectedIndex, Mode);
        outcome.Validate();
        RandomBoundaryJournalV111.Record(outcome);
        return outcome;
    }
}
