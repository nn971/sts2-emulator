namespace Sts2Emulator.Core;

/// <summary>Request for the source-pinned ranged NextFloat operation.</summary>
public sealed record RandomRangedFloatRequestV111(
    string SiteId,
    string StreamId,
    string OwnerId,
    int Ordinal,
    float MinInclusive,
    float MaxInclusive,
    string SourceVersion = "v0.111.0")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SiteId)
            || string.IsNullOrWhiteSpace(StreamId)
            || string.IsNullOrWhiteSpace(OwnerId)
            || Ordinal < 0
            || !float.IsFinite(MinInclusive)
            || !float.IsFinite(MaxInclusive)
            || MinInclusive > MaxInclusive
            || !float.IsFinite(MaxInclusive - MinInclusive)
            || SourceVersion != "v0.111.0")
            throw new ArgumentException(
                "Invalid source-pinned ranged-float request.");

        if (!StochasticSiteRegistryV111
                .IsRegisteredRandomRangedFloatRequestSite(SiteId))
            throw new ArgumentException(
                $"Ranged-float site '{SiteId}' is not registered.",
                nameof(SiteId));
    }
}

/// <summary>
/// Resolved ranged sample. The native adapter preserves native float rounding,
/// and the transcript makes no probability-law claim.
/// </summary>
public sealed record RandomRangedFloatDecisionV111(
    RandomRangedFloatRequestV111 Request,
    float SelectedValue,
    string ProviderMode,
    NativeRngSnapshot? NativeCursorBefore = null,
    NativeRngSnapshot? NativeCursorAfter = null,
    string ProbabilityStatus = "UNVERIFIED")
{
    public void Validate()
    {
        Request.Validate();
        if (!float.IsFinite(SelectedValue)
            || SelectedValue < Request.MinInclusive
            || SelectedValue > Request.MaxInclusive
            || string.IsNullOrWhiteSpace(ProviderMode)
            || ProbabilityStatus != "UNVERIFIED")
            throw new ArgumentException(
                "Illegal ranged-float outcome or probability label.");
        RandomBoundaryCursorSpanV111.Validate(
            Request.StreamId, ProviderMode,
            NativeCursorBefore, NativeCursorAfter);
        if (ProviderMode == "native-v111")
        {
            var replayedValue = NativeRngV111.ReplayNextRangedFloat(
                NativeCursorBefore!, Request.MinInclusive,
                Request.MaxInclusive, out var replayedCursorAfter);
            if (SelectedValue != replayedValue ||
                NativeCursorAfter != replayedCursorAfter)
                throw new InvalidDataException(
                    "Native ranged-float decision does not match its saved cursor transition.");
        }
    }
}

public interface IRandomRangedFloatProviderV111
{
    string Mode { get; }
    RandomRangedFloatDecisionV111 Resolve(
        RandomRangedFloatRequestV111 request, RngBundle streams);
}

/// <summary>Exact v111 ranged NextFloat adapter and cursor journal.</summary>
public sealed class NativeRandomRangedFloatProviderV111
    : IRandomRangedFloatProviderV111
{
    public string Mode => "native-v111";

    public RandomRangedFloatDecisionV111 Resolve(
        RandomRangedFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        using var nativeBoundary =
            ControlledChanceGuardV111.AuthorizeEngineOwnedNativeBoundary(
                request.SiteId, request.StreamId, "ranged_float");
        var before = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var value = NativeRngV111.NextFloat(
            streams, request.StreamId,
            request.MinInclusive, request.MaxInclusive);
        var after = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var decision = new RandomRangedFloatDecisionV111(
            request with { }, value, Mode, before, after);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}

/// <summary>Exact request-order replay without native cursor advancement.</summary>
public sealed class ScriptedRandomRangedFloatProviderV111
    : IRandomRangedFloatProviderV111
{
    private readonly RandomRangedFloatDecisionV111[] _script;
    private int _cursor;

    public string Mode => "scripted-v111";
    public int Remaining => _script.Length - _cursor;

    public ScriptedRandomRangedFloatProviderV111(
        IEnumerable<RandomRangedFloatDecisionV111> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = script.Select(decision =>
        {
            decision.Validate();
            return decision with { Request = decision.Request with { } };
        }).ToArray();
    }

    public RandomRangedFloatDecisionV111 Resolve(
        RandomRangedFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        if (_cursor >= _script.Length)
            throw new InvalidOperationException(
                "Scripted ranged-float transcript exhausted.");
        var expected = _script[_cursor];
        if (expected.Request.SiteId != request.SiteId
            || expected.Request.StreamId != request.StreamId
            || expected.Request.OwnerId != request.OwnerId
            || expected.Request.Ordinal != request.Ordinal
            || expected.Request.MinInclusive != request.MinInclusive
            || expected.Request.MaxInclusive != request.MaxInclusive
            || expected.Request.SourceVersion != request.SourceVersion)
            throw new InvalidOperationException(
                "Scripted ranged-float request mismatch at choice "
                + _cursor);
        var result = new RandomRangedFloatDecisionV111(
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
                "Unconsumed scripted ranged-float outcomes: " + Remaining);
    }
}

/// <summary>Checked intervention at one ranged-float boundary.</summary>
public sealed class ControlledRandomRangedFloatProviderV111(
    float selectedValue) : IRandomRangedFloatProviderV111
{
    public string Mode => "controlled-v111";

    public RandomRangedFloatDecisionV111 Resolve(
        RandomRangedFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var decision = new RandomRangedFloatDecisionV111(
            request with { }, selectedValue, Mode);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}
