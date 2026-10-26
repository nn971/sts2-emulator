namespace Sts2Emulator.Core;

/// <summary>Versioned list of stable sites served by the direct Chance Law
/// query operation. Query-only sites are distinct from resumable session
/// coverage.</summary>
public sealed record StochasticChanceLawQueryCoverageV111(
    string SchemaId, string QueryOperation, string[] SiteIds)
{
    private string[]? _siteIds = Copy(SiteIds);

    public string[] SiteIds
    {
        get => Copy(_siteIds)!;
        init => _siteIds = Copy(value);
    }

    public const string CurrentSchemaId =
        "sts2-v111-stochastic-chance-law-query-coverage-v1";

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            QueryOperation != "chance_law_query" || SiteIds is null ||
            SiteIds.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException(
                "Invalid stochastic Chance Law query coverage.");
        var duplicateSiteIds = SiteIds.GroupBy(siteId => siteId,
                StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(siteId => siteId, StringComparer.Ordinal)
            .ToArray();
        if (duplicateSiteIds.Length != 0)
            throw new InvalidDataException(
                "Duplicate stochastic Chance Law query coverage sites: " +
                string.Join(", ", duplicateSiteIds));
    }

    private static string[]? Copy(string[]? values) =>
        values is null ? null : (string[])values.Clone();
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
    private string[]? _semanticReferenceNatureSiteIds =
        Copy(SemanticReferenceNatureSiteIds);
    private string[]? _referenceNatureRunActionSiteIds =
        Copy(ReferenceNatureRunActionSiteIds);
    private string[]? _referenceNatureEventChoiceSiteIds =
        Copy(ReferenceNatureEventChoiceSiteIds);
    private string[]? _referenceNatureNeowOfferSiteIds = [];
    private string[]? _referenceNatureTrashHeapSiteIds = [];
    private string[]? _referenceNatureTabletOfTruthSiteIds = [];
    private string[]? _referenceNatureTrialSiteIds = [];
    private string[]? _referenceNatureNativeEventShuffleSiteIds = [];
    private string[]? _referenceNatureNativeEventGoldSiteIds = [];
    private string[]? _referenceNatureNativeEventRelicSiteIds = [];
    private string[]? _referenceNatureNativeShovelSiteIds = [];
    private string[]? _referenceNatureEndlessConveyorSiteIds = [];
    private string[]? _referenceNatureGloryReflectionsSiteIds = [];
    private string[]? _referenceNatureInfestedAutomatonSiteIds = [];
    private string[]? _referenceNaturePunchOffRewardSiteIds = [];
    private string[]? _referenceNaturePunchOffFightSiteIds = [];
    private string[]? _referenceNatureAncientOfferSiteIds = [];
    private string[]? _referenceNatureMerchantSiteIds = [];
    private string[]? _referenceNatureTreasureSiteIds = [];
    private string[]? _referenceNatureCombatRewardSiteIds = [];
    private string[]? _referenceNatureMapGenerationSiteIds = [];

    public string[] SemanticReferenceNatureSiteIds
    {
        get => Copy(_semanticReferenceNatureSiteIds)!;
        init => _semanticReferenceNatureSiteIds = Copy(value);
    }
    public string[] ReferenceNatureRunActionSiteIds
    {
        get => Copy(_referenceNatureRunActionSiteIds)!;
        init => _referenceNatureRunActionSiteIds = Copy(value);
    }
    public string[] ReferenceNatureEventChoiceSiteIds
    {
        get => Copy(_referenceNatureEventChoiceSiteIds)!;
        init => _referenceNatureEventChoiceSiteIds = Copy(value);
    }

    public string ReferenceNatureAncientOfferSessionSchemaId { get; init; } =
        ReferenceNatureAncientOfferSessionV111.CurrentSchemaId;
    public string ReferenceNatureNeowOfferSessionSchemaId { get; init; } =
        ReferenceNatureNeowOfferSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureNeowOfferSiteIds
    {
        get => Copy(_referenceNatureNeowOfferSiteIds)!;
        init => _referenceNatureNeowOfferSiteIds = Copy(value);
    }
    public string ReferenceNatureTrashHeapSessionSchemaId { get; init; } =
        ReferenceNatureTrashHeapSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureTrashHeapSiteIds
    {
        get => Copy(_referenceNatureTrashHeapSiteIds)!;
        init => _referenceNatureTrashHeapSiteIds = Copy(value);
    }
    public string ReferenceNatureTabletOfTruthSessionSchemaId { get; init; } =
        ReferenceNatureTabletOfTruthSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureTabletOfTruthSiteIds
    {
        get => Copy(_referenceNatureTabletOfTruthSiteIds)!;
        init => _referenceNatureTabletOfTruthSiteIds = Copy(value);
    }
    public string ReferenceNatureTrialSessionSchemaId { get; init; } =
        ReferenceNatureTrialSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureTrialSiteIds
    {
        get => Copy(_referenceNatureTrialSiteIds)!;
        init => _referenceNatureTrialSiteIds = Copy(value);
    }
    public string ReferenceNatureNativeEventShuffleSessionSchemaId { get; init; } =
        ReferenceNatureNativeEventShuffleSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureNativeEventShuffleSiteIds
    {
        get => Copy(_referenceNatureNativeEventShuffleSiteIds)!;
        init => _referenceNatureNativeEventShuffleSiteIds = Copy(value);
    }
    public string ReferenceNatureNativeEventGoldSessionSchemaId { get; init; } =
        ReferenceNatureNativeEventGoldSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureNativeEventGoldSiteIds
    {
        get => Copy(_referenceNatureNativeEventGoldSiteIds)!;
        init => _referenceNatureNativeEventGoldSiteIds = Copy(value);
    }
    public string ReferenceNatureNativeEventRelicSessionSchemaId { get; init; } =
        ReferenceNatureNativeEventRelicSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureNativeEventRelicSiteIds
    {
        get => Copy(_referenceNatureNativeEventRelicSiteIds)!;
        init => _referenceNatureNativeEventRelicSiteIds = Copy(value);
    }
    public string ReferenceNatureNativeShovelSessionSchemaId { get; init; } =
        ReferenceNatureNativeShovelSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureNativeShovelSiteIds
    {
        get => Copy(_referenceNatureNativeShovelSiteIds)!;
        init => _referenceNatureNativeShovelSiteIds = Copy(value);
    }
    public string ReferenceNatureEndlessConveyorSessionSchemaId { get; init; } =
        ReferenceNatureEndlessConveyorSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureEndlessConveyorSiteIds
    {
        get => Copy(_referenceNatureEndlessConveyorSiteIds)!;
        init => _referenceNatureEndlessConveyorSiteIds = Copy(value);
    }
    public string ReferenceNatureGloryReflectionsSessionSchemaId { get; init; } =
        ReferenceNatureGloryReflectionsSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureGloryReflectionsSiteIds
    {
        get => Copy(_referenceNatureGloryReflectionsSiteIds)!;
        init => _referenceNatureGloryReflectionsSiteIds = Copy(value);
    }
    public string ReferenceNatureInfestedAutomatonSessionSchemaId { get; init; } =
        ReferenceNatureInfestedAutomatonSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureInfestedAutomatonSiteIds
    {
        get => Copy(_referenceNatureInfestedAutomatonSiteIds)!;
        init => _referenceNatureInfestedAutomatonSiteIds = Copy(value);
    }
    public string ReferenceNaturePunchOffRewardSessionSchemaId { get; init; } =
        ReferenceNaturePunchOffRewardSessionV111.CurrentSchemaId;
    public string[] ReferenceNaturePunchOffRewardSiteIds
    {
        get => Copy(_referenceNaturePunchOffRewardSiteIds)!;
        init => _referenceNaturePunchOffRewardSiteIds = Copy(value);
    }
    public string ReferenceNaturePunchOffFightSessionSchemaId { get; init; } =
        ReferenceNaturePunchOffFightSessionV111.CurrentSchemaId;
    public string[] ReferenceNaturePunchOffFightSiteIds
    {
        get => Copy(_referenceNaturePunchOffFightSiteIds)!;
        init => _referenceNaturePunchOffFightSiteIds = Copy(value);
    }
    public string[] ReferenceNatureAncientOfferSiteIds
    {
        get => Copy(_referenceNatureAncientOfferSiteIds)!;
        init => _referenceNatureAncientOfferSiteIds = Copy(value);
    }
    public string ReferenceNatureMerchantSessionSchemaId { get; init; } =
        ReferenceNatureMerchantSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureMerchantSiteIds
    {
        get => Copy(_referenceNatureMerchantSiteIds)!;
        init => _referenceNatureMerchantSiteIds = Copy(value);
    }
    public string ReferenceNatureTreasureSessionSchemaId { get; init; } =
        ReferenceNatureTreasureSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureTreasureSiteIds
    {
        get => Copy(_referenceNatureTreasureSiteIds)!;
        init => _referenceNatureTreasureSiteIds = Copy(value);
    }
    public string ReferenceNatureCombatRewardSessionSchemaId { get; init; } =
        ReferenceNatureCombatRewardSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureCombatRewardSiteIds
    {
        get => Copy(_referenceNatureCombatRewardSiteIds)!;
        init => _referenceNatureCombatRewardSiteIds = Copy(value);
    }
    public string ReferenceNatureMapGenerationSessionSchemaId { get; init; } =
        ReferenceNatureMapGenerationSessionV111.CurrentSchemaId;
    public string[] ReferenceNatureMapGenerationSiteIds
    {
        get => Copy(_referenceNatureMapGenerationSiteIds)!;
        init => _referenceNatureMapGenerationSiteIds = Copy(value);
    }

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
            ReferenceNatureNativeEventGoldSessionSchemaId !=
                ReferenceNatureNativeEventGoldSessionV111.CurrentSchemaId ||
            ReferenceNatureNativeEventRelicSessionSchemaId !=
                ReferenceNatureNativeEventRelicSessionV111.CurrentSchemaId ||
            ReferenceNatureNativeShovelSessionSchemaId !=
                ReferenceNatureNativeShovelSessionV111.CurrentSchemaId ||
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
                ReferenceNatureTreasureSessionV111.CurrentSchemaId ||
            ReferenceNatureCombatRewardSessionSchemaId !=
                ReferenceNatureCombatRewardSessionV111.CurrentSchemaId ||
            ReferenceNatureMapGenerationSessionSchemaId !=
                ReferenceNatureMapGenerationSessionV111.CurrentSchemaId)
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
        ValidateSiteIds(ReferenceNatureNativeEventGoldSiteIds);
        ValidateSiteIds(ReferenceNatureNativeEventRelicSiteIds);
        ValidateSiteIds(ReferenceNatureNativeShovelSiteIds);
        ValidateSiteIds(ReferenceNatureEndlessConveyorSiteIds);
        ValidateSiteIds(ReferenceNatureGloryReflectionsSiteIds);
        ValidateSiteIds(ReferenceNatureInfestedAutomatonSiteIds);
        ValidateSiteIds(ReferenceNaturePunchOffRewardSiteIds);
        ValidateSiteIds(ReferenceNaturePunchOffFightSiteIds);
        ValidateSiteIds(ReferenceNatureMerchantSiteIds);
        ValidateSiteIds(ReferenceNatureTreasureSiteIds);
        ValidateSiteIds(ReferenceNatureCombatRewardSiteIds);
        ValidateSiteIds(ReferenceNatureMapGenerationSiteIds);

        var natureChoiceSites = new HashSet<string>(
            StochasticSiteRegistryV111.NatureChoiceSiteIds,
            StringComparer.Ordinal);
        string[][] sessionSiteGroups =
        [
            SemanticReferenceNatureSiteIds,
            ReferenceNatureRunActionSiteIds,
            ReferenceNatureEventChoiceSiteIds,
            ReferenceNatureAncientOfferSiteIds,
            ReferenceNatureNeowOfferSiteIds,
            ReferenceNatureTrashHeapSiteIds,
            ReferenceNatureTabletOfTruthSiteIds,
            ReferenceNatureTrialSiteIds,
            ReferenceNatureNativeEventShuffleSiteIds,
            ReferenceNatureNativeEventGoldSiteIds,
            ReferenceNatureNativeEventRelicSiteIds,
            ReferenceNatureNativeShovelSiteIds,
            ReferenceNatureEndlessConveyorSiteIds,
            ReferenceNatureGloryReflectionsSiteIds,
            ReferenceNatureInfestedAutomatonSiteIds,
            ReferenceNaturePunchOffRewardSiteIds,
            ReferenceNaturePunchOffFightSiteIds,
            ReferenceNatureMerchantSiteIds,
            ReferenceNatureTreasureSiteIds,
            ReferenceNatureCombatRewardSiteIds,
            ReferenceNatureMapGenerationSiteIds
        ];
        if (sessionSiteGroups.SelectMany(siteIds => siteIds)
            .Any(siteId => !natureChoiceSites.Contains(siteId)))
            throw new InvalidDataException(
                "Stochastic session coverage includes a site absent from Nature-choice capabilities.");
    }

    private static void ValidateSiteIds(string[] siteIds)
    {
        if (siteIds is null || siteIds.Any(string.IsNullOrWhiteSpace) ||
            siteIds.Distinct(StringComparer.Ordinal).Count() != siteIds.Length)
            throw new InvalidDataException(
                "Stochastic session coverage contains invalid site identifiers.");
    }

    private static string[]? Copy(string[]? values) =>
        values is null ? null : (string[])values.Clone();
}

