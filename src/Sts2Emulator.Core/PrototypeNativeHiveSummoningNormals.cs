namespace Sts2Emulator.Core;

/// <summary>
/// Source-pinned v0.111.0 Hive summon encounters. Registered for forced
/// combat tests; native Hive map and encounter-bag integration remains gated.
/// </summary>
public static class PrototypeNativeHiveSummoningNormals
{
    public const string OvicopterNormalId =
        "proto.native.hive.encounter.ovicopter_normal";
    public const string ObscuraNormalId =
        "proto.native.hive.encounter.the_obscura_normal";
    public const string OvicopterId = "proto.native.hive.ovicopter";
    public const string ToughEggId = "proto.native.hive.tough_egg";
    // A transformed ToughEgg retains the physical combat instance.
    // Native title switches to Hatchling and its HP / moves change.
    public const string HatchlingId = "proto.native.hive.hatchling";
    public const string ObscuraId = "proto.native.hive.the_obscura";
    public const string ParafrightId = "proto.native.hive.parafright";
    public const string HatchPowerId = "proto.native.hive.hatch";

    private static readonly string[] EggSlots =
        ["egg1", "egg2", "egg3", "egg4", "egg5"];

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        // Eggs laid during the enemy turn have 2 Hatch on spawn.
        // The summoning side-turn end decrements to 1 before next intent.
        new(HatchPowerId, "Hatch", 0, [],
            EnemyStacksDecayAtSideTurnEnd: 1)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // Ovicopter.cs: Lay 3 -> Smash -> Tenderizer ->
        // (Lay if <=3 living teammates, otherwise Paste) -> Smash.
        new(OvicopterId, "Ovicopter", 130, 0,
            [
                new("lay_eggs",
                [
                    new(PrototypeEnemyEffectKind.SummonEnemy, 1,
                        Repetitions: 3, EnemyId: ToughEggId,
                        SummonSlotNames: EggSlots,
                        ReserveSummonSlotAfterDeath: true)
                ]),
                new("smash",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 16,
                        AscensionDeltas: [new(9, 1)])]),
                new("tenderizer",
                [
                    new(PrototypeEnemyEffectKind.DamagePlayer, 7,
                        AscensionDeltas: [new(9, 1)]),
                    new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                        PowerId: "proto.power.vulnerable")
                ]),
                new("nutritional_paste",
                    [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                        PowerId: "proto.power.strength",
                        AscensionDeltas: [new(9, 1)])])
            ],
            MinHp: 124,
            HpAscensionDeltas: [new(8, 2)],
            MinHpAscensionDeltas: [new(8, 2)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("lay",
            [
                new("lay", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 0, NextStateId: "smash"),
                new("smash", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 1, NextStateId: "tenderizer"),
                new("tenderizer", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 2, NextStateId: "summon_choice"),
                new("paste", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 3, NextStateId: "smash"),
                new("summon_choice", PrototypeEnemyAiStateKind.Conditional,
                    ConditionalBranches:
                    [
                        new("lay", PrototypeEnemyAiConditionKind.LivingEnemiesAtMost,
                            "3"),
                        new("paste",
                            PrototypeEnemyAiConditionKind.LivingEnemiesGreaterThan,
                            "3")
                    ])
            ])),
        // A spawned Tough Egg has 2 Hatch, plus Minion. At its next
        // enemy action Hatch transforms it into a stronger Hatchling.
        new(ToughEggId, "Tough Egg", 18, 0,
            [
                new("hatch",
                    [new(PrototypeEnemyEffectKind.TransformEnemy, 0,
                        EnemyId: HatchlingId)])
            ],
            MinHp: 14,
            HpAscensionDeltas: [new(8, 1)],
            MinHpAscensionDeltas: [new(8, 1)],
            StartingPowers:
            [
                new("proto.power.minion", 1),
                new(HatchPowerId, 2)
            ],
            IsMinion: true),
        new(HatchlingId, "Hatchling", 22, 0,
            [
                new("nibble",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 4,
                        AscensionDeltas: [new(9, 1)])])
            ],
            MinHp: 19,
            HpAscensionDeltas: [new(8, 1)],
            MinHpAscensionDeltas: [new(8, 1)],
            StartingPowers: [new("proto.power.minion", 1)],
            IsMinion: true),
        // TheObscura.cs: initial Illusion, then uniform nonrepeating
        // Gaze / Wail / Hardening Strike. Wail buffs the full live team.
        new(ObscuraId, "The Obscura", 123, 0,
            [
                new("illusion",
                    [new(PrototypeEnemyEffectKind.SummonEnemy, 1,
                        EnemyId: ParafrightId,
                        SummonSlotNames: ["illusion"])]),
                new("piercing_gaze",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 10,
                        AscensionDeltas: [new(9, 1)])]),
                new("wail",
                    [new(PrototypeEnemyEffectKind.ApplyAllEnemyPower, 3,
                        PowerId: "proto.power.strength")]),
                new("hardening_strike",
                [
                    new(PrototypeEnemyEffectKind.DamagePlayer, 6,
                        AscensionDeltas: [new(9, 1)]),
                    new(PrototypeEnemyEffectKind.GainBlock, 6,
                        AscensionDeltas: [new(9, 1)])
                ])
            ],
            HpAscensionDeltas: [new(8, 6)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("illusion",
            [
                new("illusion", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 0, NextStateId: "random"),
                new("gaze", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 1, NextStateId: "random"),
                new("wail", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 2, NextStateId: "random"),
                new("hardening", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 3, NextStateId: "random"),
                new("random", PrototypeEnemyAiStateKind.Random,
                    Branches:
                    [
                        new("gaze", RepeatRule:
                            PrototypeEnemyAiRepeatRule.CannotRepeat),
                        new("wail", RepeatRule:
                            PrototypeEnemyAiRepeatRule.CannotRepeat),
                        new("hardening", RepeatRule:
                            PrototypeEnemyAiRepeatRule.CannotRepeat)
                    ])
            ])),
        // Parafright / IllusionPower: 21 HP, Slam every active turn.
        // Lethal damage leaves an untargetable shell until its next
        // enemy action (revive), as long as the Obscura still lives.
        new(ParafrightId, "Parafright", 21, 0,
            [
                new("slam", [new(PrototypeEnemyEffectKind.DamagePlayer,
                    16, AscensionDeltas: [new(9, 1)])])
            ],
            StartingPowers:
            [
                new("proto.power.illusion", 1),
                new("proto.power.minion", 1)
            ],
            IsMinion: true,
            RevivesOnEnemyTurn: true)
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(OvicopterNormalId, PrototypeRoomType.Combat,
            [OvicopterId], MinAct: 2, MaxAct: 2, Weight: 0,
            Formation: [new(OvicopterId, 5, "ovicopter")]),
        new(ObscuraNormalId, PrototypeRoomType.Combat,
            [ObscuraId], MinAct: 2, MaxAct: 2, Weight: 0,
            Formation: [new(ObscuraId, 1, "obscura")])
    ];
}
