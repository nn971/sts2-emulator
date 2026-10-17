using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>An exact, integer-weighted outcome in a conditional chance law.</summary>
public sealed record ChanceLawOutcomeV111(string OutcomeId, long Weight);

public sealed record StochasticCapabilityManifestV111(
    string SchemaId,
    string ChanceLawSchemaId,
    string ReferenceNatureProviderId,
    string ReferenceNatureSessionSchemaId,
    string ProbabilityStatus,
    string[] NatureChoiceSiteIds,
    string PlayerObservationSchemaId,
    string[] ProviderModes,
    string[] ReferenceNatureOperations)
{
    public string ReferenceNatureRunActionSessionSchemaId { get; init; } =
        ReferenceNatureRunActionSessionV111.CurrentSchemaId;
    public string ReferenceNatureEventChoiceSessionSchemaId { get; init; } =
        ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId;
}

public static class StochasticCapabilitiesV111
{
    public const string SchemaId = "sts2-v111-stochastic-capabilities-v1";
    public const string ExternalSelectionProviderId =
        "external-semantic-selection-v1";

    public static StochasticCapabilityManifestV111 Create() => new(
        SchemaId,
        ChanceLawV111.CurrentSchemaId,
        ReferenceNatureV111.ProviderId,
        ReferenceNatureSessionV111.CurrentSchemaId,
        ChanceLawV111.UnverifiedStatus,
        StochasticSiteRegistryV111.NatureChoiceSiteIds,
        PrototypeAiEnvironment.SchemaId,
        [
            ReferenceNatureV111.ProviderId,
            SemanticNativePolicyAuditV111.NativeProviderId,
            ExternalSelectionProviderId
        ],
        [
            "semantic_reference_begin", "semantic_reference_observe",
            "semantic_reference_step", "semantic_reference_resolve_next",
            "semantic_reference_status", "semantic_reference_fork",
            "semantic_reference_save", "semantic_reference_load",
            "semantic_reference_release_many",
            "reference_nature_run_action_begin",
            "reference_nature_run_action_status",
            "reference_nature_run_action_resolve_next",
            "reference_nature_run_action_fork",
            "reference_nature_run_action_save",
            "reference_nature_run_action_load",
            "reference_nature_run_action_release_many",
            "reference_nature_event_choice_begin",
            "reference_nature_event_choice_status",
            "reference_nature_event_choice_resolve_next",
            "reference_nature_event_choice_advance",
            "reference_nature_event_choice_fork",
            "reference_nature_event_choice_save",
            "reference_nature_event_choice_load",
            "reference_nature_event_choice_release_many"
        ])
    {
        ReferenceNatureRunActionSessionSchemaId =
            ReferenceNatureRunActionSessionV111.CurrentSchemaId,
        ReferenceNatureEventChoiceSessionSchemaId =
            ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId
    };
}

/// <summary>
/// Read-only conditional distribution for one unresolved semantic event.
/// EventId correlates the query with mechanics; LawId fingerprints only the
/// declared support and conditioning, never the full engine boundary identity.
/// Weights are exact integer numerators over TotalWeight; provenance makes no
/// claim that an emulator model has been verified against the original game.
/// </summary>
public sealed record ChanceLawV111(
    string SchemaId,
    string LawId,
    string EventId,
    string SiteId,
    string OperationKind,
    string OwnerId,
    int Ordinal,
    string SupportFingerprint,
    string ConditioningPolicyId,
    string ConditioningFingerprint,
    string ProbabilityStatus,
    string ProbabilityProvenance,
    ChanceLawOutcomeV111[] Outcomes,
    long TotalWeight)
{
    public const string CurrentSchemaId = "sts2-v111-chance-law-v1";
    public const string UnverifiedStatus = "UNVERIFIED";

    public ChanceLawV111 Fork() => this with
    {
        Outcomes = (ChanceLawOutcomeV111[])Outcomes.Clone()
    };

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(SiteId) ||
            string.IsNullOrWhiteSpace(OperationKind) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            string.IsNullOrWhiteSpace(ConditioningPolicyId) ||
            string.IsNullOrWhiteSpace(ConditioningFingerprint) ||
            ProbabilityStatus != UnverifiedStatus ||
            string.IsNullOrWhiteSpace(ProbabilityProvenance) ||
            Outcomes is not { Length: > 0 } || TotalWeight <= 0)
            throw new InvalidDataException("Invalid Chance Law envelope.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var outcome in Outcomes)
        {
            if (outcome is null || string.IsNullOrWhiteSpace(outcome.OutcomeId) ||
                outcome.Weight <= 0 || !ids.Add(outcome.OutcomeId))
                throw new InvalidDataException("Invalid Chance Law support.");
            total = checked(total + outcome.Weight);
        }

        if (total != TotalWeight ||
            SupportFingerprint != CanonicalJson.Sha256(Outcomes))
            throw new InvalidDataException(
                "Chance Law support fingerprint or total weight is inconsistent.");

        var expectedLawId = CanonicalJson.Sha256(new
        {
            SchemaId,
            SiteId,
            OperationKind,
            OwnerId,
            Ordinal,
            SupportFingerprint,
            ConditioningPolicyId,
            ConditioningFingerprint,
            ProbabilityStatus,
            ProbabilityProvenance
        });
        if (LawId != expectedLawId)
            throw new InvalidDataException("Chance Law identifier is inconsistent.");
    }
}

/// <summary>
/// Outcome-blind queries for supported semantic Nature sites. Queries inspect
/// only declared semantic support and provider constraints; Native RNG state
/// and selected current outcomes are not inputs.
/// </summary>
public static class ChanceLawEngineV111
{
    public const string IntentConditioningPolicyId =
        "source-filtered-intent-weights-v1";
    public const string EndTurnIndexConditioningPolicyId =
        "uniform-saved-automatic-index-support-v1";
    public const string RandomIndexConditioningPolicyId =
        "uniform-saved-random-index-support-v1";
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
    public const string CardShortlistConditioningPolicyId =
        "uniform-sequential-without-replacement-v1";
    public const string EventOfferConditioningPolicyId =
        "uniform-ordered-event-offer-support-v1";

