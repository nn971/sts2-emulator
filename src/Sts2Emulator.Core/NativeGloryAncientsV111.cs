namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed dependencies for Glory's Ancient events.
/// </summary>
public static class NativeGloryAncientsV111
{
    public const string BlessedAntlerId = "proto.native.ancient.blessed_antler";
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
    public const string ApotheosisCardId = "proto.native.ancient.apotheosis";
    public const string TriBoomerangId = "proto.native.ancient.tri_boomerang";
    public const string ClawsId = "proto.native.ancient.claws";
    public const string WarHammerId = "proto.native.ancient.war_hammer";
    public const string MaulCardId = "proto.native.ancient.maul";

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        new(BlessedAntlerId, "Blessed Antler",
            EnergyPerTurnBonus: 1,
            Triggers:
            [
                new(PrototypeCombatEventKind.BeforeHandDraw,
                    [new(PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
                        3, CardId: "proto.status.dazed")],
                    TurnEquals: 1)
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
            ProcureRandomPotionsBeforeCombat: true)
    ];

    public static PrototypeCardDefinition[] Cards { get; } =
    [
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
}
