namespace Sts2Emulator.Core;

/// <summary>Request for the source-pinned Gaussian-double sample.</summary>
public sealed record RandomGaussianDoubleRequestV111(
    string SiteId,
    string StreamId,
    string OwnerId,
    int Ordinal,
    double Mean = 0,
    double StandardDeviation = 1,
    double MinInclusive = 0,
    double MaxInclusive = 1,
    string SourceVersion = "v0.111.0")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SiteId)
            || string.IsNullOrWhiteSpace(StreamId)
            || string.IsNullOrWhiteSpace(OwnerId)
            || Ordinal < 0
            || !double.IsFinite(Mean)
            || Mean is < 0 or > 1
            || !double.IsFinite(StandardDeviation)
            || !double.IsFinite(MinInclusive)
            || !double.IsFinite(MaxInclusive)
            || MinInclusive > MaxInclusive
            || !double.IsFinite(MaxInclusive - MinInclusive)
            || SourceVersion != "v0.111.0")
            throw new ArgumentException(
                "Invalid source-pinned Gaussian-double request.");

        if (!StochasticSiteRegistryV111
                .IsRegisteredRandomGaussianDoubleRequestSite(SiteId))
            throw new ArgumentException(
                $"Gaussian-double site '{SiteId}' is not registered.",
                nameof(SiteId));
    }
}

/// <summary>
/// Resolved result for the source Gaussian-double operation. Native mode
/// records the full cursor span, including any rejected Box-Muller samples.
/// </summary>
public sealed record RandomGaussianDoubleDecisionV111(
    RandomGaussianDoubleRequestV111 Request,
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
                "Illegal Gaussian-double outcome or probability label.");
        RandomBoundaryCursorSpanV111.Validate(
            Request.StreamId, ProviderMode,
            NativeCursorBefore, NativeCursorAfter);
        if (ProviderMode == "native-v111")
        {
            var replayedValue = NativeRngV111.ReplayNextGaussianDouble(
                NativeCursorBefore!, Request.Mean,
                Request.StandardDeviation, Request.MinInclusive,
                Request.MaxInclusive, out var replayedCursorAfter);
            if (SelectedValue != replayedValue ||
                NativeCursorAfter != replayedCursorAfter)
                throw new InvalidDataException(
                    "Native Gaussian-double decision does not match its saved cursor transition.");
        }
    }
}

public interface IRandomGaussianDoubleProviderV111
{
    string Mode { get; }
    RandomGaussianDoubleDecisionV111 Resolve(
        RandomGaussianDoubleRequestV111 request, RngBundle streams);
}

/// <summary>Exact v111 Gaussian-double adapter, including rejection draws.</summary>
public sealed class NativeRandomGaussianDoubleProviderV111
    : IRandomGaussianDoubleProviderV111
{
    public string Mode => "native-v111";

    public RandomGaussianDoubleDecisionV111 Resolve(
        RandomGaussianDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        using var nativeBoundary =
            ControlledChanceGuardV111.AuthorizeEngineOwnedNativeBoundary(
                request.SiteId, request.StreamId, "gaussian_double");
        var before = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var value = NativeRngV111.NextGaussianDouble(
            streams, request.StreamId, request.Mean,
            request.StandardDeviation, request.MinInclusive,
            request.MaxInclusive);
        var after = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var decision = new RandomGaussianDoubleDecisionV111(
            request with { }, value, Mode, before, after);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}

/// <summary>Exact request-order replay without native cursor advancement.</summary>
public sealed class ScriptedRandomGaussianDoubleProviderV111
    : IRandomGaussianDoubleProviderV111
{
    private readonly RandomGaussianDoubleDecisionV111[] _script;
    private int _cursor;

    public string Mode => "scripted-v111";
    public int Remaining => _script.Length - _cursor;

    public ScriptedRandomGaussianDoubleProviderV111(
        IEnumerable<RandomGaussianDoubleDecisionV111> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = script.Select(decision =>
        {
            decision.Validate();
            return decision with { Request = decision.Request with { } };
        }).ToArray();
    }

    public RandomGaussianDoubleDecisionV111 Resolve(
        RandomGaussianDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        if (_cursor >= _script.Length)
            throw new InvalidOperationException(
                "Scripted Gaussian-double transcript exhausted.");
        var expected = _script[_cursor];
        if (expected.Request.SiteId != request.SiteId
            || expected.Request.StreamId != request.StreamId
            || expected.Request.OwnerId != request.OwnerId
            || expected.Request.Ordinal != request.Ordinal
            || expected.Request.Mean != request.Mean
            || expected.Request.StandardDeviation != request.StandardDeviation
            || expected.Request.MinInclusive != request.MinInclusive
            || expected.Request.MaxInclusive != request.MaxInclusive
            || expected.Request.SourceVersion != request.SourceVersion)
            throw new InvalidOperationException(
                "Scripted Gaussian-double request mismatch at choice "
                + _cursor);
        var result = new RandomGaussianDoubleDecisionV111(
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
                "Unconsumed scripted Gaussian-double outcomes: " + Remaining);
    }
}

/// <summary>Checked intervention at one Gaussian-double boundary.</summary>
public sealed class ControlledRandomGaussianDoubleProviderV111(
    double selectedValue) : IRandomGaussianDoubleProviderV111
{
    public string Mode => "controlled-v111";

    public RandomGaussianDoubleDecisionV111 Resolve(
        RandomGaussianDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var decision = new RandomGaussianDoubleDecisionV111(
            request with { }, selectedValue, Mode);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}
