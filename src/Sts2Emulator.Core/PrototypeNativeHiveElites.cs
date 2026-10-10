namespace Sts2Emulator.Core;

/// <summary>
/// Source-pinned first pass of the v0.111 Hive elites. Native Hive routing
/// stays disabled. See reference-build status for unsupported power hooks.
/// </summary>
public static class PrototypeNativeHiveElites
{
    public const string EntomancerId = "proto.native.hive.entomancer";
    public const string PrismId = "proto.native.hive.infested_prism";
    public const string FrontId = "proto.native.hive.decimillipede_segment_front";
    public const string MiddleId = "proto.native.hive.decimillipede_segment_middle";
    public const string BackId = "proto.native.hive.decimillipede_segment_back";
    public const string EntomancerEncounterId =
        "proto.native.hive.encounter.entomancer_elite";
    public const string PrismEncounterId =
        "proto.native.hive.encounter.infested_prisms_elite";
    public const string DecimillipedeEncounterId =
        "proto.native.hive.encounter.decimillipede_elite";
    public const string PersonalHiveId = "proto.native.hive.personal_hive";
    public const string VitalSparkId = "proto.native.hive.vital_spark";
    public const string ReattachId = "proto.native.hive.reattach";
    public const string TaintedPowerId = "proto.native.hive.tainted";

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new(PersonalHiveId, "Personal Hive", 0, []),
        new(VitalSparkId, "Vital Spark", 0, []),
        new(TaintedPowerId, "Tainted", 0, [], IsDebuff: true),
        new(ReattachId, "Reattach", 0, [])
    ];

    private static PrototypeEnemyDefinition Segment(string id) =>
        new(id, "Decimillipede Segment", 46, 0,
        [
            new("writhe",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 5,
                    Repetitions: 2, AscensionDeltas: [new(9, 1)])]),
            new("bulk",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 6,
                    AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                    PowerId: "proto.power.strength")
            ]),
            new("constrict",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 8,
                    AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                    PowerId: "proto.power.weak")
            ])
        ],
        MinHp: 40,
        HpAscensionDeltas: [new(8, 6)],
        MinHpAscensionDeltas: [new(8, 6)],
        StartingPowers: [new(ReattachId, 25)],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("writhe",
        [
            new("writhe", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "constrict"),
            new("bulk", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "writhe"),
            new("constrict", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "bulk"),
            new("reattach_random", PrototypeEnemyAiStateKind.Random,
                Branches:
                [
                    new("writhe", RepeatRule:
                        PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("bulk", RepeatRule:
                        PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("constrict", RepeatRule:
                        PrototypeEnemyAiRepeatRule.CannotRepeat)
                ])
        ]));

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // Initial BEES, then SPEAR, PHEROMONE_SPIT, looping.
        new(EntomancerId, "Entomancer", 145, 0,
        [
            new("bees",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 3,
                    Repetitions: 7,
                    RepetitionAscensionDeltas: [new(9, 1)])]),
            new("spear",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 18,
                    AscensionDeltas: [new(9, 2)])]),
            // Evaluate the source's stack condition before upgrading
            // Hive: 1 Strength and 1 Hive below 3, else 2 Strength.
            new("pheromone_spit",
            [
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                    PowerId: "proto.power.strength",
                    OwnerPowerStackConditionId: PersonalHiveId,
                    OwnerPowerStacksAtLeast: 3),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 1,
                    PowerId: "proto.power.strength",
                    OwnerPowerStackConditionId: PersonalHiveId,
                    OwnerPowerStacksLessThan: 3),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 1,
                    PowerId: PersonalHiveId,
                    OwnerPowerStackConditionId: PersonalHiveId,
                    OwnerPowerStacksLessThan: 3)
            ])
        ],
        HpAscensionDeltas: [new(8, 20)],
        StartingPowers: [new(PersonalHiveId, 1)]),
        // JAB -> RADIATE -> WHIRLWIND -> PULSATE.
        new(PrismId, "Infested Prism", 161, 0,
        [
            new("jab",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 15,
                    AscensionDeltas: [new(9, 2)])]),
            new("radiate",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 11,
                    AscensionDeltas: [new(9, 2)]),
                new(PrototypeEnemyEffectKind.GainBlock, 11,
                    AscensionDeltas: [new(9, 2)])
            ]),
            new("whirlwind",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 5,
                    Repetitions: 3, AscensionDeltas: [new(9, 1)])]),
            new("pulsate",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 8,
                    AscensionDeltas: [new(9, 2)]),
                new(PrototypeEnemyEffectKind.GainBlock, 20,
                    AscensionDeltas: [new(8, 2)]),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                    PowerId: VitalSparkId,
                    AscensionDeltas: [new(9, 1)])
            ])
        ],
        HpAscensionDeltas: [new(8, 10)],
        StartingPowers:
        [
            new(VitalSparkId, 2,
                AscensionDeltas: [new(9, 1)])
        ]),
        Segment(FrontId),
        Segment(MiddleId),
        Segment(BackId)
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(EntomancerEncounterId, PrototypeRoomType.Elite,
            [EntomancerId], MinAct: 2, MaxAct: 2, Weight: 0),
        new(PrismEncounterId, PrototypeRoomType.Elite,
            [PrismId], MinAct: 2, MaxAct: 2, Weight: 0),
        // Three positions with one shared rotational opener roll.
        // HP uniquification and two-stage reattachment are modeled;
        // encounter-local RNG and the native reattach visual delay
        // remain unverified against gameplay traces.
        new(DecimillipedeEncounterId, PrototypeRoomType.Elite,
            [FrontId, MiddleId, BackId],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new(FrontId, 0, "segment1"),
                new(MiddleId, 1, "segment2"),
                new(BackId, 2, "segment3")
            ],
            CyclicOpeningAiStateIds:
                ["writhe", "bulk", "constrict"],
            UniqueEvenEnemyHpWithinEncounter: true)
    ];
}
