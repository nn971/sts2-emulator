namespace Sts2Emulator.Core;

/// <summary>
/// One bounded source swap in an ordered pool shuffle, including the random
/// shortlist performed before a source card selection.
/// </summary>
public sealed record SemanticPoolShuffleEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, RandomIndexRequestV111 Request,
    string[] OriginalCardIds, string[] CurrentCardIds,
    int NextSwapIndex, int SelectedCount, string SourceOperationKind,
    long? SourceCardInstanceId, string ProbabilityStatus = "UNVERIFIED")
    : ISemanticChanceEventV111
{
    // This established member pair carries source identity labels. The Native
    // relic-bag queue site stores relic model IDs in the same ordered support.
    private string[]? _originalCardIds = CloneOptional(OriginalCardIds);
    private string[]? _currentCardIds = CloneOptional(CurrentCardIds);

    public string[] OriginalCardIds
    {
        get => CloneOptional(_originalCardIds)!;
        init => _originalCardIds = CloneOptional(value);
    }
    public string[] CurrentCardIds
    {
        get => CloneOptional(_currentCardIds)!;
        init => _currentCardIds = CloneOptional(value);
    }

    public const string EventSchemaId =
        SemanticPoolShuffleChoiceV111.SchemaId;
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
        OriginalCardIds = _originalCardIds!,
        CurrentCardIds = _currentCardIds!
    };

    public void Validate()
    {
        Request.Validate();
        var originalCardIds = _originalCardIds;
        var currentCardIds = _currentCardIds;
        var expectedSite = SemanticPoolShuffleChoiceV111.SiteIdForOperation(
            SourceOperationKind);
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            Request.SiteId is not (
                SemanticPoolShuffleChoiceV111.HandReturnSiteId or
                SemanticPoolShuffleChoiceV111.GenerationSiteId or
                SemanticPoolShuffleChoiceV111.RandomRareToHandSiteId or
                SemanticPoolShuffleChoiceV111.RecycleSiteId or
                SemanticPoolShuffleChoiceV111.RandomShortlistSiteId or
                NativeFisherYatesV111.SiteId or
                ControlledRecycleShuffleEngineV111.SiteId or
                PrototypeNativeRelicGrabBag.QueueShuffleSiteId or
                StochasticSiteRegistryV111.WarHammerEliteUpgradeShuffleSiteId or
                StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId or
                StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId) ||
            Request.SiteId != expectedSite ||
            Request.OperationKind != "bounded_integer" ||
            originalCardIds is not { Length: > 1 } ||
            currentCardIds is null ||
            currentCardIds.Length != originalCardIds.Length ||
            originalCardIds.Any(string.IsNullOrWhiteSpace) ||
            originalCardIds.Distinct(StringComparer.Ordinal).Count() !=
                originalCardIds.Length ||
            !currentCardIds.Order(StringComparer.Ordinal).SequenceEqual(
                originalCardIds.Order(StringComparer.Ordinal)) ||
            NextSwapIndex < 1 || NextSwapIndex >= originalCardIds.Length ||
            Request.SupportCount != NextSwapIndex + 1 ||
            SelectedCount <= 0 || SelectedCount > originalCardIds.Length ||
            (SourceOperationKind ==
                nameof(PrototypeCombatEffectKind.ChooseCards) &&
             SelectedCount >= originalCardIds.Length) ||
            string.IsNullOrWhiteSpace(SourceOperationKind) ||
            ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Invalid semantic full-pool shuffle event.");
        var expectedSupportFingerprint = CanonicalJson.Sha256(new
        {
            Request,
            Original = originalCardIds,
            Current = currentCardIds,
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

    private static string[]? CloneOptional(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
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
    public const string SchemaId = "sts2-v111-semantic-pool-shuffle-v1";
    internal const string HandReturnSiteId =
        StochasticSiteRegistryV111.CombatHandReturnShuffleSiteId;
    internal const string GenerationSiteId =
        StochasticSiteRegistryV111.CombatFullPoolShuffleSiteId;
    internal const string RandomRareToHandSiteId =
        StochasticSiteRegistryV111.CombatRandomRareDrawToHandShuffleSiteId;
    internal const string RecycleSiteId =
        StochasticSiteRegistryV111.CombatEffectDrawRecycleShuffleSiteId;
    internal const string RandomShortlistSiteId =
        StochasticSiteRegistryV111.CombatRandomShortlistShuffleSiteId;
    internal const string NativeRelicQueueOperation =
        "NativeRelicGrabBagQueueShuffle";
    internal const string NativeWarHammerOperation =
        "NativeWarHammerEliteUpgradeShuffle";
    internal const string NativeBossRelicOfferOperation =
        "NativeBossRelicOfferShuffle";
    internal const string NativeLavaRockRelicOfferOperation =
        "NativeLavaRockRelicOfferShuffle";
    internal const string NativeRecycleOperation =
        "NativeCombatDrawPileRecycleShuffle";
    internal const string NativeCombatEntryShuffleOperation =
        "NativeCombatEntryDrawPileShuffle";

    /// <summary>Pair one Native Fisher–Yates ticket with the exact ordered
    /// source identity support immediately before its swap. These audit
    /// annotations use the same event shape as controlled pool shuffles but
    /// do not suspend or alter the ordinary Native execution.</summary>
    internal static SemanticPoolShuffleDecisionV111 CreateNativeAudit(
        RandomIndexDecisionV111 providerDecision,
        IReadOnlyList<string> originalIdentities,
        IReadOnlyList<string> currentIdentities,
        int selectedCount,
        string sourceOperationKind,
        long? sourceCardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(providerDecision);
        ArgumentNullException.ThrowIfNull(originalIdentities);
        ArgumentNullException.ThrowIfNull(currentIdentities);
        providerDecision.Validate();
        var request = providerDecision.Request;
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceOperationKind);
        var operation = sourceOperationKind;
        var original = originalIdentities.ToArray();
        var current = currentIdentities.ToArray();
        var nextSwapIndex = request.BoundedCount is { } bound
            ? bound - 1 : -1;
        if (providerDecision.ProviderMode != "native-v111" ||
            request.OperationKind != "bounded_integer" ||
            request.SiteId != SiteIdForOperation(operation) ||
            nextSwapIndex < 1 || original.Length <= nextSwapIndex ||
            selectedCount <= 0 || selectedCount > original.Length ||
            original.Any(string.IsNullOrWhiteSpace) ||
            original.Distinct(StringComparer.Ordinal).Count() !=
                original.Length ||
            current.Length != original.Length ||
            !current.Order(StringComparer.Ordinal).SequenceEqual(
                original.Order(StringComparer.Ordinal)))
            throw new InvalidDataException(
                "Native pool-shuffle audit has an unsupported request or ordered support.");

        var boundaryFingerprint = NativeAuditBoundaryFingerprint(
            providerDecision, operation, sourceCardInstanceId);
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            Request = request,
            Original = original,
            Current = current,
            NextSwapIndex = nextSwapIndex,
            SelectedCount = selectedCount,
            Operation = operation,
            SourceCardInstanceId = sourceCardInstanceId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticPoolShuffleEventV111(
            SchemaId, eventId, boundaryFingerprint, supportFingerprint,
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, original, current, nextSwapIndex, selectedCount, operation,
            sourceCardInstanceId);
        var outcome = new SemanticPoolShuffleDecisionV111(
            semanticEvent, providerDecision.SelectedIndex);
        outcome.Validate();
        return outcome;
    }

    internal static void RecordNativeAudit(
        IReadOnlyList<string> originalIdentities,
        IReadOnlyList<string> finalIdentities,
        IReadOnlyList<RandomIndexDecisionV111> providerDecisions,
        int selectedCount, string sourceOperationKind,
        long? sourceCardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(originalIdentities);
        ArgumentNullException.ThrowIfNull(finalIdentities);
        ArgumentNullException.ThrowIfNull(providerDecisions);
        if (!RandomBoundaryJournalV111.HasActiveJournal)
            return;

        var original = originalIdentities.ToArray();
        if (finalIdentities.Count != original.Length)
            throw new InvalidDataException(
                "Native pool-shuffle audit changed the ordered support length.");
        if (providerDecisions.Count == 0)
        {
            if (!original.SequenceEqual(finalIdentities,
                    StringComparer.Ordinal))
                throw new InvalidDataException(
                    "Native pool-shuffle audit has a malformed support or decision prefix.");
            return;
        }

        var current = (string[])original.Clone();
        var outcomes = new SemanticPoolShuffleDecisionV111[
            providerDecisions.Count];
        for (var index = 0; index < providerDecisions.Count; index++)
        {
            var decision = providerDecisions[index];
            outcomes[index] = CreateNativeAudit(
                decision, original, current, selectedCount,
                sourceOperationKind, sourceCardInstanceId);
            var swapIndex = decision.Request.BoundedCount!.Value - 1;
            (current[swapIndex], current[decision.SelectedIndex]) =
                (current[decision.SelectedIndex], current[swapIndex]);
        }
        if (!current.SequenceEqual(finalIdentities, StringComparer.Ordinal))
            throw new InvalidDataException(
                "Native pool-shuffle audit differs from its resulting ordered pool.");
        RandomBoundaryJournalV111.RecordPoolShuffleOutcomes(
            outcomes, providerDecisions);
    }

    internal static void ValidateNativeAudit(
        SemanticPoolShuffleDecisionV111 outcome,
        RandomIndexDecisionV111 providerDecision)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(providerDecision);
        outcome.Validate();
        providerDecision.Validate();
        var semanticEvent = outcome.Event;
        if (providerDecision.ProviderMode != "native-v111" ||
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                semanticEvent.Request, providerDecision.Request) ||
            outcome.SelectedSwapIndex != providerDecision.SelectedIndex ||
            semanticEvent.BoundaryFingerprint !=
                NativeAuditBoundaryFingerprint(
                    providerDecision, semanticEvent.SourceOperationKind,
                    semanticEvent.SourceCardInstanceId))
            throw new InvalidDataException(
                "Native pool-shuffle semantic support differs from its exact provider decision.");
    }

    private static string NativeAuditBoundaryFingerprint(
        RandomIndexDecisionV111 providerDecision, string operation,
        long? sourceCardInstanceId) => CanonicalJson.Sha256(new
        {
            SchemaId = "sts2-v111-native-pool-shuffle-audit-v1",
            ProviderDecision = providerDecision,
            Operation = operation,
            SourceCardInstanceId = sourceCardInstanceId
        });

    internal static string? SiteIdForOperation(string sourceOperationKind) =>
        sourceOperationKind switch
        {
            nameof(PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle) =>
                HandReturnSiteId,
            nameof(PrototypeCombatEffectKind.MoveRandomRareDrawCardsToHand) =>
                RandomRareToHandSiteId,
            nameof(PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw) =>
                RecycleSiteId,
            nameof(PrototypeCombatEffectKind.ChooseCards) =>
                RandomShortlistSiteId,
            nameof(PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand) or
            nameof(PrototypeCombatEffectKind.CreateDistinctColorlessCardsInHand) or
            nameof(PrototypeCombatEffectKind.ChooseGeneratedCards) or
            nameof(PrototypeCombatEffectKind.ChooseGeneratedColorlessCards) =>
                GenerationSiteId,
            NativeRecycleOperation =>
                ControlledRecycleShuffleEngineV111.SiteId,
            NativeCombatEntryShuffleOperation =>
                NativeFisherYatesV111.SiteId,
            NativeRelicQueueOperation =>
                PrototypeNativeRelicGrabBag.QueueShuffleSiteId,
            NativeWarHammerOperation =>
                StochasticSiteRegistryV111.WarHammerEliteUpgradeShuffleSiteId,
            NativeBossRelicOfferOperation =>
                StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId,
            NativeLavaRockRelicOfferOperation =>
                StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId,
            _ => null
        };

    internal static SemanticPoolShuffleEventV111? DescribePending(
        RunState run, string boundaryFingerprint, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var combat = run.World?.Combat;
        var choice = combat?.PendingChoice;
        var shuffle = choice?.ControlledPoolShuffle;
        if (shuffle is null) return null;
        ValidateBoundary(run);

        var original = (string[])shuffle.OriginalCardIds.Clone();
        var current = (string[])shuffle.CurrentCardIds.Clone();
        var sourceCardId = choice!.SourceCardInstanceId;
        var request = CreateRequest(combat!, ordinal);
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            request,
            Original = original,
            Current = current,
            shuffle.NextSwapIndex,
            shuffle.SelectedCount,
            Operation = shuffle.Operation.Kind.ToString(),
            sourceCardId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticPoolShuffleEventV111(
            SchemaId, eventId, boundaryFingerprint, supportFingerprint,
            request, original, current, shuffle.NextSwapIndex,
            shuffle.SelectedCount, shuffle.Operation.Kind.ToString(),
            sourceCardId);
        semanticEvent.Validate();
        return semanticEvent;
    }

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
        var siteId = SiteIdForOperation(
            shuffle.Operation.Kind.ToString()) ??
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_POOL operation=" +
                shuffle.Operation.Kind);
        var streamId = handReturn || recycle
            ? combat.Stochastic.NativeRng is not null
                ? "run.shuffle" : "prototype.combat"
            : randomShortlist && combat.Stochastic.NativeRng?.RunStreams is not null
                ? "run.combat_card_selection"
                : (shuffle.Operation.GeneratedChoiceUsesNativeCardGenerationRng ||
                   shuffle.Operation.Kind ==
                       PrototypeCombatEffectKind
                           .CreateDistinctCharacterCommonCardsInHand) &&
                  combat.Stochastic.NativeRng?.RunStreams is not null
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
            if (combat!.Stochastic.SemanticDeck is not null ||
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

    internal static void ValidateDecision(
        SemanticPoolShuffleEventV111? pending,
        SemanticPoolShuffleDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (pending is null || pending.EventId != decision.Event.EventId ||
            CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event))
            throw new InvalidDataException(
                "Pool-shuffle decision differs from its live semantic event.");
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
        bool helloWorldAutomaticBeforeHandDraw =
            PrototypeGameEngine.IsHelloWorldAutomaticBeforeHandDrawPoolShuffle(
                combat, choice!, shuffle, choice!.OuterChoiceContinuation!);

        var result = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.ResumeControlledPoolShuffle(
                checkpoint.Fork(), decision.SelectedSwapIndex));
        if (PrototypeGameEngine
                .IsAtCombatRewardOrTerminalBoundary(result))
        {
            PrototypeStateInvariants.Validate(result);
            return result;
        }
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
        else if (resultCombat.PendingChoice?.ControlledRandomPileCard is not null)
        {
            if (SemanticRandomPileCardEngineV111.DescribePending(result) is null)
                throw new InvalidDataException(
                    "Resumed hand draw lost its random pile-card event.");
        }
        else if (resultCombat.PendingChoice?
                     .AutomaticSemanticDrawHookChoice == true)
        {
            if (SemanticDrawHookChoiceEngineV111.DescribePending(result) is null)
                throw new InvalidDataException(
                    "Resumed hand draw lost its draw-hook action event.");
        }
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
            !helloWorldAutomaticBeforeHandDraw &&
            (!combat.DrawPile.SequenceEqual(resultCombat.DrawPile) ||
             CanonicalJson.Sha256(combat.Stochastic.SemanticDeck) !=
                 CanonicalJson.Sha256(resultCombat.Stochastic.SemanticDeck)))
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
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                pending.Request, expectedRequest))
            throw new InvalidDataException(
                "Pool-shuffle decision no longer matches its live support.");
    }
}