    public static ChanceLawV111 QueryIntent(SemanticIntentEventV111 intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        intent.Validate();
        var outcomes = intent.Support.Candidates
            .Select(candidate => new ChanceLawOutcomeV111(
                candidate.IntentId, candidate.RawWeight))
            .ToArray();
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
            conditioningFingerprint, "source-shaped-unverified", outcomes);
    }

    /// <summary>
    /// Query the declared uniform index law at a suspended automatic
    /// enemy-action boundary. This preserves index identity even when
    /// candidate values repeat.
    /// </summary>
    public static ChanceLawV111 QueryEndTurnIndex(
        SemanticEndTurnIndexEventV111 indexEvent)
    {
        ArgumentNullException.ThrowIfNull(indexEvent);
        indexEvent.Validate();
        var outcomes = Enumerable.Range(0,
                indexEvent.Request.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "choice_index:" + index.ToString(CultureInfo.InvariantCulture),
                1L))
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
            "uniform-saved-enemy-action-index-support-unverified", outcomes);
    }

    /// <summary>Query a uniform law over the legal indices in a saved typed
    /// random request. Only the typed request enters the law fingerprint; the
    /// selected index, enclosing run state and private RNG state do not.</summary>
    public static ChanceLawV111 QueryRandomIndex(
        string eventId, RandomIndexRequestV111 request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var outcomes = Enumerable.Range(0,
                request.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "choice_index:" + index.ToString(CultureInfo.InvariantCulture),
                1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            RequestFingerprint = CanonicalJson.Sha256(request),
            policy = RandomIndexConditioningPolicyId
        });
        return Create(eventId, request.SiteId,
            request.OperationKind, request.OwnerId, request.Ordinal,
            RandomIndexConditioningPolicyId, conditioningFingerprint,
            "uniform-saved-index-support-unverified", outcomes);
    }

    /// <summary>
    /// Query Orobas's pre-event offer law over stable choice identities. The
    /// same eligible support builder is used by the opening resolver; neither
    /// a selected index nor private RNG state enters this query.
    /// </summary>
    public static ChanceLawV111 QueryOrobasOffer(
        RunState run, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(run);
        NativeOrobasEventV111.ValidateOpeningBoundary(run);
        var siteId = NativeOrobasEventV111.OfferSiteId;
        var ownerId = "event:OROBAS:act:2:floor:0";
        var candidates = NativeOrobasEventV111.OfferCandidateIds(
            run.Player, ordinal);
        var outcomes = candidates.Select(choiceId =>
            new ChanceLawOutcomeV111("event_choice:" + choiceId, 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            ownerId,
            ordinal,
            CandidateChoiceIds = candidates,
            policy = EventOfferConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        return Create(eventId, siteId, "event_offer_index", ownerId,
            ordinal, EventOfferConditioningPolicyId,
            conditioningFingerprint,
            "uniform-eligible-event-offer-pool-unverified", outcomes);
    }

    /// <summary>Query a controlled legacy run-action request without exposing
    /// the selected result. The event identity binds the action and request
    /// context without incorporating private run-provider state.</summary>
    public static ChanceLawV111 QueryControlledRunAction(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        continuation.Validate();
        var boundaryFingerprint = ControlledRunActionBoundaryFingerprint(run);
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ControlledRunActionEngineV111.SchemaId,
            boundaryFingerprint
        });
        return QueryRandomIndex(eventId, continuation.PendingRequest);
    }

    /// <summary>Fingerprint the pending controlled action and request without
    /// hashing the enclosing RunState (which contains private provider state).</summary>
    public static string ControlledRunActionBoundaryFingerprint(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        continuation.Validate();
        return CanonicalJson.Sha256(new
        {
            schemaId = ControlledRunActionEngineV111.SchemaId,
            continuation.ActionId,
            continuation.MapNodeId,
            request = continuation.PendingRequest,
            candidateLabels = continuation.PendingCandidateLabels
        });
    }

    public static ChanceLawV111 QueryControlledEventChoice(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var description = DescribeControlledEventChoice(run);
        return QueryRandomIndex(description.EventId, description.Request);
    }

    public static string ControlledEventChoiceBoundaryFingerprint(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return DescribeControlledEventChoice(run).BoundaryFingerprint;
    }

    private static (string EventId, string ChoiceId,
        RandomIndexRequestV111 Request, string[] CandidateLabels,
        string BoundaryFingerprint) DescribeControlledEventChoice(RunState run)
    {
        var effect = run.World?.Event?.ControlledChoice;
        var reward = run.ControlledEventRewardChoice;
        if ((effect is null) == (reward is null))
            throw new InvalidOperationException(
                "A controlled event-choice law requires exactly one pending request.");
        effect?.Validate();
        reward?.Validate();
        var eventId = effect?.EventId ?? reward!.EventId;
        var choiceId = effect?.ChoiceId ?? reward!.ChoiceId;
        var request = effect?.PendingRequest ?? reward!.PendingRequest;
        var candidateLabels = effect?.PendingCandidateLabels ??
            reward!.PendingCandidateLabels;
        var boundaryFingerprint = CanonicalJson.Sha256(new
        {
            schemaId = ControlledEventChoiceEngineV111.SchemaId,
            kind = effect is null ? "event_reward_random_choice" :
                "event_random_choice",
            eventId,
            choiceId,
            nextEffectIndex = effect?.NextEffectIndex,
            decisionPrefixLength = reward?.DecisionPrefixLength,
            request,
            candidateLabels
        });
        var chanceEventId = CanonicalJson.Sha256(new
        {
            schemaId = ControlledEventChoiceEngineV111.SchemaId,
            boundaryFingerprint
        });
        return (chanceEventId, choiceId, request, candidateLabels,
            boundaryFingerprint);
    }

    /// <summary>
    /// Query the uniform conditional law over the live physical enemy
    /// identities at a suspended random-target boundary. The selected index
    /// is not part of the event, so querying cannot depend on a prior result.
    /// </summary>
    public static ChanceLawV111 QueryTarget(SemanticTargetEventV111 target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();
        var outcomes = target.CandidateEnemyInstanceIds
            .Select(instanceId => new ChanceLawOutcomeV111(
                "enemy_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), 1L))
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
            "uniform-live-source-support-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform law over the ordered, duplicate-free generated-card
    /// model pool saved at a source effect boundary.
    /// </summary>
    public static ChanceLawV111 QueryGeneratedCard(
        SemanticGeneratedCardEventV111 generatedCard)
    {
        ArgumentNullException.ThrowIfNull(generatedCard);
        generatedCard.Validate();
        var outcomes = generatedCard.CandidateCardIds
            .Select(cardId => new ChanceLawOutcomeV111(
                "card_model:" + cardId, 1L))
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
            "uniform-ordered-model-pool-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform bounded-integer law declared by a pending semantic
    /// cost event. Card identity and source stage remain in its conditioning
    /// fingerprint through the event's support fingerprint.
    /// </summary>
    public static ChanceLawV111 QueryCost(SemanticCostEventV111 cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        cost.Validate();
        var outcomes = cost.CandidateCosts
            .Select(value => new ChanceLawOutcomeV111(
                "cost:" + value.ToString(CultureInfo.InvariantCulture), 1L))
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
            "uniform-bounded-cost-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform law over physical card instances in the ordered
    /// source support for a random pile-card effect.
    /// </summary>
    public static ChanceLawV111 QueryRandomPileCard(
        SemanticRandomPileCardEventV111 pileCard)
    {
        ArgumentNullException.ThrowIfNull(pileCard);
        pileCard.Validate();
        var outcomes = pileCard.CandidateCardInstanceIds
            .Select(instanceId => new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), 1L))
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
            "uniform-physical-pile-card-support-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform law over replacement models for one saved physical
    /// card transform. The source card identity and model are conditioned on.
    /// </summary>
    public static ChanceLawV111 QueryCardTransform(
        SemanticCardTransformEventV111 transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        transform.Validate();
        var outcomes = transform.CandidateCardIds
            .Select(cardId => new ChanceLawOutcomeV111(
                "card_model:" + cardId, 1L))
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
            "uniform-transform-pool-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform law over the ordered source potion-model pool for
    /// one pending generated-potion choice.
    /// </summary>
    public static ChanceLawV111 QueryPotionGeneration(
        SemanticPotionGenerationEventV111 potion)
    {
        ArgumentNullException.ThrowIfNull(potion);
        potion.Validate();
        var outcomes = potion.CandidatePotionIds
            .Select(potionId => new ChanceLawOutcomeV111(
                "potion_model:" + potionId, 1L))
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
            "uniform-potion-model-pool-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform law over eligible physical hand-card identities for
    /// a pending fixed-cost operation.
    /// </summary>
    public static ChanceLawV111 QueryHandCostSelection(
        SemanticHandCostSelectionEventV111 selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        selection.Validate();
        var outcomes = selection.CandidateCardInstanceIds
            .Select(instanceId => new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), 1L))
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
            "uniform-hand-card-support-unverified", outcomes);
    }

    /// <summary>
    /// Query one uniform Fisher–Yates swap in a saved without-replacement
    /// pool shuffle. Prior swaps remain part of the conditioned state.
    /// </summary>
    public static ChanceLawV111 QueryPoolShuffle(
        SemanticPoolShuffleEventV111 shuffle)
    {
        ArgumentNullException.ThrowIfNull(shuffle);
        shuffle.Validate();
        var outcomes = Enumerable.Range(0, shuffle.Request.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(CultureInfo.InvariantCulture),
                1L))
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
            "uniform-fisher-yates-swap-unverified", outcomes);
    }

    /// <summary>
    /// Query the uniform bounded insertion position for a generated card.
    /// The eligible physical pile and generated model are in the saved
    /// support fingerprint.
    /// </summary>
    public static ChanceLawV111 QueryDrawInsertion(
        SemanticDrawInsertionEventV111 insertion)
    {
        ArgumentNullException.ThrowIfNull(insertion);
        insertion.Validate();
        var outcomes = Enumerable.Range(0, insertion.Request.SupportCount)
            .Select(position => new ChanceLawOutcomeV111(
                "draw_position:" + position.ToString(
                    CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            insertion.SupportFingerprint,
            insertion.SiteId,
            insertion.OwnerId,
            insertion.Ordinal,
            insertion.GeneratedCardModelId,
            policy = DrawInsertionConditioningPolicyId
        });
        return Create(insertion.EventId, insertion.SiteId,
            insertion.OperationKind, insertion.OwnerId, insertion.Ordinal,
            DrawInsertionConditioningPolicyId, conditioningFingerprint,
            "uniform-bounded-insertion-position-unverified", outcomes);
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
        var outcomes = shortlist.CandidateCardInstanceIds
            .Select(instanceId => new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), 1L))
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
            "uniform-sequential-card-shortlist-unverified", outcomes);
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

    private static ChanceLawV111 Create(
        string eventId, string siteId, string operationKind, string ownerId,
        int ordinal, string conditioningPolicyId,
        string conditioningFingerprint, string probabilityProvenance,
        ChanceLawOutcomeV111[] outcomes)
    {
        var totalWeight = outcomes.Aggregate(0L,
            (total, outcome) => checked(total + outcome.Weight));
        var supportFingerprint = CanonicalJson.Sha256(outcomes);
        var lawId = CanonicalJson.Sha256(new
        {
            SchemaId = ChanceLawV111.CurrentSchemaId,
            SiteId = siteId,
            OperationKind = operationKind,
            OwnerId = ownerId,
            Ordinal = ordinal,
            SupportFingerprint = supportFingerprint,
            ConditioningPolicyId = conditioningPolicyId,
            ConditioningFingerprint = conditioningFingerprint,
            ProbabilityStatus = ChanceLawV111.UnverifiedStatus,
            ProbabilityProvenance = probabilityProvenance
        });
        var law = new ChanceLawV111(
            ChanceLawV111.CurrentSchemaId, lawId, eventId, siteId,
            operationKind, ownerId, ordinal, supportFingerprint,
            conditioningPolicyId, conditioningFingerprint,
            ChanceLawV111.UnverifiedStatus, probabilityProvenance,
            outcomes, totalWeight);
        law.Validate();
        return law;
    }
}

