Warning: truncated output (original token count: 88922)
Total output lines: 8045

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
    /// <summary>Individual Chance Laws carry one of these statuses. The
    /// legacy ProbabilityStatus field remains the conservative default.</summary>
    public string[] ProbabilityStatuses { get; init; } =
        [ChanceLawV111.UnverifiedStatus];
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
    public string[] ScriptedRunActionOperations { get; init; } =
        ScriptedRunActionSessionV111.Operations;
    public string ScriptedRunSessionSchemaId { get; init; } =
        ScriptedRunSessionV111.CurrentSchemaId;
    public string[] ScriptedRunSessionOperations { get; init; } =
        ScriptedRunSessionV111.Operations;
    public string SemanticNatureChoiceViewSchemaId { get; init; } =
        SemanticNatureChoiceViewV111.CurrentSchemaId;
    public string[] SemanticNatureChoiceViewOperations { get; init; } =
        ["nature_choice_observe", "semantic_nature_observe"];
    public string[] SemanticNatureChoiceViewSessionKinds { get; init; } =
    [
        "semantic_combat", "reference_combat", "run_action",
        "event_choice", "ancient_offer", "neow_offer", "trash_heap",
        "tablet_of_truth", "trial", "event_shuffle", "native_event_gold",
        "native_event_relic",
        "endless_conveyor",
        "merchant", "treasure", "combat_reward", "glory_reflections",
        "infested_automaton",
        "punch_off_reward", "punch_off_fight",
        "map_generation",
        "controlled_event_choice", "controlled_run_action"
    ];
}

public static class StochasticCapabilitiesV111
{
    public const string SchemaId = "sts2-v111-stochastic-capabilities-v3";
    public const string ExternalSelectionProviderId =
        "external-semantic-selection-v1";

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
    public const string CurrentSchemaId = "sts2-v111-chance-law-v1";
    public const string UnverifiedStatus = "UNVERIFIED";
    public const string SourceVerifiedStatus = "SOURCE_VERIFIED";
    public const long NativeSourceGridSize = 1L << 53;

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
            ProbabilityStatus is not (UnverifiedStatus or SourceVerifiedStatus) ||
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
        var canonicalDeterministicOutcome = Outcomes.Length == 1 &&
            Outcomes[0].Weight == 1L;
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
        "potion.rarity_roll.ticket",
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

    private sealed record LegacyCallerBoundIndexQuerySiteV111(
        string StreamId, string OperationKind, int MinimumSupportCount,
        int? ExactSupportCount = null);

