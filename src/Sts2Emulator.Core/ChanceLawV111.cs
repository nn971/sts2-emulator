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
    string[] ReferenceNatureOperations);

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
        [
            "combat.committed_intent.index_draw",
            "combat.next_turn_intent.index_draw",
            "combat.enemy.discard.random_insert_index",
            "combat.enemy.draw.random_insert_index",
            "combat.enemy.theft_card.select",
            "combat.enemy.summon_pool.select",
            NativeCombatRngEntryV111.InitialUniqueHpSiteId,
            NativeCombatRngEntryV111.ToughEggHatchHpSiteId,
            "combat.draw_pile.draw_card",
            "combat.effect.random_enemy",
            "combat.sly_autoplay.random_enemy",
            "combat.autoplay.random_enemy",
            "combat.generated_card.select",
            "combat.random_draw_attack.card_model",
            "combat.random_hand_card.energy_cost",
            "combat.card_draw.relic_energy_cost",
            "combat.card_draw.slither_energy_cost",
            "combat.relic.jeweled_mask.random_power_draw_card",
            "combat.card_transform.replacement.index_draw",
            "combat.potion_generation.random_model",
            "combat.random_hand_card.select",
            "combat.generation.full_pool_shuffle.swap_index",
            "combat.draw_pile.random_rare_to_hand.shuffle_swap_index",
            "combat.draw_pile.random_insert_index",
            "combat.relic.after_shuffle.generated_card.random_insert_index",
            "combat.card_selection.random_shortlist",
            "combat.autoplay.random_card.select",
            "combat.draw_pile.random_replay_card.select"
        ],
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
            "semantic_reference_release_many"
        ]);
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

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || SampleCount < 0)
            throw new InvalidDataException("Invalid ReferenceNature sample count.");
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
        SemanticPilotCombatV111 combat, ulong seed)
    {
        var session = new ReferenceNatureSessionV111(
            CurrentSchemaId, combat, ReferenceNatureStateV111.Create(seed));
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
        var child = this with
        {
            Combat = resolution.Combat,
            NatureState = resolution.Selection.NextState
        };
        child.Validate();
        return child;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Combat is null || NatureState is null)
            throw new InvalidDataException("Invalid ReferenceNature session envelope.");
        SemanticPilotCombatEngineV111.Validate(Combat);
        NatureState.Validate();
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
        var restored = new ReferenceNatureSessionV111(
            CurrentSchemaId,
            SemanticPilotCombatEngineV111.Restore(snapshot.CombatSnapshotJson),
            snapshot.NatureState);
        restored.Validate();
        return restored;
    }
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
        SemanticPilotCombatEngineV111.Validate(combat);
        state.Validate();

        if (combat.IntentPending is not null)
        {
            var intentEvent = SemanticPilotCombatEngineV111
                .DescribePendingIntentEvent(combat);
            var law = ChanceLawEngineV111.QueryIntent(intentEvent);
            var selection = Sample(law, state);
            var child = SemanticPilotCombatEngineV111.ResolveIntent(
                combat, combat.Fingerprint, selection.OutcomeId);
            return new ReferenceNatureResolutionV111(child, law, selection);
        }

        if (combat.EnemyActionEventPending is { } enemyActionEvent)
        {
            var law = ChanceLawEngineV111.QueryEndTurnIndex(
                enemyActionEvent);
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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
            var selection = Sample(law, state);
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

        throw new NotSupportedException(
            "UNSUPPORTED_REFERENCE_NATURE site=no_supported_pending_event");
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
