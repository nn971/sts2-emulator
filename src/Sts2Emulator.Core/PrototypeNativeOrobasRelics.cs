using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Implemented dependencies for Orobas's Silent Ancient offers. Foreign-card
/// Sea Glass and Prismatic Gem mechanics are deferred separately.
/// </summary>
public static class PrototypeNativeOrobasRelics
{
    public const string SandCastleId = "proto.native.ancient.sand_castle";
    public const string SandCastleAcquisitionShuffleSiteId =
        "relic.sand_castle.acquisition_upgrade.shuffle_swap_index";
    internal const string SandCastleAcquisitionShuffleOwnerId =
        "player:0:sand_castle:eligible_cards";
    public const string TouchOfOrobasId = "proto.native.ancient.touch_of_orobas";
    public const string ArchaicToothId = "proto.native.ancient.archaic_tooth";
    public const string RadiantPearlId = "proto.native.ancient.radiant_pearl";
    public const string LuminesceId = "proto.native.ancient.luminesce";
    public const string AlchemicalCofferId =
        "proto.native.ancient.alchemical_coffer";
    public const string ElectricShrympId =
        "proto.native.ancient.electric_shrymp";
    public const string DriftwoodId = "proto.native.ancient.driftwood";
    public const string GlassEyeId = "proto.native.ancient.glass_eye";
    public const string PrismaticGemId =
        "proto.native.ancient.prismatic_gem";
    public const string RingOfTheSnakeId = "proto.relic.silent_ring";
    public const string RingOfTheDrakeId = "proto.native.ancient.ring_of_the_drake";
    public const string NeutralizeId = "proto.silent.neutralize";
    public const string SuppressId = "proto.silent.suppress";

    private static readonly PrototypeRelicDefinition[] RelicDefinitions =
    [
        new(AlchemicalCofferId, "Alchemical Coffer", ExtraPotionSlots: 4),
        new(DriftwoodId, "Driftwood"),
        new(GlassEyeId, "Glass Eye"),
        new(ElectricShrympId, "Electric Shrymp",
            AcquisitionDeckChoice: new(
                PrototypePersistentDeckChoiceKind.Enchant, 1,
                EnchantmentKind: PrototypeCardEnchantmentKind.Imbued,
                RequiredCardType: PrototypeCardType.Skill)),
        new(RadiantPearlId, "Radiant Pearl",
            Triggers:
            [
                new(PrototypeCombatEventKind.CombatStarted,
                [
                    new(PrototypeCombatEffectKind.CreateCardsInHand,
                        1, CardId: LuminesceId)
                ])
            ]),
        new(SandCastleId, "Sand Castle"),
        new(TouchOfOrobasId, "Touch of Orobas"),
        new(ArchaicToothId, "Archaic Tooth"),
        // The persistent energy bonus is source-backed. Its foreign
        // character card-pool expansion remains unimplemented, so Orobas
        // stays gated until the full Prismatic Gem dependency is complete.
        new(PrismaticGemId, "Prismatic Gem", EnergyPerTurnBonus: 1),
        new(RingOfTheDrakeId, "Ring of the Drake", HandDrawBonus: 2,
            HandDrawBonusThroughTurn: 3)
    ];

