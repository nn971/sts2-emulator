namespace Sts2Emulator.Core;

/// <summary>Ordered replacement pool for one selected combat-card transform.</summary>
public sealed record SemanticCardTransformEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    long SourceCardInstanceId, string OriginalCardId,
    string[] CandidateCardIds, string SupportFingerprint,
    string ProbabilityStatus = "UNVERIFIED") : ISemanticChanceEventV111
{
    public const string EventSchemaId =
        "sts2-v111-semantic-card-transform-v1";

    public void Validate()
    {
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            SiteId != "combat.card_transform.replacement.index_draw" ||
            StreamId != "prototype.combat" ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_card_model" ||
            SourceCardInstanceId <= 0 || string.IsNullOrWhiteSpace(OriginalCardId) ||
            CandidateCardIds is not { Length: > 0 } ||
            CandidateCardIds.Any(string.IsNullOrWhiteSpace) ||
            CandidateCardIds.Distinct(StringComparer.Ordinal).Count() !=
                CandidateCardIds.Length ||
            SupportFingerprint != CanonicalJson.Sha256(CandidateCardIds) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic card-transform event.");
    }
}

public sealed record SemanticCardTransformDecisionV111(
    SemanticCardTransformEventV111 Event, int SelectedIndex,
    string SelectedCardId)
{
    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateCardIds.Length ||
            Event.CandidateCardIds[SelectedIndex] != SelectedCardId)
            throw new InvalidDataException(
                "Card-transform outcome is outside its ordered source pool.");
    }

    public SemanticCardTransformDecisionV111 Fork() => this with
    {
        Event = Event with
        {
            CandidateCardIds = (string[])Event.CandidateCardIds.Clone()
        }
    };
}
