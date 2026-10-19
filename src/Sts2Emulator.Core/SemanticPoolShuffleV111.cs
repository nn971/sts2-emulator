namespace Sts2Emulator.Core;

/// <summary>
/// One bounded source swap in a full combat-pool shuffle, including the
/// random shortlist performed before a source card selection.
/// </summary>
public sealed record SemanticPoolShuffleEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, RandomIndexRequestV111 Request,
    string[] OriginalCardIds, string[] CurrentCardIds,
    int NextSwapIndex, int SelectedCount, string SourceOperationKind,
    long? SourceCardInstanceId, string ProbabilityStatus = "UNVERIFIED")
    : ISemanticChanceEventV111
{
    public const string EventSchemaId =
        "sts2-v111-semantic-pool-shuffle-v1";
    public string SiteId => Request.SiteId;
    public string StreamId => Request.StreamId;
    public string OwnerId => Request.OwnerId;
    public int Ordinal => Request.Ordinal;
    public string OperationKind => Request.OperationKind;

    public SemanticPoolShuffleEventV111 Fork() => this with
    {
        Request = Request with
        {
            OrderedCandidates = (int[])Request.OrderedCandidates.Clone()
        },
        OriginalCardIds = (string[])OriginalCardIds.Clone(),
        CurrentCardIds = (string[])CurrentCardIds.Clone()
    };

    public void Validate()
    {
        Request.Validate();
        var expectedSite = SemanticPoolShuffleChoiceV111.SiteIdForOperation(
            SourceOperationKind);
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            Request.SiteId is not (
                "combat.hand_return.draw_pile.shuffle_swap_index" or
                "combat.generation.full_pool_shuffle.swap_index" or
                "combat.draw_pile.random_rare_to_hand.shuffle_swap_index" or
                "combat.draw.recycle.shuffle_swap_index" or
                "combat.card_selection.random_shortlist") ||
            Request.SiteId != expectedSite ||
            Request.OperationKind != "bounded_integer" ||
            OriginalCardIds is not { Length: > 1 } ||
            CurrentCardIds is null ||
            CurrentCardIds.Length != OriginalCardIds.Length ||
            OriginalCardIds.Any(string.IsNullOrWhiteSpace) ||
            OriginalCardIds.Distinct(StringComparer.Ordinal).Count() !=
                OriginalCardIds.Length ||
            !CurrentCardIds.Order(StringComparer.Ordinal).SequenceEqual(
                OriginalCardIds.Order(StringComparer.Ordinal)) ||
            NextSwapIndex < 1 || NextSwapIndex >= OriginalCardIds.Length ||
            Request.SupportCount != NextSwapIndex + 1 ||
            SelectedCount <= 0 || SelectedCount > OriginalCardIds.Length ||
            (SourceOperationKind ==
                nameof(PrototypeCombatEffectKind.ChooseCards) &&
             SelectedCount >= OriginalCardIds.Length) ||
            string.IsNullOrWhiteSpace(SourceOperationKind) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic full-pool shuffle event.");
        var expectedSupportFingerprint = CanonicalJson.Sha256(new
        {
            Request,
            Original = OriginalCardIds,
            Current = CurrentCardIds,
            NextSwapIndex,
            SelectedCount,
            Operation = SourceOperationKind,
            SourceCardInstanceId
        });
        if (SupportFingerprint != expectedSupportFingerprint)
            throw new InvalidDataException(
                "Full-pool shuffle support fingerprint is inconsistent.");
        var expectedEventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint,
            SupportFingerprint
        });
        if (EventId != expectedEventId)
            throw new InvalidDataException(
                "Full-pool shuffle event ID is inconsistent.");
    }
}

public sealed record SemanticPoolShuffleDecisionV111(
    SemanticPoolShuffleEventV111 Event, int SelectedSwapIndex)
{
    public SemanticPoolShuffleDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (SelectedSwapIndex < 0 ||
            SelectedSwapIndex > Event.NextSwapIndex)
            throw new InvalidDataException(
                "Full-pool shuffle swap is outside its bounded support.");
    }
}

