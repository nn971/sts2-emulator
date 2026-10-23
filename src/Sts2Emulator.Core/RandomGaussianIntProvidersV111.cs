namespace Sts2Emulator.Core;

/// <summary>Request for a source-pinned bounded Gaussian integer sample.</summary>
public sealed record RandomGaussianIntRequestV111(
    string SiteId,
    string StreamId,
    string OwnerId,
    int Ordinal,
    int Mean,
    int StandardDeviation,
    int MinInclusive,
    int MaxInclusive,
    string SourceVersion = "v0.111.0")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SiteId)
            || string.IsNullOrWhiteSpace(StreamId)
            || string.IsNullOrWhiteSpace(OwnerId)
            || Ordinal < 0
            || StandardDeviation < 0
            || MinInclusive > MaxInclusive
            || Mean < MinInclusive || Mean > MaxInclusive
            || SourceVersion != "v0.111.0")
            throw new ArgumentException(
                "Invalid source-pinned Gaussian-integer request.");

        if (!StochasticSiteRegistryV111
                .IsRegisteredRandomGaussianIntRequestSite(SiteId))
            throw new ArgumentException(
                $"Gaussian-integer site '{SiteId}' is not registered.",
                nameof(SiteId));
    }
}

/// <summary>
/// Resolved inclusive Gaussian-integer result. The decision records the
/// request and cursor span but makes no probability-law claim.
/// </summary>
public sealed record RandomGaussianIntDecisionV111(
    RandomGaussianIntRequestV111 Request,
    int SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore = null,
    NativeRngSnapshot? NativeCursorAfter = null,
    string ProbabilityStatus = "UNVERIFIED")
{
    public void Validate()
    {
        Request.Validate();
        if (SelectedValue < Request.MinInclusive
            || SelectedValue > Request.MaxInclusive
            || string.IsNullOrWhiteSpace(ProviderMode)
            || ProbabilityStatus != "UNVERIFIED")
            throw new ArgumentException(
                "Illegal Gaussian-integer outcome or probability label.");
        RandomBoundaryCursorSpanV111.Validate(
            Request.StreamId, ProviderMode,
            NativeCursorBefore, NativeCursorAfter);
        if (ProviderMode == "native-v111")
        {
            if (!ChanceLawEngineV111.MatchesNativeMapGaussianIntRequest(
                    Request))
                throw new InvalidDataException(
                    "Native Gaussian-integer decision has an unsupported request shape.");
            var replayedValue = NativeRngV111.ReplayNextGaussianInt(
                NativeCursorBefore!, Request.Mean,
                Request.StandardDeviation, Request.MinInclusive,
                Request.MaxInclusive, out var replayedCursorAfter);
            if (SelectedValue != replayedValue ||
                NativeCursorAfter != replayedCursorAfter)
                throw new InvalidDataException(
                    "Native Gaussian-integer decision does not match its saved cursor transition.");
        }
    }
}

public interface IRandomGaussianIntProviderV111
{
    string Mode { get; }
    RandomGaussianIntDecisionV111 Resolve(
        RandomGaussianIntRequestV111 request, RngBundle streams);
}

/// <summary>Exact v111 bounded Gaussian-integer sampler and cursor adapter.</summary>
public sealed class NativeRandomGaussianIntProviderV111
    : IRandomGaussianIntProviderV111
{
    public string Mode => "native-v111";

    public RandomGaussianIntDecisionV111 Resolve(
        RandomGaussianIntRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        if (!ChanceLawEngineV111.IsNativeMapGaussianIntQuerySite(
                request.SiteId))
            throw new NotSupportedException(
                $"Native Gaussian-integer sampling does not support site '{request.SiteId}'.");
        if (!ChanceLawEngineV111.MatchesNativeMapGaussianIntRequest(request))
            throw new ArgumentException(
                "Native Gaussian-integer sampling requires a registered source request shape.",
                nameof(request));
        using var nativeBoundary =
            ControlledChanceGuardV111.AuthorizeEngineOwnedNativeBoundary(
                request.SiteId, request.StreamId, "gaussian_int");
        var before = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var value = NativeRngV111.NextGaussianInt(
            streams, request.StreamId, request.Mean,
            request.StandardDeviation, request.MinInclusive,
            request.MaxInclusive);
        var after = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var decision = new RandomGaussianIntDecisionV111(
            request with { }, value, Mode, before, after);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}

/// <summary>Exact request-order replay without native cursor advancement.</summary>
public sealed class ScriptedRandomGaussianIntProviderV111
    : IRandomGaussianIntProviderV111
{
    private readonly RandomGaussianIntDecisionV111[] _script;
    private int _cursor;

    public string Mode => "scripted-v111";
    public int Remaining => _script.Length - _cursor;

    public ScriptedRandomGaussianIntProviderV111(
        IEnumerable<RandomGaussianIntDecisionV111> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = script.Select(decision =>
        {
            decision.Validate();
            return decision with { Request = decision.Request with { } };
        }).ToArray();
    }

    public RandomGaussianIntDecisionV111 Resolve(
        RandomGaussianIntRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        if (_cursor >= _script.Length)
            throw new InvalidOperationException(
                "Scripted Gaussian-integer transcript exhausted.");
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
                "Scripted Gaussian-integer request mismatch at choice "
                + _cursor);
        var result = new RandomGaussianIntDecisionV111(
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
                "Unconsumed scripted Gaussian-integer outcomes: " + Remaining);
    }
}

/// <summary>Checked intervention at a bounded Gaussian-integer boundary.</summary>
public sealed class ControlledRandomGaussianIntProviderV111(int selectedValue)
    : IRandomGaussianIntProviderV111
{
    public string Mode => "controlled-v111";

    public RandomGaussianIntDecisionV111 Resolve(
        RandomGaussianIntRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var decision = new RandomGaussianIntDecisionV111(
            request with { }, selectedValue, Mode);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}
