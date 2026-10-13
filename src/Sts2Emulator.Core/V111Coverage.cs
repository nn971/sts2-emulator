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
                .Where(encounter =>
                    Normalize(encounter.Id.Split('.').Last()) == normalized
                    || (item.Id == "PUNCH_OFF_EVENT_ENCOUNTER"
                        && encounter.Id
                            == PrototypeNativePunchOff.EncounterId)
                    || (item.Id == "DENSE_VEGETATION_EVENT_ENCOUNTER"
                        && encounter.Id
                            == PrototypeNativeDenseVegetation.EncounterId)
                    || (item.Id == "FAKE_MERCHANT_EVENT_ENCOUNTER"
                        && encounter.Id
                            == PrototypeNativeFakeMerchant.EncounterId)
                    || (item.Id == "BATTLEWORN_DUMMY_EVENT_ENCOUNTER"
                        && encounter.Id is
                            PrototypeNativeGloryEvents.BattlewornV1EncounterId
                            or PrototypeNativeGloryEvents.BattlewornV2EncounterId
                            or PrototypeNativeGloryEvents.BattlewornV3EncounterId)
                    || (item.Id == "THE_ARCHITECT_EVENT_ENCOUNTER"
                        && encounter.Id
                            == PrototypeNativeArchitectEnding.EncounterId))
                .Select(encounter => encounter.Id).ToArray(),
            "events" => PrototypeContent.Events.Values
                .Where(evt => Matches(item, evt.Id, evt.Name)).Select(evt => evt.Id).ToArray(),
            "rest_site_options" => RestSiteOptionEngineIds(item.Id),
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
        var hasArchaicToothPath = item.Scope == "single-player"
            && (item.Kind is "cards" or "relics")
            && (item.Id is "SUPPRESS" or "ARCHAIC_TOOTH")
            && engineIds.Contains(item.Id == "SUPPRESS"
                ? PrototypeNativeOrobasRelics.SuppressId
                : PrototypeNativeOrobasRelics.ArchaicToothId,
                StringComparer.Ordinal)
            && HasArchaicToothSuppressPath();
        var hasSuppressAcquisitionPath = item.Scope == "single-player"
            && item.Kind == "cards" && item.Id == "SUPPRESS"
            && engineIds.Contains(PrototypeNativeOrobasRelics.SuppressId,
                StringComparer.Ordinal) && hasArchaicToothPath;
        var hasTouchOrobasPath = item.Scope == "single-player"
            && item.Kind == "relics"
            && (item.Id is "TOUCH_OF_OROBAS" or "RING_OF_THE_DRAKE")
            && engineIds.Contains(item.Id == "TOUCH_OF_OROBAS"
                ? PrototypeNativeOrobasRelics.TouchOfOrobasId
                : PrototypeNativeOrobasRelics.RingOfTheDrakeId,
                StringComparer.Ordinal)
            && HasTouchOrobasRingUpgradePath();
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
            if (hasSuppressAcquisitionPath)
                integration = "integrated";
        }
        if (item.Scope == "single-player"
            && item.Kind == "cards"
            && item.Id == "SPOILS_MAP"
            && engineIds.Contains(
                PrototypeNativeSpoilsMap.CardId,
                StringComparer.Ordinal)
            && HasSpoilsMapPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player"
            && item.Kind == "cards"
            && (item.Id is "DOWSING" or "ABUNDANCE")
            && HasDowsingRodQuestPath()
            && ((item.Id == "DOWSING"
                    && engineIds.Contains(
                        "proto.native.neow.dowsing",
                        StringComparer.Ordinal)
                    && PrototypeContent.Card(
                        "proto.native.neow.dowsing") is
                        { MechanicsImplemented: true, Unplayable: true,
                            MaxUpgradeLevel: 0 })
                || (item.Id == "ABUNDANCE"
                    && engineIds.Contains(
                        "proto.native.neow.abundance",
                        StringComparer.Ordinal)
                    && PrototypeContent.Card(
                        "proto.native.neow.abundance") is
                        { MechanicsImplemented: true, MaxUpgradeLevel: 1 }
                    && PrototypeContent.Card(
                        "proto.native.neow.abundance").Effects.Any(effect =>
                        effect.Kind
                            == PrototypeCombatEffectKind.ChooseGeneratedCards
                            && effect.Amount == 3
                            && effect.GeneratedChoiceCardsUpgraded
                            && effect.GeneratedChoiceMustPick))))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "potions"
            && (item.Pool is "silent" or "shared")
            && engineIds.Length == 1
            && PrototypeNativePotionShop.NativePotionPoolOrder.Contains(
                engineIds[0], StringComparer.Ordinal)
            && PrototypeContent.PotionPool.Contains(engineIds[0],
                StringComparer.Ordinal))
        {
            _ = PrototypeContent.Potion(engineIds[0]);
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "potions"
            && item.Id == "GLOWWATER_POTION"
            && engineIds.Contains(
                "proto.native.underdocks.glowwater_potion",
                StringComparer.Ordinal)
            && PrototypeNativeUnderdocksEvents.IsSupported(
                "proto.native.underdocks.drowning_beacon")
            && PrototypeNativeUnderdocksEvents.UsesNativePotionOffer(
                "proto.native.underdocks.drowning_beacon")
            && PrototypeContent.Event(
                "proto.native.underdocks.drowning_beacon").Choices
                .Any(choice => choice.Id == "bottle"
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainPotion
                        && effect.PotionId
                            == "proto.native.underdocks.glowwater_potion"))
            && PrototypeContent.Potion(
                "proto.native.underdocks.glowwater_potion").Effects
                .SequenceEqual(
                [
                    new(PrototypeCombatEffectKind.ExhaustHand, 0),
                    new(PrototypeCombatEffectKind.DrawCards, 10)
                ]))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && ((item.Id == "ARCHAIC_TOOTH" && hasArchaicToothPath)
                || ((item.Id is "TOUCH_OF_OROBAS" or "RING_OF_THE_DRAKE")
                    && hasTouchOrobasPath)))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player"
            && item.Kind == "relics"
            && item.Id == "DOWSING_ROD"
            && engineIds.Contains(
                PrototypeNativeOvergrowthEvents.NeowRelicId("DowsingRod"),
                StringComparer.Ordinal)
            && HasDowsingRodQuestPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "CIRCLET"
            && engineIds.Contains("proto.relic.circlet",
                StringComparer.Ordinal)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeRelicTrader.EventId, 2))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "CHOSEN_CHEESE"
            && engineIds.Contains(
                PrototypeNativeRoomFullOfCheese.ChosenCheeseId,
                StringComparer.Ordinal)
            && PrototypeContent.Relic(
                PrototypeNativeRoomFullOfCheese.ChosenCheeseId)
                .MaxHpOnCombatVictory == 1
            && PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(
                PrototypeNativeRoomFullOfCheese.EventId)
            && PrototypeNativeUnderdocksEvents.IsSupported(
                PrototypeNativeRoomFullOfCheese.EventId))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id is "SWORD_OF_STONE" or "SWORD_OF_JADE"
            && HasSwordOfStoneJadePath(item.Id))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "VENERABLE_TEA_SET"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.VenerableTeaSetId,
                StringComparer.Ordinal)
            && HasVenerableTeaSetPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "LIZARD_TAIL"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.LizardTailId,
                StringComparer.Ordinal)
            && HasLizardTailPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "SLING_OF_COURAGE"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.SlingOfCourageId,
                StringComparer.Ordinal)
            && HasSlingOfCouragePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "RED_MASK"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.RedMaskId,
                StringComparer.Ordinal)
            && HasRedMaskPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "STONE_CALENDAR"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.StoneCalendarId,
                StringComparer.Ordinal)
            && HasStoneCalendarPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "POLLINOUS_CORE"
            && engineIds.Contains(
                PrototypeNativeHiveEvents.PollinousCoreId,
                StringComparer.Ordinal)
            && HasPollinousCorePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "HISTORY_COURSE"
            && engineIds.Contains(
                PrototypeNativeHiveEvents.HistoryCourseId,
                StringComparer.Ordinal)
            && HasHistoryCoursePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "FORGOTTEN_SOUL"
            && engineIds.Contains(
                PrototypeNativeGloryEvents.ForgottenSoulId,
                StringComparer.Ordinal)
            && HasForgottenSoulPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "BLESSED_ANTLER"
            && engineIds.Contains(
                NativeGloryAncientsV111.BlessedAntlerId,
                StringComparer.Ordinal)
            && HasBlessedAntlerPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "BEAUTIFUL_BRACELET"
            && engineIds.Contains(
                NativeGloryAncientsV111.BeautifulBraceletId,
                StringComparer.Ordinal)
            && HasBeautifulBraceletPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "FIDDLE"
            && engineIds.Contains(
                NativeGloryAncientsV111.FiddleId,
                StringComparer.Ordinal)
            && HasFiddlePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "BLOOD_SOAKED_ROSE"
            && engineIds.Contains(
                NativeGloryAncientsV111.BloodSoakedRoseId,
                StringComparer.Ordinal)
            && HasBloodSoakedRosePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "WHISPERING_EARRING"
            && engineIds.Contains(
                NativeGloryAncientsV111.WhisperingEarringId,
                StringComparer.Ordinal)
            && HasWhisperingEarringPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "DIAMOND_DIADEM"
            && engineIds.Contains(
                NativeGloryAncientsV111.DiamondDiademId,
                StringComparer.Ordinal)
            && HasDiamondDiademPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "BIIIG_HUG"
            && engineIds.Contains(
                NativeAncientRewardsV111.BiiigHugId,
                StringComparer.Ordinal)
            && HasBiiigHugPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "VERY_HOT_COCOA"
            && engineIds.Contains(
                NativeAncientRewardsV111.VeryHotCocoaId,
                StringComparer.Ordinal)
            && HasVeryHotCocoaPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "YUMMY_COOKIE"
            && engineIds.Contains(
                NativeAncientRewardsV111.YummyCookieId,
                StringComparer.Ordinal)
            && HasYummyCookiePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "TOASTY_MITTENS"
            && engineIds.Contains(
                NativeAncientRewardsV111.ToastyMittensId,
                StringComparer.Ordinal)
            && HasToastyMittensPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "STORYBOOK"
            && engineIds.Contains(
                NativeAncientRewardsV111.StorybookId,
                StringComparer.Ordinal)
            && HasStorybookPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "cards"
            && item.Id == "BRIGHTEST_FLAME"
            && engineIds.Contains(
                NativeAncientRewardsV111.BrightestFlameId,
                StringComparer.Ordinal)
            && HasStorybookPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && engineIds.Length == 1
            && HasSupportedBossRelicPath(item.Id, engineIds[0]))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player"
            && item.Kind == "enchantments"
            && item.Id == "VIGOROUS"
            && engineIds.Contains("Vigorous", StringComparer.Ordinal)
            && PrototypeNativeStoneOfAllTime.Definition.Choices.Any(choice =>
                choice.DeckChoice?.EnchantmentKind
                    == PrototypeCardEnchantmentKind.Vigorous
                && choice.DeckChoice.EnchantmentAmount == 8)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeStoneOfAllTime.EventId, 2))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player"
            && item.Kind == "enchantments"
            && item.Id == "CORRUPTED"
            && engineIds.Contains("Corrupted", StringComparer.Ordinal)
            && HasCorruptedSymbiotePath())
        {
            implementation = "implemented";
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
            if (item.Id == "PUNCH_OFF_EVENT_ENCOUNTER"
                && engineIds.Contains(
                    PrototypeNativePunchOff.EncounterId,
                    StringComparer.Ordinal)
                && PrototypeNativeUnderdocksEvents.IsSupported(
                    PrototypeNativePunchOff.EventId)
                && PrototypeNativePunchOff.Encounter.Weight == 0
                && PrototypeNativePunchOff.Encounter.EnemyIds.SequenceEqual(
                    ["proto.enemy.punch_construct",
                     "proto.enemy.punch_construct"],
                    StringComparer.Ordinal))
            {
                implementation = "implemented";
                integration = "integrated";
            }
            if (item.Id == "DENSE_VEGETATION_EVENT_ENCOUNTER"
                && engineIds.Contains(
                    PrototypeNativeDenseVegetation.EncounterId,
                    StringComparer.Ordinal)
                && PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(
                    PrototypeNativeDenseVegetation.EventId)
                && PrototypeContent.Encounter(
                    PrototypeNativeDenseVegetation.EncounterId).Weight == 0
                && PrototypeContent.Encounter(
                    PrototypeNativeDenseVegetation.EncounterId).EnemyIds
                    .SequenceEqual(
                        ["proto.enemy.wriggler", "proto.enemy.wriggler",
                         "proto.enemy.wriggler", "proto.enemy.wriggler"],
                        StringComparer.Ordinal))
            {
                implementation = "implemented";
                integration = "integrated";
            }
            if (item.Id == "FAKE_MERCHANT_EVENT_ENCOUNTER"
                && engineIds.Contains(
                    PrototypeNativeFakeMerchant.EncounterId,
                    StringComparer.Ordinal)
                && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                    PrototypeNativeFakeMerchant.EventId, 2)
                && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                    PrototypeNativeFakeMerchant.EventId, 3)
                && PrototypeNativeFakeMerchant.Encounter.Weight == 0
                && PrototypeNativeFakeMerchant.Encounter.EnemyIds
                    .SequenceEqual(
                        [PrototypeNativeFakeMerchant.MonsterId],
                        StringComparer.Ordinal))
            {
                implementation = "implemented";
                integration = "integrated";
            }
            if (item.Id == "MYSTERIOUS_KNIGHT_EVENT_ENCOUNTER"
                && engineIds.Contains(
                    PrototypeNativeHiveEvents.MysteriousKnightEncounterId,
                    StringComparer.Ordinal)
                && PrototypeNativeHiveEvents.IsNativeEvent(
                    PrototypeNativeHiveEvents.LanternKeyEventId)
                && PrototypeContent.Encounter(
                    PrototypeNativeHiveEvents.MysteriousKnightEncounterId)
                    .Weight == 0
                && PrototypeContent.Encounter(
                    PrototypeNativeHiveEvents.MysteriousKnightEncounterId)
                    .EnemyIds.SequenceEqual(
                        [PrototypeNativeHiveEvents.MysteriousKnightId],
                        StringComparer.Ordinal))
            {
                implementation = "implemented";
                integration = "integrated";
            }
            if (item.Id == "BATTLEWORN_DUMMY_EVENT_ENCOUNTER"
                && engineIds.SequenceEqual(
                    new[]
                    {
                        PrototypeNativeGloryEvents.BattlewornV1EncounterId,
                        PrototypeNativeGloryEvents.BattlewornV2EncounterId,
                        PrototypeNativeGloryEvents.BattlewornV3EncounterId
                    }.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal)
                && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                    PrototypeNativeGloryEvents.BattlewornDummyId, 3)
                && PrototypeContent.Event(
                    PrototypeNativeGloryEvents.BattlewornDummyId).Choices
                    .Select(choice => choice.Id)
                    .SequenceEqual(["setting_1", "setting_2", "setting_3"])
                && PrototypeContent.Power(
                    PrototypeNativeGloryEvents.BattlewornTimeLimitId)
                    .EnemyStacksDecayAtSideTurnEnd == 1
                && new[]
                    {
                        PrototypeNativeGloryEvents.BattleFriendV1Id,
                        PrototypeNativeGloryEvents.BattleFriendV2Id,
                        PrototypeNativeGloryEvents.BattleFriendV3Id
                    }
                    .All(enemyId => PrototypeContent.Enemy(enemyId)
                        .StartingPowers?.Any(power =>
                            power.PowerId
                                == PrototypeNativeGloryEvents.BattlewornTimeLimitId
                            && power.Stacks == 3) == true))
            {
                // The abstract source encounter only persists RanOutOfTime.
                // The emulator stores the same escape outcome on its enemy.
                implementation = "implemented";
                integration = "integrated";
            }
            if (item.Id == "THE_ARCHITECT_EVENT_ENCOUNTER"
                && engineIds.Contains(
                    PrototypeNativeArchitectEnding.EncounterId,
                    StringComparer.Ordinal)
                && PrototypeNativeArchitectEnding.Definition
                    .CanonicalEncounterId
                    == PrototypeNativeArchitectEnding.EncounterId
                && PrototypeContent.Encounter(
                    PrototypeNativeArchitectEnding.EncounterId) is
                    { RoomType: PrototypeRoomType.Event, Weight: 0 }
                && PrototypeContent.Encounter(
                    PrototypeNativeArchitectEnding.EncounterId).EnemyIds
                    .SequenceEqual(
                        [PrototypeNativeArchitectEnding.ActorId],
                        StringComparer.Ordinal)
                && PrototypeContent.Enemy(
                    PrototypeNativeArchitectEnding.ActorId).MaxHp == 9999
                && PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                    PrototypeNativeArchitectEnding.EventId) == false
                && PrototypeContent.Event(
                    PrototypeNativeArchitectEnding.EventId).Choices
                    .Select(choice => choice.Id)
                    .SequenceEqual(
                        [PrototypeNativeArchitectEnding.ProceedChoiceId]))
            {
                implementation = "implemented";
                integration = "integrated";
            }
            (string EncounterId, string EnemyId, string ChoiceId)? battleworn =
                item.Id switch
                {
                    "BATTLEWORN_DUMMY_EVENT_V1_ENCOUNTER" =>
                        (PrototypeNativeGloryEvents.BattlewornV1EncounterId,
                         PrototypeNativeGloryEvents.BattleFriendV1Id,
                         "setting_1"),
                    "BATTLEWORN_DUMMY_EVENT_V2_ENCOUNTER" =>
                        (PrototypeNativeGloryEvents.BattlewornV2EncounterId,
                         PrototypeNativeGloryEvents.BattleFriendV2Id,
                         "setting_2"),
                    "BATTLEWORN_DUMMY_EVENT_V3_ENCOUNTER" =>
                        (PrototypeNativeGloryEvents.BattlewornV3EncounterId,
                         PrototypeNativeGloryEvents.BattleFriendV3Id,
                         "setting_3"),
                    _ => null
                };
            if (battleworn is { } training
                && engineIds.Contains(
                    training.EncounterId,
                    StringComparer.Ordinal)
                && PrototypeContent.Event(
                    PrototypeNativeGloryEvents.BattlewornDummyId).Choices
                    .Any(choice => choice.Id == training.ChoiceId)
                && PrototypeContent.Encounter(training.EncounterId).Weight == 0
                && PrototypeContent.Encounter(training.EncounterId).EnemyIds
                    .SequenceEqual([training.EnemyId], StringComparer.Ordinal))
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
        if (item.Scope == "single-player" && item.Kind == "monsters"
            && item.Id == "ARCHITECT"
            && engineIds.Contains(
                PrototypeNativeArchitectEnding.ActorId,
                StringComparer.Ordinal)
            && IsIntegratedEvent(PrototypeNativeArchitectEnding.EventId)
            && PrototypeContent.Event(
                PrototypeNativeArchitectEnding.EventId).CanonicalEncounterId
                == PrototypeNativeArchitectEnding.EncounterId
            && PrototypeContent.Encounter(
                PrototypeNativeArchitectEnding.EncounterId) is
                { RoomType: PrototypeRoomType.Event, Weight: 0 } encounter
            && encounter.EnemyIds.SequenceEqual(
                [PrototypeNativeArchitectEnding.ActorId],
                StringComparer.Ordinal))
        {
            // The route reaches the scene actor, but its animation and
            // prior-win dialogue variants remain outside the headless model.
            integration = "integrated";
        }
        var missingVariants = hasSuppressAcquisitionPath
            ? [] : item.Variants ?? [];
        if (item.Scope == "single-player" && item.Kind == "cards"
            && item.Id == "BRIGHTEST_FLAME"
            && engineIds.Contains(
                NativeAncientRewardsV111.BrightestFlameId,
                StringComparer.Ordinal)
            && HasStorybookPath())
        {
            missingVariants = [];
        }
        var missingBranches = MissingBranches(item);
        if (item.Scope == "single-player" && item.Kind == "acts"
            && HasConfiguredActRoute(item))
        {
            implementation = "implemented";
            integration = "integrated";
        }
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
            && item.Id == "THE_FUTURE_OF_POTIONS"
            && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeFutureOfPotions.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "RELIC_TRADER"
            && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeRelicTrader.EventId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "THE_ARCHITECT"
            && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeArchitectEnding.EventId,
                StringComparer.Ordinal)
            && PrototypeNativeArchitectEnding.Definition.Choices
                .Select(choice => choice.Id)
                .SequenceEqual(
                    [PrototypeNativeArchitectEnding.ProceedChoiceId],
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
            && item.Id == "SLIPPERY_BRIDGE"
            && engineIds.Contains(
                PrototypeNativeSlipperyBridge.EventId,
                StringComparer.Ordinal)
            && HasCompleteSlipperyBridgeBranches(item))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "RANWID_THE_ELDER"
            && engineIds.Contains(
                PrototypeNativeRanwidTheElder.EventId,
                StringComparer.Ordinal)
            && HasCompleteRanwidBranches(item))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "ROOM_FULL_OF_CHEESE"
            && engineIds.Contains(
                PrototypeNativeRoomFullOfCheese.EventId,
                StringComparer.Ordinal)
            && HasCompleteRoomFullOfCheeseBranches(item))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "THIS_OR_THAT" && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeSharedEvents.ThisOrThatId,
                StringComparer.Ordinal))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "SYMBIOTE" && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeSharedEvents.SymbioteId,
                StringComparer.Ordinal)
            && HasCorruptedSymbiotePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "THE_LEGENDS_WERE_TRUE"
            && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeOvergrowthEvents.TheLegendsWereTrueId,
                StringComparer.Ordinal)
            && HasSpoilsMapPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "STONE_OF_ALL_TIME"
            && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeStoneOfAllTime.EventId,
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
            && item.Id == "INFESTED_AUTOMATON"
            && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeHiveEvents.InfestedAutomatonId,
                StringComparer.Ordinal)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeHiveEvents.InfestedAutomatonId, 2)
            && PrototypeContent.Event(
                PrototypeNativeHiveEvents.InfestedAutomatonId).Choices
                .Any(choice => choice.Id == "study")
            && PrototypeContent.Event(
                PrototypeNativeHiveEvents.InfestedAutomatonId).Choices
                .Any(choice => choice.Id == "touch_core"))
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
            && item.Id == "TEA_MASTER" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeGloryEvents.TeaMasterId,
                StringComparer.Ordinal)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeGloryEvents.TeaMasterId, 3)
            && PrototypeNativeGloryEvents.IsEligible(
                PrototypeContent.Event(
                    PrototypeNativeGloryEvents.TeaMasterId),
                new PlayerState(1, 1, 150, [], [], [])))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "BATTLEWORN_DUMMY"
            && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeGloryEvents.BattlewornDummyId,
                StringComparer.Ordinal)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeGloryEvents.BattlewornDummyId, 3)
            && PrototypeContent.Event(
                PrototypeNativeGloryEvents.BattlewornDummyId).Choices
                .Select(choice => choice.Id)
                .ToHashSet(StringComparer.Ordinal)
                .IsSupersetOf(["setting_1", "setting_2", "setting_3"]))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "TRIAL" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeGloryEvents.TrialId,
                StringComparer.Ordinal)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeGloryEvents.TrialId, 3)
            && PrototypeContent.Event(
                PrototypeNativeGloryEvents.TrialId).Choices
                .Select(choice => choice.Id)
                .ToHashSet(StringComparer.Ordinal)
                .IsSupersetOf([
                    "accept", "reject", "double_down",
                    "confirm_abandon", "cancel_abandon",
                    "merchant_guilty", "merchant_innocent",
                    "noble_guilty", "noble_innocent",
                    "nondescript_guilty", "nondescript_innocent"
                ]))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "THE_LANTERN_KEY"
            && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeHiveEvents.LanternKeyEventId,
                StringComparer.Ordinal)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeHiveEvents.LanternKeyEventId, 2)
            && PrototypeContent.Event(
                PrototypeNativeHiveEvents.LanternKeyEventId).Choices
                .Select(choice => choice.Id)
                .ToHashSet(StringComparer.Ordinal)
                .IsSupersetOf(["return_key", "keep_key"]))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "ZEN_WEAVER" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeHiveEvents.ZenWeaverId,
                StringComparer.Ordinal)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeHiveEvents.ZenWeaverId, 2)
            && PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                PrototypeNativeHiveEvents.ZenWeaverId)
            && PrototypeNativeHiveEvents.IsEligible(
                PrototypeContent.Event(
                    PrototypeNativeHiveEvents.ZenWeaverId),
                new PlayerState(1, 1, 125, [], [], []))
            && PrototypeContent.Event(
                PrototypeNativeHiveEvents.ZenWeaverId).Choices
                .Select(choice => choice.Id)
                .ToHashSet(StringComparer.Ordinal)
                .IsSupersetOf(["breathing", "emotional", "arachnid"]))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "TEZCATARA" && missingBranches.Length == 0
            && engineIds.Contains(NativeAncientRewardsV111.TezcataraId,
                StringComparer.Ordinal)
            && IsIntegratedEvent(NativeAncientRewardsV111.TezcataraId)
            && PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                NativeAncientRewardsV111.TezcataraId)
            && NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .Length == NativeAncientRewardsV111.Relics.Length
            && NativeAncientRewardsV111.Relics.All(relic =>
                PrototypeContent.Relic(relic.Id).Id == relic.Id
                && NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                    .Any(choice => choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainRelic
                        && effect.RelicId == relic.Id)))
            && NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .All(choice =>
                    PrototypeNativeEventCompletionPages.HasTerminalChoice(
                        NativeAncientRewardsV111.TezcataraId, choice.Id)))
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id is ("PAEL" or "TANX" or "VAKUU" or "NONUPEIPE")
            && missingBranches.Length == 0
            && engineIds.Length == 1
            && IsIntegratedEvent(engineIds[0])
            && HasCompleteAncientRelicBranches(engineIds[0], 10))
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
            && item.Id == "WAR_HISTORIAN_REPY"
            && missingBranches.Length == 0
            && engineIds.Contains(
                PrototypeNativeHiveEvents.WarHistorianRepyId,
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
            && item.Id == "SPIRALING_WHIRLPOOL"
            && missingBranches.Length == 0
            && engineIds.Contains(
                "proto.native.underdocks.spiraling_whirlpool",
                StringComparer.Ordinal)
            && PrototypeNativeUnderdocksEvents.IsSupported(
                "proto.native.underdocks.spiraling_whirlpool")
            && PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                "proto.native.underdocks.spiraling_whirlpool")
            && PrototypeNativeUnderdocksEvents.IsEligible(
                PrototypeContent.Event(
                    "proto.native.underdocks.spiraling_whirlpool"),
                new PlayerState(1, 1, 0,
                    [new CardInstance(1, "proto.silent.strike", 0,
                        PrototypeJson.EmptyObject())], [], []))
            && PrototypeContent.Event(
                "proto.native.underdocks.spiraling_whirlpool").Choices
                .Any(choice => choice.Id == "observe"
                    && choice.DeckChoice is
                    {
                        Kind: PrototypePersistentDeckChoiceKind.Enchant,
                        Selections: 1,
                        EnchantmentKind:
                            PrototypeCardEnchantmentKind.Spiral,
                        BasicCardsOnly: true
                    })
            && PrototypeContent.Event(
                "proto.native.underdocks.spiraling_whirlpool").Choices
                .Any(choice => choice.Id == "drink"
                    && choice.Effects.Any(effect =>
                        effect.Kind
                            == PrototypeRunEffectKind.HealPercentMaxHp
                        && effect.Amount == 33)))
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
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "FAKE_MERCHANT"
            && engineIds.Contains(PrototypeNativeFakeMerchant.EventId,
                StringComparer.Ordinal)
            && PrototypeNativeFakeMerchant.StockRelicIds.All(id =>
                PrototypeNativeFakeMerchant.Definition.Choices.Any(choice =>
                    choice.Id == PrototypeNativeFakeMerchant.ChoiceId(id)))
            && PrototypeNativeFakeMerchant.Definition.Choices.Any(choice =>
                choice.Id == PrototypeNativeFakeMerchant.LeaveChoiceId)
            && PrototypeContent.Encounter(
                PrototypeNativeFakeMerchant.EncounterId).Weight == 0)
            implementation = "implemented";

        if (item.Scope == "single-player" && item.Kind == "potions"
            && item.Id == "FOUL_POTION"
            && engineIds.Contains(
                PrototypeNativeFakeMerchant.FoulPotionId,
                StringComparer.Ordinal)
            && PrototypeContent.Potion(
                PrototypeNativeFakeMerchant.FoulPotionId).Effects.Any(
                effect => effect.Kind == PrototypeCombatEffectKind.DamageEnemy
                    && effect.Amount == 12
                    && effect.Target == PrototypeEffectTarget.AllEnemies)
            && !PrototypeContent.Potion(
                PrototypeNativeFakeMerchant.FoulPotionId).Effects.Any(
                effect => effect.Kind == PrototypeCombatEffectKind.DamagePlayer)
            && PrototypeNativeFakeMerchant.Definition.Choices.Length == 10)
        {
            implementation = "implemented";
            integration = "integrated";
        }

        if (item.Scope == "single-player" && item.Kind == "monsters"
            && item.Id == "FAKE_MERCHANT_MONSTER"
            && engineIds.Contains(PrototypeNativeFakeMerchant.MonsterId,
                StringComparer.Ordinal)
            && PrototypeContent.Encounter(
                PrototypeNativeFakeMerchant.EncounterId).EnemyIds
                .Contains(PrototypeNativeFakeMerchant.MonsterId,
                    StringComparer.Ordinal))
        {
            implementation = "implemented";
            integration = "integrated";
        }

        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id.StartsWith("FAKE_", StringComparison.Ordinal)
            && engineIds.Length > 0
            && (item.Id == "FAKE_MERCHANTS_RUG"
                ? PrototypeNativeFakeMerchant.Encounter.Weight == 0
                    && PrototypeNativeFakeMerchant.StockRelicIds.Length == 9
                : PrototypeNativeFakeMerchant.StockRelicIds.Contains(
                    engineIds[0], StringComparer.Ordinal))
            && (ExperimentalSilentThreeActRouteV111.ContainsEvent(
                    PrototypeNativeFakeMerchant.EventId, 2)
                || ExperimentalSilentThreeActRouteV111.ContainsEvent(
                    PrototypeNativeFakeMerchant.EventId, 3)))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "PHILOSOPHERS_STONE"
            && engineIds.Contains(
                "proto.relic.philosophers_stone",
                StringComparer.Ordinal)
            && HasPhilosophersStonePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "CHEMICAL_X"
            && engineIds.Contains(
                "proto.relic.chemical_x",
                StringComparer.Ordinal)
            && HasChemicalXPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "RUNIC_PYRAMID"
            && engineIds.Contains(
                "proto.relic.runic_pyramid",
                StringComparer.Ordinal)
            && HasRunicPyramidPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "MUMMIFIED_HAND"
            && engineIds.Contains(
                "proto.relic.mummified_hand",
                StringComparer.Ordinal)
            && HasMummifiedHandPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "UNCEASING_TOP"
            && engineIds.Contains(
                "proto.relic.unceasing_top",
                StringComparer.Ordinal)
            && HasUnceasingTopPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "EMPTY_CAGE"
            && engineIds.Contains(
                "proto.relic.empty_cage",
                StringComparer.Ordinal)
            && HasEmptyCagePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "FRESNEL_LENS"
            && engineIds.Contains(
                "proto.native.underdocks.fresnel_lens",
                StringComparer.Ordinal)
            && HasFresnelLensPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "DREAM_CATCHER"
            && engineIds.Contains(
                PrototypeNativeUnderdocksTrashHeap.DreamCatcherId,
                StringComparer.Ordinal)
            && PrototypeNativeUnderdocksTrashHeap.RelicIds.Contains(
                PrototypeNativeUnderdocksTrashHeap.DreamCatcherId,
                StringComparer.Ordinal)
            && PrototypeNativeUnderdocksEvents.IsSupported(
                PrototypeNativeUnderdocksTrashHeap.EventId)
            && PrototypeContent.Relic(
                PrototypeNativeUnderdocksTrashHeap.DreamCatcherId)
                .RestHealCardRewardCount == 3)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "DARKSTONE_PERIAPT"
            && engineIds.Contains(
                "proto.relic.darkstone_periapt",
                StringComparer.Ordinal)
            && PrototypeNativeUnderdocksTrashHeap.RelicIds.Contains(
                "proto.relic.darkstone_periapt",
                StringComparer.Ordinal)
            && PrototypeNativeUnderdocksEvents.IsSupported(
                PrototypeNativeUnderdocksTrashHeap.EventId)
            && !PrototypeNativeRelicGrabBag.TryGetRarity(
                "proto.relic.darkstone_periapt", out _)
            && PrototypeContent.Relic(
                "proto.relic.darkstone_periapt").RunTriggers?.Any(
                trigger => trigger.EventKind
                        == PrototypeRunEventKind.CardAdded
                    && trigger.RequiredCardType
                        == PrototypeCardType.Curse
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainMaxHp
                        && effect.Amount == 6)) == true)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "HAND_DRILL"
            && engineIds.Contains(
                PrototypeNativeUnderdocksTrashHeap.HandDrillId,
                StringComparer.Ordinal)
            && PrototypeNativeUnderdocksEvents.IsSupported(
                PrototypeNativeUnderdocksTrashHeap.EventId)
            && PrototypeNativeUnderdocksTrashHeap.RelicIds.Contains(
                PrototypeNativeUnderdocksTrashHeap.HandDrillId,
                StringComparer.Ordinal)
            && PrototypeContent.Event(
                PrototypeNativeUnderdocksTrashHeap.EventId).Choices
                .Any(choice => choice.Id == "dive")
            && PrototypeContent.Relic(
                PrototypeNativeUnderdocksTrashHeap.HandDrillId)
                .EnemyVulnerableOnBlockBroken == 2)
        {
            // Player-caused block breaks are modeled; pet attack execution
            // remains outside the current combat route.
            implementation = "partial";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "THE_BOOT"
            && engineIds.Contains(PrototypeNativeSharedRelics.TheBootId,
                StringComparer.Ordinal)
            && PrototypeNativeUnderdocksTrashHeap.RelicIds.Contains(
                PrototypeNativeSharedRelics.TheBootId,
                StringComparer.Ordinal)
            && PrototypeContent.Relic(PrototypeNativeSharedRelics.TheBootId)
                .MinPoweredAttackHpLoss == 5
            && !PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.TheBootId,
                StringComparer.Ordinal))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "MAW_BANK"
            && engineIds.Contains(
                PrototypeNativeUnderdocksTrashHeap.MawBankId,
                StringComparer.Ordinal)
            && PrototypeNativeUnderdocksTrashHeap.RelicIds.Contains(
                PrototypeNativeUnderdocksTrashHeap.MawBankId,
                StringComparer.Ordinal)
            && PrototypeContent.Relic(
                PrototypeNativeUnderdocksTrashHeap.MawBankId)
                .GoldOnRoomEntryUntilPurchase == 12
            && !PrototypeContent.RelicPool.Contains(
                PrototypeNativeUnderdocksTrashHeap.MawBankId,
                StringComparer.Ordinal))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "CENTENNIAL_PUZZLE"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.CentennialPuzzleId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.CentennialPuzzleId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.CentennialPuzzleId,
                out var centennialRarity)
            && centennialRarity == PrototypeRelicRarity.Common
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.CentennialPuzzleId).Triggers?
                .Any(trigger =>
                    trigger.EventKind
                        == PrototypeCombatEventKind.PlayerDamaged
                    && trigger.MaxTriggersPerCounterWindow == 1
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeCombatEffectKind.DrawCards
                        && effect.Amount == 3)) == true)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "TUNGSTEN_ROD"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.TungstenRodId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.TungstenRodId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.TungstenRodId,
                out var tungstenRodRarity)
            && tungstenRodRarity == PrototypeRelicRarity.Rare
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.TungstenRodId)
                .PlayerHpLossReduction == 1)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "ROYAL_POISON"
            && engineIds.Contains(
                PrototypeNativeGloryEvents.RoyalPoisonId,
                StringComparer.Ordinal)
            && PrototypeContent.Relic(
                PrototypeNativeGloryEvents.RoyalPoisonId)
                .UnblockableHpLossAtCombatStart == 4
            && PrototypeNativeGloryEvents.Events.Any(evt =>
                evt.Id == PrototypeNativeGloryEvents.RoundTeaPartyId
                && evt.Choices.Any(choice => choice.Id == "enjoy_tea")))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "BIG_MUSHROOM"
            && engineIds.Contains(
                PrototypeNativeGloryEvents.BigMushroomId,
                StringComparer.Ordinal)
            && PrototypeContent.Relic(
                PrototypeNativeGloryEvents.BigMushroomId) is
                { FirstTurnDrawBonus: -2 } bigMushroom
            && bigMushroom.RunTriggers?.Any(trigger =>
                trigger.EventKind == PrototypeRunEventKind.RelicAcquired
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.GainMaxHp
                    && effect.Amount == 20)) == true
            && PrototypeNativeGloryEvents.Events.Any(evt =>
                evt.Id == PrototypeNativeGloryEvents.HungryForMushroomsId
                && evt.Choices.Any(choice =>
                    choice.Id == "big_mushroom")))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "FRAGRANT_MUSHROOM"
            && engineIds.Contains(
                PrototypeNativeGloryEvents.FragrantMushroomId,
                StringComparer.Ordinal)
            && PrototypeContent.Relic(
                PrototypeNativeGloryEvents.FragrantMushroomId) is
                { UpgradeRandomCardsOnAcquisitionCount: 2 } fragrantMushroom
            && fragrantMushroom.RunTriggers?.Any(trigger =>
                trigger.EventKind == PrototypeRunEventKind.RelicAcquired
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.LoseHp
                    && effect.Amount == 15)) == true
            && PrototypeNativeGloryEvents.Events.Any(evt =>
                evt.Id == PrototypeNativeGloryEvents.HungryForMushroomsId
                && evt.Choices.Any(choice =>
                    choice.Id == "fragrant_mushroom")))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "LOST_WISP"
            && engineIds.Contains(
                PrototypeNativeHiveEvents.LostWispRelicId,
                StringComparer.Ordinal)
            && !PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeHiveEvents.LostWispRelicId, out _)
            && PrototypeContent.Relic(
                PrototypeNativeHiveEvents.LostWispRelicId).Triggers?.Any(
                trigger => trigger.EventKind
                        == PrototypeCombatEventKind.CardPlayed
                    && trigger.RequiredSourceCardType
                        == PrototypeCardType.Power
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeCombatEffectKind.DamageEnemy
                        && effect.Amount == 8
                        && effect.Target
                            == PrototypeEffectTarget.AllEnemies)) == true
            && PrototypeNativeHiveEvents.Definitions.Any(evt =>
                evt.Id == PrototypeNativeHiveEvents.LostWispId
                && evt.Choices.Any(choice => choice.Id == "claim"
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.AddCard
                        && effect.CardId
                            == PrototypeNativeHiveEvents.DecayId)
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainRelic
                        && effect.RelicId
                            == PrototypeNativeHiveEvents.LostWispRelicId))))
        {
            implementation = "implemented";
            integration = "integrated";
        }
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
            && item.Id == "PANTOGRAPH"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.PantographId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.PantographId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.PantographId,
                out var pantographRarity)
            && pantographRarity == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.PantographId)
                .HealBeforeBossCombat == 25)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "JUZU_BRACELET"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.JuzuBraceletId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.JuzuBraceletId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.JuzuBraceletId,
                out var juzuRarity)
            && juzuRarity == PrototypeRelicRarity.Common
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.JuzuBraceletId)
                .ExcludesMonsterRoomsFromUnknownMap
            && HasJuzuUnknownRoomPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "PETRIFIED_TOAD"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.PetrifiedToadId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.PetrifiedToadId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.PetrifiedToadId,
                out var toadRarityForRelic)
            && toadRarityForRelic == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.PetrifiedToadId)
                .PotionIdBeforeCombat
                == PrototypeNativeSharedRelics.PotionShapedRockId
            && PrototypeContent.Potion(
                PrototypeNativeSharedRelics.PotionShapedRockId) is
                { IsToken: true, Target: PrototypeCardTarget.Enemy }
                toadRockPotion
            && toadRockPotion.Effects.Length == 1
            && toadRockPotion.Effects[0].Kind
                == PrototypeCombatEffectKind.DamageEnemy
            && toadRockPotion.Effects[0].Amount == 15
            && !PrototypeContent.PotionPool.Contains(
                PrototypeNativeSharedRelics.PotionShapedRockId,
                StringComparer.Ordinal))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "potions"
            && item.Id == "POTION_SHAPED_ROCK"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.PotionShapedRockId,
                StringComparer.Ordinal)
            && PrototypeContent.Potion(
                PrototypeNativeSharedRelics.PotionShapedRockId) is
                { IsToken: true, Target: PrototypeCardTarget.Enemy }
                potionRock
            && potionRock.Effects.Length == 1
            && potionRock.Effects[0].Kind
                == PrototypeCombatEffectKind.DamageEnemy
            && potionRock.Effects[0].Amount == 15
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.PetrifiedToadId)
                .PotionIdBeforeCombat
                == PrototypeNativeSharedRelics.PotionShapedRockId
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.PetrifiedToadId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.PetrifiedToadId,
                out var toadRarityForPotion)
            && toadRarityForPotion == PrototypeRelicRarity.Uncommon
            && !PrototypeContent.PotionPool.Contains(
                PrototypeNativeSharedRelics.PotionShapedRockId,
                StringComparer.Ordinal))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "ETERNAL_FEATHER"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.EternalFeatherId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.EternalFeatherId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.EternalFeatherId,
                out var featherRarity)
            && featherRarity == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.EternalFeatherId)
                .RestSiteHealPerFiveCards == 3)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && (item.Id is "WAR_PAINT" or "WHETSTONE")
            && engineIds.Contains(item.Id == "WAR_PAINT"
                ? PrototypeNativeSharedRelics.WarPaintId
                : PrototypeNativeSharedRelics.WhetstoneId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(item.Id == "WAR_PAINT"
                ? PrototypeNativeSharedRelics.WarPaintId
                : PrototypeNativeSharedRelics.WhetstoneId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                item.Id == "WAR_PAINT"
                    ? PrototypeNativeSharedRelics.WarPaintId
                    : PrototypeNativeSharedRelics.WhetstoneId,
                out var upgradeRelicRarity)
            && upgradeRelicRarity == PrototypeRelicRarity.Common
            && PrototypeContent.Relic(item.Id == "WAR_PAINT"
                    ? PrototypeNativeSharedRelics.WarPaintId
                    : PrototypeNativeSharedRelics.WhetstoneId)
                is { UpgradeRandomCardsOnAcquisitionCount: 2 } upgradeRelic
            && upgradeRelic.UpgradeRandomCardsOnAcquisitionType
                == (item.Id == "WAR_PAINT"
                    ? PrototypeCardType.Skill
                    : PrototypeCardType.Attack))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "PEAR"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.PearId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.PearId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.PearId, out var pearRarity)
            && pearRarity == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.PearId).RunTriggers?.Any(
                trigger => trigger.EventKind
                    == PrototypeRunEventKind.RelicAcquired
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainMaxHp
                        && effect.Amount == 10)) == true)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "STRAWBERRY"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.StrawberryId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.StrawberryId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.StrawberryId,
                out var strawberryRarity)
            && strawberryRarity == PrototypeRelicRarity.Common
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.StrawberryId).RunTriggers?.Any(
                trigger => trigger.EventKind
                    == PrototypeRunEventKind.RelicAcquired
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainMaxHp
                        && effect.Amount == 7)) == true)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "AKABEKO"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.AkabekoId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.AkabekoId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.AkabekoId, out var akabekoRarity)
            && akabekoRarity == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.AkabekoId).Triggers?.Any(trigger =>
                trigger.EventKind
                    == PrototypeCombatEventKind.PlayerSideTurnStarted
                && trigger.TurnEquals == 1
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind.ApplyPlayerPower
                    && effect.Amount == 8
                    && effect.PowerId
                        == PrototypeNativeSharedRelics.VigorId)) == true
            && PrototypeContent.Power(
                PrototypeNativeSharedRelics.VigorId)
                .PlayerAttackDamageBonusPerStack == 1)
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
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "SNECKO_SKULL"
            && engineIds.Contains(PrototypeNativeSilentRelics.SneckoSkullId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSilentRelics.SneckoSkullId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSilentRelics.SneckoSkullId, out var skullRarity)
            && skullRarity == PrototypeRelicRarity.Common)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "POTION_BELT"
            && engineIds.Contains("proto.relic.potion_belt",
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                "proto.relic.potion_belt", StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                "proto.relic.potion_belt", out var potionBeltRarity)
            && potionBeltRarity == PrototypeRelicRarity.Common
            && PrototypeContent.Relic("proto.relic.potion_belt")
                is { ExtraPotionSlots: 2 } potionBelt
            && potionBelt.RunTriggers?.Any(trigger =>
                trigger.EventKind == PrototypeRunEventKind.RelicAcquired
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.GainPotionSlots
                    && effect.Amount == 2)) == true)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "powers"
            && item.Id == "VIGOR"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.VigorId,
                StringComparer.Ordinal)
            && PrototypeContent.Power(
                PrototypeNativeSharedRelics.VigorId) is
                {
                    PlayerAttackDamageBonusPerStack: 1,
                    EnemyAttackDamageBonusPerStack: 1,
                    ConsumeAfterEnemyAttack: true
                }
            && PrototypeContent.Enemy("proto.enemy.terror_eel").Moves
                .Any(move => move.Effects.Any(effect =>
                    effect.Kind == PrototypeEnemyEffectKind.ApplyEnemyPower
                    && effect.PowerId
                        == PrototypeNativeSharedRelics.VigorId))
            && PrototypeNativeUnderdocks.SupportedEliteEncounterIds.Contains(
                "proto.encounter.terror_eel_elite",
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.AkabekoId,
                StringComparer.Ordinal))
        {
            // Route-backed Vigor cases are modeled; cardless powered
            // attack commands remain outside the current combat API.
            implementation = "partial";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "PAPER_KRANE"
            && engineIds.Contains(PrototypeNativeSilentRelics.PaperKraneId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSilentRelics.PaperKraneId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSilentRelics.PaperKraneId, out var craneRarity)
            && craneRarity == PrototypeRelicRarity.Rare)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "TINGSHA"
            && engineIds.Contains("proto.relic.tingsha",
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains("proto.relic.tingsha",
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                "proto.relic.tingsha", out var tingshaRarity)
            && tingshaRarity == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic("proto.relic.tingsha").Triggers?.Any(
                trigger => trigger.EventKind
                    == PrototypeCombatEventKind.CardDiscarded
                    && trigger.RequiresPlayerSide
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeCombatEffectKind.DamageEnemy
                        && effect.Amount == 3
                        && effect.Target
                            == PrototypeEffectTarget.RandomEnemy)) == true)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "TOUGH_BANDAGES"
            && engineIds.Contains("proto.relic.tough_bandages",
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                "proto.relic.tough_bandages", StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                "proto.relic.tough_bandages", out var bandagesRarity)
            && bandagesRarity == PrototypeRelicRarity.Rare
            && PrototypeContent.Relic("proto.relic.tough_bandages").Triggers?
                .Any(trigger => trigger.EventKind
                    == PrototypeCombatEventKind.CardDiscarded
                    && trigger.RequiresPlayerSide
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeCombatEffectKind.GainPlayerBlock
                        && effect.Amount == 3)) == true)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "RING_OF_THE_SNAKE"
            && engineIds.Contains(
                PrototypeNativeOrobasRelics.RingOfTheSnakeId,
                StringComparer.Ordinal)
            && V111Characters.Get("silent").StartingRelics.Contains(
                "RING_OF_THE_SNAKE", StringComparer.Ordinal)
            && PrototypeContent.StartingRelics.Contains(
                PrototypeNativeOrobasRelics.RingOfTheSnakeId,
                StringComparer.Ordinal)
            && PrototypeContent.Relic(
                PrototypeNativeOrobasRelics.RingOfTheSnakeId)
                .FirstTurnDrawBonus == 2)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id is ("BONE_TEA" or "EMBER_TEA"
                or "TEA_OF_DISCOURTESY")
            && engineIds.Length == 1
            && PrototypeNativeGloryEvents.Events.Any(evt =>
                evt.Id == PrototypeNativeGloryEvents.TeaMasterId)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeGloryEvents.TeaMasterId, 3))
        {
            var choiceId = item.Id switch
            {
                "BONE_TEA" => "bone_tea",
                "EMBER_TEA" => "ember_tea",
                _ => "tea_of_discourtesy"
            };
            if (PrototypeContent.Event(
                    PrototypeNativeGloryEvents.TeaMasterId).Choices.Any(
                    choice => choice.Id == choiceId))
            {
                implementation = "implemented";
                integration = "integrated";
            }
        }
        if (item.Scope == "multiplayer-only")
        {
            implementation = "excluded";
            integration = "excluded";
        }
        if (item.Scope == "single-player"
            && item.Kind == "rest_site_options"
            && engineIds.Length == 1)
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id is "SHOVEL" or "GIRYA"
            && engineIds.Length == 1
            && PrototypeContent.RelicPool.Contains(engineIds[0],
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                engineIds[0], out var restRelicRarity)
            && restRelicRarity == PrototypeRelicRarity.Rare)
        {
            var player = new PlayerState(1, 1, 0, [],
                [new RelicInstance(engineIds[0], PrototypeJson.EmptyObject())],
                []);
            var supported = item.Id == "SHOVEL"
                ? PrototypeNativeRestSiteRelics.CanDig(player)
                    && PrototypeNativeRestSiteActionsV111.ForNativeOption(
                        "DIG") == PrototypeNativeRestSiteActionsV111.Dig
            : PrototypeNativeRestSiteRelics.CanLift(player)
                && PrototypeNativeRestSiteRelics.CombatStrengthBonus(
                    PrototypeNativeRestSiteRelics.Lift(player)) == 1
                && PrototypeNativeRestSiteActionsV111.ForNativeOption(
                    "LIFT") == PrototypeNativeRestSiteActionsV111.Lift;
            if (supported)
            {
                implementation = "implemented";
                integration = "integrated";
            }
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && engineIds.Length == 1
            && PrototypeContent.RelicPool.Contains(engineIds[0],
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(engineIds[0], out _)
            && (item.Id != "POCKETWATCH" || HasPocketwatchPath())
            && (item.Id != "THE_ABACUS" || HasTheAbacusPath())
            && (item.Id != "TUNING_FORK" || HasTuningForkPath())
            && HasExecutableRelicBehavior(PrototypeContent.Relic(engineIds[0])))
        {
            implementation = "implemented";
            integration = "integrated";
        }
        return new V111CoverageItem(item.Kind, item.Id, item.Scope, engineIds,
            true, engineIds.Length > 0, implementation, integration, "unverified",
            missingVariants,
            missingBranches,
            item.Source is null ? [] : [$"{V111Build.SourceCommit}:{item.Source}#{item.SourceSha256}"]);
    }

    private static bool HasExecutableRelicBehavior(
        PrototypeRelicDefinition definition)
    {
        // Every non-name field on this typed definition is consumed by the
        // shared combat, run-event, reward, shop, or acquisition handlers.
        // Comparing with the record defaults keeps name-only placeholders
        // from being reported as implemented.
        var empty = new PrototypeRelicDefinition(string.Empty, string.Empty);
        return definition with { Id = string.Empty, Name = string.Empty }
            != empty;
    }

    private static bool HasPocketwatchPath()
    {
        const string relicId = "proto.relic.pocketwatch";
        var definition = PrototypeContent.Relic(relicId);
        return HasOrdinaryRareRelic(relicId)
            && definition.ConditionalHandDrawBonus == 3
            && definition.ConditionalHandDrawMaxCardsPlayedLastTurn == 3;
    }

    private static bool HasTheAbacusPath()
    {
        const string relicId = "proto.relic.the_abacus";
        return PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                relicId, out var rarity)
            && rarity == PrototypeRelicRarity.Shop
            && PrototypeContent.Relic(relicId)
                .PlayerBlockAfterDrawPileShuffle == 6;
    }

    private static bool HasTuningForkPath()
    {
        const string relicId = "proto.relic.tuning_fork";
        var definition = PrototypeContent.Relic(relicId);
        return PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                relicId, out var rarity)
            && rarity == PrototypeRelicRarity.Uncommon
            && definition.PersistentTriggerCountProperty == "SkillsPlayed"
            && definition.Triggers?.Any(trigger =>
                trigger.EventKind == PrototypeCombatEventKind.CardPlayed
                && trigger.RequiresPlayerSide
                && trigger.RequiredSourceCardType
                    == PrototypeCardType.Skill
                && trigger.EveryNth == 10
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind.GainPlayerBlock
                    && effect.Amount == 7)) == true;
    }

    private static bool HasVenerableTeaSetPath()
    {
        var relicId = PrototypeNativeSharedRelics.VenerableTeaSetId;
        if (!PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || !PrototypeNativeRelicGrabBag.TryGetRarity(
                relicId, out var rarity)
            || rarity != PrototypeRelicRarity.Common)
        {
            return false;
        }

        var player = new PlayerState(20, 20, 0, [],
            [new RelicInstance(relicId, PrototypeJson.EmptyObject())], []);
        var charged = PrototypeNativeFakeMerchant
            .MarkTeaSetsAfterRestSite(player);
        var consumed = PrototypeNativeFakeMerchant
            .ConsumeTeaSetCharges(charged);
        return PrototypeNativeFakeMerchant.HasTeaSetCharge(
                charged.Relics.Single())
            && consumed.Bonus == 2
            && !PrototypeNativeFakeMerchant.HasTeaSetCharge(
                consumed.Player.Relics.Single());
    }

    private static bool HasLizardTailPath()
    {
        var relicId = PrototypeNativeSharedRelics.LizardTailId;
        if (!PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || !PrototypeNativeRelicGrabBag.TryGetRarity(
                relicId, out var rarity)
            || rarity != PrototypeRelicRarity.Rare)
        {
            return false;
        }

        var player = new PlayerState(5, 40, 0, [],
            [new RelicInstance(relicId, PrototypeJson.EmptyObject())], []);
        var saved = PrototypeGameEngine
            .ApplyPlayerHpLossAndDeathPrevention(player, 10);
        var usedRelic = saved.Relics.Single();
        var diedAgain = PrototypeGameEngine
            .ApplyPlayerHpLossAndDeathPrevention(saved, 20);
        return saved.Hp == 20
            && PrototypeGameEngine.IsLizardTailUsed(usedRelic)
            && diedAgain.Hp == 0;
    }

    private static bool HasSlingOfCouragePath()
    {
        var relicId = PrototypeNativeSharedRelics.SlingOfCourageId;
        if (!PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || !PrototypeNativeRelicGrabBag.TryGetRarity(
                relicId, out var rarity)
            || rarity != PrototypeRelicRarity.Shop
            || PrototypeContent.Relic(relicId)
                .StrengthOnEliteRoomEntry != 2)
        {
            return false;
        }

        var relics = new[]
        {
            new RelicInstance(relicId, PrototypeJson.EmptyObject())
        };
        var player = new PlayerState(20, 20, 0, [], relics, []);
        return PrototypeGameEngine.RelicStrengthOnRoomEntry(
                player, PrototypeRoomType.Elite) == 2
            && PrototypeGameEngine.RelicStrengthOnRoomEntry(
                player, PrototypeRoomType.Combat) == 0
            && PrototypeGameEngine.RelicStrengthOnRoomEntry(
                player with
                {
                    Relics = [relics[0] with { IsMelted = true }]
                }, PrototypeRoomType.Elite) == 0;
    }

    private static bool HasRedMaskPath()
    {
        var relicId = PrototypeNativeSharedRelics.RedMaskId;
        if (!PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || !PrototypeNativeRelicGrabBag.TryGetRarity(
                relicId, out var rarity)
            || rarity != PrototypeRelicRarity.Common)
        {
            return false;
        }

        return PrototypeContent.Relic(relicId).Triggers?.Any(trigger =>
            trigger.EventKind
                == PrototypeCombatEventKind.PlayerSideTurnStarted
            && trigger.TurnEquals == 1
            && trigger.Effects.Any(effect =>
                effect.Kind == PrototypeCombatEffectKind.ApplyEnemyPower
                && effect.Amount == 1
                && effect.Target == PrototypeEffectTarget.AllEnemies
                && effect.PowerId == "proto.power.weak")) == true;
    }

    private static bool HasStoneCalendarPath()
    {
        var relicId = PrototypeNativeSharedRelics.StoneCalendarId;
        return PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                relicId, out var rarity)
            && rarity == PrototypeRelicRarity.Rare
            && PrototypeContent.Relic(relicId).Triggers?.Any(trigger =>
                trigger.EventKind
                    == PrototypeCombatEventKind.PlayerTurnEnded
                && trigger.TurnEquals == 7
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind.DamageEnemy
                    && effect.Amount == 52
                    && effect.Target
                        == PrototypeEffectTarget.AllEnemies)) == true;
    }

    private static bool HasPollinousCorePath()
    {
        var relicId = PrototypeNativeHiveEvents.PollinousCoreId;
        var evt = PrototypeNativeHiveEvents.Definitions.SingleOrDefault(
            candidate => candidate.Id
                == PrototypeNativeHiveEvents.ColossalFlowerId);
        if (evt is null || !PrototypeNativeHiveEvents.IsNativeEvent(evt.Id)
            || evt.MinAct != 2 || evt.MaxAct != 2
            || !evt.Choices.Any(choice => choice.Id == "relic")
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal))
        {
            return false;
        }

        var relic = new RelicInstance(
            relicId, PrototypeJson.EmptyObject());
        var player = new PlayerState(20, 20, 0, [], [relic], []);
        for (var draw = 0; draw < 3; draw++)
        {
            var result = PrototypeGameEngine
                .ApplyPollinousCoreHandDraw(player);
            if (result.Bonus != 0)
                return false;
            player = result.Player;
        }

        var fourthDraw = PrototypeGameEngine
            .ApplyPollinousCoreHandDraw(player);
        var melted = PrototypeGameEngine.ApplyPollinousCoreHandDraw(
            player with
            {
                Relics = [relic with
                {
                    IsMelted = true,
                    PersistentState = JsonSerializer.SerializeToElement(
                        new { TurnsSeen = 3 })
                }]
            });
        return fourthDraw.Bonus == 2
            && fourthDraw.Player.Relics.Single().PersistentState
                .TryGetProperty("TurnsSeen", out var turnsSeen)
            && turnsSeen.ValueKind == JsonValueKind.Number
            && turnsSeen.GetInt32() == 0
            && melted.Bonus == 0
            && melted.Player.Relics.Single().PersistentState
                .GetProperty("TurnsSeen").GetInt32() == 3;
    }

    private static bool HasHistoryCoursePath()
    {
        var relicId = PrototypeNativeHiveEvents.HistoryCourseId;
        var repy = PrototypeNativeHiveEvents.Definitions.SingleOrDefault(
            evt => evt.Id == PrototypeNativeHiveEvents.WarHistorianRepyId);
        var autoplayPipeline = PrototypeContent.Rules.EndTurnPipeline.Any(
            step => step.Kind == PrototypeAutomaticStepKind.DispatchCombatEvent
                && step.EventKind
                    == PrototypeCombatEventKind.AutoPrePlayPhaseEntered);
        if (PrototypeContent.RelicPool.Contains(relicId,
                StringComparer.Ordinal)
            || PrototypeContent.Relic(relicId).Id != relicId
            || repy is null
            || !repy.Choices.Any(choice => choice.Id == "unlock_cage")
            || !autoplayPipeline)
        {
            return false;
        }

        static RunState CreateState(bool melted)
        {
            const string cardId = "proto.silent.neutralize";
            const string enemyId = "proto.enemy.wriggler";
            var relicId = PrototypeNativeHiveEvents.HistoryCourseId;
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var lastAttack = new PrototypeCombatCardSnapshot(
                cardId, 0, PrototypeJson.EmptyObject());
            var combat = new CombatState(
                Turn: 2,
                Energy: 3,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, enemyId, 20, 0, 0,
                        new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 1,
                Cards: [],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1, [],
                        IsMelted: melted)
                ],
                Counters: new PrototypeCombatCounters(
                    LastOriginalAttackLastTurn: lastAttack),
                Act: 3,
                PlayPile: []);
            var player = new PlayerState(30, 30, 0, [], [relic], []);
            var world = new RunWorldState(
                RulesetId: PrototypeContent.RulesetId,
                CharacterId: PrototypeContent.CharacterId,
                Act: 3,
                Floor: 1,
                NextCardInstanceId: 2,
                ActiveRoom: PrototypeRoomType.Combat,
                Map: new MapState([]),
                Combat: combat,
                Reward: null,
                Shop: null,
                Event: null,
                TerminalOutcome: null);
            return new RunState(
                GameBuild: V111Build.Id,
                EmulatorSchema: V111Build.StateSchema,
                RunId: "coverage-history-course",
                RunSeed: "coverage-history-course",
                DecisionIndex: 0,
                Phase: RunPhase.Combat,
                Player: player,
                Rng: PrototypeRng.CreateBundle("coverage-history-course"),
                ExtensionState: PrototypeJson.EmptyObject(),
                World: world);
        }

        var replayed = PrototypeGameEngine.ResolveHistoryCourseAutoPlay(
            CreateState(melted: false));
        var replayedCombat = replayed.World?.Combat;
        var duplicate = replayedCombat?.Cards.SingleOrDefault(card =>
            card.IsHistoryCourseDupe);
        var melted = PrototypeGameEngine.ResolveHistoryCourseAutoPlay(
            CreateState(melted: true));
        var meltedCombat = melted.World?.Combat;
        return duplicate?.CardId == "proto.silent.neutralize"
            && replayedCombat?.Energy == 3
            && replayedCombat.Enemies.Single().Hp == 17
            && replayedCombat.Enemies.Single().Statuses
                .GetValueOrDefault("proto.status.weak") == 1
            && meltedCombat?.Cards.Length == 0
            && meltedCombat.NextCardInstanceId == 1
            && meltedCombat.Enemies.Single().Hp == 20;
    }

    private static bool HasForgottenSoulPath()
    {
        var relicId = PrototypeNativeGloryEvents.ForgottenSoulId;
        var grave = PrototypeNativeGloryEvents.Events.SingleOrDefault(
            evt => evt.Id == PrototypeNativeGloryEvents.GraveOfTheForgottenId);
        if (grave is null
            || !ExperimentalSilentThreeActRouteV111.ContainsEvent(
                grave.Id, 3)
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || !grave.Choices.Any(choice => choice.Id == "accept"
                && choice.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.GainRelic
                    && effect.RelicId == relicId))
            || PrototypeContent.Relic(relicId).Triggers?.Any(trigger =>
                trigger.EventKind == PrototypeCombatEventKind.CardExhausted
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind.DamageEnemy
                    && effect.Amount == 1
                    && effect.Target
                        == PrototypeEffectTarget.RandomEnemy)) != true)
        {
            return false;
        }

        static (PlayerState Player, CombatState Combat) Dispatch(bool melted)
        {
            var relicId = PrototypeNativeGloryEvents.ForgottenSoulId;
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var triggerCount = PrototypeContent.Relic(relicId)
                .Triggers?.Length ?? 0;
            var combat = new CombatState(
                Turn: 1,
                Energy: 3,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20, 0,
                        0, new Dictionary<string, int>(StringComparer.Ordinal)),
                    new EnemyCombatState(2, "proto.enemy.wriggler", 20, 0,
                        0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 1,
                Cards: [],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1,
                        new int[triggerCount],
                        IsMelted: melted)
                ],
                Act: 3,
                PlayPile: []);
            var player = new PlayerState(30, 30, 0, [], [relic], []);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CardExhausted),
                PrototypeRng.CreateBundle("coverage-forgotten-soul"));
        }

        var active = Dispatch(melted: false).Combat;
        var melted = Dispatch(melted: true).Combat;
        return active.Enemies.Sum(enemy => enemy.Hp) == 39
            && melted.Enemies.Sum(enemy => enemy.Hp) == 40;
    }

    private static bool HasBlessedAntlerPath()
    {
        var relicId = NativeGloryAncientsV111.BlessedAntlerId;
        var definition = PrototypeContent.Relic(relicId);
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(3).Contains(
                NativeNonupeipeEventV111.EventId, StringComparer.Ordinal)
            || !HasCompleteAncientRelicBranches(
                NativeNonupeipeEventV111.EventId, 10)
            || !NativeNonupeipeEventV111.Definition.Choices.Any(choice =>
                choice.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.GainRelic
                    && effect.RelicId == relicId))
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || definition.EnergyPerTurnBonus != 1
            || definition.Triggers?.Any(trigger =>
                trigger.EventKind == PrototypeCombatEventKind.BeforeHandDraw
                && trigger.TurnEquals == 1
                && trigger.Effects.Any(effect =>
                    effect.Kind
                        == PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions
                    && effect.Amount == 3
                    && effect.CardId == "proto.status.dazed")) != true)
        {
            return false;
        }

        static (CombatState Combat, int Energy) Dispatch(
            int turn, bool melted)
        {
            var relicId = NativeGloryAncientsV111.BlessedAntlerId;
            var triggerCount = PrototypeContent.Relic(relicId)
                .Triggers?.Length ?? 0;
            var player = new PlayerState(30, 30, 0, [],
                [new RelicInstance(relicId, PrototypeJson.EmptyObject(),
                    IsMelted: melted)], []);
            var energy = PrototypeGameEngine.EnergyPerTurn(player, turn);
            var combat = new CombatState(
                Turn: turn,
                Energy: energy,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20, 0,
                        0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 1,
                Cards: [],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1,
                        new int[triggerCount], IsMelted: melted)
                ],
                Act: 3,
                PlayPile: []);
            var dispatched = PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.BeforeHandDraw),
                PrototypeRng.CreateBundle(
                    $"coverage-blessed-antler-{turn}-{melted}"))
                .Combat;
            return (dispatched, energy);
        }

        var opening = Dispatch(turn: 1, melted: false);
        var laterTurn = Dispatch(turn: 2, melted: false);
        var melted = Dispatch(turn: 1, melted: true);
        var openingCombat = opening.Combat;
        return opening.Energy == PrototypeContent.Rules.BaseEnergy + 1
            && openingCombat.Energy == opening.Energy
            && openingCombat.DrawPile.Length == 3
            && openingCombat.Cards.Length == 3
            && openingCombat.Cards.All(card => card.IsTemporary
                && card.CardId == "proto.status.dazed")
            && openingCombat.DrawPile.Order()
                .SequenceEqual(openingCombat.Cards
                    .Select(card => card.InstanceId).Order())
            && laterTurn.Energy == PrototypeContent.Rules.BaseEnergy + 1
            && laterTurn.Combat.DrawPile.Length == 0
            && melted.Energy == PrototypeContent.Rules.BaseEnergy
            && melted.Combat.DrawPile.Length == 0;
    }

    private static bool HasBeautifulBraceletPath()
    {
        var relicId = NativeGloryAncientsV111.BeautifulBraceletId;
        var choiceId = "take_" + relicId;
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(3).Contains(
                NativeNonupeipeEventV111.EventId, StringComparer.Ordinal)
            || !HasCompleteAncientRelicBranches(
                NativeNonupeipeEventV111.EventId, 10)
            || !NativeNonupeipeEventV111.Definition.Choices.Any(choice =>
                choice.Id == choiceId)
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal))
        {
            return false;
        }

        var eligibleDeck = Enumerable.Range(1, 5)
            .Select(instanceId => new CardInstance(
                instanceId, "proto.silent.strike", 0,
                PrototypeJson.EmptyObject()))
            .ToArray();
        var eligiblePlayer = new PlayerState(30, 30, 0,
            eligibleDeck, [], []);
        if (!NativeGloryAncientsV111.IsBeautifulBraceletEligible(
                eligiblePlayer))
        {
            return false;
        }

        var braceletOffered = false;
        for (var seedIndex = 0; seedIndex < 32; seedIndex++)
        {
            var eligibleBase = PrototypeNativeLaterActFixture.Create(
                $"coverage-beautiful-bracelet-offer-{seedIndex}", 3);
            var opening = NativeNonupeipeEventV111.BeginOpening(
                eligibleBase with { Player = eligiblePlayer });
            var evt = opening.World?.Event;
            if (evt?.NativeNonupeipeBraceletEligible == true
                && evt.OfferedChoiceIds?.Contains(
                    choiceId, StringComparer.Ordinal) == true)
            {
                braceletOffered = true;
                break;
            }
        }
        if (!braceletOffered)
            return false;

        var ineligibleBase = PrototypeNativeLaterActFixture.Create(
            "coverage-beautiful-bracelet-ineligible", 3);
        var ineligiblePlayer = ineligibleBase.Player with
        {
            Deck = eligibleDeck[..3]
        };
        var ineligibleOpening = NativeNonupeipeEventV111.BeginOpening(
            ineligibleBase with { Player = ineligiblePlayer });
        var ineligibleEvent = ineligibleOpening.World?.Event;
        if (ineligibleEvent?.NativeNonupeipeBraceletEligible != false
            || ineligibleEvent.OfferedChoiceIds?.Contains(
                choiceId, StringComparer.Ordinal) == true)
        {
            return false;
        }

        var acquired = NativeGloryAncientsV111
            .ApplyBeautifulBraceletAcquisition(
                eligiblePlayer,
                PrototypeRng.CreateBundle(
                    "coverage-beautiful-bracelet-acquisition"),
                nativeRunRng: null);
        return acquired.Deck.Count(card =>
                card.Enchantment is
                {
                    Kind: PrototypeCardEnchantmentKind.Swift,
                    Amount: 2
                }) == 4
            && acquired.Deck.Count(card => card.Enchantment is null) == 1
            && acquired.Deck.Select(card => card.InstanceId)
                .Order().SequenceEqual(eligibleDeck
                    .Select(card => card.InstanceId).Order());
    }

    private static bool HasFiddlePath()
    {
        var relicId = NativeGloryAncientsV111.FiddleId;
        var choiceId = "take_" + relicId;
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(3).Contains(
                NativeVakuuEventV111.EventId, StringComparer.Ordinal)
            || !HasCompleteAncientRelicBranches(
                NativeVakuuEventV111.EventId, 10)
            || !NativeVakuuEventV111.RelicPools[0].Contains(
                relicId, StringComparer.Ordinal)
            || !NativeVakuuEventV111.Definition.Choices.Any(choice =>
                choice.Id == choiceId)
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || PrototypeContent.Relic(relicId).HandDrawBonus != 2)
        {
            return false;
        }

        static RunState CreateState(bool isPlayerTurn, int turn = 1)
        {
            const string relicId = NativeGloryAncientsV111.FiddleId;
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject());
            var cards = Enumerable.Range(1, 10)
                .Select(instanceId => new CombatCardInstance(
                    instanceId, null, "proto.silent.strike", 0,
                    IsTemporary: true,
                    State: PrototypeJson.EmptyObject()))
                .ToArray();
            var combat = new CombatState(
                Turn: turn,
                Energy: PrototypeContent.Rules.BaseEnergy,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: cards.Select(card => card.InstanceId).ToArray(),
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20, 0,
                        0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 11,
                Cards: cards,
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [new CombatRelicState(0, relicId, 1, [])],
                Act: 3,
                PlayPile: [],
                IsPlayerTurn: isPlayerTurn);
            var player = new PlayerState(30, 30, 0, [], [relic], []);
            var world = new RunWorldState(
                RulesetId: PrototypeContent.RulesetId,
                CharacterId: PrototypeContent.CharacterId,
                Act: 3,
                Floor: 1,
                NextCardInstanceId: 11,
                ActiveRoom: PrototypeRoomType.Combat,
                Map: new MapState([]),
                Combat: combat,
                Reward: null,
                Shop: null,
                Event: null,
                TerminalOutcome: null);
            return new RunState(
                GameBuild: V111Build.Id,
                EmulatorSchema: V111Build.StateSchema,
                RunId: "coverage-fiddle",
                RunSeed: "coverage-fiddle",
                DecisionIndex: 0,
                Phase: RunPhase.Combat,
                Player: player,
                Rng: PrototypeRng.CreateBundle("coverage-fiddle"),
                ExtensionState: PrototypeJson.EmptyObject(),
                World: world);
        }

        var playerTurn = CreateState(isPlayerTurn: true);
        var opening = PrototypeGameEngine.DrawOpeningHand(playerTurn);
        var openingCombat = opening.World?.Combat;
        var nextTurnState = CreateState(isPlayerTurn: true, turn: 2);
        var drawPlayerHandStep = PrototypeContent.Rules.EndTurnPipeline
            .Single(step => step.Kind
                == PrototypeAutomaticStepKind.DrawPlayerHand);
        var nextTurn = PrototypeGameEngine.ResolveAutomaticStep(
            nextTurnState, drawPlayerHandStep);
        var inTurn = PrototypeGameEngine.DrawCards(
            playerTurn.Player,
            playerTurn.World!.Combat!,
            2,
            playerTurn.Rng,
            fromHandDraw: false);
        var otherSide = CreateState(isPlayerTurn: false);
        var otherSideDraw = PrototypeGameEngine.DrawCards(
            otherSide.Player,
            otherSide.World!.Combat!,
            2,
            otherSide.Rng,
            fromHandDraw: false);
        return PrototypeNativeOrobasRelics.HandDrawBonusForTurn(
                playerTurn.Player, 1) == 2
            && openingCombat?.Hand.Length
                == PrototypeContent.Rules.HandSize + 2
            && nextTurn.World?.Combat?.Hand.Length
                == PrototypeContent.Rules.HandSize + 2
            && inTurn.DrawnCardInstanceIds.Length == 0
            && inTurn.Combat.DrawPile.Length == 10
            && otherSideDraw.DrawnCardInstanceIds.Length == 2;
    }

    private static bool HasBloodSoakedRosePath()
    {
        var relicId = NativeGloryAncientsV111.BloodSoakedRoseId;
        var choiceId = "take_" + relicId;
        var definition = PrototypeContent.Relic(relicId);
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(3).Contains(
                NativeVakuuEventV111.EventId, StringComparer.Ordinal)
            || !HasCompleteAncientRelicBranches(
                NativeVakuuEventV111.EventId, 10)
            || !NativeVakuuEventV111.RelicPools[0].Contains(
                relicId, StringComparer.Ordinal)
            || !NativeVakuuEventV111.Definition.Choices.Any(choice =>
                choice.Id == choiceId)
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || definition.EnergyPerTurnBonus != 1
            || definition.RunTriggers?.Any(trigger =>
                trigger.EventKind == PrototypeRunEventKind.RelicAcquired
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.AddCard
                    && effect.CardId
                        == NativeGloryAncientsV111.EnthralledCardId)) != true)
        {
            return false;
        }

        static PlayerState Acquire(bool melted)
        {
            var relicId = NativeGloryAncientsV111.BloodSoakedRoseId;
            var player = new PlayerState(30, 30, 0, [],
            [new RelicInstance(relicId, PrototypeJson.EmptyObject(),
                IsMelted: melted)], []);
            long nextCardId = 1;
            return PrototypeGameEngine.ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId,
                rng: PrototypeRng.CreateBundle(
                    $"coverage-blood-soaked-rose-{melted}"),
                allocateCardId: () => nextCardId++);
        }

        var active = Acquire(melted: false);
        var melted = Acquire(melted: true);
        return active.Deck is
            [{ CardId: NativeGloryAncientsV111.EnthralledCardId }]
            && PrototypeGameEngine.EnergyPerTurn(active) ==
                PrototypeContent.Rules.BaseEnergy + 1
            && melted.Deck.Length == 0
            && PrototypeGameEngine.EnergyPerTurn(melted) ==
                PrototypeContent.Rules.BaseEnergy;
    }

    private static bool HasWhisperingEarringPath()
    {
        var relicId = NativeGloryAncientsV111.WhisperingEarringId;
        var choiceId = "take_" + relicId;
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(3).Contains(
                NativeVakuuEventV111.EventId, StringComparer.Ordinal)
            || !HasCompleteAncientRelicBranches(
                NativeVakuuEventV111.EventId, 10)
            || !NativeVakuuEventV111.RelicPools[0].Contains(
                relicId, StringComparer.Ordinal)
            || !NativeVakuuEventV111.Definition.Choices.Any(choice =>
                choice.Id == choiceId)
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || PrototypeContent.Relic(relicId).EnergyPerTurnBonus != 1
            || NativeGloryAncientsV111.WhisperingEarringMaxCards != 13)
        {
            return false;
        }

        static RunState CreateState(int turn, bool melted)
        {
            var relicId = NativeGloryAncientsV111.WhisperingEarringId;
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var cards = new[]
            {
                new CombatCardInstance(1, null, "proto.silent.strike", 0,
                    IsTemporary: true,
                    State: PrototypeJson.EmptyObject()),
                new CombatCardInstance(2, null, "proto.silent.neutralize", 0,
                    IsTemporary: true,
                    State: PrototypeJson.EmptyObject()),
                new CombatCardInstance(3, null, "proto.silent.defend", 0,
                    IsTemporary: true,
                    State: PrototypeJson.EmptyObject())
            };
            var combat = new CombatState(
                Turn: turn,
                Energy: PrototypeGameEngine.EnergyPerTurn(
                    new PlayerState(30, 30, 0, [], [relic], []), turn),
                PlayerBlock: 0,
                Hand: [1, 2, 3],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 30, 0,
                        0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 4,
                Cards: cards,
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [new CombatRelicState(0, relicId, 1, [],
                    IsMelted: melted)],
                Act: 3,
                PlayPile: []);
            var player = new PlayerState(30, 30, 0, [], [relic], []);
            var world = new RunWorldState(
                RulesetId: PrototypeContent.RulesetId,
                CharacterId: PrototypeContent.CharacterId,
                Act: 3,
                Floor: 1,
                NextCardInstanceId: 4,
                ActiveRoom: PrototypeRoomType.Combat,
                Map: new MapState([]),
                Combat: combat,
                Reward: null,
                Shop: null,
                Event: null,
                TerminalOutcome: null);
            return new RunState(
                GameBuild: V111Build.Id,
                EmulatorSchema: V111Build.StateSchema,
                RunId: "coverage-whispering-earring",
                RunSeed: "coverage-whispering-earring",
                DecisionIndex: 0,
                Phase: RunPhase.Combat,
                Player: player,
                Rng: PrototypeRng.CreateBundle(
                    "coverage-whispering-earring"),
                ExtensionState: PrototypeJson.EmptyObject(),
                World: world);
        }

        var opening = PrototypeGameEngine.ResolveWhisperingEarringAutoPlay(
            CreateState(turn: 1, melted: false));
        var openingCombat = opening.World?.Combat;
        var laterTurn = PrototypeGameEngine.ResolveWhisperingEarringAutoPlay(
            CreateState(turn: 2, melted: false)).World?.Combat;
        var melted = PrototypeGameEngine.ResolveWhisperingEarringAutoPlay(
            CreateState(turn: 1, melted: true)).World?.Combat;
        return openingCombat?.Energy == 2
            && openingCombat.PlayerBlock == 5
            && openingCombat.Enemies.Single().Hp == 21
            && openingCombat.Hand.Length == 0
            && openingCombat.DiscardPile.Length == 3
            && laterTurn?.Hand.Length == 3
            && melted?.Hand.Length == 3;
    }

    private static bool HasDiamondDiademPath()
    {
        var relicId = NativeGloryAncientsV111.DiamondDiademId;
        var choiceId = "take_" + relicId;
        var definition = PrototypeContent.Relic(relicId);
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(3).Contains(
                NativeNonupeipeEventV111.EventId, StringComparer.Ordinal)
            || !HasCompleteAncientRelicBranches(
                NativeNonupeipeEventV111.EventId, 10)
            || !NativeNonupeipeEventV111.Definition.Choices.Any(choice =>
                choice.Id == choiceId)
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || definition.Triggers?.Any(trigger =>
                trigger.EventKind
                    == PrototypeCombatEventKind.PlayerSideTurnStarted
                && trigger.TurnEquals == 1
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind.GainPlayerBlock
                    && effect.Amount == 20)
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind.ApplyPlayerPower
                    && effect.Amount == 1
                    && effect.PowerId == "proto.power.blur")) != true)
        {
            return false;
        }

        static CombatState Dispatch(int turn, bool melted)
        {
            var relicId = NativeGloryAncientsV111.DiamondDiademId;
            var triggerCount = PrototypeContent.Relic(relicId)
                .Triggers?.Length ?? 0;
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var player = new PlayerState(30, 30, 0, [], [relic], []);
            var combat = new CombatState(
                Turn: turn,
                Energy: PrototypeContent.Rules.BaseEnergy,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20, 0,
                        0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 1,
                Cards: [],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [new CombatRelicState(0, relicId, 1,
                    new int[triggerCount], IsMelted: melted)],
                Act: 3,
                PlayPile: []);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PlayerSideTurnStarted),
                PrototypeRng.CreateBundle(
                    $"coverage-diamond-diadem-{turn}-{melted}")).Combat;
        }

        var opening = Dispatch(turn: 1, melted: false);
        var laterTurn = Dispatch(turn: 2, melted: false);
        var melted = Dispatch(turn: 1, melted: true);
        return opening.PlayerBlock == 20
            && opening.PlayerPowers.SingleOrDefault(power =>
                power.PowerId == "proto.power.blur")?.Stacks == 1
            && laterTurn.PlayerBlock == 0
            && laterTurn.PlayerPowers.Length == 0
            && melted.PlayerBlock == 0
            && melted.PlayerPowers.Length == 0;
    }

    private static bool HasBiiigHugPath()
    {
        var relicId = NativeAncientRewardsV111.BiiigHugId;
        var rewardChoice = "take_" + relicId;
        var relic = PrototypeContent.Relic(relicId);
        var acquisition = relic.AcquisitionDeckChoice;
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(2).Contains(
                NativeAncientRewardsV111.TezcataraId,
                StringComparer.Ordinal)
            || !NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .Any(choice => choice.Id == rewardChoice
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainRelic
                        && effect.RelicId == relicId))
            || acquisition is not
                { Kind: PrototypePersistentDeckChoiceKind.Remove,
                    Selections: 4 }
            || relic.AfterShuffleGeneratedCardId
                != NativeAncientRewardsV111.SootId
            || PrototypeContent.Card(NativeAncientRewardsV111.SootId)
                .Type != PrototypeCardType.Status
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal))
        {
            return false;
        }

        static PrototypeGameEngine.PrototypeDrawCardsResult Draw(bool melted)
        {
            var relicId = NativeAncientRewardsV111.BiiigHugId;
            var source = new CombatCardInstance(
                1, null, "proto.silent.strike", 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject());
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var combat = new CombatState(
                Turn: 1,
                Energy: PrototypeContent.Rules.BaseEnergy,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [],
                DiscardPile: [source.InstanceId],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20, 0,
                        0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 2,
                Cards: [source],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [new CombatRelicState(0, relicId, 1, [],
                    IsMelted: melted)],
                Act: 2,
                PlayPile: []);
            var player = new PlayerState(30, 30, 0, [], [relic], []);
            return PrototypeGameEngine.DrawCards(
                player,
                combat,
                1,
                PrototypeRng.CreateBundle(
                    $"coverage-biiig-hug-{melted}"),
                fromHandDraw: true);
        }

        var active = Draw(melted: false);
        var melted = Draw(melted: true);
        var soot = active.Combat.Cards.SingleOrDefault(card =>
            card.CardId == NativeAncientRewardsV111.SootId);
        return soot is { IsTemporary: true }
            && active.Combat.NextCardInstanceId == 3
            && active.Combat.Hand.Concat(active.Combat.DrawPile)
                .Concat(active.Combat.DiscardPile)
                .Concat(active.Combat.ExhaustPile)
                .Contains(soot.InstanceId)
            && melted.Combat.Cards.Length == 1
            && melted.Combat.NextCardInstanceId == 2;
    }

    private static bool HasVeryHotCocoaPath()
    {
        var relicId = NativeAncientRewardsV111.VeryHotCocoaId;
        var choiceId = "take_" + relicId;
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(2).Contains(
                NativeAncientRewardsV111.TezcataraId,
                StringComparer.Ordinal)
            || !NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .Any(choice => choice.Id == choiceId
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainRelic
                        && effect.RelicId == relicId))
            || NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .Length != NativeAncientRewardsV111.Relics.Length
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || PrototypeContent.Relic(relicId).Triggers?.Any(trigger =>
                trigger.EventKind
                    == PrototypeCombatEventKind.PlayerSideTurnStarted
                && trigger.TurnEquals == 1
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind.GainEnergy
                    && effect.Amount == 4)) != true)
        {
            return false;
        }

        static CombatState Dispatch(int turn, bool melted)
        {
            var relicId = NativeAncientRewardsV111.VeryHotCocoaId;
            var triggerCount = PrototypeContent.Relic(relicId)
                .Triggers?.Length ?? 0;
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var player = new PlayerState(30, 30, 0, [], [relic], []);
            var combat = new CombatState(
                Turn: turn,
                Energy: PrototypeContent.Rules.BaseEnergy,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20, 0,
                        0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 1,
                Cards: [],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [new CombatRelicState(0, relicId, 1,
                    new int[triggerCount], IsMelted: melted)],
                Act: 2,
                PlayPile: []);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PlayerSideTurnStarted),
                PrototypeRng.CreateBundle(
                    $"coverage-very-hot-cocoa-{turn}-{melted}")).Combat;
        }

        var firstTurn = Dispatch(turn: 1, melted: false);
        var laterTurn = Dispatch(turn: 2, melted: false);
        var melted = Dispatch(turn: 1, melted: true);
        return firstTurn.Energy == PrototypeContent.Rules.BaseEnergy + 4
            && laterTurn.Energy == PrototypeContent.Rules.BaseEnergy
            && melted.Energy == PrototypeContent.Rules.BaseEnergy;
    }

    private static bool HasStorybookPath()
    {
        var relicId = NativeAncientRewardsV111.StorybookId;
        var cardId = NativeAncientRewardsV111.BrightestFlameId;
        var choiceId = "take_" + relicId;
        var relicDefinition = PrototypeContent.Relic(relicId);
        var cardDefinition = PrototypeContent.Card(cardId);
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(2).Contains(
                NativeAncientRewardsV111.TezcataraId,
                StringComparer.Ordinal)
            || !NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .Any(choice => choice.Id == choiceId
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainRelic
                        && effect.RelicId == relicId))
            || relicDefinition.RunTriggers?.Any(trigger =>
                trigger.EventKind == PrototypeRunEventKind.RelicAcquired
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.AddCard
                    && effect.CardId == cardId)) != true
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || cardDefinition.Type != PrototypeCardType.Skill
            || cardDefinition.Rarity != PrototypeCardRarity.Ancient
            || cardDefinition.MaxUpgradeLevel != 1
            || cardDefinition.InnateOnUpgrade
            || cardDefinition.RewardEligible
            || cardDefinition.CanBeGeneratedInCombat
            || !cardDefinition.Effects.Any(effect =>
                effect.Kind == PrototypeCombatEffectKind.GainEnergy
                && effect.Amount == 2
                && effect.UpgradeDelta == 1)
            || !cardDefinition.Effects.Any(effect =>
                effect.Kind == PrototypeCombatEffectKind.DrawCards
                && effect.Amount == 2
                && effect.UpgradeDelta == 1)
            || !cardDefinition.Effects.Any(effect =>
                effect.Kind == PrototypeCombatEffectKind.LosePlayerMaxHp
                && effect.Amount == 2))
        {
            return false;
        }

        static PlayerState Acquire(bool melted)
        {
            var relicId = NativeAncientRewardsV111.StorybookId;
            var player = new PlayerState(30, 30, 0, [],
            [new RelicInstance(relicId, PrototypeJson.EmptyObject(),
                IsMelted: melted)], []);
            long nextCardId = 1;
            return PrototypeGameEngine.ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId,
                rng: PrototypeRng.CreateBundle(
                    $"coverage-storybook-{melted}"),
                allocateCardId: () => nextCardId++);
        }

        var active = Acquire(melted: false);
        var melted = Acquire(melted: true);
        return active.Deck is
            [{ InstanceId: 1, CardId: NativeAncientRewardsV111.BrightestFlameId }]
            && melted.Deck.Length == 0;
    }

    private static bool HasYummyCookiePath()
    {
        var relicId = NativeAncientRewardsV111.YummyCookieId;
        var choiceId = "take_" + relicId;
        var relic = PrototypeContent.Relic(relicId);
        var acquisition = relic.AcquisitionDeckChoice;
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(2).Contains(
                NativeAncientRewardsV111.TezcataraId,
                StringComparer.Ordinal)
            || !NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .Any(choice => choice.Id == choiceId
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainRelic
                        && effect.RelicId == relicId))
            || acquisition is not
                { Kind: PrototypePersistentDeckChoiceKind.Upgrade,
                    Selections: 4 }
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal))
        {
            return false;
        }

        const string strikeId = "proto.silent.strike";
        if (PrototypeContent.Card(strikeId).MaxUpgradeLevel < 1)
            return false;

        var originalDeck = Enumerable.Range(1, 5)
            .Select(id => new CardInstance(id, strikeId, 0,
                PrototypeJson.EmptyObject()))
            .ToArray();
        var player = new PlayerState(
            30, 30, 0, originalDeck, [], []);
        var pending = PrototypeGameEngine.CreateRelicDeckChoice(
            player, relicId);
        if (pending is not
            { Kind: PrototypePersistentDeckChoiceKind.Upgrade,
                RemainingSelections: 4,
                CandidateCardInstanceIds.Length: 5,
                MinimumSelectionsRemaining: 4 })
        {
            return false;
        }

        var rng = PrototypeRng.CreateBundle("coverage-yummy-cookie");
        while (pending is { RemainingSelections: > 0 })
        {
            if (pending.CandidateCardInstanceIds.Length == 0)
                return false;
            var selectedId = pending.CandidateCardInstanceIds[0];
            var resolved = PrototypeGameEngine.ResolveRelicDeckChoice(
                player, pending, selectedId, rng);
            player = resolved.Player;
            pending = resolved.Remaining;
            if (pending?.CandidateCardInstanceIds.Contains(selectedId) == true)
                return false;
        }

        var upgraded = player.Deck
            .Where(card => card.UpgradeLevel == 1)
            .Select(card => card.InstanceId)
            .OrderBy(id => id)
            .ToArray();
        var unchanged = player.Deck
            .Where(card => card.UpgradeLevel == 0)
            .Select(card => card.InstanceId)
            .ToArray();
        return pending is null
            && player.Deck.Length == originalDeck.Length
            && player.Deck.Select(card => card.InstanceId)
                .OrderBy(id => id)
                .SequenceEqual(originalDeck.Select(card => card.InstanceId)
                    .OrderBy(id => id))
            && upgraded.Length == 4
            && unchanged.Length == 1
            && player.Deck.All(card => card.UpgradeLevel is 0 or 1);
    }

    private static bool HasToastyMittensPath()
    {
        var relicId = NativeAncientRewardsV111.ToastyMittensId;
        var choiceId = "take_" + relicId;
        var relicDefinition = PrototypeContent.Relic(relicId);
        var triggers = relicDefinition.Triggers ?? [];
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(2).Contains(
                NativeAncientRewardsV111.TezcataraId,
                StringComparer.Ordinal)
            || !NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .Any(choice => choice.Id == choiceId
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainRelic
                        && effect.RelicId == relicId))
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            || triggers.Length != 2
            || triggers[0].EventKind
                != PrototypeCombatEventKind.OpeningPlayerTurnStarted
            || triggers[1].EventKind
                != PrototypeCombatEventKind.PlayerTurnStarted
            || triggers[1].MinTurn != 2
            || triggers.Any(trigger => trigger.Effects is not
                [
                    { Kind: PrototypeCombatEffectKind.ChooseCards,
                        Selection: { SourceZone: PrototypeCardZone.Hand,
                            MinSelections: 1, MaxSelections: 1,
                            Resolution: PrototypeCardSelectionResolutionKind.MoveToExhaust } },
                    { Kind: PrototypeCombatEffectKind.ApplyPlayerPower,
                        PowerId: "proto.power.strength", Amount: 1 }
                ]))
        {
            return false;
        }

        static RunState CreateFixture(bool melted)
        {
            var relicId = NativeAncientRewardsV111.ToastyMittensId;
            var state = PrototypeNativeLaterActFixture.Create(
                $"coverage-toasty-mittens-{melted}", 2);
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var cards = Enumerable.Range(1, 5)
                .Select(id => new CombatCardInstance(
                    id, null, "proto.silent.strike", 0, false,
                    PrototypeJson.EmptyObject()))
                .ToArray();
            var triggerCount = PrototypeContent.Relic(relicId)
                .Triggers?.Length ?? 0;
            var combat = new CombatState(
                Turn: 1,
                Energy: PrototypeContent.Rules.BaseEnergy,
                PlayerBlock: 0,
                Hand: cards.Select(card => card.InstanceId).ToArray(),
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20, 0,
                        0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 6,
                Cards: cards,
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [new CombatRelicState(0, relicId, 1,
                    new int[triggerCount], IsMelted: melted)],
                Act: 2,
                PlayPile: []);
            var world = state.World!;
            return state with
            {
                Phase = RunPhase.Combat,
                Player = state.Player with { Relics = [relic] },
                World = world with
                {
                    Floor = Math.Max(1, world.Floor),
                    ActiveRoom = PrototypeRoomType.Combat,
                    Combat = combat
                }
            };
        }

        static RunState Dispatch(RunState state,
            PrototypeCombatEventKind eventKind, int turn)
        {
            var world = state.World!;
            var combat = world.Combat! with { Turn = turn };
            var result = PrototypeGameEngine.DispatchCombatEvent(
                state.Player, combat,
                new PrototypeCombatEvent(eventKind), state.Rng,
                allowSuspension: true);
            return state with
            {
                Player = result.Player,
                World = world with { Combat = result.Combat }
            };
        }

        static RunState? ResolvePrompt(RunState state)
        {
            var pending = state.World?.Combat?.PendingChoice;
            if (pending is null
                || pending.CandidateCardInstanceIds.Length == 0
                || pending.Selection.SourceZone != PrototypeCardZone.Hand
                || pending.Selection.Resolution
                    != PrototypeCardSelectionResolutionKind.MoveToExhaust)
            {
                return null;
            }

            var selectedId = pending.CandidateCardInstanceIds[0];
            return PrototypeGameEngine.ResolvePendingChoice(
                state,
                GameAction.Create("select_cards",
                    new SelectCardsPayload([selectedId])));
        }

        static int Strength(RunState state) =>
            state.World?.Combat?.PlayerPowers
                .SingleOrDefault(power =>
                    power.PowerId == "proto.power.strength")?.Stacks ?? 0;

        var active = CreateFixture(melted: false);
        active = Dispatch(active,
            PrototypeCombatEventKind.PlayerTurnStarted, turn: 1);
        if (active.World?.Combat?.PendingChoice is not null
            || Strength(active) != 0)
        {
            return false;
        }

        active = Dispatch(active,
            PrototypeCombatEventKind.OpeningPlayerTurnStarted, turn: 1);
        var openingChoice = active.World?.Combat?.PendingChoice;
        if (openingChoice is not
            { CandidateCardInstanceIds.Length: 5,
                Selection.MinSelections: 1,
                Selection.MaxSelections: 1 })
        {
            return false;
        }
        var resolvedOpening = ResolvePrompt(active);
        if (resolvedOpening is null)
            return false;
        active = resolvedOpening;
        if (Strength(active) != 1
            || active.World?.Combat?.ExhaustPile.Length != 1)
        {
            return false;
        }

        for (var turn = 2; turn <= 3; turn++)
        {
            active = Dispatch(active,
                PrototypeCombatEventKind.PlayerTurnStarted, turn);
            var pending = active.World?.Combat?.PendingChoice;
            if (pending is null
                || pending.CandidateCardInstanceIds.Length != 5 - turn + 1)
                return false;
            var resolved = ResolvePrompt(active);
            if (resolved is null)
                return false;
            active = resolved;
            if (Strength(active) != turn
                || active.World?.Combat?.ExhaustPile.Length != turn)
            {
                return false;
            }
        }

        var melted = CreateFixture(melted: true);
        melted = Dispatch(melted,
            PrototypeCombatEventKind.OpeningPlayerTurnStarted, turn: 1);
        return melted.World?.Combat?.PendingChoice is null
            && Strength(melted) == 0
            && melted.World?.Combat?.ExhaustPile.Length == 0;
    }

    private static bool HasSwordOfStoneJadePath(string nativeId)
    {
        const string stoneId = "proto.native.event.sword_of_stone";
        const string jadeId = "proto.native.event.sword_of_jade";
        const string overgrowthEventId = "proto.native.event.sunken_statue";
        const string underdocksEventId = "proto.native.underdocks.sunken_statue";
        var stone = PrototypeContent.Relic(stoneId);
        var jade = PrototypeContent.Relic(jadeId);

        bool GrantsSwordOfStone(string eventId, string choiceId) =>
            PrototypeContent.Event(eventId).Choices.Any(choice =>
                choice.Id == choiceId
                && choice.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.GainRelic
                    && effect.RelicId == stoneId));

        var jadeTriggerExists = jade.Triggers?.Any(trigger =>
            trigger.EventKind == PrototypeCombatEventKind.CombatStarted
            && trigger.Effects.Any(effect =>
                effect.Kind == PrototypeCombatEffectKind.ApplyPlayerPower
                && effect.Amount == 3
                && effect.PowerId == "proto.power.strength")) == true;
        var routeOffersExist =
            PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(
                overgrowthEventId)
            && PrototypeNativeUnderdocksEvents.IsSupported(underdocksEventId)
            && GrantsSwordOfStone(overgrowthEventId, "grab")
            && GrantsSwordOfStone(underdocksEventId, "sword");

        return routeOffersExist
            && stone.RelicIdAfterEliteWins == jadeId
            && stone.EliteWinsToTransform == 5
            && jadeTriggerExists
            && (nativeId == "SWORD_OF_STONE"
                ? stone.Id == stoneId
                : nativeId == "SWORD_OF_JADE" && jade.Id == jadeId);
    }

    private static bool HasSupportedBossRelicPath(
        string nativeId, string engineId)
    {
        if (!PrototypeContent.BossRelicPool.Contains(
                engineId, StringComparer.Ordinal))
        {
            return false;
        }

        var definition = PrototypeContent.Relic(engineId);
        return nativeId switch
        {
            "ASTROLABE" => engineId == "proto.relic.astrolabe"
                && definition.AcquisitionDeckChoice is
                {
                    Kind: PrototypePersistentDeckChoiceKind.Transform,
                    Selections: 3,
                    UpgradeTransformedCards: true
                },
            "SOZU" => engineId == "proto.relic.sozu"
                && definition.EnergyPerTurnBonus == 1
                && definition.PreventPotionAcquisition,
            "SNECKO_EYE" => engineId == "proto.relic.snecko_eye"
                && definition.HandDrawBonus == 2
                && definition.RandomizeDrawnCardCostMaxExclusive == 4,
            "VELVET_CHOKER" => engineId == "proto.relic.velvet_choker"
                && definition.EnergyPerTurnBonus == 1
                && definition.MaxCardsPlayablePerTurn == 6,
            _ => false
        };
    }

    private static string[] RestSiteOptionEngineIds(string optionId)
    {
        var actionId = PrototypeNativeRestSiteActionsV111
            .ForNativeOption(optionId);
        return actionId is null ? [] : [actionId];
    }

    private static bool HasArchaicToothSuppressPath()
    {
        var original = new CardInstance(1,
            PrototypeNativeOrobasRelics.NeutralizeId, 1,
            PrototypeJson.EmptyObject(),
            new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.Sharp, 2));
        var player = new PlayerState(20, 20, 0, [original],
            [new RelicInstance(PrototypeNativeOrobasRelics.ArchaicToothId,
                PrototypeJson.EmptyObject())], []);
        if (!PrototypeNativeOrobasRelics.IsArchaicToothEligible(player))
            return false;

        var transformed = PrototypeNativeOrobasRelics
            .ApplyArchaicToothAcquisition(player, 0).Deck.Single();
        var offerExists = NativeOrobasEventV111.Definition.Choices.Any(choice =>
            choice.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.GainRelic
                && effect.RelicId
                    == PrototypeNativeOrobasRelics.ArchaicToothId));
        var definition = PrototypeContent.Card(
            PrototypeNativeOrobasRelics.SuppressId);
        var baseDamage = definition.Effects.Any(effect =>
            effect.Kind == PrototypeCombatEffectKind.DamageEnemy
            && effect.Amount == 11 && effect.UpgradeDelta == 6);
        var upgradedWeak = definition.Effects.Any(effect =>
            effect.Kind == PrototypeCombatEffectKind.ApplyEnemyStatus
            && effect.StatusId == "proto.status.weak"
            && effect.Amount == 3 && effect.UpgradeDelta == 2);
        return offerExists
            && IsIntegratedEvent(NativeOrobasEventV111.EventId)
            && definition.Rarity == PrototypeCardRarity.Ancient
            && definition.Type == PrototypeCardType.Attack
            && definition.Target == PrototypeCardTarget.Enemy
            && definition.MechanicsImplemented
            && definition.MaxUpgradeLevel == 1
            && baseDamage && upgradedWeak
            && transformed.CardId == PrototypeNativeOrobasRelics.SuppressId
            && transformed.InstanceId == original.InstanceId
            && transformed.UpgradeLevel == original.UpgradeLevel
            && transformed.PersistentState.GetRawText()
                == original.PersistentState.GetRawText()
            && transformed.Enchantment == original.Enchantment;
    }

    private static bool HasTouchOrobasRingUpgradePath()
    {
        var player = new PlayerState(20, 20, 0, [],
            [new RelicInstance(PrototypeNativeOrobasRelics.TouchOfOrobasId,
                PrototypeJson.EmptyObject()),
             new RelicInstance(PrototypeNativeOrobasRelics.RingOfTheSnakeId,
                PrototypeJson.EmptyObject())], []);
        if (!NativeOrobasEventV111.IsChoiceEligible(player,
                "take_" + PrototypeNativeOrobasRelics.TouchOfOrobasId))
            return false;

        var upgraded = PrototypeNativeOrobasRelics
            .ApplyTouchOfOrobasAcquisition(player, 0);
        var touchChoiceExists = NativeOrobasEventV111.Definition.Choices.Any(
            choice => choice.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.GainRelic
                && effect.RelicId
                    == PrototypeNativeOrobasRelics.TouchOfOrobasId));
        var ringOfDrake = PrototypeContent.Relic(
            PrototypeNativeOrobasRelics.RingOfTheDrakeId);
        return touchChoiceExists
            && IsIntegratedEvent(NativeOrobasEventV111.EventId)
            && upgraded.Relics[0].RelicId
                == PrototypeNativeOrobasRelics.TouchOfOrobasId
            && upgraded.Relics[1].RelicId
                == PrototypeNativeOrobasRelics.RingOfTheDrakeId
            && ringOfDrake.HandDrawBonus == 2
            && ringOfDrake.HandDrawBonusThroughTurn == 3;
    }

    private static bool HasDowsingRodQuestPath()
    {
        const string cardId = "proto.native.neow.dowsing";
        var relicId = PrototypeNativeOvergrowthEvents
            .NeowRelicId("DowsingRod");
        var relic = PrototypeContent.Relic(relicId);
        var choiceExists = PrototypeContent.Event(
            PrototypeNativeOvergrowthEvents.NeowEventId).Choices.Any(choice =>
            choice.Id == "take_" + relicId
            && choice.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.GainRelic
                && effect.RelicId == relicId));
        var acquisitionAddsQuest = relic.RunTriggers?.Any(trigger =>
            trigger.EventKind == PrototypeRunEventKind.RelicAcquired
            && trigger.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.AddCard
                && effect.CardId == cardId)) == true;
        return PrototypeNativeOvergrowthEvents
                .NeowCoveredChoiceRelicNames.Contains(
                    "DowsingRod", StringComparer.Ordinal)
            && IsIntegratedEvent(
                PrototypeNativeOvergrowthEvents.NeowEventId)
            && choiceExists
            && acquisitionAddsQuest;
    }

    private static bool HasCompleteWarHistorianRepyBranches(
        V111InventoryItem item)
    {
        string[] expectedBranches =
        [
            "initial/UNLOCK_CAGE",
            "initial/UNLOCK_CHEST",
            "page/EXTRA_UNLOCK_CAGE",
            "page/EXTRA_UNLOCK_CHEST",
            "page/INITIAL",
            "page/INITIAL/UNLOCK_CAGE",
            "page/INITIAL/UNLOCK_CHEST",
            "page/UNLOCK_CAGE",
            "page/UNLOCK_CHEST"
        ];
        var definition = PrototypeContent.Event(
            PrototypeNativeHiveEvents.WarHistorianRepyId);
        var key = PrototypeContent.Card(
            PrototypeNativeHiveEvents.LanternKeyCardId);
        return item.Branches is { Length: 9 } branches
            && branches.Order(StringComparer.Ordinal).SequenceEqual(
                expectedBranches.Order(StringComparer.Ordinal),
                StringComparer.Ordinal)
            && IsIntegratedEvent(
                PrototypeNativeHiveEvents.WarHistorianRepyId)
            && definition.MinAct == 3
            && definition.MaxAct == 3
            && definition.Weight == 0
            && definition.Choices.Select(choice => choice.Id)
                .SequenceEqual(["unlock_cage", "unlock_chest"],
                    StringComparer.Ordinal)
            && key.Type == PrototypeCardType.Quest
            && key.Unplayable
            && key.MaxUpgradeLevel == 0;
    }

    private static bool HasJuzuUnknownRoomPath()
    {
        var before = new PrototypeUnknownRoomOddsState(
            MonsterWeight: 250,
            TreasureWeight: 20,
            ShopWeight: 30);
        var afterEvent = before.After(
            PrototypeRoomType.Event,
            monsterRoomAvailable: false);
        var afterTreasure = before.After(
            PrototypeRoomType.Treasure,
            monsterRoomAvailable: false);
        return afterEvent.MonsterWeight == 250
            && afterEvent.TreasureWeight == 40
            && afterEvent.ShopWeight == 60
            && afterTreasure.MonsterWeight == 250
            && afterTreasure.TreasureWeight == 20
            && afterTreasure.ShopWeight == 60;
    }

    private static bool HasFresnelLensPath()
    {
        const string relicId = "proto.native.underdocks.fresnel_lens";
        const string eventId = "proto.native.underdocks.drowning_beacon";
        var climb = PrototypeContent.Event(eventId).Choices
            .SingleOrDefault(choice => choice.Id == "climb");
        return PrototypeNativeUnderdocksEvents.IsSupported(eventId)
            && climb?.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.GainRelic
                && effect.RelicId == relicId) == true
            && PrototypeContent.Relic(relicId)
                .EnchantNewBlockCardsNimble == 2;
    }

    private static bool HasEmptyCagePath()
    {
        const string relicId = "proto.relic.empty_cage";
        var definition = PrototypeContent.Relic(relicId);
        return PrototypeContent.BossRelicPool.Contains(
                relicId, StringComparer.Ordinal)
            && definition.AcquisitionDeckChoice is
            {
                Kind: PrototypePersistentDeckChoiceKind.Remove,
                Selections: 2
            }
            && definition.CardIdAfterAcquisitionDeckChoices is null
            && definition.AcquisitionHpLossAfterDeckChoices == 0;
    }

    private static bool HasMummifiedHandPath() =>
        HasOrdinaryRareRelic("proto.relic.mummified_hand")
        && PrototypeContent.Relic("proto.relic.mummified_hand")
            .Triggers?.Any(trigger =>
                trigger.EventKind == PrototypeCombatEventKind.CardPlayed
                && trigger.RequiresPlayerSide
                && trigger.RequiredSourceCardType
                    == PrototypeCardType.Power
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind
                        .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed
                    && effect.Amount == 0)) == true;

    private static bool HasUnceasingTopPath() =>
        HasOrdinaryRareRelic("proto.relic.unceasing_top")
        && PrototypeContent.Relic("proto.relic.unceasing_top")
            .Triggers?.Any(trigger =>
                trigger.EventKind == PrototypeCombatEventKind.CardPlayed
                && trigger.RequiresPlayerSide
                && trigger.RequiresEmptyHand
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind.DrawCards
                    && effect.Amount == 1)) == true;

    private static bool HasOrdinaryRareRelic(string relicId) =>
        PrototypeContent.RelicPool.Contains(relicId, StringComparer.Ordinal)
        && PrototypeNativeRelicGrabBag.TryGetRarity(
            relicId, out var rarity)
        && rarity == PrototypeRelicRarity.Rare;

    private static bool HasChemicalXPath()
    {
        const string relicId = "proto.relic.chemical_x";
        return PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                relicId, out var rarity)
            && rarity == PrototypeRelicRarity.Shop
            && PrototypeContent.Relic(relicId).XValueBonus == 2;
    }

    private static bool HasRunicPyramidPath()
    {
        const string relicId = "proto.relic.runic_pyramid";
        return PrototypeContent.BossRelicPool.Contains(
                relicId, StringComparer.Ordinal)
            && PrototypeContent.Relic(relicId).PreventsHandDiscard;
    }

    private static bool HasPhilosophersStonePath()
    {
        const string relicId = "proto.relic.philosophers_stone";
        var definition = PrototypeContent.Relic(relicId);
        return PrototypeContent.BossRelicPool.Contains(
                relicId, StringComparer.Ordinal)
            && definition.EnergyPerTurnBonus == 1
            && definition.Triggers?.Any(trigger =>
                trigger.EventKind == PrototypeCombatEventKind.CombatStarted
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind.ApplyEnemyPower
                    && effect.Amount == 1
                    && effect.Target
                        == PrototypeEffectTarget.AllEnemies
                    && effect.PowerId == "proto.power.strength")) == true;
    }

    private static bool HasSpoilsMapPath()
    {
        var eventId = PrototypeNativeOvergrowthEvents
            .TheLegendsWereTrueId;
        var definition = PrototypeContent.Event(eventId);
        var nab = definition.Choices.SingleOrDefault(choice =>
            choice.Id == "nab_the_map");
        var exit = definition.Choices.SingleOrDefault(choice =>
            choice.Id == "slowly_find_an_exit");
        var card = PrototypeContent.Card(
            PrototypeNativeSpoilsMap.CardId);
        var player = new PlayerState(
            20,
            20,
            0,
            [new CardInstance(
                1,
                PrototypeNativeSpoilsMap.CardId,
                0,
                PrototypeNativeSpoilsMap.InitialPersistentState())],
            [],
            new PotionInstance?[3]);
        var treasure = new MapNodeState(
            "spoils-map-coverage",
            Act: 2,
            Floor: 3,
            PrototypeRoomType.Treasure);
        var assigned = PrototypeGameEngine
            .AssignSpoilsMapQuestToGeneratedMap(
                player,
                new MapState([treasure]),
                act: 2);
        var assignedState = PrototypeNativeSpoilsMap.ReadPersistentState(
            assigned.Player.Deck.Single());
        var completed = PrototypeGameEngine
            .CompleteSpoilsMapQuestAtNode(assigned.Player, treasure);
        return definition.MinAct == 1
            && definition.MaxAct == 1
            && PrototypeNativeOvergrowthEvents.RegionEventIds.Contains(
                eventId, StringComparer.Ordinal)
            && PrototypeNativeUnderdocksEvents.SupportedRegionEventIds
                .Contains(eventId, StringComparer.Ordinal)
            && PrototypeNativeOvergrowthEvents.UsesNativePotionOffer(eventId)
            && PrototypeNativeOvergrowthEvents.IsEligible(
                definition, player)
            && PrototypeNativeUnderdocksEvents.IsEligible(
                definition, player)
            && nab?.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.AddCard
                && effect.CardId == PrototypeNativeSpoilsMap.CardId) == true
            && exit?.Effects.Select(effect => effect.Kind).SequenceEqual(
                [PrototypeRunEffectKind.LoseHp,
                 PrototypeRunEffectKind.GainRandomPotion]) == true
            && exit.Effects[0].Amount == 8
            && card is
            {
                MechanicsImplemented: true,
                Unplayable: true,
                Type: PrototypeCardType.Quest,
                MaxUpgradeLevel: 0
            }
            && assignedState?.SpoilsActIndex == 1
            && assignedState.SpoilsNodeId == treasure.NodeId
            && completed.Gold == 600
            && completed.Deck.Length == 0;
    }

    private static bool HasCorruptedSymbiotePath()
    {
        var definition = PrototypeContent.Event(
            PrototypeNativeSharedEvents.SymbioteId);
        var approach = definition.Choices.SingleOrDefault(choice =>
            choice.Id == "approach");
        var locked = definition.Choices.SingleOrDefault(choice =>
            choice.Id == "approach_locked");
        var transform = definition.Choices.SingleOrDefault(choice =>
            choice.Id == "kill_with_fire");
        var attack = new CardInstance(
            1,
            "proto.silent.strike",
            0,
            PrototypeJson.EmptyObject());
        var nonAttack = new CardInstance(
            2,
            "proto.silent.defend",
            0,
            PrototypeJson.EmptyObject());
        var enchantEligible = PrototypeGameEngine.CanSelectEventDeckCard(
            attack,
            PrototypePersistentDeckChoiceKind.Enchant,
            null,
            PrototypeCardEnchantmentKind.Corrupted,
            basicCardsOnly: false);
        var nonAttackIneligible = !PrototypeGameEngine.CanSelectEventDeckCard(
            nonAttack,
            PrototypePersistentDeckChoiceKind.Enchant,
            null,
            PrototypeCardEnchantmentKind.Corrupted,
            basicCardsOnly: false);
        return definition.MinAct == 2
            && definition.MaxAct == 3
            && ExperimentalSilentThreeActRouteV111.HiveEvents.Contains(
                PrototypeNativeSharedEvents.SymbioteId,
                StringComparer.Ordinal)
            && ExperimentalSilentThreeActRouteV111.GloryEvents.Contains(
                PrototypeNativeSharedEvents.SymbioteId,
                StringComparer.Ordinal)
            && definition.Choices.Length == 3
            && approach?.DeckChoice is
                {
                    Kind: PrototypePersistentDeckChoiceKind.Enchant,
                    Selections: 1,
                    EnchantmentKind:
                        PrototypeCardEnchantmentKind.Corrupted,
                    RequiredCardType: PrototypeCardType.Attack
                }
            && locked is not null
            && transform?.DeckChoice is
                {
                    Kind: PrototypePersistentDeckChoiceKind.Transform,
                    Selections: 1
                }
            && enchantEligible
            && nonAttackIneligible
            && PrototypeNativeEventCompletionPages
                .HasTerminalChoicePage(
                    PrototypeNativeSharedEvents.SymbioteId);
    }

    private static string[] MissingBranches(V111InventoryItem item)
    {
        if (item.Kind == "events" && item.Id == "SLIPPERY_BRIDGE")
        {
            return HasCompleteSlipperyBridgeBranches(item)
                ? [] : item.Branches ?? [];
        }
        if (item.Kind == "events" && item.Id == "RANWID_THE_ELDER")
        {
            return HasCompleteRanwidBranches(item)
                ? [] : item.Branches ?? [];
        }
        if (item.Kind == "events" && item.Id == "ROOM_FULL_OF_CHEESE")
        {
            return HasCompleteRoomFullOfCheeseBranches(item)
                ? [] : item.Branches ?? [];
        }
        if (item.Kind == "events"
            && item.Id == "THE_LEGENDS_WERE_TRUE")
        {
            var expectedBranches = new[]
            {
                "initial/NAB_THE_MAP",
                "initial/SLOWLY_FIND_AN_EXIT",
                "page/INITIAL",
                "page/INITIAL/NAB_THE_MAP",
                "page/INITIAL/SLOWLY_FIND_AN_EXIT",
                "page/NAB_THE_MAP",
                "page/SLOWLY_FIND_AN_EXIT"
            };
            var supported = HasSpoilsMapPath()
                && item.Branches is { Length: 7 } branches
                && branches.Order(StringComparer.Ordinal).SequenceEqual(
                    expectedBranches.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            return supported ? [] : item.Branches ?? [];
        }
        if (item.Kind == "events" && item.Id == "SYMBIOTE")
        {
            var expectedBranches = new[]
            {
                "initial/APPROACH",
                "initial/APPROACH_LOCKED",
                "initial/KILL_WITH_FIRE",
                "page/APPROACH",
                "page/INITIAL",
                "page/INITIAL/APPROACH",
                "page/INITIAL/APPROACH_LOCKED",
                "page/INITIAL/KILL_WITH_FIRE",
                "page/KILL_WITH_FIRE"
            };
            var supported = HasCorruptedSymbiotePath()
                && item.Branches is { Length: 9 } branches
                && branches.Order(StringComparer.Ordinal).SequenceEqual(
                    expectedBranches.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            return supported ? [] : item.Branches ?? [];
        }
        if (item.Kind == "events"
            && item.Id == "WAR_HISTORIAN_REPY")
        {
            return HasCompleteWarHistorianRepyBranches(item)
                ? [] : item.Branches ?? [];
        }
        if (item.Kind == "events" && item.Id == "NEOW")
        {
            var supported = PrototypeNativeOvergrowthEvents
                .NeowCoveredChoiceRelicNames
                .Select(PrototypeNativeOvergrowthEvents.NeowRelicId)
                .Select(id => Normalize(id.Split('.').Last()))
                .ToHashSet(StringComparer.Ordinal);
            return (item.Branches ?? [])
                .Where(branch => !supported.Contains(
                    Normalize(branch.Split('/').Last())))
                .ToArray();
        }
        if (item.Kind == "events" && item.Id == "OROBAS")
        {
            var supported = NativeOrobasEventV111.Definition.Choices
                .SelectMany(choice => choice.Effects)
                .Where(effect => effect.Kind
                        == PrototypeRunEffectKind.GainRelic
                    && effect.RelicId is not null)
                .Select(effect => $"relic/{effect.RelicId!.Split('.').Last().ToUpperInvariant()}")
                .ToHashSet(StringComparer.Ordinal);
            return (item.Branches ?? [])
                .Where(branch => !supported.Contains(branch)).ToArray();
        }
        if (item.Kind == "events" && item.Id == "DARV")
        {
            var supported = NativeDarvEventV111.SupportedRelicIds
                .Select(id => $"relic/{id.Split('.').Last().ToUpperInvariant()}")
                .ToHashSet(StringComparer.Ordinal);
            return (item.Branches ?? []).Where(branch => !supported.Contains(branch))
                .ToArray();
        }
        if (item.Kind == "events"
            && item.Id == "THE_FUTURE_OF_POTIONS")
        {
            var supported = PrototypeNativeFutureOfPotions.CoveredBranches
                .ToHashSet(StringComparer.Ordinal);
            return (item.Branches ?? [])
                .Where(branch => !supported.Contains(branch)).ToArray();
        }
        if (item.Kind == "events" && item.Id == "RELIC_TRADER")
        {
            var supported = PrototypeNativeRelicTrader.CoveredBranches
                .ToHashSet(StringComparer.Ordinal);
            return (item.Branches ?? [])
                .Where(branch => !supported.Contains(branch)).ToArray();
        }
        return item.BranchInventoryComplete ? [] : item.Branches ?? [];
    }

    private static bool HasCompleteSlipperyBridgeBranches(
        V111InventoryItem item)
    {
        var expectedBranches = new List<string>
        {
            "initial/OVERCOME",
            "initial/HOLD_ON_0",
            "page/INITIAL",
            "page/INITIAL/OVERCOME",
            "page/INITIAL/HOLD_ON_0",
            "page/OVERCOME"
        };
        for (var hold = 0; hold < 7; hold++)
        {
            var current = $"HOLD_ON_{hold}";
            var next = hold == 6 ? "HOLD_ON_LOOP" : $"HOLD_ON_{hold + 1}";
            expectedBranches.Add($"page/{current}");
            expectedBranches.Add($"page/{current}/{next}");
        }
        expectedBranches.Add("page/HOLD_ON_LOOP");
        expectedBranches.Add("page/HOLD_ON_LOOP/HOLD_ON_LOOP");

        return HasSlipperyBridgePath()
            && item.Branches is { } branches
            && branches.Order(StringComparer.Ordinal).SequenceEqual(
                expectedBranches.Order(StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private static bool HasSlipperyBridgePath()
    {
        var eventId = PrototypeNativeSlipperyBridge.EventId;
        var definition = PrototypeContent.Event(eventId);
        var expectedChoices = new[]
        {
            "overcome", "hold_on_0", "hold_on_1", "hold_on_2",
            "hold_on_3", "hold_on_4", "hold_on_5", "hold_on_6",
            "hold_on_loop"
        };
        return definition.Choices.Select(choice => choice.Id)
                .SequenceEqual(expectedChoices, StringComparer.Ordinal)
            && PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(eventId)
            && PrototypeNativeUnderdocksEvents.IsSupported(eventId)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(eventId, 2)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(eventId, 3)
            && PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                eventId)
            && PrototypeNativeEventCompletionPages.HasTerminalChoice(
                eventId, "overcome");
    }

    private static bool HasCompleteRanwidBranches(V111InventoryItem item)
    {
        string[] expectedBranches =
        [
            "initial/POTION", "initial/POTION_LOCKED",
            "initial/GOLD", "initial/RELIC", "initial/RELIC_LOCKED",
            "page/INITIAL", "page/INITIAL/POTION",
            "page/INITIAL/POTION_LOCKED", "page/INITIAL/GOLD",
            "page/INITIAL/RELIC", "page/INITIAL/RELIC_LOCKED",
            "page/POTION", "page/GOLD", "page/RELIC"
        ];
        return HasRanwidPath()
            && item.Branches is { } branches
            && branches.Order(StringComparer.Ordinal).SequenceEqual(
                expectedBranches.Order(StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private static bool HasRanwidPath()
    {
        var eventId = PrototypeNativeRanwidTheElder.EventId;
        var definition = PrototypeContent.Event(eventId);
        var expectedChoices = new[]
        {
            "potion", "potion_locked", "gold", "relic", "relic_locked"
        };
        return definition.Choices.Select(choice => choice.Id)
                .SequenceEqual(expectedChoices, StringComparer.Ordinal)
            && PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                eventId)
            && new[] { "potion", "gold", "relic" }.All(choice =>
                PrototypeNativeEventCompletionPages.HasTerminalChoice(
                    eventId, choice))
            && PrototypeNativeRelicTrader.IsTradable(
                new RelicInstance(
                    "proto.relic.lantern", PrototypeJson.EmptyObject()))
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(eventId, 2)
            && ExperimentalSilentThreeActRouteV111.ContainsEvent(eventId, 3);
    }

    private static bool HasCompleteRoomFullOfCheeseBranches(
        V111InventoryItem item)
    {
        string[] expectedBranches =
        [
            "initial/GORGE", "initial/SEARCH", "page/INITIAL",
            "page/INITIAL/GORGE", "page/INITIAL/SEARCH",
            "page/GORGE", "page/SEARCH"
        ];
        return HasRoomFullOfCheesePath()
            && item.Branches is { } branches
            && branches.Order(StringComparer.Ordinal).SequenceEqual(
                expectedBranches.Order(StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private static bool HasRoomFullOfCheesePath()
    {
        var eventId = PrototypeNativeRoomFullOfCheese.EventId;
        var definition = PrototypeContent.Event(eventId);
        var relic = PrototypeContent.Relic(
            PrototypeNativeRoomFullOfCheese.ChosenCheeseId);
        return definition.Choices.Select(choice => choice.Id)
                .SequenceEqual(["gorge", "search"], StringComparer.Ordinal)
            && definition.MaxAct == 1
            && PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(eventId)
            && PrototypeNativeUnderdocksEvents.IsSupported(eventId)
            && PrototypeNativeUnderdocksEvents.NativeRegionEventIds.Contains(
                eventId, StringComparer.Ordinal)
            && PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                eventId)
            && PrototypeNativeEventCompletionPages.HasTerminalChoice(
                eventId, "gorge")
            && PrototypeNativeEventCompletionPages.HasTerminalChoice(
                eventId, "search")
            && PrototypeNativeRoomFullOfCheese.CardOfferCount == 8
            && PrototypeNativeRoomFullOfCheese.CardsToChoose == 2
            && PrototypeNativeRoomFullOfCheese.SearchHpLoss == 14
            && PrototypeContent.NativeSilentRewardCardPool.Any(id =>
                PrototypeContent.Card(id).Rarity
                    == PrototypeCardRarity.Common)
            && relic.MaxHpOnCombatVictory == 1;
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
        if (eventId == PrototypeNativeArchitectEnding.EventId)
            return true;
        if (PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(eventId))
            return true;
        if (PrototypeNativeUnderdocksEvents.IsSupported(eventId))
            return true;
        if (PrototypeNativeSharedEvents.IsSupported(eventId))
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

    private static bool HasConfiguredActRoute(V111InventoryItem item)
    {
        var act = item.Id switch
        {
            "OVERGROWTH" => ActIdentity.Overgrowth,
            "UNDERDOCKS" => ActIdentity.Underdocks,
            "HIVE" => ActIdentity.Hive,
            "GLORY" => ActIdentity.Glory,
            _ => (ActIdentity?)null
        };
        if (act is null)
            return false;

        var firstAct = act is ActIdentity.Underdocks
            ? ActIdentity.Underdocks
            : ActIdentity.Overgrowth;
        var configuration = RunConfiguration.Create(firstAct: firstAct) with
        {
            FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
        };
        configuration.Validate();
        if (!configuration.Acts.Contains(act.Value))
            return false;

        var (encounterIds, eventIds, ancientsReady) = act.Value switch
        {
            ActIdentity.Overgrowth => (
                PrototypeContent.OvergrowthWeakEncounterPool
                    .Concat(PrototypeContent.OvergrowthNormalEncounterPool)
                    .Concat(PrototypeContent.OvergrowthEliteEncounterPool)
                    .Concat(PrototypeContent.OvergrowthBossEncounterPool)
                    .ToArray(),
                PrototypeNativeOvergrowthEvents.RegionEventIds,
                PrototypeNativeOvergrowthEvents.NeowDefinition.Choices.Length > 0),
            ActIdentity.Underdocks => (
                PrototypeNativeUnderdocks.SupportedWeakEncounterIds
                    .Concat(PrototypeNativeUnderdocks.SupportedNormalEncounterIds)
                    .Concat(PrototypeNativeUnderdocks.SupportedEliteEncounterIds)
                    .Concat(PrototypeNativeUnderdocks.SupportedBossEncounterIds)
                    .ToArray(),
                PrototypeNativeUnderdocksEvents.SupportedRegionEventIds,
                PrototypeNativeOvergrowthEvents.NeowDefinition.Choices.Length > 0),
            ActIdentity.Hive => (
                ExperimentalSilentThreeActRouteV111.HiveWeak
                    .Concat(ExperimentalSilentThreeActRouteV111.HiveNormal)
                    .Concat(ExperimentalSilentThreeActRouteV111.HiveElites)
                    .Concat(ExperimentalSilentThreeActRouteV111.HiveBosses)
                    .ToArray(),
                ExperimentalSilentThreeActRouteV111.HiveEvents,
                NativeOrobasEventV111.Definition.Choices.Length > 0
                    && NativePaelRewardsV111.RewardDefinition.Choices.Length > 0
                    && NativeAncientRewardsV111.TezcataraRewardDefinition
                        .Choices.Length > 0),
            ActIdentity.Glory => (
                ExperimentalSilentThreeActRouteV111.GloryWeak
                    .Concat(ExperimentalSilentThreeActRouteV111.GloryNormal)
                    .Concat(ExperimentalSilentThreeActRouteV111.GloryElites)
                    .Concat(ExperimentalSilentThreeActRouteV111.GloryBosses)
                    .ToArray(),
                ExperimentalSilentThreeActRouteV111.GloryEvents,
                NativeNonupeipeEventV111.Definition.Choices.Length > 0
                    && NativeTanxEventV111.Definition.Choices.Length > 0
                    && NativeVakuuEventV111.Definition.Choices.Length > 0),
            _ => throw new ArgumentOutOfRangeException()
        };

        if (item.Encounters is not { Length: > 0 } sourceEncounters
            || encounterIds.Length != sourceEncounters.Length
            || encounterIds.Any(id => !PrototypeContent.Encounters.Any(
                encounter => encounter.Id == id))
            || sourceEncounters.Any(name => !encounterIds.Any(id =>
                Normalize(id.Split('.').Last()) == Normalize(name)))
            || !ancientsReady
            || eventIds.Length == 0
            || eventIds.Any(id => !PrototypeContent.Events.ContainsKey(id)))
        {
            return false;
        }

        var sourceEvents = item.Events ?? [];
        return sourceEvents
            .Where(name => !(act == ActIdentity.Hive
                && name == "COLORFUL_PHILOSOPHERS"))
            .All(name => eventIds.Any(id =>
                Normalize(id.Split('.').Last()) == Normalize(name)));
    }

    private static bool HasCompleteAncientRelicBranches(
        string eventId, int expectedCount)
    {
        if (!PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                eventId))
            return false;

        var choices = PrototypeContent.Event(eventId).Choices
            .Where(choice => choice.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.GainRelic
                && effect.RelicId is not null))
            .ToArray();
        if (choices.Length != expectedCount)
            return false;

        var relicIds = choices.Select(choice => choice.Effects
            .Where(effect => effect.Kind == PrototypeRunEffectKind.GainRelic
                && effect.RelicId is not null)
            .Select(effect => effect.RelicId!)
            .Single()).ToArray();
        return relicIds.Distinct(StringComparer.Ordinal).Count()
                == expectedCount
            && relicIds.All(relicId =>
                PrototypeContent.Relic(relicId).Id == relicId)
            && choices.All(choice =>
                PrototypeNativeEventCompletionPages.HasTerminalChoice(
                    eventId, choice.Id));
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
