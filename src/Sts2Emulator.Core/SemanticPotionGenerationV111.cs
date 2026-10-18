namespace Sts2Emulator.Core;

/// <summary>Ordered source pool for one random combat-potion model choice.</summary>
public sealed record SemanticPotionGenerationEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    string[] CandidatePotionIds, string SupportFingerprint,
    string ProbabilityStatus = "UNVERIFIED") : ISemanticChanceEventV111
{
    public SemanticPotionGenerationEventV111 Fork() => this with
    {
        CandidatePotionIds = (string[])CandidatePotionIds.Clone()
    };

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
        Event = Event.Fork()
    };
}

public static class SemanticPotionGenerationChoiceV111
{
    public const string SchemaId = "sts2-v111-semantic-potion-generation-v1";

    public static SemanticPotionGenerationDecisionV111 Select(
        SemanticPotionGenerationEventV111 pending, string eventId,
        string potionId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(potionId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Potion-generation event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidatePotionIds, potionId);
        if (index < 0)
            throw new InvalidOperationException(
                "Potion model is outside the pending source pool.");
        var decision = new SemanticPotionGenerationDecisionV111(
            pending.Fork(), index, potionId);
        decision.Validate();
        return decision;
    }

    /// <summary>Resume the source generation effect under the shared engine
    /// continuation, preserving the hidden draw-pile state.</summary>
    internal static RunState Resume(
        RunState checkpoint,
        SemanticPotionGenerationDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Potion-generation choice lost its source combat.");
        var generation = combat.PendingChoice?.ControlledPotionGeneration;
        if (generation is null ||
            !generation.CandidatePotionIds.SequenceEqual(
                decision.Event.CandidatePotionIds))
            throw new InvalidDataException(
                "Potion-generation decision no longer matches its source pool.");
        SemanticPilotCombatEngineV111.ValidatePotionGenerationBoundary(
            checkpoint);

        RunState resumed;
        using (ControlledChanceGuardV111.Enter())
            resumed = PrototypeGameEngine.ResumeControlledPotionGeneration(
                checkpoint.Fork(), decision.SelectedIndex);
        if (resumed.Phase != RunPhase.Combat)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_POTION site=combat.potion_generation " +
                "operation=unsupported_post_generation_phase");
        SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(resumed);
        var nextCombat = resumed.World?.Combat ??
            throw new InvalidDataException(
                "Potion-generation continuation lost its resulting combat.");
        if (!combat.DrawPile.SequenceEqual(nextCombat.DrawPile) ||
            CanonicalJson.Sha256(combat.SemanticDeck) !=
                CanonicalJson.Sha256(nextCombat.SemanticDeck))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.potion_generation " +
                "operation=unmodeled_draw_pile_mutation");
        return resumed;
    }
}