/// <summary>Legacy caller-bound index query metadata shared by Chance Law and
/// typed request validation.</summary>
internal sealed record LegacyCallerBoundIndexQuerySiteV111(
    string StreamId, string OperationKind, int MinimumSupportCount,
    int? ExactSupportCount = null);

/// <summary>
/// Authoritative stable site lists for capability negotiation and controlled
/// replay allowlists. Nature-choice coverage includes session sites that are
/// not admitted by legacy random-choice forwarding.
/// </summary>
public static class StochasticSiteRegistryV111
{
    private static readonly Dictionary<string, string>
        EngineOwnedPrototypeRewardBoundaries = new(StringComparer.Ordinal)
        {
            ["reward.card_rarity.ticket"] = "bounded_integer",
            ["reward.card_model.index_draw"] = "choose_index",
            ["card_rarity.roll.ticket"] = "bounded_integer",
            [NativeRewardCardModelSiteId] = "choose_index",
            ["card_rarity.upgrade_roll.ticket"] = "bounded_integer",
            ["reward.potion_odds.ticket"] = "bounded_integer",
            ["reward.random_potion.drop.ticket"] = "bounded_integer",
            ["reward.random_potion.model"] = "choose_index",
            [NativeCombatGoldSiteId] = "bounded_integer",
            [PrototypeNativePotionShop.RarityRollSiteId] =
                "bounded_integer",
            [PrototypeNativePotionShop.ModelWithinRaritySiteId] =
                "choose_index",
            [PrototypeGameEngine.PaelsToothCardReturnSiteId] =
                "choose_index"
        };

    public const string RelicGrabBagRaritySiteId =
        "relic_grab_bag.rarity.unit_float";
    public const string NativeCombatGoldSiteId =
        "reward.combat_gold.index_draw";
    public const string DrawPileRandomInsertSiteId =
        "combat.draw_pile.random_insert_index";
    public const string AfterShuffleRelicDrawPileInsertSiteId =
        "combat.relic.after_shuffle.generated_card.random_insert_index";
    public const string UnownedRandomRelicSelectionSiteId =
        "relic.unowned_random_selection.index_draw";
    public const string RandomPoolDistinctShuffleSiteId =
        "run.random_pool_distinct.shuffle_swap_index";
    public const string EventRewardCardBundleShuffleSiteId =
        "event.reward.card_bundle.shuffle_swap_index";
    public const string CombatEffectRandomTargetSiteId =
        "combat.effect.random_enemy";
    public const string CombatSlyAutoplayRandomTargetSiteId =
        "combat.sly_autoplay.random_enemy";
    public const string CombatAutoplayRandomTargetSiteId =
        "combat.autoplay.random_enemy";
    public const string CombatJeweledMaskRandomPowerDrawCardSiteId =
        "combat.relic.jeweled_mask.random_power_draw_card";
    public const string CombatGeneratedCardSelectSiteId =
        "combat.generated_card.select";
    public const string CombatRandomDrawAttackCardModelSiteId =
        "combat.random_draw_attack.card_model";
    public const string NativeRewardCardModelSiteId =
        "card_rarity.encounter_model.index_draw";
    public const string CardRarityRollUnitFloatSiteId =
        "card_rarity.roll.unit_float";
    public const string CardRarityUpgradeRollUnitFloatSiteId =
        "card_rarity.upgrade_roll.unit_float";
    public const string RewardPotionOddsUnitFloatSiteId =
        "reward.potion_odds.unit_float";
    public const string NativePotionFactoryRarityRollSiteId =
        "potion_factory.rarity_roll.unit_float";
    public const string NativePotionFactoryModelSiteId =
        "potion_factory.model_within_rarity.index_draw";
    public const string CombatCardTransformReplacementSiteId =
        "combat.card_transform.replacement.index_draw";
    public const string CombatPotionGenerationRandomModelSiteId =
        "combat.potion_generation.random_model";
    public const string CombatRandomHandCardSelectSiteId =
        "combat.random_hand_card.select";
    public const string CombatRandomHandCardEnergyCostSiteId =
        "combat.random_hand_card.energy_cost";
    public const string CombatDrawRelicEnergyCostSiteId =
        "combat.card_draw.relic_energy_cost";
    public const string CombatDrawSlitherEnergyCostSiteId =
        "combat.card_draw.slither_energy_cost";
    public const string CombatEffectDrawRecycleShuffleSiteId =
        "combat.draw.recycle.shuffle_swap_index";
    public const string CombatAutoplayRandomCardSelectSiteId =
        "combat.autoplay.random_card.select";
    public const string CombatRandomReplayCardSelectSiteId =
        "combat.draw_pile.random_replay_card.select";
    public const string CombatEnemyDiscardRandomInsertSiteId =
        "combat.enemy.discard.random_insert_index";
    public const string CombatEnemyDrawRandomInsertSiteId =
        "combat.enemy.draw.random_insert_index";
    public const string CombatEnemyTheftCardSelectSiteId =
        "combat.enemy.theft_card.select";
    public const string CombatEnemySummonPoolSelectSiteId =
        "combat.enemy.summon_pool.select";
    public const string CombatHandReturnShuffleSiteId =
        "combat.hand_return.draw_pile.shuffle_swap_index";
    public const string CombatRandomRareDrawToHandShuffleSiteId =
        "combat.draw_pile.random_rare_to_hand.shuffle_swap_index";
    public const string CombatRandomShortlistShuffleSiteId =
        "combat.card_selection.random_shortlist";
    public const string CombatFullPoolShuffleSiteId =
        "combat.generation.full_pool_shuffle.swap_index";
    public const string RewardBossRelicOfferShuffleSiteId =
        "reward.boss_relic_offer.shuffle_swap_index";
    public const string RewardLavaRockRelicOfferShuffleSiteId =
        "reward.lava_rock.relic_offer.shuffle_swap_index";
    public const string WarHammerEliteUpgradeShuffleSiteId =
        "relic.war_hammer.elite_upgrade.shuffle_swap_index";
    public const string NativeEventGoldIndexSiteId =
        "event.generated_gold.index_draw";
    public const string NativeEventGoldUnitFloatSiteId =
        "event.generated_gold.unit_float";
    public const string NativeStoneOfAllTimePotionSlotSiteId =
        "event.stone_of_all_time.potion_slot.index_draw";
    public const string NativeFutureOfPotionsCardTypeSiteId =
        "event.future_of_potions.card_type.index_draw";
    public const string NativeTabletOfTruthCardSiteId =
        "event.tablet_of_truth.card.index_draw";
    public const string NativeTrashHeapCardRewardSiteId =
        "event.trash_heap.card_reward.index_draw";
    public const string NativeTrashHeapRelicRewardSiteId =
        "event.trash_heap.relic_reward.index_draw";
    public const string NativeGloryTrialDamageSiteId =
        "event.glory_trial.damage_amount.index_draw";
    public const string NativeStoneOfAllTimePushTicketSiteId =
        "event.stone_of_all_time.push_ticket";
    public const string NativeStoneOfAllTimeLiftTicketSiteId =
        "event.stone_of_all_time.lift_ticket";

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
            [CombatEnemySummonPoolSelectSiteId] = new(
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
            [EventRewardCardBundleShuffleSiteId] = new(
                "reward", "bounded_integer", 2),
            [PrototypeNativeOrobasRelics.SandCastleAcquisitionShuffleSiteId] =
                new("event", "bounded_integer", 2),
            [RandomPoolDistinctShuffleSiteId] = new(
                "reward", "bounded_integer", 2),
            ["shop.potion_offer.shuffle_swap_index"] = new(
                "shop", "bounded_integer", 2),
            ["shop.relic_offer.shuffle_swap_index"] = new(
                "shop", "bounded_integer", 2)
        };