    private static readonly Dictionary<string,
        LegacyCallerBoundIndexQuerySiteV111>
        LegacyCallerBoundIndexQuerySites = new(StringComparer.Ordinal)
        {
            ["event.unknown_room.odds.ticket"] = new(
                "event", "bounded_integer", 1000, 1000),
            ["event.selection.weighted_ticket"] = new(
                "event", "bounded_integer", 1),
            ["event.random_upgrade.card_instance"] = new(
                "event", "choose_index", 1),
            [PrototypeGameEngine.PaelsToothCardReturnSiteId] = new(
                "reward", "choose_index", 1),
            ["combat.encounter.formation.index_draw"] = new(
                "combat", "choose_index", 1),
            ["combat.encounter.overgrowth_elite.index_draw"] = new(
                "combat", "choose_index", 1),
            ["combat.encounter.overgrowth_weak.index_draw"] = new(
                "combat", "choose_index", 1),
            ["combat.encounter.overgrowth_normal.index_draw"] = new(
                "combat", "choose_index", 1),
            ["combat.encounter.weighted_selection.ticket"] = new(
                "combat", "bounded_integer", 1),
            ["combat.encounter.underdocks.elite.index_draw"] = new(
                "combat", "choose_index", 1),
            ["combat.encounter.underdocks.weak.index_draw"] = new(
                "combat", "choose_index", 1),
            ["combat.encounter.underdocks.normal.index_draw"] = new(
                "combat", "choose_index", 1),
            ["combat.encounter.later_act.weak.index_draw"] = new(
                "combat", "choose_index", 1),
            ["combat.encounter.later_act.normal.index_draw"] = new(
                "combat", "choose_index", 1),
            ["combat.encounter.later_act.elite.index_draw"] = new(
                "combat", "choose_index", 1),
            ["combat.enemy.summon_pool.select"] = new(
                "combat", "choose_index", 1),
            [PrototypeGameEngine.ImprovementCardSiteId] = new(
                "combat", "choose_index", 1),
            ["map.legacy.floor_width.index_draw"] = new(
                "map", "choose_index", 2),
            ["map.legacy.edge_source.index_draw"] = new(
                "map", "choose_index", 1),
            ["map.legacy.orphan_target.index_draw"] = new(
                "map", "choose_index", 1),
            ["map.legacy.edge_extra_chance.ticket"] = new(
                "map", "bounded_integer", 100, 100),
            ["map.legacy.second_edge_source.index_draw"] = new(
                "map", "choose_index", 1),
            ["map.legacy.convergence.order_key"] = new(
                "map", "bounded_integer", int.MaxValue, int.MaxValue),
            ["map.legacy.convergence.source.index_draw"] = new(
                "map", "choose_index", 1),
            ["map.legacy.second_edge_target.index_draw"] = new(
                "map", "choose_index", 1),
            ["map.legacy.required_room.node.index_draw"] = new(
                "map", "choose_index", 1),
            ["map.legacy.room_assignment.index_draw"] = new(
                "map", "choose_index", 1),
            ["event.reward.card_bundle.shuffle_swap_index"] = new(
                "reward", "bounded_integer", 2),
            ["relic.sand_castle.acquisition_upgrade.shuffle_swap_index"] =
                new("event", "bounded_integer", 2),
            ["run.random_pool_distinct.shuffle_swap_index"] = new(
                "reward", "bounded_integer", 2),
            ["shop.potion_offer.shuffle_swap_index"] = new(
                "shop", "bounded_integer", 2),
            ["shop.relic_offer.shuffle_swap_index"] = new(
                "shop", "bounded_integer", 2)
        };

    private static readonly string[] NativeMapGaussianIntQuerySites =
    [
        PrototypeNativeOvergrowthMap.RestCountGaussianSiteId,
        PrototypeNativeOvergrowthMap.UnknownCountGaussianSiteId,
        PrototypeNativeLaterActMap.HiveRestCountGaussianSiteId,
        PrototypeNativeLaterActMap.UnknownCountGaussianSiteId
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
        "potion.model_within_rarity.index_draw"
    ];

