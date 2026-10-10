namespace Sts2Emulator.Core;

/// <summary>
/// Source-pinned Glory normal enemy slice. Synthetic fixtures only until
/// the native Act 3 map/encounter selector is complete.
/// Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class PrototypeNativeGloryNewNormals
{
    public const string OwlId = "proto.native.glory.owl_magistrate";
    public const string OwlEncounterId =
        "proto.native.glory.encounter.owl_magistrate_normal";
    public const string SoarId = "proto.native.glory.soar";

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        // SoarPower multiplies powered attack damage dealt to its
        // owner by 0.5. Poison and non-attack loss are unaffected.
        new(SoarId, "Soar", 0, [],
            DoesNotStack: true,
            EnemyIncomingPoweredAttackNumerator: 1,
            EnemyIncomingPoweredAttackDenominator: 2)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // Scrutiny -> six-hit Peck -> take flight / Soar ->
        // Verdict / Vulnerable 4 / remove Soar, looping.
        new(OwlId, "Owl Magistrate", 231, 0,
        [
            new("scrutiny",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 16,
                    AscensionDeltas: [new(9, 1)])]),
            new("peck_assault",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 4,
                    Repetitions: 6)]),
            new("judicial_flight",
                [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 1,
                    PowerId: SoarId)]),
            new("verdict",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 33,
                    AscensionDeltas: [new(9, 3)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 4,
                    PowerId: "proto.power.vulnerable"),
                new(PrototypeEnemyEffectKind.RemoveEnemyPower, 0,
                    PowerId: SoarId)
            ])
        ],
        HpAscensionDeltas: [new(8, 16)])
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(OwlEncounterId, PrototypeRoomType.Combat,
            [OwlId], MinAct: 3, MaxAct: 3, Weight: 0)
    ];
}
