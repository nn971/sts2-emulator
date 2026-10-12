namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed dependencies for Glory's Ancient events.
/// </summary>
public static class NativeGloryAncientsV111
{
    public const string DiamondDiademId = "proto.native.ancient.diamond_diadem";
    public const string CrossbowId = "proto.native.ancient.crossbow";
    public const string IronClubId = "proto.native.ancient.iron_club";
    public const string ThrowingAxeId = "proto.native.ancient.throwing_axe";
    public const string LoomingFruitId = "proto.native.ancient.looming_fruit";
    public const string SaiId = "proto.native.ancient.sai";
    public const string SpikedGauntletsId = "proto.native.ancient.spiked_gauntlets";
    public const string SignetRingId = "proto.native.ancient.signet_ring";

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
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
        ])
    ];
}
