namespace Sts2Emulator.Core;

/// <summary>
/// Shared v0.111.0 relic behavior used by configured Silent runs.
/// </summary>
public static class PrototypeNativeSharedRelics
{
    public const string AkabekoId = "proto.relic.akabeko";
    public const string PearId = "proto.relic.pear";
    public const string StrawberryId = "proto.relic.strawberry";
    public const string VigorId = "proto.power.vigor";

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        // Pear.AfterObtained grants ten Max HP.
        new(PearId, "Pear",
            RunTriggers:
            [
                new(PrototypeRunEventKind.RelicAcquired,
                    [new(PrototypeRunEffectKind.GainMaxHp, 10)])
            ]),
        // Strawberry.AfterObtained grants seven Max HP; GainMaxHp also
        // raises current HP by the same amount in the run model.
        new(StrawberryId, "Strawberry",
            RunTriggers:
            [
                new(PrototypeRunEventKind.RelicAcquired,
                    [new(PrototypeRunEffectKind.GainMaxHp, 7)])
            ]),
        // Akabeko.AfterSideTurnStart grants 8 Vigor on the owner's first
        // turn. PlayerSideTurnStarted is the corresponding combat boundary.
        new(AkabekoId, "Akabeko",
            Triggers:
            [
                new(PrototypeCombatEventKind.PlayerSideTurnStarted,
                    [new(PrototypeCombatEffectKind.ApplyPlayerPower, 8,
                        PowerId: VigorId)],
                    TurnEquals: 1)
            ])
    ];
}
