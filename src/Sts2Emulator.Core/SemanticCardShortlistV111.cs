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
        var expectedOperationKind =
            SiteId == "combat.draw_pile.random_replay_card.select"
                ? "choose_card_instance"
                : "choose_card_instance_without_replacement";
        if (SchemaId != EventSchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            SiteId is not ("combat.card_selection.random_shortlist" or
                "combat.autoplay.random_card.select" or
                "combat.draw_pile.random_replay_card.select") ||
            StreamId != "prototype.combat" ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != expectedOperationKind ||
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
            (SiteId == "combat.draw_pile.random_replay_card.select" &&
             RequiredCount != 1) ||
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

public static class SemanticCardShortlistChoiceV111
{
    public static SemanticCardShortlistDecisionV111 Select(
        SemanticCardShortlistEventV111 pending, string eventId,
        long cardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Card-shortlist event ID is stale or mismatched.");
        var index = Array.IndexOf(
            pending.CandidateCardInstanceIds, cardInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card is outside the pending shortlist support.");
        var decision = new SemanticCardShortlistDecisionV111(
            pending.Fork(), index, cardInstanceId,
            pending.CandidateCardModelIds[index]);
        decision.Validate();
        return decision;
    }

    /// <summary>Revalidate the live ordered shortlist and resume the saved
    /// selection, replay, or autoplay operation.</summary>
    internal static RunState Resume(
        RunState checkpoint, SemanticCardShortlistDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Card shortlist lost its source combat.");
        var choice = combat.PendingChoice;
        var shortlist = choice?.ControlledCardShortlist;
        if (shortlist is null ||
            !PrototypeGameEngine.ValidateControlledCardShortlist(
                combat, shortlist))
            throw new InvalidDataException(
                "Card shortlist is not source-reachable.");
        SemanticPilotCombatEngineV111.ValidateCardShortlistBoundary(
            checkpoint);
        ValidateLiveSupport(combat, shortlist, decision.Event);
        var randomAutoplay = shortlist.Operation.Kind ==
            PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone;

        RunState resumed;
        using (ControlledChanceGuardV111.Enter())
            resumed = PrototypeGameEngine.ResumeControlledCardShortlist(
                checkpoint.Fork(), decision.SelectedIndex);
        if (randomAutoplay && resumed.Phase is
            (RunPhase.Reward or RunPhase.Terminal))
        {
            PrototypeStateInvariants.Validate(resumed);
            return resumed;
        }
        var nextCombat = resumed.World?.Combat;
        if (resumed.Phase != RunPhase.Combat || nextCombat is null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_SHORTLIST " +
                "site=combat.card_selection.random_shortlist " +
                "operation=unsupported_post_shortlist_phase");
        if (nextCombat.PendingChoice?.ControlledCardShortlist is not null)
            SemanticPilotCombatEngineV111.ValidateCardShortlistBoundary(
                resumed);
        else if (nextCombat.PendingChoice?.ChoiceId == "select_cards")
            SemanticCardSelectionEngineV111.ValidatePendingBoundary(resumed);
        else
            SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(
                resumed);
        if (!randomAutoplay &&
            (!combat.DrawPile.SequenceEqual(nextCombat.DrawPile) ||
             CanonicalJson.Sha256(combat.SemanticDeck) !=
                 CanonicalJson.Sha256(nextCombat.SemanticDeck)))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK " +
                "site=combat.card_selection.random_shortlist " +
                "operation=unmodeled_draw_pile_mutation");
        if (randomAutoplay)
            PrototypeStateInvariants.Validate(resumed);
        return resumed;
    }

    private static void ValidateLiveSupport(
        CombatState combat,
        PrototypeControlledCardShortlistV111 shortlist,
        SemanticCardShortlistEventV111 pending)
    {
        var eligibleIds = shortlist.OriginalCandidateCardInstanceIds;
        var candidateIds = shortlist.RemainingCandidateCardInstanceIds;
        var selectedIds = shortlist.SelectedCandidateCardInstanceIds;
        var eligibleModels = eligibleIds.Select(id => combat.Cards.Single(
            card => card.InstanceId == id).CardId).ToArray();
        var candidateModels = candidateIds.Select(id => combat.Cards.Single(
            card => card.InstanceId == id).CardId).ToArray();
        var siteId = shortlist.Operation.Kind switch
        {
            PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone =>
                "combat.autoplay.random_card.select",
            PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay =>
                "combat.draw_pile.random_replay_card.select",
            _ => "combat.card_selection.random_shortlist"
        };
        if (pending.SiteId != siteId ||
            pending.RequiredCount != shortlist.RequiredCount ||
            !eligibleIds.SequenceEqual(pending.EligibleCardInstanceIds) ||
            !eligibleModels.SequenceEqual(pending.EligibleCardModelIds) ||
            !selectedIds.SequenceEqual(pending.SelectedCardInstanceIds) ||
            !candidateIds.SequenceEqual(pending.CandidateCardInstanceIds) ||
            !candidateModels.SequenceEqual(pending.CandidateCardModelIds))
            throw new InvalidDataException(
                "Card-shortlist decision no longer matches its live support.");
    }
}
