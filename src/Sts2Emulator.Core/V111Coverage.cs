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
