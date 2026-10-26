using System.Globalization;
using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed dependencies for Glory's Ancient events.
/// </summary>
public static class NativeGloryAncientsV111
{
    public const string BeautifulBraceletShuffleSiteId =
        "run.relic.beautiful_bracelet.deck_shuffle.swap_index";
    public const string BeautifulBraceletShuffleOwnerId =
        "relic:beautiful_bracelet:acquisition";
    public const string FurCoatShuffleSiteId =
        "run.relic.fur_coat.map_node_shuffle.swap_index";
    public const string FurCoatShuffleOwnerPrefix = "relic:fur_coat:act:";
    public const string BlessedAntlerId = "proto.native.ancient.blessed_antler";
    public const string BeautifulBraceletId = "proto.native.ancient.beautiful_bracelet";
    public const string FurCoatId = "proto.native.ancient.fur_coat";
    public const string DistinguishedCapeCurseSiteId =
        "run.relic.distinguished_cape.modifier_curse.index_draw";
    public const string FiddleId = "proto.native.ancient.fiddle";
    public const string WhisperingEarringId = "proto.native.ancient.whispering_earring";
    public const int WhisperingEarringMaxCards = 13;
    public const string SereTalonId = "proto.native.ancient.sere_talon";
    public const string WishCardId = "proto.native.ancient.wish";
    public const string PreservedFogId = "proto.native.ancient.preserved_fog";
    public const string FollyCardId = "proto.native.ancient.folly";
    public const string MusicBoxId = "proto.native.ancient.music_box";
    public const string JeweledMaskId = "proto.native.ancient.jeweled_mask";
    public const string ChoicesParadoxId = "proto.native.ancient.choices_paradox";
    public const string LordsParasolId = "proto.native.ancient.lords_parasol";
    public const string DistinguishedCapeId = "proto.native.ancient.distinguished_cape";
    public const string BloodSoakedRoseId = "proto.native.ancient.blood_soaked_rose";
    public const string EnthralledCardId = "proto.native.ancient.enthralled";
    public const string DiamondDiademId = "proto.native.ancient.diamond_diadem";
    public const string CrossbowId = "proto.native.ancient.crossbow";
    public const string IronClubId = "proto.native.ancient.iron_club";
    public const string MeatCleaverId = "proto.native.ancient.meat_cleaver";
    public const string ThrowingAxeId = "proto.native.ancient.throwing_axe";
    public const string TanxsWhistleId = "proto.native.ancient.tanxs_whistle";
    public const string WhistleCardId = "proto.native.ancient.whistle";
    public const string LoomingFruitId = "proto.native.ancient.looming_fruit";
    public const string SaiId = "proto.native.ancient.sai";
    public const string SpikedGauntletsId = "proto.native.ancient.spiked_gauntlets";
    public const string SignetRingId = "proto.native.ancient.signet_ring";
    public const string JewelryBoxId = "proto.native.ancient.jewelry_box";
    public const string BrilliantScarfId = "proto.native.ancient.brilliant_scarf";
    public const string DelicateFrondId = "proto.native.ancient.delicate_frond";
    public const string GlitterId = "proto.native.ancient.glitter";
    public const string ApotheosisCardId = "proto.native.ancient.apotheosis";
    public const string TriBoomerangId = "proto.native.ancient.tri_boomerang";
    public const string ClawsId = "proto.native.ancient.claws";
    public const string WarHammerId = "proto.native.ancient.war_hammer";
    public const string MaulCardId = "proto.native.ancient.maul";
    public const string ApparitionCardId = "proto.native.ancient.apparition";
    public const string ClumsyCardId = "proto.native.shared.clumsy";
    public const string DebtCardId = "proto.native.shared.debt";
    public const string WritheCardId = "proto.native.shared.writhe";

    // CurseCardPool entries with CanBeGeneratedByModifiers, ordered by the
    // source ModelId order used by Distinguished Cape's sorted pool.
    private static readonly string[] ModifierGeneratedCurseIdValues =
    [
        ClumsyCardId,
        DebtCardId,
        PrototypeNativeHiveEvents.DecayId,
        PrototypeNativeGloryEvents.DoubtId,
        "proto.native.event.guilty",
        "proto.native.neow.injury",
        PrototypeNativeHiveEvents.NormalityId,
        PrototypeNativeGloryEvents.RegretId,
        PrototypeNativeGloryEvents.ShameId,
        WritheCardId
    ];