    private static readonly string[] LegacyUniformPotionModelQuerySites =
    [
        "potion.uniform_model.index_draw"
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
            ["relic_grab_bag.queue_shuffle.swap_index"] = new(
                "run.treasure_room_relics",
                "native-relic-bag-queue-shuffle-index"),
            ["reward.boss_relic_offer.shuffle_swap_index"] = new(
                NativePlayerRngV111.RewardsStreamId,
                "native-boss-relic-offer-shuffle-index"),
            ["reward.lava_rock.relic_offer.shuffle_swap_index"] = new(
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
            ["combat.draw_pile.random_insert_index"] = new(
                NativeFisherYatesV111.StreamId,
                "native-draw-pile-random-insertion-index"),
            ["combat.relic.after_shuffle.generated_card.random_insert_index"] = new(
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
        LegacyCallerBoundIndexQuerySites.Keys
            .Order(StringComparer.Ordinal).ToArray();

    public static bool IsLegacyCallerBoundIndexQuerySite(string siteId) =>
        LegacyCallerBoundIndexQuerySites.ContainsKey(siteId);

    public static string[] NativeMapGaussianIntQuerySiteIds =>
        (string[])NativeMapGaussianIntQuerySites.Clone();

    public static bool IsNativeMapGaussianIntQuerySite(string siteId) =>
        NativeMapGaussianIntQuerySites.Contains(siteId,
            StringComparer.Ordinal);

    public static bool MatchesNativeMapGaussianIntRequest(
        RandomGaussianIntRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.Ordinal != 0)
            return false;
        return request.SiteId switch
        {
            PrototypeNativeOvergrowthMap.RestCountGaussianSiteId =>
                request.StreamId == "context.act_1_map" &&
                request.OwnerId == "act:1:rest_count" &&
                request.Mean == 7 && request.StandardDeviation == 1 &&
                request.MinInclusive == 6 && request.MaxInclusive == 7,
            PrototypeNativeOvergrowthMap.UnknownCountGaussianSiteId =>
                request.StreamId == "context.act_1_map" &&
                request.OwnerId == "act:1:unknown_count" &&
                request.Mean == 12 && request.StandardDeviation == 1 &&
                request.MinInclusive == 10 && request.MaxInclusive == 14,
            PrototypeNativeLaterActMap.HiveRestCountGaussianSiteId =>
                request.StreamId == "context.act_2_map" &&
                request.OwnerId == "map:act:2" &&
                request.Mean == 6 && request.StandardDeviation == 1 &&
                request.MinInclusive == 6 && request.MaxInclusive == 7,
            PrototypeNativeLaterActMap.UnknownCountGaussianSiteId =>
                (request.OwnerId is "map:act:2" or "map:act:3") &&
                request.StreamId == (request.OwnerId == "map:act:2"
                    ? "context.act_2_map" : "context.act_3_map") &&
                request.Mean == 12 && request.StandardDeviation == 1 &&
                request.MinInclusive == 10 && request.MaxInclusive == 14,
            _ => false
        };
    }

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

        const string siteId = "potion.rarity_roll.ticket";
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
                "rarity:" + item.Rarity.ToStr…58922 tokens truncated…rrentSchemaId || Origin is null || Action is null ||
            Run is null || InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null || string.IsNullOrWhiteSpace(Action.Kind) ||
            (Run.ControlledRunActionChoice is null &&
             Run.ControlledRunActionHistory is not { Length: > 0 } &&
             !immediateShopAction && !immediateActOneCombatAction))
            throw new InvalidDataException(
                "Invalid ReferenceNature run-action session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        var actionMapNodeId = Action.Kind == "choose_map_node"
            ? Action.ReadPayload<ChooseMapNodePayload>().NodeId
            : null;
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
                continuation.MapNodeId != actionMapNodeId ||
                (ControlledRunActionEngineV111.IsShopPurchaseAction(
                     Origin, Action)
                    ? continuation.RunAction is null ||
                      PrototypeAiEnvironment.StableActionId(
                          continuation.RunAction) !=
                      PrototypeAiEnvironment.StableActionId(Action)
                    : continuation.RunAction is not null) ||
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
            if (immediateShopAction || immediateActOneCombatAction)
            {
                var replayedRun = ControlledRunActionEngineV111.Begin(
                    Origin.Fork(), CloneAction(Action));
                replayedRun = ReferenceNatureRunCursorV111.Commit(
                    replayedRun, InitialNatureState);
                if (CanonicalJson.Sha256(replayedRun) !=
                    CanonicalJson.Sha256(Run))
                    throw new InvalidDataException(
                        "Deterministic ReferenceNature run action differs from its replay.");
                return;
            }

            var trace = Run.ControlledRunActionHistory![^1];
            trace.Validate();
            if (trace.ActionId != Action.Kind ||
                trace.MapNodeId != actionMapNodeId ||
                (ControlledRunActionEngineV111.IsShopPurchaseAction(
                     Origin, Action)
                    ? trace.ActionFingerprint !=
                      PrototypeAiEnvironment.StableActionId(Action)
                    : trace.ActionFingerprint is not null) ||
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

public sealed record ReferenceNatureAncientOfferSessionSnapshotV111(
    string SchemaId, string SiteId, string OriginSnapshotJson,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript);

/// <summary>
/// Atomic ReferenceNature envelope for Ancient offer generation. Individual
/// decisions remain private to the session until every source event-offer or
/// Fisher-Yates request has been sampled and replayed through its generator.
/// </summary>
public sealed record ReferenceNatureAncientOfferSessionV111(
    string SchemaId, string SiteId, RunState Origin, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-ancient-offer-session-v1";

    private const string EventOfferOutcomePrefix = "event_choice:";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length ==
        ChanceLawEngineV111.AncientOfferDecisionCount(Origin, SiteId);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryAncientOfferSessionStep(
            Origin, SiteId, DecisionTranscript);

    public static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId, ulong seed) =>
        Begin(source, siteId, (ulong?)seed);

    public static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId) =>
        Begin(source, siteId, (ulong?)null);

    private static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId, ulong? seed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        return Begin(source, siteId, ReferenceNatureRunCursorV111.Begin(
            source, seed, "Ancient-offer"));
    }

    private static ReferenceNatureAncientOfferSessionV111 Begin(
        RunState source, string siteId,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateOpeningBoundary(source, siteId);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Ancient-offer");
        var session = new ReferenceNatureAncientOfferSessionV111(
            CurrentSchemaId, siteId, source.Fork(), source.Fork(),
            initialNatureState.Fork(), initialNatureState.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureAncientOfferSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            DecisionTranscript = (ReferenceNatureDecisionV111[])
                DecisionTranscript.Clone()
        };
    }

    public ReferenceNatureAncientOfferSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Ancient-offer session is already complete.");

        var law = ChanceLawEngineV111.QueryAncientOfferSessionStep(
            Origin, SiteId, DecisionTranscript);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNatureState = selection.NextState;
        var nextRun = Origin.Fork();
        if (transcript.Length ==
            ChanceLawEngineV111.AncientOfferDecisionCount(Origin, SiteId))
        {
            nextRun = GenerateEvent(Origin, SiteId, transcript);
            nextRun = ReferenceNatureRunCursorV111.Commit(
                nextRun, nextNatureState);
        }
        var next = this with
        {
            Run = nextRun,
            NatureState = nextNatureState,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            !IsSupportedSite(SiteId) || Origin is null || Run is null ||
            InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Ancient-offer session envelope.");
        ValidateOpeningBoundary(Origin, SiteId);
        if (DecisionTranscript.Length >
            ChanceLawEngineV111.AncientOfferDecisionCount(Origin, SiteId))
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer transcript exceeds its source draw count.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer cursor differs from the run envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        for (var ordinal = 0; ordinal < DecisionTranscript.Length; ordinal++)
        {
            var decision = DecisionTranscript[ordinal];
            decision.Validate();
            var law = ChanceLawEngineV111.QueryAncientOfferSessionStep(
                Origin, SiteId, DecisionTranscript.Take(ordinal).ToArray());
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) != CanonicalJson.Sha256(decision))
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer transcript does not replay.");
            expectedNatureState = selection.NextState;
        }

