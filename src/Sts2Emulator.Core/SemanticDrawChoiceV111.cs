namespace Sts2Emulator.Core;

/// <summary>
/// Owns selection validation and the paired semantic-deck/physical-pile
/// commit for a deferred draw-card identity.
/// </summary>
public static class SemanticDrawChoiceV111
{
    internal static void ValidateBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var draw = choice?.ControlledSemanticDraw;
        var deck = combat?.SemanticDeck;
        if (run.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is not null &&
             (choice is null ||
              !PrototypeGameEngine.IsMayhemSemanticDrawContinuation(
                  combat, choice))) ||
            choice is not { ChoiceId: "controlled_semantic_draw",
                ControlledSemanticDraw: not null,
                OuterChoiceContinuation: not null,
                ControlledTarget: null, ControlledGeneration: null,
                ControlledDrawInsertion: null, ControlledPoolShuffle: null,
                ControlledRandomCost: null, ControlledDrawCost: null,
                ControlledCardShortlist: null,
                ControlledRandomPileCard: null } ||
            draw is null || draw.RemainingCount <= 0 || draw.EventDepth < 0 ||
            deck is not { Mode: SemanticDeckStateV111.NatureMode,
                Pending: not null })
            throw new InvalidDataException(
                "Card-effect draw is not at a source-reachable Nature boundary.");
        SemanticDeckEngineV111.Validate(deck);
        if (!SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(combat.DrawPile.Order()) ||
            SemanticDeckEngineV111.Inspect(deck) is null)
            throw new InvalidDataException(
                "Card-effect draw diverged from its semantic pile state.");
        PrototypeStateInvariants.Validate(run);
    }

    public static SemanticDrawDecisionV111 Select(
        SemanticDeckEventV111 pending, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ValidatePending(pending);
        if (selectedIndex < 0 ||
            selectedIndex >= pending.CandidateCardInstanceIds.Length)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));

        var decision = new SemanticDrawDecisionV111(
            pending with
            {
                CandidateCardInstanceIds =
                    (long[])pending.CandidateCardInstanceIds.Clone()
            }, selectedIndex,
            pending.CandidateCardInstanceIds[selectedIndex]);
        decision.Validate();
        return decision;
    }

    public static SemanticDrawDecisionV111 Select(
        SemanticDeckEventV111 pending, string eventId,
        long cardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ValidatePending(pending);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Semantic draw event ID is stale or mismatched.");
        var index = Array.IndexOf(
            pending.CandidateCardInstanceIds, cardInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card instance is outside the semantic draw support.");
        return Select(pending, index);
    }

    private static void ValidatePending(SemanticDeckEventV111 pending)
    {
        if (pending.SchemaId != SemanticDeckStateV111.SchemaId ||
            string.IsNullOrWhiteSpace(pending.EventId) ||
            pending.SiteId != "combat.draw_pile.draw_card" ||
            pending.Purpose != "draw" || pending.TopOffset != 0 ||
            pending.Ordinal < 0 ||
            pending.OperationKind != "choose_card_instance" ||
            pending.StreamId != "semantic.draw_pile" ||
            pending.OwnerId != "player:draw_pile" ||
            pending.CandidateCardInstanceIds is not { Length: > 0 } ||
            pending.SupportFingerprint != CanonicalJson.Sha256(
                pending.CandidateCardInstanceIds) ||
            pending.ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic draw-card request.");
    }

    internal static void ValidateDecision(
        SemanticDeckEventV111? pending,
        SemanticDrawDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (pending is null ||
            CanonicalJson.Sha256(decision.Event) !=
                CanonicalJson.Sha256(pending) ||
            !decision.Event.CandidateCardInstanceIds.SequenceEqual(
                pending.CandidateCardInstanceIds) ||
            decision.SelectedIndex < 0 ||
            decision.SelectedIndex >= pending.CandidateCardInstanceIds.Length ||
            pending.CandidateCardInstanceIds[decision.SelectedIndex] !=
                decision.SelectedCardInstanceId)
            throw new InvalidOperationException(
                "Semantic draw decision is outside its pending event support.");
    }

    /// <summary>
    /// Revalidates the saved request at the mechanics checkpoint, resolves
    /// its provider state, and publishes the matching physical pile update.
    /// </summary>
    internal static CombatState Commit(
        CombatState checkpoint, SemanticDrawDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (checkpoint.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode,
                  Pending: { Purpose: "draw" } } deck ||
            !SemanticDeckEngineV111.AllCardInstanceIds(deck)
                .Order().SequenceEqual(checkpoint.DrawPile.Order()))
            throw new InvalidDataException(
                "Semantic draw choice lost its Nature deck checkpoint.");
        var current = SemanticDeckEngineV111.Inspect(deck) ??
            throw new InvalidDataException(
                "Semantic draw choice lost its pending request.");
        if (CanonicalJson.Sha256(current) !=
            CanonicalJson.Sha256(decision.Event))
            throw new InvalidDataException(
                "Semantic draw choice request changed before commit.");

        var resolvedDeck = SemanticDeckEngineV111.Resolve(
            deck, deck.Fingerprint, decision.SelectedIndex);
        return SemanticDrawPileAdapterV111.ApplyCommittedPhysicalMutation(
            checkpoint, checkpoint.DrawPile, resolvedDeck);
    }
}
