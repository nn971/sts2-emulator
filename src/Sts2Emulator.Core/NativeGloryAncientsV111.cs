namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed dependencies for Glory's Ancient events.
/// </summary>
public static class NativeGloryAncientsV111
{
    public const string LoomingFruitId = "proto.native.ancient.looming_fruit";
    public const string SignetRingId = "proto.native.ancient.signet_ring";

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        new(LoomingFruitId, "Looming Fruit", RunTriggers:
        [
            new(PrototypeRunEventKind.RelicAcquired,
                [new(PrototypeRunEffectKind.GainMaxHp, 31)])
        ]),
        new(SignetRingId, "Signet Ring", RunTriggers:
        [
            new(PrototypeRunEventKind.RelicAcquired,
                [new(PrototypeRunEffectKind.GainGold, 888)])
        ])
    ];
}
