namespace Sts2Emulator.Core;

/// <summary>Versioned list of stable sites served by the direct Chance Law
/// query operation. Query-only sites are distinct from resumable session
/// coverage.</summary>
public sealed record StochasticChanceLawQueryCoverageV111(
    string SchemaId, string QueryOperation, string[] SiteIds)
{
    public const string CurrentSchemaId =
        "sts2-v111-stochastic-chance-law-query-coverage-v1";

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            QueryOperation != "chance_law_query" || SiteIds is null ||
            SiteIds.Any(string.IsNullOrWhiteSpace) ||
            SiteIds.Distinct(StringComparer.Ordinal).Count() != SiteIds.Length)
            throw new InvalidDataException(
                "Invalid stochastic Chance Law query coverage.");
    }
}

/// <summary>
/// Versioned report of which stochastic sites each atomic ReferenceNature
/// session can resolve. This is separate from the union of Nature-choice
/// sites so clients can negotiate the correct continuation.
/// </summary>
public sealed record StochasticSessionCoverageV111(
    string SchemaId,
    string SemanticReferenceNatureSessionSchemaId,
    string[] SemanticReferenceNatureSiteIds,
    string ReferenceNatureRunActionSessionSchemaId,
    string[] ReferenceNatureRunActionSiteIds,
    string ReferenceNatureEventChoiceSessionSchemaId,
    string[] ReferenceNatureEventChoiceSiteIds)
{
    public string ReferenceNatureAncientOfferSessionSchemaId { get; init; } =
        ReferenceNatureAncientOfferSessionV111.CurrentSchemaId;
    public string ReferenceNatureNeowOfferSessionSchemaId { get; init; } =
        ReferenceNatureNeowOfferSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureNeowOfferSiteIds { get; init; } = [];
    public string ReferenceNatureTrashHeapSessionSchemaId { get; init; } =
        ReferenceNatureTrashHeapSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureTrashHeapSiteIds { get; init; } = [];
    public string ReferenceNatureTabletOfTruthSessionSchemaId { get; init; } =
        ReferenceNatureTabletOfTruthSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureTabletOfTruthSiteIds { get; init; } = [];
    public string ReferenceNatureTrialSessionSchemaId { get; init; } =
        ReferenceNatureTrialSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureTrialSiteIds { get; init; } = [];
    public string ReferenceNatureNativeEventShuffleSessionSchemaId { get; init; } =
        ReferenceNatureNativeEventShuffleSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureNativeEventShuffleSiteIds { get; init; } = [];
    public string ReferenceNatureEndlessConveyorSessionSchemaId { get; init; } =
        ReferenceNatureEndlessConveyorSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureEndlessConveyorSiteIds { get; init; } = [];
    public string ReferenceNatureGloryReflectionsSessionSchemaId { get; init; } =
        ReferenceNatureGloryReflectionsSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureGloryReflectionsSiteIds { get; init; } = [];
    public string ReferenceNatureInfestedAutomatonSessionSchemaId { get; init; } =
        ReferenceNatureInfestedAutomatonSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureInfestedAutomatonSiteIds { get; init; } = [];
    public string ReferenceNaturePunchOffRewardSessionSchemaId { get; init; } =
        ReferenceNaturePunchOffRewardSessionV111.CurrentSchemaId;
    public string[] ReferenceNaturePunchOffRewardSiteIds { get; init; } = [];
    public string ReferenceNaturePunchOffFightSessionSchemaId { get; init; } =
        ReferenceNaturePunchOffFightSessionV111.CurrentSchemaId;
    public string[] ReferenceNaturePunchOffFightSiteIds { get; init; } = [];
    public string[] ReferenceNatureAncientOfferSiteIds { get; init; } = [];
    public string ReferenceNatureMerchantSessionSchemaId { get; init; } =
        ReferenceNatureMerchantSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureMerchantSiteIds { get; init; } = [];
    public string ReferenceNatureTreasureSessionSchemaId { get; init; } =
        ReferenceNatureTreasureSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureTreasureSiteIds { get; init; } = [];

    public const string CurrentSchemaId =
        "sts2-v111-stochastic-session-coverage-v1";

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            SemanticReferenceNatureSessionSchemaId !=
                ReferenceNatureSessionV111.CurrentSchemaId ||
            ReferenceNatureRunActionSessionSchemaId !=
                ReferenceNatureRunActionSessionV111.CurrentSchemaId ||
            ReferenceNatureEventChoiceSessionSchemaId !=
                ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId ||
            ReferenceNatureAncientOfferSessionSchemaId !=
                ReferenceNatureAncientOfferSessionV111.CurrentSchemaId ||
            ReferenceNatureNeowOfferSessionSchemaId !=
                ReferenceNatureNeowOfferSessionV111.CurrentSchemaId ||
            ReferenceNatureTrashHeapSessionSchemaId !=
                ReferenceNatureTrashHeapSessionV111.CurrentSchemaId ||
            ReferenceNatureTabletOfTruthSessionSchemaId !=
                ReferenceNatureTabletOfTruthSessionV111.CurrentSchemaId ||
            ReferenceNatureTrialSessionSchemaId !=
                ReferenceNatureTrialSessionV111.CurrentSchemaId ||
            ReferenceNatureNativeEventShuffleSessionSchemaId !=
                ReferenceNatureNativeEventShuffleSessionV111.CurrentSchemaId ||
            ReferenceNatureEndlessConveyorSessionSchemaId !=
                ReferenceNatureEndlessConveyorSessionV111.CurrentSchemaId ||
            ReferenceNatureGloryReflectionsSessionSchemaId !=
                ReferenceNatureGloryReflectionsSessionV111.CurrentSchemaId ||
            ReferenceNatureInfestedAutomatonSessionSchemaId !=
                ReferenceNatureInfestedAutomatonSessionV111.CurrentSchemaId ||
            ReferenceNaturePunchOffRewardSessionSchemaId !=
                ReferenceNaturePunchOffRewardSessionV111.CurrentSchemaId ||
            ReferenceNaturePunchOffFightSessionSchemaId !=
                ReferenceNaturePunchOffFightSessionV111.CurrentSchemaId ||
            ReferenceNatureMerchantSessionSchemaId !=
                ReferenceNatureMerchantSessionV111.CurrentSchemaId ||
            ReferenceNatureTreasureSessionSchemaId !=
                ReferenceNatureTreasureSessionV111.CurrentSchemaId)
            throw new InvalidDataException(
                "Invalid stochastic session coverage schema.");

        ValidateSiteIds(SemanticReferenceNatureSiteIds);
        ValidateSiteIds(ReferenceNatureRunActionSiteIds);
        ValidateSiteIds(ReferenceNatureEventChoiceSiteIds);
        ValidateSiteIds(ReferenceNatureAncientOfferSiteIds);
        ValidateSiteIds(ReferenceNatureNeowOfferSiteIds);
        ValidateSiteIds(ReferenceNatureTrashHeapSiteIds);
        ValidateSiteIds(ReferenceNatureTabletOfTruthSiteIds);
        ValidateSiteIds(ReferenceNatureTrialSiteIds);
        ValidateSiteIds(ReferenceNatureNativeEventShuffleSiteIds);
        ValidateSiteIds(ReferenceNatureEndlessConveyorSiteIds);
        ValidateSiteIds(ReferenceNatureGloryReflectionsSiteIds);
        ValidateSiteIds(ReferenceNatureInfestedAutomatonSiteIds);
        ValidateSiteIds(ReferenceNaturePunchOffRewardSiteIds);
        ValidateSiteIds(ReferenceNaturePunchOffFightSiteIds);
        ValidateSiteIds(ReferenceNatureMerchantSiteIds);
        ValidateSiteIds(ReferenceNatureTreasureSiteIds);
    }

    private static void ValidateSiteIds(string[] siteIds)
    {
        if (siteIds is null || siteIds.Any(string.IsNullOrWhiteSpace) ||
            siteIds.Distinct(StringComparer.Ordinal).Count() != siteIds.Length)
            throw new InvalidDataException(
                "Stochastic session coverage contains invalid site identifiers.");
    }
}

