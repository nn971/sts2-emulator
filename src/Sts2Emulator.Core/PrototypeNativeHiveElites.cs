namespace Sts2Emulator.Core;

/// <summary>
/// Gated Hive elite implementations sourced from the pinned v0.111.0
/// DecimillipedeSegment / Entomancer / InfestedPrism model families.
/// </summary>
public static class PrototypeNativeHiveElites
{
    public const string DecimillipedeEncounterId =
        "proto.native.hive.encounter.decimillipede_elite";
    public const string EntomancerEncounterId =
        "proto.native.hive.encounter.entomancer_elite";
    public const string PrismsEncounterId =
        "proto.native.hive.encounter.infested_prisms_elite";
    public const string FrontId = "proto.native.hive.decimillipede_segment_front";
    public const string MiddleId = "proto.native.hive.decimillipede_segment_middle";
    public const string BackId = "proto.native.hive.decimillipede_segment_back";
    public const string EntomancerId = "proto.native.hive.entomancer";
    public const string PrismId = "proto.native.hive.infested_prism";
    public const string ReattachId = "proto.native.hive.reattach";
    public const string PersonalHiveId = "proto.native.hive.personal_hive";
    public const string VitalSparkId = "proto.native.hive.vital_spark";
    public const string TaintedId = "proto.native.hive.tainted";

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new(ReattachId, "Reattach", 0, [],
            DoesNotStack: true),
        new(PersonalHiveId, "Personal Hive", 0, [],
            StatusCardAddedToDrawWhenAttacked: "proto.status.dazed"),
        new(VitalSparkId, "Vital Spark", 0, [],
            TaintsPlayerSkills: true),
        new(TaintedId, "Tainted", 0, [],
            IsDebuff: true,
            PlayerIncomingPoweredAttackFlatBonusPerStack: 1,
            RemoveAtEnemySideTurnEnd: true)
    ];

    private static readonly PrototypeEnemyMoveDefinition[] SegmentMoves =
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
        ]),
        new("dead", []),
        new("reattach",
            [new(PrototypeEnemyEffectKind.ReattachSelf, 25)])
    ];

    private static readonly PrototypeEnemyAiDefinition SegmentAi =
        new("writhe",
        [
            new("writhe", PrototypeEnemyAiStateKind.Move, 0, "constrict"),
            new("bulk", PrototypeEnemyAiStateKind.Move, 1, "writhe"),
            new("constrict", PrototypeEnemyAiStateKind.Move, 2, "bulk"),
            new("dead", PrototypeEnemyAiStateKind.Move, 3, "reattach"),
            new("reattach", PrototypeEnemyAiStateKind.Move, 4, "random"),
            new("random", PrototypeEnemyAiStateKind.Random,
                Branches:
                [
                    new("writhe",
                        RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("bulk",
                        RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("constrict",
                        RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat)
                ])
        ]);

    private static PrototypeEnemyDefinition Segment(
        string id, string name) =>
        new(id, name, 46, 0, SegmentMoves,
            MinHp: 40, HpAscensionDeltas: [new(8, 6)],
            MinHpAscensionDeltas: [new(8, 6)],
            StartingPowers: [new(ReattachId, 25)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: SegmentAi,
            ReattachesWithLivingAlly: true,
            UniqueEvenInitialHp: true);

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        Segment(FrontId, "Decimillipede Segment (Front)"),
        Segment(MiddleId, "Decimillipede Segment (Middle)"),
        Segment(BackId, "Decimillipede Segment (Back)"),
        // Native sequence: Bees -> Spear -> Pheromone Spit -> Bees.
        // Spit at hive <3: Hive+1, Strength+1; at hive >=3:
        // Strength+2 without increasing the hive counter.
        new(EntomancerId, "Entomancer", 145, 0,
        [
            new("bees",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 3,
                    Repetitions: 7,
                    RepetitionAscensionDeltas: [new(9, 1)])]),
            new("spear",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 18,
                    AscensionDeltas: [new(9, 2)])]),
            new("pheromone_spit",
            [
                new(PrototypeEnemyEffectKind.ApplyEnemyPowerWithCapFallback,
                    1, PowerId: PersonalHiveId,
                    MaxPowerStacksBeforeFallback: 3,
                    FallbackPowerId: "proto.power.strength",
                    FallbackPowerAmount: 1),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 1,
                    PowerId: "proto.power.strength")
            ])
        ],
            HpAscensionDeltas: [new(8, 20)],
            StartingPowers: [new(PersonalHiveId, 1)],
            MovePolicy: PrototypeEnemyMovePolicy.SequentialLoop),
        // Native sequence: Jab -> Radiate -> Whirlwind -> Pulsate.
        // Vital Spark taints every skill in combat, including generated
        // cards, and each use of a tainted skill creates TaintedPower.
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
            StartingPowers: [new(VitalSparkId, 2,
                AscensionDeltas: [new(9, 1)])])
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(DecimillipedeEncounterId, PrototypeRoomType.Elite,
            [FrontId, MiddleId, BackId],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new(FrontId, 0, "segment1"),
                new(MiddleId, 1, "segment2"),
                new(BackId, 2, "segment3")
            ],
            CyclicOpeningAiStateIds: ["writhe", "bulk", "constrict"]),
        new(EntomancerEncounterId, PrototypeRoomType.Elite,
            [EntomancerId], MinAct: 2, MaxAct: 2, Weight: 0),
        new(PrismsEncounterId, PrototypeRoomType.Elite,
            [PrismId], MinAct: 2, MaxAct: 2, Weight: 0)
    ];
}
