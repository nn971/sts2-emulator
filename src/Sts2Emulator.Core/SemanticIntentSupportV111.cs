namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped candidate at one actual enemy-intent random ticket.
/// TicketStart maps a semantic move selection to the first legal bounded
/// primitive ticket in its source raw-weight interval. Chance Law queries
/// map exact Native NextInt grid preimages through these intervals; that does
/// not establish source eligibility completeness or player-visible
/// conditional probabilities.
/// </summary>
public sealed record SemanticIntentCandidateV111(
    string IntentId, string MoveId, int MoveIndex,
    int RawWeight, int TicketStart);

/// <summary>
/// Candidate list after the same phase/history/formation eligibility filters
/// used by the original combat selector, at the current suspended RNG call.
/// </summary>
public sealed record SemanticIntentSupportV111(
    string OwnerId, SemanticIntentCandidateV111[] Candidates,
    int TotalRawWeight)
{
    private SemanticIntentCandidateV111[]? _candidates =
        CloneOptional(Candidates);

    public SemanticIntentCandidateV111[] Candidates
    {
        get => CloneOptional(_candidates)!;
        init => _candidates = CloneOptional(value);
    }

    public const string SchemaId = "sts2-v111-semantic-weighted-intent-v1";

    public SemanticIntentSupportV111 Fork() => this with
    {
        Candidates = _candidates!
    };

    private static SemanticIntentCandidateV111[]? CloneOptional(
        SemanticIntentCandidateV111[]? values) => values?.Select(candidate =>
            candidate is null ? null! : candidate with { }).ToArray();
}

/// <summary>A Nature-facing weighted intent support event.</summary>
public sealed record SemanticIntentEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, string SiteId, string StreamId,
    string OwnerId, int Ordinal, SemanticIntentSupportV111 Support,
    string ProbabilityStatus = "UNVERIFIED",
    string ProbabilityProvenance = "source-shaped-unverified",
    bool WeightsVerified = false) : ISemanticChanceEventV111
{
    private SemanticIntentSupportV111? _support = Support?.Fork();

    public SemanticIntentSupportV111 Support
    {
        get => _support!.Fork();
        init => _support = value?.Fork();
    }

    public const string EventSchemaId = SemanticIntentChoiceV111.SchemaId;
    public string OperationKind => "weighted_choice";

    public SemanticIntentEventV111 Fork() => this with
    {
        Support = _support!
    };

    public void Validate()
    {
        var support = _support;
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            string.IsNullOrWhiteSpace(SiteId) ||
            string.IsNullOrWhiteSpace(StreamId) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            support is null || support.OwnerId != OwnerId ||
            support.Candidates is not { Length: > 0 } ||
            support.TotalRawWeight <= 0 ||
            SupportFingerprint != CanonicalJson.Sha256(support) ||
            ProbabilityStatus != "UNVERIFIED" ||
            ProbabilityProvenance != "source-shaped-unverified" ||
            WeightsVerified)
            throw new InvalidDataException("Invalid semantic intent event.");
        var intentIds = new HashSet<string>(StringComparer.Ordinal);
        int ticketStart = 0;
        foreach (var candidate in support.Candidates)
        {
            if (candidate is null ||
                string.IsNullOrWhiteSpace(candidate.IntentId) ||
                string.IsNullOrWhiteSpace(candidate.MoveId) ||
                candidate.RawWeight <= 0 ||
                candidate.TicketStart != ticketStart ||
                !intentIds.Add(candidate.IntentId))
                throw new InvalidDataException(
                    "Semantic intent candidates are malformed or unordered.");
            ticketStart = checked(ticketStart + candidate.RawWeight);
        }
        if (ticketStart != support.TotalRawWeight)
            throw new InvalidDataException(
                "Semantic intent weights do not match their total.");
    }
}

/// <summary>
/// Selected semantic branch and its event support. The source selector's
/// bounded ticket realization is retained separately for mechanics replay.
/// </summary>
public sealed record SemanticIntentDecisionV111(
    SemanticIntentEventV111 Event, string IntentId)
{
    public SemanticIntentDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (string.IsNullOrWhiteSpace(IntentId) ||
            !Event.Support.Candidates.Any(candidate =>
                candidate.IntentId == IntentId))
            throw new InvalidDataException(
                "Semantic intent outcome is outside its event support.");
    }
}

