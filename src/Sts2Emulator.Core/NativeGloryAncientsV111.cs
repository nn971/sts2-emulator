namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed dependencies for Glory's Ancient events.
/// </summary>
public static class NativeGloryAncientsV111
{
    public const string DiamondDiademId = "proto.native.ancient.diamond_diadem";
    public const string LoomingFruitId = "proto.native.ancient.looming_fruit";
    public const string SaiId = "proto.native.ancient.sai";
    public const string SignetRingId = "proto.native.ancient.signet_ring";

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
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
        new(SignetRingId, "Signet Ring", RunTriggers:
        [
            new(PrototypeRunEventKind.RelicAcquired,
                [new(PrototypeRunEffectKind.GainGold, 888)])
        ])
    ];
}
