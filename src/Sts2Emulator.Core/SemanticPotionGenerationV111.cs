namespace Sts2Emulator.Core;

/// <summary>Ordered source pool for one random combat-potion model choice.</summary>
public sealed record SemanticPotionGenerationEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    string[] CandidatePotionIds, string SupportFingerprint,
    string ProbabilityStatus = "UNVERIFIED") : ISemanticChanceEventV111
{
    public void Validate()
    {
        if (SchemaId != SemanticPotionGenerationChoiceV111.SchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            SiteId != "combat.potion_generation.random_model" ||
            StreamId != "prototype.combat" ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_potion_model" ||
            CandidatePotionIds is not { Length: > 0 } ||
            CandidatePotionIds.Any(string.IsNullOrWhiteSpace) ||
            CandidatePotionIds.Distinct(StringComparer.Ordinal).Count() !=
                CandidatePotionIds.Length ||
            SupportFingerprint != CanonicalJson.Sha256(CandidatePotionIds) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic potion-generation event.");
    }
}

public sealed record SemanticPotionGenerationDecisionV111(
    SemanticPotionGenerationEventV111 Event, int SelectedIndex,
    string SelectedPotionId)
{
    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidatePotionIds.Length ||
            Event.CandidatePotionIds[SelectedIndex] != SelectedPotionId)
            throw new InvalidDataException(
                "Potion-generation outcome is outside its ordered source pool.");
    }

    public SemanticPotionGenerationDecisionV111 Fork() => this with
    {
        Event = Event with
        {
            CandidatePotionIds = (string[])Event.CandidatePotionIds.Clone()
        }
    };
}

public static class SemanticPotionGenerationChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-potion-generation-v1";
}
