namespace Sts2Emulator.Core;

/// <summary>One source-bounded position for a generated draw-pile card.</summary>
public sealed record SemanticDrawInsertionEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, RandomIndexRequestV111 Request,
    string GeneratedCardModelId, long[] EligibleDrawPileInstanceIds,
    string ProbabilityStatus = "UNVERIFIED") : ISemanticChanceEventV111
{
    public const string EventSchemaId =
        "sts2-v111-semantic-draw-insertion-v1";
    public string SiteId => Request.SiteId;
    public string OperationKind => Request.OperationKind;
    public string StreamId => Request.StreamId;
    public string OwnerId => Request.OwnerId;
    public int Ordinal => Request.Ordinal;

    public SemanticDrawInsertionEventV111 Fork() => this with
    {
        Request = Request with
        {
            OrderedCandidates = (int[])Request.OrderedCandidates.Clone()
        },
        EligibleDrawPileInstanceIds =
            (long[])EligibleDrawPileInstanceIds.Clone()
    };

    public void Validate()
    {
        Request.Validate();
        if (SchemaId != EventSchemaId ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(GeneratedCardModelId) ||
            Request.SiteId != "combat.draw_pile.random_insert_index" ||
            Request.StreamId != "prototype.combat" ||
            Request.OperationKind != "bounded_integer" ||
            EligibleDrawPileInstanceIds is null ||
            EligibleDrawPileInstanceIds.Distinct().Count() !=
                EligibleDrawPileInstanceIds.Length ||
            Request.SupportCount != EligibleDrawPileInstanceIds.Length + 1 ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic draw-insertion event.");
        var expectedSupportFingerprint = CanonicalJson.Sha256(new
        {
            Request, GeneratedCardModelId,
            EligibleDrawPileInstanceIds
        });
        if (SupportFingerprint != expectedSupportFingerprint)
            throw new InvalidDataException(
                "Draw-insertion support fingerprint is inconsistent.");
        var expectedEventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint,
            SupportFingerprint
        });
        if (EventId != expectedEventId)
            throw new InvalidDataException(
                "Draw-insertion event ID is inconsistent.");
    }
}

/// <summary>
/// SelectedPosition is the source insertion index in CombatState's
/// bottom-first DrawPile array. Provider-owned semantic deck slots are
/// top-first, so the engine converts this position when committing it.
/// </summary>
public sealed record SemanticDrawInsertionDecisionV111(
    SemanticDrawInsertionEventV111 Event, int SelectedPosition)
{
    public SemanticDrawInsertionDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (SelectedPosition < 0 ||
            SelectedPosition >= Event.Request.SupportCount)
            throw new InvalidDataException(
                "Draw insertion position is outside its bounded support.");
    }
}
