using System.Text.Json;

namespace Sts2Emulator.Core;

public sealed record V111InventoryItem(
    string Kind, string Id, string Scope, string? Pool, string? Act,
    string? Source, string? SourceSha256, string[]? Variants,
    string[]? Branches, bool BranchInventoryComplete = false,
    string[]? Encounters = null, string[]? Events = null, string[]? Ancients = null);

public sealed record V111Inventory(
    string Schema, string GameVersion, string GameCommit, string BuildFingerprint,
    string CorpusCommit, string SourceCommit,
    Dictionary<string, string> CorpusBlobs, V111InventoryItem[] Items);

public sealed record V111CoverageItem(
    string Kind, string NativeId, string Scope, string[] EngineIds,
    bool Catalogued, bool Registered, string Implementation, string Integration, string Validation,
    string[] MissingVariants, string[] MissingBranches, string[] Evidence);

public sealed record V111CoverageSummary(
    string Kind, int Eligible, int ScopeReviewRequired, int Excluded,
    int Catalogued, int Implemented, int Integrated, int Validated);

public sealed record V111CoverageReport(
    string Schema, string GameBuild, string InventoryHash,
    string CorpusCommit, string SourceCommit,
    V111CoverageSummary[] Summary, V111CoverageItem[] Items,
    bool ReleaseReady);

