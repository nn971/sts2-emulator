namespace Sts2Emulator.Core;

/// <summary>An ordered generated-card model pool at its source effect boundary.</summary>
public sealed record SemanticGeneratedCardEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    string[] CandidateCardIds, string SupportFingerprint,
    string ProbabilityStatus = "UNVERIFIED")
    : ISemanticChanceEventV111
{
    public void Validate()
    {
        if (SchemaId != SemanticGenerationChoiceV111.SchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            SiteId is not ("combat.generated_card.select" or
                "combat.random_draw_attack.card_model") ||
            StreamId != "prototype.combat" ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_card_model" ||
            CandidateCardIds is not { Length: > 0 } ||
            CandidateCardIds.Any(string.IsNullOrWhiteSpace) ||
            CandidateCardIds.Distinct(StringComparer.Ordinal).Count() !=
                CandidateCardIds.Length ||
            SupportFingerprint != CanonicalJson.Sha256(CandidateCardIds) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic generated-card event.");
    }
}

/// <summary>Typed result for one without-replacement generated-card pick.</summary>
public sealed record SemanticGeneratedCardDecisionV111(
    SemanticGeneratedCardEventV111 Event, int SelectedIndex,
    string SelectedCardId)
{
    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateCardIds.Length ||
            Event.CandidateCardIds[SelectedIndex] != SelectedCardId)
            throw new InvalidDataException(
                "Generated-card outcome is outside its ordered source pool.");
    }

    public SemanticGeneratedCardDecisionV111 Fork() => this with
    {
        Event = Event with
        {
            CandidateCardIds = (string[])Event.CandidateCardIds.Clone()
        }
    };
}

public static class SemanticGenerationChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-generation-choice-v1";
}