/// <summary>Forkable state for the independent ReferenceNature sampler.</summary>
public sealed record ReferenceNatureStateV111(
    string SchemaId, ulong SamplerState, long SampleCount)
{
    public const string CurrentSchemaId = "sts2-v111-reference-nature-state-v1";

    public static ReferenceNatureStateV111 Create(ulong seed) =>
        new(CurrentSchemaId, seed, 0);

    public ReferenceNatureStateV111 Fork() => this;

    public static ReferenceNatureStateV111 ForRunSession(
        RunState run, ulong? seed)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.ReferenceNatureState is { } saved)
        {
            if (seed is not null)
                throw new InvalidOperationException(
                    "This run already owns a ReferenceNature cursor; omit nature_seed to continue it.");
            saved.Validate();
            return saved.Fork();
        }
        if (seed is null)
            throw new InvalidOperationException(
                "A first ReferenceNature session requires nature_seed.");
        return Create(seed.Value);
    }

    public bool SameCursor(ReferenceNatureStateV111? other) =>
        other is not null &&
        CanonicalJson.Sha256(this) == CanonicalJson.Sha256(other);

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || SampleCount < 0)
            throw new InvalidDataException("Invalid ReferenceNature sample count.");
    }
}

/// <summary>
/// Shared lifecycle rules for ReferenceNature sessions attached to a run.
/// Boundary sessions keep their mechanics and transcript types, but cursor
/// initialization, publication, and run-cursor validation have one owner.
/// </summary>
internal static class ReferenceNatureRunCursorV111
{
    public static ReferenceNatureStateV111 Begin(
        RunState source, ulong? seed, string boundary)
    {
        ArgumentNullException.ThrowIfNull(source);
        var cursor = ReferenceNatureStateV111.ForRunSession(source, seed);
        ValidateInitial(source, cursor, boundary);
        return cursor;
    }

