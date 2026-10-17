namespace Sts2Emulator.Core;

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
                ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId)
            throw new InvalidDataException(
                "Invalid stochastic session coverage schema.");

        ValidateSiteIds(SemanticReferenceNatureSiteIds);
        ValidateSiteIds(ReferenceNatureRunActionSiteIds);
        ValidateSiteIds(ReferenceNatureEventChoiceSiteIds);
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
        "potion.uncommon_model.index_draw",
        "relic.acquisition.random_rare_card.model",
        "relic.acquisition.random_potion.model",
        "run.potion.fill_empty_slots.model",
        "reward.card_transform.replacement.index_draw",
        "relic.unowned_random_selection.index_draw"
    ];

    private static readonly HashSet<string> SemanticReferenceNatureSiteSet =
        new(SemanticReferenceNatureSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionSiteSet =
        new(RunActionSites, StringComparer.Ordinal);
    private static readonly HashSet<string> RunActionEventEntrySiteSet =
        new(RunActionEventEntrySites, StringComparer.Ordinal);
    private static readonly HashSet<string> ControlledEventChoiceSiteSet =
        new(ControlledEventChoiceSites, StringComparer.Ordinal);
    private static readonly string[] RunActionSessionSites = RunActionSites
        .Concat(RunActionEventEntrySites)
        .Append("relic.unowned_random_selection.index_draw")
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    private static readonly string[] NatureChoiceSites =
        SemanticReferenceNatureSites
            .Concat(RunActionSites)
            .Concat(RunActionEventEntrySites)
            .Concat(ControlledEventChoiceSites)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static string[] SemanticReferenceNatureSiteIds =>
        (string[])SemanticReferenceNatureSites.Clone();
    public static string[] RunActionSiteIds => (string[])RunActionSites.Clone();
    public static string[] RunActionEventEntrySiteIds =>
        (string[])RunActionEventEntrySites.Clone();
    public static string[] ControlledRunActionSiteIds =>
        (string[])RunActionSessionSites.Clone();
    public static string[] ControlledEventChoiceSiteIds =>
        (string[])ControlledEventChoiceSites.Clone();
    public static string[] NatureChoiceSiteIds =>
        (string[])NatureChoiceSites.Clone();

    public static bool IsSemanticReferenceNatureSite(string siteId) =>
        SemanticReferenceNatureSiteSet.Contains(siteId);

    public static bool IsRunActionSite(string siteId) =>
        RunActionSiteSet.Contains(siteId);

    public static bool IsRunActionEventEntrySite(string siteId) =>
        RunActionEventEntrySiteSet.Contains(siteId);

    public static bool IsControlledEventChoiceSite(string siteId) =>
        ControlledEventChoiceSiteSet.Contains(siteId);

    public static StochasticSessionCoverageV111 CreateSessionCoverage()
    {
        var coverage = new StochasticSessionCoverageV111(
            StochasticSessionCoverageV111.CurrentSchemaId,
            ReferenceNatureSessionV111.CurrentSchemaId,
            SemanticReferenceNatureSiteIds,
            ReferenceNatureRunActionSessionV111.CurrentSchemaId,
            ControlledRunActionSiteIds,
            ReferenceNatureControlledEventChoiceSessionV111.CurrentSchemaId,
            ControlledEventChoiceSiteIds);
        coverage.Validate();
        return coverage;
    }
}
