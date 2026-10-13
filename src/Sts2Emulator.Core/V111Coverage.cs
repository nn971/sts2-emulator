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
            && item.Id is "DAUGHTER_OF_THE_WIND" or "MR_STRUGGLES"
                or "BING_BONG"
            && HasDollRoomRelicMechanic(item.Id))
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
            && item.Id == "SEAL_OF_GOLD"
            && engineIds.Contains(
                NativeAncientRewardsV111.SealOfGoldId,
                StringComparer.Ordinal)
            && HasSealOfGoldPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "PUMPKIN_CANDLE"
            && engineIds.Contains(
                NativeAncientRewardsV111.PumpkinCandleId,
                StringComparer.Ordinal)
            && HasPumpkinCandlePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "GOLDEN_COMPASS"
            && engineIds.Contains(
                NativeAncientRewardsV111.GoldenCompassId,
                StringComparer.Ordinal)
            && HasGoldenCompassPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "NUTRITIOUS_SOUP"
            && engineIds.Contains(
                NativeAncientRewardsV111.NutritiousSoupId,
                StringComparer.Ordinal)
            && HasNutritiousSoupPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "TOY_BOX"
            && engineIds.Contains(
                NativeAncientRewardsV111.ToyBoxId,
                StringComparer.Ordinal)
            && HasToyBoxPath())
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
            && item.Id == "DOLL_ROOM" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeDollRoom.EventId,
                StringComparer.Ordinal)
            && HasDollRoomPath())
            implementation = "implemented";
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "CRYSTAL_SPHERE" && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeCrystalSphere.EventId,
                StringComparer.Ordinal)
            && HasCrystalSpherePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "events"
            && item.Id == "WELCOME_TO_WONGOS"
            && missingBranches.Length == 0
            && engineIds.Contains(PrototypeNativeWongos.EventId,
                StringComparer.Ordinal)
            && HasWongosPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
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
            && item.Id == "BEATING_REMNANT"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.BeatingRemnantId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.BeatingRemnantId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.BeatingRemnantId,
                out var beatingRemnantRarity)
            && beatingRemnantRarity == PrototypeRelicRarity.Rare
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.BeatingRemnantId)
                .PlayerHpLossLimitPerTurn == 20
            && HasBeatingRemnantPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "STONE_CRACKER"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.StoneCrackerId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.StoneCrackerId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.StoneCrackerId,
                out var stoneCrackerRarity)
            && stoneCrackerRarity == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.StoneCrackerId).Triggers?
                .Any(trigger =>
                    trigger.EventKind
                        == PrototypeCombatEventKind.CombatStarted
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeCombatEffectKind
                            .UpgradeRandomDrawPileCards
                        && effect.Amount == 2)) == true
            && HasStoneCrackerPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "STRIKE_DUMMY"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.StrikeDummyId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.StrikeDummyId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.StrikeDummyId,
                out var strikeDummyRarity)
            && strikeDummyRarity == PrototypeRelicRarity.Common
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.StrikeDummyId) is
                { AttackDamageBonusRequiredCardTag: "Strike",
                    AttackDamageBonusPerCardTag: 3 }
            && HasStrikeDummyPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "STURDY_CLAMP"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.SturdyClampId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.SturdyClampId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.SturdyClampId,
                out var sturdyClampRarity)
            && sturdyClampRarity == PrototypeRelicRarity.Rare
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.SturdyClampId)
                .MaxPlayerBlockRetainedAfterClear == 10
            && HasSturdyClampPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "GHOST_SEED"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.GhostSeedId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.GhostSeedId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.GhostSeedId,
                out var ghostSeedRarity)
            && ghostSeedRarity == PrototypeRelicRarity.Shop
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.GhostSeedId)
                .EtherealizesBasicStrikesAndDefends
            && HasGhostSeedPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "ROYAL_STAMP"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.RoyalStampId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.RoyalStampId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.RoyalStampId,
                out var royalStampRarity)
            && royalStampRarity == PrototypeRelicRarity.Shop
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.RoyalStampId)
                .AcquisitionDeckChoice is
                { Kind: PrototypePersistentDeckChoiceKind.Enchant,
                    Selections: 1,
                    EnchantmentKind:
                        PrototypeCardEnchantmentKind.RoyallyApproved }
            && HasRoyalStampPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "MYSTIC_LIGHTER"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.MysticLighterId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.MysticLighterId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.MysticLighterId,
                out var mysticLighterRarity)
            && mysticLighterRarity == PrototypeRelicRarity.Shop
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.MysticLighterId)
                .EnchantedAttackDamageBonus == 9
            && HasMysticLighterPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "PEN_NIB"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.PenNibId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.PenNibId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.PenNibId,
                out var penNibRarity)
            && penNibRarity == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.PenNibId) is
                {
                    PersistentTriggerCountProperty: "AttacksPlayed",
                    PersistentTriggerCountModulo: 10
                } penNibDefinition
            && penNibDefinition.Triggers?.Any(trigger =>
                trigger.EventKind == PrototypeCombatEventKind.CardPlayed
                && trigger.RequiredSourceCardType
                    == PrototypeCardType.Attack) == true
            && HasPenNibPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "GNARLED_HAMMER"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.GnarledHammerId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.GnarledHammerId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.GnarledHammerId,
                out var gnarledHammerRarity)
            && gnarledHammerRarity == PrototypeRelicRarity.Shop
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.GnarledHammerId)
                .AcquisitionDeckChoice is
                {
                    Kind: PrototypePersistentDeckChoiceKind.Enchant,
                    Selections: 3,
                    MinimumSelections: 0,
                    EnchantmentKind:
                        PrototypeCardEnchantmentKind.Sharp,
                    EnchantmentAmount: 3
                }
            && HasGnarledHammerPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "KUSARIGAMA"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.KusarigamaId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.KusarigamaId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.KusarigamaId,
                out var kusarigamaRarity)
            && kusarigamaRarity == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.KusarigamaId).Triggers?.Any(
                trigger => trigger.EventKind
                        == PrototypeCombatEventKind.CardPlayed
                    && trigger.RequiredSourceCardType
                        == PrototypeCardType.Attack
                    && trigger.RequiresPlayerSide
                    && trigger.EveryNth == 3
                    && trigger.ResetCounterEachTurn
                    && trigger.Effects.Any(effect =>
                        effect.Kind
                            == PrototypeCombatEffectKind.DamageEnemy
                        && effect.Amount == 6
                        && effect.Target
                            == PrototypeEffectTarget.RandomEnemy)) == true
            && HasKusarigamaPath())
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
            && item.Id == "BELLOWS"
            && engineIds.Contains(PrototypeNativeSharedRelics.BellowsId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.BellowsId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.BellowsId, out var bellowsRarity)
            && bellowsRarity == PrototypeRelicRarity.Rare
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.BellowsId).Triggers?.Any(
                trigger => trigger.EventKind
                    == PrototypeCombatEventKind.PlayerSideTurnStarted
                    && trigger.TurnEquals == 1
                    && trigger.Effects.Any(effect =>
                        effect.Kind
                            == PrototypeCombatEffectKind.UpgradeHandCards))
                == true
            && HasBellowsPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "CANDELABRA"
            && engineIds.Contains(PrototypeNativeSharedRelics.CandelabraId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.CandelabraId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.CandelabraId,
                out var candelabraRarity)
            && candelabraRarity == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.CandelabraId).Triggers?.Any(
                trigger => trigger.EventKind
                    == PrototypeCombatEventKind.PlayerSideTurnStarted
                    && trigger.TurnEquals == 2
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeCombatEffectKind.GainEnergy
                        && effect.Amount == 2)) == true
            && HasCandelabraPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "CHANDELIER"
            && engineIds.Contains(PrototypeNativeSharedRelics.ChandelierId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.ChandelierId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.ChandelierId,
                out var chandelierRarity)
            && chandelierRarity == PrototypeRelicRarity.Rare
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.ChandelierId).Triggers?.Any(
                trigger => trigger.EventKind
                    == PrototypeCombatEventKind.PlayerSideTurnStarted
                    && trigger.TurnEquals == 3
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeCombatEffectKind.GainEnergy
                        && effect.Amount == 3)) == true
            && HasChandelierPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "FESTIVE_POPPER"
            && engineIds.Contains(PrototypeNativeSharedRelics.FestivePopperId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.FestivePopperId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.FestivePopperId,
                out var festivePopperRarity)
            && festivePopperRarity == PrototypeRelicRarity.Common
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.FestivePopperId).Triggers?.Any(
                trigger => trigger.EventKind
                    == PrototypeCombatEventKind.PlayerSideTurnStarted
                    && trigger.TurnEquals == 1
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeCombatEffectKind.DamageEnemy
                        && effect.Amount == 9
                        && effect.Target
                            == PrototypeEffectTarget.AllEnemies)) == true
            && HasFestivePopperPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "GAME_PIECE"
            && engineIds.Contains(PrototypeNativeSharedRelics.GamePieceId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.GamePieceId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.GamePieceId,
                out var gamePieceRarity)
            && gamePieceRarity == PrototypeRelicRarity.Rare
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.GamePieceId).Triggers?.Any(
                trigger => trigger.EventKind
                    == PrototypeCombatEventKind.CardPlayed
                    && trigger.RequiresPlayerSide
                    && trigger.RequiredSourceCardType
                        == PrototypeCardType.Power
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeCombatEffectKind.DrawCards
                        && effect.Amount == 1)) == true
            && HasGamePiecePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "REGAL_PILLOW"
            && engineIds.Contains(PrototypeNativeSharedRelics.RegalPillowId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.RegalPillowId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.RegalPillowId,
                out var regalPillowRarity)
            && regalPillowRarity == PrototypeRelicRarity.Common
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.RegalPillowId)
                .RestSiteHealBonus == 15
            && HasRegalPillowPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "CLOAK_CLASP"
            && engineIds.Contains(PrototypeNativeSharedRelics.CloakClaspId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.CloakClaspId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.CloakClaspId,
                out var cloakClaspRarity)
            && cloakClaspRarity == PrototypeRelicRarity.Rare
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.CloakClaspId).Triggers?.Any(
                trigger => trigger.EventKind
                    == PrototypeCombatEventKind.PlayerTurnEnded
                    && trigger.RequiresPlayerSide
                    && trigger.Effects.Any(effect =>
                        effect.Kind
                            == PrototypeCombatEffectKind.GainPlayerBlock
                        && effect.CountKind
                            == PrototypeCombatCountKind.OtherCardsInHand
                        && effect.AmountPerCount == 1)) == true
            && HasCloakClaspPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "TINY_MAILBOX"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.TinyMailboxId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.TinyMailboxId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.TinyMailboxId,
                out var tinyMailboxRarity)
            && tinyMailboxRarity == PrototypeRelicRarity.Uncommon
            && PrototypeContent.Relic(PrototypeNativeSharedRelics.TinyMailboxId)
                .RestHealPotionRewardCount == 2
            && HasTinyMailboxPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "WHITE_STAR"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.WhiteStarId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.WhiteStarId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.WhiteStarId,
                out var whiteStarRarity)
            && whiteStarRarity == PrototypeRelicRarity.Rare
            && PrototypeContent.Relic(PrototypeNativeSharedRelics.WhiteStarId)
                .ExtraEliteRareCardRewardCount == 1
            && HasWhiteStarPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "BREAD"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.BreadId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.BreadId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.BreadId,
                out var breadRarity)
            && breadRarity == PrototypeRelicRarity.Shop
            && PrototypeContent.Relic(PrototypeNativeSharedRelics.BreadId)
                is { EnergyPerTurnBonus: 1, EnergyBonusMinTurn: 2 }
            && HasBreadPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "GORGET"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.GorgetId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.GorgetId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.GorgetId,
                out var gorgetRarity)
            && gorgetRarity == PrototypeRelicRarity.Common
            && PrototypeContent.Relic(PrototypeNativeSharedRelics.GorgetId)
                .Triggers?.Any(trigger =>
                    trigger.EventKind
                        == PrototypeCombatEventKind.CombatStarted
                    && trigger.Effects.Any(effect =>
                        effect.Kind
                            == PrototypeCombatEffectKind.ApplyPlayerPower
                        && effect.Amount == 4
                        && effect.PowerId == "proto.power.plating")) == true
            && HasGorgetPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "AMETHYST_AUBERGINE"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.AmethystAubergineId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.AmethystAubergineId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.AmethystAubergineId,
                out var amethystAubergineRarity)
            && amethystAubergineRarity == PrototypeRelicRarity.Common
            && !PrototypeNativeRelicGrabBag.IsShopEligible(
                PrototypeNativeSharedRelics.AmethystAubergineId)
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.AmethystAubergineId)
                .CombatVictoryGoldReward == 15
            && HasAmethystAuberginePath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "RINGING_TRIANGLE"
            && engineIds.Contains(
                PrototypeNativeSharedRelics.RingingTriangleId,
                StringComparer.Ordinal)
            && PrototypeContent.RelicPool.Contains(
                PrototypeNativeSharedRelics.RingingTriangleId,
                StringComparer.Ordinal)
            && PrototypeNativeRelicGrabBag.TryGetRarity(
                PrototypeNativeSharedRelics.RingingTriangleId,
                out var ringingTriangleRarity)
            && ringingTriangleRarity == PrototypeRelicRarity.Shop
            && PrototypeContent.Relic(
                PrototypeNativeSharedRelics.RingingTriangleId)
                .PreventsFirstTurnHandDiscard
            && HasRingingTrianglePath())
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
            && item.Id == "WONGOS_MYSTERY_TICKET"
            && engineIds.Contains(
                PrototypeNativeWongos.MysteryTicketId,
                StringComparer.Ordinal)
            && !PrototypeContent.RelicPool.Contains(
                PrototypeNativeWongos.MysteryTicketId,
                StringComparer.Ordinal)
            && HasWongosPath())
        {
            implementation = "implemented";
            integration = "integrated";
        }
        if (item.Scope == "single-player" && item.Kind == "relics"
            && item.Id == "WONGO_CUSTOMER_APPRECIATION_BADGE"
            && engineIds.Contains(
                PrototypeNativeWongos.AppreciationBadgeId,
                StringComparer.Ordinal)
            && !PrototypeContent.RelicPool.Contains(
                PrototypeNativeWongos.AppreciationBadgeId,
                StringComparer.Ordinal)
            && HasWongosPath())
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

    private static bool HasBeatingRemnantPath()
    {
        const string relicId = PrototypeNativeSharedRelics.BeatingRemnantId;
        const string tungstenRodId = PrototypeNativeSharedRelics.TungstenRodId;
        var remnant = new RelicInstance(
            relicId, PrototypeJson.EmptyObject());
        var tungstenRod = new RelicInstance(
            tungstenRodId, PrototypeJson.EmptyObject());
        var player = new PlayerState(
            100, 100, 0, [], [remnant, tungstenRod], []);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: [],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var first = PrototypeGameEngine.ApplyPlayerHpLoss(
            player, combat, 15);
        var second = PrototypeGameEngine.ApplyPlayerHpLoss(
            first.Player, first.Combat, 15);
        var blockedByLimit = PrototypeGameEngine.ApplyPlayerHpLoss(
            second.Player, second.Combat, 4);
        var nextTurnCombat = blockedByLimit.Combat with
        {
            Counters = PrototypeGameEngine.AdvanceTurnCounters(
                blockedByLimit.Combat.CounterState)
        };
        var nextTurn = PrototypeGameEngine.ApplyPlayerHpLoss(
            blockedByLimit.Player, nextTurnCombat, 5);

        var meltedPlayer = player with
        {
            Hp = 100,
            Relics =
            [
                remnant with { IsMelted = true },
                tungstenRod with { IsMelted = true }
            ]
        };
        var melted = PrototypeGameEngine.ApplyPlayerHpLoss(
            meltedPlayer, combat, 30);

        return first.UnblockedHpLoss == 14
            && second.UnblockedHpLoss == 6
            && second.Player.Hp == 80
            && blockedByLimit.UnblockedHpLoss == 0
            && blockedByLimit.Player.Hp == 80
            && blockedByLimit.Combat.CounterState.HpLostThisTurn == 20
            && nextTurn.Player.Hp == 76
            && nextTurn.Combat.CounterState.HpLostThisTurn == 4
            && melted.UnblockedHpLoss == 30
            && melted.Player.Hp == 70
            && melted.Combat.CounterState.HpLostThisTurn == 0;
    }

    private static bool HasStoneCrackerPath()
    {
        static (PlayerState Player, CombatState Combat) Fixture(bool melted)
        {
            const string relicId =
                PrototypeNativeSharedRelics.StoneCrackerId;
            const string strikeId = "proto.silent.strike";
            const string defendId = "proto.silent.defend";
            const string dazedId = "proto.status.dazed";
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var cards = new[]
            {
                new CombatCardInstance(1, 1, strikeId, 0, false,
                    PrototypeJson.EmptyObject()),
                new CombatCardInstance(2, 2, defendId, 0, false,
                    PrototypeJson.EmptyObject()),
                new CombatCardInstance(3, 3, strikeId, 0, false,
                    PrototypeJson.EmptyObject()),
                new CombatCardInstance(4, 4, dazedId, 0, false,
                    PrototypeJson.EmptyObject()),
                new CombatCardInstance(5, 5, strikeId, 1, false,
                    PrototypeJson.EmptyObject()),
                new CombatCardInstance(6, 6, defendId, 0, false,
                    PrototypeJson.EmptyObject())
            };
            var deck = new[]
            {
                new CardInstance(1, strikeId, 0,
                    PrototypeJson.EmptyObject()),
                new CardInstance(2, defendId, 0,
                    PrototypeJson.EmptyObject()),
                new CardInstance(3, strikeId, 0,
                    PrototypeJson.EmptyObject()),
                new CardInstance(4, dazedId, 0,
                    PrototypeJson.EmptyObject()),
                new CardInstance(5, strikeId, 1,
                    PrototypeJson.EmptyObject()),
                new CardInstance(6, defendId, 0,
                    PrototypeJson.EmptyObject())
            };
            var player = new PlayerState(
                40, 40, 0, deck, [relic], []);
            var combat = new CombatState(
                Turn: 1,
                Energy: 3,
                PlayerBlock: 0,
                Hand: [6],
                DrawPile: [1, 2, 3, 4, 5],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 7,
                Cards: cards,
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1, new int[1],
                        IsMelted: melted)
                ],
                Act: 1,
                PlayPile: []);
            return (player, combat);
        }

        static (PlayerState Player, CombatState Combat) Dispatch(
            PlayerState player, CombatState combat, string seed) =>
            PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CombatStarted),
                PrototypeRng.CreateBundle(seed));

        var (player, combat) = Fixture(melted: false);
        var active = Dispatch(
            player, combat, "coverage-stone-cracker-active");
        var upgradedDrawCards = active.Combat.Cards.Count(card =>
            card.InstanceId is 1 or 2 or 3
            && card.UpgradeLevel == 1);
        var untouchedCards = active.Combat.Cards.Where(card =>
            card.InstanceId is 4 or 5 or 6).ToArray();

        var (meltedPlayer, meltedCombat) = Fixture(melted: true);
        var melted = Dispatch(
            meltedPlayer,
            meltedCombat,
            "coverage-stone-cracker-melted");

        return upgradedDrawCards == 2
            && untouchedCards.Single(card => card.InstanceId == 4)
                .UpgradeLevel == 0
            && untouchedCards.Single(card => card.InstanceId == 5)
                .UpgradeLevel == 1
            && untouchedCards.Single(card => card.InstanceId == 6)
                .UpgradeLevel == 0
            && active.Player.Deck.SequenceEqual(player.Deck)
            && melted.Combat.Cards.All(card => card.UpgradeLevel ==
                meltedCombat.Cards.Single(original =>
                    original.InstanceId == card.InstanceId).UpgradeLevel)
            && melted.Player.Deck.SequenceEqual(meltedPlayer.Deck);
    }

    private static bool HasStrikeDummyPath()
    {
        const string strikeId = "proto.silent.strike";
        const string shivId = "proto.silent.shiv";
        const int strikeInstanceId = 1;
        const int shivInstanceId = 2;

        CombatState Fixture(bool melted)
        {
            const string relicId =
                PrototypeNativeSharedRelics.StrikeDummyId;
            var triggerCount = PrototypeContent.Relic(relicId)
                .Triggers?.Length ?? 0;
            return new CombatState(
                Turn: 1,
                Energy: 3,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(3, "proto.enemy.wriggler", 30,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 3,
                Cards:
                [
                    new CombatCardInstance(strikeInstanceId, 1, strikeId, 0,
                        false, PrototypeJson.EmptyObject()),
                    new CombatCardInstance(shivInstanceId, 2, shivId, 0,
                        false, PrototypeJson.EmptyObject())
                ],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1,
                        new int[triggerCount], IsMelted: melted)
                ],
                Act: 1,
                PlayPile: []);
        }

        var activeCombat = Fixture(melted: false);
        var activeStrike = PrototypeGameEngine.ModifyPlayerAttackDamage(
            activeCombat, strikeInstanceId, 3, 6);
        var activeShiv = PrototypeGameEngine.ModifyPlayerAttackDamage(
            activeCombat, shivInstanceId, 3, 4);
        var meltedCombat = Fixture(melted: true);
        var meltedStrike = PrototypeGameEngine.ModifyPlayerAttackDamage(
            meltedCombat, strikeInstanceId, 3, 6);

        return activeStrike == 9
            && activeShiv == 4
            && meltedStrike == 6;
    }

    private static bool HasSturdyClampPath()
    {
        const string relicId = PrototypeNativeSharedRelics.SturdyClampId;
        var relic = new RelicInstance(
            relicId, PrototypeJson.EmptyObject());
        var player = new PlayerState(30, 30, 0, [], [relic], []);
        var meltedPlayer = player with
        {
            Relics = [relic with { IsMelted = true }]
        };

        static CombatState WithBlock(int block) => new(
            Turn: 1,
            Energy: 3,
            PlayerBlock: block,
            Hand: [],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: [],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var clamped = PrototypeGameEngine.ApplyPlayerBlockClear(
            player, WithBlock(17));
        var belowLimit = PrototypeGameEngine.ApplyPlayerBlockClear(
            player, WithBlock(6));
        var melted = PrototypeGameEngine.ApplyPlayerBlockClear(
            meltedPlayer, WithBlock(17));

        return clamped.PlayerBlock == 10
            && belowLimit.PlayerBlock == 6
            && melted.PlayerBlock == 0;
    }

    private static bool HasGhostSeedPath()
    {
        string[] cardIds =
        [
            "proto.silent.strike",
            "proto.silent.defend",
            "proto.silent.shiv",
            "proto.status.wound"
        ];
        static CombatState Fixture(bool melted, string[] ids)
        {
            const string relicId =
                PrototypeNativeSharedRelics.GhostSeedId;
            var triggerCount = PrototypeContent.Relic(relicId)
                .Triggers?.Length ?? 0;
            var cards = ids.Select((id, index) =>
                new CombatCardInstance(index + 1, index + 1, id, 0,
                    false, PrototypeJson.EmptyObject())).ToArray();
            return new CombatState(
                Turn: 1,
                Energy: 3,
                PlayerBlock: 0,
                Hand: [1, 2, 3, 4],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies: [],
                NextCardInstanceId: 5,
                Cards: cards,
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1,
                        new int[triggerCount], IsMelted: melted)
                ],
                Act: 1,
                PlayPile: []);
        }

        var active = Fixture(false, cardIds);
        var melted = Fixture(true, cardIds);
        return PrototypeGameEngine.CardHasKeyword(
                active, 1, PrototypeCardKeyword.Ethereal)
            && PrototypeGameEngine.CardHasKeyword(
                active, 2, PrototypeCardKeyword.Ethereal)
            && !PrototypeGameEngine.CardHasKeyword(
                active, 3, PrototypeCardKeyword.Ethereal)
            && !PrototypeGameEngine.CardHasKeyword(
                active, 4, PrototypeCardKeyword.Ethereal)
            && !PrototypeGameEngine.CardHasKeyword(
                melted, 1, PrototypeCardKeyword.Ethereal)
            && !PrototypeGameEngine.CardHasKeyword(
                melted, 2, PrototypeCardKeyword.Ethereal);
    }

    private static bool HasRoyalStampPath()
    {
        const string relicId = PrototypeNativeSharedRelics.RoyalStampId;
        const string strikeId = "proto.silent.strike";
        const string defendId = "proto.silent.defend";
        const string powerId = "proto.silent.footwork";
        var player = new PlayerState(
            30, 30, 0,
            [
                new CardInstance(1, strikeId, 0,
                    PrototypeJson.EmptyObject()),
                new CardInstance(2, defendId, 0,
                    PrototypeJson.EmptyObject()),
                new CardInstance(3, powerId, 0,
                    PrototypeJson.EmptyObject())
            ],
            [new RelicInstance(relicId, PrototypeJson.EmptyObject())],
            []);
        var pending = PrototypeGameEngine.CreateRelicDeckChoice(
            player, relicId);
        if (pending is not
            { Kind: PrototypePersistentDeckChoiceKind.Enchant,
                RemainingSelections: 1 }
            || !pending.CandidateCardInstanceIds
                .Order().SequenceEqual(new long[] { 1, 2 })
            || PrototypeGameEngine.CanSelectEventDeckCard(
                player.Deck[2],
                PrototypePersistentDeckChoiceKind.Enchant,
                null,
                PrototypeCardEnchantmentKind.RoyallyApproved,
                false))
        {
            return false;
        }

        var enchanted = player.Deck[0] with
        {
            Enchantment = new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.RoyallyApproved)
        };
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: [],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(1, 1, strikeId, 0, false,
                    PrototypeJson.EmptyObject(), Enchantment:
                        enchanted.Enchantment)
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1,
            Act: 1,
            PlayPile: []);

        return PrototypeGameEngine.IsInnate(combat.Cards[0])
            && PrototypeGameEngine.CardHasKeyword(
                combat, 1, PrototypeCardKeyword.Innate)
            && PrototypeGameEngine.CardHasKeyword(
                combat, 1, PrototypeCardKeyword.Retain)
            && !PrototypeGameEngine.CanSelectEventDeckCard(
                enchanted,
                PrototypePersistentDeckChoiceKind.Enchant,
                null,
                PrototypeCardEnchantmentKind.RoyallyApproved,
                false);
    }

    private static bool HasMysticLighterPath()
    {
        const long strikeInstanceId = 1;
        static CombatState Fixture(
            bool melted, PrototypeCardEnchantment? enchantment)
        {
            const long strikeInstanceId = 1;
            const string relicId =
                PrototypeNativeSharedRelics.MysticLighterId;
            const string strikeId = "proto.silent.strike";
            var card = new CombatCardInstance(
                strikeInstanceId, 1, strikeId, 0, false,
                PrototypeJson.EmptyObject(), Enchantment: enchantment);
            return new CombatState(
                Turn: 1,
                Energy: 3,
                PlayerBlock: 0,
                Hand: [strikeInstanceId],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(3, "proto.enemy.wriggler", 30,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 2,
                Cards: [card],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1, [],
                        IsMelted: melted)
                ],
                Act: 1,
                PlayPile: []);
        }

        var enchanted = Fixture(
            false,
            new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.Sharp, Amount: 2));
        var ordinary = Fixture(melted: false, enchantment: null);
        var melted = Fixture(
            true,
            new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.Sharp, Amount: 2));

        var enchantedDamage = PrototypeGameEngine.ModifyPlayerAttackDamage(
            enchanted, strikeInstanceId, 3, 6);
        var ordinaryDamage = PrototypeGameEngine.ModifyPlayerAttackDamage(
            ordinary, strikeInstanceId, 3, 6);
        var meltedDamage = PrototypeGameEngine.ModifyPlayerAttackDamage(
            melted, strikeInstanceId, 3, 6);
        return enchantedDamage == 17
            && ordinaryDamage == 6
            && meltedDamage == 8;
    }

    private static bool HasPenNibPath()
    {
        const long strikeInstanceId = 1;
        const string relicId = PrototypeNativeSharedRelics.PenNibId;
        const string strikeId = "proto.silent.strike";
        var persistent = System.Text.Json.JsonSerializer.SerializeToElement(
            new { AttacksPlayed = 9 });
        var player = new PlayerState(30, 30, 0, [],
            [new RelicInstance(relicId, persistent)], []);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [strikeInstanceId],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(3, "proto.enemy.wriggler", 30,
                    0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(strikeInstanceId, 1, strikeId, 0,
                    false, PrototypeJson.EmptyObject())
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1,
            Relics:
            [
                new CombatRelicState(0, relicId, 1, [9])
            ],
            Act: 1,
            PlayPile: []);

        var tenthAttackDamage = PrototypeGameEngine.ModifyPlayerAttackDamage(
            combat, strikeInstanceId, 3, 6);
        var completed = PrototypeGameEngine.DispatchCombatEvent(
            player,
            combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CardPlayed,
                SourceCardInstanceId: strikeInstanceId,
                CardId: strikeId,
                IsPoweredAttack: true),
            PrototypeRng.CreateBundle("coverage-pen-nib"));
        var persistedCounter = completed.Player.Relics[0]
            .PersistentState.GetProperty("AttacksPlayed").GetInt32();
        var nextAttackDamage = PrototypeGameEngine.ModifyPlayerAttackDamage(
            completed.Combat, strikeInstanceId, 3, 6);
        var melted = combat with
        {
            Relics = [combat.RelicStates[0] with { IsMelted = true }]
        };
        var meltedAttackDamage = PrototypeGameEngine.ModifyPlayerAttackDamage(
            melted, strikeInstanceId, 3, 6);

        return tenthAttackDamage == 12
            && completed.Combat.RelicStates[0].TriggerCounts[0] == 10
            && persistedCounter == 0
            && nextAttackDamage == 6
            && meltedAttackDamage == 6;
    }

    private static bool HasGnarledHammerPath()
    {
        const string relicId = PrototypeNativeSharedRelics.GnarledHammerId;
        var player = new PlayerState(
            30, 30, 0,
            [
                new CardInstance(1, "proto.silent.strike", 0,
                    PrototypeJson.EmptyObject()),
                new CardInstance(2, "proto.silent.neutralize", 0,
                    PrototypeJson.EmptyObject()),
                new CardInstance(3, "proto.silent.shiv", 0,
                    PrototypeJson.EmptyObject()),
                new CardInstance(4, "proto.silent.defend", 0,
                    PrototypeJson.EmptyObject()),
                new CardInstance(5, "proto.status.wound", 0,
                    PrototypeJson.EmptyObject())
            ],
            [new RelicInstance(relicId, PrototypeJson.EmptyObject())],
            []);
        var pending = PrototypeGameEngine.CreateRelicDeckChoice(
            player, relicId);
        if (pending is not
                { Kind: PrototypePersistentDeckChoiceKind.Enchant,
                    RemainingSelections: 3,
                    MinimumSelectionsRemaining: 0 }
            || !pending.CandidateCardInstanceIds
                .Order().SequenceEqual(new long[] { 1, 2, 3 }))
        {
            return false;
        }

        var rng = PrototypeRng.CreateBundle("coverage-gnarled-hammer");
        var first = PrototypeGameEngine.ResolveRelicDeckChoice(
            player, pending, 1, rng);
        var second = PrototypeGameEngine.ResolveRelicDeckChoice(
            first.Player, first.Remaining!, 2, rng);
        var third = PrototypeGameEngine.ResolveRelicDeckChoice(
            second.Player, second.Remaining!, 3, rng);
        return third.Remaining is null
            && third.Player.Deck.Take(3).All(card =>
                card.Enchantment is
                {
                    Kind: PrototypeCardEnchantmentKind.Sharp,
                    Amount: 3
                })
            && third.Player.Deck.Skip(3).All(card =>
                card.Enchantment is null);
    }

    private static bool HasKusarigamaPath()
    {
        const long attackId = 1;
        const string relicId = PrototypeNativeSharedRelics.KusarigamaId;
        const string strikeId = "proto.silent.strike";
        var player = new PlayerState(30, 30, 0,
            [new CardInstance(1, strikeId, 0,
                PrototypeJson.EmptyObject())],
            [new RelicInstance(relicId, PrototypeJson.EmptyObject())], []);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [attackId],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(3, "proto.enemy.wriggler", 30,
                    0, 0, new Dictionary<string, int>(StringComparer.Ordinal)),
                new EnemyCombatState(4, "proto.enemy.wriggler", 30,
                    0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(attackId, 1, strikeId, 0,
                    false, PrototypeJson.EmptyObject())
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1,
            Relics: [new CombatRelicState(0, relicId, 1, [0])],
            Act: 2,
            PlayPile: []);
        var rng = PrototypeRng.CreateBundle("coverage-kusarigama");

        (PlayerState Player, CombatState Combat) PlayAttack(
            PlayerState currentPlayer, CombatState currentCombat) =>
            PrototypeGameEngine.DispatchCombatEvent(
                currentPlayer,
                currentCombat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CardPlayed,
                    SourceCardInstanceId: attackId,
                    CardId: strikeId,
                    IsPoweredAttack: true),
                rng);

        var first = PlayAttack(player, combat);
        var second = PlayAttack(first.Player, first.Combat);
        if (second.Combat.Enemies.Sum(enemy => enemy.Hp) != 60)
        {
            return false;
        }

        var third = PlayAttack(second.Player, second.Combat);
        if (third.Combat.Enemies.Sum(enemy => enemy.Hp) != 54
            || third.Combat.RelicStates[0].TriggerCounts[0] != 3)
        {
            return false;
        }

        var nextTurn = PrototypeGameEngine.DispatchCombatEvent(
            third.Player,
            third.Combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.PlayerTurnStarted),
            rng);
        if (nextTurn.Combat.RelicStates[0].TriggerCounts[0] != 0)
        {
            return false;
        }

        var fourth = PlayAttack(nextTurn.Player, nextTurn.Combat);
        var fifth = PlayAttack(fourth.Player, fourth.Combat);
        var sixth = PlayAttack(fifth.Player, fifth.Combat);
        var melted = combat with
        {
            Relics = [combat.RelicStates[0] with { IsMelted = true }]
        };
        var meltedFirst = PlayAttack(player, melted);
        var meltedSecond = PlayAttack(meltedFirst.Player, meltedFirst.Combat);
        var meltedThird = PlayAttack(meltedSecond.Player, meltedSecond.Combat);

        return sixth.Combat.Enemies.Sum(enemy => enemy.Hp) == 48
            && meltedThird.Combat.Enemies.Sum(enemy => enemy.Hp) == 60;
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

    private static bool HasBellowsPath()
    {
        var relicId = PrototypeNativeSharedRelics.BellowsId;
        var triggerCount = PrototypeContent.Relic(relicId)
            .Triggers?.Length ?? 0;

        CombatState Dispatch(int turn, bool melted)
        {
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var player = new PlayerState(
                30, 30, 0, [], [relic], []);
            var combatCards = new[]
            {
                new CombatCardInstance(1, 1, "proto.silent.strike", 0,
                    false, PrototypeJson.EmptyObject()),
                new CombatCardInstance(2, 2, "proto.silent.defend", 1,
                    false, PrototypeJson.EmptyObject()),
                new CombatCardInstance(3, 3, "proto.status.wound", 0,
                    false, PrototypeJson.EmptyObject()),
                new CombatCardInstance(4, 4, "proto.silent.strike", 0,
                    false, PrototypeJson.EmptyObject())
            };
            var combat = new CombatState(
                Turn: turn,
                Energy: 3,
                PlayerBlock: 0,
                Hand: [1, 2, 3],
                DrawPile: [4],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 5,
                Cards: combatCards,
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1,
                        new int[triggerCount], IsMelted: melted)
                ],
                Act: 1,
                PlayPile: []);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PlayerSideTurnStarted),
                PrototypeRng.CreateBundle(
                    $"coverage-bellows-{turn}-{melted}")).Combat;
        }

        var firstTurn = Dispatch(turn: 1, melted: false);
        var laterTurn = Dispatch(turn: 2, melted: false);
        var melted = Dispatch(turn: 1, melted: true);
        static int UpgradeLevel(CombatState state, long instanceId) =>
            state.Cards.Single(card => card.InstanceId == instanceId)
                .UpgradeLevel;
        return UpgradeLevel(firstTurn, 1) == 1
            && UpgradeLevel(firstTurn, 2) == 1
            && UpgradeLevel(firstTurn, 3) == 0
            && UpgradeLevel(firstTurn, 4) == 0
            && laterTurn.Cards.All(card => card.UpgradeLevel == 0)
            && melted.Cards.All(card => card.UpgradeLevel == 0);
    }

    private static bool HasCandelabraPath()
    {
        var relicId = PrototypeNativeSharedRelics.CandelabraId;
        var triggerCount = PrototypeContent.Relic(relicId)
            .Triggers?.Length ?? 0;

        int Dispatch(int turn, bool melted)
        {
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var player = new PlayerState(
                30, 30, 0, [], [relic], []);
            var combat = new CombatState(
                Turn: turn,
                Energy: 1,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
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
                Act: 1,
                PlayPile: []);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PlayerSideTurnStarted),
                PrototypeRng.CreateBundle(
                    $"coverage-candelabra-{turn}-{melted}")).Combat.Energy;
        }

        return Dispatch(turn: 1, melted: false) == 1
            && Dispatch(turn: 2, melted: false) == 3
            && Dispatch(turn: 2, melted: true) == 1;
    }

    private static bool HasChandelierPath()
    {
        var relicId = PrototypeNativeSharedRelics.ChandelierId;
        var triggerCount = PrototypeContent.Relic(relicId)
            .Triggers?.Length ?? 0;

        int Dispatch(int turn, bool melted)
        {
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var player = new PlayerState(
                30, 30, 0, [], [relic], []);
            var combat = new CombatState(
                Turn: turn,
                Energy: 1,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
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
                Act: 1,
                PlayPile: []);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PlayerSideTurnStarted),
                PrototypeRng.CreateBundle(
                    $"coverage-chandelier-{turn}-{melted}")).Combat.Energy;
        }

        return Dispatch(turn: 2, melted: false) == 1
            && Dispatch(turn: 3, melted: false) == 4
            && Dispatch(turn: 3, melted: true) == 1;
    }

    private static bool HasFestivePopperPath()
    {
        var relicId = PrototypeNativeSharedRelics.FestivePopperId;
        var triggerCount = PrototypeContent.Relic(relicId)
            .Triggers?.Length ?? 0;

        EnemyCombatState[] Dispatch(int turn, bool melted)
        {
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var player = new PlayerState(
                30, 30, 0, [], [relic], []);
            var combat = new CombatState(
                Turn: turn,
                Energy: 3,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 30,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal)),
                    new EnemyCombatState(2, "proto.enemy.wriggler", 20,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
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
                Act: 1,
                PlayPile: []);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PlayerSideTurnStarted),
                PrototypeRng.CreateBundle(
                    $"coverage-festive-popper-{turn}-{melted}")).Combat.Enemies;
        }

        static bool HasHp(
            EnemyCombatState[] enemies, int first, int second) =>
            enemies[0].Hp == first && enemies[1].Hp == second;
        return HasHp(Dispatch(turn: 1, melted: false), 21, 11)
            && HasHp(Dispatch(turn: 2, melted: false), 30, 20)
            && HasHp(Dispatch(turn: 1, melted: true), 30, 20);
    }

    private static bool HasGamePiecePath()
    {
        const string powerCardId = "proto.silent.footwork";
        const string attackCardId = "proto.silent.strike";
        var relicId = PrototypeNativeSharedRelics.GamePieceId;
        var triggerCount = PrototypeContent.Relic(relicId)
            .Triggers?.Length ?? 0;

        (PlayerState Player, CombatState Combat) Fixture(bool melted)
        {
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var player = new PlayerState(
                30, 30, 0, [], [relic], []);
            var combat = new CombatState(
                Turn: 1,
                Energy: 3,
                PlayerBlock: 0,
                Hand: [],
                DrawPile: [2, 3],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 5,
                Cards:
                [
                    new CombatCardInstance(1, 1, powerCardId, 0,
                        false, PrototypeJson.EmptyObject()),
                    new CombatCardInstance(2, 2, attackCardId, 0,
                        false, PrototypeJson.EmptyObject()),
                    new CombatCardInstance(3, 3, "proto.silent.defend", 0,
                        false, PrototypeJson.EmptyObject()),
                    new CombatCardInstance(4, 4, powerCardId, 0,
                        false, PrototypeJson.EmptyObject())
                ],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1,
                        new int[triggerCount], IsMelted: melted)
                ],
                Act: 1,
                PlayPile: [1, 4]);
            return (player, combat);
        }

        static PrototypeCombatEvent Played(long cardInstanceId, string cardId) =>
            new(PrototypeCombatEventKind.CardPlayed,
                SourceCardInstanceId: cardInstanceId,
                CardId: cardId);

        var (player, combat) = Fixture(melted: false);
        var first = PrototypeGameEngine.DispatchCombatEvent(
            player, combat, Played(1, powerCardId),
            PrototypeRng.CreateBundle("coverage-game-piece-first"));
        var second = PrototypeGameEngine.DispatchCombatEvent(
            first.Player, first.Combat, Played(4, powerCardId),
            PrototypeRng.CreateBundle("coverage-game-piece-second"));
        var (attackPlayer, attackCombat) = Fixture(melted: false);
        var attack = PrototypeGameEngine.DispatchCombatEvent(
            attackPlayer, attackCombat, Played(2, attackCardId),
            PrototypeRng.CreateBundle("coverage-game-piece-attack"));
        var (meltedPlayer, meltedCombat) = Fixture(melted: true);
        var melted = PrototypeGameEngine.DispatchCombatEvent(
            meltedPlayer, meltedCombat, Played(1, powerCardId),
            PrototypeRng.CreateBundle("coverage-game-piece-melted"));
        return first.Combat.Hand.Length == 1
            && first.Combat.DrawPile.Length == 1
            && second.Combat.Hand.Length == 2
            && second.Combat.DrawPile.Length == 0
            && attack.Combat.Hand.Length == 0
            && attack.Combat.DrawPile.Length == 2
            && melted.Combat.Hand.Length == 0
            && melted.Combat.DrawPile.Length == 2;
    }

    private static bool HasRegalPillowPath()
    {
        var basePlayer = new PlayerState(30, 80, 0, [], [], []);
        var expectedBase = Math.Max(
            1,
            basePlayer.MaxHp * PrototypeContent.Rules.RestHealPercent / 100);
        var pillow = new RelicInstance(
            PrototypeNativeSharedRelics.RegalPillowId,
            PrototypeJson.EmptyObject());
        var activePlayer = basePlayer with { Relics = [pillow] };
        var meltedPlayer = basePlayer with
        {
            Relics = [pillow with { IsMelted = true }]
        };
        return PrototypeGameEngine.RestSiteHealAmount(basePlayer)
                == expectedBase
            && PrototypeGameEngine.RestSiteHealAmount(activePlayer)
                == expectedBase + 15
            && PrototypeGameEngine.RestSiteHealAmount(meltedPlayer)
                == expectedBase;
    }

    private static bool HasCloakClaspPath()
    {
        var relicId = PrototypeNativeSharedRelics.CloakClaspId;
        var triggerCount = PrototypeContent.Relic(relicId)
            .Triggers?.Length ?? 0;

        (PlayerState Player, CombatState Combat) Fixture(
            int handSize, bool melted)
        {
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var player = new PlayerState(
                30, 30, 0, [], [relic], []);
            var cards = new[]
            {
                new CombatCardInstance(1, 1, "proto.silent.strike", 0,
                    false, PrototypeJson.EmptyObject()),
                new CombatCardInstance(2, 2, "proto.silent.defend", 0,
                    false, PrototypeJson.EmptyObject()),
                new CombatCardInstance(3, 3, "proto.status.wound", 0,
                    false, PrototypeJson.EmptyObject()),
                new CombatCardInstance(4, 4, "proto.silent.strike", 0,
                    false, PrototypeJson.EmptyObject())
            };
            var combat = new CombatState(
                Turn: 1,
                Energy: 3,
                PlayerBlock: 2,
                Hand: handSize == 0 ? [] : [1, 2, 3],
                DrawPile: [4],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 5,
                Cards: cards,
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1,
                        new int[triggerCount], IsMelted: melted)
                ],
                Act: 1,
                PlayPile: []);
            return (player, combat);
        }

        int EndTurnBlock(int handSize, bool melted)
        {
            var (player, combat) = Fixture(handSize, melted);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PlayerTurnEnded),
                PrototypeRng.CreateBundle(
                    $"coverage-cloak-clasp-{handSize}-{melted}")).Combat.PlayerBlock;
        }

        return EndTurnBlock(handSize: 3, melted: false) == 5
            && EndTurnBlock(handSize: 0, melted: false) == 2
            && EndTurnBlock(handSize: 3, melted: true) == 2;
    }

    private static bool HasRingingTrianglePath()
    {
        RunState Fixture(int turn, bool melted)
        {
            var state = PrototypeNativeLaterActFixture.Create(
                $"coverage-ringing-triangle-{turn}-{melted}", 2);
            var relic = new RelicInstance(
                PrototypeNativeSharedRelics.RingingTriangleId,
                PrototypeJson.EmptyObject(), IsMelted: melted);
            var player = state.Player with { Relics = [relic] };
            var combat = new CombatState(
                Turn: turn,
                Energy: 3,
                PlayerBlock: 0,
                Hand: [1, 2],
                DrawPile: [],
                DiscardPile: [],
                ExhaustPile: [],
                Enemies:
                [
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 3,
                Cards:
                [
                    new CombatCardInstance(1, 1, "proto.silent.strike", 0,
                        false, PrototypeJson.EmptyObject()),
                    new CombatCardInstance(2, 2, "proto.silent.defend", 0,
                        false, PrototypeJson.EmptyObject())
                ],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0,
                        PrototypeNativeSharedRelics.RingingTriangleId,
                        1, [], IsMelted: melted)
                ],
                Act: 2,
                PlayPile: []);
            return state with
            {
                Player = player,
                Phase = RunPhase.Combat,
                World = state.World! with
                {
                    ActiveRoom = PrototypeRoomType.Combat,
                    Combat = combat
                }
            };
        }

        RunState Discard(RunState state) =>
            PrototypeGameEngine.ResolveAutomaticStep(
                state,
                new PrototypeAutomaticStep(
                    PrototypeAutomaticStepKind.DiscardPlayerHand));

        var firstTurn = Discard(Fixture(turn: 1, melted: false));
        var laterTurn = Discard(Fixture(turn: 2, melted: false));
        var melted = Discard(Fixture(turn: 1, melted: true));
        return firstTurn.World?.Combat is
                { Hand.Length: 2, DiscardPile.Length: 0 }
            && laterTurn.World?.Combat is
                { Hand.Length: 0, DiscardPile.Length: 2 }
            && melted.World?.Combat is
                { Hand.Length: 0, DiscardPile.Length: 2 };
    }

    private static bool HasAmethystAuberginePath()
    {
        const string relicId =
            PrototypeNativeSharedRelics.AmethystAubergineId;
        var relic = new RelicInstance(
            relicId, PrototypeJson.EmptyObject());
        var active = new PlayerState(30, 30, 0, [], [relic], []);
        var melted = new PlayerState(30, 30, 0, [],
            [relic with { IsMelted = true }], []);
        var ectoplasm = new PlayerState(30, 30, 0, [],
            [relic, new RelicInstance(
                "proto.relic.ectoplasm", PrototypeJson.EmptyObject())], []);

        var bag = new PrototypeRelicBagState(
            [relicId], [], [], []);
        var beforeTreasureWorld = PrototypeNativeLaterActFixture
            .Create("coverage-amethyst-before-treasure", 3).World! with
        {
            Floor = PrototypeNativeLaterActMap.RoomRows(3) + 1 - 8,
            RelicBag = bag
        };
        var treasureWorld = beforeTreasureWorld with
        {
            Floor = PrototypeNativeLaterActMap.RoomRows(3) + 1 - 7
        };
        var beforeTreasure = PrototypeNativeRelicGrabBag.Draw(
            beforeTreasureWorld, new PlayerState(30, 30, 0, [], [], []),
            PrototypeRng.CreateBundle("coverage-amethyst-before"),
            merchant: false, forcedRarity: PrototypeRelicRarity.Common);
        var atTreasure = PrototypeNativeRelicGrabBag.Draw(
            treasureWorld, new PlayerState(30, 30, 0, [], [], []),
            PrototypeRng.CreateBundle("coverage-amethyst-treasure"),
            merchant: false, forcedRarity: PrototypeRelicRarity.Common);
        var shopWorld = beforeTreasureWorld with { Act = 2, Floor = 1 };
        var atShop = PrototypeNativeRelicGrabBag.Draw(
            shopWorld, new PlayerState(30, 30, 0, [], [], []),
            PrototypeRng.CreateBundle("coverage-amethyst-shop"),
            merchant: true, forcedRarity: PrototypeRelicRarity.Common);

        return PrototypeNativeAmethystAubergine.GoldBonus(
                active, PrototypeRoomType.Combat, 1, endsAct: false) == 15
            && PrototypeNativeAmethystAubergine.GoldBonus(
                active, PrototypeRoomType.Elite, 2, endsAct: false) == 15
            && PrototypeNativeAmethystAubergine.GoldBonus(
                active, PrototypeRoomType.Boss, 2, endsAct: true) == 15
            && PrototypeNativeAmethystAubergine.GoldBonus(
                active, PrototypeRoomType.Boss, 3, endsAct: false) == 15
            && PrototypeNativeAmethystAubergine.GoldBonus(
                active, PrototypeRoomType.Boss, 3, endsAct: true) == 0
            && PrototypeNativeAmethystAubergine.GoldBonus(
                melted, PrototypeRoomType.Combat, 1, endsAct: false) == 0
            && PrototypeNativeAmethystAubergine.GoldBonus(
                ectoplasm, PrototypeRoomType.Combat, 1, endsAct: false) == 0
            && PrototypeNativeAmethystAubergine.ExtraRewardOptions(
                active, 12, PrototypeRoomType.Combat, 2,
                endsAct: false).SequenceEqual([12, 15])
            && beforeTreasure.Id == relicId
            && atTreasure.Id is null
            && atShop.Id is null;
    }

    private static bool HasGorgetPath()
    {
        const string relicId = PrototypeNativeSharedRelics.GorgetId;
        var triggerCount = PrototypeContent.Relic(relicId)
            .Triggers?.Length ?? 0;

        (PlayerState Player, CombatState Combat) Start(bool melted)
        {
            var relic = new RelicInstance(
                relicId, PrototypeJson.EmptyObject(), IsMelted: melted);
            var player = new PlayerState(30, 30, 0, [], [relic], []);
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
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
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
                Act: 1,
                PlayPile: []);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CombatStarted),
                PrototypeRng.CreateBundle($"coverage-gorget-start-{melted}"));
        }

        var started = Start(melted: false);
        var meltedStart = Start(melted: true);
        var endTurn = PrototypeGameEngine.DispatchCombatEvent(
            started.Player,
            started.Combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.PlayerTurnEnded),
            PrototypeRng.CreateBundle("coverage-gorget-end"));
        return started.Combat.PlayerPowers.Any(power =>
                power.PowerId == "proto.power.plating" && power.Stacks == 4)
            && endTurn.Combat.PlayerBlock == 4
            && meltedStart.Combat.PlayerPowers.Length == 0;
    }

    private static bool HasBreadPath()
    {
        const string relicId = PrototypeNativeSharedRelics.BreadId;
        var relic = new RelicInstance(
            relicId, PrototypeJson.EmptyObject());
        var meltedRelic = relic with { IsMelted = true };
        var active = new PlayerState(30, 30, 0, [], [relic], []);
        var melted = new PlayerState(30, 30, 0, [], [meltedRelic], []);
        var triggerCount = PrototypeContent.Relic(relicId)
            .Triggers?.Length ?? 0;

        (PlayerState Player, CombatState Combat) Start(
            PlayerState player, bool isMelted)
        {
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
                    new EnemyCombatState(1, "proto.enemy.wriggler", 20,
                        0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
                ],
                NextCardInstanceId: 1,
                Cards: [],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1,
                Relics:
                [
                    new CombatRelicState(0, relicId, 1,
                        new int[triggerCount], IsMelted: isMelted)
                ],
                Act: 1,
                PlayPile: []);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PlayerSideTurnStarted),
                PrototypeRng.CreateBundle(
                    $"coverage-bread-start-{isMelted}"));
        }

        var activeTurnOne = Start(active, isMelted: false);
        var meltedTurnOne = Start(melted, isMelted: true);
        return PrototypeGameEngine.EnergyPerTurn(active) == 3
            && activeTurnOne.Combat.Energy == 1
            && PrototypeGameEngine.EnergyPerTurn(active, turn: 2) == 4
            && PrototypeGameEngine.EnergyPerTurn(active, turn: 3) == 4
            && meltedTurnOne.Combat.Energy == 3
            && PrototypeGameEngine.EnergyPerTurn(melted, turn: 2) == 3;
    }

    private static bool HasWhiteStarPath()
    {
        const string relicId = PrototypeNativeSharedRelics.WhiteStarId;
        var relic = new RelicInstance(
            relicId, PrototypeJson.EmptyObject());
        var active = new PlayerState(30, 30, 0, [], [relic], []);
        var melted = new PlayerState(30, 30, 0, [],
            [relic with { IsMelted = true }], []);
        var rareReward = PrototypeNativeCardRarityOdds.GenerateEncounterCards(
            3, 0, PrototypeRoomType.Boss,
            PrototypeNativeCardRarityOdds.InitialOffsetBasisPoints,
            PrototypeRng.CreateBundle("coverage-white-star-rare"), act: 2);

        var bag = new PrototypeRelicBagState(
            [], [], [relicId], []);
        var beforeTreasureWorld = PrototypeNativeLaterActFixture
            .Create("coverage-white-star-before-treasure", 3).World! with
        {
            Floor = PrototypeNativeLaterActMap.RoomRows(3) + 1 - 8,
            RelicBag = bag
        };
        var treasureWorld = beforeTreasureWorld with
        {
            Floor = PrototypeNativeLaterActMap.RoomRows(3) + 1 - 7
        };
        var beforeTreasure = PrototypeNativeRelicGrabBag.Draw(
            beforeTreasureWorld, new PlayerState(30, 30, 0, [], [], []),
            PrototypeRng.CreateBundle("coverage-white-star-before"),
            merchant: false, forcedRarity: PrototypeRelicRarity.Rare);
        var atTreasure = PrototypeNativeRelicGrabBag.Draw(
            treasureWorld, new PlayerState(30, 30, 0, [], [], []),
            PrototypeRng.CreateBundle("coverage-white-star-treasure"),
            merchant: false, forcedRarity: PrototypeRelicRarity.Rare);

        return PrototypeNativeWhiteStar
                .AdditionalRareCardRewardGroups(
                    active, PrototypeRoomType.Elite) == 1
            && PrototypeNativeWhiteStar
                .AdditionalRareCardRewardGroups(
                    active, PrototypeRoomType.Combat) == 0
            && PrototypeNativeWhiteStar
                .AdditionalRareCardRewardGroups(
                    active, PrototypeRoomType.Boss) == 0
            && PrototypeNativeWhiteStar
                .AdditionalRareCardRewardGroups(
                    melted, PrototypeRoomType.Elite) == 0
            && rareReward.Cards.All(card =>
                PrototypeContent.Card(card).Rarity
                    == PrototypeCardRarity.Rare)
            && beforeTreasure.Id == relicId
            && atTreasure.Id is null;
    }

    private static bool HasTinyMailboxPath()
    {
        const string relicId = PrototypeNativeSharedRelics.TinyMailboxId;
        var engine = new PrototypeGameEngine();

        RunState AtRest(string seed, bool melted)
        {
            var state = PrototypeNativeLaterActFixture.Create(seed, 3);
            var world = state.World!;
            var restNode = world.Map.Nodes.First(node =>
                node.RoomType == PrototypeRoomType.Rest);
            return state with
            {
                Player = state.Player with
                {
                    Relics = melted
                        ?
                        [
                            new RelicInstance(
                                relicId, PrototypeJson.EmptyObject(),
                                IsMelted: true)
                        ]
                        :
                        [
                            new RelicInstance(
                                relicId, PrototypeJson.EmptyObject()),
                            new RelicInstance(
                                PrototypeNativeUnderdocksTrashHeap
                                    .DreamCatcherId,
                                PrototypeJson.EmptyObject())
                        ]
                },
                World = world with
                {
                    Floor = restNode.Floor,
                    ActiveRoom = PrototypeRoomType.Rest,
                    Map = world.Map with
                    {
                        CurrentNodeId = restNode.NodeId
                    }
                },
                Phase = RunPhase.Rest
            };
        }

        var rest = AtRest("coverage-tiny-mailbox", melted: false);
        var heal = engine.GetLegalActions(rest).First(action =>
            action.Kind == PrototypeNativeRestSiteActionsV111.Heal);
        var reward = engine.Step(rest, heal).State;
        var rewardState = reward.World?.Reward;
        var options = rewardState?.ExtraPotionOptions
            ?? Array.Empty<string>();
        var takeFirst = engine.GetLegalActions(reward).FirstOrDefault(action =>
            action.Kind == "take_reward_extra_potion"
            && action.ReadPayload<ChooseExtraPotionRewardPayload>()
                .GroupIndex == 0);
        var afterPotion = takeFirst is null
            ? reward
            : engine.Step(reward, takeFirst).State;

        var meltedRest = AtRest(
            "coverage-tiny-mailbox-melted", melted: true);
        var meltedHeal = engine.GetLegalActions(meltedRest).First(action =>
            action.Kind == PrototypeNativeRestSiteActionsV111.Heal);
        var meltedResult = engine.Step(meltedRest, meltedHeal).State;
        return reward.Phase == RunPhase.Reward
            && rewardState?.CardOptions.Length == 3
            && options.Length == 2
            && options.All(id => PrototypeContent.Potions.ContainsKey(id))
            && takeFirst is not null
            && afterPotion.Player.PotionSlots.Any(potion => potion is not null)
            && afterPotion.World?.Reward?.ExtraPotionGroupsResolved is
                { Length: 2 } resolved
            && resolved[0]
            && !resolved[1]
            && meltedResult.Phase == RunPhase.MapChoice
            && meltedResult.World?.Reward is null;
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

    private static bool HasSealOfGoldPath()
    {
        var relicId = NativeAncientRewardsV111.SealOfGoldId;
        var choiceId = "take_" + relicId;
        var triggers = PrototypeContent.Relic(relicId).Triggers;
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
            || triggers is not
                [
                    { EventKind: PrototypeCombatEventKind.PlayerSideTurnStarted,
                        Effects:
                        [
                            { Kind: PrototypeCombatEffectKind.GainEnergyForGold,
                                Amount: 1, PlayerGoldCost: 3 }
                        ] }
                ])
        {
            return false;
        }

        static (PlayerState Player, CombatState Combat) Dispatch(
            int gold, params bool[] meltedFlags)
        {
            var relicId = NativeAncientRewardsV111.SealOfGoldId;
            var triggerCount = PrototypeContent.Relic(relicId)
                .Triggers?.Length ?? 0;
            var relics = meltedFlags.Select(melted =>
                new RelicInstance(relicId,
                    PrototypeJson.EmptyObject(), IsMelted: melted)).ToArray();
            var player = new PlayerState(30, 30, gold, [], relics, []);
            var combatRelics = meltedFlags.Select((melted, index) =>
                new CombatRelicState(index, relicId, index + 1,
                    new int[triggerCount], IsMelted: melted)).ToArray();
            var combat = new CombatState(
                Turn: 1,
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
                Relics: combatRelics,
                Act: 2,
                PlayPile: []);
            return PrototypeGameEngine.DispatchCombatEvent(
                player,
                combat,
                new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PlayerSideTurnStarted),
                PrototypeRng.CreateBundle(
                    $"coverage-seal-of-gold-{gold}-{meltedFlags.Length}"));
        }

        var belowThreshold = Dispatch(2, false);
        var exactThreshold = Dispatch(3, false);
        var duplicateShort = Dispatch(5, false, false);
        var duplicateAffordable = Dispatch(6, false, false);
        var meltedOnly = Dispatch(3, true);
        var meltedBeforeActive = Dispatch(3, true, false);
        var baseEnergy = PrototypeContent.Rules.BaseEnergy;
        return belowThreshold.Player.Gold == 2
            && belowThreshold.Combat.Energy == baseEnergy
            && exactThreshold.Player.Gold == 0
            && exactThreshold.Combat.Energy == baseEnergy + 1
            && duplicateShort.Player.Gold == 2
            && duplicateShort.Combat.Energy == baseEnergy + 1
            && duplicateAffordable.Player.Gold == 0
            && duplicateAffordable.Combat.Energy == baseEnergy + 2
            && meltedOnly.Player.Gold == 3
            && meltedOnly.Combat.Energy == baseEnergy
            && meltedBeforeActive.Player.Gold == 0
            && meltedBeforeActive.Combat.Energy == baseEnergy + 1;
    }

    private static bool HasPumpkinCandlePath()
    {
        var relicId = NativeAncientRewardsV111.PumpkinCandleId;
        var choiceId = "take_" + relicId;
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
            || PrototypeNativeRestSiteActionsV111.Kindle != "rest_kindle")
        {
            return false;
        }

        static PlayerState Acquire()
        {
            var relicId = NativeAncientRewardsV111.PumpkinCandleId;
            var player = new PlayerState(30, 30, 0, [],
                [new RelicInstance(relicId,
                    PrototypeJson.EmptyObject())], []);
            return PrototypeGameEngine.ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId,
                rng: PrototypeRng.CreateBundle(
                    "coverage-pumpkin-candle-acquisition"));
        }

        var acquired = Acquire();
        var candleIndex = Array.FindIndex(acquired.Relics,
            relic => relic.RelicId == relicId);
        if (candleIndex < 0
            || NativeAncientRewardsV111.KindleCount(
                acquired.Relics[candleIndex]) != 5
            || PrototypeGameEngine.EnergyPerTurn(acquired)
                != PrototypeContent.Rules.BaseEnergy + 1)
        {
            return false;
        }

        var state = PrototypeNativeLaterActFixture.Create(
            "coverage-pumpkin-candle-exit", 2) with
        {
            Player = acquired
        };
        for (var combatIndex = 0; combatIndex < 6; combatIndex++)
        {
            state = state with
            {
                DecisionIndex = 100 + combatIndex
            };
            state = NativeAncientRewardsV111.ApplyCombatEnd(state);
            var candle = state.Player.Relics[candleIndex];
            var expectedCharges = Math.Max(0, 4 - combatIndex);
            if (NativeAncientRewardsV111.KindleCount(candle)
                    != expectedCharges
                || PrototypeGameEngine.EnergyPerTurn(state.Player)
                    != PrototypeContent.Rules.BaseEnergy
                        + (expectedCharges > 0 ? 1 : 0))
            {
                return false;
            }

            var repeated = NativeAncientRewardsV111.ApplyCombatEnd(state);
            if (NativeAncientRewardsV111.KindleCount(
                    repeated.Player.Relics[candleIndex])
                != expectedCharges)
            {
                return false;
            }
        }

        var rekindled = NativeAncientRewardsV111.Rekindle(
            state.Player, candleIndex);
        var active = rekindled.Relics[candleIndex];
        var melted = active with { IsMelted = true };
        var meltedPlayer = rekindled with { Relics = [melted] };
        return NativeAncientRewardsV111.KindleCount(active) == 5
            && PrototypeGameEngine.EnergyPerTurn(rekindled)
                == PrototypeContent.Rules.BaseEnergy + 1
            && PrototypeGameEngine.EnergyPerTurn(meltedPlayer)
                == PrototypeContent.Rules.BaseEnergy
            && NativeAncientRewardsV111.KindleCount(
                NativeAncientRewardsV111.AfterCombatEnd(meltedPlayer)
                    .Relics[0]) == 5;
    }

    private static bool HasGoldenCompassPath()
    {
        var relicId = NativeAncientRewardsV111.GoldenCompassId;
        var choiceId = "take_" + relicId;
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(2).Contains(
                NativeAncientRewardsV111.TezcataraId,
                StringComparer.Ordinal)
            || !NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .Any(choice => choice.Id == choiceId
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainRelic
                        && effect.RelicId == relicId))
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal))
        {
            return false;
        }

        var expectedRooms = new[]
        {
            PrototypeRoomType.Combat,
            PrototypeRoomType.Unknown,
            PrototypeRoomType.Combat,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Combat,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Unknown,
            PrototypeRoomType.Treasure,
            PrototypeRoomType.Unknown,
            PrototypeRoomType.Treasure,
            PrototypeRoomType.Unknown,
            PrototypeRoomType.Shop,
            PrototypeRoomType.Elite,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Elite,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Boss
        };

        static RunState AddCompass(RunState before, string relicId)
        {
            var after = before with
            {
                Player = before.Player with
                {
                    Relics = before.Player.Relics.Append(
                        new RelicInstance(relicId,
                            PrototypeJson.EmptyObject())).ToArray()
                }
            };
            return PrototypeGoldenPathMapV111.ApplyAcquisition(
                before, after);
        }

        static bool HasGoldenMap(MapState map,
            PrototypeRoomType[] expectedRooms, int act)
        {
            if (map.GenerationProfileId
                    != PrototypeGoldenPathMapV111.ProfileId
                || map.Nodes.Length != expectedRooms.Length
                || map.EntryNodeIds is not { Length: 1 } entries
                || entries[0] != $"{act}:1:3")
            {
                return false;
            }

            for (var index = 0; index < expectedRooms.Length; index++)
            {
                var node = map.Nodes[index];
                var expectedNext = index + 1 < expectedRooms.Length
                    ? new[] { $"{act}:{index + 2}:3" }
                    : Array.Empty<string>();
                if (node.NodeId != $"{act}:{index + 1}:3"
                    || node.Act != act
                    || node.Floor != index + 1
                    || node.RoomType != expectedRooms[index]
                    || !(node.NextNodeIds ?? Array.Empty<string>())
                        .SequenceEqual(
                        expectedNext, StringComparer.Ordinal))
                {
                    return false;
                }
            }

            return map.Nodes[^1].Floor
                == PrototypeGoldenPathMapV111.BossFloor;
        }

        var before = PrototypeNativeLaterActFixture.Create(
            "coverage-golden-compass", 2);
        var acquired = AddCompass(before, relicId);
        var compass = acquired.Player.Relics[^1];
        if (PrototypeGoldenPathMapV111.AcquisitionActIndex(compass) != 1
            || !PrototypeGoldenPathMapV111.IsActive(
                acquired.Player, 2)
            || PrototypeGoldenPathMapV111.IsActive(
                acquired.Player, 3)
            || !HasGoldenMap(
                acquired.World!.Map, expectedRooms, 2)
            || acquired.World.Floor != before.World!.Floor
            || acquired.World.Map.CurrentNodeId is not null)
        {
            return false;
        }

        var midActWorld = before.World! with
        {
            Floor = 5,
            Map = before.World.Map with
            {
                CurrentNodeId = "2:5:3"
            }
        };
        var midActBefore = before with { World = midActWorld };
        var midActAcquired = AddCompass(midActBefore, relicId);
        return HasGoldenMap(
                midActAcquired.World!.Map, expectedRooms, 2)
            && midActAcquired.World.Floor == 5
            && midActAcquired.World.Map.CurrentNodeId == "2:5:3"
            && PrototypeGoldenPathMapV111.IsActive(
                midActAcquired.Player, 2);
    }

    private static bool HasNutritiousSoupPath()
    {
        var relicId = NativeAncientRewardsV111.NutritiousSoupId;
        var choiceId = "take_" + relicId;
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(2).Contains(
                NativeAncientRewardsV111.TezcataraId,
                StringComparer.Ordinal)
            || !NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .Any(choice => choice.Id == choiceId
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainRelic
                        && effect.RelicId == relicId))
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal))
        {
            return false;
        }

        const string strikeId = "proto.silent.strike";
        const string defendId = "proto.silent.defend";
        var strikeDefinition = PrototypeContent.Card(strikeId);
        if (strikeDefinition.Rarity != PrototypeCardRarity.Basic
            || strikeDefinition.Tags?.Contains(
                "Strike", StringComparer.Ordinal) != true)
        {
            return false;
        }

        var fixture = PrototypeNativeLaterActFixture.Create(
            "coverage-tezcatara-soup", 2);
        var eligibleDeck = new[]
        {
            new CardInstance(1, strikeId, 0,
                PrototypeJson.EmptyObject()),
            new CardInstance(2, strikeId, 0,
                PrototypeJson.EmptyObject()),
            new CardInstance(3, strikeId, 1,
                PrototypeJson.EmptyObject()),
            new CardInstance(4, strikeId, 0,
                PrototypeJson.EmptyObject(),
                new PrototypeCardEnchantment(
                    PrototypeCardEnchantmentKind.Sharp)),
            new CardInstance(5, defendId, 0,
                PrototypeJson.EmptyObject())
        };
        var originalPlayer = fixture.Player with { Deck = eligibleDeck };

        var soupOfferSeen = false;
        for (var seedIndex = 0; seedIndex < 64 && !soupOfferSeen;
             seedIndex++)
        {
            var seed = $"coverage-tezcatara-soup-{seedIndex}";
            var opening = NativeAncientRewardsV111.BeginTezcataraOpening(
                fixture with { RunSeed = seed, Player = originalPlayer });
            var evt = opening.World?.Event;
            var offers = evt?.OfferedChoiceIds;
            if (offers is not { Length: 3 })
                return false;
            NativeAncientRewardsV111.ValidateEventContext(opening);
            if (offers[0] == "take_" + relicId)
                soupOfferSeen = true;
        }
        if (!soupOfferSeen)
            return false;

        var noStrikePlayer = originalPlayer with
        {
            Deck =
            [new CardInstance(1, defendId, 0,
                PrototypeJson.EmptyObject())]
        };
        var noStrikeOpening =
            NativeAncientRewardsV111.BeginTezcataraOpening(
                fixture with
                {
                    RunSeed = "coverage-tezcatara-no-basic-strike",
                    Player = noStrikePlayer
                });
        var noStrikeOffers = noStrikeOpening.World?.Event?.OfferedChoiceIds;
        if (noStrikeOffers is not { Length: 3 }
            || noStrikeOffers[0] == "take_" + relicId)
        {
            return false;
        }
        NativeAncientRewardsV111.ValidateEventContext(noStrikeOpening);

        static PlayerState Acquire(
            string relicId, CardInstance[] deck, bool melted)
        {
            var player = new PlayerState(30, 30, 0, deck,
                [new RelicInstance(relicId,
                    PrototypeJson.EmptyObject(), IsMelted: melted)], []);
            return PrototypeGameEngine.ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId,
                rng: PrototypeRng.CreateBundle(
                    $"coverage-nutritious-soup-{melted}"));
        }

        var acquired = Acquire(relicId, eligibleDeck, melted: false);
        var melted = Acquire(relicId, eligibleDeck, melted: true);
        var acquiredCards = acquired.Deck.ToDictionary(
            card => card.InstanceId);
        var meltedCards = melted.Deck.ToDictionary(
            card => card.InstanceId);
        return acquiredCards.Count == eligibleDeck.Length
            && acquiredCards[1].Enchantment?.Kind
                == PrototypeCardEnchantmentKind.TezcatarasEmber
            && acquiredCards[2].Enchantment?.Kind
                == PrototypeCardEnchantmentKind.TezcatarasEmber
            && acquiredCards[3].Enchantment?.Kind
                == PrototypeCardEnchantmentKind.TezcatarasEmber
            && acquiredCards[4].Enchantment?.Kind
                == PrototypeCardEnchantmentKind.Sharp
            && acquiredCards[5].Enchantment is null
            && acquiredCards.All(pair =>
                pair.Value.CardId == eligibleDeck.Single(card =>
                    card.InstanceId == pair.Key).CardId
                && pair.Value.UpgradeLevel == eligibleDeck.Single(card =>
                    card.InstanceId == pair.Key).UpgradeLevel)
            && acquiredCards.Where(pair => pair.Key is 1 or 2 or 3)
                .All(pair => PrototypeCardRules.IsEternal(pair.Value))
            && meltedCards.All(pair => pair.Value.Enchantment
                == eligibleDeck.Single(card =>
                    card.InstanceId == pair.Key).Enchantment);
    }

    private static bool HasToyBoxPath()
    {
        var relicId = NativeAncientRewardsV111.ToyBoxId;
        var choiceId = "take_" + relicId;
        if (!ExperimentalSilentThreeActRouteV111.AncientPool(2).Contains(
                NativeAncientRewardsV111.TezcataraId,
                StringComparer.Ordinal)
            || !NativeAncientRewardsV111.TezcataraRewardDefinition.Choices
                .Any(choice => choice.Id == choiceId
                    && choice.Effects.Any(effect =>
                        effect.Kind == PrototypeRunEffectKind.GainRelic
                        && effect.RelicId == relicId))
            || PrototypeContent.RelicPool.Contains(
                relicId, StringComparer.Ordinal))
        {
            return false;
        }

        var engine = new PrototypeGameEngine();
        RunState? opening = null;
        for (var seedIndex = 0; seedIndex < 64; seedIndex++)
        {
            var seed = $"coverage-tezcatara-toy-box-{seedIndex}";
            var fixture = PrototypeNativeLaterActFixture.Create(seed, 2);
            var candidate = NativeAncientRewardsV111
                .BeginTezcataraOpening(fixture);
            NativeAncientRewardsV111.ValidateEventContext(candidate);
            if (candidate.World?.Event?.OfferedChoiceIds is
                { Length: 3 } offers
                && offers[2] == choiceId)
            {
                opening = candidate;
                break;
            }
        }

        if (opening is null)
        {
            return false;
        }

        var acquireAction = engine.GetLegalActions(opening)
            .FirstOrDefault(action => action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId
                    == choiceId);
        if (acquireAction is null)
        {
            return false;
        }

        var acquired = engine.Step(opening, acquireAction).State;
        var acquiredWorld = acquired.World;
        var acquiredReward = acquiredWorld?.Reward;
        if (acquired.Phase != RunPhase.Reward
            || acquiredReward is not
                { SourceRoom: "Event:proto.native.ancient.tezcatara",
                    ExtraRelicRewardIds.Length: 5,
                    ExtraRelicWaxFlags.Length: 5,
                    IndependentSelection: true } reward
            || reward.ExtraRelicWaxFlags.Any(flag => !flag)
            || reward.ExtraRelicRewardIds.Distinct(
                StringComparer.Ordinal).Count() != 5
            || acquiredWorld?.RelicBag is not { } reservedBag
            || reward.ExtraRelicRewardIds.Any(id =>
                !PrototypeNativeRelicGrabBag.TryGetRarity(id, out _)
                || reservedBag.Remaining.Contains(
                    id, StringComparer.Ordinal)))
        {
            return false;
        }

        var toyBox = acquired.Player.Relics.SingleOrDefault(
            relic => relic.RelicId == relicId);
        if (toyBox is null
            || NativeAncientRewardsV111.ToyBoxCombatsSeen(toyBox) != 0)
        {
            return false;
        }

        var state = acquired;
        var firstWaxIndex = state.Player.Relics.Length;
        for (var groupIndex = 0; groupIndex < 5; groupIndex++)
        {
            var action = engine.GetLegalActions(state)
                .FirstOrDefault(candidate =>
                    candidate.Kind == "take_reward_relic"
                    && candidate.ReadPayload<ChooseRelicPayload>().Index
                        == 0);
            if (action is null)
            {
                return false;
            }
            state = engine.Step(state, action).State;
            var acquiredWax = state.Player.Relics
                .Skip(firstWaxIndex).Take(groupIndex + 1).ToArray();
            if (acquiredWax.Length != groupIndex + 1
                || acquiredWax.Any(relic => !relic.IsWax))
            {
                return false;
            }
        }

        var toyBoxIndex = Array.FindIndex(state.Player.Relics,
            relic => relic.RelicId == relicId);
        var waxInventory = state.Player.Relics
            .Where(relic => relic.IsWax).ToArray();
        if (toyBoxIndex < 0
            || waxInventory.Length != 5
            || NativeAncientRewardsV111.ToyBoxCombatsSeen(
                state.Player.Relics[toyBoxIndex]) != 0)
        {
            return false;
        }

        var isolatedPlayer = state.Player with
        {
            Relics =
            [state.Player.Relics[toyBoxIndex], .. waxInventory]
        };
        var world = state.World! with
        {
            ActiveRoom = PrototypeRoomType.Combat,
            Combat = null,
            Reward = null,
            CustomRewardReturn = null,
            Event = null,
            LastCombatEndDecisionIndex = null
        };
        var lifecycle = state with
        {
            Phase = RunPhase.Combat,
            Player = isolatedPlayer,
            World = world,
            DecisionIndex = 1000
        };

        var meltedAtAcquisition = lifecycle with
        {
            DecisionIndex = lifecycle.DecisionIndex + 1,
            Player = lifecycle.Player with
            {
                Relics =
                [lifecycle.Player.Relics[0] with { IsMelted = true },
                    .. lifecycle.Player.Relics.Skip(1)]
            }
        };
        var afterMeltedAtAcquisition =
            NativeAncientRewardsV111.ApplyCombatEnd(meltedAtAcquisition);
        if (NativeAncientRewardsV111.ToyBoxCombatsSeen(
                afterMeltedAtAcquisition.Player.Relics[0]) != 0
            || afterMeltedAtAcquisition.Player.Relics.Skip(1)
                .Any(relic => relic.IsMelted))
        {
            return false;
        }

        for (var combatCount = 1; combatCount <= 15; combatCount++)
        {
            lifecycle = lifecycle with
            {
                DecisionIndex = lifecycle.DecisionIndex + 1
            };
            lifecycle = NativeAncientRewardsV111.ApplyCombatEnd(
                lifecycle);
            var currentToyBox = lifecycle.Player.Relics[0];
            var currentWax = lifecycle.Player.Relics.Skip(1).ToArray();
            var expectedMelted = combatCount / 3;
            if (NativeAncientRewardsV111.ToyBoxCombatsSeen(
                    currentToyBox) != combatCount
                || currentWax.Length != 5
                || currentWax.Take(expectedMelted)
                    .Any(relic => !relic.IsMelted)
                || currentWax.Skip(expectedMelted)
                    .Any(relic => relic.IsMelted))
            {
                return false;
            }

            var repeated = NativeAncientRewardsV111.ApplyCombatEnd(
                lifecycle);
            if (NativeAncientRewardsV111.ToyBoxCombatsSeen(
                    repeated.Player.Relics[0]) != combatCount
                || repeated.Player.Relics.Skip(1)
                    .Where(relic => relic.IsWax).Count(relic =>
                        relic.IsMelted) != expectedMelted)
            {
                return false;
            }
        }

        lifecycle = lifecycle with
        {
            DecisionIndex = lifecycle.DecisionIndex + 1
        };
        var capped = NativeAncientRewardsV111.ApplyCombatEnd(lifecycle);
        return NativeAncientRewardsV111.ToyBoxCombatsSeen(
                capped.Player.Relics[0]) == 15
            && capped.Player.Relics.Skip(1).All(relic => relic.IsMelted)
            && NativeAncientRewardsV111.ToyBoxCombatsSeen(
                afterMeltedAtAcquisition.Player.Relics[0]) == 0;
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
        if (item.Kind == "events" && item.Id == "DOLL_ROOM")
        {
            return HasCompleteDollRoomBranches(item)
                ? [] : item.Branches ?? [];
        }
        if (item.Kind == "events" && item.Id == "CRYSTAL_SPHERE")
        {
            return HasCompleteCrystalSphereBranches(item)
                ? [] : item.Branches ?? [];
        }
        if (item.Kind == "events" && item.Id == "WELCOME_TO_WONGOS")
        {
            return HasCompleteWongosBranches(item)
                ? [] : item.Branches ?? [];
        }
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

    private static bool HasCompleteDollRoomBranches(
        V111InventoryItem item)
    {
        var expectedBranches = PrototypeNativeDollRoom.CoveredBranches;
        var definition = PrototypeNativeDollRoom.Definition;
        return item.Branches is { Length: 14 } branches
            && branches.Order(StringComparer.Ordinal).SequenceEqual(
                expectedBranches.Order(StringComparer.Ordinal),
                StringComparer.Ordinal)
            && IsIntegratedEvent(PrototypeNativeDollRoom.EventId)
            && definition.MinAct == 2
            && definition.MaxAct == 2
            && definition.Choices.Select(choice => choice.Id)
                .SequenceEqual(
                [
                    "random", "take_some_time", "examine",
                    "daughter_of_the_wind", "mr_struggles", "fable"
                ], StringComparer.Ordinal)
            && PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                PrototypeNativeDollRoom.EventId)
            && PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                PrototypeNativeDollRoom.EventId) == 2
            && RelicChoiceEffectsMatch();

        static bool RelicChoiceEffectsMatch() =>
            PrototypeNativeDollRoom.Definition.Choices.Any(choice =>
                choice.Id == "daughter_of_the_wind"
                && choice.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.GainRelic
                    && effect.RelicId
                        == PrototypeNativeDollRoom.DaughterOfTheWindId))
            && PrototypeNativeDollRoom.Definition.Choices.Any(choice =>
                choice.Id == "mr_struggles"
                && choice.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.GainRelic
                    && effect.RelicId
                        == PrototypeNativeDollRoom.MrStrugglesId))
            && PrototypeNativeDollRoom.Definition.Choices.Any(choice =>
                choice.Id == "fable"
                && choice.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.GainRelic
                    && effect.RelicId == PrototypeNativeDollRoom.BingBongId));
    }

    private static bool HasDollRoomRelicMechanic(string nativeId)
    {
        var relicId = nativeId switch
        {
            "DAUGHTER_OF_THE_WIND" =>
                PrototypeNativeDollRoom.DaughterOfTheWindId,
            "MR_STRUGGLES" => PrototypeNativeDollRoom.MrStrugglesId,
            "BING_BONG" => PrototypeNativeDollRoom.BingBongId,
            _ => null
        };
        if (relicId is null
            || !PrototypeContent.Relics.ContainsKey(relicId)
            || !PrototypeNativeDollRoom.Definition.Choices.Any(choice =>
                choice.Effects.Any(effect =>
                    effect.Kind == PrototypeRunEffectKind.GainRelic
                    && effect.RelicId == relicId))
            || !ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeDollRoom.EventId, 2))
        {
            return false;
        }

        var relic = PrototypeContent.Relic(relicId);
        return nativeId switch
        {
            "DAUGHTER_OF_THE_WIND" => relic.Triggers?.Any(trigger =>
                trigger.EventKind == PrototypeCombatEventKind.CardPlayed
                && trigger.RequiredSourceCardType
                    == PrototypeCardType.Attack
                && trigger.Effects.Any(effect =>
                    effect.Kind == PrototypeCombatEffectKind.GainPlayerBlock
                    && effect.Amount == 1)) == true,
            "MR_STRUGGLES" => relic.Triggers is { } strugglesTriggers
                && new[]
                {
                    PrototypeCombatEventKind.OpeningPlayerTurnStarted,
                    PrototypeCombatEventKind.PlayerTurnStarted
                }.All(eventKind => strugglesTriggers.Count(trigger =>
                    trigger.EventKind == eventKind
                    && trigger.Effects.Any(effect =>
                        effect.Kind == PrototypeCombatEffectKind.DamageEnemy
                        && effect.Target == PrototypeEffectTarget.AllEnemies
                        && effect.AmountPerCount == 1
                        && effect.CountKind
                            == PrototypeCombatCountKind.TurnNumber)) == 1),
            "BING_BONG" => relic.RunTriggers?.Any(trigger =>
                trigger.EventKind == PrototypeRunEventKind.CardAdded
                && trigger.CopyAddedCardToDeckBottom) == true,
            _ => false
        };
    }

    private static bool HasDollRoomPath()
    {
        if (!HasDollRoomRelicMechanic("DAUGHTER_OF_THE_WIND")
            || !HasDollRoomRelicMechanic("MR_STRUGGLES")
            || !HasDollRoomRelicMechanic("BING_BONG"))
        {
            return false;
        }

        static RunState EventFixture(string seed)
        {
            var state = PrototypeNativeLaterActFixture.Create(seed, 2);
            var world = state.World!;
            var node = world.Map.Nodes.First(item =>
                item.RoomType == PrototypeRoomType.Unknown
                && item.NextNodeIds is { Length: > 0 });
            return state with
            {
                Phase = RunPhase.Event,
                World = world with
                {
                    Floor = node.Floor,
                    ActiveRoom = PrototypeRoomType.Event,
                    Map = world.Map with { CurrentNodeId = node.NodeId },
                    Event = new EventState(
                        PrototypeNativeDollRoom.EventId),
                    EventHistory = world.EventIds.Append(
                        PrototypeNativeDollRoom.EventId).ToArray()
                }
            };
        }

        var engine = new PrototypeGameEngine();
        var time = EventFixture("coverage-doll-room-time");
        if (!IsIntegratedEvent(PrototypeNativeDollRoom.EventId)
            || engine.GetLegalActions(time).Count != 3)
        {
            return false;
        }

        var timeChoice = engine.GetLegalActions(time).Single(action =>
            action.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_some_time");
        var timeOffer = engine.Step(time, timeChoice).State;
        if (timeOffer.Player.Hp != time.Player.Hp - 5
            || timeOffer.World?.Event is not
                { NativePageIndex: 1, OfferedChoiceIds.Length: 2 })
        {
            return false;
        }

        var take = engine.GetLegalActions(timeOffer).First();
        var received = engine.Step(timeOffer, take).State;
        var receivedChoice = take.ReadPayload<EventChoicePayload>().ChoiceId;
        var receivedRelic = PrototypeNativeDollRoom
            .RelicIdForChoice(receivedChoice);
        if (!received.Player.Relics.Any(relic =>
                relic.RelicId == receivedRelic)
            || received.World?.Event is not
                { NativePageIndex: 2, OfferedChoiceIds: null }
            || engine.GetLegalActions(received).Single().Kind
                != "continue_event")
        {
            return false;
        }

        var examine = EventFixture("coverage-doll-room-examine");
        var examineAction = engine.GetLegalActions(examine).Single(action =>
            action.ReadPayload<EventChoicePayload>().ChoiceId == "examine");
        var examineOffer = engine.Step(examine, examineAction).State;
        if (examineOffer.Player.Hp != examine.Player.Hp - 15
            || examineOffer.World?.Event is not
                { NativePageIndex: 1, OfferedChoiceIds.Length: 3 }
            || engine.GetLegalActions(examineOffer).Count != 3)
        {
            return false;
        }

        var random = EventFixture("coverage-doll-room-random");
        var randomAction = engine.GetLegalActions(random).Single(action =>
            action.ReadPayload<EventChoicePayload>().ChoiceId == "random");
        var randomResult = engine.Step(random, randomAction).State;
        if (randomResult.Player.Hp != random.Player.Hp
            || randomResult.Player.Relics.Length
                != random.Player.Relics.Length + 1
            || !PrototypeNativeDollRoom.RelicChoiceIds.Select(
                    PrototypeNativeDollRoom.RelicIdForChoice)
                .Contains(randomResult.Player.Relics[^1].RelicId,
                    StringComparer.Ordinal))
        {
            return false;
        }

        var relicId = PrototypeNativeDollRoom.BingBongId;
        var card = new CardInstance(
            1, "proto.silent.strike", 1, PrototypeJson.EmptyObject(),
            new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.Sharp, 2));
        var player = new PlayerState(
            70, 70, 0, [card],
            [new RelicInstance(relicId, PrototypeJson.EmptyObject())], []);
        var fixture = EventFixture("coverage-doll-room-bing-bong");
        var nextId = fixture.World!.NextCardInstanceId;
        var copied = PrototypeGameEngine.ApplyRelicRunEvent(
            player,
            PrototypeRunEventKind.CardAdded,
            addedCard: card,
            rng: fixture.Rng,
            world: fixture.World,
            allocateCardId: () => nextId++);
        return copied.Deck.Length == 2
            && copied.Deck[1].InstanceId == fixture.World.NextCardInstanceId
            && copied.Deck[1].CardId == card.CardId
            && copied.Deck[1].UpgradeLevel == card.UpgradeLevel
            && copied.Deck[1].PersistentState.GetRawText()
                == card.PersistentState.GetRawText()
            && copied.Deck[1].Enchantment == card.Enchantment
            && nextId == fixture.World.NextCardInstanceId + 1;
    }

    private static bool HasCompleteCrystalSphereBranches(
        V111InventoryItem item)
    {
        var definition = PrototypeNativeCrystalSphere.Definition;
        return item.Branches is { Length: 8 } branches
            && branches.Order(StringComparer.Ordinal).SequenceEqual(
                PrototypeNativeCrystalSphere.CoveredBranches
                    .Order(StringComparer.Ordinal),
                StringComparer.Ordinal)
            && IsIntegratedEvent(PrototypeNativeCrystalSphere.EventId)
            && definition.MinAct == 2
            && definition.MaxAct == 3
            && definition.Choices.Select(choice => choice.Id).SequenceEqual(
                ["uncover_future", "payment_plan"],
                StringComparer.Ordinal)
            && PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                PrototypeNativeCrystalSphere.EventId)
            && PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                PrototypeNativeCrystalSphere.EventId) == 2
            && HasCrystalSpherePath();
    }

    private static bool HasCompleteWongosBranches(
        V111InventoryItem item)
    {
        var definition = PrototypeNativeWongos.Definition;
        return item.Branches is { Length: 19 } branches
            && branches.Order(StringComparer.Ordinal).SequenceEqual(
                PrototypeNativeWongos.CoveredBranches
                    .Order(StringComparer.Ordinal),
                StringComparer.Ordinal)
            && IsIntegratedEvent(PrototypeNativeWongos.EventId)
            && definition.MinAct == 1
            && definition.MaxAct == 1
            && definition.OncePerRun
            && definition.Choices.Select(choice => choice.Id).SequenceEqual(
                ["bargain_bin", "featured_item", "mystery_box", "leave"],
                StringComparer.Ordinal)
            && PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                PrototypeNativeWongos.EventId)
            && PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                PrototypeNativeWongos.EventId) == 2
            && HasWongosPath();
    }

    private static bool HasWongosPath()
    {
        static RunState EventFixture(
            string seed,
            int gold,
            int wongoPoints,
            bool addUpgradedCard = false,
            ActIdentity firstAct = ActIdentity.Overgrowth)
        {
            var profile = SimulationProfile.Unlocked() with
            {
                WongoPoints = wongoPoints
            };
            var configuration = RunConfiguration.Create(
                firstAct: firstAct,
                profile: profile) with
            {
                FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
            };
            var state = V111RunFactory.Create(seed, configuration);
            var engine = new PrototypeGameEngine();
            state = engine.Step(state,
                engine.GetLegalActions(state).Single()).State;
            var world = state.World!;
            var node = world.Map.Nodes.First(item =>
                item.RoomType == PrototypeRoomType.Unknown
                && item.NextNodeIds is { Length: > 0 });
            var player = state.Player with { Gold = gold };
            if (addUpgradedCard)
            {
                var selected = player.Deck.First(card =>
                    PrototypeContent.Card(card.CardId).MaxUpgradeLevel > 0);
                player = player with
                {
                    Deck = player.Deck.Select(card => card with
                    {
                        UpgradeLevel = card.InstanceId == selected.InstanceId
                            ? 1 : 0
                    }).ToArray()
                };
            }

            var eventState = new EventState(
                PrototypeNativeWongos.EventId);
            var initialized = PrototypeNativeWongos.Initialize(
                world,
                player,
                eventState,
                state.Rng);
            return state with
            {
                Player = player,
                Phase = RunPhase.Event,
                World = initialized.World with
                {
                    Floor = node.Floor,
                    ActiveRoom = PrototypeRoomType.Event,
                    Map = initialized.World.Map with
                    {
                        CurrentNodeId = node.NodeId
                    },
                    Event = initialized.Event,
                    EventHistory = world.EventIds.Append(
                        PrototypeNativeWongos.EventId).ToArray()
                }
            };
        }

        static string[] ChoiceIds(
            PrototypeGameEngine engine, RunState state) =>
            engine.GetLegalActions(state)
                .Where(action => action.Kind == "event_choice")
                .Select(action => action.ReadPayload<EventChoicePayload>()
                    .ChoiceId)
                .Order(StringComparer.Ordinal)
                .ToArray();

        static GameAction Choice(
            PrototypeGameEngine engine,
            RunState state,
            string choiceId) => engine.GetLegalActions(state).Single(action =>
                action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId
                    == choiceId);

        static bool TerminalChoicePage(
            PrototypeGameEngine engine,
            RunState state,
            string choiceId,
            string pageId) =>
            state.Phase == RunPhase.Event
            && state.World?.Event is
                {
                    NativePageIndex: 2,
                    NativeWongoResultPage: var resultPage,
                    ChosenChoiceId: var chosenChoice
                }
            && resultPage == pageId
            && chosenChoice == choiceId
            && engine.GetLegalActions(state).Single().Kind
                == "continue_event";

        var engine = new PrototypeGameEngine();
        if (!PrototypeNativeOvergrowthEvents.RegionEventIds.Contains(
                PrototypeNativeWongos.EventId, StringComparer.Ordinal)
            || !PrototypeNativeUnderdocksEvents.IsSupported(
                PrototypeNativeWongos.EventId)
            || PrototypeNativeOvergrowthEvents.IsEligible(
                PrototypeNativeWongos.Definition,
                new PlayerState(30, 30, 99, [], [], []))
            || PrototypeNativeUnderdocksEvents.IsEligible(
                PrototypeNativeWongos.Definition,
                new PlayerState(30, 30, 99, [], [], [])))
        {
            return false;
        }
        var bargain = EventFixture(
            "coverage-wongos-bargain", 100, 0);
        var featured = bargain.World!.Event!.NativeWongoFeaturedRelicId;
        if (featured is null
            || !PrototypeNativeRelicGrabBag.TryGetRarity(
                featured, out var featuredRarity)
            || featuredRarity != PrototypeRelicRarity.Rare
            || !ChoiceIds(engine, bargain).SequenceEqual(
                ["bargain_bin", "leave"], StringComparer.Ordinal))
        {
            return false;
        }
        var bargainBought = engine.Step(bargain,
            Choice(engine, bargain, "bargain_bin")).State;
        if (bargainBought.Player.Gold != 0
            || bargainBought.Configuration?.Profile.WongoPoints != 32
            || bargainBought.Player.Relics.Length
                != bargain.Player.Relics.Length + 1
            || !PrototypeNativeRelicGrabBag.TryGetRarity(
                bargainBought.Player.Relics[^1].RelicId,
                out var bargainRarity)
            || bargainRarity != PrototypeRelicRarity.Common
            || TerminalChoicePage(
                engine, bargainBought, "bargain_bin", "after_buy") == false)
        {
            return false;
        }
        var locked = EventFixture("coverage-wongos-locked", 150, 0);
        if (!ChoiceIds(engine, locked).SequenceEqual(
                ["bargain_bin", "leave"], StringComparer.Ordinal))
            return false;
        var unaffordable = EventFixture(
            "coverage-wongos-unaffordable", 99, 0);
        if (!ChoiceIds(engine, unaffordable).SequenceEqual(
                ["leave"], StringComparer.Ordinal))
            return false;

        var counter = EventFixture(
            "coverage-wongos-counter", 200, 2000);
        var counterFeatured = counter.World!.Event!
            .NativeWongoFeaturedRelicId;
        if (counterFeatured is null
            || !ChoiceIds(engine, counter).Contains(
                "featured_item", StringComparer.Ordinal))
            return false;
        var counterBought = engine.Step(counter,
            Choice(engine, counter, "featured_item")).State;
        if (counterBought.Player.Gold != 0
            || counterBought.Configuration?.Profile.WongoPoints != 2016
            || counterBought.Player.Relics[^1].RelicId != counterFeatured
            || TerminalChoicePage(engine, counterBought,
                "featured_item", "badge_counter") == false
            || counterBought.Player.Relics.Any(relic =>
                relic.RelicId == PrototypeNativeWongos.AppreciationBadgeId))
            return false;
        var mystery = EventFixture(
            "coverage-wongos-mystery", 300, 1994,
            firstAct: ActIdentity.Underdocks);
        var mysteryBought = engine.Step(mystery,
            Choice(engine, mystery, "mystery_box")).State;
        if (mysteryBought.Player.Gold != 0
            || mysteryBought.Configuration?.Profile.WongoPoints != 2002
            || !mysteryBought.Player.Relics.Any(relic =>
                relic.RelicId == PrototypeNativeWongos.MysteryTicketId
                && relic.PersistentState.TryGetProperty(
                    "CombatsFinished", out var fights)
                && fights.GetInt32() == 0)
            || !mysteryBought.Player.Relics.Any(relic =>
                relic.RelicId
                    == PrototypeNativeWongos.AppreciationBadgeId)
            || TerminalChoicePage(engine, mysteryBought,
                "mystery_box", "receive_badge") == false)
        {
            return false;
        }
        var ticketPlayer = mysteryBought.Player;
        var ticketWorld = mysteryBought.World! with
        {
            ActiveRoom = PrototypeRoomType.Combat
        };
        var ticketRng = PrototypeRng.CreateBundle(
            "coverage-wongos-ticket-combats");
        var fifthFightRewards = new List<string>();
        for (var fight = 1; fight <= PrototypeNativeWongos.TicketCombats;
             fight++)
        {
            var rewards = new List<string>();
            ticketPlayer = PrototypeGameEngine.UpdateWongosTicketCombatEnd(
                ticketPlayer,
                ref ticketWorld,
                ticketRng,
                rewards,
                out var awarded);
            if (fight < PrototypeNativeWongos.TicketCombats
                ? awarded || rewards.Count != 0
                : !awarded || rewards.Count
                    != PrototypeNativeWongos.TicketRelicRewards)
            {
                return false;
            }
            if (fight == PrototypeNativeWongos.TicketCombats)
                fifthFightRewards = rewards;
        }
        ticketPlayer = PrototypeGameEngine.UpdateWongosTicketCombatEnd(
            ticketPlayer,
            ref ticketWorld,
            ticketRng,
            new List<string>(),
            out var repeatedReward);
        var finalTicket = ticketPlayer.Relics.Single(relic =>
            relic.RelicId == PrototypeNativeWongos.MysteryTicketId);
        var leave = EventFixture(
            "coverage-wongos-leave", 150, 0, addUpgradedCard: true);
        var upgraded = leave.Player.Deck.Single(card =>
            card.UpgradeLevel == 1);
        var left = engine.Step(leave,
            Choice(engine, leave, "leave")).State;
        return !repeatedReward
            && fifthFightRewards.Count
                == PrototypeNativeWongos.TicketRelicRewards
            && fifthFightRewards.Distinct(StringComparer.Ordinal).Count()
                == PrototypeNativeWongos.TicketRelicRewards
            && finalTicket.PersistentState.TryGetProperty(
                "CombatsFinished", out var totalFights)
            && totalFights.GetInt32() == PrototypeNativeWongos.TicketCombats + 1
            && finalTicket.PersistentState.TryGetProperty(
                "GaveRelic", out var gaveRelic)
            && gaveRelic.GetBoolean()
            && left.Player.Gold == leave.Player.Gold
            && left.Player.Deck.Single(card =>
                card.InstanceId == upgraded.InstanceId).UpgradeLevel == 0
            && left.Configuration?.Profile.WongoPoints == 0
            && TerminalChoicePage(engine, left, "leave", "leave");
    }

    private static bool HasCrystalSpherePath()
    {
        static RunState EventFixture(string seed, int act)
        {
            var state = PrototypeNativeLaterActFixture.Create(seed, act);
            var world = state.World!;
            var node = world.Map.Nodes.First(item =>
                item.RoomType == PrototypeRoomType.Unknown
                && item.NextNodeIds is { Length: > 0 });
            var eventId = PrototypeNativeCrystalSphere.EventId;
            return state with
            {
                Player = state.Player with { Gold = 300 },
                Phase = RunPhase.Event,
                World = world with
                {
                    Floor = node.Floor,
                    ActiveRoom = PrototypeRoomType.Event,
                    Map = world.Map with { CurrentNodeId = node.NodeId },
                    Event = new EventState(eventId, NativeEventGold: 80),
                    EventHistory = world.EventIds.Append(eventId).ToArray()
                }
            };
        }

        static GameAction FindAction(
            PrototypeGameEngine engine,
            RunState state,
            string kind) => engine.GetLegalActions(state)
                .Single(action => action.Kind == kind);

        static GameAction FindCellAction(
            PrototypeGameEngine engine,
            RunState state,
            int x,
            int y) => engine.GetLegalActions(state).Single(action =>
                action.Kind == "crystal_sphere_reveal_cell"
                && action.ReadPayload<ChooseCrystalSphereCellPayload>()
                    is { X: var cellX, Y: var cellY }
                && cellX == x && cellY == y);

        var engine = new PrototypeGameEngine();
        var uncover = EventFixture("coverage-crystal-sphere-uncover", 2);
        if (!IsIntegratedEvent(PrototypeNativeCrystalSphere.EventId)
            || !ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeCrystalSphere.EventId, 2)
            || !ExperimentalSilentThreeActRouteV111.ContainsEvent(
                PrototypeNativeCrystalSphere.EventId, 3)
            || engine.GetLegalActions(uncover).Count != 2)
        {
            return false;
        }

        var uncoverAction = engine.GetLegalActions(uncover).Single(action =>
            action.ReadPayload<EventChoicePayload>().ChoiceId
                == PrototypeNativeCrystalSphere.UncoverFutureChoiceId);
        var uncovering = engine.Step(uncover, uncoverAction).State;
        var uncoverBoard = uncovering.World?.Event?.NativeCrystalSphere;
        if (uncovering.Player.Gold != 220
            || uncoverBoard is not
                { DivinationsRemaining: 3, SmallToolSelected: false }
            || uncoverBoard.Items.Length != 15)
        {
            return false;
        }

        var setSmall = engine.GetLegalActions(uncovering).Single(action =>
            action.Kind == "crystal_sphere_set_tool"
            && action.ReadPayload<SetCrystalSphereToolPayload>()
                .SmallToolSelected);
        uncovering = engine.Step(uncovering, setSmall).State;
        var uncoverGold = uncovering.World!.Event!.NativeCrystalSphere!
            .Items.First(item => item.Kind == "gold_small");
        for (var pick = 0; pick < 3; pick++)
        {
            uncovering = engine.Step(uncovering,
                FindCellAction(engine, uncovering,
                    uncoverGold.X, uncoverGold.Y)).State;
        }
        if (uncovering.Phase != RunPhase.Reward
            || uncovering.World?.Reward is not
                { IndependentSelection: true, GoldOption: 10 }
            || uncovering.World.Event is not
                { NativePageIndex: 0, ChosenChoiceId: "uncover_future" })
        {
            return false;
        }

        uncovering = engine.Step(uncovering,
            FindAction(engine, uncovering, "take_reward_gold")).State;
        uncovering = engine.Step(uncovering,
            FindAction(engine, uncovering, "leave_reward")).State;
        if (uncovering.Phase != RunPhase.Event
            || uncovering.Player.Gold != 230
            || uncovering.World?.Event is not
                { NativePageIndex: 2, ChosenChoiceId: "uncover_future" }
            || engine.GetLegalActions(uncovering).Single().Kind
                != "continue_event")
        {
            return false;
        }

        var payment = EventFixture("coverage-crystal-sphere-payment", 3);
        var paymentChoice = engine.GetLegalActions(payment).Single(action =>
            action.ReadPayload<EventChoicePayload>().ChoiceId
                == PrototypeNativeCrystalSphere.PaymentPlanChoiceId);
        var paying = engine.Step(payment, paymentChoice).State;
        var paymentBoard = paying.World?.Event?.NativeCrystalSphere;
        if (paymentBoard is not { DivinationsRemaining: 6 }
            || !paying.Player.Deck.Any(card =>
                card.CardId == NativeGloryAncientsV111.DebtCardId))
        {
            return false;
        }

        var smallTool = engine.GetLegalActions(paying).Single(action =>
            action.Kind == "crystal_sphere_set_tool"
            && action.ReadPayload<SetCrystalSphereToolPayload>()
                .SmallToolSelected);
        paying = engine.Step(paying, smallTool).State;
        paymentBoard = paying.World!.Event!.NativeCrystalSphere!;
        var curse = paymentBoard.Items.Single(item => item.Kind == "curse");
        for (var x = curse.X; x < curse.X + curse.Width; x++)
        for (var y = curse.Y; y < curse.Y + curse.Height; y++)
        {
            paying = engine.Step(paying,
                FindCellAction(engine, paying, x, y)).State;
        }
        if (!paying.Player.Deck.Any(card =>
                card.CardId == PrototypeNativeGloryEvents.DoubtId))
        {
            return false;
        }

        var paymentGold = paymentBoard.Items.First(item =>
            item.Kind == "gold_small");
        for (var pick = 0; pick < 2; pick++)
        {
            paying = engine.Step(paying,
                FindCellAction(engine, paying,
                    paymentGold.X, paymentGold.Y)).State;
        }
        if (paying.Phase != RunPhase.Reward
            || paying.World?.Reward is not
                { IndependentSelection: true, GoldOption: 10 }
            || paying.World.Event is not
                { NativePageIndex: 0, ChosenChoiceId: "payment_plan" })
        {
            return false;
        }

        paying = engine.Step(paying,
            FindAction(engine, paying, "leave_reward")).State;
        return paying.Phase == RunPhase.Event
            && paying.World?.Event is
                { NativePageIndex: 2, ChosenChoiceId: "payment_plan" }
            && engine.GetLegalActions(paying).Single().Kind
                == "continue_event";
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
