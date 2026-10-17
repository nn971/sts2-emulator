namespace Sts2Emulator.Core;

/// <summary>
/// One ordered provider outcome for a continuation that crosses discrete
/// index and unit-float boundaries. Exactly one typed decision is present.
/// </summary>
public sealed record ControlledStochasticDecisionV111(
    RandomIndexDecisionV111? IndexDecision = null,
    RandomUnitFloatDecisionV111? UnitFloatDecision = null,
    string[]? CandidateLabels = null)
{
    public static ControlledStochasticDecisionV111 FromIndex(
        RandomIndexDecisionV111 decision, string[]? candidateLabels = null) =>
        new(decision, null, candidateLabels);

    public static ControlledStochasticDecisionV111 FromUnitFloat(
        RandomUnitFloatDecisionV111 decision) => new(null, decision);

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
        CandidateLabels = CandidateLabels is null ? null :
            (string[])CandidateLabels.Clone()
    };

    public void Validate()
    {
        if ((IndexDecision is null) == (UnitFloatDecision is null))
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
            var unitFloat = UnitFloatDecision!;
            unitFloat.Validate();
            if (unitFloat.ProviderMode != "controlled-v111" ||
                unitFloat.NativeCursorBefore is not null ||
                unitFloat.NativeCursorAfter is not null ||
                CandidateLabels is not null)
                throw new InvalidDataException(
                    "Typed stochastic unit-float outcome has invalid controlled provenance.");
        }
    }
}

/// <summary>Exact request identity for the next mixed stochastic boundary.</summary>
public sealed record ControlledStochasticRequestV111(
    RandomIndexRequestV111? IndexRequest = null,
    RandomUnitFloatRequestV111? UnitFloatRequest = null)
{
    public string SiteId => IndexRequest?.SiteId ?? UnitFloatRequest!.SiteId;
    public string StreamId => IndexRequest?.StreamId ?? UnitFloatRequest!.StreamId;
    public string OwnerId => IndexRequest?.OwnerId ?? UnitFloatRequest!.OwnerId;
    public int Ordinal => IndexRequest?.Ordinal ?? UnitFloatRequest!.Ordinal;

    public static ControlledStochasticRequestV111 FromIndex(
        RandomIndexRequestV111 request) => new(request, null);

    public static ControlledStochasticRequestV111 FromUnitFloat(
        RandomUnitFloatRequestV111 request) => new(null, request);

    public ControlledStochasticRequestV111 Fork() => this with
    {
        IndexRequest = IndexRequest is null ? null : IndexRequest with
        {
            OrderedCandidates = (int[])IndexRequest.OrderedCandidates.Clone()
        },
        UnitFloatRequest = UnitFloatRequest is null ? null :
            UnitFloatRequest with { }
    };

    public void Validate()
    {
        if ((IndexRequest is null) == (UnitFloatRequest is null))
            throw new InvalidDataException(
                "A typed stochastic request must contain exactly one request.");
        IndexRequest?.Validate();
        UnitFloatRequest?.Validate();
    }
}

/// <summary>Serializable prefix and pending request for a versioned mixed
/// index/unit-float continuation.</summary>
public sealed record ControlledStochasticContinuationStateV111(
    string SchemaId,
    ControlledStochasticDecisionV111[] Decisions,
    ControlledStochasticRequestV111 PendingRequest,
    string[]? PendingCandidateLabels = null)
{
    public const string CurrentSchemaId =
        "sts2-v111-controlled-stochastic-continuation-v1";

    public ControlledStochasticContinuationStateV111 Fork() => this with
    {
        Decisions = Decisions.Select(decision => decision.Fork()).ToArray(),
        PendingRequest = PendingRequest.Fork(),
        PendingCandidateLabels = PendingCandidateLabels is null ? null :
            (string[])PendingCandidateLabels.Clone()
    };

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Decisions is null ||
            PendingRequest is null || PendingCandidateLabels is not null &&
                (PendingRequest.IndexRequest is null ||
                 PendingCandidateLabels.Length !=
                    PendingRequest.IndexRequest.SupportCount ||
                 PendingCandidateLabels.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException(
                "Malformed mixed controlled stochastic continuation.");
        PendingRequest.Validate();
        foreach (var decision in Decisions)
        {
            if (decision is null)
                throw new InvalidDataException(
                    "Mixed stochastic continuation contains a null decision.");
            decision.Validate();
        }
    }
}