    public static string[] LegacyCallerBoundIndexQuerySiteIds =>
        LegacyCallerBoundIndexQuerySites.Keys
            .Order(StringComparer.Ordinal).ToArray();

    public static bool IsLegacyCallerBoundIndexQuerySite(string siteId) =>
        LegacyCallerBoundIndexQuerySites.ContainsKey(siteId);

    internal static bool TryGetLegacyCallerBoundIndexQuerySite(
        string siteId, out LegacyCallerBoundIndexQuerySiteV111 site) =>
        LegacyCallerBoundIndexQuerySites.TryGetValue(siteId, out site!);

    internal static bool TryGetRequiredNativeRandomIndexOperationKind(
        string siteId, string streamId, out string operationKind)
    {
        if (!IsNativeRandomIndexSite(siteId))
        {
            operationKind = string.Empty;
            return false;
        }

        if (NativeBoundedIndexOperationSites.Contains(
                siteId, StringComparer.Ordinal))
        {
            operationKind = "bounded_integer";
            return true;
        }

        var allowsChooseIndex = SupportsEngineOwnedNativeIndexBoundaryForStream(
            siteId, streamId, "choose_index");
        var allowsBoundedInteger =
            SupportsEngineOwnedNativeIndexBoundaryForStream(
                siteId, streamId, "bounded_integer");
        if (allowsChooseIndex != allowsBoundedInteger)
        {
            operationKind = allowsChooseIndex
                ? "choose_index" : "bounded_integer";
            return true;
        }

        operationKind = "choose_index";
        return true;
    }

    internal static bool RequiresLegacyBoundedRandomIndexOperation(
        string siteId) =>
        NativeBoundedIntegerSites.Contains(siteId, StringComparer.Ordinal) ||
        NativeEventContextBoundedIntegerSites.Contains(
            siteId, StringComparer.Ordinal) ||
        NativeFisherYatesSites.Contains(siteId, StringComparer.Ordinal) ||
        LegacyFisherYatesSites.Contains(siteId, StringComparer.Ordinal);

    public static string[] ControlledDrawInsertionSiteIds =>
    [
        DrawPileRandomInsertSiteId,
        AfterShuffleRelicDrawPileInsertSiteId,
        PrototypeControlledDrawInsertionV111.PersonalHiveDazedSiteId
    ];

    public static string[] CombatRandomTargetSiteIds =>
    [
        CombatEffectRandomTargetSiteId,
        CombatSlyAutoplayRandomTargetSiteId,
        CombatAutoplayRandomTargetSiteId
    ];

    public static string[] CombatGeneratedCardModelSiteIds =>
    [
        CombatGeneratedCardSelectSiteId,
        CombatRandomDrawAttackCardModelSiteId,
        CombatCardTransformReplacementSiteId
    ];

    public static string[] CombatRandomPilePositionSiteIds =>
    [
        DrawPileRandomInsertSiteId,
        AfterShuffleRelicDrawPileInsertSiteId,
        PrototypeControlledDrawInsertionV111.PersonalHiveDazedSiteId,
        CombatEnemyDiscardRandomInsertSiteId,
        CombatEnemyDrawRandomInsertSiteId
    ];

    public static string[] CombatEnemyPileInsertionSiteIds =>
    [
        CombatEnemyDiscardRandomInsertSiteId,
        CombatEnemyDrawRandomInsertSiteId
    ];

    public static string[] CombatCardPoolShuffleSiteIds =>
    [
        CombatHandReturnShuffleSiteId,
        CombatRandomRareDrawToHandShuffleSiteId,
        CombatRandomShortlistShuffleSiteId,
        CombatFullPoolShuffleSiteId
    ];

    public static string[] NativePolicyPoolShuffleSiteIds =>
        CombatCardPoolShuffleSiteIds
            .Append(NativeFisherYatesV111.SiteId)
            .Append(ControlledRecycleShuffleEngineV111.SiteId)
            .Append(PrototypeNativeRelicGrabBag.QueueShuffleSiteId)
            .Append(WarHammerEliteUpgradeShuffleSiteId)
            .Append(RewardBossRelicOfferShuffleSiteId)
            .Append(RewardLavaRockRelicOfferShuffleSiteId)
            .ToArray();

    public static string[] CombatIntentSiteIds =>
    [
        NativeCombatRngEntryV111.CommittedIntentSiteId,
        NativeCombatRngEntryV111.NextTurnIntentSiteId
    ];

    private static readonly string[] CombatCardInstanceIndexAuditSites =
    [
        CombatRandomHandCardSelectSiteId,
        CombatJeweledMaskRandomPowerDrawCardSiteId,
        CombatAutoplayRandomCardSelectSiteId,
        CombatRandomReplayCardSelectSiteId,
        CombatEnemyTheftCardSelectSiteId
    ];

    private static readonly string[] NativePolicyCardInstanceIndexAuditSites =
        CombatCardInstanceIndexAuditSites
            .Append(PrototypeGameEngine.FishingRodUpgradeSiteId)
            .ToArray();

    private static readonly string[] CombatCardEnergyCostAuditSites =
    [
        CombatRandomHandCardEnergyCostSiteId,
        CombatDrawRelicEnergyCostSiteId,
        CombatDrawSlitherEnergyCostSiteId
    ];

    private static readonly HashSet<string>
        CombatCardInstanceIndexAuditSiteSet = new(
            CombatCardInstanceIndexAuditSites, StringComparer.Ordinal);
    private static readonly HashSet<string>
        CombatCardEnergyCostAuditSiteSet = new(
            CombatCardEnergyCostAuditSites, StringComparer.Ordinal);

    public static string[] CombatCardInstanceIndexAuditSiteIds =>
        (string[])CombatCardInstanceIndexAuditSites.Clone();

    public static string[] NativePolicyCardInstanceIndexAuditSiteIds =>
        (string[])NativePolicyCardInstanceIndexAuditSites.Clone();

    public static bool IsCombatCardInstanceIndexAuditSite(string siteId) =>
        CombatCardInstanceIndexAuditSiteSet.Contains(siteId);

    public static bool IsNativePolicyCardInstanceIndexAuditSite(string siteId) =>
        NativePolicyCardInstanceIndexAuditSites.Contains(
            siteId, StringComparer.Ordinal);

    public static string[] CombatCardEnergyCostAuditSiteIds =>
        (string[])CombatCardEnergyCostAuditSites.Clone();

    public static bool IsCombatCardEnergyCostAuditSite(string siteId) =>
        CombatCardEnergyCostAuditSiteSet.Contains(siteId);

    public static string[] NeowOfferIndexSiteIds =>
    [
        PrototypeNativeOvergrowthEvents.NeowCursedRelicSiteId,
        PrototypeNativeOvergrowthEvents.NeowLavaRockPairSiteId,
        PrototypeNativeOvergrowthEvents.NeowOysterPairSiteId,
        PrototypeNativeOvergrowthEvents.NeowTalismanPairSiteId
    ];

