namespace Sts2Emulator.Core;

/// <summary>
/// Completes the three source-pinned v0.111 Glory weak encounters
/// in the forced-combat registry; native Act 3 routing remains gated.
/// Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class PrototypeNativeGloryWeak
{
    public const string LivingShieldId = "proto.native.glory.living_shield";
    public const string RampartId = "proto.native.glory.rampart";
    public const string TurretOperatorWeakId =
        "proto.native.glory.encounter.turret_operator_weak";
    public const string TurretOperatorId =
        "proto.native.glory.turret_operator";

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        // Native RampartPower.AfterSideTurnStart on PLAYER side:
        // grant 25 unpowered Block to each living Turret Operator.
        // The owner must still be alive; two shields stack independently.
        new(RampartId, "Rampart", 0, [],
            AllyBlockAtPlayerTurnStartPerStack: 1,
            AllyBlockTargetEnemyId: TurretOperatorId)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // LivingShield.cs: opener Shield Slam. While a living ally
        // remains, keep slamming; once alone, Smash forever and gain
        // +3 Strength after each Smash.
        new(LivingShieldId, "Living Shield", 55, 0,
        [
            new("shield_slam",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 6)]),
            new("smash",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 16,
                    AscensionDeltas: [new(9, 2)]),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                    PowerId: "proto.power.strength")
            ])
        ],
        HpAscensionDeltas: [new(8, 10)],
        StartingPowers: [new(RampartId, 25)],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("slam",
        [
            new("slam", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "check_allies"),
            new("smash", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "smash"),
            new("check_allies", PrototypeEnemyAiStateKind.Conditional,
                ConditionalBranches:
                [
                    new("slam",
                        PrototypeEnemyAiConditionKind.LivingEnemiesGreaterThan,
                        "1"),
                    new("smash",
                        PrototypeEnemyAiConditionKind.IsAlone)
                ])
        ]))
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(TurretOperatorWeakId, PrototypeRoomType.Combat,
            [LivingShieldId, TurretOperatorId],
            MinAct: 3, MaxAct: 3, Weight: 0,
            Formation:
            [
                new(LivingShieldId, 0),
                new(TurretOperatorId, 1)
            ])
    ];
}