    public static PrototypeRelicDefinition[] Relics => RelicDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneRelicDefinition)
        .ToArray();

    private static readonly PrototypeCardDefinition[] CardDefinitions =
    [
        // Luminesce is an Ancient-token skill, created only by Radiant Pearl.
        // It retains until played, then grants two Energy and exhausts;
        // its upgrade raises the Energy to three.
        new(LuminesceId, "Luminesce", 0,
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.GainEnergy, 2,
                UpgradeDelta: 1)],
            ExhaustOnUse: true, Rarity: PrototypeCardRarity.Token,
            Retain: true, RewardEligible: false,
            Type: PrototypeCardType.Skill, MaxUpgradeLevel: 1)
    ];

    public static PrototypeCardDefinition[] Cards => CardDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneCardDefinition)
        .ToArray();

    public static bool IsTouchOfOrobasEligible(PlayerState player) =>
        player.ActiveRelics.Any(relic =>
            relic.RelicId == RingOfTheSnakeId);

    public static bool IsArchaicToothEligible(PlayerState player) =>
        player.Deck.Any(card => card.CardId == NeutralizeId
            && !PrototypeCardRules.IsEternal(card));

    internal static PlayerState ApplyArchaicToothAcquisition(
        PlayerState player, int acquiredRelicIndex)
    {
        if ((uint)acquiredRelicIndex >= (uint)player.Relics.Length
            || player.Relics[acquiredRelicIndex].RelicId != ArchaicToothId)
            throw new InvalidOperationException(
                "Archaic Tooth acquisition requires its newly acquired instance.");

        var cardIndex = Array.FindIndex(player.Deck, card =>
            card.CardId == NeutralizeId
            && !PrototypeCardRules.IsEternal(card));
        if (cardIndex < 0)
            throw new InvalidOperationException(
                "Archaic Tooth requires Silent's transformable Neutralize.");

        var deck = (CardInstance[])player.Deck.Clone();
        var original = deck[cardIndex];
        deck[cardIndex] = original with { CardId = SuppressId };
        var relics = (RelicInstance[])player.Relics.Clone();
        relics[acquiredRelicIndex] = relics[acquiredRelicIndex] with
        {
            PersistentState = System.Text.Json.JsonSerializer
                .SerializeToElement(new
                {
                    StarterCardId = NeutralizeId,
                    AncientCardId = SuppressId,
                    CardInstanceId = original.InstanceId
                })
        };
        return player with { Deck = deck, Relics = relics };
    }

    internal static PlayerState ApplyAlchemicalCofferAcquisition(
        PlayerState player, int acquiredRelicIndex, RngBundle rng,
        RngBundle? nativeRunRng = null)
    {
        if ((uint)acquiredRelicIndex >= (uint)player.Relics.Length
            || player.Relics[acquiredRelicIndex].RelicId != AlchemicalCofferId)
            throw new InvalidOperationException(
                "Alchemical Coffer acquisition requires its newly acquired instance.");

        var oldSlotCount = player.PotionSlots.Length;
        var slots = player.PotionSlots
            .Concat(new PotionInstance?[4])
            .ToArray();
        var potionIds = nativeRunRng is null
            ? PrototypeNativePotionShop.PickWeightedDistinct(
                rng, "event", 4)
            : PrototypeNativePotionShop.PickWeightedDistinctNativeWithAudit(
                nativeRunRng, "run.combat_potion_generation", 4,
                "relic:alchemical_coffer:acquisition",
                new NativeRandomIndexProviderV111()).PotionIds;
        if (!player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId).PreventPotionAcquisition))
        {
            for (var i = 0; i < potionIds.Length; i++)
            {
                slots[oldSlotCount + i] = new PotionInstance(
                    potionIds[i], PrototypeJson.EmptyObject());
            }
        }

        return player with { PotionSlots = slots };
    }

    internal static PlayerState ApplyTouchOfOrobasAcquisition(
        PlayerState player, int acquiredRelicIndex)
    {
        if ((uint)acquiredRelicIndex >= (uint)player.Relics.Length
            || player.Relics[acquiredRelicIndex].RelicId != TouchOfOrobasId)
            throw new InvalidOperationException(
                "Touch of Orobas acquisition requires its newly acquired instance.");

        var starterIndex = Array.FindIndex(player.Relics, relic =>
            relic.RelicId == RingOfTheSnakeId);
        if (starterIndex < 0)
            throw new InvalidOperationException(
                "Touch of Orobas requires the Silent starter relic.");

        var relics = (RelicInstance[])player.Relics.Clone();
        relics[starterIndex] = new RelicInstance(
            RingOfTheDrakeId, PrototypeJson.EmptyObject());
        relics[acquiredRelicIndex] = relics[acquiredRelicIndex] with
        {
            PersistentState = System.Text.Json.JsonSerializer
                .SerializeToElement(new
                {
                    StarterRelicId = RingOfTheSnakeId,
                    UpgradedRelicId = RingOfTheDrakeId
                })
        };
        return player with { Relics = relics };
    }

    internal static int HandDrawBonusForTurn(PlayerState player, int turn) =>
        player.ActiveRelics.Sum(relic =>
        {
            var definition = PrototypeContent.Relic(relic.RelicId);
            return definition.HandDrawBonusThroughTurn == 0
                || turn <= definition.HandDrawBonusThroughTurn
                    ? definition.HandDrawBonus
                    : 0;
        });

    internal static PlayerState ApplySandCastleAcquisition(
        PlayerState player, RngBundle rng)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rng);

        // SandCastle.AfterObtained filters CardModel.IsUpgradable, shuffles
        // the complete eligible deck list, then upgrades at most six.
        // Prototype RNG is retained here until acquisition randomness moves
        // behind the native/scripted random-decision providers.
        var candidates = player.Deck
            .Where(card => PrototypeGameEngine.CanSelectEventDeckCard(
                card, PrototypePersistentDeckChoiceKind.Upgrade,
                null, null, false))
            .Select(card => card.InstanceId)
            .ToArray();
        _ = LegacyFisherYatesV111.ShuffleInPlace(
            candidates,
            rng,
            "event",
            SandCastleAcquisitionShuffleSiteId,
            SandCastleAcquisitionShuffleOwnerId,
            controlledCandidateLabel: instanceId =>
                "card-instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture));
        var selected = candidates.Take(6).ToHashSet();
        if (selected.Count == 0)
            return player;

        return player with
        {
            Deck = player.Deck.Select(card => selected.Contains(card.InstanceId)
                ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                : card).ToArray()
        };
    }
}
