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
        var standardSite = Request.SiteId ==
            "combat.draw_pile.random_insert_index";
        var standardOwnerMatchesStream =
            Request.StreamId == "prototype.combat"
                ? Request.OwnerId == "prototype.combat"
                : IsNativePositionOwner(Request.OwnerId);
        var supportedSite = standardSite &&
                standardOwnerMatchesStream ||
            Request.SiteId ==
                "combat.relic.after_shuffle.generated_card.random_insert_index" &&
                Request.OwnerId.StartsWith("relic:", StringComparison.Ordinal) &&
                Request.OwnerId.Contains(":application:",
                    StringComparison.Ordinal) &&
                HasInsertedCardAndPile(Request.OwnerId) ||
            Request.SiteId ==
                PrototypeControlledDrawInsertionV111.PersonalHiveDazedSiteId &&
                Request.OwnerId.StartsWith("enemy:", StringComparison.Ordinal) &&
                Request.OwnerId.Contains(":power:personal_hive:source:",
                    StringComparison.Ordinal) &&
                HasInsertedCardAndPile(Request.OwnerId) &&
                GeneratedCardModelId == "proto.status.dazed";
        if (SchemaId != EventSchemaId ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(GeneratedCardModelId) ||
            !supportedSite ||
            Request.StreamId is not ("prototype.combat" or "run.shuffle") ||
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

    private static bool IsNativePositionOwner(string ownerId) =>
        (ownerId.StartsWith("card_instance:", StringComparison.Ordinal) ||
         ownerId.StartsWith("effect:", StringComparison.Ordinal)) &&
        HasInsertedCardAndPile(ownerId);

    private static bool HasInsertedCardAndPile(string ownerId) =>
        ownerId.Contains(":card:", StringComparison.Ordinal) &&
        ownerId.Contains(":draw_pile:", StringComparison.Ordinal);
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
