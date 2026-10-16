namespace Sts2Emulator.Core;

/// <summary>A live random-enemy support at the source effect boundary.</summary>
public sealed record SemanticTargetEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    int[] CandidateEnemyInstanceIds, string SupportFingerprint,
    string ProbabilityStatus = "UNVERIFIED", string TargetKind = "effect_random_enemy")
    : ISemanticChanceEventV111
{
    public void Validate()
    {
        if (SchemaId != SemanticTargetChoiceV111.SchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            SiteId is not ("combat.effect.random_enemy" or
                "combat.sly_autoplay.random_enemy") ||
            StreamId != "run.combat_targets" ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_enemy_instance" ||
            CandidateEnemyInstanceIds is not { Length: > 0 } ||
            CandidateEnemyInstanceIds.Distinct().Count() !=
                CandidateEnemyInstanceIds.Length ||
            SupportFingerprint != CanonicalJson.Sha256(
                CandidateEnemyInstanceIds) ||
            ProbabilityStatus != "UNVERIFIED" ||
            TargetKind is not ("effect_random_enemy" or "sly_auto_play") ||
            (TargetKind == "sly_auto_play") !=
                (SiteId == "combat.sly_autoplay.random_enemy"))
            throw new InvalidDataException("Invalid semantic target event.");
    }
}

/// <summary>Typed result for one random target selection.</summary>
public sealed record SemanticTargetDecisionV111(
    SemanticTargetEventV111 Event, int SelectedIndex,
    int SelectedEnemyInstanceId)
{
    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateEnemyInstanceIds.Length ||
            Event.CandidateEnemyInstanceIds[SelectedIndex] !=
                SelectedEnemyInstanceId)
            throw new InvalidDataException(
                "Invalid semantic target event or outcome.");
    }

    public SemanticTargetDecisionV111 Fork() => this with
    {
        Event = Event with
        {
            CandidateEnemyInstanceIds =
                (int[])Event.CandidateEnemyInstanceIds.Clone()
        }
    };
}

public static class SemanticTargetChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-target-choice-v1";
}