    private static readonly string[] SemanticReferenceNatureSites =
    [
        NativeCombatRngEntryV111.CommittedIntentSiteId,
        NativeCombatRngEntryV111.NextTurnIntentSiteId,
        CombatEnemyDiscardRandomInsertSiteId,
        CombatEnemyDrawRandomInsertSiteId,
        CombatEnemyTheftCardSelectSiteId,
        CombatEnemySummonPoolSelectSiteId,
        NativeCombatRngEntryV111.InitialUniqueHpSiteId,
        NativeCombatRngEntryV111.ToughEggHatchHpSiteId,
        "combat.draw_pile.draw_card",
        "combat.draw_pile.reveal_card",
        CombatEffectRandomTargetSiteId,
        CombatSlyAutoplayRandomTargetSiteId,
        CombatAutoplayRandomTargetSiteId,
        CombatGeneratedCardSelectSiteId,
        CombatRandomDrawAttackCardModelSiteId,
        CombatRandomHandCardEnergyCostSiteId,
        CombatDrawRelicEnergyCostSiteId,
        CombatDrawSlitherEnergyCostSiteId,
        CombatJeweledMaskRandomPowerDrawCardSiteId,
        CombatCardTransformReplacementSiteId,
        CombatPotionGenerationRandomModelSiteId,
        CombatRandomHandCardSelectSiteId,
        CombatHandReturnShuffleSiteId,
        CombatFullPoolShuffleSiteId,
        CombatRandomRareDrawToHandShuffleSiteId,
        CombatEffectDrawRecycleShuffleSiteId,
        DrawPileRandomInsertSiteId,
        AfterShuffleRelicDrawPileInsertSiteId,
        CombatRandomShortlistShuffleSiteId,
        CombatAutoplayRandomCardSelectSiteId,
        CombatRandomReplayCardSelectSiteId
    ];