/// <summary>
/// Suspension raised when a mixed provider replay reaches a request beyond
/// its saved prefix. The request retains its original index or float type.
/// </summary>
public sealed class ControlledStochasticChanceRequiredExceptionV111 : Exception
{
    public ControlledStochasticChanceRequiredExceptionV111(
        ControlledStochasticRequestV111 request,
        string[]? candidateLabels = null)
        : base("A controlled stochastic continuation needs another typed outcome.")
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request.Fork();
        CandidateLabels = candidateLabels is null ? null :
            (string[])candidateLabels.Clone();
        Request.Validate();
        if (CandidateLabels is not null &&
            (Request.IndexRequest is null ||
             CandidateLabels.Length != Request.IndexRequest.SupportCount ||
             CandidateLabels.Any(string.IsNullOrWhiteSpace)))
            throw new ArgumentException(
                "Candidate labels must match an index request support.",
                nameof(candidateLabels));
    }

    public ControlledStochasticRequestV111 Request { get; }
    public string[]? CandidateLabels { get; }
}

internal sealed record ControlledStochasticReplayResultV111(
    RunState? Run,
    ControlledStochasticChanceRequiredExceptionV111? PendingChance)
{
    public bool Suspended => PendingChance is not null;
}

/// <summary>
/// Replays a single chronological prefix across both existing typed provider
/// interfaces. It deliberately owns no law, boundary allowlist, or mechanics.
/// </summary>
internal sealed class ControlledStochasticChanceReplayV111 :
    IRandomIndexProviderV111,
    IRandomUnitFloatProviderV111,
    IControlledChanceReplayScopeV111
{
    private static readonly AsyncLocal<ControlledStochasticChanceReplayV111?>
        Active = new();
    private readonly ControlledStochasticChanceReplayV111? _previous;
    private readonly ControlledStochasticDecisionV111[] _decisions;
    private int _cursor;
    private bool _disposed;

    private ControlledStochasticChanceReplayV111(
        ControlledStochasticDecisionV111[] decisions)
    {
        _previous = Active.Value;
        _decisions = decisions.Select(decision =>
        {
            ArgumentNullException.ThrowIfNull(decision);
            decision.Validate();
            return decision.Fork();
        }).ToArray();
        Active.Value = this;
    }

    public string Mode => "controlled-v111";

    public static ControlledStochasticChanceReplayV111 Begin(
        ControlledStochasticDecisionV111[] decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return new ControlledStochasticChanceReplayV111(decisions);
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
            ControlledStochasticRequestV111.FromIndex(request));
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
            ControlledStochasticRequestV111.FromUnitFloat(request));
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

    private ControlledStochasticDecisionV111 Take(
        ControlledStochasticRequestV111 request)
    {
        if (_cursor >= _decisions.Length)
            throw new ControlledStochasticChanceRequiredExceptionV111(request);
        var expected = _decisions[_cursor];
        var expectedRequest = expected.IndexDecision is { } index
            ? ControlledStochasticRequestV111.FromIndex(index.Request)
            : ControlledStochasticRequestV111.FromUnitFloat(
                expected.UnitFloatDecision!.Request);
        if (!SameRequest(expectedRequest, request))
            throw new InvalidDataException(
                "Typed stochastic replay diverged from its ordered saved prefix.");
        _cursor++;
        return expected;
    }

    public void EnsureConsumed()
    {
        if (_cursor != _decisions.Length)
            throw new InvalidDataException(
                "Typed stochastic replay left saved outcomes unused.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (!ReferenceEquals(Active.Value, this))
            throw new InvalidOperationException(
                "Typed stochastic replay scopes must be disposed in stack order.");
        Active.Value = _previous;
        _disposed = true;
    }

    private static bool SameRequest(
        ControlledStochasticRequestV111 left,
        ControlledStochasticRequestV111 right)
    {
        if (left.IndexRequest is not null || right.IndexRequest is not null)
            return left.IndexRequest is { } a && right.IndexRequest is { } b
                && SameRequest(a, b);
        return left.UnitFloatRequest is { } leftFloat &&
            right.UnitFloatRequest is { } rightFloat &&
            SameRequest(leftFloat, rightFloat);
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
}