/// <summary>
/// Authoritative stable site lists shared by capability negotiation and the
/// controlled replay allowlists. Session coverage remains separate from the
/// union of sites that can produce Nature-choice payloads.
/// </summary>
public static class StochasticSiteRegistryV111
{
    public const string UnownedRandomRelicSelectionSiteId =
        "relic.unowned_random_selection.index_draw";

    private static readonly string[] SemanticReferenceNatureSites =
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
        "combat.draw_pile.reveal_card",
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
    ];

    private static readonly string[] RunActionSites =
    [
        "run.encounter.overgrowth_boss.index_draw",
        "map.legacy.floor_width.index_draw",
        "map.legacy.edge_source.index_draw",
        "map.legacy.orphan_target.index_draw",
        "map.legacy.edge_extra_chance.ticket",
        "map.legacy.second_edge_source.index_draw",
        "map.legacy.convergence.order_key",
        "map.legacy.convergence.source.index_draw",
        "map.legacy.second_edge_target.index_draw",
        "map.legacy.required_room.node.index_draw",
        "map.legacy.room_assignment.index_draw"
    ];

    private static readonly string[] RunActionEventEntrySites =
    [
        "event.selection.weighted_ticket",
        "event.generated_gold.index_draw",
        "event.stone_of_all_time.potion_slot.index_draw",
        "event.fake_merchant.relic_offer.shuffle_swap_index",
        "event.endless_conveyor.dish_weighted_ticket",
        "event.future_of_potions.card_type.index_draw"
    ];

    private static readonly string[] RunActionShopSites =
    [
        "shop.card_rarity.ticket",
        "shop.card_model.index_draw",
        "shop.card.price.index_draw",
        "shop.potion_offer.shuffle_swap_index",
        "shop.potion_offer.price.index_draw",
        "shop.relic_offer.shuffle_swap_index",
        "shop.relic_offer.price.index_draw"
    ];

    private static readonly string[] RunActionTreasureSites =
    [
        "treasure.relic_model.index_draw"
    ];

    private static readonly string[] RunActionShovelSites =
    [
        UnownedRandomRelicSelectionSiteId
    ];

    private static readonly string[] ControlledEventChoiceSites =
    [
        "event.random_upgrade.card_instance",
        "event.reward.random_rare_card.model",
        "event.reward.random_potion.model",
        "event.reward.offer_potion.model.index_draw",
        "event.reward.card_bundle.shuffle_swap_index",
        "relic_grab_bag.queue_shuffle.swap_index",
        "relic_grab_bag.rarity.ticket",
        "event.potion.fill_empty_slots.model",
        "reward.card_rarity.ticket",
        "reward.card_model.index_draw",
        "run.random_pool_distinct.shuffle_swap_index",
        "event.reward.random_modifier_curse.index_draw",
        "event.fake_merchant.relic_offer.shuffle_swap_index",
        NativeGloryAncientsV111.BeautifulBraceletShuffleSiteId,
        NativeGloryAncientsV111.DistinguishedCapeCurseSiteId,
        NativeGloryAncientsV111.FurCoatShuffleSiteId,
        "relic.sand_castle.acquisition_upgrade.shuffle_swap_index",
        "potion.uncommon_model.index_draw",
        "relic.acquisition.random_rare_card.model",
        "relic.acquisition.random_potion.model",
        "run.potion.fill_empty_slots.model",
        "reward.card_transform.replacement.index_draw",
        UnownedRandomRelicSelectionSiteId
    ];

    private static readonly string[] ChanceLawQuerySites =
    [
        NativeOrobasEventV111.OfferSiteId,
        NativePaelRewardsV111.OfferSiteId,
        NativeAncientRewardsV111.TezcataraOfferSiteId,
        NativeNonupeipeEventV111.ShuffleSiteId,
        NativeTanxEventV111.ShuffleSiteId,
        NativeVakuuEventV111.ShuffleSiteId,
        NativeDarvEventV111.OfferVariantSiteId,
        NativeDarvEventV111.RelicShuffleSiteId,
        .. ChanceLawEngineV111.NeowOfferSessionSiteIds,
        .. ChanceLawEngineV111.TrashHeapSessionSiteIds,
        .. ChanceLawEngineV111.TabletOfTruthSessionSiteIds,
        .. ChanceLawEngineV111.TrialSessionSiteIds,
        .. ChanceLawEngineV111.NativeEventShuffleSessionSiteIds,
        .. ChanceLawEngineV111.NativeEventEntryIndexQuerySiteIds,
        .. ChanceLawEngineV111.NativeEventGoldQuerySiteIds,
        .. ChanceLawEngineV111.NativeEventNoEffectIndexQuerySiteIds,
        .. ChanceLawEngineV111.NativeEventCardPickQuerySiteIds,
        .. ChanceLawEngineV111.NativePunchOffRewardQuerySiteIds,
        .. ChanceLawEngineV111.NativePunchOffFightQuerySiteIds,
        "event.endless_conveyor.dish_weighted_ticket",
        "event.endless_conveyor.random_upgrade.card_instance",
        "event.endless_conveyor.colorless_card.index_draw",
        "shop.merchant.sale_slot.index_draw",
        "card_rarity.roll.unit_float",
        "shop.merchant.card_model.index_draw",
        "shop.colorless_card_model.index_draw",
        "potion_factory.rarity_roll.unit_float",
        "potion_factory.model_within_rarity.index_draw",
        "relic_grab_bag.rarity.unit_float",
        PrototypeNativeRelicGrabBag.NativeMerchantModelSiteId,
        "shop.merchant.card_upgrade_roll.unit_float",
        "treasure.relic_grab_bag.draw",
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId,
        "shop.merchant.card_price_modifier.unit_float",
        "shop.merchant.potion_price_modifier.unit_float",
        "shop.merchant.relic_price_modifier.unit_float"
    ];

    private static readonly string[] NativeMerchantPriceSites =
    [
        "shop.merchant.card_price_modifier.unit_float",
        "shop.merchant.potion_price_modifier.unit_float",
        "shop.merchant.relic_price_modifier.unit_float"
    ];

    private static readonly string[] NativeMerchantSessionSites =
    [
        "shop.merchant.sale_slot.index_draw",
        "card_rarity.roll.unit_float",
        "shop.merchant.card_model.index_draw",
        "shop.merchant.card_upgrade_roll.unit_float",
        "shop.colorless_card_model.index_draw",
        "potion_factory.rarity_roll.unit_float",
        "potion_factory.model_within_rarity.index_draw",
        "relic_grab_bag.rarity.unit_float",
        PrototypeNativeRelicGrabBag.NativeMerchantModelSiteId,
        "shop.merchant.card_price_modifier.unit_float",
        "shop.merchant.potion_price_modifier.unit_float",
        "shop.merchant.relic_price_modifier.unit_float"
    ];

    private static readonly string[] NativeTreasureSessionSites =
    [
        "relic_grab_bag.rarity.unit_float",
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId
    ];

    private static readonly string[] AncientOfferSessionSites =
    [
        NativeOrobasEventV111.OfferSiteId,
        NativePaelRewardsV111.OfferSiteId,
        NativeAncientRewardsV111.TezcataraOfferSiteId,
        NativeNonupeipeEventV111.ShuffleSiteId,
        NativeTanxEventV111.ShuffleSiteId,
        NativeVakuuEventV111.ShuffleSiteId,
        NativeDarvEventV111.OfferVariantSiteId
    ];

    private static readonly string[] NeowOfferSessionSites =
        ChanceLawEngineV111.NeowOfferSessionSiteIds;
    private static readonly string[] TrashHeapSessionSites =
        ChanceLawEngineV111.TrashHeapSessionSiteIds;
    private static readonly string[] TabletOfTruthSessionSites =
        ChanceLawEngineV111.TabletOfTruthSessionSiteIds;
    private static readonly string[] TrialSessionSites =
        ChanceLawEngineV111.TrialSessionSiteIds;
    private static readonly string[] NativeEventShuffleSessionSites =
        ChanceLawEngineV111.NativeEventShuffleSessionSiteIds;
    private static readonly string[] EndlessConveyorSessionSites =
        ReferenceNatureEndlessConveyorSessionV111.SessionSiteIds;
    private static readonly string[] GloryReflectionsSessionSites =
        ReferenceNatureGloryReflectionsSessionV111.SessionSiteIds;
    private static readonly string[] InfestedAutomatonSessionSites =
        ReferenceNatureInfestedAutomatonSessionV111.SessionSiteIds;
    private static readonly string[] PunchOffRewardSessionSites =
        ReferenceNaturePunchOffRewardSessionV111.SessionSiteIds;
    private static readonly string[] PunchOffFightSessionSites =
        ReferenceNaturePunchOffFightSessionV111.SessionSiteIds;

    private static readonly HashSet<string> SemanticReferenceNatureSiteSet =
        new(SemanticReferenceNatureSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionSiteSet =
        new(RunActionSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionEventEntrySiteSet =
        new(RunActionEventEntrySites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionShopSiteSet =
        new(RunActionShopSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionTreasureSiteSet =
        new(RunActionTreasureSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionShovelSiteSet =
        new(RunActionShovelSites, StringComparer.Ordinal);
    private static readonly HashSet<string> ControlledEventChoiceSiteSet =
        new(ControlledEventChoiceSites, StringComparer.Ordinal);
    private static readonly string[] RunActionSessionSites = RunActionSites
        .Concat(RunActionEventEntrySites)
        .Concat(RunActionShopSites)
        .Concat(RunActionTreasureSites)
        .Concat(RunActionShovelSites)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    private static readonly string[] NatureChoiceSites =
        SemanticReferenceNatureSites
            .Concat(RunActionSites)
            .Concat(RunActionEventEntrySites)
            .Concat(RunActionShopSites)
            .Concat(RunActionTreasureSites)
            .Concat(ControlledEventChoiceSites)
            .Concat(NativeMerchantSessionSites)
            .Concat(NativeTreasureSessionSites)
            .Concat(NeowOfferSessionSites)
            .Concat(TrashHeapSessionSites)
            .Concat(TabletOfTruthSessionSites)
            .Concat(TrialSessionSites)
            .Concat(NativeEventShuffleSessionSites)
            .Concat(EndlessConveyorSessionSites)
            .Concat(GloryReflectionsSessionSites)
            .Concat(InfestedAutomatonSessionSites)
            .Concat(PunchOffRewardSessionSites)
            .Concat(PunchOffFightSessionSites)
            .Concat(ChanceLawEngineV111.NativeEventCardPickQuerySiteIds)
            .Concat(ChanceLawEngineV111.NativePunchOffRewardQuerySiteIds)
            .Concat(ChanceLawEngineV111.NativePunchOffFightQuerySiteIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static string[] SemanticReferenceNatureSiteIds =>
        (string[])SemanticReferenceNatureSites.Clone();
    public static string[] RunActionSiteIds => (string[])RunActionSites.Clone();
    public static string[] RunActionEventEntrySiteIds =>
        (string[])RunActionEventEntrySites.Clone();
    public static string[] RunActionShopSiteIds =>
        (string[])RunActionShopSites.Clone();
    public static string[] RunActionTreasureSiteIds =>
        (string[])RunActionTreasureSites.Clone();
    public static string[] RunActionShovelSiteIds =>
        (string[])RunActionShovelSites.Clone();
    public static string[] ControlledRunActionSiteIds =>
        (string[])RunActionSessionSites.Clone();
    public static string[] ControlledEventChoiceSiteIds =>
        (string[])ControlledEventChoiceSites.Clone();
    public static string[] ChanceLawQuerySiteIds =>
        (string[])ChanceLawQuerySites.Clone();
    public static string[] NativeMerchantPriceSiteIds =>
        (string[])NativeMerchantPriceSites.Clone();
    public static string[] AncientOfferSessionSiteIds =>
        (string[])AncientOfferSessionSites.Clone();
    public static string[] NativeMerchantSessionSiteIds =>
        (string[])NativeMerchantSessionSites.Clone();
    public static string[] NativeTreasureSessionSiteIds =>
        (string[])NativeTreasureSessionSites.Clone();
    public static string[] NeowOfferSessionSiteIds =>
        (string[])NeowOfferSessionSites.Clone();
    public static string[] TrashHeapSessionSiteIds =>
        (string[])TrashHeapSessionSites.Clone();
    public static string[] TabletOfTruthSessionSiteIds =>
        (string[])TabletOfTruthSessionSites.Clone();
    public static string[] TrialSessionSiteIds =>
        (string[])TrialSessionSites.Clone();
    public static string[] NativeEventShuffleSessionSiteIds =>
        (string[])NativeEventShuffleSessionSites.Clone();
    public static string[] EndlessConveyorSessionSiteIds =>
        (string[])EndlessConveyorSessionSites.Clone();
    public static string[] NatureChoiceSiteIds =>
        (string[])NatureChoiceSites.Clone();

    public static bool IsSemanticReferenceNatureSite(string siteId) =>
        SemanticReferenceNatureSiteSet.Contains(siteId);

    public static bool IsRunActionSite(string siteId) =>
        RunActionSiteSet.Contains(siteId);

    public static bool IsRunActionEventEntrySite(string siteId) =>
        RunActionEventEntrySiteSet.Contains(siteId);

    public static bool IsRunActionShopSite(string siteId) =>
        RunActionShopSiteSet.Contains(siteId);

    public static bool IsRunActionTreasureSite(string siteId) =>
        RunActionTreasureSiteSet.Contains(siteId);

    public static bool IsRunActionShovelSite(string siteId) =>
        RunActionShovelSiteSet.Contains(siteId);

    public static bool IsControlledEventChoiceSite(string siteId) =>
        ControlledEventChoiceSiteSet.Contains(siteId);

    public static bool IsChanceLawQuerySite(string siteId) =>
        ChanceLawQuerySites.Contains(siteId, StringComparer.Ordinal);

    public static bool IsNativeMerchantPriceSite(string siteId) =>
        NativeMerchantPriceSites.Contains(siteId, StringComparer.Ordinal);

    public static StochasticChanceLawQueryCoverageV111
        CreateChanceLawQueryCoverage()
    {
        var coverage = new StochasticChanceLawQueryCoverageV111(
            StochasticChanceLawQueryCoverageV111.CurrentSchemaId,
            "chance_law_query", ChanceLawQuerySiteIds);
        coverage.Validate();
        return coverage;
    }

    public static StochasticSessionCoverageV111 CreateSessionCoverage()
    {
        var coverage = new StochasticSessionCoverageV111(
            StochasticSessionCoverageV111.CurrentSchemaId,
            ReferenceNatureSessionV111.CurrentSchemaId,
            SemanticReferenceNatureSiteIds,
            ReferenceNatureRunActionSessionV111.CurrentSchemaId,
            ControlledRunActionSiteIds,
            ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId,
            ControlledEventChoiceSiteIds)
        {
            ReferenceNatureAncientOfferSessionSchemaId =
                ReferenceNatureAncientOfferSessionV111.CurrentSchemaId,
            ReferenceNatureAncientOfferSiteIds = AncientOfferSessionSites,
            ReferenceNatureMerchantSessionSchemaId =
                ReferenceNatureMerchantSessionV111.CurrentSchemaId,
            ReferenceNatureMerchantSiteIds = NativeMerchantSessionSites,
            ReferenceNatureTreasureSessionSchemaId =
                ReferenceNatureTreasureSessionV111.CurrentSchemaId,
            ReferenceNatureTreasureSiteIds = NativeTreasureSessionSites,
            ReferenceNatureNeowOfferSessionSchemaId =
                ReferenceNatureNeowOfferSessionV111.CurrentSchemaId,
            ReferenceNatureNeowOfferSiteIds = NeowOfferSessionSites,
            ReferenceNatureTrashHeapSessionSchemaId =
                ReferenceNatureTrashHeapSessionV111.CurrentSchemaId,
            ReferenceNatureTrashHeapSiteIds = TrashHeapSessionSites,
            ReferenceNatureTabletOfTruthSessionSchemaId =
                ReferenceNatureTabletOfTruthSessionV111.CurrentSchemaId,
            ReferenceNatureTabletOfTruthSiteIds = TabletOfTruthSessionSites,
            ReferenceNatureTrialSessionSchemaId =
                ReferenceNatureTrialSessionV111.CurrentSchemaId,
            ReferenceNatureTrialSiteIds = TrialSessionSites,
            ReferenceNatureNativeEventShuffleSessionSchemaId =
                ReferenceNatureNativeEventShuffleSessionV111.CurrentSchemaId,
            ReferenceNatureNativeEventShuffleSiteIds =
                NativeEventShuffleSessionSites,
            ReferenceNatureEndlessConveyorSessionSchemaId =
                ReferenceNatureEndlessConveyorSessionV111.CurrentSchemaId,
            ReferenceNatureEndlessConveyorSiteIds =
                EndlessConveyorSessionSites,
            ReferenceNatureGloryReflectionsSessionSchemaId =
                ReferenceNatureGloryReflectionsSessionV111.CurrentSchemaId,
            ReferenceNatureGloryReflectionsSiteIds =
                GloryReflectionsSessionSites,
            ReferenceNatureInfestedAutomatonSessionSchemaId =
                ReferenceNatureInfestedAutomatonSessionV111.CurrentSchemaId,
            ReferenceNatureInfestedAutomatonSiteIds =
                InfestedAutomatonSessionSites,
            ReferenceNaturePunchOffRewardSessionSchemaId =
                ReferenceNaturePunchOffRewardSessionV111.CurrentSchemaId,
            ReferenceNaturePunchOffRewardSiteIds =
                PunchOffRewardSessionSites,
            ReferenceNaturePunchOffFightSessionSchemaId =
                ReferenceNaturePunchOffFightSessionV111.CurrentSchemaId,
            ReferenceNaturePunchOffFightSiteIds =
                PunchOffFightSessionSites
        };
        coverage.Validate();
        return coverage;
    }
}
