using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// One ordered provider outcome for a versioned continuation that can cross
/// any typed random primitive. Exactly one typed decision is present.
/// </summary>
public sealed record ControlledStochasticDecisionV111(
    RandomIndexDecisionV111? IndexDecision = null,
    RandomUnitFloatDecisionV111? UnitFloatDecision = null,
    string[]? CandidateLabels = null)
{
    private string[]? _candidateLabels = Clone(CandidateLabels);

    public string[]? CandidateLabels
    {
        get => Clone(_candidateLabels);
        init => _candidateLabels = Clone(value);
    }

    private static string[]? Clone(string[]? labels) => labels is null
        ? null
        : (string[])labels.Clone();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RandomRangedFloatDecisionV111? RangedFloatDecision { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RandomRangedDoubleDecisionV111? RangedDoubleDecision { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RandomGaussianIntDecisionV111? GaussianIntDecision { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RandomGaussianDoubleDecisionV111? GaussianDoubleDecision { get; init; }

    public static ControlledStochasticDecisionV111 FromIndex(
        RandomIndexDecisionV111 decision, string[]? candidateLabels = null) =>
        new(decision, null, candidateLabels);

    public static ControlledStochasticDecisionV111 FromUnitFloat(
        RandomUnitFloatDecisionV111 decision) => new(null, decision);

    public static ControlledStochasticDecisionV111 FromRangedFloat(
        RandomRangedFloatDecisionV111 decision) => new()
        { RangedFloatDecision = decision };

    public static ControlledStochasticDecisionV111 FromRangedDouble(
        RandomRangedDoubleDecisionV111 decision) => new()
        { RangedDoubleDecision = decision };

    public static ControlledStochasticDecisionV111 FromGaussianInt(
        RandomGaussianIntDecisionV111 decision) => new()
        { GaussianIntDecision = decision };

    public static ControlledStochasticDecisionV111 FromGaussianDouble(
        RandomGaussianDoubleDecisionV111 decision) => new()
        { GaussianDoubleDecision = decision };

    public ControlledStochasticRequestV111 ToRequest() =>
        IndexDecision is { } index ?
            ControlledStochasticRequestV111.FromIndex(index.Request) :
        UnitFloatDecision is { } unitFloat ?
            ControlledStochasticRequestV111.FromUnitFloat(unitFloat.Request) :
        RangedFloatDecision is { } rangedFloat ?
            ControlledStochasticRequestV111.FromRangedFloat(rangedFloat.Request) :
        RangedDoubleDecision is { } rangedDouble ?
            ControlledStochasticRequestV111.FromRangedDouble(rangedDouble.Request) :
        GaussianIntDecision is { } gaussianInt ?
            ControlledStochasticRequestV111.FromGaussianInt(gaussianInt.Request) :
        GaussianDoubleDecision is { } gaussianDouble ?
            ControlledStochasticRequestV111.FromGaussianDouble(gaussianDouble.Request) :
        throw new InvalidDataException(
            "A typed stochastic outcome has no request.");

    public bool IsIndexOrUnitFloat() =>
        IndexDecision is not null || UnitFloatDecision is not null;

    public ControlledStochasticDecisionV111 Fork() => this with
    {
        IndexDecision = IndexDecision is null ? null :
            IndexDecision with
            {
                Request = IndexDecision.Request with
                {
                    OrderedCandidates = (int[])
                        IndexDecision.Request.OrderedCandidates.Clone()
                }
            },
        UnitFloatDecision = UnitFloatDecision is null ? null :
            UnitFloatDecision with
            {
                Request = UnitFloatDecision.Request with { }
            },
        RangedFloatDecision = RangedFloatDecision is null ? null :
            RangedFloatDecision with
            {
                Request = RangedFloatDecision.Request with { }
            },
        RangedDoubleDecision = RangedDoubleDecision is null ? null :
            RangedDoubleDecision with
            {
                Request = RangedDoubleDecision.Request with { }
            },
        GaussianIntDecision = GaussianIntDecision is null ? null :
            GaussianIntDecision with
            {
                Request = GaussianIntDecision.Request with { }
            },
        GaussianDoubleDecision = GaussianDoubleDecision is null ? null :
            GaussianDoubleDecision with
            {
                Request = GaussianDoubleDecision.Request with { }
            },
        CandidateLabels = CandidateLabels is null ? null :
            (string[])CandidateLabels.Clone()
    };

    public void Validate()
    {
        int count = (IndexDecision is not null ? 1 : 0) +
            (UnitFloatDecision is not null ? 1 : 0) +
            (RangedFloatDecision is not null ? 1 : 0) +
            (RangedDoubleDecision is not null ? 1 : 0) +
            (GaussianIntDecision is not null ? 1 : 0) +
            (GaussianDoubleDecision is not null ? 1 : 0);
        if (count != 1)
            throw new InvalidDataException(
                "A typed stochastic outcome must contain exactly one decision.");
        if (IndexDecision is { } index)
        {
            index.Validate();
            if (index.ProviderMode != "controlled-v111" ||
                index.NativeCursorBefore is not null ||
                index.NativeCursorAfter is not null ||
                CandidateLabels is not null &&
                    (CandidateLabels.Length != index.Request.SupportCount ||
                     CandidateLabels.Any(string.IsNullOrWhiteSpace)))
                throw new InvalidDataException(
                    "Typed stochastic index outcome has invalid controlled provenance.");
        }
        else
        {
            if (CandidateLabels is not null)
                throw new InvalidDataException(
                    "Only index outcomes can carry candidate labels.");
            if (UnitFloatDecision is { } unitFloat)
            {
                unitFloat.Validate();
                if (unitFloat.ProviderMode != "controlled-v111" ||
                    unitFloat.NativeCursorBefore is not null ||
                    unitFloat.NativeCursorAfter is not null)
                    throw new InvalidDataException(
                        "Typed stochastic unit-float outcome has invalid controlled provenance.");
            }
            else if (RangedFloatDecision is { } rangedFloat)
            {
                rangedFloat.Validate();
                if (!HasControlledProvenance(
                        rangedFloat.ProviderMode,
                        rangedFloat.NativeCursorBefore,
                        rangedFloat.NativeCursorAfter))
                    throw new InvalidDataException(
                        "Typed stochastic ranged-float outcome has invalid controlled provenance.");
            }
            else if (RangedDoubleDecision is { } rangedDouble)
            {
                rangedDouble.Validate();
                if (!HasControlledProvenance(
                        rangedDouble.ProviderMode,
                        rangedDouble.NativeCursorBefore,
                        rangedDouble.NativeCursorAfter))
                    throw new InvalidDataException(
                        "Typed stochastic ranged-double outcome has invalid controlled provenance.");
            }
            else if (GaussianIntDecision is { } gaussianInt)
            {
                gaussianInt.Validate();
                if (!HasControlledProvenance(
                        gaussianInt.ProviderMode,
                        gaussianInt.NativeCursorBefore,
                        gaussianInt.NativeCursorAfter))
                    throw new InvalidDataException(
                        "Typed stochastic Gaussian-integer outcome has invalid controlled provenance.");
            }
            else
            {
                var gaussianDouble = GaussianDoubleDecision!;
                gaussianDouble.Validate();
                if (!HasControlledProvenance(
                        gaussianDouble.ProviderMode,
                        gaussianDouble.NativeCursorBefore,
                        gaussianDouble.NativeCursorAfter))
                    throw new InvalidDataException(
                        "Typed stochastic Gaussian-double outcome has invalid controlled provenance.");
            }
        }
    }

    private static bool HasControlledProvenance(
        string providerMode, NativeRngSnapshot? cursorBefore,
        NativeRngSnapshot? cursorAfter) =>
        providerMode == "controlled-v111" && cursorBefore is null &&
        cursorAfter is null;
}

/// <summary>Exact request identity for the next typed stochastic boundary.</summary>
public sealed record ControlledStochasticRequestV111(
    RandomIndexRequestV111? IndexRequest = null,
    RandomUnitFloatRequestV111? UnitFloatRequest = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RandomRangedFloatRequestV111? RangedFloatRequest { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RandomRangedDoubleRequestV111? RangedDoubleRequest { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RandomGaussianIntRequestV111? GaussianIntRequest { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RandomGaussianDoubleRequestV111? GaussianDoubleRequest { get; init; }

    public string SiteId => IndexRequest?.SiteId ?? UnitFloatRequest?.SiteId ??
        RangedFloatRequest?.SiteId ?? RangedDoubleRequest?.SiteId ??
        GaussianIntRequest?.SiteId ?? GaussianDoubleRequest?.SiteId ??
        throw new InvalidDataException("Typed stochastic request is empty.");
    public string StreamId => IndexRequest?.StreamId ?? UnitFloatRequest?.StreamId ??
        RangedFloatRequest?.StreamId ?? RangedDoubleRequest?.StreamId ??
        GaussianIntRequest?.StreamId ?? GaussianDoubleRequest?.StreamId ??
        throw new InvalidDataException("Typed stochastic request is empty.");
    public string OwnerId => IndexRequest?.OwnerId ?? UnitFloatRequest?.OwnerId ??
        RangedFloatRequest?.OwnerId ?? RangedDoubleRequest?.OwnerId ??
        GaussianIntRequest?.OwnerId ?? GaussianDoubleRequest?.OwnerId ??
        throw new InvalidDataException("Typed stochastic request is empty.");
    public int Ordinal => IndexRequest?.Ordinal ?? UnitFloatRequest?.Ordinal ??
        RangedFloatRequest?.Ordinal ?? RangedDoubleRequest?.Ordinal ??
        GaussianIntRequest?.Ordinal ?? GaussianDoubleRequest?.Ordinal ??
        throw new InvalidDataException("Typed stochastic request is empty.");

    public static ControlledStochasticRequestV111 FromIndex(
        RandomIndexRequestV111 request) => new(request, null);

    public static ControlledStochasticRequestV111 FromUnitFloat(
        RandomUnitFloatRequestV111 request) => new(null, request);

    public static ControlledStochasticRequestV111 FromRangedFloat(
        RandomRangedFloatRequestV111 request) => new()
        { RangedFloatRequest = request };

    public static ControlledStochasticRequestV111 FromRangedDouble(
        RandomRangedDoubleRequestV111 request) => new()
        { RangedDoubleRequest = request };

    public static ControlledStochasticRequestV111 FromGaussianInt(
        RandomGaussianIntRequestV111 request) => new()
        { GaussianIntRequest = request };

    public static ControlledStochasticRequestV111 FromGaussianDouble(
        RandomGaussianDoubleRequestV111 request) => new()
        { GaussianDoubleRequest = request };

    public ControlledStochasticRequestV111 Fork() => this with
    {
        IndexRequest = IndexRequest is null ? null : IndexRequest with
        {
            OrderedCandidates = (int[])IndexRequest.OrderedCandidates.Clone()
        },
        UnitFloatRequest = UnitFloatRequest is null ? null :
            UnitFloatRequest with { },
        RangedFloatRequest = RangedFloatRequest is null ? null :
            RangedFloatRequest with { },
        RangedDoubleRequest = RangedDoubleRequest is null ? null :
            RangedDoubleRequest with { },
        GaussianIntRequest = GaussianIntRequest is null ? null :
            GaussianIntRequest with { },
        GaussianDoubleRequest = GaussianDoubleRequest is null ? null :
            GaussianDoubleRequest with { }
    };

    public void Validate()
    {
        int count = (IndexRequest is not null ? 1 : 0) +
            (UnitFloatRequest is not null ? 1 : 0) +
            (RangedFloatRequest is not null ? 1 : 0) +
            (RangedDoubleRequest is not null ? 1 : 0) +
            (GaussianIntRequest is not null ? 1 : 0) +
            (GaussianDoubleRequest is not null ? 1 : 0);
        if (count != 1)
            throw new InvalidDataException(
                "A typed stochastic request must contain exactly one request.");
        IndexRequest?.Validate();
        UnitFloatRequest?.Validate();
        RangedFloatRequest?.Validate();
        RangedDoubleRequest?.Validate();
        GaussianIntRequest?.Validate();
        GaussianDoubleRequest?.Validate();
    }
}

/// <summary>Serializable prefix and pending request for a versioned mixed
/// stochastic continuation. Version 1 remains restricted to index and
/// unit-float operations; version 2 admits all six provider primitives.</summary>
public sealed record ControlledStochasticContinuationStateV111(
    string SchemaId,
    ControlledStochasticDecisionV111[] Decisions,
    ControlledStochasticRequestV111 PendingRequest,
    string[]? PendingCandidateLabels = null)
{
    private ControlledStochasticDecisionV111[]? _decisions =
        CloneDecisions(Decisions);
    private string[]? _pendingCandidateLabels =
        CloneLabels(PendingCandidateLabels);

    public ControlledStochasticDecisionV111[] Decisions
    {
        get => CloneDecisions(_decisions)!;
        init => _decisions = CloneDecisions(value);
    }
    public string[]? PendingCandidateLabels
    {
        get => CloneLabels(_pendingCandidateLabels);
        init => _pendingCandidateLabels = CloneLabels(value);
    }

    public const string CurrentSchemaId =
        "sts2-v111-controlled-stochastic-continuation-v1";
    public const string MixedPrimitiveSchemaId =
        "sts2-v111-controlled-stochastic-continuation-v2";

    public ControlledStochasticContinuationStateV111 Fork() => this with
    {
        Decisions = _decisions!,
        PendingRequest = PendingRequest.Fork(),
        PendingCandidateLabels = _pendingCandidateLabels
    };

    public void Validate()
    {
        var decisions = _decisions;
        var pendingCandidateLabels = _pendingCandidateLabels;
        if (SchemaId is not (CurrentSchemaId or MixedPrimitiveSchemaId) ||
            decisions is null ||
            PendingRequest is null || pendingCandidateLabels is not null &&
                (PendingRequest.IndexRequest is null ||
                 pendingCandidateLabels.Length !=
                    PendingRequest.IndexRequest.SupportCount ||
                 pendingCandidateLabels.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException(
                "Malformed mixed controlled stochastic continuation.");
        PendingRequest.Validate();
        foreach (var decision in decisions)
        {
            if (decision is null)
                throw new InvalidDataException(
                    "Mixed stochastic continuation contains a null decision.");
            decision.Validate();
            if (SchemaId == CurrentSchemaId &&
                !decision.IsIndexOrUnitFloat())
                throw new InvalidDataException(
                    "Version 1 stochastic continuations contain only index and unit-float outcomes.");
        }
        if (SchemaId == CurrentSchemaId &&
            PendingRequest.IndexRequest is null &&
            PendingRequest.UnitFloatRequest is null)
            throw new InvalidDataException(
                "Version 1 stochastic continuations contain only index and unit-float requests.");
    }

    private static ControlledStochasticDecisionV111[]? CloneDecisions(
        ControlledStochasticDecisionV111[]? decisions) => decisions?.Select(
            decision => decision is null ? null! : decision.Fork()).ToArray();

    private static string[]? CloneLabels(string[]? labels) => labels is null
        ? null
        : (string[])labels.Clone();
}

/// <summary>
/// Suspension raised when a mixed provider replay reaches a request beyond
/// its saved prefix. The request retains its original primitive type.
/// </summary>
public sealed class ControlledStochasticChanceRequiredExceptionV111 : Exception
{
    private readonly string[]? _candidateLabels;

    public ControlledStochasticChanceRequiredExceptionV111(
        ControlledStochasticRequestV111 request,
        string[]? candidateLabels = null)
        : base("A controlled stochastic continuation needs another typed outcome.")
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request.Fork();
        _candidateLabels = candidateLabels is null ? null :
            (string[])candidateLabels.Clone();
        Request.Validate();
        var savedCandidateLabels = _candidateLabels;
        if (savedCandidateLabels is not null &&
            (Request.IndexRequest is null ||
             savedCandidateLabels.Length !=
                Request.IndexRequest.SupportCount ||
             savedCandidateLabels.Any(string.IsNullOrWhiteSpace)))
            throw new ArgumentException(
                "Candidate labels must match an index request support.",
                nameof(candidateLabels));
    }

    public ControlledStochasticRequestV111 Request { get; }
    public string[]? CandidateLabels => _candidateLabels is null ? null :
        (string[])_candidateLabels.Clone();
}

/// <summary>
/// Result of capturing or advancing a versioned mixed stochastic continuation.
/// A suspended result carries the complete replay prefix and exact next request.
/// </summary>
internal sealed record ControlledStochasticContinuationResultV111(
    RunState? Run,
    ControlledStochasticContinuationStateV111? Continuation)
{
    public bool Suspended => Continuation is not null;
}

/// <summary>
/// Replays one chronological prefix across every typed provider interface.
/// It deliberately owns no law, boundary allowlist, or mechanics.
/// </summary>
internal sealed class ControlledStochasticChanceReplayV111 :
    IRandomBoundaryProviderV111,
    IControlledChanceReplayScopeV111
{
    private static readonly AsyncLocal<ControlledStochasticChanceReplayV111?>
        Active = new();
    private readonly ControlledStochasticChanceReplayV111? _previous;
    private readonly ControlledStochasticDecisionV111[] _decisions;
    private ControlledStochasticDecisionV111? _lastDecision;
    private int _cursor;
    private string[]? _candidateLabels;
    private readonly bool _registeredAsActive;
    private readonly bool _allowMissingCandidateLabels;
    private bool _disposed;

    private ControlledStochasticChanceReplayV111(
        ControlledStochasticDecisionV111[] decisions,
        bool registerAsActive,
        bool allowMissingCandidateLabels = false)
    {
        _previous = registerAsActive ? Active.Value : null;
        _decisions = decisions.Select(decision =>
        {
            ArgumentNullException.ThrowIfNull(decision);
            decision.Validate();
            return decision.Fork();
        }).ToArray();
        _registeredAsActive = registerAsActive;
        _allowMissingCandidateLabels = allowMissingCandidateLabels;
        if (registerAsActive)
            Active.Value = this;
    }

    public string Mode => "controlled-v111";
    public int Remaining => _decisions.Length - _cursor;
    internal int ConsumedCount => _cursor;
    internal IReadOnlyList<ControlledStochasticDecisionV111> Decisions =>
        _decisions;
    public static bool IsActive => Active.Value is not null;

    internal static void ClearLastResolvedDecision()
    {
        if (Active.Value is { } active)
            active._lastDecision = null;
    }

    internal static string? GetSelectedIndexCandidateLabel(
        RandomIndexRequestV111 request, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(request);
        var decision = Active.Value?._lastDecision;
        if (decision?.IndexDecision is not { } index ||
            !SameRequest(index.Request, request))
            return null;
        if (index.SelectedIndex != selectedIndex)
            throw new InvalidDataException(
                "Resolved index differs from its active typed replay outcome.");
        return decision.CandidateLabels is { } labels
            ? labels[selectedIndex]
            : null;
    }

    public static ControlledStochasticChanceReplayV111 Begin(
        ControlledStochasticDecisionV111[] decisions,
        bool allowMissingCandidateLabels = false)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return new ControlledStochasticChanceReplayV111(
            decisions, registerAsActive: true,
            allowMissingCandidateLabels: allowMissingCandidateLabels);
    }

    /// <summary>
    /// Creates a prefix cursor for a boundary adapter that performs its own
    /// request normalization and provider dispatch. It shares decision
    /// validation and cursor ownership without changing the active-provider
    /// signal observed by unrelated engine paths.
    /// </summary>
    internal static ControlledStochasticChanceReplayV111 BeginDetachedPrefix(
        ControlledStochasticDecisionV111[] decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return new ControlledStochasticChanceReplayV111(
            decisions, registerAsActive: false);
    }

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var active = Active.Value ?? throw new InvalidOperationException(
            "Typed stochastic replay is not active.");
        var expected = active.Take(
            ControlledStochasticRequestV111.FromIndex(request),
            active._candidateLabels);
        if (expected.IndexDecision is not { } index ||
            !SameRequest(index.Request, request))
            throw new InvalidDataException(
                "Typed stochastic replay diverged from its saved index request.");
        var resolved = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, index.SelectedIndex, Mode);
        resolved.Validate();
        RandomBoundaryJournalV111.Record(resolved);
        return resolved;
    }

    public RandomUnitFloatDecisionV111 Resolve(
        RandomUnitFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var active = Active.Value ?? throw new InvalidOperationException(
            "Typed stochastic replay is not active.");
        var expected = active.Take(
            ControlledStochasticRequestV111.FromUnitFloat(request), null);
        if (expected.UnitFloatDecision is not { } unitFloat ||
            !SameRequest(unitFloat.Request, request))
            throw new InvalidDataException(
                "Typed stochastic replay diverged from its saved unit-float request.");
        var resolved = new RandomUnitFloatDecisionV111(
            request with { }, unitFloat.SelectedValue, Mode);
        resolved.Validate();
        RandomBoundaryJournalV111.Record(resolved);
        return resolved;
    }

    public RandomRangedFloatDecisionV111 Resolve(
        RandomRangedFloatRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var active = Active.Value ?? throw new InvalidOperationException(
            "Typed stochastic replay is not active.");
        var expected = active.Take(
            ControlledStochasticRequestV111.FromRangedFloat(request), null);
        if (expected.RangedFloatDecision is not { } rangedFloat ||
            !SameRequest(rangedFloat.Request, request))
            throw new InvalidDataException(
                "Typed stochastic replay diverged from its saved ranged-float request.");
        var resolved = new RandomRangedFloatDecisionV111(
            request with { }, rangedFloat.SelectedValue, Mode);
        resolved.Validate();
        RandomBoundaryJournalV111.Record(resolved);
        return resolved;
    }

    public RandomRangedDoubleDecisionV111 Resolve(
        RandomRangedDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var active = Active.Value ?? throw new InvalidOperationException(
            "Typed stochastic replay is not active.");
        var expected = active.Take(
            ControlledStochasticRequestV111.FromRangedDouble(request), null);
        if (expected.RangedDoubleDecision is not { } rangedDouble ||
            !SameRequest(rangedDouble.Request, request))
            throw new InvalidDataException(
                "Typed stochastic replay diverged from its saved ranged-double request.");
        var resolved = new RandomRangedDoubleDecisionV111(
            request with { }, rangedDouble.SelectedValue, Mode);
        resolved.Validate();
        RandomBoundaryJournalV111.Record(resolved);
        return resolved;
    }

    public RandomGaussianIntDecisionV111 Resolve(
        RandomGaussianIntRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var active = Active.Value ?? throw new InvalidOperationException(
            "Typed stochastic replay is not active.");
        var expected = active.Take(
            ControlledStochasticRequestV111.FromGaussianInt(request), null);
        if (expected.GaussianIntDecision is not { } gaussianInt ||
            !SameRequest(gaussianInt.Request, request))
            throw new InvalidDataException(
                "Typed stochastic replay diverged from its saved Gaussian-integer request.");
        var resolved = new RandomGaussianIntDecisionV111(
            request with { }, gaussianInt.SelectedValue, Mode);
        resolved.Validate();
        RandomBoundaryJournalV111.Record(resolved);
        return resolved;
    }

    public RandomGaussianDoubleDecisionV111 Resolve(
        RandomGaussianDoubleRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.Validate();
        var active = Active.Value ?? throw new InvalidOperationException(
            "Typed stochastic replay is not active.");
        var expected = active.Take(
            ControlledStochasticRequestV111.FromGaussianDouble(request), null);
        if (expected.GaussianDoubleDecision is not { } gaussianDouble ||
            !SameRequest(gaussianDouble.Request, request))
            throw new InvalidDataException(
                "Typed stochastic replay diverged from its saved Gaussian-double request.");
        var resolved = new RandomGaussianDoubleDecisionV111(
            request with { }, gaussianDouble.SelectedValue, Mode);
        resolved.Validate();
        RandomBoundaryJournalV111.Record(resolved);
        return resolved;
    }

    private ControlledStochasticDecisionV111 Take(
        ControlledStochasticRequestV111 request,
        string[]? candidateLabels)
    {
        if (!TryTake(request, candidateLabels, true, out var expected, null))
            throw new ControlledStochasticChanceRequiredExceptionV111(
                request, candidateLabels);
        return expected;
    }

    internal bool TryTake(
        ControlledStochasticRequestV111 request,
        out ControlledStochasticDecisionV111 decision,
        string? divergenceMessage = null) =>
        TryTake(request, null, false, out decision, divergenceMessage);

    private bool TryTake(
        ControlledStochasticRequestV111 request,
        string[]? candidateLabels,
        bool compareCandidateLabels,
        out ControlledStochasticDecisionV111 decision,
        string? divergenceMessage)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (_cursor >= _decisions.Length)
        {
            decision = null!;
            return false;
        }

        var expected = _decisions[_cursor];
        if (!SameRequest(expected.ToRequest(), request))
            throw new InvalidDataException(
                divergenceMessage ??
                    "Typed stochastic replay diverged from its ordered saved prefix.");
        if (compareCandidateLabels &&
            (expected.CandidateLabels is { } savedLabels
                ? candidateLabels is null ||
                  !savedLabels.SequenceEqual(candidateLabels,
                      StringComparer.Ordinal)
                : candidateLabels is not null &&
                  !_allowMissingCandidateLabels))
            throw new InvalidDataException(
                "Typed stochastic replay diverged from its saved candidate labels.");
        _cursor++;
        decision = expected;
        _lastDecision = expected;
        return true;
    }

    public static IDisposable DescribeCandidates(string[]? candidateLabels)
    {
        var active = Active.Value;
        if (active is null)
            return EmptyScope.Instance;
        if (candidateLabels is not null &&
            candidateLabels.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(
                "Candidate labels cannot be blank.", nameof(candidateLabels));
        var previous = active._candidateLabels;
        active._candidateLabels = candidateLabels is null
            ? null : (string[])candidateLabels.Clone();
        return new CandidateScope(active, previous);
    }

    public void EnsureConsumed() => EnsureConsumed(errorMessage: null);

    public void EnsureConsumed(string? errorMessage)
    {
        if (_cursor != _decisions.Length)
            throw new InvalidDataException(
                errorMessage ??
                    "Typed stochastic replay left saved outcomes unused.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (_registeredAsActive)
        {
            if (!ReferenceEquals(Active.Value, this))
                throw new InvalidOperationException(
                    "Typed stochastic replay scopes must be disposed in stack order.");
            Active.Value = _previous;
        }
        _disposed = true;
    }

    private sealed class CandidateScope(
        ControlledStochasticChanceReplayV111 owner,
        string[]? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            if (!ReferenceEquals(Active.Value, owner))
                throw new InvalidOperationException(
                    "Candidate-label scopes must be disposed inside their typed replay scope.");
            owner._candidateLabels = previous;
            _disposed = true;
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }

    internal static bool SameRequest(
        ControlledStochasticRequestV111 left,
        ControlledStochasticRequestV111 right)
    {
        if (left.IndexRequest is not null || right.IndexRequest is not null)
            return left.IndexRequest is { } a && right.IndexRequest is { } b &&
                SameRequest(a, b);
        if (left.UnitFloatRequest is not null || right.UnitFloatRequest is not null)
            return left.UnitFloatRequest is { } a &&
                right.UnitFloatRequest is { } b && SameRequest(a, b);
        if (left.RangedFloatRequest is not null ||
            right.RangedFloatRequest is not null)
            return left.RangedFloatRequest is { } a &&
                right.RangedFloatRequest is { } b && SameRequest(a, b);
        if (left.RangedDoubleRequest is not null ||
            right.RangedDoubleRequest is not null)
            return left.RangedDoubleRequest is { } a &&
                right.RangedDoubleRequest is { } b && SameRequest(a, b);
        if (left.GaussianIntRequest is not null ||
            right.GaussianIntRequest is not null)
            return left.GaussianIntRequest is { } a &&
                right.GaussianIntRequest is { } b && SameRequest(a, b);
        return left.GaussianDoubleRequest is { } leftGaussian &&
            right.GaussianDoubleRequest is { } rightGaussian &&
            SameRequest(leftGaussian, rightGaussian);
    }

    private static bool SameRequest(
        RandomIndexRequestV111 left, RandomIndexRequestV111 right) =>
        left.SiteId == right.SiteId && left.StreamId == right.StreamId &&
        left.OwnerId == right.OwnerId && left.Ordinal == right.Ordinal &&
        left.SourceVersion == right.SourceVersion &&
        left.OperationKind == right.OperationKind &&
        left.BoundedCount == right.BoundedCount &&
        left.OrderedCandidates.SequenceEqual(right.OrderedCandidates);

    private static bool SameRequest(
        RandomUnitFloatRequestV111 left,
        RandomUnitFloatRequestV111 right) =>
        left.SiteId == right.SiteId && left.StreamId == right.StreamId &&
        left.OwnerId == right.OwnerId && left.Ordinal == right.Ordinal &&
        left.SourceVersion == right.SourceVersion;

    private static bool SameRequest(
        RandomRangedFloatRequestV111 left,
        RandomRangedFloatRequestV111 right) =>
        left.SiteId == right.SiteId && left.StreamId == right.StreamId &&
        left.OwnerId == right.OwnerId && left.Ordinal == right.Ordinal &&
        left.MinInclusive == right.MinInclusive &&
        left.MaxInclusive == right.MaxInclusive &&
        left.SourceVersion == right.SourceVersion;

    private static bool SameRequest(
        RandomRangedDoubleRequestV111 left,
        RandomRangedDoubleRequestV111 right) =>
        left.SiteId == right.SiteId && left.StreamId == right.StreamId &&
        left.OwnerId == right.OwnerId && left.Ordinal == right.Ordinal &&
        left.MinInclusive == right.MinInclusive &&
        left.MaxInclusive == right.MaxInclusive &&
        left.SourceVersion == right.SourceVersion;

    private static bool SameRequest(
        RandomGaussianIntRequestV111 left,
        RandomGaussianIntRequestV111 right) =>
        left.SiteId == right.SiteId && left.StreamId == right.StreamId &&
        left.OwnerId == right.OwnerId && left.Ordinal == right.Ordinal &&
        left.Mean == right.Mean &&
        left.StandardDeviation == right.StandardDeviation &&
        left.MinInclusive == right.MinInclusive &&
        left.MaxInclusive == right.MaxInclusive &&
        left.SourceVersion == right.SourceVersion;

    private static bool SameRequest(
        RandomGaussianDoubleRequestV111 left,
        RandomGaussianDoubleRequestV111 right) =>
        left.SiteId == right.SiteId && left.StreamId == right.StreamId &&
        left.OwnerId == right.OwnerId && left.Ordinal == right.Ordinal &&
        left.Mean == right.Mean &&
        left.StandardDeviation == right.StandardDeviation &&
        left.MinInclusive == right.MinInclusive &&
        left.MaxInclusive == right.MaxInclusive &&
        left.SourceVersion == right.SourceVersion;
}
