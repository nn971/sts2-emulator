namespace Sts2Emulator.Core;

/// <summary>Request for the source-pinned ranged NextDouble operation.</summary>
public sealed record RandomRangedDoubleRequestV111(
    string SiteId,
    string StreamId,
    string OwnerId,
    int Ordinal,
    double MinInclusive,
    double MaxInclusive,
    string SourceVersion = "v0.111.0")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SiteId)
            || string.IsNullOrWhiteSpace(StreamId)
            || string.IsNullOrWhiteSpace(OwnerId)
            || Ordinal < 0
            || !double.IsFinite(MinInclusive)
            || !double.IsFinite(MaxInclusive)
            || MinInclusive > MaxInclusive
            || !double.IsFinite(MaxInclusive - MinInclusive)
            || SourceVersion != "v0.111.0")
            throw new ArgumentException(
                "Invalid source-pinned ranged-double request.");

        if (!StochasticSiteRegistryV111
                .IsRegisteredRandomRangedDoubleRequestSite(SiteId))
            throw new ArgumentException(
                $"Ranged-double site '{SiteId}' is not registered.",
                nameof(SiteId));
    }
}

/// <summary>
/// Resolved bounded double sample. The native adapter preserves the source
/// range expression and transcript; no probability law is implied.
/// </summary>
public sealed record RandomRangedDoubleDecisionV111(
    RandomRangedDoubleRequestV111 Request,
    double SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore = null,
    NativeRngSnapshot? NativeCursorAfter = null,
    string ProbabilityStatus = "UNVERIFIED")
{
    public void Validate()
    {
        Request.Validate();
        if (!double.IsFinite(SelectedValue)
            || SelectedValue < Request.MinInclusive
            || SelectedValue > Request.MaxInclusive
            || string.IsNullOrWhiteSpace(ProviderMode)
            || ProbabilityStatus != "UNVERIFIED")
            throw new ArgumentException(
                "Illegal ranged-double outcome or probability label.");
        RandomBoundaryCursorSpanV111.Validate(
            Request.StreamId, ProviderMode,
            NativeCursorBefore, NativeCursorAfter);
        if (ProviderMode == "native-v111")
        {
            var replayedValue = NativeRngV111.ReplayNextRangedDouble(
                NativeCursorBefore!, Request.MinInclusive,
                Request.MaxInclusive, out var replayedCursorAfter);
            if (SelectedValue != replayedValue ||
                NativeCursorAfter != replayedCursorAfter)
                throw new InvalidDataException(
                    "Native ranged-double decision does not match its saved cursor transition.");
        }
    }
}

public interface IRandomRangedDoubleProviderV111
{
    string Mode { get; }
    RandomRangedDoubleDecisionV111 Resolve(
        RandomRangedDoubleRequestV111 request, RngBundle streams);
}

/// <summary>Exact v111 ranged NextDouble adapter and cursor journal.</summary>
public sealed class NativeRandomRangedDoubleProviderV111
    : IRandomRangedDoubleProviderV111
{
    public string Mode => "native-v111";

    public RandomRangedDoubleDecisionV111 Resolve(
        RandomRangedDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        using var nativeBoundary =
            ControlledChanceGuardV111.AuthorizeEngineOwnedNativeBoundary(
                request.SiteId, request.StreamId, "ranged_double");
        var before = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var value = NativeRngV111.NextDouble(
            streams, request.StreamId,
            request.MinInclusive, request.MaxInclusive);
        var after = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var decision = new RandomRangedDoubleDecisionV111(
            request with { }, value, Mode, before, after);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}

/// <summary>Exact request-order replay without native cursor advancement.</summary>
public sealed class ScriptedRandomRangedDoubleProviderV111
    : IRandomRangedDoubleProviderV111
{
    private readonly RandomRangedDoubleDecisionV111[] _script;
    private int _cursor;

    public string Mode => "scripted-v111";
    public int Remaining => _script.Length - _cursor;

    public ScriptedRandomRangedDoubleProviderV111(
        IEnumerable<RandomRangedDoubleDecisionV111> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = script.Select(decision =>
        {
            decision.Validate();
            return decision with { Request = decision.Request with { } };
        }).ToArray();
    }

    public RandomRangedDoubleDecisionV111 Resolve(
        RandomRangedDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        if (_cursor >= _script.Length)
            throw new InvalidOperationException(
                "Scripted ranged-double transcript exhausted.");
        var expected = _script[_cursor];
        if (expected.Request.SiteId != request.SiteId
            || expected.Request.StreamId != request.StreamId
            || expected.Request.OwnerId != request.OwnerId
            || expected.Request.Ordinal != request.Ordinal
            || expected.Request.MinInclusive != request.MinInclusive
            || expected.Request.MaxInclusive != request.MaxInclusive
            || expected.Request.SourceVersion != request.SourceVersion)
            throw new InvalidOperationException(
                "Scripted ranged-double request mismatch at choice "
                + _cursor);
        var result = new RandomRangedDoubleDecisionV111(
            request with { }, expected.SelectedValue, Mode);
        result.Validate();
        _cursor++;
        RandomBoundaryJournalV111.Record(result);
        return result;
    }

    public void AssertComplete()
    {
        if (Remaining != 0)
            throw new InvalidOperationException(
                "Unconsumed scripted ranged-double outcomes: " + Remaining);
    }
}

/// <summary>Checked intervention at one ranged-double boundary.</summary>
public sealed class ControlledRandomRangedDoubleProviderV111(
    double selectedValue) : IRandomRangedDoubleProviderV111
{
    public string Mode => "controlled-v111";

    public RandomRangedDoubleDecisionV111 Resolve(
        RandomRangedDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var decision = new RandomRangedDoubleDecisionV111(
            request with { }, selectedValue, Mode);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}
