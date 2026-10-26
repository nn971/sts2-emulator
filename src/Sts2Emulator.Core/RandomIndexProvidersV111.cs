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
    private int[] _orderedCandidates =
        CloneRequired(OrderedCandidates, nameof(OrderedCandidates));
    private string[]? _candidateLabels;

    public int[] OrderedCandidates
    {
        get => (int[])_orderedCandidates.Clone();
        init => _orderedCandidates = CloneRequired(
            value, nameof(OrderedCandidates));
    }

    /// <summary>
    /// Optional semantic identities aligned with the index support. Labels
    /// describe outcomes without changing the sampled index or request owner.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? CandidateLabels
    {
        get => _candidateLabels is null
            ? null : (string[])_candidateLabels.Clone();
        init => _candidateLabels = value is null
            ? null : (string[])value.Clone();
    }

    // A bounded integer ticket is compact even for very large ranges.
    [JsonIgnore]
    public int SupportCount => OperationKind == "bounded_integer"
        ? BoundedCount ?? 0 : _orderedCandidates.Length;

    public void Validate()
    {
        ValidateShape();

        if (!StochasticSiteRegistryV111
                .IsRegisteredRandomIndexRequestSite(SiteId))
            throw new ArgumentException(
                $"Random-choice site '{SiteId}' is not registered.",
                nameof(SiteId));
    }

    private void ValidateShape()
    {
        if (string.IsNullOrWhiteSpace(SiteId)
            || string.IsNullOrWhiteSpace(StreamId)
            || string.IsNullOrWhiteSpace(OwnerId)
            || SourceVersion != "v0.111.0"
            || OperationKind is not ("choose_index" or "bounded_integer")
            || Ordinal < 0
            || (OperationKind == "choose_index" &&
                (_orderedCandidates.Length == 0 || BoundedCount is not null))
            || (OperationKind == "bounded_integer" &&
                (_orderedCandidates.Length != 0 || BoundedCount is null or <= 0)) ||
            (_candidateLabels is not null &&
                (_candidateLabels.Length != SupportCount ||
                 _candidateLabels.Any(string.IsNullOrWhiteSpace) ||
                 _candidateLabels.Distinct(StringComparer.Ordinal).Count() !=
                    _candidateLabels.Length)))
            throw new ArgumentException("Invalid source-pinned random-choice request.");
    }

    internal void ValidateForNativeProvider()
    {
        // Native admission is source-operation based; do not consult the
        // provider-neutral legacy/query registration set here.
        ValidateShape();
        if (!StochasticSiteRegistryV111.IsNativeRandomIndexSite(SiteId))
            throw new NotSupportedException(
                $"Native random choice does not support site '{SiteId}'.");
        if (StochasticSiteRegistryV111
                .TryGetRequiredNativeRandomIndexOperationKind(
                    SiteId, StreamId, out var requiredOperationKind) &&
            OperationKind != requiredOperationKind)
            throw new ArgumentException(
                $"Operation kind '{OperationKind}' does not match the Native kind '{requiredOperationKind}' for site '{SiteId}'.",
                nameof(OperationKind));
    }

    internal void ValidateForLegacyProvider()
    {
        ValidateShape();
        if (!StochasticSiteRegistryV111.IsLegacyRandomChoiceSite(SiteId))
            throw new NotSupportedException(
                $"Legacy random choice does not support site '{SiteId}'.");

        if (StochasticSiteRegistryV111.TryGetLegacyCallerBoundIndexQuerySite(
                SiteId, out var registeredSite))
        {
            if (StreamId != registeredSite.StreamId ||
                OperationKind != registeredSite.OperationKind ||
                SupportCount < registeredSite.MinimumSupportCount ||
                registeredSite.ExactSupportCount is { } exactSupportCount &&
                    SupportCount != exactSupportCount)
                throw new ArgumentException(
                    $"Request does not match the registered legacy stream, operation, or support for site '{SiteId}'.",
                    nameof(SiteId));
            return;
        }

        if (StochasticSiteRegistryV111
                .RequiresLegacyBoundedRandomIndexOperation(SiteId) &&
            OperationKind != "bounded_integer")
            throw new ArgumentException(
                $"Legacy site '{SiteId}' requires a bounded-integer request.",
                nameof(OperationKind));
    }

    private static int[] CloneRequired(int[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (int[])values.Clone();
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
        if (ProviderMode == "native-v111")
            Request.ValidateForNativeProvider();
        else if (ProviderMode == "legacy-prototype-v0")
            Request.ValidateForLegacyProvider();
        else
            Request.Validate();
        if (SelectedIndex < 0 || SelectedIndex >= Request.SupportCount ||
            string.IsNullOrWhiteSpace(ProviderMode) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new ArgumentException("Illegal random-choice outcome or probability label.");
        RandomBoundaryCursorSpanV111.Validate(
            Request.StreamId, ProviderMode,
            NativeCursorBefore, NativeCursorAfter);
        if (ProviderMode == "native-v111")
        {
            var replayedIndex = NativeRngV111.ReplayNextInt(
                Request.StreamId, NativeCursorBefore!, Request.SupportCount,
                out var replayedCursorAfter);
            if (SelectedIndex != replayedIndex ||
                NativeCursorAfter != replayedCursorAfter)
                throw new InvalidDataException(
                    "Native index decision does not match its saved cursor transition.");
        }
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
    private readonly bool _recordToJournal;

    public NativeRandomIndexProviderV111(bool recordToJournal = true)
    {
        _recordToJournal = recordToJournal;
    }

    public RandomIndexDecisionV111 Resolve(RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.ValidateForNativeProvider();
        using var nativeBoundary =
            ControlledChanceGuardV111.AuthorizeEngineOwnedNativeBoundary(
                request.SiteId, request.StreamId, request.OperationKind);
        var before = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        int index = NativeRngV111.NextInt(streams, request.StreamId, request.SupportCount);
        var after = NativeRngV111.ToSnapshot(
            streams.GetStream(request.StreamId));
        var decision = new RandomIndexDecisionV111(
            request with { OrderedCandidates = (int[])request.OrderedCandidates.Clone() },
            index, Mode, before, after);
        decision.Validate();
        if (_recordToJournal)
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
        request.ValidateForLegacyProvider();
        if (ControlledChanceGuardV111.Active &&
            ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
            !ControlledChanceGuardV111.IsEngineOwnedPrototypeRewardRequest(
                request.SiteId, request.StreamId, request.OperationKind))
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=" + request.SiteId +
                " stream=" + request.StreamId +
                " operation=" + request.OperationKind +
                " : Native control cannot fall back to prototype RNG.");
        var stream = streams.GetStream(request.StreamId);
        if (stream.Codec != PrototypeRng.Codec ||
            stream.StateBytes.Length != sizeof(ulong))
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
/// Convenience boundary for source sites that still use the legacy six-stream
/// profile. It records the same typed request as native and scripted providers
/// while preserving the legacy modulo draw and named-stream cursor.
/// </summary>
public static class LegacyRandomChoiceV111
{
    public static RandomIndexDecisionV111 ChooseIndex(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId,
        int[] orderedCandidates,
        int? ordinal = null,
        string[]? candidateLabels = null)
    {
        ArgumentNullException.ThrowIfNull(orderedCandidates);
        if (orderedCandidates.Length == 0)
            throw new ArgumentException(
                "Legacy index selection requires a nonempty ordered support.",
                nameof(orderedCandidates));
        return Resolve(
            streams, siteId, streamId, ownerId, orderedCandidates,
            boundedCount: null, ordinal: ordinal,
            candidateLabels: candidateLabels);
    }

    public static RandomIndexDecisionV111 ChooseIndexRange(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId,
        int candidateCount,
        int? ordinal = null,
        string[]? candidateLabels = null)
    {
        if (candidateCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(candidateCount));
        return ChooseIndex(
            streams, siteId, streamId, ownerId,
            Enumerable.Range(0, candidateCount).ToArray(), ordinal,
            candidateLabels);
    }

    public static RandomIndexDecisionV111 ChooseBoundedInteger(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId,
        int boundedCount,
        int? ordinal = null)
    {
        if (boundedCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(boundedCount));
        return Resolve(
            streams, siteId, streamId, ownerId, [],
            boundedCount: boundedCount, ordinal: ordinal,
            candidateLabels: null);
    }

    private static RandomIndexDecisionV111 Resolve(
        RngBundle streams,
        string siteId,
        string streamId,
        string ownerId,
        int[] orderedCandidates,
        int? boundedCount,
        int? ordinal,
        string[]? candidateLabels)
    {
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(orderedCandidates);
        if ((boundedCount is null && orderedCandidates.Length == 0) ||
            (boundedCount is { } invalidBound &&
             (invalidBound <= 0 || orderedCandidates.Length != 0)))
            throw new ArgumentException(
                "Legacy random-choice support does not match its operation.");
        if (!StochasticSiteRegistryV111.IsLegacyRandomChoiceSite(siteId))
            throw new NotSupportedException(
                $"Legacy random choice does not support site '{siteId}'.");
        var matchingStream = streams.GetStream(streamId);
        int streamOrdinal;
        if (ordinal is { } explicitOrdinal)
        {
            streamOrdinal = explicitOrdinal;
        }
        else if (StochasticProviderScopeV111.GetNextOrdinal(
                     streams, streamId) is { } scopedOrdinal)
        {
            streamOrdinal = scopedOrdinal;
        }
        else
        {
            var providerOrdinal = matchingStream.CallCount ?? 0;
            if (providerOrdinal > int.MaxValue)
                throw new NotSupportedException(
                    "Legacy random-choice provider ordinal exceeds the request range.");
            streamOrdinal = (int)providerOrdinal;
        }
        if (streamOrdinal < 0)
            throw new NotSupportedException(
                "Legacy random-choice provider ordinal exceeds the request range.");
        var request = boundedCount is { } bound
            ? new RandomIndexRequestV111(
                siteId, streamId, ownerId, streamOrdinal, [],
                OperationKind: "bounded_integer", BoundedCount: bound)
            : new RandomIndexRequestV111(
                siteId, streamId, ownerId, streamOrdinal,
                (int[])orderedCandidates.Clone(),
                OperationKind: "choose_index")
            {
                CandidateLabels = candidateLabels
            };
        var operationKind = request.OperationKind;
        if (ControlledChanceGuardV111.Active
            && !ControlledChanceGuardV111.EngineOwnedNativeProviderActive
            && !ControlledChanceGuardV111.IsEngineOwnedPrototypeRewardRequest(
                siteId, streamId, operationKind))
        {
            if (ControlledEventChoiceReplayV111.IsActive &&
                ControlledEventChoiceReplayV111.IsControlledReplaySite(siteId))
            {
                if (StochasticProviderScopeV111.HasProviderOverride)
                    return StochasticProviderScopeV111.ResolveIndex(
                        new LegacyPrototypeRandomIndexProviderV111(),
                        request, streams);
                var controlledDecision = new RandomIndexDecisionV111(
                    request,
                    ControlledEventChoiceReplayV111.Resolve(request),
                    "controlled-v111");
                controlledDecision.Validate();
                return controlledDecision;
            }
            if (PrototypeControlledEnemyActionContinuationV111
                    .IsEnemyActionSite(siteId) &&
                StochasticProviderScopeV111.HasProviderOverride)
                return StochasticProviderScopeV111.ResolveIndex(
                    new LegacyPrototypeRandomIndexProviderV111(),
                    request, streams);
            throw new NotSupportedException(
                $"UNSUPPORTED_CONTROLLED_CHANCE site={siteId} " +
                $"operation={operationKind} requires a saved continuation.");
        }
        var decision = StochasticProviderScopeV111.ResolveIndex(
            new LegacyPrototypeRandomIndexProviderV111(), request, streams);
        return decision;
    }
}

/// <summary>
/// Compatibility wrapper for legacy-profile Fisher–Yates sites. It reuses
/// the shared descending swap mechanics and the original named stream.
/// </summary>
public static class LegacyFisherYatesV111
{
    public static RandomIndexDecisionV111[] ShuffleInPlace<T>(
        IList<T> values,
        RngBundle streams,
        string streamId,
        string siteId,
        string ownerId,
        int? ordinal = null,
        Func<T, string>? controlledCandidateLabel = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (!StochasticSiteRegistryV111.IsLegacyFisherYatesSite(siteId))
            throw new NotSupportedException(
                $"Legacy Fisher-Yates does not support site '{siteId}'.");
        var controlledEventChoice =
            ControlledChanceGuardV111.Active
            && !ControlledChanceGuardV111.EngineOwnedNativeProviderActive
            && ControlledEventChoiceReplayV111.IsActive
            && ControlledEventChoiceReplayV111.IsEventChoiceSite(siteId);
        if (ControlledChanceGuardV111.Active
            && !ControlledChanceGuardV111.EngineOwnedNativeProviderActive
            && !controlledEventChoice)
            throw new NotSupportedException(
                $"UNSUPPORTED_CONTROLLED_CHANCE site={siteId} " +
                "operation=shuffle requires a saved continuation.");

        var streamOrdinal = streams.GetStream(streamId).CallCount ?? 0;
        if (streamOrdinal < 0 || streamOrdinal > int.MaxValue)
            throw new NotSupportedException(
                "Legacy shuffle provider ordinal exceeds the request range.");
        var callOrdinal = ordinal ?? (int)streamOrdinal;
        if (callOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        IRandomIndexProviderV111 provider = controlledEventChoice
            ? new ControlledEventChoiceRandomIndexProviderV111()
            : new LegacyPrototypeRandomIndexProviderV111();
        return NativeFisherYatesV111.ShuffleInPlaceCore(
            values,
            streams,
            provider,
            streamId,
            ownerId,
            siteId,
            callOrdinal,
            controlledCandidateLabel);
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
