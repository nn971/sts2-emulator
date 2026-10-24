using System.Globalization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string IntentConditioningPolicyId =
        "source-filtered-intent-weights-v1";
    public const string EndTurnIndexConditioningPolicyId =
        "uniform-saved-automatic-index-support-v1";
    public const string DrawConditioningPolicyId =
        "uniform-unresolved-pool-permutation-v1";
    public const string RevealConditioningPolicyId =
        "uniform-unresolved-pool-reveal-v1";
    public const string TargetConditioningPolicyId =
        "uniform-live-enemy-instance-support-v1";
    public const string GeneratedCardConditioningPolicyId =
        "uniform-ordered-generated-card-model-pool-v1";
    public const string CostConditioningPolicyId =
        "uniform-bounded-cost-support-v1";
    public const string RandomPileCardConditioningPolicyId =
        "uniform-ordered-physical-pile-card-support-v1";
    public const string CardTransformConditioningPolicyId =
        "uniform-card-transform-replacement-pool-v1";
    public const string PotionGenerationConditioningPolicyId =
        "uniform-potion-generation-model-pool-v1";
    public const string HandCostSelectionConditioningPolicyId =
        "uniform-hand-cost-target-support-v1";
    public const string PoolShuffleConditioningPolicyId =
        "uniform-fisher-yates-bounded-swap-v1";
    public const string DrawInsertionConditioningPolicyId =
        "uniform-bounded-draw-pile-insertion-position-v1";
    public const string NativeDrawInsertionConditioningPolicyId =
        "source-bounded-draw-insertion-nextint-grid-v1";
    public const string CardShortlistConditioningPolicyId =
        "uniform-sequential-without-replacement-v1";

    public static ChanceLawV111 QueryIntent(SemanticIntentEventV111 intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        intent.Validate();
        var totalRawWeight = intent.Support.TotalRawWeight;
        long totalGridWeight = 0;
        var outcomes = intent.Support.Candidates
            .Select(candidate =>
            {
                var first = candidate.TicketStart == 0
                    ? 0
                    : NativeNextIntBoundaryGridIndex(
                        candidate.TicketStart, totalRawWeight);
                var endTicket = checked(candidate.TicketStart +
                    candidate.RawWeight);
                var end = NativeNextIntBoundaryGridIndex(
                    endTicket, totalRawWeight);
                var weight = checked(end - first);
                if (weight <= 0)
                    throw new InvalidDataException(
                        "Native intent-ticket grid produced an empty outcome bin.");
                totalGridWeight = checked(totalGridWeight + weight);
                return new ChanceLawOutcomeV111(
                    candidate.IntentId, weight);
            })
            .ToArray();
        if (totalGridWeight != NativeNextDoubleGridSize)
            throw new InvalidDataException(
                "Native intent-ticket grid masses do not cover the input grid.");
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            intent.SupportFingerprint,
            intent.SiteId,
            intent.OwnerId,
            intent.Ordinal,
            policy = IntentConditioningPolicyId
        });

        return Create(intent.EventId, intent.SiteId, intent.OperationKind,
            intent.OwnerId, intent.Ordinal, IntentConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the declared index law at a suspended automatic enemy-action
    /// boundary. Canonical Native streams use exact NextInt grid masses;
    /// legacy and unknown streams keep equal request-index weights. This
    /// preserves index identity even when candidate values repeat.
    /// </summary>
    public static ChanceLawV111 QueryEndTurnIndex(
        SemanticEndTurnIndexEventV111 indexEvent)
    {
        ArgumentNullException.ThrowIfNull(indexEvent);
        indexEvent.Validate();
        var sourceVerifiedNativeIndex = NativeRngV111.IsCanonicalStreamId(
            indexEvent.Request.StreamId);
        var indexWeights = sourceVerifiedNativeIndex
            ? NativeNextIntGridWeights(indexEvent.Request.SupportCount)
            : Enumerable.Repeat(1L,
                indexEvent.Request.SupportCount).ToArray();
        var outcomes = Enumerable.Range(0,
                indexEvent.Request.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "choice_index:" + index.ToString(CultureInfo.InvariantCulture),
                indexWeights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            indexEvent.SupportFingerprint,
            indexEvent.Request.SiteId,
            indexEvent.Request.OwnerId,
            indexEvent.Request.Ordinal,
            indexEvent.Request.OperationKind,
            policy = EndTurnIndexConditioningPolicyId
        });
        return Create(indexEvent.EventId, indexEvent.Request.SiteId,
            indexEvent.Request.OperationKind, indexEvent.Request.OwnerId,
            indexEvent.Request.Ordinal,
            EndTurnIndexConditioningPolicyId, conditioningFingerprint,
            sourceVerifiedNativeIndex
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "uniform-saved-enemy-action-index-support-unverified",
            outcomes, sourceVerifiedNativeIndex
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }

    /// <summary>
    /// Query one Native draw-pile Fisher-Yates index over its declared
    /// current bound. The ordered pile and surrounding combat continuation
    /// are not inspected.
    /// </summary>
    public static ChanceLawV111 QueryNativeDrawPileShuffleIndex(
        string ownerId, int supportCount, int ordinal)
    {
        return QueryNativeBoundedIntegerLawSite(
            NativeFisherYatesV111.SiteId, ownerId, supportCount, ordinal);
    }

    /// <summary>
    /// Query one Native discard-recycle Fisher-Yates index over its declared
    /// current bound. The ordered discard pile and hand-draw continuation are
    /// not inspected.
    /// </summary>
    public static ChanceLawV111 QueryNativeRecycleShuffleIndex(
        string ownerId, int supportCount, int ordinal)
    {
        return QueryNativeBoundedIntegerLawSite(
            ControlledRecycleShuffleEngineV111.SiteId, ownerId,
            supportCount, ordinal);
    }

    /// <summary>
    /// Query one Native Personal Hive Dazed insertion index over its declared
    /// current bound. The source-specific owner and draw-pile support are not
    /// inspected.
    /// </summary>
    public static ChanceLawV111 QueryNativePersonalHiveDazedInsertionIndex(
        string ownerId, int supportCount, int ordinal)
    {
        return QueryNativeBoundedIntegerLawSite(
            PrototypeControlledDrawInsertionV111.PersonalHiveDazedSiteId,
            ownerId, supportCount, ordinal);
    }

    /// <summary>
    /// Query the Native bounded-index law over the live physical enemy
    /// identities at a suspended random-target boundary. The selected index
    /// is not part of the event, so querying cannot depend on a prior result.
    /// </summary>
    public static ChanceLawV111 QueryTarget(SemanticTargetEventV111 target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();
        var indexWeights = NativeNextIntGridWeights(
            target.CandidateEnemyInstanceIds.Length);
        var outcomes = target.CandidateEnemyInstanceIds
            .Select((instanceId, index) => new ChanceLawOutcomeV111(
                "enemy_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), indexWeights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            target.SupportFingerprint,
            target.SiteId,
            target.OwnerId,
            target.Ordinal,
            target.TargetKind,
            policy = TargetConditioningPolicyId
        });
        return Create(target.EventId, target.SiteId, target.OperationKind,
            target.OwnerId, target.Ordinal, TargetConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the Native bounded-index law over the ordered, duplicate-free
    /// generated-card model pool saved at a source effect boundary.
    /// </summary>
    public static ChanceLawV111 QueryGeneratedCard(
        SemanticGeneratedCardEventV111 generatedCard)
    {
        ArgumentNullException.ThrowIfNull(generatedCard);
        generatedCard.Validate();
        var indexWeights = NativeNextIntGridWeights(
            generatedCard.CandidateCardIds.Length);
        var outcomes = generatedCard.CandidateCardIds
            .Select((cardId, index) => new ChanceLawOutcomeV111(
                "card_model:" + cardId, indexWeights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            generatedCard.SupportFingerprint,
            generatedCard.SiteId,
            generatedCard.OwnerId,
            generatedCard.Ordinal,
            policy = GeneratedCardConditioningPolicyId
        });
        return Create(generatedCard.EventId, generatedCard.SiteId,
            generatedCard.OperationKind, generatedCard.OwnerId,
            generatedCard.Ordinal, GeneratedCardConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the Native bounded-index law declared by a pending semantic cost
    /// event. Card identity and source stage remain in its conditioning
    /// fingerprint through the event's support fingerprint.
    /// </summary>
    public static ChanceLawV111 QueryCost(SemanticCostEventV111 cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        cost.Validate();
        var indexWeights = NativeNextIntGridWeights(
            cost.CandidateCosts.Length);
        var outcomes = cost.CandidateCosts
            .Select((value, index) => new ChanceLawOutcomeV111(
                "cost:" + value.ToString(CultureInfo.InvariantCulture),
                indexWeights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            cost.SupportFingerprint,
            cost.SiteId,
            cost.OwnerId,
            cost.Ordinal,
            policy = CostConditioningPolicyId
        });
        return Create(cost.EventId, cost.SiteId, cost.OperationKind,
            cost.OwnerId, cost.Ordinal, CostConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the Native bounded-index law over physical card instances in
    /// the ordered source support for a random pile-card effect.
    /// </summary>
    public static ChanceLawV111 QueryRandomPileCard(
        SemanticRandomPileCardEventV111 pileCard)
    {
        ArgumentNullException.ThrowIfNull(pileCard);
        pileCard.Validate();
        var indexWeights = NativeNextIntGridWeights(
            pileCard.CandidateCardInstanceIds.Length);
        var outcomes = pileCard.CandidateCardInstanceIds
            .Select((instanceId, index) => new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), indexWeights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            pileCard.SupportFingerprint,
            pileCard.SiteId,
            pileCard.OwnerId,
            pileCard.Ordinal,
            policy = RandomPileCardConditioningPolicyId
        });
        return Create(pileCard.EventId, pileCard.SiteId,
            pileCard.OperationKind, pileCard.OwnerId, pileCard.Ordinal,
            RandomPileCardConditioningPolicyId, conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the Native bounded-index law over replacement models for one
    /// saved physical card transform. The source card identity and model are
    /// conditioned on.
    /// </summary>
    public static ChanceLawV111 QueryCardTransform(
        SemanticCardTransformEventV111 transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        transform.Validate();
        var indexWeights = NativeNextIntGridWeights(
            transform.CandidateCardIds.Length);
        var outcomes = transform.CandidateCardIds
            .Select((cardId, index) => new ChanceLawOutcomeV111(
                "card_model:" + cardId, indexWeights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            transform.SupportFingerprint,
            transform.SiteId,
            transform.OwnerId,
            transform.Ordinal,
            transform.SourceCardInstanceId,
            transform.OriginalCardId,
            policy = CardTransformConditioningPolicyId
        });
        return Create(transform.EventId, transform.SiteId,
            transform.OperationKind, transform.OwnerId, transform.Ordinal,
            CardTransformConditioningPolicyId, conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the Native bounded-index law over the ordered source potion-model
    /// pool for one pending generated-potion choice.
    /// </summary>
    public static ChanceLawV111 QueryPotionGeneration(
        SemanticPotionGenerationEventV111 potion)
    {
        ArgumentNullException.ThrowIfNull(potion);
        potion.Validate();
        var indexWeights = NativeNextIntGridWeights(
            potion.CandidatePotionIds.Length);
        var outcomes = potion.CandidatePotionIds
            .Select((potionId, index) => new ChanceLawOutcomeV111(
                "potion_model:" + potionId, indexWeights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            potion.SupportFingerprint,
            potion.SiteId,
            potion.OwnerId,
            potion.Ordinal,
            policy = PotionGenerationConditioningPolicyId
        });
        return Create(potion.EventId, potion.SiteId,
            potion.OperationKind, potion.OwnerId, potion.Ordinal,
            PotionGenerationConditioningPolicyId, conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the Native bounded-index law over eligible physical hand-card
    /// identities for a pending fixed-cost operation.
    /// </summary>
    public static ChanceLawV111 QueryHandCostSelection(
        SemanticHandCostSelectionEventV111 selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        selection.Validate();
        var indexWeights = NativeNextIntGridWeights(
            selection.CandidateCardInstanceIds.Length);
        var outcomes = selection.CandidateCardInstanceIds
            .Select((instanceId, index) => new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), indexWeights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            selection.SupportFingerprint,
            selection.SiteId,
            selection.OwnerId,
            selection.Ordinal,
            selection.AppliedCost,
            policy = HandCostSelectionConditioningPolicyId
        });
        return Create(selection.EventId, selection.SiteId,
            selection.OperationKind, selection.OwnerId, selection.Ordinal,
            HandCostSelectionConditioningPolicyId, conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query one bounded Fisher–Yates swap in a saved without-replacement
    /// pool shuffle. The pinned Native NextInt masses describe each swap;
    /// prior swaps remain part of the conditioned state.
    /// </summary>
    public static ChanceLawV111 QueryPoolShuffle(
        SemanticPoolShuffleEventV111 shuffle)
    {
        ArgumentNullException.ThrowIfNull(shuffle);
        shuffle.Validate();
        var indexWeights = NativeNextIntGridWeights(
            shuffle.Request.SupportCount);
        var outcomes = Enumerable.Range(0, indexWeights.Length)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(CultureInfo.InvariantCulture),
                indexWeights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            shuffle.SupportFingerprint,
            shuffle.SiteId,
            shuffle.OwnerId,
            shuffle.Ordinal,
            shuffle.NextSwapIndex,
            shuffle.SelectedCount,
            policy = PoolShuffleConditioningPolicyId
        });
        return Create(shuffle.EventId, shuffle.SiteId,
            shuffle.OperationKind, shuffle.OwnerId, shuffle.Ordinal,
            PoolShuffleConditioningPolicyId, conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query a bounded draw-pile insertion position for a generated card.
    /// Recognized Native streams use exact NextInt grid masses; legacy routes
    /// retain the equal-position model.
    /// </summary>
    public static ChanceLawV111 QueryDrawInsertion(
        SemanticDrawInsertionEventV111 insertion)
    {
        ArgumentNullException.ThrowIfNull(insertion);
        insertion.Validate();
        var hasNativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
            insertion.Request.StreamId);
        var indexWeights = hasNativeIndexLaw
            ? NativeNextIntGridWeights(insertion.Request.SupportCount)
            : Enumerable.Repeat(1L,
                insertion.Request.SupportCount).ToArray();
        var policy = hasNativeIndexLaw
            ? NativeDrawInsertionConditioningPolicyId
            : DrawInsertionConditioningPolicyId;
        var outcomes = indexWeights.Select((weight, position) =>
                new ChanceLawOutcomeV111(
                    "draw_position:" + position.ToString(
                        CultureInfo.InvariantCulture), weight))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            insertion.SupportFingerprint,
            insertion.SiteId,
            insertion.OwnerId,
            insertion.Ordinal,
            insertion.GeneratedCardModelId,
            policy
        });
        return Create(insertion.EventId, insertion.SiteId,
            insertion.OperationKind, insertion.OwnerId, insertion.Ordinal,
            policy, conditioningFingerprint,
            hasNativeIndexLaw
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "uniform-bounded-insertion-position-unverified",
            outcomes, hasNativeIndexLaw
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }

    /// <summary>
    /// Query one conditional pick from a saved without-replacement shortlist.
    /// The remaining physical identities and all prior picks are included in
    /// the event conditioning fingerprint.
    /// </summary>
    public static ChanceLawV111 QueryCardShortlist(
        SemanticCardShortlistEventV111 shortlist)
    {
        ArgumentNullException.ThrowIfNull(shortlist);
        shortlist.Validate();
        var hasNativeIndexLaw = shortlist.SiteId is
            StochasticSiteRegistryV111.CombatAutoplayRandomCardSelectSiteId or
            StochasticSiteRegistryV111.CombatRandomReplayCardSelectSiteId;
        var indexWeights = hasNativeIndexLaw
            ? NativeNextIntGridWeights(
                shortlist.CandidateCardInstanceIds.Length)
            : Enumerable.Repeat(1L,
                shortlist.CandidateCardInstanceIds.Length).ToArray();
        var outcomes = shortlist.CandidateCardInstanceIds
            .Select((instanceId, index) => new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), indexWeights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            shortlist.SupportFingerprint,
            shortlist.SiteId,
            shortlist.OwnerId,
            shortlist.Ordinal,
            shortlist.RequiredCount,
            shortlist.SelectedCardInstanceIds,
            policy = CardShortlistConditioningPolicyId
        });
        return Create(shortlist.EventId, shortlist.SiteId,
            shortlist.OperationKind, shortlist.OwnerId, shortlist.Ordinal,
            CardShortlistConditioningPolicyId, conditioningFingerprint,
            hasNativeIndexLaw
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "full-pool-shuffle-shortlist-law-unverified",
            outcomes, hasNativeIndexLaw
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }

    public static ChanceLawV111 QueryDraw(SemanticDeckStateV111 deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            deck.Pending is not
            {
                SiteId: "combat.draw_pile.draw_card",
                Purpose: "draw"
            })
            throw new InvalidOperationException(
                "Chance Law draw query requires one unresolved Nature draw_card event.");

        return QueryDeckReveal(deck);
    }

    /// <summary>
    /// Query the conditional identity distribution for a pending draw, peek,
    /// or scry reveal. A known position has no pending event and therefore
    /// cannot be sampled again through this API.
    /// </summary>
    public static ChanceLawV111 QueryDeckReveal(
        SemanticDeckStateV111 deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        SemanticDeckEngineV111.Validate(deck);
        var pending = deck.Pending;
        if (deck.Mode != SemanticDeckStateV111.NatureMode ||
            pending is null ||
            (pending.Purpose, pending.SiteId) is not
                ("draw", "combat.draw_pile.draw_card") and not
                ("peek", "combat.draw_pile.reveal_card") and not
                ("scry", "combat.draw_pile.reveal_card"))
            throw new InvalidOperationException(
                "Chance Law reveal query requires one unresolved Nature deck event.");
        var drawEvent = SemanticDeckEngineV111.Inspect(deck) ??
            throw new InvalidDataException("Pending deck reveal disappeared.");
        var outcomes = pending.CandidateCardInstanceIds
            .Select(cardId => new ChanceLawOutcomeV111(
                "card_instance:" + cardId.ToString(CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var policy = pending.Purpose == "draw"
            ? DrawConditioningPolicyId
            : RevealConditioningPolicyId;
        return Create(drawEvent.EventId, drawEvent.SiteId,
            drawEvent.OperationKind, drawEvent.OwnerId, drawEvent.Ordinal,
            policy, deck.Fingerprint,
            "uniform-deferred-permutation-model-unverified", outcomes);
    }
}
