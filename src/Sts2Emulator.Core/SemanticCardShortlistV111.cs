namespace Sts2Emulator.Core;

/// <summary>One pick in a source random pile shortlist, sampled without
/// replacement and in the order the source offer will present it.</summary>
public sealed record SemanticCardShortlistEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    long[] EligibleCardInstanceIds, string[] EligibleCardModelIds,
    long[] SelectedCardInstanceIds, long[] CandidateCardInstanceIds,
    string[] CandidateCardModelIds, int RequiredCount,
    string ProbabilityStatus = "UNVERIFIED")
    : ISemanticChanceEventV111
{
    public const string EventSchemaId =
        "sts2-v111-semantic-card-shortlist-v1";

    public SemanticCardShortlistEventV111 Fork() => this with
    {
        EligibleCardInstanceIds = (long[])EligibleCardInstanceIds.Clone(),
        EligibleCardModelIds = (string[])EligibleCardModelIds.Clone(),
        SelectedCardInstanceIds = (long[])SelectedCardInstanceIds.Clone(),
        CandidateCardInstanceIds = (long[])CandidateCardInstanceIds.Clone(),
        CandidateCardModelIds = (string[])CandidateCardModelIds.Clone()
    };

    public void Validate()
    {
        var remaining = EligibleCardInstanceIds is null ||
                SelectedCardInstanceIds is null
            ? null
            : EligibleCardInstanceIds
                .Where(id => !SelectedCardInstanceIds.Contains(id)).ToArray();
        if (SchemaId != EventSchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            SiteId is not ("combat.card_selection.random_shortlist" or
                "combat.autoplay.random_card.select") ||
            StreamId != "prototype.combat" ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_card_instance_without_replacement" ||
            EligibleCardInstanceIds is not { Length: > 1 } ||
            EligibleCardModelIds is null ||
            EligibleCardModelIds.Length != EligibleCardInstanceIds.Length ||
            EligibleCardModelIds.Any(string.IsNullOrWhiteSpace) ||
            SelectedCardInstanceIds is null ||
            CandidateCardInstanceIds is not { Length: > 0 } ||
            CandidateCardModelIds is null ||
            CandidateCardModelIds.Length != CandidateCardInstanceIds.Length ||
            CandidateCardModelIds.Any(string.IsNullOrWhiteSpace) ||
            EligibleCardInstanceIds.Any(id => id <= 0) ||
            EligibleCardInstanceIds.Distinct().Count() !=
                EligibleCardInstanceIds.Length ||
            SelectedCardInstanceIds.Distinct().Count() !=
                SelectedCardInstanceIds.Length ||
            SelectedCardInstanceIds.Any(id =>
                !EligibleCardInstanceIds.Contains(id)) ||
            !CandidateCardInstanceIds.SequenceEqual(remaining!) ||
            CandidateCardInstanceIds.Any(id => id <= 0) ||
            CandidateCardInstanceIds.Distinct().Count() !=
                CandidateCardInstanceIds.Length ||
            RequiredCount < 1 ||
            RequiredCount >= EligibleCardInstanceIds.Length ||
            SelectedCardInstanceIds.Length >= RequiredCount ||
            ProbabilityStatus != "UNVERIFIED" ||
            SupportFingerprint != CanonicalJson.Sha256(new
            {
                eligibleCardInstanceIds = EligibleCardInstanceIds,
                eligibleCardModelIds = EligibleCardModelIds,
                selectedCardInstanceIds = SelectedCardInstanceIds,
                candidateCardInstanceIds = CandidateCardInstanceIds,
                candidateCardModelIds = CandidateCardModelIds,
                requiredCount = RequiredCount
            }) ||
            EventId != CanonicalJson.Sha256(new
            {
                SchemaId,
                BoundaryFingerprint,
                SupportFingerprint
            }))
            throw new InvalidDataException(
                "Invalid semantic card-shortlist event.");
    }
}

public sealed record SemanticCardShortlistDecisionV111(
    SemanticCardShortlistEventV111 Event, int SelectedIndex,
    long SelectedCardInstanceId, string SelectedCardModelId)
{
    public SemanticCardShortlistDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateCardInstanceIds.Length ||
            Event.CandidateCardInstanceIds[SelectedIndex] !=
                SelectedCardInstanceId ||
            Event.CandidateCardModelIds[SelectedIndex] !=
                SelectedCardModelId)
            throw new InvalidDataException(
                "Card-shortlist decision is outside its ordered support.");
    }
}