    public static void ValidateInitial(
        RunState source,
        ReferenceNatureStateV111 initial,
        string boundary)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(initial);
        if (source.ReferenceNatureState is { } saved &&
            !initial.SameCursor(saved))
            throw new InvalidOperationException(
                $"{boundary} session cannot replace the run's ReferenceNature cursor.");
    }

    public static bool MatchesRunSession(
        RunState origin,
        RunState current,
        ReferenceNatureStateV111 initial,
        ReferenceNatureStateV111 cursor,
        int decisionCount,
        bool complete)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(cursor);

        var initialMatchesOrigin = origin.ReferenceNatureState is not { } saved ||
            initial.SameCursor(saved);
        var pendingRunMatchesOrigin = origin.ReferenceNatureState is { } originCursor
            ? originCursor.SameCursor(current.ReferenceNatureState)
            : current.ReferenceNatureState is null;
        var runMatchesCursor = complete
            ? cursor.SameCursor(current.ReferenceNatureState)
            : pendingRunMatchesOrigin;
        return initialMatchesOrigin && runMatchesCursor &&
            cursor.SampleCount == checked(initial.SampleCount + decisionCount);
    }

    public static RunState Commit(
        RunState run, ReferenceNatureStateV111 cursor)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(cursor);
        cursor.Validate();
        return run with { ReferenceNatureState = cursor.Fork() };
    }
}

public sealed record ReferenceNatureSelectionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex,
    ReferenceNatureStateV111 NextState);

public sealed record ReferenceNatureResolutionV111(
    SemanticPilotCombatV111 Combat, ChanceLawV111 Law,
    ReferenceNatureSelectionV111 Selection);

public sealed record ReferenceNatureSessionSnapshotV111(
    string SchemaId, string CombatSnapshotJson,
    ReferenceNatureStateV111 NatureState);

/// <summary>
/// Atomic ReferenceNature simulation state: semantic mechanics continuation
/// and the independent sampler cursor share one fork/save/restore boundary.
/// </summary>
public sealed record ReferenceNatureSessionV111(
    string SchemaId, SemanticPilotCombatV111 Combat,
    ReferenceNatureStateV111 NatureState)
{
    public const string CurrentSchemaId = "sts2-v111-reference-nature-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    public static ReferenceNatureSessionV111 Begin(
        SemanticPilotCombatV111 combat, ulong seed) => Begin(combat, (ulong?)seed);

    public static ReferenceNatureSessionV111 Begin(
        SemanticPilotCombatV111 combat) => Begin(combat, null);

    private static ReferenceNatureSessionV111 Begin(
        SemanticPilotCombatV111 combat, ulong? seed)
    {
        ArgumentNullException.ThrowIfNull(combat);
        var natureState = ReferenceNatureRunCursorV111.Begin(
            combat.Run, seed, "Semantic combat");
        if (combat.CombatTerminal &&
            !natureState.SameCursor(combat.Run.ReferenceNatureState))
        {
            var committedRun = ReferenceNatureRunCursorV111.Commit(
                combat.Run, natureState);
            combat = combat with
            {
                Run = committedRun,
                PendingEndTurn = combat.PendingEndTurn is { } endTurn
                    ? endTurn with { Run = committedRun }
                    : null
            };
        }
        var session = new ReferenceNatureSessionV111(
            CurrentSchemaId, combat.Fork(), natureState);
        session.Validate();
        return session;
    }

    public ReferenceNatureSessionV111 Fork()
    {
        Validate();
        return this with { Combat = Combat.Fork(), NatureState = NatureState.Fork() };
    }

    public ReferenceNatureSessionV111 ResolveNext()
    {
        Validate();
        var resolution = ReferenceNatureV111.ResolvePending(Combat, NatureState);
        var resolvedCombat = resolution.Combat;
        if (resolvedCombat.CombatTerminal)
        {
            var committedRun = ReferenceNatureRunCursorV111.Commit(
                resolvedCombat.Run, resolution.Selection.NextState);
            resolvedCombat = resolvedCombat with
            {
                Run = committedRun,
                PendingEndTurn = resolvedCombat.PendingEndTurn is { } endTurn
                    ? endTurn with { Run = committedRun }
                    : null
            };
        }
        var child = this with
        {
            Combat = resolvedCombat,
            NatureState = resolution.Selection.NextState
        };
        child.Validate();
        return child;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Combat is null || NatureState is null)
            throw new InvalidDataException("Invalid ReferenceNature session envelope.");
        Combat.Validate();
        NatureState.Validate();
        if (Combat.CombatTerminal &&
            !NatureState.SameCursor(Combat.Run.ReferenceNatureState))
            throw new InvalidDataException(
                "ReferenceNature sampler cursor differs from the committed run state.");
    }

    public static string Save(ReferenceNatureSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureSessionSnapshotV111(
            CurrentSchemaId,
            SemanticPilotCombatEngineV111.Save(session.Combat),
            session.NatureState);
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<ReferenceNatureSessionSnapshotV111>(json)
            ?? throw new InvalidDataException("Null ReferenceNature snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.CombatSnapshotJson) ||
            snapshot.NatureState is null)
            throw new InvalidDataException("Invalid ReferenceNature snapshot.");
        var combat = SemanticPilotCombatEngineV111.Restore(
            snapshot.CombatSnapshotJson);
        if (combat.CombatTerminal && combat.Run.ReferenceNatureState is null)
        {
            var committedRun = combat.Run with
            {
                ReferenceNatureState = snapshot.NatureState.Fork()
            };
            combat = combat with
            {
                Run = committedRun,
                PendingEndTurn = combat.PendingEndTurn is { } endTurn
                    ? endTurn with { Run = committedRun }
                    : null
            };
        }
        var restored = new ReferenceNatureSessionV111(
            CurrentSchemaId, combat, snapshot.NatureState);
        restored.Validate();
        return restored;
    }
}

