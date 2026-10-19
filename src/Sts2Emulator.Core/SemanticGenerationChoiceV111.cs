namespace Sts2Emulator.Core;

/// <summary>An ordered generated-card model pool at its source effect boundary.</summary>
public sealed record SemanticGeneratedCardEventV111(
    string SchemaId, string EventId, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    string[] CandidateCardIds, string SupportFingerprint,
    string ProbabilityStatus = "UNVERIFIED")
    : ISemanticChanceEventV111
{
    public SemanticGeneratedCardEventV111 Fork() => this with
    {
        CandidateCardIds = (string[])CandidateCardIds.Clone()
    };

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

    internal static void ValidateBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var generation = choice?.ControlledGeneration;
        if (run.Phase != RunPhase.Combat ||
            combat is not { PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is null &&
             !combat.IsPlayerTurn) ||
            choice is not { ChoiceId: "controlled_generated_card",
                OuterChoiceContinuation: not null } ||
            generation is null ||
            !PrototypeGameEngine.ValidateControlledGenerationSupport(generation))
            throw new InvalidDataException(
                "Semantic generated-card pool is not source-reachable.");
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Generated-card continuation lost its provider-owned deck.",
            "Generated-card continuation changed the hidden draw pile.");
        PrototypeStateInvariants.Validate(run);
    }

    public static SemanticGeneratedCardDecisionV111 Select(
        SemanticGeneratedCardEventV111 pending, string eventId,
        string cardId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Generated-card event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateCardIds, cardId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card model is outside the pending generated-card support.");
        var decision = new SemanticGeneratedCardDecisionV111(
            pending.Fork(), index, cardId);
        decision.Validate();
        return decision;
    }

    /// <summary>
    /// Revalidates the source pool and resumes either ordinary card
    /// generation or the card-model stage of draw-pile insertion.
    /// </summary>
    internal static RunState Resume(
        RunState checkpoint, SemanticGeneratedCardDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();

        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Generated-card choice lost its source combat.");
        var choice = combat.PendingChoice;
        var insertion = choice?.ControlledDrawInsertion;
        bool drawInsertionModelChoice = decision.Event.SiteId ==
            "combat.random_draw_attack.card_model";
        if (drawInsertionModelChoice)
        {
            if (choice is not { ChoiceId: "controlled_draw_insertion" } ||
                insertion is not { Stage: "card_model" } insertionChoice ||
                !insertionChoice.CandidateCardIds.SequenceEqual(
                    decision.Event.CandidateCardIds))
                throw new InvalidDataException(
                    "Generated-card decision no longer matches its insertion pool.");
            SemanticDrawInsertionChoiceV111.ValidateBoundary(checkpoint);
        }
        else
        {
            if (decision.Event.SiteId != "combat.generated_card.select" ||
                choice is not { ChoiceId: "controlled_generated_card" } ||
                choice.ControlledGeneration is not { } generation ||
                !generation.CandidateCardIds.SequenceEqual(
                    decision.Event.CandidateCardIds))
                throw new InvalidDataException(
                    "Generated-card decision no longer matches its source pool.");
            ValidateBoundary(checkpoint);
        }

        RunState resumed;
        using (ControlledChanceGuardV111.Enter())
        using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            resumed = drawInsertionModelChoice
                ? PrototypeGameEngine.ResumeControlledDrawInsertion(
                    checkpoint.Fork(), decision.SelectedIndex)
                : PrototypeGameEngine.ResumeControlledGeneratedCard(
                    checkpoint.Fork(), decision.SelectedIndex);
        if (resumed.Phase == RunPhase.Combat)
        {
            SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(
                resumed);
            var nextCombat = resumed.World?.Combat ??
                throw new InvalidDataException(
                    "Generated-card continuation lost its resulting combat.");
            SemanticDrawPileAdapterV111.RequireUnchangedDrawPile(
                combat, nextCombat,
                "UNSUPPORTED_SEMANTIC_DECK site=combat.generated_card " +
                "operation=unmodeled_draw_pile_mutation");
        }
        else if (!PrototypeGameEngine
                     .IsAtCombatRewardOrTerminalBoundary(resumed))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.generated_card " +
                "operation=unsupported_post_generation_phase");
        return resumed;
    }
}
