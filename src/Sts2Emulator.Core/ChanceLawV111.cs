using System.Globalization;
using System.Numerics;
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
    private string[]? _natureChoiceSiteIds = Clone(NatureChoiceSiteIds);
    private string[]? _providerModes = Clone(ProviderModes);
    private string[]? _referenceNatureOperations =
        Clone(ReferenceNatureOperations);
    private string[]? _probabilityStatuses =
        [ChanceLawV111.UnverifiedStatus];
    private string[]? _scriptedRunActionOperations =
        Clone(ScriptedRunActionSessionV111.Operations);
    private string[]? _scriptedRunSessionOperations =
        Clone(ScriptedRunSessionV111.Operations);
    private string[]? _semanticNatureChoiceViewOperations =
        ["nature_choice_observe", "semantic_nature_observe"];
    private string[]? _semanticNatureChoiceViewSessionKinds =
        StochasticCapabilitiesV111.SemanticNatureChoiceViewSessionKinds;

    public string[] NatureChoiceSiteIds
    {
        get => Clone(_natureChoiceSiteIds)!;
        init => _natureChoiceSiteIds = Clone(value);
    }
    public string[] ProviderModes
    {
        get => Clone(_providerModes)!;
        init => _providerModes = Clone(value);
    }
    public string[] ReferenceNatureOperations
    {
        get => Clone(_referenceNatureOperations)!;
        init => _referenceNatureOperations = Clone(value);
    }

    /// <summary>Individual Chance Laws carry one of these statuses. The
    /// legacy ProbabilityStatus field remains the conservative default.</summary>
    public string[] ProbabilityStatuses
    {
        get => Clone(_probabilityStatuses)!;
        init => _probabilityStatuses = Clone(value);
    }
    public string ReferenceNatureRunActionSessionSchemaId { get; init; } =
        ReferenceNatureRunActionSessionV111.CurrentSchemaId;
    public string ReferenceNatureEventChoiceSessionSchemaId { get; init; } =
        ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId;
    public string ReferenceNatureAncientOfferSessionSchemaId { get; init; } =
        ReferenceNatureAncientOfferSessionV111.CurrentSchemaId;
    public string ReferenceNatureNeowOfferSessionSchemaId { get; init; } =
        ReferenceNatureNeowOfferSessionV111.CurrentSchemaId;
    public string ReferenceNatureTrashHeapSessionSchemaId { get; init; } =
        ReferenceNatureTrashHeapSessionV111.CurrentSchemaId;
    public string ReferenceNatureTabletOfTruthSessionSchemaId { get; init; } =
        ReferenceNatureTabletOfTruthSessionV111.CurrentSchemaId;
    public string ReferenceNatureTrialSessionSchemaId { get; init; } =
        ReferenceNatureTrialSessionV111.CurrentSchemaId;
    public string ReferenceNatureNativeEventShuffleSessionSchemaId { get; init; } =
        ReferenceNatureNativeEventShuffleSessionV111.CurrentSchemaId;
    public string ReferenceNatureNativeEventGoldSessionSchemaId { get; init; } =
        ReferenceNatureNativeEventGoldSessionV111.CurrentSchemaId;
    public string ReferenceNatureNativeEventRelicSessionSchemaId { get; init; } =
        ReferenceNatureNativeEventRelicSessionV111.CurrentSchemaId;
    public string ReferenceNatureNativeShovelSessionSchemaId { get; init; } =
        ReferenceNatureNativeShovelSessionV111.CurrentSchemaId;
    public string ReferenceNatureEndlessConveyorSessionSchemaId { get; init; } =
        ReferenceNatureEndlessConveyorSessionV111.CurrentSchemaId;
    public string ReferenceNatureGloryReflectionsSessionSchemaId { get; init; } =
        ReferenceNatureGloryReflectionsSessionV111.CurrentSchemaId;
    public string ReferenceNatureInfestedAutomatonSessionSchemaId { get; init; } =
        ReferenceNatureInfestedAutomatonSessionV111.CurrentSchemaId;
    public string ReferenceNaturePunchOffRewardSessionSchemaId { get; init; } =
        ReferenceNaturePunchOffRewardSessionV111.CurrentSchemaId;
    public string ReferenceNaturePunchOffFightSessionSchemaId { get; init; } =
        ReferenceNaturePunchOffFightSessionV111.CurrentSchemaId;
    public string ReferenceNatureMerchantSessionSchemaId { get; init; } =
        ReferenceNatureMerchantSessionV111.CurrentSchemaId;
    public string ReferenceNatureTreasureSessionSchemaId { get; init; } =
        ReferenceNatureTreasureSessionV111.CurrentSchemaId;
    public string ReferenceNatureCombatRewardSessionSchemaId { get; init; } =
        ReferenceNatureCombatRewardSessionV111.CurrentSchemaId;
    public string ReferenceNatureMapGenerationSessionSchemaId { get; init; } =
        ReferenceNatureMapGenerationSessionV111.CurrentSchemaId;
    public string ScriptedRunActionSessionSchemaId { get; init; } =
        ScriptedRunActionSessionV111.CurrentSchemaId;
    public string[] ScriptedRunActionOperations
    {
        get => Clone(_scriptedRunActionOperations)!;
        init => _scriptedRunActionOperations = Clone(value);
    }
    public string ScriptedRunSessionSchemaId { get; init; } =
        ScriptedRunSessionV111.CurrentSchemaId;
    public string[] ScriptedRunSessionOperations
    {
        get => Clone(_scriptedRunSessionOperations)!;
        init => _scriptedRunSessionOperations = Clone(value);
    }
    public string SemanticNatureChoiceViewSchemaId { get; init; } =
        SemanticNatureChoiceViewV111.CurrentSchemaId;
    public string[] SemanticNatureChoiceViewOperations
    {
        get => Clone(_semanticNatureChoiceViewOperations)!;
        init => _semanticNatureChoiceViewOperations = Clone(value);
    }
    public string[] SemanticNatureChoiceViewSessionKinds
    {
        get => Clone(_semanticNatureChoiceViewSessionKinds)!;
        init => _semanticNatureChoiceViewSessionKinds = Clone(value);
    }

    private static string[]? Clone(string[]? values) =>
        values is null ? null : (string[])values.Clone();
}

public static class StochasticCapabilitiesV111
{
    public const string SchemaId = "sts2-v111-stochastic-capabilities-v3";
    public const string ExternalSelectionProviderId =
        "external-semantic-selection-v1";

    private static readonly string[] NatureChoiceViewSessionKinds =
    [
        "semantic_combat", "reference_combat", "run_action",
        "event_choice", "ancient_offer", "neow_offer", "trash_heap",
        "tablet_of_truth", "trial", "event_shuffle", "native_event_gold",
        "native_event_relic", "native_shovel", "endless_conveyor",
        "merchant", "treasure", "combat_reward", "glory_reflections",
        "infested_automaton", "punch_off_reward", "punch_off_fight",
        "map_generation", "controlled_event_choice", "controlled_run_action"
    ];

    public static string[] SemanticNatureChoiceViewSessionKinds =>
        (string[])NatureChoiceViewSessionKinds.Clone();

    public static bool IsSemanticNatureChoiceViewSessionKind(string sessionKind) =>
        NatureChoiceViewSessionKinds.Contains(sessionKind,
            StringComparer.Ordinal);

    public static StochasticCapabilityManifestV111 Create() =>
        Create(StochasticSiteRegistryV111.CreateSessionCoverage());

    public static StochasticCapabilityManifestV111 Create(
        StochasticSessionCoverageV111 sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        sessions.Validate();
        return CreateValidated(sessions);
    }