public sealed record ReferenceNatureDecisionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(OutcomeId) || OutcomeIndex < 0 ||
            OutcomeId != "choice_index:" +
                OutcomeIndex.ToString(CultureInfo.InvariantCulture))
            throw new InvalidDataException(
                "Invalid ReferenceNature decision.");
    }
}

public sealed record ReferenceNatureRunActionSessionSnapshotV111(
    string SchemaId, string OriginSnapshotJson, GameAction Action,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript);

/// <summary>
/// Atomic ReferenceNature envelope for existing controlled run-action
/// continuations. It reuses the run mechanics resolver and the same sampler
/// state as semantic combat sessions.
/// </summary>
public sealed record ReferenceNatureRunActionSessionV111(
    string SchemaId, RunState Origin, GameAction Action, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-run-action-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => Run.ControlledRunActionChoice is null;

    public static ReferenceNatureRunActionSessionV111 Begin(
        RunState source, GameAction action, ulong seed) =>
        Begin(source, action,
            ReferenceNatureRunCursorV111.Begin(
                source, seed, "Run-action"));

    public static ReferenceNatureRunActionSessionV111 Begin(
        RunState source, GameAction action) =>
        Begin(source, action,
            ReferenceNatureRunCursorV111.Begin(
                source, null, "Run-action"));

    private static ReferenceNatureRunActionSessionV111 Begin(
        RunState source, GameAction action,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Run-action");
        var detachedAction = CloneAction(action);
        var run = ControlledRunActionEngineV111.Begin(
            source.Fork(), detachedAction);
        var session = new ReferenceNatureRunActionSessionV111(
            CurrentSchemaId, source.Fork(), detachedAction, run,
            initialNatureState.Fork(), initialNatureState.Fork(), []);
        if (session.Complete)
            session = session with
            {
                Run = ReferenceNatureRunCursorV111.Commit(
                    session.Run, initialNatureState)
            };
        session.Validate();
        return session;
    }

    public ReferenceNatureRunActionSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Action = CloneAction(Action),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            DecisionTranscript = (ReferenceNatureDecisionV111[])
                DecisionTranscript.Clone()
        };
    }

    public ReferenceNatureRunActionSessionV111 ResolveNext()
    {
        Validate();
        var law = ChanceLawEngineV111.QueryControlledRunAction(Run);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var resolvedRun = ControlledRunActionEngineV111.Resolve(
            Run.Fork(), selection.OutcomeIndex);
        if (resolvedRun.ControlledRunActionChoice is null)
            resolvedRun = ReferenceNatureRunCursorV111.Commit(
                resolvedRun, selection.NextState);
        var next = this with
        {
            Run = resolvedRun,
            NatureState = selection.NextState,
            DecisionTranscript = DecisionTranscript.Append(
                new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)).ToArray()
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Action is null ||
            Run is null || InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null || string.IsNullOrWhiteSpace(Action.Kind) ||
            (Run.ControlledRunActionChoice is null &&
             Run.ControlledRunActionHistory is not { Length: > 0 }))
            throw new InvalidDataException(
                "Invalid ReferenceNature run-action session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature run-action cursor differs from the run envelope.");
        for (int index = 0; index < DecisionTranscript.Length; index++)
            DecisionTranscript[index].Validate();

        if (Run.ControlledRunActionChoice is { } continuation)
        {
            continuation.Validate();
            if (continuation.ActionId != Action.Kind ||
                continuation.Decisions.Length != DecisionTranscript.Length)
                throw new InvalidDataException(
                    "Pending run-action continuation differs from ReferenceNature history.");
            for (int index = 0; index < continuation.Decisions.Length; index++)
                if (continuation.Decisions[index].ProviderMode !=
                        "controlled-v111" ||
                    continuation.Decisions[index].SelectedIndex !=
                        DecisionTranscript[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "Pending run-action decisions differ from ReferenceNature history.");
        }
        else
        {
            var trace = Run.ControlledRunActionHistory![^1];
            trace.Validate();
            if (trace.ActionId != Action.Kind ||
                trace.Decisions.Length != DecisionTranscript.Length)
                throw new InvalidDataException(
                    "Completed run-action trace differs from ReferenceNature history.");
            for (int index = 0; index < trace.Decisions.Length; index++)
                if (trace.Decisions[index].ProviderMode != "controlled-v111" ||
                    trace.Decisions[index].SelectedIndex !=
                        DecisionTranscript[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "Completed run-action outcomes differ from ReferenceNature history.");
        }
    }

    public static string Save(ReferenceNatureRunActionSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureRunActionSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            CloneAction(session.Action), RunSnapshot.Save(session.Run),
            session.InitialNatureState, session.NatureState,
            (ReferenceNatureDecisionV111[])
                session.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureRunActionSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureRunActionSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature run-action snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            snapshot.Action is null ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature run-action snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.Action,
            snapshot.InitialNatureState);
        for (int index = 0; index < snapshot.DecisionTranscript.Length; index++)
        {
            if (replay.Complete)
                throw new InvalidDataException(
                    "ReferenceNature run-action replay ended before its transcript.");
            replay = replay.ResolveNext();
            if (CanonicalJson.Sha256(replay.DecisionTranscript[^1]) !=
                CanonicalJson.Sha256(snapshot.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "ReferenceNature run-action replay selected a different outcome.");
        }
        if (replay.Complete && savedRun.ReferenceNatureState is null)
            savedRun = savedRun with
            {
                ReferenceNatureState = snapshot.NatureState.Fork()
            };
        if (CanonicalJson.Sha256(replay.Run) != CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            replay.DecisionTranscript.Length != snapshot.DecisionTranscript.Length)
            throw new InvalidDataException(
                "ReferenceNature run-action replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}

public sealed record ReferenceNatureControlledEventChoiceStepV111(
    string Kind, ReferenceNatureDecisionV111? NatureDecision = null,
    GameAction? PlayerAction = null)
{
    public void Validate()
    {
        if (Kind == "nature")
        {
            if (NatureDecision is null || PlayerAction is not null)
                throw new InvalidDataException(
                    "Malformed ReferenceNature event-choice sample step.");
            NatureDecision.Validate();
            return;
        }
        if (Kind == "player_action")
        {
            if (PlayerAction is null || NatureDecision is not null ||
                string.IsNullOrWhiteSpace(PlayerAction.Kind))
                throw new InvalidDataException(
                    "Malformed ReferenceNature event-choice player step.");
            return;
        }
        throw new InvalidDataException(
            "Unknown ReferenceNature event-choice session step.");
    }

    public ReferenceNatureControlledEventChoiceStepV111 Fork() => this with
    {
        PlayerAction = PlayerAction is null
            ? null : CloneAction(PlayerAction)
    };

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}

public sealed record ReferenceNatureControlledEventChoiceSnapshotV111(
    string SchemaId, string OriginSnapshotJson, string ChoiceId,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureControlledEventChoiceStepV111[] Steps);

/// <summary>
/// Atomic ReferenceNature envelope for controlled event-effect and
/// post-reward event-choice continuations. Player reward actions and Nature
/// samples share one ordered replay transcript and sampler state.
/// </summary>
public sealed record ReferenceNatureControlledEventChoiceSessionV111(
    string SchemaId, RunState Origin, string ChoiceId, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureControlledEventChoiceStepV111[] Steps)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-event-choice-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool NaturePending => Run.World?.Event?.ControlledChoice is not null ||
        Run.ControlledEventRewardChoice is not null;

    [JsonIgnore]
    public bool CanAdvancePlayer => !NaturePending &&
        Run.Phase == RunPhase.Reward && Run.World?.Event is { } eventState &&
        eventState.ChosenChoiceId == ChoiceId &&
        Run.World.Reward is
        {
            AddRandomCurseAfterReward: true,
            SourceRoom: var sourceRoom
        } && sourceRoom == "Event:" + eventState.EventId &&
        Run.ControlledEventChoiceHistory is { Length: > 0 } history &&
        history[^1].EventId == eventState.EventId &&
        history[^1].ChoiceId == ChoiceId;

    [JsonIgnore]
    public bool Complete => !NaturePending && !CanAdvancePlayer;

    [JsonIgnore]
    public ReferenceNatureDecisionV111[] DecisionTranscript => Steps
        .Where(step => step.Kind == "nature")
        .Select(step => step.NatureDecision!)
        .ToArray();

    public static ReferenceNatureControlledEventChoiceSessionV111 Begin(
        RunState source, string choiceId, ulong seed) =>
        Begin(source, choiceId,
            ReferenceNatureRunCursorV111.Begin(
                source, seed, "Event-choice"));

    public static ReferenceNatureControlledEventChoiceSessionV111 Begin(
        RunState source, string choiceId) =>
        Begin(source, choiceId,
            ReferenceNatureRunCursorV111.Begin(
                source, null, "Event-choice"));

    private static ReferenceNatureControlledEventChoiceSessionV111 Begin(
        RunState source, string choiceId,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Event-choice");
        var run = ControlledEventChoiceEngineV111.Begin(
            source.Fork(), choiceId);
        var session = new ReferenceNatureControlledEventChoiceSessionV111(
            CurrentSchemaId, source.Fork(), choiceId, run,
            initialNatureState.Fork(), initialNatureState.Fork(), []);
        if (session.Complete)
            session = session with
            {
                Run = ReferenceNatureRunCursorV111.Commit(
                    session.Run, initialNatureState)
            };
        session.Validate();
        return session;
    }

    public ReferenceNatureControlledEventChoiceSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            Steps = Steps.Select(step => step.Fork()).ToArray()
        };
    }

    public ReferenceNatureControlledEventChoiceSessionV111 ResolveNext()
    {
        Validate();
        if (!NaturePending)
            throw new InvalidOperationException(
                "No controlled event Nature outcome is pending.");
        var law = ChanceLawEngineV111.QueryControlledEventChoice(Run);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var resolvedRun = ControlledEventChoiceEngineV111.Resolve(
            Run.Fork(), selection.OutcomeIndex);
        var next = this with
        {
            Run = resolvedRun,
            NatureState = selection.NextState,
            Steps = Steps.Append(new ReferenceNatureControlledEventChoiceStepV111(
                "nature", new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex))).ToArray()
        };
        if (next.Complete)
            next = next with
            {
                Run = next.Run with
                {
                    ReferenceNatureState = selection.NextState
                }
            };
        next.Validate();
        return next;
    }

    public ReferenceNatureControlledEventChoiceSessionV111 AdvancePlayerAction(
        GameAction action)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(action);
        if (!CanAdvancePlayer)
            throw new InvalidOperationException(
                "No supported event reward player action is pending.");
        var engine = new PrototypeGameEngine();
        var actionId = PrototypeAiEnvironment.StableActionId(action);
        if (!engine.GetLegalActions(Run).Any(candidate =>
                StringComparer.Ordinal.Equals(
                    PrototypeAiEnvironment.StableActionId(candidate), actionId)))
            throw new InvalidOperationException(
                "ReferenceNature event advance requires a current legal reward action.");
        var nextRun = ControlledEventChoiceEngineV111.Advance(
            Run.Fork(), CloneAction(action));
        var next = this with
        {
            Run = nextRun,
            Steps = Steps.Append(new ReferenceNatureControlledEventChoiceStepV111(
                "player_action", PlayerAction: CloneAction(action))).ToArray()
        };
        if (next.Complete)
            next = next with
            {
                Run = ReferenceNatureRunCursorV111.Commit(
                    next.Run, NatureState)
            };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            string.IsNullOrWhiteSpace(ChoiceId) || InitialNatureState is null ||
            NatureState is null || Steps is null ||
            Origin.Phase != RunPhase.Event || Origin.World?.Event is not { } originEvent ||
            !PrototypeGameEngine.IsControlledEventChoice(
                originEvent.EventId, ChoiceId))
            throw new InvalidDataException(
                "Invalid ReferenceNature controlled event-choice envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();
        var decisions = DecisionTranscript;
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                decisions.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature event-choice cursor differs from the run envelope.");
        foreach (var step in Steps)
            step.Validate();

        var effect = Run.World?.Event?.ControlledChoice;
        var reward = Run.ControlledEventRewardChoice;
        if (effect is not null && reward is not null)
            throw new InvalidDataException(
                "Run state contains two simultaneous controlled event requests.");
        if (effect is null && reward is null)
        {
            if (Run.ControlledEventChoiceHistory is not { Length: > 0 } history ||
                history[^1].EventId != originEvent.EventId ||
                history[^1].ChoiceId != ChoiceId ||
                history[^1].Decisions.Length != decisions.Length)
                throw new InvalidDataException(
                    "Completed ReferenceNature event choice differs from its mechanics trace.");
            history[^1].Validate();
            for (int index = 0; index < decisions.Length; index++)
                if (history[^1].Decisions[index].SelectedIndex !=
                    decisions[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "Completed event-choice outcomes differ from ReferenceNature history.");
            return;
        }

        var continuationDecisions = effect?.Decisions ?? reward!.Decisions;
        if (continuationDecisions.Length != decisions.Length ||
            (effect?.EventId ?? reward!.EventId) != originEvent.EventId ||
            (effect?.ChoiceId ?? reward!.ChoiceId) != ChoiceId)
            throw new InvalidDataException(
                "Pending event continuation differs from ReferenceNature history.");
        for (int index = 0; index < decisions.Length; index++)
            if (continuationDecisions[index].ProviderMode != "controlled-v111" ||
                continuationDecisions[index].SelectedIndex != decisions[index].OutcomeIndex)
                throw new InvalidDataException(
                    "Pending event outcomes differ from ReferenceNature history.");
    }

    public static string Save(
        ReferenceNatureControlledEventChoiceSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureControlledEventChoiceSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin), session.ChoiceId,
            RunSnapshot.Save(session.Run), session.InitialNatureState,
            session.NatureState,
            session.Steps.Select(step => step.Fork()).ToArray());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureControlledEventChoiceSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureControlledEventChoiceSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature event-choice snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.ChoiceId) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.Steps is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature event-choice snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.ChoiceId,
            snapshot.InitialNatureState);
        foreach (var savedStep in snapshot.Steps)
        {
            savedStep.Validate();
            replay = savedStep.Kind switch
            {
                "nature" when replay.NaturePending => replay.ResolveNext(),
                "player_action" when replay.CanAdvancePlayer =>
                    replay.AdvancePlayerAction(savedStep.PlayerAction!),
                _ => throw new InvalidDataException(
                    "ReferenceNature event-choice replay step is out of order.")
            };
            if (CanonicalJson.Sha256(replay.Steps[^1]) !=
                CanonicalJson.Sha256(savedStep))
                throw new InvalidDataException(
                    "ReferenceNature event-choice replay diverged from its transcript.");
        }
        if (replay.Complete && savedRun.ReferenceNatureState is null)
            savedRun = savedRun with
            {
                ReferenceNatureState = snapshot.NatureState.Fork()
            };
        if (CanonicalJson.Sha256(replay.Run) != CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            CanonicalJson.Sha256(replay.Steps) !=
                CanonicalJson.Sha256(snapshot.Steps))
            throw new InvalidDataException(
                "ReferenceNature event-choice replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}

/// <summary>
/// Small seeded reference policy that samples the declared law using an
/// independent SplitMix64 stream. It does not use or advance a game RNG.
/// </summary>
public static class ReferenceNatureV111
{
    public const string ProviderId = "reference-nature-splitmix64-v1";
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

    /// <summary>
    /// Sample and resolve the next supported Nature site through the existing
    /// semantic mechanics continuation. The returned child and next sampler
    /// state are published together only after resolution succeeds.
    /// </summary>
    public static ReferenceNatureResolutionV111 ResolvePending(
        SemanticPilotCombatV111 combat, ReferenceNatureStateV111 state)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(state);
        combat.Validate();
        state.Validate();

        if (combat.IntentPending is not null)
        {
            var intentEvent = SemanticPilotCombatEngineV111
                .DescribePendingIntentEvent(combat);
            var law = ChanceLawEngineV111.QueryIntent(intentEvent);
            var selection = SampleValidated(law, state);
            var child = SemanticPilotCombatEngineV111.ResolveIntent(
                combat, combat.Fingerprint, selection.OutcomeId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.EnemyActionEventPending is { } enemyActionEvent)
        {
            var law = ChanceLawEngineV111.QueryEndTurnIndex(
                enemyActionEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != enemyActionEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    enemyActionEvent.Request.SupportCount ||
                selection.OutcomeId != "choice_index:" +
                    selection.OutcomeIndex.ToString(
                        CultureInfo.InvariantCulture))
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending enemy-action index event.");
            var child = SemanticPilotCombatEngineV111.Resolve(
                combat, combat.Fingerprint, selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.TargetPending is { } targetEvent)
        {
            var law = ChanceLawEngineV111.QueryTarget(targetEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != targetEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    targetEvent.CandidateEnemyInstanceIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending target event.");
            var enemyInstanceId =
                targetEvent.CandidateEnemyInstanceIds[selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveRandomTarget(
                combat, combat.Fingerprint, targetEvent.EventId,
                enemyInstanceId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.GeneratedCardPending is { } generatedCardEvent)
        {
            var law = ChanceLawEngineV111.QueryGeneratedCard(
                generatedCardEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != generatedCardEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    generatedCardEvent.CandidateCardIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending generated-card event.");
            var cardId = generatedCardEvent.CandidateCardIds[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveGeneratedCard(
                combat, combat.Fingerprint, generatedCardEvent.EventId,
                cardId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.CostPending is { } costEvent)
        {
            var law = ChanceLawEngineV111.QueryCost(costEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != costEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= costEvent.CandidateCosts.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending cost event.");
            var selectedCost = costEvent.CandidateCosts[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveCost(
                combat, combat.Fingerprint, costEvent.EventId,
                selectedCost);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.RandomPileCardPending is { } randomPileCardEvent)
        {
            var law = ChanceLawEngineV111.QueryRandomPileCard(
                randomPileCardEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != randomPileCardEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    randomPileCardEvent.CandidateCardInstanceIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending pile-card event.");
            var cardInstanceId = randomPileCardEvent
                .CandidateCardInstanceIds[selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveRandomPileCard(
                combat, combat.Fingerprint, randomPileCardEvent.EventId,
                cardInstanceId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.CardTransformPending is { } transformEvent)
        {
            var law = ChanceLawEngineV111.QueryCardTransform(transformEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != transformEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    transformEvent.CandidateCardIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending transform event.");
            var cardId = transformEvent.CandidateCardIds[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveCardTransform(
                combat, combat.Fingerprint, transformEvent.EventId, cardId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.PotionGenerationPending is { } potionEvent)
        {
            var law = ChanceLawEngineV111.QueryPotionGeneration(potionEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != potionEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= potionEvent.CandidatePotionIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending potion event.");
            var potionId = potionEvent.CandidatePotionIds[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolvePotionGeneration(
                combat, combat.Fingerprint, potionEvent.EventId, potionId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.HandCostSelectionPending is { } handCostEvent)
        {
            var law = ChanceLawEngineV111.QueryHandCostSelection(
                handCostEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != handCostEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    handCostEvent.CandidateCardInstanceIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending hand-cost event.");
            var cardInstanceId = handCostEvent.CandidateCardInstanceIds[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111
                .ResolveHandCostSelection(
                    combat, combat.Fingerprint, handCostEvent.EventId,
                    cardInstanceId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.PoolShufflePending is { } poolShuffleEvent)
        {
            var law = ChanceLawEngineV111.QueryPoolShuffle(poolShuffleEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != poolShuffleEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= poolShuffleEvent.Request.SupportCount)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending pool-shuffle event.");
            var child = SemanticPilotCombatEngineV111.ResolvePoolShuffle(
                combat, combat.Fingerprint, poolShuffleEvent.EventId,
                selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.DrawInsertionPending is { } insertionEvent)
        {
            var law = ChanceLawEngineV111.QueryDrawInsertion(insertionEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != insertionEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= insertionEvent.Request.SupportCount)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending draw-insertion event.");
            var child = SemanticPilotCombatEngineV111.ResolveDrawInsertion(
                combat, combat.Fingerprint, insertionEvent.EventId,
                selection.OutcomeIndex);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.CardShortlistPending is { } shortlistEvent)
        {
            var law = ChanceLawEngineV111.QueryCardShortlist(shortlistEvent);
            var selection = SampleValidated(law, state);
            if (selection.EventId != shortlistEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    shortlistEvent.CandidateCardInstanceIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending shortlist event.");
            var cardInstanceId = shortlistEvent.CandidateCardInstanceIds[
                selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveCardShortlist(
                combat, combat.Fingerprint, shortlistEvent.EventId,
                cardInstanceId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.DrawPending is { } drawEvent)
        {
            var deck = combat.Run.World?.Combat?.SemanticDeck ??
                throw new InvalidDataException(
                    "Pending semantic draw lost its provider-owned deck.");
            var law = ChanceLawEngineV111.QueryDraw(deck);
            var selection = SampleValidated(law, state);
            if (selection.EventId != drawEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >= drawEvent.CandidateCardInstanceIds.Length)
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending draw event.");
            var cardInstanceId =
                drawEvent.CandidateCardInstanceIds[selection.OutcomeIndex];
            var child = SemanticPilotCombatEngineV111.ResolveDrawCard(
                combat, combat.Fingerprint, drawEvent.EventId,
                cardInstanceId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.DeckRevealPending is { } revealEvent)
        {
            var deck = combat.Run.World?.Combat?.SemanticDeck ??
                throw new InvalidDataException(
                    "Pending semantic reveal lost its provider-owned deck.");
            var law = ChanceLawEngineV111.QueryDeckReveal(deck);
            var selection = SampleValidated(law, state);
            if (selection.EventId != revealEvent.EventId ||
                selection.OutcomeIndex < 0 ||
                selection.OutcomeIndex >=
                    revealEvent.CandidateCardInstanceIds.Length ||
                selection.OutcomeId != "card_instance:" +
                    revealEvent.CandidateCardInstanceIds[
                        selection.OutcomeIndex].ToString(
                            CultureInfo.InvariantCulture))
                throw new InvalidDataException(
                    "ReferenceNature law no longer matches the pending deck reveal.");
            var child = SemanticPilotCombatEngineV111.ResolveDeckReveal(
                combat, combat.Fingerprint, revealEvent.EventId,
                revealEvent.CandidateCardInstanceIds[
                    selection.OutcomeIndex]);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        throw new NotSupportedException(
            "UNSUPPORTED_REFERENCE_NATURE site=no_supported_pending_event");
    }

    public static ReferenceNatureSelectionV111 SampleValidated(
        ChanceLawV111 law, ReferenceNatureStateV111 state)
    {
        var selection = Sample(law, state);
        if (selection.LawId != law.LawId ||
            selection.EventId != law.EventId ||
            selection.OutcomeIndex < 0 ||
            selection.OutcomeIndex >= law.Outcomes.Length ||
            selection.OutcomeId !=
                law.Outcomes[selection.OutcomeIndex].OutcomeId)
            throw new InvalidDataException(
                "ReferenceNature sample does not match the queried Chance Law support.");
        return selection;
    }

    public static ReferenceNatureSelectionV111 Sample(
        ChanceLawV111 law, ReferenceNatureStateV111 state)
    {
        ArgumentNullException.ThrowIfNull(law);
        ArgumentNullException.ThrowIfNull(state);
        law.Validate();
        state.Validate();

        ulong randomState = state.SamplerState;
        ulong ticket = NextBounded(ref randomState, (ulong)law.TotalWeight);
        long cumulative = 0;
        for (var index = 0; index < law.Outcomes.Length; index++)
        {
            cumulative = checked(cumulative + law.Outcomes[index].Weight);
            if (ticket < (ulong)cumulative)
            {
                return new ReferenceNatureSelectionV111(
                    law.LawId, law.EventId, law.Outcomes[index].OutcomeId,
                    index, new ReferenceNatureStateV111(
                        ReferenceNatureStateV111.CurrentSchemaId,
                        randomState, checked(state.SampleCount + 1)));
            }
        }

        throw new InvalidDataException("ReferenceNature sampled outside law support.");
    }

    private static ulong NextBounded(ref ulong state, ulong bound)
    {
        if (bound == 0)
            throw new ArgumentOutOfRangeException(nameof(bound));
        var threshold = unchecked(0UL - bound) % bound;
        while (true)
        {
            var value = NextUInt64(ref state);
            if (value >= threshold)
                return value % bound;
        }
    }

    private static ulong NextUInt64(ref ulong state)
    {
        state = unchecked(state + GoldenGamma);
        ulong value = state;
        value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
        value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
        return value ^ (value >> 31);
    }
}
