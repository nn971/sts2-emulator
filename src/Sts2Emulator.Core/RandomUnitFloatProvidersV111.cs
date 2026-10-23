namespace Sts2Emulator.Core;

/// <summary>
/// Source-pinned unit-float request. This represents NativeRng.NextFloat()
/// before a mechanic maps the rounded float result to a discrete value.
/// </summary>
public sealed record RandomUnitFloatRequestV111(
    string SiteId,
    string StreamId,
    string OwnerId,
    int Ordinal,
    string SourceVersion = "v0.111.0")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SiteId)
            || string.IsNullOrWhiteSpace(StreamId)
            || string.IsNullOrWhiteSpace(OwnerId)
            || Ordinal < 0
            || SourceVersion != "v0.111.0")
            throw new ArgumentException("Invalid source-pinned unit-float request.");

        if (!StochasticSiteRegistryV111
                .IsRegisteredRandomUnitFloatRequestSite(SiteId))
            throw new ArgumentException(
                $"Unit-float site '{SiteId}' is not registered.",
                nameof(SiteId));
    }
}

/// <summary>
/// Resolved continuous sample. No probability law is implied by the value or
/// provider mode; native mode includes exact source cursor snapshots.
/// </summary>
public sealed record RandomUnitFloatDecisionV111(
    RandomUnitFloatRequestV111 Request,
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
            || SelectedValue < 0
            // The native double sample is below one, but rounding its result
            // to float can produce exactly 1.0f.
            || SelectedValue > 1
            || string.IsNullOrWhiteSpace(ProviderMode)
            || ProbabilityStatus != "UNVERIFIED")
            throw new ArgumentException("Illegal unit-float outcome or probability label.");
        RandomBoundaryCursorSpanV111.Validate(
            Request.StreamId, ProviderMode,
            NativeCursorBefore, NativeCursorAfter);
        if (ProviderMode == "native-v111")
        {
            var replayedValue = NativeRngV111.ReplayNextUnitFloat(
                NativeCursorBefore!, out var replayedCursorAfter);
            if (SelectedValue != replayedValue ||
                NativeCursorAfter != replayedCursorAfter)
                throw new InvalidDataException(
                    "Native unit-float decision does not match its saved cursor transition.");
        }
    }
}

/// <summary>Provider for a source unit-float boundary.</summary>
public interface IRandomUnitFloatProviderV111
{
    string Mode { get; }
    RandomUnitFloatDecisionV111 Resolve(
        RandomUnitFloatRequestV111 request, RngBundle streams);
}

/// <summary>Exact v111 NextFloat() adapter, including float rounding to 1.</summary>
public sealed class NativeRandomUnitFloatProviderV111
    : IRandomUnitFloatProviderV111
{
    public string Mode => "native-v111";

    public RandomUnitFloatDecisionV111 Resolve(
        RandomUnitFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        if (!StochasticSiteRegistryV111.IsNativeUnitFloatSite(
                request.SiteId))
            throw new NotSupportedException(
                $"Native unit-float sampling does not support site '{request.SiteId}'.");
        using var nativeBoundary =
            ControlledChanceGuardV111.AuthorizeEngineOwnedNativeBoundary(
                request.SiteId, request.StreamId, "unit_float");
        var before = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var value = NativeRngV111.NextFloat(streams, request.StreamId);
        var after = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var decision = new RandomUnitFloatDecisionV111(
            request with { }, value, Mode, before, after);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}

/// <summary>
/// Exact replay of source unit-float request identities and a supplied
/// outcome transcript. It does not touch native RNG state.
/// </summary>
public sealed class ScriptedRandomUnitFloatProviderV111
    : IRandomUnitFloatProviderV111
{
    private readonly RandomUnitFloatDecisionV111[] _script;
    private int _cursor;

    public string Mode => "scripted-v111";
    public int Remaining => _script.Length - _cursor;

    public ScriptedRandomUnitFloatProviderV111(
        IEnumerable<RandomUnitFloatDecisionV111> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = script.Select(decision =>
        {
            decision.Validate();
            return decision with { Request = decision.Request with { } };
        }).ToArray();
    }

    public RandomUnitFloatDecisionV111 Resolve(
        RandomUnitFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        if (_cursor >= _script.Length)
            throw new InvalidOperationException(
                "Scripted unit-float transcript exhausted.");
        var expected = _script[_cursor];
        if (expected.Request.SiteId != request.SiteId
            || expected.Request.StreamId != request.StreamId
            || expected.Request.OwnerId != request.OwnerId
            || expected.Request.Ordinal != request.Ordinal
            || expected.Request.SourceVersion != request.SourceVersion)
            throw new InvalidOperationException(
                "Scripted unit-float site/owner/ordinal mismatch at choice "
                + _cursor);
        var result = new RandomUnitFloatDecisionV111(
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
                "Unconsumed scripted unit-float outcomes: " + Remaining);
    }
}

/// <summary>Checked intervention at one unit-float site.</summary>
public sealed class ControlledRandomUnitFloatProviderV111(float selectedValue)
    : IRandomUnitFloatProviderV111
{
    public string Mode => "controlled-v111";

    public RandomUnitFloatDecisionV111 Resolve(
        RandomUnitFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var decision = new RandomUnitFloatDecisionV111(
            request with { }, selectedValue, Mode);
        decision.Validate();
        RandomBoundaryJournalV111.Record(decision);
        return decision;
    }
}