        var expectedRun = Complete
            ? ReferenceNatureRunCursorV111.Commit(
                GenerateEvent(Origin, SiteId, DecisionTranscript),
                expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer replay differs from its session envelope.");
    }

    public static string Save(ReferenceNatureAncientOfferSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureAncientOfferSessionSnapshotV111(
            CurrentSchemaId, session.SiteId, RunSnapshot.Save(session.Origin),
            RunSnapshot.Save(session.Run), session.InitialNatureState,
            session.NatureState,
            (ReferenceNatureDecisionV111[])session.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureAncientOfferSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureAncientOfferSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Ancient-offer snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            !IsSupportedSite(snapshot.SiteId) ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Ancient-offer snapshot.");

        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.SiteId,
            snapshot.InitialNatureState);
        for (var index = 0; index < snapshot.DecisionTranscript.Length; index++)
        {
            if (replay.Complete)
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer replay exceeded its source draw count.");
            replay = replay.ResolveNext();
            if (CanonicalJson.Sha256(replay.DecisionTranscript[^1]) !=
                CanonicalJson.Sha256(snapshot.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer replay selected a different outcome.");
        }
        if (CanonicalJson.Sha256(replay.Run) != CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            replay.DecisionTranscript.Length !=
                snapshot.DecisionTranscript.Length)
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    private static RunState GenerateEvent(
        RunState origin, string siteId,
        ReferenceNatureDecisionV111[] transcript)
    {
        var scripted = BuildReplayChoices(origin, siteId, transcript);
        var decisions = scripted.Select(choice =>
        {
            var decision = new RandomIndexDecisionV111(
                choice.Request with
                {
                    OrderedCandidates = (int[])
                        choice.Request.OrderedCandidates.Clone()
                }, choice.SelectedIndex, "controlled-v111");
            decision.Validate();
            return ControlledStochasticDecisionV111.FromIndex(decision);
        }).ToArray();
        // Session validation replays the generator repeatedly; keep those
        // audit-only calls out of any enclosing source-boundary journal.
        using var replayJournal = RandomBoundaryJournalV111.Begin();
        var replay = ControlledChanceContinuationExecutorV111.CaptureStochastic(
            origin, ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions, checkpoint => GenerateEventMechanics(checkpoint, siteId));
        if (replay.Run is not { } run || replay.Continuation is not null)
            throw new InvalidOperationException(
                "Ancient offer generator did not consume the complete Nature transcript.");
        return run;
    }

    private static AncientOfferReplayChoiceV111[] BuildReplayChoices(
        RunState origin, string siteId,
        ReferenceNatureDecisionV111[] transcript)
    {
        if (siteId == NativeDarvEventV111.OfferVariantSiteId)
            return BuildDarvReplayChoices(origin, transcript);
        var isShuffle = ChanceLawEngineV111.IsShuffleAncientOfferSite(siteId);
        var expectedCount = ChanceLawEngineV111.AncientOfferDecisionCount(
            origin, siteId);
        if (transcript.Length != expectedCount)
            throw new InvalidOperationException(
                "Ancient offer generation requires its complete sampled transcript.");
        AncientOfferReplayChoiceV111[] scripted;
        if (isShuffle)
        {
            var pools = ChanceLawEngineV111.AncientOfferShuffleChoicePools(
                origin, siteId);
            var requests = ChanceLawEngineV111.AncientOfferShuffleRequests(
                siteId, pools);
            scripted = transcript.Select((decision, ordinal) =>
            {
                var expectedOutcome = "swap_index:" +
                    decision.OutcomeIndex.ToString(CultureInfo.InvariantCulture);
                if (decision.OutcomeId != expectedOutcome ||
                    decision.OutcomeIndex < 0 ||
                    decision.OutcomeIndex >=
                        requests[ordinal].Request.SupportCount)
                    throw new InvalidDataException(
                        "Sampled Ancient offer shuffle index is outside its source support.");
                return new AncientOfferReplayChoiceV111(
                    requests[ordinal].Request, decision.OutcomeIndex);
            }).ToArray();
        }
        else
        {
            var candidates = Enumerable.Range(0, 3).Select(ordinal =>
                CandidateChoiceIds(origin, siteId, ordinal)).ToArray();
            scripted = transcript.Select((decision, ordinal) =>
            {
                var selectedIndex = Array.FindIndex(candidates[ordinal], choiceId =>
                    EventOfferOutcomePrefix + choiceId == decision.OutcomeId);
                if (selectedIndex < 0)
                    throw new InvalidDataException(
                        "Sampled Ancient offer is outside its source candidate pool.");
                var request = new RandomIndexRequestV111(
                    siteId, StreamId(siteId), OwnerId(siteId), ordinal,
                    Enumerable.Range(0, candidates[ordinal].Length).ToArray());
                return new AncientOfferReplayChoiceV111(request, selectedIndex);
            }).ToArray();
        }
        return scripted;
    }

    private static RunState GenerateEventMechanics(
        RunState origin, string siteId)
    {
        var provider = new AncientOfferScopedIndexProviderV111();
        return siteId switch
        {
            NativeOrobasEventV111.OfferSiteId =>
                NativeOrobasEventV111.BeginOpeningWithAudit(origin, provider).State,
            NativePaelRewardsV111.OfferSiteId =>
                NativePaelRewardsV111.BeginPaelOpeningWithAudit(origin, provider).State,
            NativeAncientRewardsV111.TezcataraOfferSiteId =>
                NativeAncientRewardsV111.BeginTezcataraOpeningWithAudit(
                    origin, provider).State,
            NativeNonupeipeEventV111.ShuffleSiteId =>
                NativeNonupeipeEventV111.BeginOpeningWithAudit(
                    origin, provider).State,
            NativeTanxEventV111.ShuffleSiteId =>
                NativeTanxEventV111.BeginOpeningWithAudit(origin, provider).State,
            NativeVakuuEventV111.ShuffleSiteId =>
                NativeVakuuEventV111.BeginOpeningWithAudit(origin, provider).State,
            NativeDarvEventV111.OfferVariantSiteId =>
                NativeDarvEventV111.BeginOpeningWithAudit(origin, provider).State,
            _ => throw new NotSupportedException(
                $"ReferenceNature Ancient-offer session does not support '{siteId}'.")
        };
    }

    private static AncientOfferReplayChoiceV111[] BuildDarvReplayChoices(
        RunState origin, ReferenceNatureDecisionV111[] transcript)
    {
        var shuffleRequests = ChanceLawEngineV111
            .DarvOfferShuffleRequests(origin);
        if (transcript.Length != shuffleRequests.Length + 1)
            throw new InvalidOperationException(
                "Darv generation requires the offer variant and complete shuffle prefix.");
        var variant = transcript[0];
        var expectedVariant = variant.OutcomeIndex switch
        {
            0 => "offer_variant:dusty_tome",
            1 => "offer_variant:three_relics",
            _ => null
        };
        if (variant.LawId is null ||
            variant.OutcomeId != expectedVariant)
            throw new InvalidDataException(
                "Sampled Darv offer variant is outside its binary support.");

        var scripted = new List<AncientOfferReplayChoiceV111>(
            shuffleRequests.Length + 1)
        {
            new(ChanceLawEngineV111.DarvOfferVariantRequest(origin),
                variant.OutcomeIndex)
        };
        for (var index = 0; index < shuffleRequests.Length; index++)
        {
            var decision = transcript[index + 1];
            var expectedOutcome = "swap_index:" +
                decision.OutcomeIndex.ToString(CultureInfo.InvariantCulture);
            if (decision.OutcomeId != expectedOutcome ||
                decision.OutcomeIndex < 0 ||
                decision.OutcomeIndex >= shuffleRequests[index].SupportCount)
                throw new InvalidDataException(
                    "Sampled Darv shuffle index is outside its source support.");
            scripted.Add(new AncientOfferReplayChoiceV111(
                shuffleRequests[index], decision.OutcomeIndex));
        }
        return scripted.ToArray();
    }

    private static string[] CandidateChoiceIds(
        RunState run, string siteId, int ordinal)
    {
        if (siteId == NativeOrobasEventV111.OfferSiteId)
            return NativeOrobasEventV111.OfferCandidateIds(run.Player, ordinal);
        if (siteId == NativePaelRewardsV111.OfferSiteId)
            return NativePaelRewardsV111.OfferPools(run.Player)[ordinal]
                .Select(id => "take_" + id).ToArray();
        if (siteId == NativeAncientRewardsV111.TezcataraOfferSiteId)
            return NativeAncientRewardsV111.TezcataraPools(run.Player)[ordinal]
                .Select(id => "take_" + id).ToArray();
        throw new NotSupportedException(
            $"ReferenceNature Ancient-offer session does not support '{siteId}'.");
    }

    private static void ValidateOpeningBoundary(RunState run, string siteId)
    {
        ChanceLawEngineV111.ValidateAncientOfferOpeningBoundary(run, siteId);
    }

    private static bool IsSupportedSite(string siteId) =>
        ChanceLawEngineV111.IsSupportedAncientOfferSessionSite(siteId);

    private static string StreamId(string siteId) => siteId switch
    {
        NativeOrobasEventV111.OfferSiteId => "context.event.OROBAS.player_0",
        NativePaelRewardsV111.OfferSiteId => "context.event.PAEL.player_0",
        NativeAncientRewardsV111.TezcataraOfferSiteId =>
            "context.event.TEZCATARA.player_0",
        _ => throw new NotSupportedException(
            $"No event RNG stream is registered for '{siteId}'.")
    };

    private static string OwnerId(string siteId) => siteId switch
    {
        NativeOrobasEventV111.OfferSiteId => "event:OROBAS:act:2:floor:0",
        NativePaelRewardsV111.OfferSiteId => "event:PAEL:act:2:floor:0",
        NativeAncientRewardsV111.TezcataraOfferSiteId =>
            "event:TEZCATARA:act:2:floor:0",
        _ => throw new NotSupportedException(
            $"No event owner is registered for '{siteId}'.")
    };

    private sealed record AncientOfferReplayChoiceV111(
        RandomIndexRequestV111 Request, int SelectedIndex);

    /// <summary>Routes explicit Ancient-offer providers through the active
    /// shared continuation scope while preserving the event generator's
    /// established controlled-provider marker.</summary>
    private sealed class AncientOfferScopedIndexProviderV111
        : IRandomIndexProviderV111
    {
        public string Mode => "scripted-v111";

        public RandomIndexDecisionV111 Resolve(
            RandomIndexRequestV111 request, RngBundle streams) =>
            StochasticProviderScopeV111.ResolveIndex(this, request, streams);
    }
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
/// post-reward event-choice continuations. Event player actions and Nature
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
        (ControlledEventChoiceEngineV111
            .IsStonePushDeckChoiceBoundary(Run)
        || Run.Phase == RunPhase.Reward
            && Run.World?.Event is { } eventState
            && eventState.ChosenChoiceId == ChoiceId
            && Run.World.Reward is
            {
                AddRandomCurseAfterReward: true,
                SourceRoom: var sourceRoom
            }
            && sourceRoom == "Event:" + eventState.EventId
            && Run.ControlledEventChoiceHistory is
                { Length: > 0 } history
            && history[^1].EventId == eventState.EventId
            && history[^1].ChoiceId == ChoiceId);

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
        var origin = NeedsNatureCallingBellBag(source, choiceId)
            ? ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
                source, createRelicBagWhenMissing: true)
            : source.Fork();
        var run = ControlledEventChoiceEngineV111.Begin(
            origin.Fork(), choiceId);
        var session = new ReferenceNatureControlledEventChoiceSessionV111(
            CurrentSchemaId, origin, choiceId, run,
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

    private static bool NeedsNatureCallingBellBag(
        RunState source, string choiceId)
    {
        if (source.NativeRunRng is null ||
            source.World is not { } world || world.Event is not
                { EventId: NativeDarvEventV111.EventId } eventState ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            !PrototypeContent.Events.TryGetValue(
                eventState.EventId, out var definition))
            return false;
        return definition.Choices.FirstOrDefault(choice =>
                choice.Id == choiceId)?.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.GainRelic &&
                effect.RelicId == NativeDarvEventV111.CallingBellId)
            == true;
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
                "No supported event player action is pending.");
        var engine = new PrototypeGameEngine();
        var actionId = PrototypeAiEnvironment.StableActionId(action);
        if (!engine.GetLegalActions(Run).Any(candidate =>
                StringComparer.Ordinal.Equals(
                    PrototypeAiEnvironment.StableActionId(candidate), actionId)))
            throw new InvalidOperationException(
                "ReferenceNature event advance requires a current legal player action.");
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
        if (NeedsNatureCallingBellBag(Origin, ChoiceId))
        {
            ReferenceNatureRunProjectionV111
                .ValidateNatureRelicBagProjection(
                    Origin, "Native Calling Bell event-choice origin");
            ReferenceNatureRunProjectionV111
                .ValidateNatureRelicBagProjection(
                    Run, "Native Calling Bell event-choice replay");
        }
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
            if (ControlledEventChoiceEngineV111
                    .IsStonePushDeckChoiceBoundary(Run))
            {
                if (decisions.Length != 0 || Steps.Length != 0)
                    throw new InvalidDataException(
                        "Stone of All Time push deck-choice boundary has an unexpected transcript prefix.");
                return;
            }
            if (ControlledEventChoiceEngineV111
                    .IsStonePushNoChanceTerminal(Run, Origin, ChoiceId))
            {
                if (decisions.Length != 0 || Steps.Length != 0)
                    throw new InvalidDataException(
                        "Lethal Stone of All Time push has an unexpected chance transcript.");
                return;
            }
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