    private static readonly string[] RunActionSites =
    [
        "run.encounter.overgrowth_boss.index_draw",
        PrototypeNativeUnderdocks.BossEncounterIndexSiteId,
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
        "event.unknown_room.odds.ticket",
        NativeEventGoldIndexSiteId,
        NativeStoneOfAllTimePotionSlotSiteId,
        "event.fake_merchant.relic_offer.shuffle_swap_index",
        "event.endless_conveyor.dish_weighted_ticket",
        NativeFutureOfPotionsCardTypeSiteId
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

    private static readonly string[] RunActionShopPurchaseSites =
    [
        NativeGloryAncientsV111.BeautifulBraceletShuffleSiteId,
        NativeGloryAncientsV111.DistinguishedCapeCurseSiteId,
        NativePaelRewardsV111.LegionSkinSiteId,
        PrototypeNativeOrobasRelics.SandCastleAcquisitionShuffleSiteId,
        "relic.acquisition.random_rare_card.model",
        "relic.acquisition.random_potion.model",
        "event.potion.fill_empty_slots.model",
        "run.potion.fill_empty_slots.model",
        "reward.card_transform.replacement.index_draw"
    ];

    private static readonly string[] RunActionRestHealRewardSites =
    [
        "reward.card_rarity.ticket",
        "reward.card_model.index_draw",
        "card_rarity.roll.ticket",
        NativeRewardCardModelSiteId,
        "card_rarity.upgrade_roll.ticket",
        CardRarityRollUnitFloatSiteId,
        CardRarityUpgradeRollUnitFloatSiteId
    ];

    private static readonly string[] RunActionTreasureSites =
    [
        "treasure.relic_model.index_draw",
        "relic_grab_bag.rarity.ticket",
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId
    ];

    private static readonly string[] RunActionShovelSites =
    [
        UnownedRandomRelicSelectionSiteId,
        "relic_grab_bag.rarity.ticket",
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId
    ];

    private static readonly string[] RunActionOvergrowthCombatEncounterSites =
    [
        "combat.encounter.overgrowth_elite.index_draw",
        "combat.encounter.overgrowth_weak.index_draw",
        "combat.encounter.overgrowth_normal.index_draw"
    ];

    private static readonly string[] RunActionUnderdocksCombatSites =
    [
        "combat.encounter.underdocks.elite.index_draw",
        "combat.encounter.underdocks.weak.index_draw",
        "combat.encounter.underdocks.normal.index_draw"
    ];

    private static readonly string[] RunActionCombatSites =
    [
        "combat.encounter.formation.index_draw",
        "combat.encounter.weighted_selection.ticket",
        NativeCombatRngEntryV111.InitialUniqueHpSiteId,
        NativeCombatRngEntryV111.CommittedIntentSiteId,
        NativeFisherYatesV111.SiteId
    ];

    private static readonly string[] RunActionCombatEntryPotionSites =
    [
        "potion.rarity_roll.ticket",
        "potion.model_within_rarity.index_draw",
        NativePotionFactoryRarityRollSiteId,
        NativePotionFactoryModelSiteId
    ];

    private static readonly string[] ControlledEventChoiceSites =
    [
        "event.random_upgrade.card_instance",
        "event.reward.random_rare_card.model",
        "event.reward.random_potion.model",
        "event.reward.offer_potion.model.index_draw",
        EventRewardCardBundleShuffleSiteId,
        PrototypeNativeRelicGrabBag.QueueShuffleSiteId,
        "relic_grab_bag.rarity.ticket",
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId,
        "event.potion.fill_empty_slots.model",
        PrototypeNativePotionShop.RarityRollSiteId,
        PrototypeNativePotionShop.ModelWithinRaritySiteId,
        PrototypeNativePotionShop.UniformModelSiteId,
        "reward.card_rarity.ticket",
        "reward.card_model.index_draw",
        RandomPoolDistinctShuffleSiteId,
        "event.reward.random_modifier_curse.index_draw",
        "event.fake_merchant.relic_offer.shuffle_swap_index",
        NativeStoneOfAllTimeLiftTicketSiteId,
        NativeStoneOfAllTimePushTicketSiteId,
        NativeGloryAncientsV111.BeautifulBraceletShuffleSiteId,
        NativeGloryAncientsV111.DistinguishedCapeCurseSiteId,
        NativeGloryAncientsV111.FurCoatShuffleSiteId,
        NativePaelRewardsV111.LegionSkinSiteId,
        PrototypeNativeOrobasRelics.SandCastleAcquisitionShuffleSiteId,
        "potion.uncommon_model.index_draw",
        "relic.acquisition.random_rare_card.model",
        "relic.acquisition.random_potion.model",
        "run.potion.fill_empty_slots.model",
        "reward.card_transform.replacement.index_draw",
        UnownedRandomRelicSelectionSiteId
    ];

    private static readonly string[] ControlledPlayableSites =
    [
        ControlledPlayableCombatEngineV111.ShuffleSite,
        NativeCombatRngEntryV111.InitialUniqueHpSiteId,
        NativeCombatRngEntryV111.ToughEggHatchHpSiteId,
        NativeCombatRngEntryV111.NextTurnIntentSiteId,
        ControlledRecycleShuffleEngineV111.SiteId,
        CombatEffectRandomTargetSiteId,
        CombatSlyAutoplayRandomTargetSiteId,
        CombatAutoplayRandomTargetSiteId,
        CombatGeneratedCardSelectSiteId,
        CombatPotionGenerationRandomModelSiteId,
        CombatCardTransformReplacementSiteId,
        CombatRandomDrawAttackCardModelSiteId,
        DrawPileRandomInsertSiteId,
        PrototypeControlledDrawInsertionV111.PersonalHiveDazedSiteId,
        CombatEnemyTheftCardSelectSiteId,
        CombatEnemySummonPoolSelectSiteId,
        CombatHandReturnShuffleSiteId,
        CombatFullPoolShuffleSiteId,
        CombatRandomHandCardSelectSiteId,
        CombatRandomHandCardEnergyCostSiteId,
        CombatRandomShortlistShuffleSiteId
    ];

    private static readonly string[] NativeGaussianIntSites =
    [
        PrototypeNativeOvergrowthMap.RestCountGaussianSiteId,
        PrototypeNativeOvergrowthMap.UnknownCountGaussianSiteId,
        PrototypeNativeLaterActMap.HiveRestCountGaussianSiteId,
        PrototypeNativeLaterActMap.UnknownCountGaussianSiteId
    ];

    private static readonly string[] NativePersistentCombatEndIndexSites =
    [
        PrototypeGameEngine.FishingRodUpgradeSiteId
    ];

    private static readonly string[] NativeEventGoldSites =
    [
        NativeEventGoldIndexSiteId,
        NativeEventGoldUnitFloatSiteId
    ];
    private static readonly string[] NativeEventEntryIndexSites =
    [
        NativeStoneOfAllTimePotionSlotSiteId,
        NativeFutureOfPotionsCardTypeSiteId
    ];
    private static readonly string[] NativeEventNoEffectIndexSites =
    [
        NativeStoneOfAllTimePushTicketSiteId,
        NativeStoneOfAllTimeLiftTicketSiteId
    ];
    private static readonly string[] NativeEventCardPickSites =
    [
        PrototypeNativeGloryEvents.ReflectionsDowngradeSiteId,
        PrototypeNativeGloryEvents.ReflectionsUpgradeSiteId,
        PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId
    ];
    private static readonly string[] NativePunchOffRewardSites =
    [
        PrototypeNativePunchOff.RelicRewardSiteId,
        PrototypeNativePunchOff.PotionRewardSiteId
    ];
    private static readonly string[] NativePunchOffFightSites =
    [
        PrototypeNativePunchOff.ConstructHpOffsetSiteId
    ];
    private static readonly string[] NativeEventShufflePlanSites =
    [
        PrototypeGameEngine.FishingRodUpgradeSiteId,
        PrototypeGameEngine.ImprovementCardSiteId,
        PrototypeGameEngine.PaelsToothCardReturnSiteId,
        WarHammerEliteUpgradeShuffleSiteId,
        PrototypeNativeUnderdocksDoors.ShuffleSiteId,
        PrototypeNativeGloryEvents.HungryForMushroomsShuffleSiteId,
        PrototypeNativeGloryEvents.TinkerTypeOfferShuffleSiteId,
        PrototypeNativeGloryEvents.TinkerRiderOfferShuffleSiteId,
        PrototypeNativeGloryEvents.BattlewornUpgradeShuffleSiteId,
        PrototypeNativeFakeMerchant.RelicOfferShuffleSiteId,
        PrototypeNativeHiveEvents.WarHistorianRepyRelicShuffleSiteId
    ];

    private static string[] BuildChanceLawQuerySites() =>
    [
        .. ChanceLawEngineV111.LegacyMerchantIndexQuerySiteIds,
        .. ChanceLawEngineV111.LegacyRarityTicketQuerySiteIds,
        .. ChanceLawEngineV111.LegacyCardUpgradeTicketQuerySiteIds,
        .. ChanceLawEngineV111.LegacyRewardCardRarityTicketQuerySiteIds,
        .. ChanceLawEngineV111.LegacyRewardCardModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyRewardCardTransformQuerySiteIds,
        .. ChanceLawEngineV111.LegacyShopCardRarityQuerySiteIds,
        .. ChanceLawEngineV111.LegacyShopCardModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyShopCardPriceQuerySiteIds,
        .. ChanceLawEngineV111.LegacyShopPotionPriceQuerySiteIds,
        .. ChanceLawEngineV111.LegacyShopRelicPriceQuerySiteIds,
        .. LegacyCallerBoundIndexQuerySiteIds,
        .. NativeGaussianIntSites,
        .. ChanceLawEngineV111.LegacyPotionDropTicketQuerySiteIds,
        .. ChanceLawEngineV111.LegacyRewardPotionModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyPotionRewardOddsTicketQuerySiteIds,
        .. ChanceLawEngineV111.LegacyUncommonPotionModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyPotionModelWithinRarityQuerySiteIds,
        .. ChanceLawEngineV111.LegacyUniformPotionModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyEventRewardPotionModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyEventRewardRareCardModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyEventOfferPotionModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyEventModifierCurseModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyEventFillPotionSlotQuerySiteIds,
        .. ChanceLawEngineV111.LegacyEliteRelicModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyTreasureRelicModelQuerySiteIds,
        .. ChanceLawEngineV111.LegacyRelicAcquisitionRareCardQuerySiteIds,
        .. ChanceLawEngineV111.LegacyRelicAcquisitionPotionQuerySiteIds,
        .. ChanceLawEngineV111.LegacyRunPotionFillSlotQuerySiteIds,
        .. ChanceLawEngineV111.UnownedRelicSelectionQuerySiteIds,
        NativeOrobasEventV111.OfferSiteId,
        NativePaelRewardsV111.OfferSiteId,
        NativeAncientRewardsV111.TezcataraOfferSiteId,
        NativeNonupeipeEventV111.ShuffleSiteId,
        NativeTanxEventV111.ShuffleSiteId,
        NativeVakuuEventV111.ShuffleSiteId,
        NativeDarvEventV111.OfferVariantSiteId,
        NativeDarvEventV111.RelicShuffleSiteId,
        .. ReferenceNatureNeowOfferSessionV111.NeowOfferSessionSiteIds,
        .. ReferenceNatureTrashHeapSessionV111.TrashHeapSessionSiteIds,
        .. ReferenceNatureTabletOfTruthSessionV111.TabletOfTruthSessionSiteIds,
        .. ReferenceNatureTrialSessionV111.TrialSessionSiteIds,
        .. ReferenceNatureNativeEventShuffleSessionV111
            .NativeEventShuffleSessionSiteIds,
        .. NativeEventEntryIndexSites,
        .. NativeEventGoldSites,
        .. NativeEventNoEffectIndexSites,
        .. NativeEventCardPickSites,
        .. NativePunchOffRewardSites,
        .. NativePunchOffFightSites,
        ExperimentalSilentThreeActRouteV111.DarvHiveAssignmentSiteId,
        ExperimentalSilentThreeActRouteV111.DarvGloryAssignmentSiteId,
        ExperimentalSilentThreeActRouteV111.AncientAssignmentSiteId,
        "run.encounter.overgrowth_boss.index_draw",
        PrototypeNativeUnderdocks.BossEncounterIndexSiteId,
        ExperimentalSilentThreeActRouteV111.BossIndexSiteId,
        ExperimentalSilentThreeActRouteV111.SecondBossIndexSiteId,
        PrototypeNativeOvergrowthMap.PathStartColumnSiteId,
        PrototypeNativeOvergrowthMap.PathDeltaShuffleSiteId,
        PrototypeNativeOvergrowthMap.RoomAssignmentShuffleSiteId,
        PrototypeNativeLaterActMap.PathStartSiteId,
        PrototypeNativeLaterActMap.PathDeltaShuffleSiteId,
        PrototypeNativeLaterActMap.RoomAssignmentShuffleSiteId,
        PrototypeNativeLaterActMap.GloryRestCountSiteId,
        NativeCombatGoldSiteId,
        NativeCombatRngEntryV111.InitialUniqueHpSiteId,
        NativeCombatRngEntryV111.ToughEggHatchHpSiteId,
        RewardPotionOddsUnitFloatSiteId,
        RewardBossRelicOfferShuffleSiteId,
        RewardLavaRockRelicOfferShuffleSiteId,
        NativeFisherYatesV111.SiteId,
        ControlledRecycleShuffleEngineV111.SiteId,
        "combat.effect.personal_hive.dazed.random_insert_index",
        DrawPileRandomInsertSiteId,
        AfterShuffleRelicDrawPileInsertSiteId,
        CombatEnemyDrawRandomInsertSiteId,
        CombatEnemyDiscardRandomInsertSiteId,
        NativeRewardCardModelSiteId,
        CardRarityUpgradeRollUnitFloatSiteId,
        "event.endless_conveyor.dish_weighted_ticket",
        "event.endless_conveyor.random_upgrade.card_instance",
        "event.endless_conveyor.colorless_card.index_draw",
        "shop.merchant.sale_slot.index_draw",
        CardRarityRollUnitFloatSiteId,
        "shop.merchant.card_model.index_draw",
        "shop.colorless_card_model.index_draw",
        PrototypeNativeRelicGrabBag.QueueShuffleSiteId,
        NativePotionFactoryRarityRollSiteId,
        NativePotionFactoryModelSiteId,
        RelicGrabBagRaritySiteId,
        PrototypeNativeRelicGrabBag.NativeMerchantModelSiteId,
        "relic.paels_legion.skin.index_draw",
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

    private static readonly string[] LegacyNativeMerchantFallbackSites =
    [
        "shop.merchant.sale_slot.index_draw",
        "card_rarity.roll.ticket",
        "shop.merchant.card_model.index_draw",
        "shop.merchant.card_upgrade_roll.ticket",
        "shop.merchant.card_price_modifier.index_draw",
        "shop.colorless_card_model.index_draw",
        PrototypeNativePotionShop.RarityRollSiteId,
        PrototypeNativePotionShop.ModelWithinRaritySiteId,
        "shop.merchant.potion_price_modifier.index_draw",
        "relic_grab_bag.rarity.ticket",
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId,
        PrototypeNativeRelicGrabBag.NativeMerchantModelSiteId,
        "shop.merchant.relic_price_modifier.index_draw"
    ];

    private static readonly HashSet<string>
        LegacyNativeMerchantFallbackSiteSet = new(
            LegacyNativeMerchantFallbackSites, StringComparer.Ordinal);

    private static readonly string[] NativeMerchantSessionSites =
    [
        CardRarityRollUnitFloatSiteId,
        "shop.merchant.card_upgrade_roll.unit_float",
        "relic.acquisition.random_rare_card.model",
        "relic.acquisition.random_potion.model",
        "run.potion.fill_empty_slots.model",
        NativePotionFactoryRarityRollSiteId,
        NativePotionFactoryModelSiteId,
        RelicGrabBagRaritySiteId,
        "shop.merchant.card_price_modifier.unit_float",
        "shop.merchant.potion_price_modifier.unit_float",
        "shop.merchant.relic_price_modifier.unit_float",
        .. LegacyNativeMerchantFallbackSites
    ];

    private static readonly string[] NativeTreasureSessionSites =
    [
        RelicGrabBagRaritySiteId,
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId
    ];

    private static string[] NativeCombatRewardSessionSites =>
        ReferenceNatureCombatRewardSessionV111.SessionSiteIds;

    private static readonly string[] NativeMapGenerationSessionSites =
    [
        PrototypeNativeOvergrowthMap.PathStartColumnSiteId,
        PrototypeNativeOvergrowthMap.PathDeltaShuffleSiteId,
        PrototypeNativeOvergrowthMap.RestCountGaussianSiteId,
        PrototypeNativeOvergrowthMap.UnknownCountGaussianSiteId,
        PrototypeNativeOvergrowthMap.RoomAssignmentShuffleSiteId,
        PrototypeNativeLaterActMap.PathStartSiteId,
        PrototypeNativeLaterActMap.PathDeltaShuffleSiteId,
        PrototypeNativeLaterActMap.HiveRestCountGaussianSiteId,
        PrototypeNativeLaterActMap.UnknownCountGaussianSiteId,
        PrototypeNativeLaterActMap.GloryRestCountSiteId,
        PrototypeNativeLaterActMap.RoomAssignmentShuffleSiteId
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
        ReferenceNatureNeowOfferSessionV111.NeowOfferSessionSiteIds;
    private static readonly string[] TrashHeapSessionSites =
        ReferenceNatureTrashHeapSessionV111.TrashHeapSessionSiteIds;
    private static readonly string[] TabletOfTruthSessionSites =
        ReferenceNatureTabletOfTruthSessionV111.TabletOfTruthSessionSiteIds;
    private static readonly string[] TrialSessionSites =
        ReferenceNatureTrialSessionV111.TrialSessionSiteIds;
    private static readonly string[] NativeEventShuffleSessionSites =
        ReferenceNatureNativeEventShuffleSessionV111
            .NativeEventShuffleSessionSiteIds;
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
    private static readonly string[] NativeEventContextIndexSites =
    [
        .. NativeEventEntryIndexSites,
        .. NativeEventCardPickSites,
        .. ReferenceNatureTrashHeapSessionV111.TrashHeapSessionSiteIds,
        .. ReferenceNatureTabletOfTruthSessionV111.TabletOfTruthSessionSiteIds,
        PrototypeNativeEndlessConveyor.UpgradeCardSiteId,
        PrototypeNativeEndlessConveyor.ColorlessCardSiteId
    ];
    private static readonly string[] NativeEventContextBoundedIntegerSites =
    [
        NativeEventGoldIndexSiteId,
        NativeStoneOfAllTimeLiftTicketSiteId,
        .. ReferenceNatureTrialSessionV111.TrialSessionSiteIds
    ];
    private static readonly string[] NativeEventContextUnitFloatSites =
    [
        NativeEventGoldUnitFloatSiteId,
        PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId
    ];
    private static readonly string[] NativeUnitFloatSites =
    [
        RewardPotionOddsUnitFloatSiteId,
        CardRarityUpgradeRollUnitFloatSiteId,
        CardRarityRollUnitFloatSiteId,
        NativePotionFactoryRarityRollSiteId,
        RelicGrabBagRaritySiteId,
        "shop.merchant.card_upgrade_roll.unit_float",
        .. NativeMerchantPriceSites,
        .. NativeEventContextUnitFloatSites
    ];
    private static readonly string[] NativeBoundedIntegerSites =
    [
        NativeCombatGoldSiteId
    ];
    private static readonly string[] NativeDiagnosticRandomIndexSites =
    [
        NativeCombatRngEntryV111.DiagnosticMonsterLocalSiteId
    ];
    private static readonly string[] NativeRangedFloatSites = [];
    private static readonly string[] NativeRangedDoubleSites = [];
    private static readonly string[] NativeGaussianDoubleSites = [];
    private static readonly string[] NativeEventContextShuffleSites =
    [
        PrototypeNativeFakeMerchant.RelicOfferShuffleSiteId,
        PrototypeNativeGloryEvents.BattlewornUpgradeShuffleSiteId,
        PrototypeNativeGloryEvents.HungryForMushroomsShuffleSiteId,
        PrototypeNativeGloryEvents.TinkerTypeOfferShuffleSiteId,
        PrototypeNativeGloryEvents.TinkerRiderOfferShuffleSiteId,
        PrototypeNativeUnderdocksDoors.ShuffleSiteId
    ];
    private static readonly string[] NativeEventContextSites =
        NativeEventContextIndexSites
            .Concat(NativeEventContextBoundedIntegerSites)
            .Concat(NativeEventContextUnitFloatSites)
            .Concat(NativeEventContextShuffleSites)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    private static readonly string[] NativeFisherYatesSites =
    [
        NativeFisherYatesV111.SiteId,
        ControlledRecycleShuffleEngineV111.SiteId,
        CombatHandReturnShuffleSiteId,
        CombatRandomRareDrawToHandShuffleSiteId,
        CombatRandomShortlistShuffleSiteId,
        CombatFullPoolShuffleSiteId,
        .. StochasticSiteRegistryV111.NativeEventContextShuffleSiteIds,
        NativeDarvEventV111.RelicShuffleSiteId,
        NativeGloryAncientsV111.BeautifulBraceletShuffleSiteId,
        NativeGloryAncientsV111.FurCoatShuffleSiteId,
        PrototypeNativeOvergrowthEvents.NeowPositiveRelicShuffleSiteId,
        NativeNonupeipeEventV111.ShuffleSiteId,
        NativeTanxEventV111.ShuffleSiteId,
        NativeVakuuEventV111.ShuffleSiteId,
        PrototypeNativeHiveEvents.WarHistorianRepyRelicShuffleSiteId,
        PrototypeNativeOvergrowthMap.PathDeltaShuffleSiteId,
        PrototypeNativeOvergrowthMap.RoomAssignmentShuffleSiteId,
        PrototypeNativeLaterActMap.PathDeltaShuffleSiteId,
        PrototypeNativeLaterActMap.RoomAssignmentShuffleSiteId,
        PrototypeNativeRelicGrabBag.QueueShuffleSiteId,
        WarHammerEliteUpgradeShuffleSiteId,
        RewardLavaRockRelicOfferShuffleSiteId,
        RewardBossRelicOfferShuffleSiteId
    ];

    private static readonly string[] LegacyFisherYatesSites =
        NativeFisherYatesSites.Concat(new[]
        {
            CombatEffectDrawRecycleShuffleSiteId,
            EventRewardCardBundleShuffleSiteId,
            PrototypeNativeOrobasRelics.SandCastleAcquisitionShuffleSiteId,
            RandomPoolDistinctShuffleSiteId,
            "shop.potion_offer.shuffle_swap_index",
            "shop.relic_offer.shuffle_swap_index"
        }).Distinct(StringComparer.Ordinal).ToArray();

    private static readonly string[] NativeBoundedIndexOperationSites =
    [
        .. NativeBoundedIntegerSites,
        .. NativeEventContextBoundedIntegerSites,
        .. NativeFisherYatesSites,
        .. NativeDiagnosticRandomIndexSites,
        NativeDarvEventV111.OfferVariantSiteId,
        PrototypeNativeOvergrowthMap.PathStartColumnSiteId,
        PrototypeNativeLaterActMap.PathStartSiteId,
        PrototypeNativeLaterActMap.GloryRestCountSiteId,
        PrototypeNativePunchOff.ConstructHpOffsetSiteId
    ];

    private static readonly HashSet<string> SemanticReferenceNatureSiteSet =
        new(SemanticReferenceNatureSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionSiteSet =
        new(RunActionSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionEventEntrySiteSet =
        new(RunActionEventEntrySites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionShopSiteSet =
        new(RunActionShopSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionShopPurchaseSiteSet =
        new(RunActionShopPurchaseSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionRestHealRewardSiteSet =
        new(RunActionRestHealRewardSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionTreasureSiteSet =
        new(RunActionTreasureSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionShovelSiteSet =
        new(RunActionShovelSites, StringComparer.Ordinal);
    private static readonly HashSet<string> ControlledEventChoiceSiteSet =
        new(ControlledEventChoiceSites, StringComparer.Ordinal);
    private static class ChanceLawQueryRegistration
    {
        internal static readonly string[] SiteIds =
            BuildChanceLawQuerySites()
                .Distinct(StringComparer.Ordinal).ToArray();
        internal static readonly HashSet<string> SiteSet =
            new(SiteIds, StringComparer.Ordinal);
    }
    private static readonly string[] RunActionSessionSites = RunActionSites
        .Concat(RunActionEventEntrySites)
        .Concat(RunActionShopSites)
        .Concat(RunActionShopPurchaseSites)
        .Concat(RunActionRestHealRewardSites)
        .Concat(RunActionTreasureSites)
        .Concat(RunActionShovelSites)
        .Concat(RunActionOvergrowthCombatEncounterSites)
        .Concat(RunActionCombatSites)
        .Concat(RunActionUnderdocksCombatSites)
        .Concat(RunActionCombatEntryPotionSites)
        .Concat(NativeMapGenerationSessionSites)
        .Concat(AncientOfferSessionSites)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    private static readonly string[] NatureChoiceSiteCandidates =
    [
        .. SemanticReferenceNatureSites,
        .. RunActionSites,
        .. RunActionEventEntrySites,
        .. RunActionShopSites,
        .. RunActionShopPurchaseSites,
        .. RunActionRestHealRewardSites,
        .. RunActionTreasureSites,
        .. RunActionOvergrowthCombatEncounterSites,
        .. RunActionCombatSites,
        .. RunActionUnderdocksCombatSites,
        .. RunActionCombatEntryPotionSites,
        .. ControlledEventChoiceSites,
        .. NativeMerchantSessionSites,
        .. NativeTreasureSessionSites,
        .. NativeCombatRewardSessionSites,
        .. NativeMapGenerationSessionSites,
        .. ReferenceNatureNeowOfferSessionV111.NeowOfferSessionSiteIds,
        .. ReferenceNatureTrashHeapSessionV111.TrashHeapSessionSiteIds,
        .. ReferenceNatureTabletOfTruthSessionV111.TabletOfTruthSessionSiteIds,
        .. ReferenceNatureTrialSessionV111.TrialSessionSiteIds,
        .. ReferenceNatureNativeEventShuffleSessionV111
            .NativeEventShuffleSessionSiteIds,
        .. ReferenceNatureEndlessConveyorSessionV111.SessionSiteIds,
        .. ReferenceNatureInfestedAutomatonSessionV111.SessionSiteIds,
    ];

    private static readonly string[] NatureChoiceSites =
        NatureChoiceSiteCandidates
            .Concat(ReferenceNatureNativeEventGoldSessionV111.SessionSiteIds)
            .Concat(ReferenceNatureNativeEventRelicSessionV111.SessionSiteIds)
            .Concat(ReferenceNatureNativeShovelSessionV111.SessionSiteIds)
            .Concat(ReferenceNatureGloryReflectionsSessionV111.SessionSiteIds)
            .Concat(ReferenceNaturePunchOffRewardSessionV111.SessionSiteIds)
            .Concat(ReferenceNaturePunchOffFightSessionV111.SessionSiteIds)
            .Concat(AncientOfferSessionSites)
            .Distinct(StringComparer.Ordinal).ToArray();

    private static readonly string[] NativeRandomIndexSites =
        NatureChoiceSites
            .Concat(NativePersistentCombatEndIndexSites)
            .Concat(ControlledPlayableSites)
            .Concat(NativeEventContextIndexSites)
            .Concat(NativeBoundedIndexOperationSites)
            .Concat(CombatIntentSiteIds)
            .Where(siteId =>
                !NativeUnitFloatSites.Contains(
                    siteId, StringComparer.Ordinal) &&
                !NativeGaussianIntSites.Contains(
                    siteId, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal).ToArray();

    private static readonly HashSet<string> NativeRandomIndexSiteSet =
        new(NativeRandomIndexSites, StringComparer.Ordinal);

    public static string[] SemanticReferenceNatureSiteIds =>
        (string[])SemanticReferenceNatureSites.Clone();
    public static string[] RunActionSiteIds => (string[])RunActionSites.Clone();
    public static string[] RunActionEventEntrySiteIds =>
        (string[])RunActionEventEntrySites.Clone();
    public static string[] RunActionShopSiteIds =>
        (string[])RunActionShopSites.Clone();
    public static string[] RunActionShopPurchaseSiteIds =>
        (string[])RunActionShopPurchaseSites.Clone();
    public static string[] RunActionRestHealRewardSiteIds =>
        (string[])RunActionRestHealRewardSites.Clone();
    public static string[] RunActionTreasureSiteIds =>
        (string[])RunActionTreasureSites.Clone();
    public static string[] RunActionShovelSiteIds =>
        (string[])RunActionShovelSites.Clone();
    public static string[] RunActionCombatSiteIds =>
        RunActionOvergrowthCombatEncounterSites
            .Concat(RunActionCombatSites)
            .Concat(RunActionUnderdocksCombatSites)
            .Distinct(StringComparer.Ordinal).ToArray();
    public static string[] RunActionUnderdocksCombatSiteIds =>
        (string[])RunActionUnderdocksCombatSites.Clone();
    public static string[] RunActionOvergrowthCombatEntrySiteIds =>
        RunActionOvergrowthCombatEncounterSites
            .Concat(RunActionCombatSites)
            .Concat(RunActionCombatEntryPotionSites)
            .Distinct(StringComparer.Ordinal).ToArray();
    public static string[] RunActionUnderdocksCombatEntrySiteIds =>
        RunActionUnderdocksCombatSites
            .Concat(RunActionCombatSites)
            .Concat(RunActionCombatEntryPotionSites)
            .Distinct(StringComparer.Ordinal).ToArray();
    public static string[] RunActionCombatEntryPotionSiteIds =>
        (string[])RunActionCombatEntryPotionSites.Clone();
    public static string[] RunActionCombatEntrySiteIds =>
        RunActionCombatSiteIds.Concat(RunActionCombatEntryPotionSites)
            .Distinct(StringComparer.Ordinal).ToArray();
    public static string[] ControlledRunActionSiteIds =>
        (string[])RunActionSessionSites.Clone();
    public static string[] ControlledEventChoiceSiteIds =>
        (string[])ControlledEventChoiceSites.Clone();
    public static string[] ControlledPlayableSiteIds =>
        (string[])ControlledPlayableSites.Clone();
    public static string[] ChanceLawQuerySiteIds =>
        (string[])ChanceLawQueryRegistration.SiteIds.Clone();
    public static string[] NativeUnitFloatSiteIds =>
        (string[])NativeUnitFloatSites.Clone();
    public static string[] NativeRangedFloatSiteIds =>
        (string[])NativeRangedFloatSites.Clone();
    public static string[] NativeRangedDoubleSiteIds =>
        (string[])NativeRangedDoubleSites.Clone();
    public static string[] NativeGaussianDoubleSiteIds =>
        (string[])NativeGaussianDoubleSites.Clone();
    public static string[] NativeBoundedIntegerSiteIds =>
        (string[])NativeBoundedIntegerSites.Clone();
    public static string[] NativeBoundedIndexOperationSiteIds =>
        (string[])NativeBoundedIndexOperationSites.Clone();
    public static string[] NativeDiagnosticRandomIndexSiteIds =>
        (string[])NativeDiagnosticRandomIndexSites.Clone();
    public static string[] NativeMerchantPriceSiteIds =>
        (string[])NativeMerchantPriceSites.Clone();
    public static string[] AncientOfferSessionSiteIds =>
        (string[])AncientOfferSessionSites.Clone();
    public static string[] NativeMerchantSessionSiteIds =>
        (string[])NativeMerchantSessionSites.Clone();
    public static string[] LegacyNativeMerchantFallbackSiteIds =>
        (string[])LegacyNativeMerchantFallbackSites.Clone();
    public static string[] NativeTreasureSessionSiteIds =>
        (string[])NativeTreasureSessionSites.Clone();
    public static string[] NativeCombatRewardSessionSiteIds =>
        NativeCombatRewardSessionSites;
    public static string[] NativeMapGenerationSessionSiteIds =>
        (string[])NativeMapGenerationSessionSites.Clone();
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
    public static string[] NativeEventContextSiteIds =>
        (string[])NativeEventContextSites.Clone();
    public static string[] NativeEventGoldSiteIds =>
        (string[])NativeEventGoldSites.Clone();
    public static string[] NativeEventEntryIndexSiteIds =>
        (string[])NativeEventEntryIndexSites.Clone();
    public static string[] NativeEventNoEffectIndexSiteIds =>
        (string[])NativeEventNoEffectIndexSites.Clone();
    public static string[] NativeEventCardPickSiteIds =>
        (string[])NativeEventCardPickSites.Clone();
    public static string[] NativePunchOffRewardSiteIds =>
        (string[])NativePunchOffRewardSites.Clone();
    public static string[] NativePunchOffFightSiteIds =>
        (string[])NativePunchOffFightSites.Clone();
    public static string[] NativeEventShufflePlanSiteIds =>
        (string[])NativeEventShufflePlanSites.Clone();
    public static string[] NativeEventContextIndexSiteIds =>
        (string[])NativeEventContextIndexSites.Clone();
    public static string[] NativeEventContextBoundedIntegerSiteIds =>
        (string[])NativeEventContextBoundedIntegerSites.Clone();
    public static string[] NativeEventContextUnitFloatSiteIds =>
        (string[])NativeEventContextUnitFloatSites.Clone();
    public static string[] NativeEventContextShuffleSiteIds =>
        (string[])NativeEventContextShuffleSites.Clone();
    public static string[] NativeFisherYatesSiteIds =>
        (string[])NativeFisherYatesSites.Clone();
    public static string[] LegacyFisherYatesSiteIds =>
        (string[])LegacyFisherYatesSites.Clone();
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

    public static bool IsRunActionShopPurchaseSite(string siteId) =>
        RunActionShopPurchaseSiteSet.Contains(siteId);

    public static bool IsRunActionRestHealRewardSite(string siteId) =>
        RunActionRestHealRewardSiteSet.Contains(siteId);

    public static bool IsRunActionTreasureSite(string siteId) =>
        RunActionTreasureSiteSet.Contains(siteId);

    public static bool IsRunActionShovelSite(string siteId) =>
        RunActionShovelSiteSet.Contains(siteId);

    public static bool IsRunActionCombatSite(string siteId) =>
        RunActionCombatSites.Contains(siteId, StringComparer.Ordinal)
        || RunActionOvergrowthCombatEncounterSites.Contains(
            siteId, StringComparer.Ordinal)
        || RunActionUnderdocksCombatSites.Contains(
            siteId, StringComparer.Ordinal);

    public static bool IsRunActionOvergrowthCombatEntrySite(string siteId) =>
        RunActionOvergrowthCombatEncounterSites.Contains(
            siteId, StringComparer.Ordinal)
        || RunActionCombatSites.Contains(siteId, StringComparer.Ordinal)
        || IsRunActionCombatEntryPotionSite(siteId);

    public static bool IsRunActionUnderdocksCombatEntrySite(string siteId) =>
        RunActionUnderdocksCombatSites.Contains(
            siteId, StringComparer.Ordinal)
        || RunActionCombatSites.Contains(siteId, StringComparer.Ordinal)
        || IsRunActionCombatEntryPotionSite(siteId);

    public static bool IsRunActionCombatEntryPotionSite(string siteId) =>
        RunActionCombatEntryPotionSites.Contains(
            siteId, StringComparer.Ordinal);

    public static bool IsRunActionCombatEntrySite(string siteId) =>
        IsRunActionCombatSite(siteId)
        || IsRunActionCombatEntryPotionSite(siteId);

    public static bool IsControlledEventChoiceSite(string siteId) =>
        ControlledEventChoiceSiteSet.Contains(siteId);

    public static bool IsChanceLawQuerySite(string siteId) =>
        ChanceLawQueryRegistration.SiteSet.Contains(siteId);

    public static bool IsLegacyRandomChoiceSite(string siteId) =>
        IsChanceLawQuerySite(siteId)
        || NatureChoiceSiteCandidates.Contains(siteId, StringComparer.Ordinal)
        || siteId is "map.overgrowth.rest_count.gaussian_component.component"
            or "map.overgrowth.unknown_count.gaussian_component.component"
            or "map.later_act.hive_rest_count.gaussian_int.component"
            or "map.later_act.unknown_count.gaussian_int.component";

    public static bool IsLegacyFisherYatesSite(string siteId) =>
        LegacyFisherYatesSites.Contains(siteId, StringComparer.Ordinal);

    public static bool IsNativeRandomIndexSite(string siteId) =>
        NativeRandomIndexSiteSet.Contains(siteId)
        && !IsNativeUnitFloatSite(siteId)
        && !IsNativeGaussianIntSite(siteId);

    internal static bool IsRegisteredRandomIndexRequestSite(string siteId) =>
        IsLegacyRandomChoiceSite(siteId) ||
        IsNativeRandomIndexSite(siteId);

    public static bool IsNativeUnitFloatSite(string siteId) =>
        NativeUnitFloatSites.Contains(siteId, StringComparer.Ordinal);

    internal static bool IsRegisteredRandomUnitFloatRequestSite(
        string siteId) => IsNativeUnitFloatSite(siteId);

    internal static bool IsRegisteredRandomRangedFloatRequestSite(
        string siteId) =>
        NativeRangedFloatSites.Contains(siteId, StringComparer.Ordinal);

    internal static bool IsRegisteredRandomRangedDoubleRequestSite(
        string siteId) =>
        NativeRangedDoubleSites.Contains(siteId, StringComparer.Ordinal);

    public static bool IsNativeGaussianIntSite(string siteId) =>
        NativeGaussianIntSites.Contains(siteId, StringComparer.Ordinal);

    public static string[] NativeGaussianIntSiteIds =>
        (string[])NativeGaussianIntSites.Clone();

    internal static bool MatchesNativeGaussianIntRequest(
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

    public static string[] NativePersistentCombatEndIndexSiteIds =>
        (string[])NativePersistentCombatEndIndexSites.Clone();

    internal static bool IsRegisteredRandomGaussianIntRequestSite(
        string siteId) => IsNativeGaussianIntSite(siteId);

    internal static bool IsRegisteredRandomGaussianDoubleRequestSite(
        string siteId) =>
        NativeGaussianDoubleSites.Contains(siteId, StringComparer.Ordinal);

    internal static bool SupportsEngineOwnedNativeBoundary(
        string siteId, string operationKind)
    {
        if (string.IsNullOrWhiteSpace(siteId) ||
            string.IsNullOrWhiteSpace(operationKind))
            return false;

        if (operationKind is "choose_index" or "bounded_integer")
            return IsNativeRandomIndexSite(siteId);

        return operationKind switch
        {
            "unit_float" => IsNativeUnitFloatSite(siteId),
            "gaussian_int" => IsNativeGaussianIntSite(siteId),
            // These boundary kinds have no registered native gameplay sites
            // yet. Add an audited finite site set before enabling fallback.
            "ranged_float" or "ranged_double" or "gaussian_double" => false,
            _ => false
        };
    }

    internal static bool SupportsEngineOwnedPrototypeRewardBoundary(
        string siteId, string streamId, string operationKind) =>
        !string.IsNullOrWhiteSpace(siteId) && streamId == "reward" &&
        !string.IsNullOrWhiteSpace(operationKind) &&
        EngineOwnedPrototypeRewardBoundaries.TryGetValue(
            siteId, out var expectedOperationKind) &&
        operationKind == expectedOperationKind;

    internal static bool SupportsEngineOwnedNativeBoundary(
        string siteId, string streamId, string operationKind)
    {
        if (!SupportsEngineOwnedNativeBoundary(siteId, operationKind))
            return false;

        if ((operationKind is "choose_index" or "bounded_integer") &&
            TryGetRequiredNativeRandomIndexOperationKind(
                siteId, streamId, out var requiredOperationKind) &&
            operationKind != requiredOperationKind)
            return false;

        return SupportsEngineOwnedNativeBoundaryForStream(
            siteId, streamId, operationKind);
    }

    private static bool SupportsEngineOwnedNativeBoundaryForStream(
        string siteId, string streamId, string operationKind) => streamId switch
        {
            "run.monster_ai" => operationKind == "bounded_integer" &&
                CombatIntentSiteIds.Contains(siteId, StringComparer.Ordinal),
            "run.shuffle" =>
                operationKind == "bounded_integer" &&
                (siteId == NativeFisherYatesV111.SiteId ||
                 siteId == ControlledRecycleShuffleEngineV111.SiteId ||
                 siteId == CombatHandReturnShuffleSiteId ||
                 CombatRandomPilePositionSiteIds.Contains(
                     siteId, StringComparer.Ordinal)),
            "run.combat_energy_costs" =>
                operationKind == "bounded_integer" &&
                (siteId is CombatRandomHandCardEnergyCostSiteId or
                    CombatDrawRelicEnergyCostSiteId or
                    CombatDrawSlitherEnergyCostSiteId),
            "run.combat_targets" => operationKind == "choose_index" &&
                CombatRandomTargetSiteIds.Contains(
                    siteId, StringComparer.Ordinal),
            "run.combat_card_selection" => siteId switch
            {
                CombatRandomHandCardSelectSiteId or
                CombatJeweledMaskRandomPowerDrawCardSiteId or
                CombatRandomReplayCardSelectSiteId or
                CombatAutoplayRandomCardSelectSiteId =>
                    operationKind == "choose_index",
                CombatRandomRareDrawToHandShuffleSiteId or
                CombatRandomShortlistShuffleSiteId =>
                    operationKind == "bounded_integer",
                _ => false
            },
            "run.combat_card_generation" => siteId switch
            {
                "combat.generated_card.select" or
                "combat.random_draw_attack.card_model" or
                CombatCardTransformReplacementSiteId or
                CombatEnemyTheftCardSelectSiteId =>
                    operationKind == "choose_index",
                CombatFullPoolShuffleSiteId =>
                    operationKind == "bounded_integer",
                _ => false
            },
            "run.combat_potion_generation" => siteId switch
            {
                CombatPotionGenerationRandomModelSiteId or
                NativePotionFactoryModelSiteId =>
                    operationKind == "choose_index",
                NativePotionFactoryRarityRollSiteId =>
                    operationKind == "unit_float",
                _ => false
            },
            "run.niche" => siteId switch
            {
                StochasticSiteRegistryV111.WarHammerEliteUpgradeShuffleSiteId =>
                    operationKind == "bounded_integer",
                NativeCombatRngEntryV111.InitialUniqueHpSiteId or
                NativeCombatRngEntryV111.ToughEggHatchHpSiteId =>
                    operationKind == "choose_index",
                PrototypeGameEngine.FishingRodUpgradeSiteId =>
                    operationKind == "choose_index",
                _ => false
            },
            "run.treasure_room_relics" =>
                siteId == PrototypeNativeRelicGrabBag.QueueShuffleSiteId &&
                operationKind == "bounded_integer",
            NativePlayerRngV111.RewardsStreamId => siteId switch
            {
                RewardPotionOddsUnitFloatSiteId or
                NativePotionFactoryRarityRollSiteId or
                CardRarityRollUnitFloatSiteId or
                CardRarityUpgradeRollUnitFloatSiteId or
                "relic_grab_bag.rarity.unit_float" =>
                    operationKind == "unit_float",
                NativeCombatGoldSiteId or
                "reward.boss_relic_offer.shuffle_swap_index" or
                "reward.lava_rock.relic_offer.shuffle_swap_index" =>
                    operationKind == "bounded_integer",
                NativePotionFactoryModelSiteId or
                NativeRewardCardModelSiteId or
                "run.potion.fill_empty_slots.model" =>
                    operationKind == "choose_index",
                _ => false
            },
            _ => false
        };

    private static bool SupportsEngineOwnedNativeIndexBoundaryForStream(
        string siteId, string streamId, string operationKind) =>
        (operationKind is "choose_index" or "bounded_integer") &&
        SupportsEngineOwnedNativeBoundaryForStream(
            siteId, streamId, operationKind);

    public static bool IsNativeBoundedIntegerSite(string siteId) =>
        NativeBoundedIntegerSites.Contains(siteId, StringComparer.Ordinal);

    public static bool IsNativeMerchantPriceSite(string siteId) =>
        NativeMerchantPriceSites.Contains(siteId, StringComparer.Ordinal);

    public static bool IsLegacyNativeMerchantFallbackSite(string siteId) =>
        LegacyNativeMerchantFallbackSiteSet.Contains(siteId);

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
            ReferenceNatureCombatRewardSessionSchemaId =
                ReferenceNatureCombatRewardSessionV111.CurrentSchemaId,
            ReferenceNatureCombatRewardSiteIds =
                NativeCombatRewardSessionSites,
            ReferenceNatureMapGenerationSessionSchemaId =
                ReferenceNatureMapGenerationSessionV111.CurrentSchemaId,
            ReferenceNatureMapGenerationSiteIds =
                NativeMapGenerationSessionSites,
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
            ReferenceNatureNativeEventGoldSessionSchemaId =
                ReferenceNatureNativeEventGoldSessionV111.CurrentSchemaId,
            ReferenceNatureNativeEventGoldSiteIds =
                ReferenceNatureNativeEventGoldSessionV111.SessionSiteIds,
            ReferenceNatureNativeEventRelicSessionSchemaId =
                ReferenceNatureNativeEventRelicSessionV111.CurrentSchemaId,
            ReferenceNatureNativeEventRelicSiteIds =
                ReferenceNatureNativeEventRelicSessionV111.SessionSiteIds,
            ReferenceNatureNativeShovelSessionSchemaId =
                ReferenceNatureNativeShovelSessionV111.CurrentSchemaId,
            ReferenceNatureNativeShovelSiteIds =
                ReferenceNatureNativeShovelSessionV111.SessionSiteIds,
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