    private static StochasticCapabilityManifestV111 CreateValidated(
        StochasticSessionCoverageV111 sessions) => new(
        SchemaId,
        ChanceLawV111.CurrentSchemaId,
        ReferenceNatureV111.ProviderId,
        sessions.SemanticReferenceNatureSessionSchemaId,
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
            "reference_nature_event_choice_release_many",
            "reference_nature_ancient_offer_begin",
            "reference_nature_ancient_offer_status",
            "reference_nature_ancient_offer_resolve_next",
            "reference_nature_ancient_offer_fork",
            "reference_nature_ancient_offer_save",
            "reference_nature_ancient_offer_load",
            "reference_nature_ancient_offer_release_many",
            "reference_nature_neow_offer_begin",
            "reference_nature_neow_offer_status",
            "reference_nature_neow_offer_resolve_next",
            "reference_nature_neow_offer_fork",
            "reference_nature_neow_offer_save",
            "reference_nature_neow_offer_load",
            "reference_nature_neow_offer_release_many",
            "reference_nature_trash_heap_begin",
            "reference_nature_trash_heap_status",
            "reference_nature_trash_heap_resolve_next",
            "reference_nature_trash_heap_fork",
            "reference_nature_trash_heap_save",
            "reference_nature_trash_heap_load",
            "reference_nature_trash_heap_release_many",
            "reference_nature_tablet_of_truth_begin",
            "reference_nature_tablet_of_truth_status",
            "reference_nature_tablet_of_truth_resolve_next",
            "reference_nature_tablet_of_truth_fork",
            "reference_nature_tablet_of_truth_save",
            "reference_nature_tablet_of_truth_load",
            "reference_nature_tablet_of_truth_release_many",
            "reference_nature_trial_begin",
            "reference_nature_trial_status",
            "reference_nature_trial_resolve_next",
            "reference_nature_trial_fork",
            "reference_nature_trial_save",
            "reference_nature_trial_load",
            "reference_nature_trial_release_many",
            "reference_nature_native_event_shuffle_begin",
            "reference_nature_native_event_shuffle_status",
            "reference_nature_native_event_shuffle_resolve_next",
            "reference_nature_native_event_shuffle_fork",
            "reference_nature_native_event_shuffle_save",
            "reference_nature_native_event_shuffle_load",
            "reference_nature_native_event_shuffle_release_many",
            "reference_nature_native_event_gold_begin",
            "reference_nature_native_event_gold_status",
            "reference_nature_native_event_gold_resolve_next",
            "reference_nature_native_event_gold_fork",
            "reference_nature_native_event_gold_save",
            "reference_nature_native_event_gold_load",
            "reference_nature_native_event_gold_release_many",
            "reference_nature_native_event_relic_begin",
            "reference_nature_native_event_relic_status",
            "reference_nature_native_event_relic_resolve_next",
            "reference_nature_native_event_relic_fork",
            "reference_nature_native_event_relic_save",
            "reference_nature_native_event_relic_load",
            "reference_nature_native_event_relic_release_many",
            "reference_nature_native_shovel_begin",
            "reference_nature_native_shovel_status",
            "reference_nature_native_shovel_resolve_next",
            "reference_nature_native_shovel_fork",
            "reference_nature_native_shovel_save",
            "reference_nature_native_shovel_load",
            "reference_nature_native_shovel_release_many",
            "reference_nature_endless_conveyor_begin",
            "reference_nature_endless_conveyor_status",
            "reference_nature_endless_conveyor_resolve_next",
            "reference_nature_endless_conveyor_fork",
            "reference_nature_endless_conveyor_save",
            "reference_nature_endless_conveyor_load",
            "reference_nature_endless_conveyor_release_many",
            "reference_nature_glory_reflections_begin",
            "reference_nature_glory_reflections_status",
            "reference_nature_glory_reflections_resolve_next",
            "reference_nature_glory_reflections_fork",
            "reference_nature_glory_reflections_save",
            "reference_nature_glory_reflections_load",
            "reference_nature_glory_reflections_release_many",
            "reference_nature_infested_automaton_begin",
            "reference_nature_infested_automaton_status",
            "reference_nature_infested_automaton_resolve_next",
            "reference_nature_infested_automaton_fork",
            "reference_nature_infested_automaton_save",
            "reference_nature_infested_automaton_load",
            "reference_nature_infested_automaton_release_many",
            "reference_nature_punch_off_reward_begin",
            "reference_nature_punch_off_reward_status",
            "reference_nature_punch_off_reward_resolve_next",
            "reference_nature_punch_off_reward_fork",
            "reference_nature_punch_off_reward_save",
            "reference_nature_punch_off_reward_load",
            "reference_nature_punch_off_reward_release_many",
            "reference_nature_punch_off_fight_begin",
            "reference_nature_punch_off_fight_status",
            "reference_nature_punch_off_fight_resolve_next",
            "reference_nature_punch_off_fight_fork",
            "reference_nature_punch_off_fight_save",
            "reference_nature_punch_off_fight_load",
            "reference_nature_punch_off_fight_release_many",
            "reference_nature_merchant_begin",
            "reference_nature_merchant_status",
            "reference_nature_merchant_resolve_next",
            "reference_nature_merchant_fork",
            "reference_nature_merchant_save",
            "reference_nature_merchant_load",
            "reference_nature_merchant_release_many",
            "reference_nature_treasure_begin",
            "reference_nature_treasure_status",
            "reference_nature_treasure_resolve_next",
            "reference_nature_treasure_fork",
            "reference_nature_treasure_save",
            "reference_nature_treasure_load",
            "reference_nature_treasure_release_many",
            "reference_nature_combat_reward_begin",
            "reference_nature_combat_reward_status",
            "reference_nature_combat_reward_resolve_next",
            "reference_nature_combat_reward_fork",
            "reference_nature_combat_reward_save",
            "reference_nature_combat_reward_load",
            "reference_nature_combat_reward_release_many",
            "reference_nature_map_generation_begin",
            "reference_nature_map_generation_status",
            "reference_nature_map_generation_resolve_next",
            "reference_nature_map_generation_fork",
            "reference_nature_map_generation_save",
            "reference_nature_map_generation_load",
            "reference_nature_map_generation_release_many"
        ])
    {
        ProbabilityStatuses =
        [
            ChanceLawV111.UnverifiedStatus,
            ChanceLawV111.SourceVerifiedStatus
        ],
        ReferenceNatureRunActionSessionSchemaId =
            sessions.ReferenceNatureRunActionSessionSchemaId,
        ReferenceNatureEventChoiceSessionSchemaId =
            sessions.ReferenceNatureEventChoiceSessionSchemaId,
        ReferenceNatureAncientOfferSessionSchemaId =
            sessions.ReferenceNatureAncientOfferSessionSchemaId,
        ReferenceNatureNeowOfferSessionSchemaId =
            sessions.ReferenceNatureNeowOfferSessionSchemaId,
        ReferenceNatureTrashHeapSessionSchemaId =
            sessions.ReferenceNatureTrashHeapSessionSchemaId,
        ReferenceNatureTabletOfTruthSessionSchemaId =
            sessions.ReferenceNatureTabletOfTruthSessionSchemaId,
        ReferenceNatureTrialSessionSchemaId =
            sessions.ReferenceNatureTrialSessionSchemaId,
        ReferenceNatureNativeEventShuffleSessionSchemaId =
            sessions.ReferenceNatureNativeEventShuffleSessionSchemaId,
        ReferenceNatureNativeEventGoldSessionSchemaId =
            sessions.ReferenceNatureNativeEventGoldSessionSchemaId,
        ReferenceNatureNativeEventRelicSessionSchemaId =
            sessions.ReferenceNatureNativeEventRelicSessionSchemaId,
        ReferenceNatureNativeShovelSessionSchemaId =
            sessions.ReferenceNatureNativeShovelSessionSchemaId,
        ReferenceNatureEndlessConveyorSessionSchemaId =
            sessions.ReferenceNatureEndlessConveyorSessionSchemaId,
        ReferenceNatureGloryReflectionsSessionSchemaId =
            sessions.ReferenceNatureGloryReflectionsSessionSchemaId,
        ReferenceNatureInfestedAutomatonSessionSchemaId =
            sessions.ReferenceNatureInfestedAutomatonSessionSchemaId,
        ReferenceNaturePunchOffRewardSessionSchemaId =
            sessions.ReferenceNaturePunchOffRewardSessionSchemaId,
        ReferenceNaturePunchOffFightSessionSchemaId =
            sessions.ReferenceNaturePunchOffFightSessionSchemaId,
        ReferenceNatureMerchantSessionSchemaId =
            sessions.ReferenceNatureMerchantSessionSchemaId,
        ReferenceNatureTreasureSessionSchemaId =
            sessions.ReferenceNatureTreasureSessionSchemaId,
        ReferenceNatureCombatRewardSessionSchemaId =
            sessions.ReferenceNatureCombatRewardSessionSchemaId,
        ReferenceNatureMapGenerationSessionSchemaId =
            sessions.ReferenceNatureMapGenerationSessionSchemaId,
        ScriptedRunActionSessionSchemaId =
            ScriptedRunActionSessionV111.CurrentSchemaId,
        ScriptedRunActionOperations =
            ScriptedRunActionSessionV111.Operations,
        ScriptedRunSessionSchemaId =
            ScriptedRunSessionV111.CurrentSchemaId,
        ScriptedRunSessionOperations =
            ScriptedRunSessionV111.Operations
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
    private ChanceLawOutcomeV111[]? _outcomes = Clone(Outcomes);

    public ChanceLawOutcomeV111[] Outcomes
    {
        get => Clone(_outcomes)!;
        init => _outcomes = Clone(value);
    }

    public const string CurrentSchemaId = "sts2-v111-chance-law-v1";
    public const string UnverifiedStatus = "UNVERIFIED";
    public const string SourceVerifiedStatus = "SOURCE_VERIFIED";
    public const long NativeSourceGridSize = 1L << 53;

    private static ChanceLawOutcomeV111[]? Clone(
        ChanceLawOutcomeV111[]? outcomes) => outcomes is null
        ? null
        : (ChanceLawOutcomeV111[])outcomes.Clone();

    public ChanceLawV111 Fork() => this with
    {
        Outcomes = (ChanceLawOutcomeV111[])Outcomes.Clone()
    };

    public void Validate()
    {
        var outcomes = Outcomes;
        if (SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(SiteId) ||
            string.IsNullOrWhiteSpace(OperationKind) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            string.IsNullOrWhiteSpace(ConditioningPolicyId) ||
            string.IsNullOrWhiteSpace(ConditioningFingerprint) ||
            ProbabilityStatus is not (UnverifiedStatus or SourceVerifiedStatus) ||
            string.IsNullOrWhiteSpace(ProbabilityProvenance) ||
            outcomes is not { Length: > 0 } || TotalWeight <= 0)
            throw new InvalidDataException("Invalid Chance Law envelope.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var outcome in outcomes)
        {
            if (outcome is null || string.IsNullOrWhiteSpace(outcome.OutcomeId) ||
                outcome.Weight <= 0 || !ids.Add(outcome.OutcomeId))
                throw new InvalidDataException("Invalid Chance Law support.");
            total = checked(total + outcome.Weight);
        }

        if (total != TotalWeight ||
            SupportFingerprint != CanonicalJson.Sha256(outcomes))
            throw new InvalidDataException(
                "Chance Law support fingerprint or total weight is inconsistent.");
        var canonicalDeterministicOutcome = outcomes.Length == 1 &&
            outcomes[0].Weight == 1L;
        if (ProbabilityStatus == SourceVerifiedStatus &&
            TotalWeight != NativeSourceGridSize &&
            !canonicalDeterministicOutcome)
            throw new InvalidDataException(
                "A source-verified Chance Law must cover the complete Native 53-bit input grid or encode one deterministic outcome.");

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
public static partial class ChanceLawEngineV111
{
    private static readonly string[] LegacyMerchantIndexQuerySites =
    [
        "shop.merchant.card_price_modifier.index_draw",
        "shop.merchant.card_upgrade_roll.ticket",
        "shop.merchant.potion_price_modifier.index_draw",
        "shop.merchant.relic_price_modifier.index_draw"
    ];

    private static readonly string[] LegacyRarityTicketQuerySites =
    [
        "card_rarity.roll.ticket",
        PrototypeNativePotionShop.RarityRollSiteId,
        "relic_grab_bag.rarity.ticket"
    ];

    private static readonly string[] LegacyCardUpgradeTicketQuerySites =
    [
        "card_rarity.upgrade_roll.ticket"
    ];

    private static readonly string[] LegacyRewardCardRarityTicketQuerySites =
    [
        "reward.card_rarity.ticket"
    ];

    private static readonly string[] LegacyRewardCardModelQuerySites =
    [
        "reward.card_model.index_draw"
    ];

    private static readonly string[] LegacyRewardCardTransformQuerySites =
    [
        "reward.card_transform.replacement.index_draw"
    ];

    private static readonly string[] LegacyShopCardRarityQuerySites =
    [
        "shop.card_rarity.ticket"
    ];

    private static readonly string[] LegacyShopCardModelQuerySites =
    [
        "shop.card_model.index_draw"
    ];

    private static readonly string[] LegacyShopCardPriceQuerySites =
    [
        "shop.card.price.index_draw"
    ];

    private static readonly string[] LegacyShopPotionPriceQuerySites =
    [
        "shop.potion_offer.price.index_draw"
    ];

    private static readonly string[] LegacyShopRelicPriceQuerySites =
    [
        "shop.relic_offer.price.index_draw"
    ];

    private static readonly string[] LegacyPotionDropTicketQuerySites =
    [
        "reward.random_potion.drop.ticket"
    ];

    private static readonly string[] LegacyRewardPotionModelQuerySites =
    [
        "reward.random_potion.model"
    ];

    private static readonly string[] LegacyPotionRewardOddsTicketQuerySites =
    [
        "reward.potion_odds.ticket"
    ];

    private static readonly string[] LegacyUncommonPotionModelQuerySites =
    [
        "potion.uncommon_model.index_draw"
    ];

    private static readonly string[] LegacyPotionModelWithinRarityQuerySites =
    [
        PrototypeNativePotionShop.ModelWithinRaritySiteId
    ];

    private static readonly string[] LegacyUniformPotionModelQuerySites =
    [
        PrototypeNativePotionShop.UniformModelSiteId
    ];

    private static readonly string[] LegacyEventRewardPotionModelQuerySites =
    [
        "event.reward.random_potion.model"
    ];

    private static readonly string[] LegacyEventRewardRareCardModelQuerySites =
    [
        "event.reward.random_rare_card.model"
    ];

    private static readonly string[] LegacyEventOfferPotionModelQuerySites =
    [
        "event.reward.offer_potion.model.index_draw"
    ];

    private static readonly string[] LegacyEventModifierCurseModelQuerySites =
    [
        "event.reward.random_modifier_curse.index_draw"
    ];

    private static readonly string[] LegacyEventFillPotionSlotQuerySites =
    [
        "event.potion.fill_empty_slots.model"
    ];

    private static readonly string[] LegacyEliteRelicModelQuerySites =
    [
        "reward.elite_relic.model"
    ];

    private static readonly string[] LegacyTreasureRelicModelQuerySites =
    [
        "treasure.relic_model.index_draw"
    ];

    private static readonly string[] LegacyRelicAcquisitionRareCardQuerySites =
    [
        "relic.acquisition.random_rare_card.model"
    ];

    private static readonly string[] LegacyRelicAcquisitionPotionQuerySites =
    [
        "relic.acquisition.random_potion.model"
    ];

    private static readonly string[] LegacyRunPotionFillSlotQuerySites =
    [
        "run.potion.fill_empty_slots.model"
    ];

    private static readonly string[] UnownedRelicSelectionQuerySites =
    [
        StochasticSiteRegistryV111.UnownedRandomRelicSelectionSiteId
    ];

    private sealed record NativeBoundedIntegerLawSiteV111(
        string StreamId, string Boundary);

    private static readonly Dictionary<string,
        NativeBoundedIntegerLawSiteV111> NativeBoundedIntegerLawSites =
        new(StringComparer.Ordinal)
        {
            [PrototypeNativeRelicGrabBag.QueueShuffleSiteId] = new(
                "run.treasure_room_relics",
                "native-relic-bag-queue-shuffle-index"),
            [StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId] = new(
                NativePlayerRngV111.RewardsStreamId,
                "native-boss-relic-offer-shuffle-index"),
            [StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId] = new(
                NativePlayerRngV111.RewardsStreamId,
                "native-lava-rock-relic-offer-shuffle-index"),
            [NativeFisherYatesV111.SiteId] = new(
                NativeFisherYatesV111.StreamId,
                "native-draw-pile-shuffle-index"),
            [ControlledRecycleShuffleEngineV111.SiteId] = new(
                NativeFisherYatesV111.StreamId,
                "native-recycle-shuffle-index"),
            [PrototypeControlledDrawInsertionV111.PersonalHiveDazedSiteId] = new(
                NativeFisherYatesV111.StreamId,
                "native-personal-hive-dazed-insertion-index"),
            [StochasticSiteRegistryV111.DrawPileRandomInsertSiteId] = new(
                NativeFisherYatesV111.StreamId,
                "native-draw-pile-random-insertion-index"),
            [StochasticSiteRegistryV111.AfterShuffleRelicDrawPileInsertSiteId] = new(
                NativeFisherYatesV111.StreamId,
                "native-after-shuffle-relic-insertion-index"),
            ["combat.enemy.draw.random_insert_index"] = new(
                NativeFisherYatesV111.StreamId,
                "native-enemy-draw-insertion-index"),
            ["combat.enemy.discard.random_insert_index"] = new(
                NativeFisherYatesV111.StreamId,
                "native-enemy-discard-insertion-index")
        };

    private const long NativeNextDoubleGridSize =
        ChanceLawV111.NativeSourceGridSize;

    public static bool IsNativeBoundedIntegerLawSite(string siteId) =>
        NativeBoundedIntegerLawSites.ContainsKey(siteId);

    public static string[] LegacyMerchantIndexQuerySiteIds =>
        (string[])LegacyMerchantIndexQuerySites.Clone();

    public static string[] LegacyRarityTicketQuerySiteIds =>
        (string[])LegacyRarityTicketQuerySites.Clone();

    public static string[] LegacyCardUpgradeTicketQuerySiteIds =>
        (string[])LegacyCardUpgradeTicketQuerySites.Clone();

    public static string[] LegacyRewardCardRarityTicketQuerySiteIds =>
        (string[])LegacyRewardCardRarityTicketQuerySites.Clone();

    public static string[] LegacyRewardCardModelQuerySiteIds =>
        (string[])LegacyRewardCardModelQuerySites.Clone();

    public static string[] LegacyRewardCardTransformQuerySiteIds =>
        (string[])LegacyRewardCardTransformQuerySites.Clone();

    public static string[] LegacyShopCardRarityQuerySiteIds =>
        (string[])LegacyShopCardRarityQuerySites.Clone();

    public static string[] LegacyShopCardModelQuerySiteIds =>
        (string[])LegacyShopCardModelQuerySites.Clone();

    public static string[] LegacyShopCardPriceQuerySiteIds =>
        (string[])LegacyShopCardPriceQuerySites.Clone();

    public static string[] LegacyShopPotionPriceQuerySiteIds =>
        (string[])LegacyShopPotionPriceQuerySites.Clone();

    public static string[] LegacyShopRelicPriceQuerySiteIds =>
        (string[])LegacyShopRelicPriceQuerySites.Clone();

    public static string[] LegacyCallerBoundIndexQuerySiteIds =>
        StochasticSiteRegistryV111.LegacyCallerBoundIndexQuerySiteIds;

    public static bool IsLegacyCallerBoundIndexQuerySite(string siteId) =>
        StochasticSiteRegistryV111.IsLegacyCallerBoundIndexQuerySite(siteId);

    public static string[] NativeMapGaussianIntQuerySiteIds =>
        StochasticSiteRegistryV111.NativeGaussianIntSiteIds;

    public static bool IsNativeMapGaussianIntQuerySite(string siteId) =>
        StochasticSiteRegistryV111.IsNativeGaussianIntSite(siteId);

    public static bool MatchesNativeMapGaussianIntRequest(
        RandomGaussianIntRequestV111 request)
        => StochasticSiteRegistryV111
            .MatchesNativeGaussianIntRequest(request);

    /// <summary>
    /// Query the emulator's continuous truncated-normal model for one of the
    /// source-shaped Native map quota Gaussian requests. The model does not
    /// count the finite Native RNG grid and is intentionally unverified.
    /// </summary>
    public static ChanceLawV111 QueryNativeMapGaussianInt(
        string siteId, string streamId, string ownerId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal != 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal),
                "Each Native map quota Gaussian site is queried at ordinal zero.");

        int mean;
        int standardDeviation = 1;
        int minInclusive;
        int maxInclusive;
        string expectedStream;
        string expectedOwner;
        switch (siteId)
        {
            case PrototypeNativeOvergrowthMap.RestCountGaussianSiteId:
                mean = 7;
                minInclusive = 6;
                maxInclusive = 7;
                expectedStream = "context.act_1_map";
                expectedOwner = "act:1:rest_count";
                break;
            case PrototypeNativeOvergrowthMap.UnknownCountGaussianSiteId:
                mean = 12;
                minInclusive = 10;
                maxInclusive = 14;
                expectedStream = "context.act_1_map";
                expectedOwner = "act:1:unknown_count";
                break;
            case PrototypeNativeLaterActMap.HiveRestCountGaussianSiteId:
                mean = 6;
                minInclusive = 6;
                maxInclusive = 7;
                expectedStream = "context.act_2_map";
                expectedOwner = "map:act:2";
                break;
            case PrototypeNativeLaterActMap.UnknownCountGaussianSiteId:
                mean = 12;
                minInclusive = 10;
                maxInclusive = 14;
                if (ownerId is not ("map:act:2" or "map:act:3"))
                    throw new ArgumentException(
                        "Later-act Unknown quota law requires owner map:act:2 or map:act:3.",
                        nameof(ownerId));
                expectedStream = ownerId == "map:act:2"
                    ? "context.act_2_map" : "context.act_3_map";
                expectedOwner = ownerId;
                break;
            default:
                throw new NotSupportedException(
                    $"Native map Gaussian Chance Law does not support site '{siteId}'.");
        }

        if (streamId != expectedStream || ownerId != expectedOwner)
            throw new ArgumentException(
                $"Native map Gaussian site '{siteId}' requires stream '{expectedStream}' and owner '{expectedOwner}'.");

        const double weightScale = 1_000_000_000_000d;
        var outcomes = Enumerable.Range(minInclusive,
                maxInclusive - minInclusive + 1)
            .Select(value => new ChanceLawOutcomeV111(
                "integer:" + value.ToString(CultureInfo.InvariantCulture),
                Math.Max(1L, checked((long)Math.Round(
                    (NormalCdf((value + 0.5 - mean) /
                        (double)standardDeviation) -
                     NormalCdf((value - 0.5 - mean) /
                        (double)standardDeviation)) * weightScale,
                    MidpointRounding.AwayFromZero)))))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-map-truncated-gaussian-request",
            siteId,
            streamId,
            ownerId,
            ordinal
        });
        var conditioning = new
        {
            model = "continuous-normal-rounded-and-truncated-v1",
            mean,
            standardDeviation,
            minInclusive,
            maxInclusive
        };
        return Create(eventId, siteId, "gaussian_int", ownerId, ordinal,
            "native-map-truncated-rounded-normal-model-v1",
            CanonicalJson.Sha256(new { streamId, ownerId, ordinal, conditioning }),
            "continuous truncated-rounded-normal approximation; Native finite-grid and source parity UNVERIFIED",
            outcomes);
    }

    /// <summary>
    /// Query one request emitted by the source-shaped Native Overgrowth,
    /// Hive, or Glory map generator. This dispatcher is the fail-closed law
    /// boundary used by the atomic ReferenceNature map continuation.
    /// </summary>
    public static ChanceLawV111 QueryNativeMapRequest(
        ControlledStochasticRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.GaussianIntRequest is { } gaussian)
        {
            if (!MatchesNativeMapGaussianIntRequest(gaussian))
                throw new NotSupportedException(
                    $"Native map Chance Law does not support request '{gaussian.SiteId}'.");
            return QueryNativeMapGaussianInt(
                gaussian.SiteId, gaussian.StreamId, gaussian.OwnerId,
                gaussian.Ordinal);
        }

        if (request.IndexRequest is not { } index ||
            index.OperationKind != "bounded_integer" ||
            index.OrderedCandidates.Length != 0 ||
            index.BoundedCount is not { } supportCount)
            throw new NotSupportedException(
                "Native map Chance Law supports only bounded-index and Gaussian-integer requests.");

        if (index.SiteId == PrototypeNativeOvergrowthMap.PathStartColumnSiteId)
        {
            RequireMapRequest(index, "context.act_1_map", supportCount: 7);
            return QueryOvergrowthPathStart(index.OwnerId, index.Ordinal);
        }
        if (index.SiteId == PrototypeNativeLaterActMap.PathStartSiteId)
        {
            var act = LaterActFromOwner(index.OwnerId);
            RequireMapRequest(index, $"context.act_{act}_map", supportCount: 7);
            return QueryLaterActPathStart(act, index.Ordinal);
        }
        if (index.SiteId == PrototypeNativeOvergrowthMap.PathDeltaShuffleSiteId)
        {
            var expectedSupport = index.Ordinal % 2 == 0 ? 3 : 2;
            RequireMapRequest(index, "context.act_1_map", expectedSupport);
            return QueryOvergrowthPathDeltaSwap(index.OwnerId, index.Ordinal);
        }
        if (index.SiteId == PrototypeNativeLaterActMap.PathDeltaShuffleSiteId)
        {
            var act = LaterActFromOwner(index.OwnerId);
            var expectedSupport = index.Ordinal % 2 == 0 ? 3 : 2;
            RequireMapRequest(index, $"context.act_{act}_map", expectedSupport);
            return QueryLaterActPathDeltaSwap(act, index.Ordinal);
        }
        if (index.SiteId == PrototypeNativeOvergrowthMap.RoomAssignmentShuffleSiteId)
        {
            RequireMapRequest(index, "context.act_1_map", supportCount);
            return QueryOvergrowthRoomAssignmentSwap(
                index.OwnerId, index.Ordinal, supportCount);
        }
        if (index.SiteId == PrototypeNativeLaterActMap.RoomAssignmentShuffleSiteId)
        {
            var act = LaterActFromOwner(index.OwnerId);
            RequireMapRequest(index, $"context.act_{act}_map", supportCount);
            return QueryLaterActRoomAssignmentSwap(
                act, index.Ordinal, supportCount);
        }
        if (index.SiteId == PrototypeNativeLaterActMap.GloryRestCountSiteId)
        {
            RequireMapRequest(index, "context.act_3_map", supportCount: 2);
            if (index.OwnerId != "map:act:3" || index.Ordinal != 0)
                throw new InvalidDataException(
                    "Glory rest-quota request differs from its source-shaped owner or ordinal.");
            return QueryLaterActGloryRestCount(index.Ordinal);
        }

        throw new NotSupportedException(
            $"Native map Chance Law does not support request '{index.SiteId}'.");
    }

    private static int LaterActFromOwner(string ownerId) => ownerId switch
    {
        "map:act:2" => 2,
        "map:act:3" => 3,
        _ => throw new InvalidDataException(
            "Later-act map request requires owner map:act:2 or map:act:3.")
    };

    private static void RequireMapRequest(
        RandomIndexRequestV111 request, string expectedStream,
        int supportCount)
    {
        if (request.StreamId != expectedStream ||
            request.BoundedCount != supportCount)
            throw new InvalidDataException(
                "Native map request differs from its declared stream or bounded support.");
    }

    private static double NormalCdf(double value)
    {
        var absolute = Math.Abs(value);
        var t = 1.0 / (1.0 + 0.2316419 * absolute);
        var polynomial = t * (0.319381530 + t * (-0.356563782 +
            t * (1.781477937 + t * (-1.821255978 +
            t * 1.330274429))));
        var tail = 0.3989422804014327 *
            Math.Exp(-0.5 * absolute * absolute) * polynomial;
        return value >= 0 ? 1.0 - tail : tail;
    }

    public static string[] LegacyPotionDropTicketQuerySiteIds =>
        (string[])LegacyPotionDropTicketQuerySites.Clone();

    public static string[] LegacyRewardPotionModelQuerySiteIds =>
        (string[])LegacyRewardPotionModelQuerySites.Clone();

    public static string[] LegacyPotionRewardOddsTicketQuerySiteIds =>
        (string[])LegacyPotionRewardOddsTicketQuerySites.Clone();

    public static string[] LegacyUncommonPotionModelQuerySiteIds =>
        (string[])LegacyUncommonPotionModelQuerySites.Clone();

    public static string[] LegacyPotionModelWithinRarityQuerySiteIds =>
        (string[])LegacyPotionModelWithinRarityQuerySites.Clone();

    public static string[] LegacyUniformPotionModelQuerySiteIds =>
        (string[])LegacyUniformPotionModelQuerySites.Clone();

    public static string[] LegacyEventRewardPotionModelQuerySiteIds =>
        (string[])LegacyEventRewardPotionModelQuerySites.Clone();

    public static string[] LegacyEventRewardRareCardModelQuerySiteIds =>
        (string[])LegacyEventRewardRareCardModelQuerySites.Clone();

    public static string[] LegacyEventOfferPotionModelQuerySiteIds =>
        (string[])LegacyEventOfferPotionModelQuerySites.Clone();

    public static string[] LegacyEventModifierCurseModelQuerySiteIds =>
        (string[])LegacyEventModifierCurseModelQuerySites.Clone();

    public static string[] LegacyEventFillPotionSlotQuerySiteIds =>
        (string[])LegacyEventFillPotionSlotQuerySites.Clone();

    public static string[] LegacyEliteRelicModelQuerySiteIds =>
        (string[])LegacyEliteRelicModelQuerySites.Clone();

    public static string[] LegacyTreasureRelicModelQuerySiteIds =>
        (string[])LegacyTreasureRelicModelQuerySites.Clone();

    public static string[] LegacyRelicAcquisitionRareCardQuerySiteIds =>
        (string[])LegacyRelicAcquisitionRareCardQuerySites.Clone();

    public static string[] LegacyRelicAcquisitionPotionQuerySiteIds =>
        (string[])LegacyRelicAcquisitionPotionQuerySites.Clone();

    public static string[] LegacyRunPotionFillSlotQuerySiteIds =>
        (string[])LegacyRunPotionFillSlotQuerySites.Clone();

    public static string[] UnownedRelicSelectionQuerySiteIds =>
        (string[])UnownedRelicSelectionQuerySites.Clone();

    /// <summary>
    /// Query the retained legacy card-rarity integer ticket after the source
    /// thresholds have been converted to exact ticket counts. The outcome
    /// law remains unverified against the native game RNG.
    /// </summary>
    public static ChanceLawV111 QueryLegacyCardRarityTicket(
        PrototypeNativeCardOddsSource source, int currentOffsetBasisPoints,
        int ascension, int ordinal)
    {
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var (rareThreshold, uncommonEnd) =
            PrototypeNativeCardRarityOdds.NativeFloatThresholds(
                currentOffsetBasisPoints, ascension, source);
        const string siteId = "card_rarity.roll.ticket";
        const string streamId = "reward";
        var ownerId = $"source:{source}:offset:{currentOffsetBasisPoints}";
        var weights = LegacyRarityTicketWeights(rareThreshold, uncommonEnd);
        var outcomes = LegacyRarityOutcomes(weights.Common,
            weights.Uncommon, weights.Rare);
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            source,
            currentOffsetBasisPoints,
            ascension,
            rareThreshold,
            uncommonEnd,
            ticketCount = 10000
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-card-rarity-ticket-threshold-v1", conditioning,
            "uniform 10,000-way legacy bounded-ticket model; threshold " +
            "mapping is source-shaped and remains unverified against the " +
            "native game RNG.", outcomes);
    }

    /// <summary>Query the retained legacy 10/25/65 potion rarity ticket.</summary>
    public static ChanceLawV111 QueryLegacyPotionRarityTicket(
        string streamId, string ownerId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (streamId is not ("reward" or "event" or "shop"))
            throw new ArgumentException(
                "Legacy potion rarity tickets use reward, event, or shop streams.",
                nameof(streamId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = PrototypeNativePotionShop.RarityRollSiteId;
        var outcomes = LegacyRarityOutcomes(65, 25, 10);
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            ticketCount = 100,
            rareTicketEnd = 10,
            uncommonTicketEnd = 35
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-potion-rarity-ticket-v1", conditioning,
            "uniform 100-way legacy bounded-ticket model with source-shaped " +
            "10/25/65 rarity bins; probabilities remain unverified against " +
            "the native game RNG.", outcomes);
    }

    /// <summary>
    /// Query the retained legacy 50/33/17 relic rarity ticket. The merchant
    /// flag selects the exact owner suffix used by the legacy call.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRelicRarityTicket(
        string ownerId, bool merchant, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "relic_grab_bag.rarity.ticket";
        const string streamId = "reward";
        var requestOwnerId = ownerId + (merchant ? ":merchant" : ":reward");
        var outcomes = LegacyRarityOutcomes(50, 33, 17);
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId = requestOwnerId,
            ownerPrefix = ownerId,
            ordinal,
            merchant,
            ticketCount = 100,
            commonTicketEnd = 50,
            uncommonTicketEnd = 83
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, requestOwnerId, ordinal,
            "legacy-relic-rarity-ticket-v1", conditioning,
            "uniform 100-way legacy bounded-ticket model with source-shaped " +
            "50/33/17 rarity bins; probabilities remain unverified against " +
            "the native game RNG.", outcomes);
    }

    /// <summary>
    /// Query the legacy card-upgrade threshold ticket. Its bounded integer
    /// mapping is source-shaped but remains unverified against Native RNG.
    /// </summary>
    public static ChanceLawV111 QueryLegacyCardUpgradeTicket(
        string cardId, int act, int ascension, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var thresholdBasisPoints =
            PrototypeNativeCardRarityOdds
                .EncounterCardUpgradeThresholdBasisPoints(
                    cardId, act, ascension);
        const string siteId = "card_rarity.upgrade_roll.ticket";
        const string streamId = "reward";
        var ownerId = $"card:{cardId}:act:{act}:ascension:{ascension}";
        const int ticketCount = 10000;
        var outcomes = new List<ChanceLawOutcomeV111>(2);
        if (thresholdBasisPoints > 0)
            outcomes.Add(new ChanceLawOutcomeV111(
                "upgrade_check:passed", thresholdBasisPoints));
        if (thresholdBasisPoints < ticketCount)
            outcomes.Add(new ChanceLawOutcomeV111(
                "upgrade_check:not_passed",
                ticketCount - thresholdBasisPoints));
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            cardId,
            act,
            ascension,
            thresholdBasisPoints,
            ticketCount,
            comparison = "ticket_less_than_threshold"
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-card-upgrade-threshold-ticket-v1", conditioning,
            "uniform 10,000-way legacy bounded-ticket model; source-shaped " +
            "upgrade threshold remains unverified against the native game RNG.",
            outcomes.ToArray());
    }

    /// <summary>
    /// Query the next legacy card-reward rarity ticket after the caller's
    /// declared within-group selections. Empty rarity pools are removed using
    /// the same ordered prototype pool as reward generation.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRewardCardRarityTicket(
        int act, string groupOwnerId, string[] priorCardIds, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        ArgumentException.ThrowIfNullOrWhiteSpace(groupOwnerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var available = RemainingRewardCardPool(priorCardIds);
        var rarityWeights = PrototypeContent.RewardRarityWeights(act)
            .Where(item => item.Weight > 0 && available.Any(cardId =>
                PrototypeContent.Card(cardId).Rarity == item.Rarity))
            .ToArray();
        if (rarityWeights.Length == 0)
            throw new ArgumentException(
                "The declared remaining reward card pool has no drawable rarity.",
                nameof(priorCardIds));

        var raritySupport = rarityWeights.Select(item => new
        {
            item.Rarity,
            item.Weight
        }).ToArray();
        var ownerId = groupOwnerId + ":card:" + priorCardIds.Length +
            ":weights:" + CanonicalJson.Sha256(raritySupport);
        var outcomes = rarityWeights.Select(item =>
            new ChanceLawOutcomeV111(
                "rarity:" + item.Rarity.ToString().ToLowerInvariant(),
                item.Weight)).ToArray();
        const string siteId = "reward.card_rarity.ticket";
        const string streamId = "reward";
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            groupOwnerId,
            ordinal,
            act,
            priorCardIds,
            availableCardPoolFingerprint = CanonicalJson.Sha256(available),
            raritySupport,
            totalWeight = rarityWeights.Sum(item => item.Weight)
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-reward-card-rarity-ticket-v1", conditioning,
            "Uniform ticket-index weights over the source-shaped prototype " +
            "rarity weights after empty tiers are removed; reward pool and " +
            "Native parity remain unverified.", outcomes);
    }

    /// <summary>
    /// Query the legacy reward card identity draw within its selected rarity,
    /// after removing card identities already selected in the same group.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRewardCardModel(
        int act, string groupOwnerId, PrototypeCardRarity rarity,
        string[] priorCardIds, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        ArgumentException.ThrowIfNullOrWhiteSpace(groupOwnerId);
        if (!Enum.IsDefined(rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (!PrototypeContent.RewardRarityWeights(act).Any(item =>
                item.Rarity == rarity && item.Weight > 0))
            throw new ArgumentException(
                "The declared rarity is not selectable by the reward rarity ticket for this Act.",
                nameof(rarity));

        var available = RemainingRewardCardPool(priorCardIds);
        var candidates = available.Where(cardId =>
            PrototypeContent.Card(cardId).Rarity == rarity).ToArray();
        if (candidates.Length == 0)
            throw new ArgumentException(
                "The declared rarity and prior selections leave no reward card to draw.",
                nameof(priorCardIds));

        const string siteId = "reward.card_model.index_draw";
        const string streamId = "reward";
        var poolFingerprint = CanonicalJson.Sha256(candidates);
        var ownerId = groupOwnerId + ":card:" + priorCardIds.Length +
            ":rarity:" + rarity + ":pool:" + poolFingerprint;
        var outcomes = candidates.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            groupOwnerId,
            ordinal,
            act,
            rarity,
            priorCardIds,
            orderedCandidates = candidates,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-reward-card-model-pool-v1", conditioning,
            "Equal index weights over the remaining source-ordered prototype " +
            "cards of the declared rarity; pool fidelity and Native parity " +
            "remain unverified.", outcomes, operationKind: "choose_index");
    }

    private static string[] RemainingRewardCardPool(
        string[] priorCardIds, PrototypeCardType? requiredType = null)
    {
        ArgumentNullException.ThrowIfNull(priorCardIds);
        var pool = PrototypeContent.RewardCardPool.Where(cardId =>
                requiredType is null ||
                PrototypeContent.Card(cardId).Type == requiredType.Value)
            .ToArray();
        var poolSet = pool.ToHashSet(StringComparer.Ordinal);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cardId in priorCardIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
            if (!poolSet.Contains(cardId) || !selected.Add(cardId))
                throw new ArgumentException(
                    "Prior card IDs must be unique members of the declared source card pool.",
                    nameof(priorCardIds));
        }

        return pool.Where(cardId => !selected.Contains(cardId)).ToArray();
    }

    private static void ValidateLegacyShopCardType(
        PrototypeCardType requiredType)
    {
        if (requiredType is not (PrototypeCardType.Attack or
            PrototypeCardType.Skill or PrototypeCardType.Power))
            throw new ArgumentOutOfRangeException(nameof(requiredType));
    }

    /// <summary>
    /// Query the legacy Shop rarity ticket after caller-declared offers have
    /// been removed from the requested card-type pool.
    /// </summary>
    public static ChanceLawV111 QueryLegacyShopCardRarityTicket(
        int act, PrototypeCardType requiredType, string[] priorCardIds,
        int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        ValidateLegacyShopCardType(requiredType);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var available = RemainingRewardCardPool(priorCardIds, requiredType);
        var rarityWeights = PrototypeContent.ShopRarityWeights(act)
            .Where(item => item.Weight > 0 && available.Any(cardId =>
                PrototypeContent.Card(cardId).Rarity == item.Rarity))
            .ToArray();
        if (rarityWeights.Length == 0)
            throw new ArgumentException(
                "The remaining Shop card pool has no drawable rarity.",
                nameof(priorCardIds));

        var raritySupport = rarityWeights.Select(item => new
        {
            item.Rarity,
            item.Weight
        }).ToArray();
        var ownerId = $"act:{act}:type:{requiredType}:offer:" +
            priorCardIds.Length + ":weights:" +
            CanonicalJson.Sha256(raritySupport);
        var outcomes = rarityWeights.Select(item =>
            new ChanceLawOutcomeV111(
                "rarity:" + item.Rarity.ToString().ToLowerInvariant(),
                item.Weight)).ToArray();
        const string siteId = "shop.card_rarity.ticket";
        const string streamId = "shop";
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            act,
            requiredType,
            priorCardIds,
            availableCardPoolFingerprint = CanonicalJson.Sha256(available),
            raritySupport,
            totalWeight = rarityWeights.Sum(item => item.Weight)
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-shop-card-rarity-ticket-v1", conditioning,
            "Uniform ticket-index weights over source-shaped Shop rarity " +
            "weights after empty tiers are removed; card-pool fidelity and " +
            "Native parity remain unverified.", outcomes);
    }

    /// <summary>
    /// Query the legacy Shop card-model draw within its selected rarity and
    /// card type after caller-declared prior offers are removed.
    /// </summary>
    public static ChanceLawV111 QueryLegacyShopCardModel(
        int act, PrototypeCardType requiredType, PrototypeCardRarity rarity,
        string[] priorCardIds, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        ValidateLegacyShopCardType(requiredType);
        if (!Enum.IsDefined(rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (!PrototypeContent.ShopRarityWeights(act).Any(item =>
                item.Rarity == rarity && item.Weight > 0))
            throw new ArgumentException(
                "The declared rarity is not selectable by the Shop rarity ticket for this Act.",
                nameof(rarity));

        var available = RemainingRewardCardPool(priorCardIds, requiredType);
        var candidates = available.Where(cardId =>
            PrototypeContent.Card(cardId).Rarity == rarity).ToArray();
        if (candidates.Length == 0)
            throw new ArgumentException(
                "The declared rarity and prior offers leave no Shop card to draw.",
                nameof(priorCardIds));

        const string siteId = "shop.card_model.index_draw";
        const string streamId = "shop";
        var poolFingerprint = CanonicalJson.Sha256(candidates);
        var ownerId = $"act:{act}:type:{requiredType}:offer:" +
            priorCardIds.Length + ":rarity:" + rarity + ":pool:" +
            poolFingerprint;
        var outcomes = candidates.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            act,
            requiredType,
            rarity,
            priorCardIds,
            orderedCandidates = candidates,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-shop-card-model-pool-v1", conditioning,
            "Equal index weights over the remaining source-ordered prototype " +
            "Shop cards of the declared rarity and type; pool fidelity and " +
            "Native parity remain unverified.", outcomes,
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query the retained legacy Shop card price over its rarity-specific
    /// integer price range.
    /// </summary>
    public static ChanceLawV111 QueryLegacyShopCardPrice(
        string cardId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (!PrototypeContent.RewardCardPool.Contains(
                cardId, StringComparer.Ordinal))
            throw new ArgumentException(
                "The declared card must belong to the legacy supported Shop pool.",
                nameof(cardId));

        const string siteId = "shop.card.price.index_draw";
        const string streamId = "shop";
        var prices = PrototypeLegacyShopPriceV111.CardOutcomes(cardId);
        var ownerId = "card:" + cardId;
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-shop-card-price-range-v1",
            new
            {
                siteId,
                streamId,
                ownerId,
                ordinal,
                cardId,
                orderedPrices = prices
            },
            "Equal request-index weights over the rarity-specific prototype " +
            "Shop price range; equal-index weighting remains unverified.",
            prices.Select(price => new ChanceLawOutcomeV111(
                "price:" + price, 1L)).ToArray(),
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query the retained legacy Shop potion price for a declared offer.
    /// </summary>
    public static ChanceLawV111 QueryLegacyShopPotionPrice(
        int act, int floor, int offerOrdinal, string potionId, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (floor < 0)
            throw new ArgumentOutOfRangeException(nameof(floor));
        if (offerOrdinal is < 0 or >= 3)
            throw new ArgumentOutOfRangeException(nameof(offerOrdinal));
        ArgumentException.ThrowIfNullOrWhiteSpace(potionId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (!PrototypeContent.PotionPool.Contains(
                potionId, StringComparer.Ordinal))
            throw new ArgumentException(
                "The declared potion must belong to the legacy supported Shop pool.",
                nameof(potionId));

        const string siteId = "shop.potion_offer.price.index_draw";
        const string streamId = "shop";
        var prices = PrototypeLegacyShopPriceV111.PotionOutcomes;
        var ownerId =
            $"act:{act}:floor:{floor}:offer:{offerOrdinal}:potion:{potionId}";
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-shop-potion-price-range-v1",
            new
            {
                siteId,
                streamId,
                ownerId,
                ordinal,
                act,
                floor,
                offerOrdinal,
                potionId,
                orderedPrices = prices
            },
            "Equal request-index weights over the inclusive 40–60 prototype " +
            "Shop potion price range; equal-index weighting remains " +
            "unverified.",
            prices.Select(price => new ChanceLawOutcomeV111(
                "price:" + price, 1L)).ToArray(),
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query the retained legacy Shop relic price for a declared offer.
    /// </summary>
    public static ChanceLawV111 QueryLegacyShopRelicPrice(
        int act, int floor, int offerOrdinal, string relicId, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (floor < 0)
            throw new ArgumentOutOfRangeException(nameof(floor));
        if (offerOrdinal is < 0 or >= 3)
            throw new ArgumentOutOfRangeException(nameof(offerOrdinal));
        ArgumentException.ThrowIfNullOrWhiteSpace(relicId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (!PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal))
            throw new ArgumentException(
                "The declared relic must belong to the legacy supported Shop pool.",
                nameof(relicId));

        const string siteId = "shop.relic_offer.price.index_draw";
        const string streamId = "shop";
        var prices = PrototypeLegacyShopPriceV111.RelicOutcomes;
        var ownerId =
            $"act:{act}:floor:{floor}:offer:{offerOrdinal}:relic:{relicId}";
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-shop-relic-price-range-v1",
            new
            {
                siteId,
                streamId,
                ownerId,
                ordinal,
                act,
                floor,
                offerOrdinal,
                relicId,
                orderedPrices = prices
            },
            "Equal request-index weights over the inclusive 100–140 prototype " +
            "Shop relic price range; equal-index weighting remains " +
            "unverified.",
            prices.Select(price => new ChanceLawOutcomeV111(
                "price:" + price, 1L)).ToArray(),
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query a legacy persistent-deck transform over the ordered reward pool
    /// with the source card model removed.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRewardCardTransform(
        long cardInstanceId, string originalCardId, int ordinal)
    {
        if (cardInstanceId < 0)
            throw new ArgumentOutOfRangeException(nameof(cardInstanceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(originalCardId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "reward.card_transform.replacement.index_draw";
        const string streamId = "reward";
        var pool = PrototypeContent.RewardCardPool.Where(cardId =>
            !StringComparer.Ordinal.Equals(cardId, originalCardId)).ToArray();
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "No prototype card is available for transformation.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = "card_instance:" + cardInstanceId +
            ":original:" + originalCardId + ":pool:" + poolFingerprint;
        var outcomes = pool.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            cardInstanceId,
            originalCardId,
            orderedCandidates = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-reward-card-transform-pool-v1", conditioning,
            "Equal index weights over the source-ordered prototype reward " +
            "pool excluding the original model; pool fidelity and Native " +
            "parity remain unverified.", outcomes,
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query the retained legacy binary combat-potion ticket. The caller must
    /// declare that potion acquisition is currently allowed; otherwise the
    /// source branch does not draw this ticket.
    /// </summary>
    public static ChanceLawV111 QueryLegacyPotionDropTicket(
        int act, int floor, PrototypeRoomType room,
        bool canAcquirePotion, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (floor < 0)
            throw new ArgumentOutOfRangeException(nameof(floor));
        if (room is not (PrototypeRoomType.Combat or
            PrototypeRoomType.Elite or PrototypeRoomType.Boss))
            throw new ArgumentOutOfRangeException(nameof(room),
                "Legacy combat-potion tickets require Combat, Elite, or Boss rewards.");
        if (!canAcquirePotion)
            throw new ArgumentException(
                "The legacy potion-drop ticket is not drawn when potion acquisition is prevented.",
                nameof(canAcquirePotion));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "reward.random_potion.drop.ticket";
        const string streamId = "reward";
        var ownerId = $"act:{act}:floor:{floor}:room:{room}";
        var outcomes = new[]
        {
            new ChanceLawOutcomeV111("potion_reward:offered", 1L),
            new ChanceLawOutcomeV111("potion_reward:not_offered", 1L)
        };
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            act,
            floor,
            room,
            canAcquirePotion,
            ticketCount = 2,
            offeredTicket = 0
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-combat-potion-drop-ticket-v1", conditioning,
            "Uniform two-way legacy bounded ticket when potion acquisition is " +
            "allowed; the legacy RNG law remains unverified against Native.",
            outcomes);
    }

    /// <summary>
    /// Query the legacy reward-potion model after a successful drop ticket.
    /// It uses the fixed ordered prototype pool and does not verify source
    /// pool completeness or Native stream parity.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRewardPotionModel(
        int act, int floor, PrototypeRoomType room,
        bool canAcquirePotion, bool dropOffered, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (floor < 0)
            throw new ArgumentOutOfRangeException(nameof(floor));
        if (room is not (PrototypeRoomType.Combat or
            PrototypeRoomType.Elite or PrototypeRoomType.Boss))
            throw new ArgumentOutOfRangeException(nameof(room),
                "Legacy combat-potion rewards require Combat, Elite, or Boss rooms.");
        if (!canAcquirePotion || !dropOffered)
            throw new ArgumentException(
                "The reward-potion model draw requires an allowed, successful potion drop.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "reward.random_potion.model";
        const string streamId = "reward";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The legacy reward-potion pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"act:{act}:floor:{floor}:room:{room}:pool:" +
            poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            act,
            floor,
            room,
            canAcquirePotion,
            dropOffered,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-reward-potion-model-pool-v1", conditioning,
            "Equal index weights over the declared ordered prototype reward " +
            "potion pool; pool fidelity and Native parity remain unverified.",
            outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy 1,000-way combat-potion pity ticket. Forced rewards
    /// bypass this draw; the configured Native float route has a separate law.
    /// </summary>
    public static ChanceLawV111 QueryLegacyPotionRewardOddsTicket(
        int currentThousandths, bool elite, bool forced, int ordinal)
    {
        if (forced)
            throw new ArgumentException(
                "Forced potion rewards bypass the legacy odds ticket.",
                nameof(forced));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "reward.potion_odds.ticket";
        const string streamId = "reward";
        const string ownerId = "run:potion_reward_odds";
        var threshold = checked(currentThousandths +
            (elite ? PrototypeNativePotionRewardOdds.EliteBonus : 0));
        var offeredWeight = Math.Clamp((long)threshold, 0L, 1000L);
        var missedWeight = 1000L - offeredWeight;
        var outcomes = new List<ChanceLawOutcomeV111>(2);
        if (offeredWeight > 0)
            outcomes.Add(new ChanceLawOutcomeV111(
                "potion_reward:offered", offeredWeight));
        if (missedWeight > 0)
            outcomes.Add(new ChanceLawOutcomeV111(
                "potion_reward:not_offered", missedWeight));
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            currentThousandths,
            elite,
            forced,
            threshold,
            ticketCount = 1000,
            comparison = "ticket_less_than_threshold"
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-potion-reward-odds-ticket-v1", conditioning,
            "Uniform 1,000-way legacy bounded ticket with the source-shaped " +
            "pity and Elite threshold; RNG parity remains unverified.",
            outcomes.ToArray());
    }

    /// <summary>
    /// Query the retained legacy Uncommon-potion model index over its fixed
    /// source-ordered implemented pool.
    /// </summary>
    public static ChanceLawV111 QueryLegacyUncommonPotionModel(
        string streamId, string ownerPrefix, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerPrefix);
        if (streamId is not ("event" or "reward"))
            throw new ArgumentException(
                "Legacy Uncommon-potion model requests use event or reward streams.",
                nameof(streamId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "potion.uncommon_model.index_draw";
        var pool = PrototypeNativePotionCourier.ImplementedUncommonPotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The implemented Uncommon-potion pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = ownerPrefix + ":pool:" + poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ownerPrefix,
            ordinal,
            orderedImplementedUncommonPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-uncommon-potion-model-pool-v1", conditioning,
            "Equal index weights over the ordered implemented Uncommon " +
            "potion pool; source-pool completeness and stream parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query a legacy potion model draw within a declared rarity after prior
    /// potion exclusions have been applied to the source-ordered pool.
    /// </summary>
    public static ChanceLawV111 QueryLegacyPotionModelWithinRarity(
        PrototypeNativePotionRarity rarity, string streamId,
        string ownerPrefix, string[] excludedPotionIds, int ordinal)
    {
        if (!Enum.IsDefined(rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity));
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerPrefix);
        ArgumentNullException.ThrowIfNull(excludedPotionIds);
        if (streamId is not ("event" or "reward" or "shop"))
            throw new ArgumentException(
                "Legacy potion model requests use event, reward, or shop streams.",
                nameof(streamId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var knownPotionIds = PrototypeNativePotionShop.NativePotionPoolOrder
            .ToHashSet(StringComparer.Ordinal);
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var potionId in excludedPotionIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(potionId);
            if (!knownPotionIds.Contains(potionId))
                throw new ArgumentException(
                    "Excluded potion IDs must belong to the supported potion pool.",
                    nameof(excludedPotionIds));
            excluded.Add(potionId);
        }

        var candidates = PrototypeNativePotionShop.CandidatesForRarity(
            rarity, excluded);
        if (candidates.Length == 0)
            throw new ArgumentException(
                "The declared rarity and exclusions leave no potion model to draw.",
                nameof(excludedPotionIds));
        var poolFingerprint = CanonicalJson.Sha256(candidates);
        var ownerId = ownerPrefix + ":rarity:" + rarity + ":pool:" +
            poolFingerprint;
        var outcomes = candidates.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId = PrototypeNativePotionShop.ModelWithinRaritySiteId,
            streamId,
            ownerId,
            ownerPrefix,
            ordinal,
            rarity,
            excludedPotionIds = excluded.Order(StringComparer.Ordinal)
                .ToArray(),
            orderedCandidates = candidates,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            PrototypeNativePotionShop.ModelWithinRaritySiteId, streamId,
            ownerId,
            ordinal, "legacy-potion-model-within-rarity-pool-v1",
            conditioning,
            "Equal index weights over the remaining source-ordered legacy " +
            "potion rarity pool; source-pool completeness and stream parity " +
            "remain unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query a legacy no-rarity potion draw over the complete ordered potion
    /// pool shared by uniform-offer source paths.
    /// </summary>
    public static ChanceLawV111 QueryLegacyUniformPotionModel(
        string streamId, string ownerPrefix, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerPrefix);
        if (streamId is not ("event" or "reward"))
            throw new ArgumentException(
                "Legacy uniform potion requests use event or reward streams.",
                nameof(streamId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = PrototypeNativePotionShop.UniformModelSiteId;
        var pool = PrototypeNativePotionShop.NativePotionPoolOrder;
        if (pool.Count == 0)
            throw new InvalidOperationException(
                "The legacy uniform potion pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = ownerPrefix + ":pool:" + poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ownerPrefix,
            ordinal,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-uniform-potion-model-pool-v1", conditioning,
            "Equal index weights over the ordered legacy uniform potion pool; " +
            "source-pool completeness and stream parity remain unverified.",
            outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the generic legacy random-potion event effect. Event-specific
    /// Native branches use separate source sites and are outside this law.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventRewardPotionModel(
        string eventId, string choiceId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "event.reward.random_potion.model";
        const string streamId = "event";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The generic event reward potion pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"event:{eventId}:choice:{choiceId}:pool:" +
            poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            eventId,
            choiceId,
            orderedPotionPool = pool,
            poolFingerprint,
            route = "generic_legacy_uniform_pool"
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-event-reward-potion-model-pool-v1", conditioning,
            "Equal index weights over the generic event's ordered prototype " +
            "potion pool; Native event-specific branches and source-pool " +
            "completeness remain unverified.", outcomes,
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy random-Rare-card event effect over the current ordered
    /// prototype reward pool filtered to Rare cards.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventRewardRareCardModel(
        string eventId, string choiceId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "event.reward.random_rare_card.model";
        const string streamId = "reward";
        var pool = PrototypeContent.RewardCardPool.Where(cardId =>
            PrototypeContent.Card(cardId).Rarity ==
            PrototypeCardRarity.Rare).ToArray();
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The generic event reward Rare-card pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"event:{eventId}:choice:{choiceId}:pool:" +
            poolFingerprint;
        var outcomes = pool.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            eventId,
            choiceId,
            orderedRareRewardPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-event-reward-rare-card-pool-v1", conditioning,
            "Equal index weights over the ordered prototype Rare-card reward " +
            "pool; Native character-pool fidelity and stream parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy random-Rare-card relic acquisition model draw. The
    /// configured Native route can query its declared player-stream bound
    /// separately through <see cref="QueryNativePlayerModelIndex"/>.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRelicAcquisitionRareCardModel(
        int ordinal)
    {
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "relic.acquisition.random_rare_card.model";
        const string streamId = "reward";
        var pool = PrototypeContent.RewardCardPool.Where(cardId =>
            PrototypeContent.Card(cardId).Rarity ==
            PrototypeCardRarity.Rare).ToArray();
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The legacy random-Rare-card relic acquisition pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = "player:rare_pool:" + poolFingerprint;
        var outcomes = pool.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            orderedRareRewardPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-relic-acquisition-rare-card-pool-v1", conditioning,
            "Equal index weights over the ordered prototype Rare reward-card " +
            "pool; Native character-pool fidelity and stream attribution " +
            "remain unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy random-potion relic acquisition model draw for the
    /// caller-declared first open potion slot.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRelicAcquisitionPotionModel(
        int slotIndex, int ordinal)
    {
        if (slotIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "relic.acquisition.random_potion.model";
        const string streamId = "event";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The legacy random-potion relic acquisition pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"player:potion_slot:{slotIndex}:pool:" +
            poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            slotIndex,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-relic-acquisition-potion-pool-v1", conditioning,
            "Equal index weights over the ordered prototype potion pool for " +
            "the caller-declared open slot; source-pool fidelity and Native " +
            "stream attribution remain unverified.", outcomes,
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy Run-effect potion fill draw for one caller-declared
    /// empty slot. The effect skips all draws when potion acquisition is not
    /// allowed and draws only for currently empty slots.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRunPotionFillSlotModel(
        int slotIndex, bool canAcquirePotion, bool slotIsEmpty, int ordinal)
    {
        if (slotIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        if (!canAcquirePotion || !slotIsEmpty)
            throw new ArgumentException(
                "Run potion-slot filling requires available potion capacity and a declared empty slot.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "run.potion.fill_empty_slots.model";
        const string streamId = "reward";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The Run potion fill pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"player:potion_slot:{slotIndex}:pool:" +
            poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            slotIndex,
            canAcquirePotion,
            slotIsEmpty,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-run-potion-fill-pool-v1", conditioning,
            "Equal index weights over the ordered prototype potion pool for " +
            "the caller-declared empty slot; source-pool fidelity and Native " +
            "stream attribution remain unverified.", outcomes,
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query a declared Native player-stream bounded index for a model draw.
    /// This verifies the NextInt transform, not owner or pool fidelity.
    /// </summary>
    public static ChanceLawV111 QueryNativePlayerModelIndex(
        string siteId, string ownerId, int supportCount, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (siteId is not ("relic.acquisition.random_rare_card.model" or
            "relic.acquisition.random_potion.model" or
            "run.potion.fill_empty_slots.model" or
            StochasticSiteRegistryV111.UnownedRandomRelicSelectionSiteId))
            throw new NotSupportedException(
                $"Native player model law does not support site '{siteId}'.");
        if (supportCount < 1)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var request = new RandomIndexRequestV111(
            siteId, NativePlayerRngV111.RewardsStreamId, ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray());
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-relic-acquisition-model-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query one Native player-reward draw made by Entropic Brew while
    /// filling an empty potion slot. The caller validates the slot owner
    /// against the action and the ordered source potion pool.
    /// </summary>
    public static ChanceLawV111 QueryNativeRunPotionFillSlotModel(
        RandomIndexRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.ValidateForNativeProvider();
        var pool = PrototypeContent.PotionPool;
        if (request.SiteId != "run.potion.fill_empty_slots.model"
            || request.StreamId != NativePlayerRngV111.RewardsStreamId
            || request.OperationKind != "choose_index"
            || request.SupportCount != pool.Length
            || !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, pool.Length)))
            throw new InvalidDataException(
                "Native Entropic Brew request differs from its ordered potion pool.");

        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-run-potion-fill-slot-model-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query the caller-declared unowned-relic index support on one of its
    /// legacy event streams or the hypothesized Native player reward stream.
    /// Pool filtering and the owner prefix are supplied by the caller.
    /// </summary>
    public static ChanceLawV111 QueryUnownedRelicSelectionIndex(
        string streamId, string ownerId, int supportCount, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (supportCount < 1)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId =
            StochasticSiteRegistryV111.UnownedRandomRelicSelectionSiteId;
        if (streamId == NativePlayerRngV111.RewardsStreamId)
            return QueryNativePlayerModelIndex(
                siteId, ownerId, supportCount, ordinal);
        if (streamId is not ("event" or "reward"))
            throw new ArgumentException(
                "Unowned-relic selection stream must be event, reward, or player.rewards.",
                nameof(streamId));

        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray());
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "legacy-unowned-relic-selection-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query the legacy custom event reward's modifier-generated curse draw.
    /// Native event reward ownership and source-pool completeness remain open.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventModifierCurseModel(
        string eventId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId =
            "event.reward.random_modifier_curse.index_draw";
        const string streamId = "reward";
        var pool = NativeGloryAncientsV111.ModifierGeneratedCurseIds;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The modifier-generated event curse pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"event:{eventId}:pool:" + poolFingerprint;
        var outcomes = pool.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            eventId,
            orderedModifierCursePool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-event-modifier-curse-model-pool-v1", conditioning,
            "Equal index weights over the implemented ordered modifier-curse " +
            "pool; source event-reward audit and Native parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy event potion fill draw for a declared empty slot.
    /// The source skips all slot draws when potion acquisition is unavailable.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventFillPotionSlot(
        int slotIndex, bool canAcquirePotion, bool slotIsEmpty, int ordinal)
    {
        if (slotIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        if (!canAcquirePotion || !slotIsEmpty)
            throw new ArgumentException(
                "Event potion-slot filling requires available potion capacity and a declared empty slot.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "event.potion.fill_empty_slots.model";
        const string streamId = "event";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The event potion fill pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = "player:potion_slot:" + slotIndex + ":pool:" +
            poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            slotIndex,
            canAcquirePotion,
            slotIsEmpty,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-event-fill-potion-slot-pool-v1", conditioning,
            "Equal index weights over the ordered prototype potion pool for " +
            "an eligible empty slot; pool fidelity and Native parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy custom event reward's potion option. The source skips
    /// the draw when potion acquisition is prevented.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventOfferPotionModel(
        string rewardOwnerPrefix, bool canAcquirePotion, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rewardOwnerPrefix);
        if (!canAcquirePotion)
            throw new ArgumentException(
                "The event potion-option draw is skipped when potion acquisition is prevented.",
                nameof(canAcquirePotion));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "event.reward.offer_potion.model.index_draw";
        const string streamId = "reward";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The custom event potion-option pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = rewardOwnerPrefix + ":potion_pool:" + poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            rewardOwnerPrefix,
            ordinal,
            canAcquirePotion,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-event-offer-potion-model-pool-v1", conditioning,
            "Equal index weights over the ordered prototype custom-reward " +
            "potion pool; pool completeness and Native parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy Elite relic model draw over its fixed ordered pool.
    /// Configured Native relic-grab-bag generation uses a separate route.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEliteRelicModel(
        int act, int floor, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (floor < 0)
            throw new ArgumentOutOfRangeException(nameof(floor));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "reward.elite_relic.model";
        const string streamId = "reward";
        var pool = PrototypeContent.RelicPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The legacy Elite relic pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"act:{act}:floor:{floor}:pool:" + poolFingerprint;
        var outcomes = pool.Select(relicId =>
            new ChanceLawOutcomeV111("relic:" + relicId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            act,
            floor,
            orderedRelicPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-elite-relic-model-pool-v1", conditioning,
            "Equal index weights over the ordered prototype Elite relic pool; " +
            "source-pool fidelity and Native relic-bag parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy Treasure relic model draw after excluding relics the
    /// player already owns. Configured Native relic-grab-bag generation uses
    /// a separate route.
    /// </summary>
    public static ChanceLawV111 QueryLegacyTreasureRelicModel(
        int act, int floor, string[] ownedRelicIds, int ordinal)
    {
        if (act < 1)
            throw new ArgumentOutOfRangeException(nameof(act));
        if (floor < 0)
            throw new ArgumentOutOfRangeException(nameof(floor));
        ArgumentNullException.ThrowIfNull(ownedRelicIds);
        if (ownedRelicIds.Any(string.IsNullOrWhiteSpace) ||
            ownedRelicIds.Distinct(StringComparer.Ordinal).Count() !=
                ownedRelicIds.Length)
            throw new ArgumentException(
                "Owned relic IDs must be non-empty and unique.",
                nameof(ownedRelicIds));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "treasure.relic_model.index_draw";
        const string streamId = "reward";
        var owned = ownedRelicIds.ToHashSet(StringComparer.Ordinal);
        var pool = PrototypeContent.RelicPool
            .Where(relicId => !owned.Contains(relicId))
            .ToArray();
        if (pool.Length == 0)
            throw new ArgumentException(
                "The legacy Treasure relic draw has no available pool and consumes no index draw.",
                nameof(ownedRelicIds));

        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"act:{act}:floor:{floor}:pool:" + poolFingerprint;
        var outcomes = pool.Select(relicId =>
            new ChanceLawOutcomeV111("relic:" + relicId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            act,
            floor,
            orderedAvailableRelicPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-treasure-relic-model-pool-v1", conditioning,
            "Equal index weights over the ordered prototype relic pool after " +
            "owned relics are excluded; source-pool fidelity and Native " +
            "relic-bag parity remain unverified.", outcomes,
            operationKind: "choose_index");
    }

    private static (long Rare, long Uncommon, long Common)
        LegacyRarityTicketWeights(int rareThreshold, int uncommonEnd)
    {
        const int ticketCount = 10000;
        var rare = Math.Clamp(rareThreshold, 0, ticketCount);
        var uncommonThrough = Math.Clamp(uncommonEnd, 0, ticketCount);
        var uncommon = Math.Max(0, uncommonThrough - rare);
        var common = checked(ticketCount - rare - uncommon);
        return (rare, uncommon, common);
    }

    private static ChanceLawOutcomeV111[] LegacyRarityOutcomes(
        long common, long uncommon, long rare) =>
        new[]
        {
            new ChanceLawOutcomeV111("rarity:common", common),
            new ChanceLawOutcomeV111("rarity:uncommon", uncommon),
            new ChanceLawOutcomeV111("rarity:rare", rare)
        }.Where(outcome => outcome.Weight > 0).ToArray();

    private static ChanceLawV111 CreateLegacyCategoricalLaw(
        string siteId, string streamId, string ownerId, int ordinal,
        string conditioningPolicyId, object conditioning,
        string probabilityProvenance, ChanceLawOutcomeV111[] outcomes,
        string operationKind = "bounded_integer")
    {
        var conditioningFingerprint = CanonicalJson.Sha256(conditioning);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            streamId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        return Create(eventId, siteId, operationKind, ownerId,
            ordinal, conditioningPolicyId, conditioningFingerprint,
            probabilityProvenance, outcomes);
    }

    /// <summary>
    /// Query an allowlisted legacy index request using its fixed stream and
    /// source operation. The caller declares the owner and current support.
    /// </summary>
    public static ChanceLawV111 QueryLegacyCallerBoundIndex(
        string siteId, string ownerId, int ordinal, int supportCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (!StochasticSiteRegistryV111.TryGetLegacyCallerBoundIndexQuerySite(
                siteId, out var site))
            throw new NotSupportedException(
                $"Legacy bounded-index Chance Law does not support site '{siteId}'.");
        if (supportCount < site.MinimumSupportCount)
            throw new ArgumentOutOfRangeException(nameof(supportCount),
                $"The declared support for '{siteId}' must be at least {site.MinimumSupportCount}.");
        if (site.ExactSupportCount is { } exactSupportCount &&
            supportCount != exactSupportCount)
            throw new ArgumentException(
                $"The declared support for '{siteId}' must be {exactSupportCount}.",
                nameof(supportCount));

        var request = site.OperationKind == "bounded_integer"
            ? new RandomIndexRequestV111(
                siteId, site.StreamId, ownerId, ordinal, [],
                OperationKind: site.OperationKind,
                BoundedCount: supportCount)
            : new RandomIndexRequestV111(
                siteId, site.StreamId, ownerId, ordinal,
                Enumerable.Range(0, supportCount).ToArray(),
                OperationKind: site.OperationKind);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "legacy-caller-bounded-index-request",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query the retained legacy Native-merchant request-index model. The
    /// declared support is checked against the source-shaped integer range;
    /// weights remain unverified because these calls use the prototype RNG.
    /// </summary>
    public static ChanceLawV111 QueryLegacyMerchantIndexRequest(
        string siteId, string ownerId, int ordinal, int supportCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        RandomIndexRequestV111 request;
        if (siteId is "shop.merchant.card_price_modifier.index_draw" or
            "shop.merchant.potion_price_modifier.index_draw")
        {
            if (supportCount != 11)
                throw new ArgumentException(
                    "Legacy card and potion price requests have eleven integer-percent outcomes.",
                    nameof(supportCount));
            request = new RandomIndexRequestV111(
                siteId, "shop", ownerId, ordinal,
                Enumerable.Range(0, 11).ToArray());
        }
        else if (siteId ==
            "shop.merchant.relic_price_modifier.index_draw")
        {
            if (supportCount != 31)
                throw new ArgumentException(
                    "Legacy relic price requests have thirty-one integer-percent outcomes.",
                    nameof(supportCount));
            request = new RandomIndexRequestV111(
                siteId, "shop", ownerId, ordinal,
                Enumerable.Range(0, 31).ToArray());
        }
        else if (siteId == "shop.merchant.card_upgrade_roll.ticket")
        {
            if (supportCount != 10000)
                throw new ArgumentException(
                    "The legacy merchant upgrade request has a 10,000-way ticket.",
                    nameof(supportCount));
            request = new RandomIndexRequestV111(
                siteId, "shop", ownerId, ordinal, [],
                OperationKind: "bounded_integer", BoundedCount: 10000);
        }
        else
        {
            throw new NotSupportedException(
                $"Legacy merchant Chance Law does not support site '{siteId}'.");
        }

        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "legacy-native-merchant-request-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query an allowlisted Native bounded-integer site using its fixed source
    /// stream. The caller declares the current exclusive bound, owner, and
    /// ordinal; site-specific pool and prefix checks remain outside this law.
    /// </summary>
    public static ChanceLawV111 QueryNativeBoundedIntegerLawSite(
        string siteId, string ownerId, int supportCount, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (!NativeBoundedIntegerLawSites.TryGetValue(
                siteId, out var site))
            throw new NotSupportedException(
                $"Native bounded-integer law does not support site '{siteId}'.");
        return QueryNativeBoundedIntegerIndex(
            siteId, site.StreamId, ownerId, supportCount, ordinal,
            site.Boundary);
    }

    /// <summary>
    /// Query one source-shaped Native encounter-formation index. The query
    /// binds the declared model entry and floor to the encounter-local
    /// context stream, while leaving the candidate formation and preceding
    /// GenerateMonsters prefix with the caller.
    /// </summary>
    public static ChanceLawV111 QueryNativeEncounterFormationIndex(
        string encounterEntry, int totalFloor, int supportCount, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterEntry);
        if (totalFloor < 0)
            throw new ArgumentOutOfRangeException(nameof(totalFloor));
        if (supportCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var siteId = NativeEncounterFormationFactoryV111.FormationSiteId;
        var streamId = NativeRngV111Contexts.EncounterFormationStreamId(
            encounterEntry, totalFloor);
        var ownerId = $"encounter:{encounterEntry}:floor:{totalFloor}";
        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray());
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-encounter-formation-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query one Native unique-monster-HP index over a caller-declared
    /// ordered HP support. The caller owns the live eligibility pool and
    /// creature creation history; this law covers only the run.niche index.
    /// </summary>
    public static ChanceLawV111 QueryNativeInitialHpIndex(
        string ownerId, int[] orderedHpSupport, int ordinal)
    {
        ValidateNativeCreatureOwnerId(ownerId);
        ArgumentNullException.ThrowIfNull(orderedHpSupport);
        if (orderedHpSupport.Length == 0 ||
            orderedHpSupport.Any(hp => hp <= 0) ||
            orderedHpSupport.Distinct().Count() != orderedHpSupport.Length)
            throw new ArgumentException(
                "Native initial-HP support must contain distinct positive HP values.",
                nameof(orderedHpSupport));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var request = new RandomIndexRequestV111(
            NativeCombatRngEntryV111.InitialUniqueHpSiteId,
            "run.niche", ownerId, ordinal,
            orderedHpSupport);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-initial-unique-hp-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query the Native in-place Tough Egg hatch HP index. Ascension selects
    /// the source interval; the caller owns the event/creature eligibility.
    /// </summary>
    public static ChanceLawV111 QueryNativeToughEggHatchHpIndex(
        string ownerId, int ascension, int ordinal)
    {
        ValidateNativeCreatureOwnerId(ownerId);
        if (ascension < 0)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        if (ordinal != 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Tough Egg hatch HP is the creature's ordinal-zero draw.");

        var minHp = ascension >= 8 ? 20 : 19;
        var orderedHpSupport = Enumerable.Range(minHp, 4).ToArray();
        var request = new RandomIndexRequestV111(
            NativeCombatRngEntryV111.ToughEggHatchHpSiteId,
            "run.niche", ownerId, ordinal, orderedHpSupport);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-tough-egg-hatch-hp-index",
            ascension,
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    private static void ValidateNativeCreatureOwnerId(string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        const string ownerPrefix = "creature:";
        if (!ownerId.StartsWith(ownerPrefix, StringComparison.Ordinal) ||
            !long.TryParse(ownerId.AsSpan(ownerPrefix.Length),
                NumberStyles.None, CultureInfo.InvariantCulture,
                out var combatId) || combatId < 0 ||
            ownerId != ownerPrefix + combatId.ToString(
                CultureInfo.InvariantCulture))
            throw new ArgumentException(
                "Native HP owner must identify one canonical creature CombatId.",
                nameof(ownerId));
    }

    public const string NativeBoundedIndexSourceVerifiedProvenance =
        "v0.111.0 Rng.NextInt(maxExclusive) multiplies the uniform 53-bit " +
        "NextDouble grid and truncates. Exact source bin masses include " +
        "binary64 rounding boundaries; run-seed frequencies and stream " +
        "derivation are outside this claim.";
    public const string NativeUnitFloatThresholdSourceVerifiedProvenance =
        "v0.111.0 Rng.NextFloat() rounds the uniform 53-bit NextDouble grid " +
        "to binary32 before the source threshold comparisons. Exact grid " +
        "preimages include float rounding and comparison inclusivity; seed " +
        "frequencies and stream derivation are outside this claim.";
    public const string NativeUnitFloatMappedOutcomeSourceVerifiedProvenance =
        "v0.111.0 Rng.NextFloat() rounds the uniform 53-bit NextDouble grid " +
        "to binary32 before the source outcome mapping. Exact grid " +
        "preimages include subsequent source float and integer rounding; " +
        "seed frequencies and stream derivation are outside this claim.";
    public const string NativeUnitFloatWeightedTicketSourceVerifiedProvenance =
        "v0.111.0 Rng.NextFloat() rounds the uniform 53-bit NextDouble grid " +
        "to binary32 before the weighted ticket mapping. Exact grid " +
        "preimages include the source float multiplication and sequential " +
        "ticket subtraction; seed frequencies are outside this claim.";

    /// <summary>Exact integer masses for each result of the pinned Native
    /// bounded-index transform, scaled by the 2^53 input-grid size. This
    /// counts the binary64 multiply's round-to-nearest bins before truncation.
    /// </summary>
    internal static long[] NativeNextIntGridWeights(int boundExclusive)
    {
        if (boundExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(boundExclusive));

        var weights = new long[boundExclusive];
        long total = 0;
        for (var index = 0; index < boundExclusive; index++)
        {
            var first = index == 0 ? 0 : NativeNextIntBoundaryGridIndex(
                index, boundExclusive);
            var end = NativeNextIntBoundaryGridIndex(
                index + 1, boundExclusive);
            var weight = checked(end - first);
            if (weight <= 0)
                throw new InvalidDataException(
                    "Native bounded-index grid produced an empty outcome bin.");
            weights[index] = weight;
            total = checked(total + weight);
        }

        if (total != NativeNextDoubleGridSize)
            throw new InvalidDataException(
                "Native bounded-index grid masses do not cover the full input grid.");
        return weights;
    }

    /// <summary>Counts grid inputs whose source-rounded unit float is strictly
    /// below a threshold. The mapping is monotone, so a binary search over the
    /// 2^53 inputs gives the exact preimage count without enumerating the grid.
    /// </summary>
    internal static long NativeNextFloatGridCountBelow(float threshold)
    {
        if (!float.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold));
        return NativeNextFloatGridCount(value => value < threshold);
    }

    /// <summary>Counts grid inputs whose source-rounded unit float is at most
    /// a threshold, preserving source branches that use an inclusive check.
    /// </summary>
    internal static long NativeNextFloatGridCountAtOrBelow(float threshold)
    {
        if (!float.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold));
        return NativeNextFloatGridCount(value => value <= threshold);
    }

    private static long NativeNextFloatGridCount(Func<float, bool> includes)
    {
        ArgumentNullException.ThrowIfNull(includes);
        var low = 0L;
        var high = NativeNextDoubleGridSize;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            var sourceFloat = NativeUnitFloatGridValue(middle);
            if (includes(sourceFloat))
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private static float NativeUnitFloatGridValue(long gridIndex) =>
        (float)(gridIndex * (1.0 / NativeNextDoubleGridSize));

    internal static long[] NativeNextFloatWeightedTicketGridWeights(
        IReadOnlyList<int> candidateWeights)
    {
        ArgumentNullException.ThrowIfNull(candidateWeights);
        if (candidateWeights.Count == 0 ||
            candidateWeights.Any(weight => weight <= 0))
            throw new ArgumentOutOfRangeException(nameof(candidateWeights));
        var totalWeight = checked(candidateWeights.Sum());

        int MapTicket(float roll)
        {
            var ticket = roll * totalWeight;
            for (var index = 0; index < candidateWeights.Count; index++)
            {
                if (ticket < candidateWeights[index])
                    return index;
                ticket -= candidateWeights[index];
            }
            return candidateWeights.Count - 1;
        }

        var mappedWeights = NativeNextFloatMappedIntegerWeights(MapTicket);
        var weights = new long[candidateWeights.Count];
        long total = 0;
        foreach (var (index, weight) in mappedWeights)
        {
            if (index < 0 || index >= weights.Length)
                throw new InvalidDataException(
                    "Native weighted-ticket mapping returned an invalid index.");
            weights[index] = weight;
            total = checked(total + weight);
        }
        if (total != NativeNextDoubleGridSize)
            throw new InvalidDataException(
                "Native weighted-ticket masses do not cover the input grid.");
        return weights;
    }

    private static (long Rare, long Uncommon, long Common)
        NativeCardRarityGridWeights(int rareThreshold, int uncommonEnd)
    {
        var rare = NativeNextFloatGridCountBelow(
            rareThreshold / 10000f);
        var uncommonEndCount = NativeNextFloatGridCountBelow(
            uncommonEnd / 10000f);
        if (uncommonEndCount < rare)
            throw new InvalidDataException(
                "Native card-rarity float thresholds are out of order.");
        var uncommon = checked(uncommonEndCount - rare);
        var common = checked(NativeNextDoubleGridSize - uncommonEndCount);
        return (rare, uncommon, common);
    }

    private static (long Common, long Uncommon, long Rare)
        NativePotionFactoryRarityGridWeights()
    {
        var rare = NativeNextFloatGridCountAtOrBelow(0.1f);
        var uncommonEnd = NativeNextFloatGridCountAtOrBelow(0.35f);
        if (uncommonEnd < rare)
            throw new InvalidDataException(
                "Native PotionFactory rarity thresholds are out of order.");
        var uncommon = checked(uncommonEnd - rare);
        var common = checked(NativeNextDoubleGridSize - uncommonEnd);
        return (common, uncommon, rare);
    }

    private static (long Common, long Uncommon, long Rare)
        NativeRelicRarityGridWeights()
    {
        var commonThreshold = PrototypeNativeRelicGrabBag
            .CommonRarityWeight /
            (float)PrototypeNativeRelicGrabBag.RarityWeightScale;
        var uncommonThreshold = (PrototypeNativeRelicGrabBag
            .CommonRarityWeight +
            PrototypeNativeRelicGrabBag.UncommonRarityWeight) /
            (float)PrototypeNativeRelicGrabBag.RarityWeightScale;
        var common = NativeNextFloatGridCountBelow(commonThreshold);
        var uncommonEnd =
            NativeNextFloatGridCountBelow(uncommonThreshold);
        if (uncommonEnd < common)
            throw new InvalidDataException(
                "Native relic-rarity float thresholds are out of order.");
        var uncommon = checked(uncommonEnd - common);
        var rare = checked(NativeNextDoubleGridSize - uncommonEnd);
        return (common, uncommon, rare);
    }

    private static long NativeNextIntBoundaryGridIndex(
        int integerBoundary, int boundExclusive)
    {
        if (integerBoundary <= 0 || integerBoundary > boundExclusive)
            throw new ArgumentOutOfRangeException(nameof(integerBoundary));

        // The lower neighbor of a power-of-two integer has half the spacing
        // of the values immediately above it. The integer is even in binary64
        // at these magnitudes, so the midpoint rounds to the integer itself.
        // Thus a product rounds to at least this boundary at
        // integerBoundary - 2^-denominatorPower. Scaling that rational
        // threshold by 2^53 / boundExclusive and taking its ceiling counts
        // the grid inputs below the boundary.
        var exponent = BitOperations.Log2((uint)integerBoundary);
        var denominatorPower = 53 - exponent +
            ((integerBoundary & (integerBoundary - 1)) == 0 ? 1 : 0);
        var denominatorScale = BigInteger.One << denominatorPower;
        var midpointNumerator =
            (BigInteger)integerBoundary * denominatorScale - BigInteger.One;
        var numerator = midpointNumerator * NativeNextDoubleGridSize;
        var denominator = denominatorScale * boundExclusive;
        var quotient = BigInteger.DivRem(numerator, denominator,
            out var remainder);
        if (!remainder.IsZero)
            quotient += BigInteger.One;
        return checked((long)quotient);
    }

    public const string IntentConditioningPolicyId =
        "source-filtered-intent-weights-v1";
    public const string NativeRandomIndexConditioningPolicyId =
        "source-native-random-index-nextint-grid-v1";
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
    public const string NativeDrawInsertionConditioningPolicyId =
        "source-bounded-draw-insertion-nextint-grid-v1";
    public const string CardShortlistConditioningPolicyId =
        "uniform-sequential-without-replacement-v1";
    public const string EventOfferConditioningPolicyId =
        "uniform-ordered-event-offer-entry-v1";
    public const string AncientOfferShuffleConditioningPolicyId =
        "uniform-ancient-offer-fisher-yates-swap-v1";
    public const string DarvOfferVariantConditioningPolicyId =
        "uniform-darv-offer-variant-v1";
    public const string NativePotionFactoryIndexConditioningPolicyId =
        "source-bounded-potion-factory-model-index-v1";
    public const string NativePotionFactoryThresholdConditioningPolicyId =
        "source-threshold-potion-factory-rarity-v1";
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

    /// <summary>Query legal indices in a saved typed random request. Canonical
    /// Native stream IDs use exact NextInt grid masses; legacy or unknown
    /// streams retain the unverified uniform request-index model. Only the
    /// typed request enters the law fingerprint; selected outcomes, enclosing
    /// run state and private RNG state do not.</summary>
    public static ChanceLawV111 QueryRandomIndex(
        string eventId, RandomIndexRequestV111 request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var nativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
            request.StreamId);
        var policy = nativeIndexLaw
            ? NativeRandomIndexConditioningPolicyId
            : RandomIndexConditioningPolicyId;
        var weights = nativeIndexLaw
            ? NativeNextIntGridWeights(request.SupportCount)
            : Enumerable.Repeat(1L, request.SupportCount).ToArray();
        var outcomes = Enumerable.Range(0,
                request.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "choice_index:" + index.ToString(CultureInfo.InvariantCulture),
                weights[index]))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            RequestFingerprint = CanonicalJson.Sha256(request),
            policy
        });
        return Create(eventId, request.SiteId,
            request.OperationKind, request.OwnerId, request.Ordinal,
            policy, conditioningFingerprint,
            nativeIndexLaw
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "uniform-saved-index-support-unverified", outcomes,
            nativeIndexLaw
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }

    internal static ChanceLawV111 QueryUniformLabeledIndexSupport(
        string eventId, RandomIndexRequestV111 request,
        string[] candidateLabels, string conditioningPolicyId,
        string probabilityProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateLabels);
        ArgumentException.ThrowIfNullOrWhiteSpace(conditioningPolicyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(probabilityProvenance);
        request.Validate();
        if (request.OperationKind != "choose_index" ||
            candidateLabels.Length != request.SupportCount ||
            candidateLabels.Any(string.IsNullOrWhiteSpace) ||
            candidateLabels.Distinct(StringComparer.Ordinal).Count() !=
                candidateLabels.Length)
            throw new ArgumentException(
                "Labeled index support must match the request and contain distinct labels.",
                nameof(candidateLabels));

        var outcomes = candidateLabels.Select((label, index) =>
            new ChanceLawOutcomeV111(
                $"{label}:index:{index.ToString(CultureInfo.InvariantCulture)}",
                1L)).ToArray();
        return Create(eventId, request.SiteId, request.OperationKind,
            request.OwnerId, request.Ordinal, conditioningPolicyId,
            CanonicalJson.Sha256(new
            {
                RequestFingerprint = CanonicalJson.Sha256(request),
                CandidateLabels = candidateLabels
            }), probabilityProvenance, outcomes);
    }

    /// <summary>
    /// Query the bounded-index transform used by Fishing Rod's persistent
    /// combat-end upgrade. The caller supplies the declared support size;
    /// this law certifies only Native NextInt masses on that support, not the
    /// source relic trigger or eligible-card pool.
    /// </summary>
    public static ChanceLawV111 QueryNativePersistentCombatEndUpgrade(
        RandomIndexRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.SiteId != PrototypeGameEngine.FishingRodUpgradeSiteId ||
            request.StreamId != "run.niche" ||
            request.OperationKind != "choose_index")
            throw new ArgumentException(
                "Fishing Rod law requires its Native run.niche index request.",
                nameof(request));
        return QueryRandomIndex("relic.persistent_combat_end", request);
    }

    internal static ChanceLawV111 QueryImprovementCombatEndUpgrade(
        string actionId, RandomIndexRequestV111 request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.SiteId != PrototypeGameEngine.ImprovementCardSiteId ||
            request.StreamId != "combat" ||
            request.OperationKind != "choose_index")
            throw new ArgumentException(
                "Improvement law requires its legacy combat index request.",
                nameof(request));
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "combat.improvement.power.index_choice",
            actionId
        });
        return QueryRandomIndex(eventId, request);
    }

    internal static ChanceLawV111 QueryPaelsToothCombatEndReturn(
        string actionId, RandomIndexRequestV111 request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.SiteId != PrototypeGameEngine.PaelsToothCardReturnSiteId ||
            request.StreamId != "reward" ||
            request.OperationKind != "choose_index")
            throw new ArgumentException(
                "Pael's Tooth law requires its legacy reward index request.",
                nameof(request));
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "relic.paels_tooth.card_return.index_choice",
            actionId
        });
        return QueryRandomIndex(eventId, request);
    }

    public static ChanceLawV111 QueryPaelsLegionSkin(
        string ownerId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        const string ownerPrefix = "relic:paels_legion:instance:";
        var slotText = ownerId.StartsWith(ownerPrefix,
            StringComparison.Ordinal)
            ? ownerId[ownerPrefix.Length..] : string.Empty;
        if (ordinal != 0 || !int.TryParse(slotText,
                System.Globalization.NumberStyles.None,
                CultureInfo.InvariantCulture, out var slot) || slot < 0)
            throw new ArgumentException(
                "Pael's Legion law requires its relic-instance owner ID and isolated stream ordinal zero.",
                ordinal != 0 ? nameof(ordinal) : nameof(ownerId));
        var request = new RandomIndexRequestV111(
            NativePaelRewardsV111.LegionSkinSiteId, "reward", ownerId,
            ordinal, Enumerable.Range(0,
                NativePaelRewardsV111.LegionSkins.Length).ToArray());
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "relic.paels_legion.skin_choice",
            slot
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query one of the fixed Act 1 boss pools before run initialization.
    /// Outcomes preserve source-ordered encounter identities; these laws do
    /// not claim their legacy seed-to-index distribution is calibrated.
    /// </summary>
    public static ChanceLawV111 QueryOvergrowthBossEncounter(int ordinal) =>
        QueryActOneBossEncounter(
            "run.encounter.overgrowth_boss.index_draw", ordinal);

    public static ChanceLawV111 QueryUnderdocksBossEncounter(int ordinal) =>
        QueryActOneBossEncounter(
            PrototypeNativeUnderdocks.BossEncounterIndexSiteId, ordinal);

    /// <summary>
    /// Query the Glory Ascension 10 second-boss choice conditional on the
    /// already-selected first boss. This exposes only the declared remaining
    /// pool and does not inspect an initialized run's hidden encounter bag.
    /// </summary>
    public static ChanceLawV111 QueryLaterActSecondBoss(
        string firstBossId, int ascension, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstBossId);
        if (ascension < 10)
            throw new ArgumentOutOfRangeException(
                nameof(ascension),
                "A second boss is assigned only at Glory Ascension 10 or higher.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Second-boss assignment requires a nonnegative declared stream ordinal.");

        var bossPool = ExperimentalSilentThreeActRouteV111.Pool(
            3, PrototypeRoomType.Boss, weak: false);
        if (!bossPool.Contains(firstBossId, StringComparer.Ordinal))
            throw new ArgumentException(
                "The declared first boss is not in the Glory boss pool.",
                nameof(firstBossId));
        var alternatives = bossPool.Where(id => id != firstBossId).ToArray();
        var siteId = ExperimentalSilentThreeActRouteV111
            .SecondBossIndexSiteId;
        var ownerId = $"act:3:first_boss:{firstBossId}:alternatives:"
            + CanonicalJson.Sha256(alternatives);
        var outcomes = alternatives.Select(encounterId =>
            new ChanceLawOutcomeV111(
                "encounter:" + encounterId, 1L)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            act = 3,
            ascension,
            firstBossId,
            ownerId,
            orderedEncounterIds = alternatives
        });

        return Create(
            eventId,
            siteId,
            "choose_index",
            ownerId,
            ordinal,
            "uniform-source-ordered-second-boss-index-unverified",
            conditioningFingerprint,
            "equal-index-mass-over-remaining-glory-bosses-unverified",
            outcomes);
    }

    /// <summary>
    /// Query the raw Hive or Glory boss-pool index before the deterministic
    /// first-undiscovered-boss preference is applied.
    /// </summary>
    public static ChanceLawV111 QueryLaterActBossPool(int act, int ordinal)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(
                nameof(act), "Later-act boss selection supports Hive or Glory.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Later-act boss selection requires a nonnegative declared stream ordinal.");

        var siteId = ExperimentalSilentThreeActRouteV111.BossIndexSiteId;
        var bossPool = ExperimentalSilentThreeActRouteV111.Pool(
            act, PrototypeRoomType.Boss, weak: false);
        var ownerId = $"act:{act}:boss_pool:"
            + CanonicalJson.Sha256(bossPool);
        var outcomes = bossPool.Select(encounterId =>
            new ChanceLawOutcomeV111(
                "encounter:" + encounterId, 1L)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            act,
            ownerId,
            orderedEncounterIds = bossPool
        });

        return Create(
            eventId,
            siteId,
            "choose_index",
            ownerId,
            ordinal,
            "uniform-source-ordered-later-act-boss-index-unverified",
            conditioningFingerprint,
            "equal-index-mass-over-ordered-later-act-boss-pool-unverified",
            outcomes);
    }

    /// <summary>
    /// Query the Glory map's single Native bounded draw for a rest-room quota.
    /// The exact NextInt(2) transform is source-verified independently of map
    /// generation order, seed derivation, and cursor parity.
    /// </summary>
    public static ChanceLawV111 QueryLaterActGloryRestCount(int ordinal)
    {
        if (ordinal != 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Glory rest-quota selection has one site-local draw at ordinal zero.");

        const int act = 3;
        const int restCountOffset = 5;
        const int supportCount = 2;
        const string ownerId = "map:act:3";
        var siteId = PrototypeNativeLaterActMap.GloryRestCountSiteId;
        var indexWeights = NativeNextIntGridWeights(supportCount);
        var outcomes = indexWeights.Select((weight, index) =>
            new ChanceLawOutcomeV111(
                $"rest_quota:{restCountOffset + index}", weight)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            act,
            ownerId,
            supportCount,
            orderedRestCounts = new[] { 5, 6 }
        });

        return Create(
            eventId,
            siteId,
            "bounded_integer",
            ownerId,
            ordinal,
            NativeRandomIndexConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query one raw Native path-start column draw before any path-start retry
    /// constraint is applied. The caller declares its site-local ordinal.
    /// </summary>
    public static ChanceLawV111 QueryLaterActPathStart(int act, int ordinal)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(
                nameof(act), "Later-act path starts support Hive or Glory.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Later-act path-start queries require a nonnegative site-local ordinal.");

        return QueryMapPathStart(
            PrototypeNativeLaterActMap.PathStartSiteId,
            $"map:act:{act}", act, ordinal);
    }

    /// <summary>
    /// Query one raw Native Overgrowth path-start draw. The source owner
    /// distinguishes each path and the second-path collision retries.
    /// </summary>
    public static ChanceLawV111 QueryOvergrowthPathStart(
        string ownerId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (!IsOvergrowthPathStartOwner(ownerId))
            throw new ArgumentException(
                "Owner ID must identify an Act 1 path start or path-two retry.",
                nameof(ownerId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Overgrowth path-start queries require a nonnegative site-local ordinal.");

        return QueryMapPathStart(
            PrototypeNativeOvergrowthMap.PathStartColumnSiteId,
            ownerId, 1, ordinal);
    }

    /// <summary>
    /// Query one raw Native Hive/Glory delta-shuffle index. Every three-entry
    /// Fisher-Yates shuffle uses bounds 3 then 2 at successive site ordinals.
    /// </summary>
    public static ChanceLawV111 QueryLaterActPathDeltaSwap(
        int act, int ordinal)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(
                nameof(act), "Later-act path deltas support Hive or Glory.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Later-act path-delta queries require a nonnegative site-local ordinal.");

        return QueryMapPathDeltaSwap(
            PrototypeNativeLaterActMap.PathDeltaShuffleSiteId,
            $"map:act:{act}", ordinal);
    }

    /// <summary>
    /// Query one raw Native Overgrowth delta-shuffle index using its source
    /// path/floor owner and site-local ordinal.
    /// </summary>
    public static ChanceLawV111 QueryOvergrowthPathDeltaSwap(
        string ownerId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (!IsOvergrowthPathDeltaOwner(ownerId))
            throw new ArgumentException(
                "Owner ID must identify an Act 1 path and floor delta shuffle.",
                nameof(ownerId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Overgrowth path-delta queries require a nonnegative site-local ordinal.");

        return QueryMapPathDeltaSwap(
            PrototypeNativeOvergrowthMap.PathDeltaShuffleSiteId,
            ownerId, ordinal);
    }

    private static ChanceLawV111 QueryMapPathDeltaSwap(
        string siteId, string ownerId, int ordinal)
    {
        var supportCount = ordinal % 2 == 0 ? 3 : 2;
        var indexWeights = NativeNextIntGridWeights(supportCount);
        var outcomes = indexWeights.Select((weight, index) =>
            new ChanceLawOutcomeV111($"swap_index:{index}", weight))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            ownerId,
            ordinal,
            sourcePoolSize = 3,
            supportCount
        });

        return Create(
            eventId,
            siteId,
            "bounded_integer",
            ownerId,
            ordinal,
            NativeRandomIndexConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query a Hive/Glory room-placement Fisher-Yates swap using its current
    /// caller-declared bounded support.
    /// </summary>
    public static ChanceLawV111 QueryLaterActRoomAssignmentSwap(
        int act, int ordinal, int supportCount)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(
                nameof(act), "Later-act room placement supports Hive or Glory.");
        var ownerId = $"map:act:{act}";
        return QueryMapRoomAssignmentSwap(
            PrototypeNativeLaterActMap.RoomAssignmentShuffleSiteId,
            ownerId, ordinal, supportCount);
    }

    /// <summary>
    /// Query one Overgrowth room-placement Fisher-Yates swap. The owner binds
    /// the room type, attempt, and ordered-pool fingerprint.
    /// </summary>
    public static ChanceLawV111 QueryOvergrowthRoomAssignmentSwap(
        string ownerId, int ordinal, int supportCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (!IsOvergrowthRoomAssignmentOwner(ownerId))
            throw new ArgumentException(
                "Owner ID must identify an Act 1 room type, placement attempt, and pool fingerprint.",
                nameof(ownerId));
        return QueryMapRoomAssignmentSwap(
            PrototypeNativeOvergrowthMap.RoomAssignmentShuffleSiteId,
            ownerId, ordinal, supportCount);
    }

    private static ChanceLawV111 QueryMapRoomAssignmentSwap(
        string siteId, string ownerId, int ordinal, int supportCount)
    {
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (supportCount < 2)
            throw new ArgumentOutOfRangeException(
                nameof(supportCount),
                "A Fisher-Yates swap requires a bounded support of at least two.");

        var indexWeights = NativeNextIntGridWeights(supportCount);
        var outcomes = indexWeights.Select((weight, index) =>
            new ChanceLawOutcomeV111($"swap_index:{index}", weight))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            ownerId,
            ordinal,
            supportCount
        });

        return Create(
            eventId,
            siteId,
            "bounded_integer",
            ownerId,
            ordinal,
            NativeRandomIndexConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static ChanceLawV111 QueryMapPathStart(
        string siteId, string ownerId, int act, int ordinal)
    {
        const int supportCount = PrototypeNativeLaterActMap.MapWidth;
        var indexWeights = NativeNextIntGridWeights(supportCount);
        var outcomes = indexWeights.Select((weight, index) =>
            new ChanceLawOutcomeV111(
                $"path_column:{index}", weight)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            act,
            ownerId,
            supportCount,
            orderedColumns = Enumerable.Range(0, supportCount).ToArray()
        });

        return Create(
            eventId,
            siteId,
            "bounded_integer",
            ownerId,
            ordinal,
            NativeRandomIndexConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static bool IsOvergrowthPathStartOwner(string ownerId)
    {
        var parts = ownerId.Split(':');
        if (parts.Length != 5 || parts[0] != "act" || parts[1] != "1" ||
            parts[2] != "path" ||
            !int.TryParse(parts[3], NumberStyles.None,
                CultureInfo.InvariantCulture, out var pathOrdinal) ||
            pathOrdinal is < 0 or > 6)
            return false;
        return parts[4] == "start" ||
            pathOrdinal == 1 && parts[4] == "start_retry";
    }

    private static bool IsOvergrowthPathDeltaOwner(string ownerId)
    {
        var parts = ownerId.Split(':');
        return parts.Length == 7 && parts[0] == "act" && parts[1] == "1" &&
            parts[2] == "path" &&
            int.TryParse(parts[3], NumberStyles.None,
                CultureInfo.InvariantCulture, out var pathOrdinal) &&
            pathOrdinal is >= 0 and <= 6 && parts[4] == "floor" &&
            int.TryParse(parts[5], NumberStyles.None,
                CultureInfo.InvariantCulture, out var floorOrdinal) &&
            floorOrdinal is >= 1 and < PrototypeNativeOvergrowthMap.RoomRows &&
            parts[6] == "deltas";
    }

    private static bool IsOvergrowthRoomAssignmentOwner(string ownerId)
    {
        var parts = ownerId.Split(':');
        return parts.Length == 8 && parts[0] == "act" && parts[1] == "1" &&
            parts[2] == "room" &&
            parts[3] is "Rest" or "Shop" or "Elite" or "Unknown" &&
            parts[4] == "attempt" &&
            int.TryParse(parts[5], NumberStyles.None,
                CultureInfo.InvariantCulture, out var attempt) &&
            attempt is >= 0 and < 3 && parts[6] == "pool" &&
            parts[7].Length == 64 && parts[7].All(Uri.IsHexDigit);
    }

    public static ChanceLawV111 QueryLaterActDarvHiveAssignment(int ordinal)
    {
        var siteId = ExperimentalSilentThreeActRouteV111
            .DarvHiveAssignmentSiteId;
        const string ownerId = "run:darv_assignment:hive";
        var outcomes = new[]
        {
            new ChanceLawOutcomeV111("darv_assignment:none", 1L),
            new ChanceLawOutcomeV111("darv_assignment:hive", 1L)
        };
        return CreateDeclaredUnverifiedIndexLaw(
            siteId, ownerId, ordinal,
            "uniform-two-way-darv-assignment-unverified",
            new { siteId, ownerId, ordinal },
            "equal-index-mass-over-two-way-darv-assignment-unverified",
            outcomes, operationKind: "bounded_integer");
    }

    public static ChanceLawV111 QueryLaterActDarvGloryAssignment(
        bool hiveReceivesDarv, int ordinal)
    {
        var siteId = ExperimentalSilentThreeActRouteV111
            .DarvGloryAssignmentSiteId;
        const string ownerId = "run:darv_assignment:glory";
        ChanceLawOutcomeV111[] outcomes = hiveReceivesDarv
            ? [new ChanceLawOutcomeV111("darv_assignment:none", 1L)]
            : [
                new ChanceLawOutcomeV111("darv_assignment:none", 1L),
                new ChanceLawOutcomeV111("darv_assignment:glory", 1L)
            ];
        return CreateDeclaredUnverifiedIndexLaw(
            siteId, ownerId, ordinal,
            "uniform-remaining-darv-assignment-unverified",
            new { siteId, ownerId, ordinal, hiveReceivesDarv },
            "equal-index-mass-over-remaining-darv-assignment-unverified",
            outcomes, operationKind: "bounded_integer");
    }

    public static ChanceLawV111 QueryLaterActAncientAssignment(
        int act, bool includeDarv, int ordinal)
    {
        var choices = ExperimentalSilentThreeActRouteV111.AncientPool(
            act, includeDarv);
        var siteId = ExperimentalSilentThreeActRouteV111
            .AncientAssignmentSiteId;
        var ownerId = $"act:{act}:include_darv:{includeDarv}:pool:"
            + CanonicalJson.Sha256(choices);
        var outcomes = choices.Select(choiceId =>
            new ChanceLawOutcomeV111("ancient_event:" + choiceId, 1L))
            .ToArray();
        return CreateDeclaredUnverifiedIndexLaw(
            siteId, ownerId, ordinal,
            "uniform-source-ordered-later-act-ancient-unverified",
            new { siteId, act, includeDarv, ownerId, orderedChoiceIds = choices },
            "equal-index-mass-over-declared-later-act-ancient-pool-unverified",
            outcomes);
    }

    /// <summary>
    /// Query the exact Native bounded-index law for a combat gold amount.
    /// Combat bounds require the caller's source gold proportion; elite and
    /// boss bounds are fixed by room and Ascension.
    /// </summary>
    public static ChanceLawV111 QueryNativeCombatGold(
        PrototypeRoomType room, int ascension, float? goldProportion,
        int ordinal)
    {
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (room == PrototypeRoomType.Combat && goldProportion is null)
            throw new ArgumentException(
                "Combat gold queries require the declared gold proportion.",
                nameof(goldProportion));
        if (room != PrototypeRoomType.Combat && goldProportion is not null)
            throw new ArgumentException(
                "Gold proportion is only accepted for normal combat rewards.",
                nameof(goldProportion));

        var bounds = PrototypeNativeCombatGoldReward.Bounds(
            room, ascension, goldProportion ?? 1f);
        if (bounds is null)
            throw new ArgumentException(
                "No combat gold draw occurs at zero gold proportion.",
                nameof(goldProportion));

        var (minimum, maximum) = bounds.Value;
        var supportCount = checked(maximum - minimum + 1);
        var ownerId =
            $"room:{room}:ascension:{ascension}:bounds:{minimum}:{maximum}";
        const string siteId = "reward.combat_gold.index_draw";
        var indexWeights = NativeNextIntGridWeights(supportCount);
        var outcomes = indexWeights.Select((weight, index) =>
            new ChanceLawOutcomeV111($"gold:{minimum + index}", weight))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            room,
            ascension,
            goldProportion = room == PrototypeRoomType.Combat
                ? goldProportion : null,
            minimum,
            maximum,
            supportCount,
            ownerId
        });

        return Create(
            eventId,
            siteId,
            "bounded_integer",
            ownerId,
            ordinal,
            NativeRandomIndexConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the exact Native unit-float threshold law for combat potion
    /// reward presence. The caller declares the persistent pity value, Elite
    /// bonus, and that the force hook did not bypass this draw.
    /// </summary>
    public static ChanceLawV111 QueryNativePotionRewardOdds(
        int currentThousandths, bool elite, bool forced, int ordinal)
    {
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (currentThousandths % 100 != 0)
            throw new ArgumentOutOfRangeException(nameof(currentThousandths),
                "Potion reward odds must be on the persisted 100-thousandth increment.");
        if (forced)
            throw new ArgumentException(
                "Forced potion rewards bypass the odds draw.", nameof(forced));

        const string siteId = "reward.potion_odds.unit_float";
        const string ownerId = "run:potion_reward_odds";
        var thresholdThousandths = checked(currentThousandths +
            (elite ? PrototypeNativePotionRewardOdds.EliteBonus : 0));
        var threshold = thresholdThousandths / 1000f;
        var offeredWeight = NativeNextFloatGridCountBelow(threshold);
        var missedWeight = checked(NativeNextDoubleGridSize - offeredWeight);
        var outcomes = new List<ChanceLawOutcomeV111>(2);
        if (offeredWeight > 0)
            outcomes.Add(new ChanceLawOutcomeV111(
                "potion_reward:offered", offeredWeight));
        if (missedWeight > 0)
            outcomes.Add(new ChanceLawOutcomeV111(
                "potion_reward:not_offered", missedWeight));

        var conditioning = new
        {
            siteId,
            streamId = NativePlayerRngV111.RewardsStreamId,
            ownerId,
            ordinal,
            currentThousandths,
            elite,
            forced,
            thresholdThousandths,
            threshold,
            comparison = "source_float_strictly_less_than_threshold"
        };
        return CreateNativeBoundaryLaw(
            siteId, "unit_float_threshold", ownerId, ordinal,
            "native-combat-potion-odds-float-threshold-v1",
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatThresholdSourceVerifiedProvenance,
            outcomes.ToArray(), ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query one source-conditioned Native card-rarity float threshold.
    /// Outcomes are the raw rolled rarity before CardFactory's available-tier
    /// fallback. The owner and stream ordinal are caller-declared.
    /// </summary>
    public static ChanceLawV111 QueryNativeCardRarity(
        PrototypeNativeCardOddsSource source, int currentOffsetBasisPoints,
        int ascension, string ownerId, int ordinal)
    {
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (!Enum.IsDefined(source))
            throw new ArgumentOutOfRangeException(nameof(source));

        var (rareThreshold, uncommonEnd) =
            PrototypeNativeCardRarityOdds.NativeFloatThresholds(
                currentOffsetBasisPoints, ascension, source);
        var weights = rareThreshold >= 10000
            ? (Rare: NativeNextDoubleGridSize, Uncommon: 0L, Common: 0L)
            : NativeCardRarityGridWeights(rareThreshold, uncommonEnd);
        var outcomes = new[]
            {
                new ChanceLawOutcomeV111("rarity:rare", weights.Rare),
                new ChanceLawOutcomeV111("rarity:uncommon", weights.Uncommon),
                new ChanceLawOutcomeV111("rarity:common", weights.Common)
            }
            .Where(outcome => outcome.Weight > 0).ToArray();
        const string siteId = "card_rarity.roll.unit_float";
        var conditioning = new
        {
            siteId,
            streamId = NativePlayerRngV111.RewardsStreamId,
            ownerId,
            ordinal,
            source,
            currentOffsetBasisPoints,
            ascension,
            rareThreshold,
            uncommonEnd,
            rawRarityBeforeAvailableTierFallback = true
        };
        return CreateNativeBoundaryLaw(
            siteId, "unit_float_threshold", ownerId, ordinal,
            "native-card-rarity-source-threshold-v1",
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatThresholdSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the raw Native combat-card upgrade comparison. The inclusive
    /// float endpoint means a zero threshold still has a one-grid-input pass.
    /// </summary>
    public static ChanceLawV111 QueryNativeEncounterCardUpgrade(
        string cardId, int act, int ascension, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (!PrototypeContent.NativeSilentRewardCardPool.Contains(
                cardId, StringComparer.Ordinal))
            throw new ArgumentException(
                "Card-upgrade laws require an implemented Native Silent reward card.",
                nameof(cardId));

        var thresholdBasisPoints =
            PrototypeNativeCardRarityOdds
                .EncounterCardUpgradeThresholdBasisPoints(
                    cardId, act, ascension);
        var threshold = thresholdBasisPoints / 10000f;
        var upgradedWeight =
            NativeNextFloatGridCountAtOrBelow(threshold);
        var notUpgradedWeight = checked(
            NativeNextDoubleGridSize - upgradedWeight);
        var outcomes = new List<ChanceLawOutcomeV111>(2);
        if (upgradedWeight > 0)
            outcomes.Add(new ChanceLawOutcomeV111(
                "upgrade_check:passed", upgradedWeight));
        if (notUpgradedWeight > 0)
            outcomes.Add(new ChanceLawOutcomeV111(
                "upgrade_check:not_passed", notUpgradedWeight));

        const string siteId = "card_rarity.upgrade_roll.unit_float";
        var ownerId = $"card:{cardId}:act:{act}:ascension:{ascension}";
        var conditioning = new
        {
            siteId,
            streamId = NativePlayerRngV111.RewardsStreamId,
            ownerId,
            ordinal,
            cardId,
            act,
            ascension,
            thresholdBasisPoints,
            threshold,
            comparison = "source_float_less_than_or_equal_to_threshold",
            outcomeScope = "upgrade_check_flag"
        };
        return CreateNativeBoundaryLaw(
            siteId, "unit_float_inclusive_threshold", ownerId, ordinal,
            "native-card-upgrade-inclusive-float-threshold-v1",
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatThresholdSourceVerifiedProvenance,
            outcomes.ToArray(), ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the source-ordered PotionFactory rarity thresholds for an
    /// explicitly declared Native request, independent of its RNG cursor.
    /// </summary>
    public static ChanceLawV111 QueryNativePotionFactoryRarity(
        RandomUnitFloatRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        const string siteId = "potion_factory.rarity_roll.unit_float";
        if (request.SiteId != siteId || request.StreamId is not
            ("run.combat_potion_generation" or "player.rewards" or
             "player.shops"))
            throw new ArgumentException(
                "PotionFactory rarity requests require a recognized source stream.",
                nameof(request));

        var (common, uncommon, rare) =
            NativePotionFactoryRarityGridWeights();
        var outcomes = new[]
            {
                new ChanceLawOutcomeV111("rarity:common", common),
                new ChanceLawOutcomeV111("rarity:uncommon", uncommon),
                new ChanceLawOutcomeV111("rarity:rare", rare)
            }
            .Where(outcome => outcome.Weight > 0).ToArray();
        var conditioning = new
        {
            request,
            rareInclusiveThreshold = 0.1f,
            uncommonInclusiveThreshold = 0.35f,
            common,
            uncommon,
            rare
        };
        return CreateNativeBoundaryLaw(
            siteId, "unit_float_threshold", request.OwnerId,
            request.Ordinal, NativePotionFactoryThresholdConditioningPolicyId,
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatThresholdSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query one Native PotionFactory model index over the source-ordered
    /// remaining support for the preceding rarity result.
    /// </summary>
    public static ChanceLawV111 QueryNativePotionFactoryModelIndex(
        RandomIndexRequestV111 request, PrototypeNativePotionRarity rarity,
        string[] excludedPotionIds)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(excludedPotionIds);
        request.Validate();
        if (request.SiteId != "potion_factory.model_within_rarity.index_draw" ||
            request.StreamId is not ("run.combat_potion_generation" or
                NativePlayerRngV111.RewardsStreamId or
                NativePlayerRngV111.ShopsStreamId) ||
            request.OperationKind != "choose_index" ||
            !Enum.IsDefined(rarity) || request.Ordinal != excludedPotionIds.Length ||
            excludedPotionIds.Any(string.IsNullOrWhiteSpace) ||
            excludedPotionIds.Distinct(StringComparer.Ordinal).Count() !=
                excludedPotionIds.Length)
            throw new ArgumentException(
                "Native combat PotionFactory model request is malformed.");

        var candidates = PrototypeNativePotionShop.CandidatesForRarity(
            rarity, excludedPotionIds);
        if (candidates.Length == 0 ||
            request.SupportCount != candidates.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)))
            throw new InvalidDataException(
                "Native combat PotionFactory support differs from its rarity and exclusion prefix.");
        var weights = NativeNextIntGridWeights(candidates.Length);
        var conditioning = new
        {
            request,
            rarity = rarity.ToString(),
            excludedPotionIds,
            candidates
        };
        var outcomes = candidates.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"potion_model:{id}:index:{index}", weights[index]))
            .ToArray();
        return CreateNativeBoundaryLaw(
            request.SiteId, request.OperationKind, request.OwnerId,
            request.Ordinal, NativePotionFactoryIndexConditioningPolicyId,
            CanonicalJson.Sha256(conditioning),
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query the raw Native relic rarity threshold for a caller-declared
    /// Rewards-stream request. Queue identity and tier fallback are separate.
    /// </summary>
    public static ChanceLawV111 QueryNativeRelicRarity(
        RandomUnitFloatRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        const string siteId = "relic_grab_bag.rarity.unit_float";
        if (request.SiteId != siteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId)
            throw new ArgumentException(
                "Native relic rarity requests require the player.rewards stream.",
                nameof(request));

        var (common, uncommon, rare) = NativeRelicRarityGridWeights();
        var outcomes = new[]
            {
                new ChanceLawOutcomeV111("rarity:common", common),
                new ChanceLawOutcomeV111("rarity:uncommon", uncommon),
                new ChanceLawOutcomeV111("rarity:rare", rare)
            }
            .Where(outcome => outcome.Weight > 0).ToArray();
        var conditioning = new
        {
            request,
            commonThreshold = PrototypeNativeRelicGrabBag
                .CommonRarityWeight /
                (float)PrototypeNativeRelicGrabBag.RarityWeightScale,
            uncommonThreshold = (PrototypeNativeRelicGrabBag
                .CommonRarityWeight +
                PrototypeNativeRelicGrabBag.UncommonRarityWeight) /
                (float)PrototypeNativeRelicGrabBag.RarityWeightScale,
            common,
            uncommon,
            rare
        };
        return CreateNativeBoundaryLaw(
            siteId, "unit_float_threshold", request.OwnerId,
            request.Ordinal, "native-relic-rarity-float-threshold-v1",
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatThresholdSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query a Native encounter card-model index over the caller-declared
    /// remaining pool size. The pool and reward prefix are not inspected.
    /// </summary>
    public static ChanceLawV111 QueryNativeEncounterCardModelIndex(
        string ownerId, int supportCount, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (supportCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "card_rarity.encounter_model.index_draw";
        var request = new RandomIndexRequestV111(
            siteId, NativePlayerRngV111.RewardsStreamId, ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray());
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-encounter-card-model-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query one Native relic-bag Fisher-Yates index over its caller-declared
    /// current bound. Queue eligibility and the preceding shuffle prefix are
    /// not inspected.
    /// </summary>
    public static ChanceLawV111 QueryNativeRelicBagQueueShuffleIndex(
        string ownerId, int supportCount, int ordinal)
    {
        return QueryNativeBoundedIntegerLawSite(
            PrototypeNativeRelicGrabBag.QueueShuffleSiteId, ownerId,
            supportCount, ordinal);
    }

    /// <summary>
    /// Query one Native boss-relic offer Fisher-Yates index over its declared
    /// current bound. The eligible pool and reward prefix are not inspected.
    /// </summary>
    public static ChanceLawV111 QueryNativeBossRelicOfferShuffleIndex(
        string ownerId, int supportCount, int ordinal)
    {
        return QueryNativeBoundedIntegerLawSite(
            StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId, ownerId,
            supportCount, ordinal);
    }

    /// <summary>
    /// Query one Native Lava Rock extra-relic Fisher-Yates index over its
    /// declared current bound. The eligible pool and reward prefix are not
    /// inspected.
    /// </summary>
    public static ChanceLawV111 QueryNativeLavaRockRelicOfferShuffleIndex(
        string ownerId, int supportCount, int ordinal)
    {
        return QueryNativeBoundedIntegerLawSite(
            StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId, ownerId,
            supportCount, ordinal);
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

    private static ChanceLawV111 QueryNativeBoundedIntegerIndex(
        string siteId, string streamId, string ownerId,
        int supportCount, int ordinal, string boundary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundary);
        if (!NativeBoundedIntegerLawSites.ContainsKey(siteId))
            throw new NotSupportedException(
                $"Native bounded-integer law does not support site '{siteId}'.");
        if (supportCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal, Array.Empty<int>(),
            OperationKind: "bounded_integer", BoundedCount: supportCount);
        var eventId = CanonicalJson.Sha256(new { boundary, request });
        return QueryRandomIndex(eventId, request);
    }

    private static ChanceLawV111 CreateDeclaredUnverifiedIndexLaw(
        string siteId, string ownerId, int ordinal,
        string conditioningPolicyId, object conditioning,
        string probabilityProvenance, ChanceLawOutcomeV111[] outcomes,
        string operationKind = "choose_index")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conditioningPolicyId);
        ArgumentNullException.ThrowIfNull(conditioning);
        ArgumentException.ThrowIfNullOrWhiteSpace(probabilityProvenance);
        ArgumentNullException.ThrowIfNull(outcomes);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var conditioningFingerprint = CanonicalJson.Sha256(conditioning);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        return Create(
            eventId,
            siteId,
            operationKind,
            ownerId,
            ordinal,
            conditioningPolicyId,
            conditioningFingerprint,
            probabilityProvenance,
            outcomes);
    }

    private static ChanceLawV111 QueryActOneBossEncounter(
        string siteId, int ordinal)
    {
        if (ordinal != 0)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                "Act 1 boss selection has exactly one draw at ordinal zero.");

        var (bossPool, ownerPrefix) = siteId switch
        {
            "run.encounter.overgrowth_boss.index_draw" =>
                (PrototypeContent.OvergrowthBossEncounterPool,
                    "run:act:1:boss_pool:"),
            PrototypeNativeUnderdocks.BossEncounterIndexSiteId =>
                (PrototypeNativeUnderdocks.NativeBossEncounterIds,
                    "act:1:underdocks:boss_pool:"),
            _ => throw new NotSupportedException(
                $"Act 1 boss law does not support site '{siteId}'.")
        };
        var ownerId = ownerPrefix
            + CanonicalJson.Sha256(bossPool);
        var outcomes = bossPool.Select(encounterId =>
            new ChanceLawOutcomeV111(
                "encounter:" + encounterId, 1L)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = siteId,
            ownerId,
            ordinal
        });
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            ownerId,
            ordinal,
            orderedEncounterIds = bossPool
        });

        return Create(
            eventId,
            siteId,
            "choose_index",
            ownerId,
            ordinal,
            "uniform-source-ordered-boss-index-unverified",
            conditioningFingerprint,
            "equal-index-mass-over-configured-underdocks-boss-pool-unverified",
            outcomes);
    }

    /// <summary>
    /// Query one pre-event Ancient offer decision. Direct offer pools return
    /// stable choice identities; shuffle-based offers return the bounded
    /// Fisher-Yates swap index at the requested ordinal. The same pool builders
    /// and opening boundaries are shared with the event resolvers.
    /// </summary>
    public static ChanceLawV111 QueryAncientOffer(
        RunState run, string siteId, int ordinal) =>
        QueryAncientOffer(run, siteId, ordinal, null, null);

    public static ChanceLawV111 QueryAncientOffer(
        RunState run, string siteId, int ordinal,
        int[]? priorIndices, int? priorVariantIndex = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (siteId == NativeDarvEventV111.OfferVariantSiteId)
        {
            if (priorVariantIndex is not null)
                throw new ArgumentException(
                    "prior_variant_index applies only to Darv relic-shuffle queries.",
                    nameof(priorVariantIndex));
            return QueryDarvOfferVariant(run, ordinal, priorIndices);
        }
        if (siteId == NativeDarvEventV111.RelicShuffleSiteId)
            return QueryDarvRelicShuffle(
                run, ordinal, priorIndices, priorVariantIndex);
        if (priorVariantIndex is not null)
            throw new ArgumentException(
                "prior_variant_index applies only to Darv relic-shuffle queries.",
                nameof(priorVariantIndex));
        if (IsShuffleAncientOfferSite(siteId))
            return QueryAncientOfferShuffle(
                run, siteId, ordinal, priorIndices);
        if (priorIndices is { Length: > 0 })
            throw new ArgumentException(
                "prior_indices apply only to shuffle-based Ancient offers.",
                nameof(priorIndices));
        string ownerId;
        string[] candidates;
        if (siteId == NativeOrobasEventV111.OfferSiteId)
        {
            NativeOrobasEventV111.ValidateOpeningBoundary(run);
            ownerId = "event:OROBAS:act:2:floor:0";
            candidates = NativeOrobasEventV111.OfferCandidateIds(
                run.Player, ordinal);
        }
        else if (siteId == NativePaelRewardsV111.OfferSiteId)
        {
            NativePaelRewardsV111.ValidateOpeningBoundary(run);
            ownerId = "event:PAEL:act:2:floor:0";
            var pools = NativePaelRewardsV111.OfferPools(run.Player);
            if (ordinal < 0 || ordinal >= pools.Length)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            candidates = pools[ordinal].Select(id => "take_" + id).ToArray();
        }
        else if (siteId == NativeAncientRewardsV111.TezcataraOfferSiteId)
        {
            NativeAncientRewardsV111.ValidateTezcataraOpeningBoundary(run);
            ownerId = "event:TEZCATARA:act:2:floor:0";
            var pools = NativeAncientRewardsV111.TezcataraPools(run.Player);
            if (ordinal < 0 || ordinal >= pools.Length)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            candidates = pools[ordinal].Select(id => "take_" + id).ToArray();
        }
        else
        {
            throw new NotSupportedException(
                $"Ancient offer query does not support site '{siteId}'.");
        }

        var orderedChoiceIds = candidates;
        var indexWeights = NativeNextIntGridWeights(orderedChoiceIds.Length);
        var weights = new Dictionary<string, long>(StringComparer.Ordinal);
        var outcomeOrder = new List<string>();
        for (var index = 0; index < orderedChoiceIds.Length; index++)
        {
            var choiceId = orderedChoiceIds[index];
            var outcomeId = "event_choice:" + choiceId;
            if (!weights.TryGetValue(outcomeId, out var weight))
            {
                weights.Add(outcomeId, indexWeights[index]);
                outcomeOrder.Add(outcomeId);
            }
            else
            {
                weights[outcomeId] = checked(weight + indexWeights[index]);
            }
        }
        var outcomes = outcomeOrder.Select(outcomeId =>
            new ChanceLawOutcomeV111(outcomeId, weights[outcomeId])).ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            siteId,
            ownerId,
            ordinal,
            CandidateChoiceIds = orderedChoiceIds,
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
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>Compatibility entry point for the original query-only site.</summary>
    public static ChanceLawV111 QueryOrobasOffer(
        RunState run, int ordinal) => QueryAncientOffer(
            run, NativeOrobasEventV111.OfferSiteId, ordinal);

    /// <summary>
    /// Counts exact 53-bit NextDouble grid preimages for a monotone integer
    /// mapping of the source-rounded unit NextFloat. Binary search locates
    /// each integer outcome's contiguous grid interval.
    /// </summary>
    private static SortedDictionary<int, long>
        NativeNextFloatMappedIntegerWeights(Func<float, int> mapOutcome)
    {
        ArgumentNullException.ThrowIfNull(mapOutcome);
        var weights = new SortedDictionary<int, long>();
        long firstGridIndex = 0;
        long totalWeight = 0;
        while (firstGridIndex < NativeNextDoubleGridSize)
        {
            var mappedOutcome = mapOutcome(
                NativeUnitFloatGridValue(firstGridIndex));
            var low = firstGridIndex + 1;
            var high = NativeNextDoubleGridSize;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                var candidate = mapOutcome(
                    NativeUnitFloatGridValue(middle));
                if (candidate > mappedOutcome)
                    high = middle;
                else
                    low = middle + 1;
            }

            var endGridIndex = low;
            var weight = checked(endGridIndex - firstGridIndex);
            if (weight <= 0 ||
                weights.Count > 0 && weights.Last().Key >= mappedOutcome)
                throw new InvalidDataException(
                    "Native unit-float outcome mapping is not monotone.");
            weights.Add(mappedOutcome, weight);
            totalWeight = checked(totalWeight + weight);
            firstGridIndex = endGridIndex;
        }

        if (weights.Count == 0 ||
            totalWeight != NativeNextDoubleGridSize)
            throw new InvalidOperationException(
                "Native unit-float outcome masses do not cover the input grid.");
        return weights;
    }

    private static ChanceLawV111 CreateNativeBoundaryLaw(
        string siteId, string operationKind, string ownerId, int ordinal,
        string policy, string conditioningFingerprint,
        string provenance, ChanceLawOutcomeV111[] outcomes,
        string probabilityStatus = ChanceLawV111.UnverifiedStatus)
    {
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId, ownerId, ordinal, conditioningFingerprint
        });
        return Create(eventId, siteId, operationKind, ownerId, ordinal,
            policy, conditioningFingerprint, provenance, outcomes,
            probabilityStatus);
    }

    public static int AncientOfferDecisionCount(RunState run, string siteId)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (IsChoiceAncientOfferSite(siteId))
        {
            ValidateAncientOfferOpeningBoundary(run, siteId);
            return 3;
        }
        if (siteId == NativeDarvEventV111.OfferVariantSiteId)
        {
            NativeDarvEventV111.ValidateOpeningBoundary(run);
            return checked(1 + DarvOfferShuffleRequests(run).Length);
        }
        ValidateAncientOfferOpeningBoundary(run, siteId);
        if (siteId == NativeDarvEventV111.RelicShuffleSiteId)
            return DarvOfferShuffleRequests(run).Length;
        var pools = AncientOfferShuffleChoicePools(run, siteId);
        return AncientOfferShuffleRequests(siteId, pools).Length;
    }

    internal static bool IsSupportedAncientOfferSite(string siteId) =>
        IsChoiceAncientOfferSite(siteId) || IsShuffleAncientOfferSite(siteId) ||
        siteId == NativeDarvEventV111.OfferVariantSiteId;

    public static bool IsSupportedAncientOfferQuerySite(string siteId) =>
        IsSupportedAncientOfferSite(siteId);

    internal static bool IsSupportedAncientOfferSessionSite(string siteId) =>
        siteId != NativeDarvEventV111.RelicShuffleSiteId &&
        IsSupportedAncientOfferSite(siteId);

    public static bool IsShuffleAncientOfferSite(string siteId) =>
        siteId is NativeNonupeipeEventV111.ShuffleSiteId or
            NativeTanxEventV111.ShuffleSiteId or
            NativeVakuuEventV111.ShuffleSiteId or
            NativeDarvEventV111.RelicShuffleSiteId;

    internal static ChanceLawV111 QueryAncientOfferSessionStep(
        RunState run, string sessionSiteId,
        ReferenceNatureDecisionV111[] prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (sessionSiteId == NativeDarvEventV111.OfferVariantSiteId)
        {
            if (prefix.Length == 0)
                return QueryDarvOfferVariant(run, 0, null);
            return QueryDarvRelicShuffle(run, prefix.Length - 1,
                prefix.Skip(1).Select(decision => decision.OutcomeIndex)
                    .ToArray(),
                prefix[0].OutcomeIndex);
        }
        var priorIndices = IsShuffleAncientOfferSite(sessionSiteId)
            ? prefix.Select(decision => decision.OutcomeIndex).ToArray()
            : null;
        return QueryAncientOffer(run, sessionSiteId, prefix.Length,
            priorIndices);
    }

    internal static void ValidateAncientOfferOpeningBoundary(
        RunState run, string siteId)
    {
        if (siteId is NativeDarvEventV111.OfferVariantSiteId or
            NativeDarvEventV111.RelicShuffleSiteId)
            NativeDarvEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeOrobasEventV111.OfferSiteId)
            NativeOrobasEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativePaelRewardsV111.OfferSiteId)
            NativePaelRewardsV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeAncientRewardsV111.TezcataraOfferSiteId)
            NativeAncientRewardsV111.ValidateTezcataraOpeningBoundary(run);
        else if (siteId == NativeNonupeipeEventV111.ShuffleSiteId)
            NativeNonupeipeEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeTanxEventV111.ShuffleSiteId)
            NativeTanxEventV111.ValidateOpeningBoundary(run);
        else if (siteId == NativeVakuuEventV111.ShuffleSiteId)
            NativeVakuuEventV111.ValidateOpeningBoundary(run);
        else
            throw new NotSupportedException(
                $"Ancient offer query does not support site '{siteId}'.");
    }

    internal static string[][] AncientOfferShuffleChoicePools(
        RunState run, string siteId)
    {
        if (siteId == NativeDarvEventV111.RelicShuffleSiteId)
            return [NativeDarvEventV111.PoolOneRelicIdsForAct(
                    run.World!.Act, run.Player)
                .Select(id => "take_" + id).ToArray()];
        if (siteId == NativeNonupeipeEventV111.ShuffleSiteId)
            return [NativeNonupeipeEventV111.CandidateRelics(
                    NativeNonupeipeEventV111.HasBeautifulBraceletEligibility(
                        run.Player))
                .Select(id => "take_" + id).ToArray()];
        if (siteId == NativeTanxEventV111.ShuffleSiteId)
            return [NativeTanxEventV111.CandidateRelics(
                    NativeTanxEventV111.HasTriBoomerangEligibility(run.Player))
                .Select(id => "take_" + id).ToArray()];
        if (siteId == NativeVakuuEventV111.ShuffleSiteId)
            return NativeVakuuEventV111.RelicPools.Select(pool =>
                pool.Select(id => "take_" + id).ToArray()).ToArray();
        throw new NotSupportedException(
            $"Ancient offer query does not support shuffle site '{siteId}'.");
    }

    internal static AncientOfferShuffleRequestV111[]
        AncientOfferShuffleRequests(string siteId, string[][] pools)
    {
        ArgumentNullException.ThrowIfNull(pools);
        if (siteId is not (NativeNonupeipeEventV111.ShuffleSiteId or
                NativeTanxEventV111.ShuffleSiteId or
                NativeVakuuEventV111.ShuffleSiteId))
            throw new NotSupportedException(
                $"Ancient offer query does not support shuffle site '{siteId}'.");
        var streamId = siteId switch
        {
            NativeNonupeipeEventV111.ShuffleSiteId =>
                "context.event.NONUPEIPE.player_0",
            NativeTanxEventV111.ShuffleSiteId =>
                "context.event.TANX.player_0",
            NativeVakuuEventV111.ShuffleSiteId =>
                "context.event.VAKUU.player_0",
            _ => throw new NotSupportedException(
                $"Ancient offer query does not support shuffle site '{siteId}'.")
        };
        var ownerId = siteId switch
        {
            NativeNonupeipeEventV111.ShuffleSiteId =>
                "event:NONUPEIPE:act:3:floor:0",
            NativeTanxEventV111.ShuffleSiteId =>
                "event:TANX:act:3:floor:0",
            NativeVakuuEventV111.ShuffleSiteId =>
                "event:VAKUU:act:3:floor:0",
            _ => throw new NotSupportedException(
                $"Ancient offer query does not support shuffle site '{siteId}'.")
        };
        var requests = new List<AncientOfferShuffleRequestV111>();
        for (var poolIndex = 0; poolIndex < pools.Length; poolIndex++)
        {
            var pool = pools[poolIndex];
            for (var end = pool.Length - 1; end > 0; end--)
            {
                var ordinal = requests.Count;
                requests.Add(new AncientOfferShuffleRequestV111(
                    poolIndex, end,
                    new RandomIndexRequestV111(
                        siteId, streamId, ownerId, ordinal, [],
                        OperationKind: "bounded_integer",
                        BoundedCount: end + 1)));
            }
        }
        return requests.ToArray();
    }

    private static ChanceLawV111 QueryAncientOfferShuffle(
        RunState run, string siteId, int ordinal, int[]? priorIndices)
    {
        ValidateAncientOfferOpeningBoundary(run, siteId);
        var initialPools = AncientOfferShuffleChoicePools(run, siteId);
        var requests = AncientOfferShuffleRequests(siteId, initialPools);
        if (ordinal < 0 || ordinal >= requests.Length)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        priorIndices ??= [];
        if (priorIndices.Length != ordinal)
            throw new ArgumentException(
                "Shuffle-based Ancient offer queries require one prior index for every earlier ordinal.",
                nameof(priorIndices));

        var currentPools = initialPools
            .Select(pool => (string[])pool.Clone()).ToArray();
        for (var index = 0; index < ordinal; index++)
        {
            var request = requests[index];
            var selectedIndex = priorIndices[index];
            if (selectedIndex < 0 ||
                selectedIndex >= request.Request.SupportCount)
                throw new ArgumentOutOfRangeException(nameof(priorIndices),
                    $"Prior swap index {index} is outside its saved support.");
            (currentPools[request.PoolIndex][request.EndIndex],
                currentPools[request.PoolIndex][selectedIndex]) =
                (currentPools[request.PoolIndex][selectedIndex],
                    currentPools[request.PoolIndex][request.EndIndex]);
        }

        var selectedRequest = requests[ordinal].Request;
        var ownerId = selectedRequest.OwnerId;
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            InitialChoicePools = initialPools,
            CurrentChoicePools = currentPools,
            PriorIndices = priorIndices,
            RequestFingerprint = CanonicalJson.Sha256(selectedRequest),
            policy = AncientOfferShuffleConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        var weights = NativeNextIntGridWeights(
            selectedRequest.SupportCount);
        var outcomes = Enumerable.Range(0, selectedRequest.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(CultureInfo.InvariantCulture),
                weights[index])).ToArray();
        return Create(eventId, siteId, selectedRequest.OperationKind,
            ownerId, ordinal, AncientOfferShuffleConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static ChanceLawV111 QueryDarvOfferVariant(
        RunState run, int ordinal, int[]? priorIndices)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        if (ordinal != 0 || priorIndices is { Length: > 0 })
            throw new InvalidOperationException(
                "Darv offer-variant law supports only ordinal zero without a prefix.");
        var request = DarvOfferVariantRequest(run);
        var ownerId = request.OwnerId;
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            act = run.World!.Act,
            request,
            outcomes = new[] { "offer_variant:dusty_tome",
                "offer_variant:three_relics" },
            policy = DarvOfferVariantConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId = request.SiteId,
            ownerId,
            ordinal,
            conditioningFingerprint
        });
        var weights = NativeNextIntGridWeights(request.SupportCount);
        var outcomes = new[]
        {
            new ChanceLawOutcomeV111("offer_variant:dusty_tome", weights[0]),
            new ChanceLawOutcomeV111("offer_variant:three_relics", weights[1])
        };
        return Create(eventId, request.SiteId, request.OperationKind,
            ownerId, ordinal, DarvOfferVariantConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static ChanceLawV111 QueryDarvRelicShuffle(
        RunState run, int ordinal, int[]? priorIndices,
        int? priorVariantIndex)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        var requests = DarvOfferShuffleRequests(run);
        if (ordinal < 0 || ordinal >= requests.Length)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        priorIndices ??= [];
        if (priorIndices.Length != ordinal)
            throw new ArgumentException(
                "Darv shuffle queries require one prior index per earlier swap.",
                nameof(priorIndices));
        if (priorVariantIndex is { } variant && variant is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(priorVariantIndex));

        var initial = NativeDarvEventV111.PoolOneRelicIdsForAct(
            run.World!.Act, run.Player);
        var current = (string[])initial.Clone();
        for (var index = 0; index < ordinal; index++)
        {
            var selectedIndex = priorIndices[index];
            if (selectedIndex < 0 ||
                selectedIndex >= requests[index].SupportCount)
                throw new ArgumentOutOfRangeException(nameof(priorIndices),
                    $"Prior Darv swap index {index} is outside its support.");
            var end = initial.Length - index - 1;
            (current[end], current[selectedIndex]) =
                (current[selectedIndex], current[end]);
        }

        var selectedRequest = requests[ordinal];
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            act = run.World.Act,
            initial,
            current,
            priorVariantIndex,
            priorIndices,
            request = CanonicalJson.Sha256(selectedRequest),
            policy = AncientOfferShuffleConditioningPolicyId
        });
        var eventId = CanonicalJson.Sha256(new
        {
            schemaId = ChanceLawV111.CurrentSchemaId,
            siteId = selectedRequest.SiteId,
            ownerId = selectedRequest.OwnerId,
            ordinal,
            conditioningFingerprint
        });
        var weights = NativeNextIntGridWeights(
            selectedRequest.SupportCount);
        var outcomes = Enumerable.Range(0, selectedRequest.SupportCount)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(CultureInfo.InvariantCulture),
                weights[index])).ToArray();
        return Create(eventId, selectedRequest.SiteId,
            selectedRequest.OperationKind, selectedRequest.OwnerId,
            ordinal, AncientOfferShuffleConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    internal static RandomIndexRequestV111 DarvOfferVariantRequest(
        RunState run)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        var streamId = NativeRngV111Contexts.EventStreamId(
            NativeDarvEventV111.NativeRngEventId);
        var ownerId = $"event:DARV:act:{run.World!.Act}:floor:0";
        var request = new RandomIndexRequestV111(
            NativeDarvEventV111.OfferVariantSiteId,
            streamId, ownerId, 0, [],
            OperationKind: "bounded_integer", BoundedCount: 2);
        request.Validate();
        return request;
    }

    internal static RandomIndexRequestV111[] DarvOfferShuffleRequests(
        RunState run)
    {
        NativeDarvEventV111.ValidateOpeningBoundary(run);
        var relics = NativeDarvEventV111.PoolOneRelicIdsForAct(
            run.World!.Act, run.Player);
        var streamId = NativeRngV111Contexts.EventStreamId(
            NativeDarvEventV111.NativeRngEventId);
        var ownerId = $"event:DARV:act:{run.World.Act}:floor:0";
        var requests = new List<RandomIndexRequestV111>();
        for (var end = relics.Length - 1; end > 0; end--)
        {
            var request = new RandomIndexRequestV111(
                NativeDarvEventV111.RelicShuffleSiteId,
                streamId, ownerId, requests.Count, [],
                OperationKind: "bounded_integer", BoundedCount: end + 1);
            request.Validate();
            requests.Add(request);
        }
        return requests.ToArray();
    }

    private static bool IsChoiceAncientOfferSite(string siteId) =>
        siteId is NativeOrobasEventV111.OfferSiteId or
            NativePaelRewardsV111.OfferSiteId or
            NativeAncientRewardsV111.TezcataraOfferSiteId;

    /// <summary>Query a controlled legacy run-action request without exposing
    /// the selected result. The event identity binds the action and request
    /// context without incorporating private run-provider state.</summary>
    public static ChanceLawV111 QueryControlledRunAction(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var continuation = run.Stochastic.ControlledRunActionChoice ??
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
        var continuation = run.Stochastic.ControlledRunActionChoice ??
            throw new InvalidOperationException(
                "No controlled run-action chance is pending.");
        continuation.Validate();
        var boundary = new
        {
            schemaId = ControlledRunActionEngineV111.SchemaId,
            continuation.ActionId,
            continuation.MapNodeId,
            request = continuation.PendingRequest,
            candidateLabels = continuation.PendingCandidateLabels
        };
        return continuation.RunAction is null
            ? CanonicalJson.Sha256(boundary)
            : CanonicalJson.Sha256(new
            {
                boundary,
                actionFingerprint = PrototypeAiEnvironment.StableActionId(
                    continuation.RunAction)
            });
    }

    public static ChanceLawV111 QueryControlledEventChoice(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var description = DescribeControlledEventChoice(run);
        if (run.World?.Event?.EventId ==
                PrototypeNativeStoneOfAllTime.EventId &&
            run.World?.Event?.Stochastic.ControlledChoice?.ChoiceId is
                "push" or "lift")
            return QueryControlledStoneOfAllTimeTicket(
                run, description.Request,
                description.BoundaryFingerprint);
        return QueryRandomIndex(description.EventId, description.Request);
    }

    private static ChanceLawV111 QueryControlledStoneOfAllTimeTicket(
        RunState run, RandomIndexRequestV111 request,
        string boundaryFingerprint)
    {
        var continuation = run.World?.Event?.Stochastic.ControlledChoice ??
            throw new InvalidOperationException(
                "Stone of All Time ticket law requires a pending controlled event choice.");
        continuation.Validate();
        var choiceId = continuation.ChoiceId;
        var expectedSiteId = choiceId switch
        {
            "push" => NativeStoneOfAllTimePushTicketSiteId,
            "lift" => NativeStoneOfAllTimeLiftTicketSiteId,
            _ => throw new InvalidOperationException(
                "Stone of All Time controlled choice has no ticket law.")
        };
        var partialState = continuation.PartialState;
        var partialWorld = partialState.World;
        var partialEvent = partialWorld?.Event ??
            throw new InvalidOperationException(
                "Pending Stone of All Time ticket has no event state.");
        var nativeRoute = partialState.Stochastic.NativeRunRng is not null;
        var expectedStreamId = nativeRoute
            ? NativeRngV111Contexts.EventStreamId(
                PrototypeNativeStoneOfAllTime.NativeRngEventId)
            : "event";
        if (continuation.EventId != PrototypeNativeStoneOfAllTime.EventId ||
            request.SiteId != expectedSiteId ||
            request.StreamId != expectedStreamId ||
            request.OwnerId != $"event:stone_of_all_time:{choiceId}" ||
            (nativeRoute && request.Ordinal != 1) ||
            request.OperationKind != "bounded_integer" ||
            request.BoundedCount != 100)
            throw new InvalidOperationException(
                "Pending Stone of All Time ticket request disagrees with its source boundary.");
        var nativeRouteMismatch = nativeRoute &&
            (partialWorld is not { Act: 2 } ||
             !PrototypeGameEngine.UsesNativeRegionalSystems(partialWorld!));
        var legacyRouteMismatch = !nativeRoute &&
            partialWorld is not { Act: 2 };
        var eventRngMismatch = nativeRoute
            ? partialEvent.Stochastic.NativeRng?.StreamId != expectedStreamId ||
                partialEvent.Stochastic.ProviderMode is not null
            : partialEvent.Stochastic.NativeRng is not null ||
                partialEvent.Stochastic.ProviderMode is not null;
        if (partialState.Phase != RunPhase.Event ||
            partialWorld is not { Act: 2 } ||
            partialEvent.EventId != PrototypeNativeStoneOfAllTime.EventId ||
            partialEvent.NativePageIndex != 0 ||
            eventRngMismatch ||
            nativeRouteMismatch || legacyRouteMismatch ||
            (choiceId == "lift" && partialEvent.ChosenChoiceId is not null) ||
            (choiceId == "push" && partialEvent.ChosenChoiceId != "push"))
            throw new InvalidOperationException(
                "Pending Stone of All Time ticket is outside its Native or legacy event-action boundary.");
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            boundary = "reference_nature.controlled_event.stone_of_all_time.ticket",
            boundaryFingerprint,
            eventId = partialEvent.EventId,
            actionId = choiceId,
            providerRoute = nativeRoute ? "native" : "legacy",
            request
        });
        return CreateNativeBoundaryLaw(
            request.SiteId, "bounded_integer_no_effect", request.OwnerId,
            request.Ordinal,
            nativeRoute
                ? NativeStoneOfAllTimeNoEffectPolicyId
                : LegacyStoneOfAllTimeNoEffectPolicyId,
            conditioningFingerprint,
            nativeRoute
                ? NativeStoneOfAllTimeNoEffectProvenance
                : LegacyStoneOfAllTimeNoEffectProvenance,
            [new ChanceLawOutcomeV111("no_effect", 1L)],
            ChanceLawV111.SourceVerifiedStatus);
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
        var effect = run.World?.Event?.Stochastic.ControlledChoice;
        var reward = run.Stochastic.ControlledEventRewardChoice;
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

    private static ChanceLawV111 Create(
        string eventId, string siteId, string operationKind, string ownerId,
        int ordinal, string conditioningPolicyId,
        string conditioningFingerprint, string probabilityProvenance,
        ChanceLawOutcomeV111[] outcomes,
        string probabilityStatus = ChanceLawV111.UnverifiedStatus)
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
            ProbabilityStatus = probabilityStatus,
            ProbabilityProvenance = probabilityProvenance
        });
        var law = new ChanceLawV111(
            ChanceLawV111.CurrentSchemaId, lawId, eventId, siteId,
            operationKind, ownerId, ordinal, supportFingerprint,
            conditioningPolicyId, conditioningFingerprint,
            probabilityStatus, probabilityProvenance,
            outcomes, totalWeight);
        law.Validate();
        return law;
    }
}

internal sealed record AncientOfferShuffleRequestV111(
    int PoolIndex, int EndIndex, RandomIndexRequestV111 Request);