    public static string[] ModifierGeneratedCurseIds =>
        (string[])ModifierGeneratedCurseIdValues.Clone();

    private static readonly PrototypeRelicDefinition[] RelicDefinitions =
    [
        new(PreservedFogId, "Preserved Fog",
            AcquisitionDeckChoice: new(
                PrototypePersistentDeckChoiceKind.Remove, 3),
            CardIdAfterAcquisitionDeckChoices: FollyCardId),
        new(MusicBoxId, "Music Box", Triggers:
        [
            new(PrototypeCombatEventKind.CardPlayed,
                [new(PrototypeCombatEffectKind.CreateEventSourceCardCopyInHand,
                    1,
                    EventSourceCardKeyword: new(
                        PrototypeCardKeyword.Ethereal,
                        Expiry: PrototypeCardKeywordOverrideExpiry.EndOfTurn))],
                RequiredSourceCardType: PrototypeCardType.Attack,
                ResetCounterEachTurn: true,
                MaxTriggersPerCounterWindow: 1)
        ]),
        new(JeweledMaskId, "Jeweled Mask", Triggers:
        [
            new(PrototypeCombatEventKind.BeforeHandDraw,
                [new(PrototypeCombatEffectKind.MoveRandomPowerDrawCardToHandFree,
                    1)],
                TurnEquals: 1)
        ]),
        new(ChoicesParadoxId, "Choices Paradox", Triggers:
        [
            new(PrototypeCombatEventKind.OpeningPlayerTurnStarted,
                [new(PrototypeCombatEffectKind.ChooseGeneratedCards,
                    5,
                    GeneratedChoiceMustPick: true,
                    GeneratedChoiceCharacterCards: true,
                    GeneratedChoiceCardsRetain: true,
                    GeneratedChoiceUsesNativeCardGenerationRng: true)],
                TurnEquals: 1)
        ]),
        new(LordsParasolId, "Lord's Parasol"),
        new(DistinguishedCapeId, "Distinguished Cape"),
        new(BlessedAntlerId, "Blessed Antler",
            EnergyPerTurnBonus: 1,
            Triggers:
            [
                new(PrototypeCombatEventKind.BeforeHandDraw,
                    [new(PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
                        3, CardId: "proto.status.dazed")],
                    TurnEquals: 1)
            ]),
        new(BeautifulBraceletId, "Beautiful Bracelet"),
        new(FurCoatId, "Fur Coat"),
        new(FiddleId, "Fiddle", HandDrawBonus: 2),
        new(WhisperingEarringId, "Whispering Earring",
            EnergyPerTurnBonus: 1),
        new(BloodSoakedRoseId, "Blood-Soaked Rose",
            EnergyPerTurnBonus: 1,
            RunTriggers:
            [
                new(PrototypeRunEventKind.RelicAcquired,
                    [new(PrototypeRunEffectKind.AddCard,
                        CardId: EnthralledCardId)])
            ]),
        new(SereTalonId, "Sere Talon",
            RunTriggers:
            [
                new(PrototypeRunEventKind.RelicAcquired,
                [
                    new(PrototypeRunEffectKind.LoseMaxHp, 9),
                    new(PrototypeRunEffectKind.AddCard, CardId: WishCardId),
                    new(PrototypeRunEffectKind.AddCard, CardId: WishCardId),
                    new(PrototypeRunEffectKind.AddCard, CardId: WishCardId)
                ])
            ]),
        new(CrossbowId, "Crossbow", Triggers:
        [
            new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                [new(PrototypeCombatEffectKind.CreateRandomCharacterAttackCardsInHand,
                    1, GeneratedCharacterCardsFreeThisTurn: true)])
        ]),
        new(IronClubId, "Iron Club", Triggers:
        [
            new(PrototypeCombatEventKind.CardPlayed,
                [new(PrototypeCombatEffectKind.DrawCards, 1)],
                EveryNth: 4)
        ], PersistentTriggerCountProperty: "CardsPlayed"),
        new(ThrowingAxeId, "Throwing Axe",
            AdditionalCardPlayCountOncePerCombat: 1),
        new(MeatCleaverId, "Meat Cleaver"),
        new(TanxsWhistleId, "Tanx's Whistle", RunTriggers:
        [
            new(PrototypeRunEventKind.RelicAcquired,
                [new(PrototypeRunEffectKind.AddCard,
                    CardId: WhistleCardId)])
        ]),
        new(TriBoomerangId, "Tri Boomerang",
            AcquisitionDeckChoice: new(
                PrototypePersistentDeckChoiceKind.Enchant, 3,
                EnchantmentKind: PrototypeCardEnchantmentKind.Instinct,
                RequiredCardType: PrototypeCardType.Attack)),
        new(ClawsId, "Claws",
            AcquisitionDeckChoice: new(
                PrototypePersistentDeckChoiceKind.Transform, 6,
                TransformToCardId: MaulCardId,
                PreserveUpgradeOnTransformation: true,
                PreserveEnchantmentOnTransformation: true,
                MinimumSelections: 0)),
        new(WarHammerId, "War Hammer"),
        new(DiamondDiademId, "Diamond Diadem", Triggers:
        [
            new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                [
                    new(PrototypeCombatEffectKind.GainPlayerBlock, 20),
                    new(PrototypeCombatEffectKind.ApplyPlayerPower, 1,
                        PowerId: "proto.power.blur")
                ], TurnEquals: 1)
        ]),
        new(LoomingFruitId, "Looming Fruit", RunTriggers:
        [
            new(PrototypeRunEventKind.RelicAcquired,
                [new(PrototypeRunEffectKind.GainMaxHp, 31)])
        ]),
        new(SaiId, "Sai", Triggers:
        [
            new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                [new(PrototypeCombatEffectKind.GainPlayerBlock, 7)])
        ]),
        new(SpikedGauntletsId, "Spiked Gauntlets",
            EnergyPerTurnBonus: 1,
            PowerCardEnergyCostIncrease: 1),
        new(SignetRingId, "Signet Ring", RunTriggers:
        [
            new(PrototypeRunEventKind.RelicAcquired,
                [new(PrototypeRunEffectKind.GainGold, 888)])
        ]),
        new(JewelryBoxId, "Jewelry Box", RunTriggers:
        [
            new(PrototypeRunEventKind.RelicAcquired,
                [new(PrototypeRunEffectKind.AddCard,
                    CardId: ApotheosisCardId)])
        ]),
        new(BrilliantScarfId, "Brilliant Scarf",
            FreeCardAtManualCardPlayCount: 4),
        new(DelicateFrondId, "Delicate Frond",
            ProcureRandomPotionsBeforeCombat: true),
        new(GlitterId, "Glitter")
    ];

    public static bool IsBeautifulBraceletEligible(PlayerState player) =>
        player.Deck.Count(card => PrototypeGameEngine.CanSelectEventDeckCard(
            card,
            PrototypePersistentDeckChoiceKind.Enchant,
            transformToCardId: null,
            enchantmentKind: PrototypeCardEnchantmentKind.Swift,
            basicCardsOnly: false)) >= 4;

    internal static PlayerState ApplyBeautifulBraceletAcquisition(
        PlayerState player,
        RngBundle rng,
        RngBundle? nativeRunRng)
    {
        var eligible = player.Deck
            .Where(card => PrototypeGameEngine.CanSelectEventDeckCard(
                card,
                PrototypePersistentDeckChoiceKind.Enchant,
                transformToCardId: null,
                enchantmentKind: PrototypeCardEnchantmentKind.Swift,
                basicCardsOnly: false))
            .ToArray();
        if (eligible.Length == 0)
            return player;

        var shuffled = (CardInstance[])eligible.Clone();
        if (nativeRunRng is null)
            _ = LegacyFisherYatesV111.ShuffleInPlace(
                shuffled,
                rng,
                "event",
                BeautifulBraceletShuffleSiteId,
                BeautifulBraceletShuffleOwnerId,
                controlledCandidateLabel: card =>
                    "card-instance:" + card.InstanceId.ToString(
                        CultureInfo.InvariantCulture));
        else
        {
            var ordinal = nativeRunRng.GetStream("run.niche").CallCount ?? 0;
            if (ordinal < 0 || ordinal > int.MaxValue)
                throw new NotSupportedException(
                    "Beautiful Bracelet run.niche ordinal exceeds the request range.");
            _ = NativeFisherYatesV111.ShuffleInPlace(
                shuffled, nativeRunRng,
                new NativeRandomIndexProviderV111(), "run.niche",
                BeautifulBraceletShuffleOwnerId,
                BeautifulBraceletShuffleSiteId,
                ordinalOffset: (int)ordinal,
                controlledCandidateLabel: card =>
                    "card-instance:" + card.InstanceId.ToString(
                        CultureInfo.InvariantCulture));
        }

        var selectedIds = shuffled.Take(Math.Min(4, shuffled.Length))
            .Select(card => card.InstanceId)
            .ToHashSet();
        return player with
        {
            Deck = player.Deck.Select(card => selectedIds.Contains(
                    card.InstanceId)
                ? card with
                {
                    Enchantment = new PrototypeCardEnchantment(
                        PrototypeCardEnchantmentKind.Swift, 2)
                }
                : card).ToArray()
        };
    }

    internal static PlayerState MarkFurCoatCombats(
        PlayerState player,
        MapState map,
        int act,
        string runSeed,
        RngBundle prototypeRng,
        bool nativeRng)
    {
        var candidates = map.Nodes
            .Where(node => node.Act == act
                && node.RoomType is PrototypeRoomType.Combat
                    or PrototypeRoomType.Elite)
            .ToArray();
        NativeRngSnapshot? snapshot = null;
        var controlledEventReplay =
            ControlledEventChoiceReplayV111.IsActive
            && ControlledEventChoiceReplayV111.IsEventChoiceSite(
                FurCoatShuffleSiteId);
        if (nativeRng)
        {
            var stream = NativeRngV111Contexts.ForPlayerContent(
                runSeed, "FurCoat");
            var bundle = new RngBundle([stream]);
            _ = NativeFisherYatesV111.ShuffleInPlace(
                candidates, bundle, new NativeRandomIndexProviderV111(),
                stream.StreamId, FurCoatShuffleOwnerPrefix + act,
                FurCoatShuffleSiteId,
                controlledCandidateLabel: node => node.NodeId);
            if (!controlledEventReplay)
                snapshot = NativeRngV111.ToSnapshot(
                    bundle.GetStream(stream.StreamId));
        }
        else
        {
            _ = LegacyFisherYatesV111.ShuffleInPlace(
                candidates,
                prototypeRng,
                "event",
                FurCoatShuffleSiteId,
                FurCoatShuffleOwnerPrefix + act,
                controlledCandidateLabel: node => node.NodeId);
        }

        var selected = candidates.Take(Math.Min(8, candidates.Length))
            .Select(node => node.NodeId).ToArray();
        var relics = (RelicInstance[])player.Relics.Clone();
        var index = Array.FindLastIndex(relics,
            relic => relic.RelicId == FurCoatId);
        if (index < 0)
            throw new InvalidOperationException(
                "Fur Coat state requires an acquired Fur Coat relic.");
        relics[index] = relics[index] with
        {
            PersistentState = JsonSerializer.SerializeToElement(new
            {
                Act = act,
                MarkedNodeIds = selected,
                Rng = snapshot
            })
        };
        return player with { Relics = relics };
    }

    internal static bool IsFurCoatCombat(PlayerState player, int act,
        string? nodeId)
    {
        if (nodeId is null)
            return false;
        foreach (var relic in player.ActiveRelics.Where(relic =>
                     relic.RelicId == FurCoatId))
        {
            if (relic.PersistentState.ValueKind != JsonValueKind.Object
                || !relic.PersistentState.TryGetProperty("Act", out var actValue)
                || !actValue.TryGetInt32(out var relicAct)
                || !relic.PersistentState.TryGetProperty("MarkedNodeIds", out var ids)
                || ids.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException(
                    "Fur Coat requires persisted act and map-node state.");
            if (relicAct == act && ids.EnumerateArray().Any(value =>
                    value.ValueKind == JsonValueKind.String
                    && StringComparer.Ordinal.Equals(value.GetString(), nodeId)))
                return true;
        }
        return false;
    }

    internal static CombatState ApplyFurCoatCombatStart(
        CombatState combat, PlayerState player)
    {
        if (!IsFurCoatCombat(player, combat.Act,
                combat.CurrentMapNodeId))
            return combat;
        return combat with
        {
            Enemies = combat.Enemies.Select(enemy => enemy.Hp > 0
                ? enemy with { Hp = 1 }
                : enemy).ToArray()
        };
    }

    public static PrototypeRelicDefinition[] Relics =>
        RelicDefinitions
            .Select(PrototypeDefinitionCopiesV111.CloneRelicDefinition)
            .ToArray();

    private static readonly PrototypeCardDefinition[] CardDefinitions =
    [
        new(ApparitionCardId, "Apparition", 1,
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.ApplyPlayerPower, 1,
                PowerId: "proto.power.intangible")],
            ExhaustOnUse: true,
            Rarity: PrototypeCardRarity.Ancient,
            Ethereal: true,
            RewardEligible: false,
            Type: PrototypeCardType.Skill,
            LoseEtherealOnUpgrade: true),
        new(ClumsyCardId, "Clumsy", -1,
            PrototypeCardTarget.None, [],
            Ethereal: true,
            Unplayable: true,
            RewardEligible: false,
            Rarity: PrototypeCardRarity.Curse,
            Type: PrototypeCardType.Curse,
            MaxUpgradeLevel: 0),
        new(DebtCardId, "Debt", -1,
            PrototypeCardTarget.None, [],
            Unplayable: true,
            RewardEligible: false,
            Rarity: PrototypeCardRarity.Curse,
            Type: PrototypeCardType.Curse,
            EndTurnGoldLossIfInHand: 10,
            MaxUpgradeLevel: 0),
        new(WritheCardId, "Writhe", -1,
            PrototypeCardTarget.None, [],
            Innate: true,
            Unplayable: true,
            RewardEligible: false,
            Rarity: PrototypeCardRarity.Curse,
            Type: PrototypeCardType.Curse,
            MaxUpgradeLevel: 0),
        new(ApotheosisCardId, "Apotheosis", new(
            PrototypeCardCostKind.Fixed, 2, UpgradeDelta: -1),
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.UpgradeAllCards, 0)],
            ExhaustOnUse: true,
            Rarity: PrototypeCardRarity.Ancient,
            Innate: true,
            RewardEligible: false,
            Type: PrototypeCardType.Skill,
            CanBeGeneratedInCombat: false),
        new(MaulCardId, "Maul", new(
            PrototypeCardCostKind.Fixed, 1),
            PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 5,
                    UpgradeDelta: 1, Repetitions: 2),
                new(PrototypeCombatEffectKind.BuffMaulDamage, 2,
                    UpgradeDelta: 1)
            ],
            Rarity: PrototypeCardRarity.Ancient,
            RewardEligible: false,
            Type: PrototypeCardType.Attack,
            CanBeGeneratedInCombat: false),
        new(WhistleCardId, "Whistle", new(
            PrototypeCardCostKind.Fixed, 2),
            PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 33,
                    UpgradeDelta: 11),
                new(PrototypeCombatEffectKind.StunEnemy, 1)
            ],
            ExhaustOnUse: true,
            Rarity: PrototypeCardRarity.Ancient,
            RewardEligible: false,
            Type: PrototypeCardType.Attack,
            CanBeGeneratedInCombat: false)
    ];

    public static PrototypeCardDefinition[] Cards =>
        CardDefinitions
            .Select(PrototypeDefinitionCopiesV111.CloneCardDefinition)
            .ToArray();
}