public static class SemanticPoolShuffleChoiceV111
{
    internal static string SiteIdForOperation(string sourceOperationKind) =>
        sourceOperationKind switch
        {
            nameof(PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle) =>
                "combat.hand_return.draw_pile.shuffle_swap_index",
            nameof(PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand) =>
                "combat.draw_pile.random_rare_to_hand.shuffle_swap_index",
            nameof(PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw) =>
                "combat.draw.recycle.shuffle_swap_index",
            nameof(PrototypeCombatEffectKind.ChooseCards) =>
                "combat.card_selection.random_shortlist",
            _ => "combat.generation.full_pool_shuffle.swap_index"
        };

    internal static RandomIndexRequestV111 CreateRequest(
        CombatState combat, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var choice = combat.PendingChoice;
        var shuffle = choice?.ControlledPoolShuffle ??
            throw new InvalidDataException(
                "Full-pool shuffle is missing its source operation.");
        bool handReturn = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle;
        bool recycle = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw;
        bool randomRareToHand = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand;
        bool randomShortlist = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.ChooseCards;
        var siteId = SiteIdForOperation(shuffle.Operation.Kind.ToString());
        var streamId = handReturn || recycle
            ? combat.NativeRng is not null
                ? "run.shuffle" : "prototype.combat"
            : randomShortlist && combat.NativeRng?.RunStreams is not null
                ? "run.combat_card_selection"
                : shuffle.Operation.GeneratedChoiceUsesNativeCardGenerationRng &&
                  combat.NativeRng?.RunStreams is not null
                    ? "run.combat_card_generation" : "prototype.combat";
        var sourceCardId = choice!.SourceCardInstanceId;
        var shortlistCardIds = randomShortlist
            ? PrototypeGameEngine.ControlledCardShortlistCandidates(
                combat, shuffle.Operation) ?? throw new InvalidDataException(
                    "Random shortlist lost its source card pool.")
            : [];
        var ownerId = handReturn || recycle
            ? "effect:" + shuffle.Operation.Kind + ":source:" +
                (sourceCardId?.ToString() ?? "none") + ":pool:" +
                CanonicalJson.Sha256(shuffle.OriginalCardIds)
            : randomShortlist
                ? "selection:" + (shuffle.Operation.Selection?.SourceZone ??
                    throw new InvalidDataException(
                        "Random shortlist lost its source zone.")) + ":pool:" +
                    CanonicalJson.Sha256(shortlistCardIds)
                : "card:" + (sourceCardId?.ToString() ?? "effect") +
                    ":operation:" + shuffle.Operation.Kind + ":pool:" +
                    CanonicalJson.Sha256(shuffle.OriginalCardIds);
        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal, [],
            OperationKind: "bounded_integer",
            BoundedCount: shuffle.NextSwapIndex + 1);
        request.Validate();
        return request;
    }

    internal static void ValidateBoundary(RunState run)
    {
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var shuffle = choice?.ControlledPoolShuffle;
        if (run.Phase != RunPhase.Combat ||
            combat is not { PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is null &&
             !combat.IsPlayerTurn) ||
            choice is not { ChoiceId: "controlled_pool_shuffle",
                OuterChoiceContinuation: not null } ||
            shuffle is null ||
            !PrototypeGameEngine.ValidateControlledPoolShuffle(
                combat, shuffle, choice.SourceCardInstanceId))
            throw new InvalidDataException(
                "Full-pool shuffle is not source-reachable.");
        if (shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw)
        {
            if (combat!.SemanticDeck is not null ||
                combat.DrawPile.Length != 0 || combat.DiscardPile.Length <= 1 ||
                choice!.OuterChoiceContinuation!.Operations.FirstOrDefault() is not
                    { Kind: PrototypeCombatEffectKind.DrawCards, Amount: > 0 } ||
                (combat.AutomaticPipelineContinuation is not null &&
                 !PrototypeGameEngine.IsMayhemLegacyRecycleContinuation(
                     combat, choice)))
                throw new InvalidDataException(
                    "Legacy recycle shuffle lost its saved draw continuation.");
            PrototypeStateInvariants.Validate(run);
            return;
        }
        SemanticDrawPileAdapterV111.ValidateNatureDeckBoundary(
            combat,
            "Full-pool shuffle lost the provider-owned deck.",
            "Full-pool shuffle changed the hidden draw pile.");
        PrototypeStateInvariants.Validate(run);
    }

    public static SemanticPoolShuffleDecisionV111 Select(
        SemanticPoolShuffleEventV111 pending, string eventId, int swapIndex)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Pool-shuffle event ID is stale or mismatched.");
        var decision = new SemanticPoolShuffleDecisionV111(
            pending.Fork(), swapIndex);
        decision.Validate();
        return decision;
    }

    /// <summary>Revalidate one bounded source swap and resume the ordinary
    /// full-pool shuffle continuation.</summary>
    internal static RunState Resume(
        RunState checkpoint, SemanticPoolShuffleDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Full-pool shuffle lost its source combat.");
        var choice = combat.PendingChoice;
        var shuffle = choice?.ControlledPoolShuffle;
        if (shuffle is null)
            throw new InvalidDataException(
                "Full-pool shuffle is missing its source operation.");
        ValidateBoundary(checkpoint);
        ValidateLiveSupport(combat, choice!, shuffle, decision.Event);

        RunState result;
        using (ControlledChanceGuardV111.Enter())
            result = PrototypeGameEngine.ResumeControlledPoolShuffle(
                checkpoint.Fork(), decision.SelectedSwapIndex);
        if (result.Phase != RunPhase.Combat)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_POOL site=" + decision.Event.SiteId + " " +
                "operation=unsupported_post_shuffle_phase");
        var resultCombat = result.World?.Combat ??
            throw new InvalidDataException(
                "Full-pool shuffle continuation lost combat.");
        if (resultCombat.PendingChoice?.ControlledPoolShuffle is not null)
            ValidateBoundary(result);
        else if (resultCombat.PendingChoice?.ControlledDrawInsertion is not null)
            SemanticDrawInsertionChoiceV111.ValidateBoundary(result);
        else if (resultCombat.PendingChoice?.ChoiceId == "select_cards")
        {
            SemanticCardSelectionEngineV111.ValidatePendingBoundary(result);
            _ = SemanticCardSelectionEngineV111.DescribePending(
                result, CanonicalJson.Sha256(result));
        }
        else
            SemanticPilotCombatEngineV111.ValidateSemanticCombatBoundary(result);

        bool randomRareToHand = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand;
        bool handReturn = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle;
        bool recycle = shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw;
        if (!randomRareToHand && !handReturn && !recycle &&
            (!combat.DrawPile.SequenceEqual(resultCombat.DrawPile) ||
             CanonicalJson.Sha256(combat.SemanticDeck) !=
                 CanonicalJson.Sha256(resultCombat.SemanticDeck)))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=" + decision.Event.SiteId + " " +
                "operation=unmodeled_draw_pile_mutation");
        if (randomRareToHand || handReturn || recycle)
            PrototypeStateInvariants.Validate(result);
        return result;
    }

    private static void ValidateLiveSupport(
        CombatState combat, PendingCombatChoiceState choice,
        PrototypeControlledPoolShuffleV111 shuffle,
        SemanticPoolShuffleEventV111 pending)
    {
        var sourceCardId = choice.SourceCardInstanceId;
        var expectedRequest = CreateRequest(combat, pending.Ordinal);
        if (!pending.OriginalCardIds.SequenceEqual(shuffle.OriginalCardIds) ||
            !pending.CurrentCardIds.SequenceEqual(shuffle.CurrentCardIds) ||
            pending.NextSwapIndex != shuffle.NextSwapIndex ||
            pending.SelectedCount != shuffle.SelectedCount ||
            pending.SourceOperationKind != shuffle.Operation.Kind.ToString() ||
            pending.SourceCardInstanceId != sourceCardId ||
            CanonicalJson.Sha256(pending.Request) !=
                CanonicalJson.Sha256(expectedRequest))
            throw new InvalidDataException(
                "Pool-shuffle decision no longer matches its live support.");
    }
}
