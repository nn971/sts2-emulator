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

    public SemanticCardTransformEventV111 Fork() => this with
    {
        CandidateCardIds = (string[])CandidateCardIds.Clone()
    };

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
        Event = Event.Fork()
    };
}

public static class SemanticCardTransformChoiceV111
{
    internal static void ValidateBoundary(
        RunState run, PrototypeControlledCardTransformV111 transform)
    {
        var combat = run.World?.Combat;
        if (run.Phase != RunPhase.Combat || combat is null ||
            combat.PendingChoice is not
                { ChoiceId: "controlled_card_transform" } ||
            !PrototypeGameEngine.ValidateControlledCardTransform(
                combat, transform))
            throw new InvalidDataException(
                "Card-transform pool is not source-reachable.");
        if (combat.SemanticDeck is { } deck)
        {
            SemanticDeckEngineV111.Validate(deck);
            if (deck.Mode != SemanticDeckStateV111.NatureMode ||
                deck.Pending is not null ||
                !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                    .Order().SequenceEqual(combat.DrawPile.Order()))
                throw new InvalidDataException(
                    "Card-transform continuation diverged from its semantic draw pile.");
        }
    }

    public static SemanticCardTransformDecisionV111 Select(
        SemanticCardTransformEventV111 pending, string eventId,
        string cardId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Card-transform event ID is stale or mismatched.");
        var index = Array.IndexOf(pending.CandidateCardIds, cardId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card model is outside the pending transform support.");
        var decision = new SemanticCardTransformDecisionV111(
            pending.Fork(), index, cardId);
        decision.Validate();
        return decision;
    }

    /// <summary>Revalidate the live replacement pool and apply one outcome
    /// through the ordinary controlled transform continuation.</summary>
    internal static RunState Resume(
        RunState checkpoint, SemanticCardTransformDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Card-transform choice lost its source combat.");
        var transform = combat.PendingChoice?.ControlledCardTransform ??
            throw new InvalidDataException(
                "Card-transform choice lost its suspended operation.");
        ValidateBoundary(checkpoint, transform);
        var sourceCardInstanceId = transform.SelectedCardInstanceIds[
            transform.NextTransformIndex];
        var sourceCard = combat.Cards.Single(card =>
            card.InstanceId == sourceCardInstanceId);
        if (decision.Event.SourceCardInstanceId != sourceCardInstanceId ||
            decision.Event.OriginalCardId != sourceCard.CardId ||
            !transform.CandidateCardIds.SequenceEqual(
                decision.Event.CandidateCardIds))
            throw new InvalidDataException(
                "Card-transform decision no longer matches its source pool.");

        RunState resumed;
        using (ControlledChanceGuardV111.Enter())
            resumed = PrototypeGameEngine.ResumeControlledCardTransform(
                checkpoint.Fork(), decision.SelectedIndex);
        if (resumed.Phase == RunPhase.Combat)
            SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(
                resumed);
        else if (resumed.Phase is not (RunPhase.Reward or RunPhase.Terminal))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_TRANSFORM " +
                "site=combat.card_transform " +
                "operation=unsupported_post_transform_phase");
        return resumed;
    }
}
