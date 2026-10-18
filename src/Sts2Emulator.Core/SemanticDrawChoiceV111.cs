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
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Card-effect draw is not at a source-reachable Nature boundary.",
            "Card-effect draw diverged from its semantic pile state.",
            allowPending: true);
        if (SemanticDeckEngineV111.Inspect(deck) is null)
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
    /// Confirms that a resumed draw recorded the selected identity for the
    /// exact request and candidate index supplied to its continuation.
    /// </summary>
    internal static SemanticDrawDecisionV111 ValidateRecordedChoice(
        RunState run, SemanticDeckEventV111 pending, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(pending);
        ValidatePending(pending);
        if (selectedIndex < 0 ||
            selectedIndex >= pending.CandidateCardInstanceIds.Length)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));

        var deck = run.World?.Combat?.SemanticDeck ??
            throw new InvalidDataException(
                "Semantic draw continuation lost its provider-owned deck.");
        var recorded = deck.Transcript?.SingleOrDefault(choice =>
            choice.EventId == pending.EventId) ??
            throw new InvalidDataException(
                "Semantic draw continuation did not record its source event.");
        if (recorded.SiteId != pending.SiteId ||
            recorded.Purpose != pending.Purpose ||
            recorded.TopOffset != pending.TopOffset ||
            recorded.Ordinal != pending.Ordinal ||
            recorded.ProbabilityStatus != pending.ProbabilityStatus ||
            !recorded.CandidateCardInstanceIds.SequenceEqual(
                pending.CandidateCardInstanceIds) ||
            recorded.SelectedIndex != selectedIndex ||
            recorded.SelectedCardInstanceId !=
                pending.CandidateCardInstanceIds[selectedIndex])
            throw new InvalidDataException(
                "Semantic draw transcript diverged from its event.");

        var decision = new SemanticDrawDecisionV111(
            pending with
            {
                CandidateCardInstanceIds =
                    (long[])pending.CandidateCardInstanceIds.Clone()
            }, recorded.SelectedIndex, recorded.SelectedCardInstanceId);
        ValidateDecision(pending, decision);
        return decision;
    }

    /// <summary>
    /// Resumes a controlled card-effect draw through the ordinary engine and
    /// verifies its committed transcript when the continuation stays in combat.
    /// </summary>
    internal static RunState ResumeControlledEffectDraw(
        RunState checkpoint, SemanticDrawDecisionV111 decision,
        bool allowEngineOwnedPrototypeReward)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        ValidateBoundary(checkpoint);
        var deck = checkpoint.World!.Combat!.SemanticDeck!;
        ValidateDecision(
            SemanticDeckEngineV111.Inspect(deck), decision);

        RunState resumed;
        if (allowEngineOwnedPrototypeReward)
        {
            using (ControlledChanceGuardV111.Enter())
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
                resumed = PrototypeGameEngine
                    .ResumeControlledSemanticEffectDraw(
                        checkpoint, decision.SelectedIndex);
        }
        else
        {
            using (ControlledChanceGuardV111.Enter())
                resumed = PrototypeGameEngine
                    .ResumeControlledSemanticEffectDraw(
                        checkpoint, decision.SelectedIndex);
        }

        ValidateEffectDrawResult(
            resumed, decision.Event, decision.SelectedIndex);
        return resumed;
    }

    private static void ValidateEffectDrawResult(
        RunState run, SemanticDeckEventV111 pending, int selectedIndex)
    {
        if (run.Phase == RunPhase.Combat)
        {
            SemanticPilotCombatEngineV111
                .ValidateSemanticCombatBoundary(run);
            PrototypeStateInvariants.Validate(run);
            ValidateRecordedChoice(
                run, pending, selectedIndex);
        }
        else if (run.Phase is not (RunPhase.Reward or RunPhase.Terminal))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.card_effect.draw " +
                "operation=unsupported_post_draw_phase");
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
        var deck = SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            checkpoint,
            "Semantic draw choice lost its Nature deck checkpoint.",
            "Semantic draw choice lost its Nature deck checkpoint.",
            requiredPendingPurpose: "draw") ??
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
