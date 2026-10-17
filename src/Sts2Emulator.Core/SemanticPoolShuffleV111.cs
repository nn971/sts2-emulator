namespace Sts2Emulator.Core;

/// <summary>One bounded source swap in a full combat-pool shuffle.</summary>
public sealed record SemanticPoolShuffleEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, RandomIndexRequestV111 Request,
    string[] OriginalCardIds, string[] CurrentCardIds,
    int NextSwapIndex, int SelectedCount, string SourceOperationKind,
    long? SourceCardInstanceId, string ProbabilityStatus = "UNVERIFIED")
    : ISemanticChanceEventV111
{
    public const string EventSchemaId =
        "sts2-v111-semantic-pool-shuffle-v1";
    public string SiteId => Request.SiteId;
    public string StreamId => Request.StreamId;
    public string OwnerId => Request.OwnerId;
    public int Ordinal => Request.Ordinal;
    public string OperationKind => Request.OperationKind;

    public SemanticPoolShuffleEventV111 Fork() => this with
    {
        Request = Request with
        {
            OrderedCandidates = (int[])Request.OrderedCandidates.Clone()
        },
        OriginalCardIds = (string[])OriginalCardIds.Clone(),
        CurrentCardIds = (string[])CurrentCardIds.Clone()
    };

    public void Validate()
    {
        Request.Validate();
        var expectedSite = SourceOperationKind ==
                PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand.ToString()
            ? "combat.draw_pile.random_rare_to_hand.shuffle_swap_index"
            : SourceOperationKind ==
                PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw.ToString()
                ? "combat.draw.recycle.shuffle_swap_index"
            : "combat.generation.full_pool_shuffle.swap_index";
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            Request.SiteId is not (
                "combat.generation.full_pool_shuffle.swap_index" or
                "combat.draw_pile.random_rare_to_hand.shuffle_swap_index" or
                "combat.draw.recycle.shuffle_swap_index") ||
            Request.SiteId != expectedSite ||
            Request.OperationKind != "bounded_integer" ||
            OriginalCardIds is not { Length: > 1 } ||
            CurrentCardIds is null ||
            CurrentCardIds.Length != OriginalCardIds.Length ||
            OriginalCardIds.Any(string.IsNullOrWhiteSpace) ||
            OriginalCardIds.Distinct(StringComparer.Ordinal).Count() !=
                OriginalCardIds.Length ||
            !CurrentCardIds.Order(StringComparer.Ordinal).SequenceEqual(
                OriginalCardIds.Order(StringComparer.Ordinal)) ||
            NextSwapIndex < 1 || NextSwapIndex >= OriginalCardIds.Length ||
            Request.SupportCount != NextSwapIndex + 1 ||
            SelectedCount <= 0 || SelectedCount > OriginalCardIds.Length ||
            string.IsNullOrWhiteSpace(SourceOperationKind) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic full-pool shuffle event.");
        var expectedSupportFingerprint = CanonicalJson.Sha256(new
        {
            Request,
            Original = OriginalCardIds,
            Current = CurrentCardIds,
            NextSwapIndex,
            SelectedCount,
            Operation = SourceOperationKind,
            SourceCardInstanceId
        });
        if (SupportFingerprint != expectedSupportFingerprint)
            throw new InvalidDataException(
                "Full-pool shuffle support fingerprint is inconsistent.");
        var expectedEventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint,
            SupportFingerprint
        });
        if (EventId != expectedEventId)
            throw new InvalidDataException(
                "Full-pool shuffle event ID is inconsistent.");
    }
}

public sealed record SemanticPoolShuffleDecisionV111(
    SemanticPoolShuffleEventV111 Event, int SelectedSwapIndex)
{
    public SemanticPoolShuffleDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (SelectedSwapIndex < 0 ||
            SelectedSwapIndex > Event.NextSwapIndex)
            throw new InvalidDataException(
                "Full-pool shuffle swap is outside its bounded support.");
    }
}