/// <summary>
/// Coverage is derived from the pinned inventory and executable registries.
/// Registration and prototype tests never imply native parity. Variant/branch
/// evidence remains missing until an explicit native fixture is recorded.
/// </summary>
public static class V111Coverage
{
    public const string SchemaId = "sts2-v111-coverage-v1";
    public static V111Inventory LoadInventory()
    {
        using var stream = typeof(V111Coverage).Assembly.GetManifestResourceStream(
            "Sts2Emulator.Core.v111-inventory.json")
            ?? throw new InvalidOperationException("Pinned v111 inventory is missing.");
        var inventory = JsonSerializer.Deserialize<V111Inventory>(stream, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
        }) ?? throw new InvalidDataException("Invalid v111 inventory.");
        if (inventory.Schema != "sts2-v111-inventory-v1"
            || inventory.BuildFingerprint != V111Build.Fingerprint
            || inventory.GameVersion != V111Build.Version
            || inventory.GameCommit != V111Build.Commit
            || inventory.CorpusCommit != V111Build.CorpusCommit
            || inventory.SourceCommit != V111Build.SourceCommit
            || inventory.Items.Select(x => (x.Kind, x.Id)).Distinct().Count() != inventory.Items.Length)
        {
            throw new InvalidDataException("Inventory identity or unique keys do not match the v111 pin.");
        }
        return inventory;
    }

    public static V111CoverageReport Create()
    {
        var inventory = LoadInventory();
        var items = inventory.Items.Select(Describe).ToArray();
        var summary = items.GroupBy(x => x.Kind).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(group => new V111CoverageSummary(
                group.Key, group.Count(x => x.Scope == "single-player"),
                group.Count(x => x.Scope == "review-required"),
                group.Count(x => x.Scope == "multiplayer-only"),
                group.Count(x => x.Scope == "single-player" && x.Catalogued),
                group.Count(x => x.Scope == "single-player" && x.Implementation == "implemented"),
                group.Count(x => x.Scope == "single-player" && x.Integration == "integrated"),
                group.Count(x => x.Scope == "single-player" && x.Validation == "native-parity")))
            .ToArray();
        var releaseReady = items.All(item => item.Scope == "multiplayer-only"
            || item.Scope == "single-player" && item.Implementation == "implemented"
                && item.Integration == "integrated" && item.Validation == "native-parity"
                && item.MissingVariants.Length == 0 && item.MissingBranches.Length == 0);
        return new V111CoverageReport(SchemaId, V111Build.Id, CanonicalJson.Sha256(inventory),
            inventory.CorpusCommit, inventory.SourceCommit, summary, items,
            ReleaseReady: releaseReady);
    }

    private static V111CoverageItem Describe(V111InventoryItem item)
    {
        var normalized = Normalize(item.Id);
        var ids = item.Kind switch
        {
            "cards" => PrototypeContent.Cards.Values
                .Where(card => Matches(item, card.Id, card.Name)).Select(card => card.Id).ToArray(),
            "potions" => PrototypeContent.Potions.Values
                .Where(potion => Matches(item, potion.Id, potion.Name)).Select(potion => potion.Id).ToArray(),
            "relics" => PrototypeContent.Relics.Values
                .Where(relic => Matches(item, relic.Id, relic.Name)).Select(relic => relic.Id).ToArray(),
            "powers" => PrototypeContent.Powers.Values
                .Where(power => Matches(item, power.Id, power.Name)).Select(power => power.Id).ToArray(),
            "monsters" => PrototypeContent.Enemies.Values
                .Where(enemy => Matches(item, enemy.Id, enemy.Name)).Select(enemy => enemy.Id).ToArray(),
            "encounters" => PrototypeContent.Encounters
                .Where(encounter => Normalize(encounter.Id.Split('.').Last()) == normalized)
                .Select(encounter => encounter.Id).ToArray(),
            "events" => PrototypeContent.Events.Values
                .Where(evt => Matches(item, evt.Id, evt.Name)).Select(evt => evt.Id).ToArray(),
            "characters" => V111Characters.All.Where(character => Normalize(character.Id) == normalized)
                .Select(character => character.Id).ToArray(),
            "acts" => Enum.GetNames<ActIdentity>().Where(act => Normalize(act) == normalized),
            "enchantments" => Enum.GetNames<PrototypeCardEnchantmentKind>().Where(kind => Normalize(kind) == normalized),
            "afflictions" => Enum.GetNames<PrototypeCardAfflictionKind>().Where(kind => Normalize(kind) == normalized),
            _ => Array.Empty<string>()
        };
        var engineIds = ids.Order(StringComparer.Ordinal).ToArray();
        var implementation = engineIds.Length == 0 ? "missing" : "partial";
        var integration = "missing";
        if (item.Scope == "single-player" && item.Kind == "cards")
        {
            if (engineIds.Any(id => PrototypeContent.Card(id).MechanicsImplemented
                && !PrototypeContent.Card(id).MultiplayerOnly))
            {
                implementation = "implemented";
                // Ordinary acquisition is established by pool membership. Event
                // or generated cards need separate path evidence, not a count.
                if (engineIds.Any(id => PrototypeContent.RewardCardPool.Contains(id)
                    || PrototypeContent.StartingDeck.Contains(id)
                    || PrototypeColorlessCards.ImplementedShopPool.Contains(id)))
                    integration = "integrated";
            }
            if (engineIds.Contains(NativeDarvEventV111.CurseOfTheBellCardId,
                    StringComparer.Ordinal)
                && NativeDarvEventV111.SupportedRelicIds.Contains(
                    NativeDarvEventV111.CallingBellId, StringComparer.Ordinal))
                integration = "integrated";
            if (engineIds.Contains(NativeDarvEventV111.DustyTomeCardId,
                    StringComparer.Ordinal)
                && NativeDarvEventV111.SupportedRelicIds.Contains(
                    NativeDarvEventV111.DustyTomeId, StringComparer.Ordinal)
                && PrototypeContent.Card(
                    NativeDarvEventV111.DustyTomeCardId).MaxUpgradeLevel > 0)
                integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "encounters")
        {
            var pools = PrototypeContent.OvergrowthWeakEncounterPool
                .Concat(PrototypeContent.OvergrowthNormalEncounterPool)
                .Concat(PrototypeContent.OvergrowthEliteEncounterPool)
                .Concat(PrototypeContent.OvergrowthBossEncounterPool)
                .Concat(PrototypeNativeUnderdocks.SupportedWeakEncounterIds)
                .Concat(PrototypeNativeUnderdocks.SupportedNormalEncounterIds)
                .Concat(PrototypeNativeUnderdocks.SupportedEliteEncounterIds)
                .Concat(PrototypeNativeUnderdocks.NativeBossEncounterIds)
                .Concat(ExperimentalSilentThreeActRouteV111.HiveWeak)
                .Concat(ExperimentalSilentThreeActRouteV111.HiveNormal)
                .Concat(ExperimentalSilentThreeActRouteV111.HiveElites)
                .Concat(ExperimentalSilentThreeActRouteV111.HiveBosses)
                .Concat(ExperimentalSilentThreeActRouteV111.GloryWeak)
                .Concat(ExperimentalSilentThreeActRouteV111.GloryNormal)
                .Concat(ExperimentalSilentThreeActRouteV111.GloryElites)
                .Concat(ExperimentalSilentThreeActRouteV111.GloryBosses)
                .ToHashSet(StringComparer.Ordinal);
            if (engineIds.Any(pools.Contains))
            {
                implementation = "implemented";
                integration = "integrated";
            }
        }
        if (item.Scope == "single-player" && item.Kind == "events"
            && engineIds.Length > 0)
        {
            implementation = "partial";
            if (engineIds.Any(IsIntegratedEvent))
                integration = "integrated";
        }
        var missingBranches = MissingBranches(item);
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "DARV" && missingBranches.Length == 0
            && engineIds.Contains(NativeDarvEventV111.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "AROMA_OF_CHAOS" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.event.aroma_of_chaos",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "BRAIN_LEECH" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeBrainLeech.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "SELF_HELP_BOOK" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeSelfHelpBook.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "POTION_COURIER" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativePotionCourier.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "MORPHIC_GROVE" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.event.morphic_grove",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "JUNGLE_MAZE_ADVENTURE"
            && missingBranches.Length == 0
            && engineIds.Contains(
                "proto.native.event.jungle_maze_adventure",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "BUGSLAYER" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeLaterActs.HiveBugslayerId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "WELLSPRING" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.event.wellspring",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "WHISPERING_HOLLOW" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.event.whispering_hollow",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "LUMINOUS_CHOIR" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.event.luminous_choir",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "SAPPHIRE_SEED" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.event.sapphire_seed",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "UNREST_SITE" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.event.unrest_site",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "TABLET_OF_TRUTH" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeTabletOfTruth.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "BYRDONIS_NEST" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.event.byrdonis_nest",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "WOOD_CARVINGS" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.event.wood_carvings",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "SUNKEN_STATUE" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.event.sunken_statue",
                StringComparer.Ordinal)
            && engineIds.Contains("proto.native.underdocks.sunken_statue",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "DOORS_OF_LIGHT_AND_DARK"
            && missingBranches.Length == 0
            && engineIds.Contains(
                "proto.native.underdocks.doors_of_light_and_dark",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "DROWNING_BEACON" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.underdocks.drowning_beacon",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "SUNKEN_TREASURY" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.underdocks.sunken_treasury",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "AMALGAMATOR" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeHiveEvents.AmalgamatorId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "COLOSSAL_FLOWER" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeHiveEvents.ColossalFlowerId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "FIELD_OF_MAN_SIZED_HOLES"
            && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeHiveEvents.FieldOfManSizedHolesId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "LOST_WISP" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeHiveEvents.LostWispId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "SPIRIT_GRAFTER" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeHiveEvents.SpiritGrafterId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "GRAVE_OF_THE_FORGOTTEN"
            && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeGloryEvents.GraveOfTheForgottenId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "HUNGRY_FOR_MUSHROOMS"
            && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeGloryEvents.HungryForMushroomsId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "REFLECTIONS" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeGloryEvents.ReflectionsId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "ROUND_TEA_PARTY" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeGloryEvents.RoundTeaPartyId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "TINKER_TIME" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeGloryEvents.TinkerTimeId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "DENSE_VEGETATION" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeDenseVegetation.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "WATERLOGGED_SCRIPTORIUM"
            && missingBranches.Length == 0
            && engineIds.Contains(
                "proto.native.underdocks.waterlogged_scriptorium",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "TRASH_HEAP" && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeUnderdocksTrashHeap.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "ABYSSAL_BATHS" && missingBranches.Length == 0
            && engineIds.Contains("proto.native.underdocks.abyssal_baths",
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "ENDLESS_CONVEYOR"
            && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeEndlessConveyor.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "PUNCH_OFF" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativePunchOff.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "relics"
            && engineIds.Any(id =>
            {
                var relic = PrototypeContent.Relic(id);
                var blackStarIntegrated = relic.ExtraEliteRelicRewardCount > 0
                    && PrototypeContent.RelicPool.Contains(id,
                        StringComparer.Ordinal)
                    && PrototypeNativeRelicGrabBag.TryGetRarity(id, out _);
                var ectoplasmIntegrated = relic.PreventGoldGain
                    && relic.EnergyPerTurnBonus > 0
                    && PrototypeContent.BossRelicPool.Contains(id,
                        StringComparer.Ordinal);
                var callingBellIntegrated = id == NativeDarvEventV111.CallingBellId
                    && PrototypeContent.Card(
                        NativeDarvEventV111.CurseOfTheBellCardId).Eternal
                    && NativeDarvEventV111.SupportedRelicIds.Contains(id,
                        StringComparer.Ordinal);
                var dustyTomeIntegrated = id == NativeDarvEventV111.DustyTomeId
                    && NativeDarvEventV111.SupportedRelicIds.Contains(id,
                        StringComparer.Ordinal)
                    && PrototypeContent.Card(
                        NativeDarvEventV111.DustyTomeCardId).MaxUpgradeLevel > 0;
                var pandorasBoxIntegrated = id == NativeDarvEventV111.PandorasBoxId
                    && NativeDarvEventV111.SupportedRelicIds.Contains(id,
                        StringComparer.Ordinal)
                    && PrototypeContent.NativeSilentCardPool.Any(cardId =>
                        PrototypeContent.Card(cardId).Tags?.Any(tag =>
                            tag is "Strike" or "Defend") == true);
                return blackStarIntegrated || ectoplasmIntegrated
                    || callingBellIntegrated || dustyTomeIntegrated
                    || pandorasBoxIntegrated;
            }))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "WINGED_BOOTS"
            && engineIds.Contains(PrototypeNativeWingedBoots.RelicId,
                StringComparer.Ordinal))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "TWISTED_FUNNEL"
            && engineIds.Contains(PrototypeNativeSilentRelics.TwistedFunnelId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSilentRelics.TwistedFunnelId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSilentRelics.TwistedFunnelId, out var rarity)
            && rarity == PrototypeRelicRarity.Uncommon)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "NINJA_SCROLL"
            && engineIds.Contains(PrototypeNativeSilentRelics.NinjaScrollId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSilentRelics.NinjaScrollId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSilentRelics.NinjaScrollId, out var ninjaRarity)
            && ninjaRarity == PrototypeRelicRarity.Shop)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "HELICAL_DART"
            && engineIds.Contains(PrototypeNativeSilentRelics.HelicalDartId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSilentRelics.HelicalDartId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSilentRelics.HelicalDartId, out var dartRarity)
            && dartRarity == PrototypeRelicRarity.Rare)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "multiplayer-only")
        {
            implementation = "excluded";
            integration = "excluded";
        }
        return new V111CoverageItem(item.Kind, item.Id, item.Scope, engineIds,
            true, engineIds.Length > 0, implementation, integration, "unverified",
            item.Variants ?? [],
            missingBranches,
            item.Source is null ? [] : [$"{V111Build.SourceCommit}:{item.Source}#{item.SourceSha256}"]);
    }

    private static string[] MissingBranches(V111InventoryItem item)
    {
        if (item.Kind == "events" && item.Id == "DARV")
        {
            var supported = NativeDarvEventV111.SupportedRelicIds
                .Select(id => $"relic/{id.Split('.').Last().ToUpperInvariant()}")
                .ToHashSet(StringComparer.Ordinal);
            return (item.Branches ?? []).Where(branch => !supported.Contains(branch))
                .ToArray();
        }
        return item.BranchInventoryComplete ? [] : item.Branches ?? [];
    }

    private static bool Matches(V111InventoryItem item, string engineId, string name)
    {
        var key = Normalize(item.Id);
        var last = Normalize(engineId.Split('.').Last());
        if (item.Kind == "cards" && (key.StartsWith("STRIKE", StringComparison.Ordinal)
            || key.StartsWith("DEFEND", StringComparison.Ordinal)))
        {
            return engineId.StartsWith($"proto.{item.Pool}.", StringComparison.Ordinal)
                && key == last + Normalize(item.Pool ?? "");
        }
        return key == last || key == Normalize(name);
    }

    private static bool IsIntegratedEvent(string eventId)
    {
        if (eventId == PrototypeNativeOvergrowthEvents.NeowEventId)
            return true;
        if (PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(eventId))
            return true;
        if (PrototypeNativeUnderdocksEvents.IsSupported(eventId))
            return true;
        if (ExperimentalSilentThreeActRouteV111.ContainsEvent(eventId, 2)
            || ExperimentalSilentThreeActRouteV111.ContainsEvent(eventId, 3))
            return true;
        if (eventId == PrototypeNativeHiveEvents.WarHistorianRepyId)
            return true;

        // Configured three-act runs prepare these act-local assignments and
        // execute the existing opening handlers at each transition. Orobas's
        // foreign-card options are explicitly deferred; implementation and
        // branch-completeness claims remain separate from this routing claim.
        return eventId is NativePaelRewardsV111.PaelId
            or NativeAncientRewardsV111.TezcataraId
            or NativeOrobasEventV111.EventId
            or NativeDarvEventV111.EventId
            or NativeTanxEventV111.EventId
            or NativeVakuuEventV111.EventId
            or NativeNonupeipeEventV111.EventId;
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