/// <summary>
/// A provider-sampled weighted intent outcome. SourceTicket retains the exact
/// primitive bounded-integer result; IntentId identifies the weighted move
/// branch whose half-open ticket interval contains that result.
/// </summary>
public sealed record SemanticIntentOutcomeV111(
    SemanticIntentEventV111 Event, string IntentId, int SourceTicket)
{
    public SemanticIntentOutcomeV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        var candidate = Event.Support.Candidates.SingleOrDefault(item =>
            item.IntentId == IntentId);
        if (candidate is null || SourceTicket < candidate.TicketStart ||
            SourceTicket >= checked(candidate.TicketStart + candidate.RawWeight))
            throw new InvalidDataException(
                "Sampled intent ticket is outside its weighted outcome interval.");
    }
}

/// <summary>
/// Provider-private audit link between a weighted semantic outcome and the
/// exact primitive sample that selected its ticket.
/// </summary>
public sealed record SemanticIntentAuditEntryV111(
    SemanticIntentOutcomeV111 Outcome,
    RandomIndexDecisionV111 ProviderDecision)
{
    public SemanticIntentAuditEntryV111 Fork() => this with
    {
        Outcome = Outcome.Fork(),
        ProviderDecision = ProviderDecision with
        {
            Request = ProviderDecision.Request with
            {
                OrderedCandidates = (int[])ProviderDecision.Request
                    .OrderedCandidates.Clone()
            }
        }
    };

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Outcome);
        ArgumentNullException.ThrowIfNull(ProviderDecision);
        Outcome.Validate();
        ProviderDecision.Validate();
        var semanticEvent = Outcome.Event;
        var request = ProviderDecision.Request;
        if (ProviderDecision.ProviderMode != "native-v111" ||
            request.SiteId != semanticEvent.SiteId ||
            request.StreamId != semanticEvent.StreamId ||
            request.OwnerId != semanticEvent.OwnerId ||
            request.Ordinal != semanticEvent.Ordinal ||
            request.SupportCount != semanticEvent.Support.TotalRawWeight ||
            ProviderDecision.SelectedIndex != Outcome.SourceTicket)
            throw new InvalidDataException(
                "Weighted intent outcome is not paired with its sampled provider decision.");
    }
}

/// <summary>A semantic intent outcome paired with its retained primitive
/// ticket position in an automatic end-turn transcript.</summary>
public sealed record SemanticIntentTranscriptEntryV111(
    int TranscriptIndex, SemanticIntentDecisionV111 Decision)
{
    public SemanticIntentTranscriptEntryV111 Fork() => this with
    {
        Decision = Decision.Fork()
    };

    public void Validate()
    {
        if (TranscriptIndex < 0 || Decision is null)
            throw new InvalidDataException(
                "Invalid semantic intent transcript entry.");
        Decision.Validate();
    }
}

public static class SemanticIntentChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-intent-event-v1";

    internal static SemanticIntentEventV111 CreateEvent(
        string boundaryFingerprint, RandomIndexRequestV111 request,
        SemanticIntentSupportV111 support)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(support);
        var detachedSupport = support with
        {
            Candidates =
                (SemanticIntentCandidateV111[])support.Candidates.Clone()
        };
        var supportFingerprint = CanonicalJson.Sha256(support);
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticIntentEventV111(
            SchemaId, eventId, boundaryFingerprint, supportFingerprint,
            request.SiteId, request.StreamId, request.OwnerId,
            request.Ordinal, detachedSupport);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static (SemanticIntentDecisionV111 Decision, int SourceTicket)
        Select(SemanticIntentEventV111 pending, string intentId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var candidate = pending.Support.Candidates.SingleOrDefault(item =>
            item.IntentId == intentId) ??
            throw new InvalidOperationException(
                "Illegal or stale semantic intent candidate ID.");
        var decision = new SemanticIntentDecisionV111(
            pending.Fork(), candidate.IntentId);
        decision.Validate();
        return (decision, candidate.TicketStart);
    }

    internal static void ValidateDecision(
        SemanticIntentEventV111 pending,
        SemanticIntentDecisionV111 decision, int sourceTicket)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(decision);
        pending.Validate();
        decision.Validate();
        var candidate = pending.Support.Candidates.SingleOrDefault(item =>
            item.IntentId == decision.IntentId);
        if (candidate is null || sourceTicket != candidate.TicketStart ||
            CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event))
            throw new InvalidOperationException(
                "Semantic intent event is stale or outside current support.");
    }
}
