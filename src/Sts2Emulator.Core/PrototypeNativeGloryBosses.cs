namespace Sts2Emulator.Core;

/// <summary>
/// Source-pinned Glory three-knight elite and final-act bosses. All entries
/// remain forced-combat only until Glory route generation is source-audited.
/// Source: Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class PrototypeNativeGloryBosses
{
    public const string KnightsEncounterId = "proto.native.glory.encounter.knights_elite";
    public const string QueenEncounterId = "proto.native.glory.encounter.queen_boss";
    public const string TestSubjectEncounterId = "proto.native.glory.encounter.test_subject_boss";
    public const string AeonglassEncounterId = "proto.native.glory.encounter.aeonglass_boss";
    public const string FlailKnightId = "proto.native.glory.flail_knight";
    public const string SpectralKnightId = "proto.native.glory.spectral_knight";
    public const string MagiKnightId = "proto.native.glory.magi_knight";
    public const string QueenId = "proto.native.glory.queen";
    public const string AmalgamId = "proto.native.glory.torch_head_amalgam";
    public const string TestSubjectId = "proto.native.glory.test_subject";
    public const string AeonglassId = "proto.native.glory.aeonglass";
    public const string HexId = "proto.native.glory.hex";
    public const string DampenId = "proto.native.glory.dampen";
    public const string ChainsId = "proto.native.glory.chains_of_binding";
    public const string AdaptableId = "proto.native.glory.adaptable";
    public const string EnrageId = "proto.native.glory.enrage";
    public const string PainfulStabsId = "proto.native.glory.painful_stabs";
    public const string NemesisId = "proto.native.glory.nemesis";
    public const string NemesisIntangibleId = "proto.native.glory.nemesis_intangible";
    public const string WitheringPresenceId = "proto.native.glory.withering_presence";
    public const string WitherId = "proto.status.wither";

    public static PrototypeCardDefinition[] Cards { get; } =
    [
        new(WitherId, "Wither", -1, PrototypeCardTarget.None, [],
            Rarity: PrototypeCardRarity.Status, Type: PrototypeCardType.Status,
            Unplayable: true, RewardEligible: false,
            EndTurnDamageIfInHand: 3, MaxUpgradeLevel: 0)
    ];

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new(HexId, "Hex", 0, [], DoesNotStack: true, IsDebuff: true,
            SourceBoundToEnemy: true,
            SourceBoundCardAffliction: PrototypeCardAfflictionKind.Hexed,
            ClearSourceAfflictionWhenRemoved: true),
        new(DampenId, "Dampen", 0, [], DoesNotStack: true, IsDebuff: true,
            SourceBoundToEnemy: true, DowngradeExistingCardsOnApply: true,
            RestoreDowngradedCardsWhenLastSourceRemoved: true),
        new(ChainsId, "Chains of Binding", 0, [], IsDebuff: true,
            SourceBoundToEnemy: true),
        new(AdaptableId, "Adaptable", 0, [], DoesNotStack: true,
            OwnerDeathTriggersFatal: false),
        new(EnrageId, "Enrage", 0, []),
        new(PainfulStabsId, "Painful Stabs", 0, []),
        new(NemesisId, "Nemesis", 0, [], DoesNotStack: true),
        new(NemesisIntangibleId, "Intangible (Nemesis)", 0, [],
            DoesNotStack: true, EnemyHpLossCapPerTrigger: 1),
        new(WitheringPresenceId, "Withering Presence", 0, [],
            DoesNotStack: true)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        new(FlailKnightId, "Flail Knight", 101, 0,
        [
            new("war_chant", [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                PowerId: "proto.power.strength")]),
            new("flail", [new(PrototypeEnemyEffectKind.DamagePlayer, 9,
                Repetitions: 2, AscensionDeltas: [new(9, 1)])]),
            new("ram", [new(PrototypeEnemyEffectKind.DamagePlayer, 15,
                AscensionDeltas: [new(9, 2)])])
        ], HpAscensionDeltas: [new(8, 7)],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("ram",
        [
            new("ram", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "random"),
            new("chant", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "random"),
            new("flail", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "random"),
            new("random", PrototypeEnemyAiStateKind.Random,
                Branches:
                [
                    new("chant", RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("flail", Weight: 2),
                    new("ram", Weight: 2)
                ])
        ])),
        new(SpectralKnightId, "Spectral Knight", 93, 0,
        [
            new("hex", [new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                PowerId: HexId)]),
            new("soul_slash", [new(PrototypeEnemyEffectKind.DamagePlayer, 15,
                AscensionDeltas: [new(9, 2)])]),
            new("soul_flame", [new(PrototypeEnemyEffectKind.DamagePlayer, 3,
                Repetitions: 3, AscensionDeltas: [new(9, 1)])])
        ], HpAscensionDeltas: [new(8, 4)],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("hex",
        [
            new("hex", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "slash"),
            new("slash", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "random"),
            new("flame", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "random"),
            new("random", PrototypeEnemyAiStateKind.Random,
                Branches:
                [
                    new("slash", Weight: 2),
                    new("flame", RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat)
                ])
        ])),
        new(MagiKnightId, "Magi Knight", 82, 0,
        [
            new("power_shield",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 6,
                    AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.GainBlock, 5,
                    AscensionDeltas: [new(8, 4)])
            ]),
            new("dampen", [new(PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                PowerId: DampenId)]),
            new("ram", [new(PrototypeEnemyEffectKind.DamagePlayer, 10,
                AscensionDeltas: [new(9, 1)])]),
            new("prep", [new(PrototypeEnemyEffectKind.GainBlock, 5,
                AscensionDeltas: [new(8, 4)])]),
            new("magic_bomb", [new(PrototypeEnemyEffectKind.DamagePlayer, 35,
                AscensionDeltas: [new(9, 5)])])
        ], HpAscensionDeltas: [new(8, 7)], MoveLoopStartIndex: 2),
        new(AmalgamId, "Torch Head Amalgam", 199, 0,
        [
            new("strong_tackle", [new(PrototypeEnemyEffectKind.DamagePlayer, 26,
                AscensionDeltas: [new(9, 6)])]),
            new("tackle", [new(PrototypeEnemyEffectKind.DamagePlayer, 18,
                AscensionDeltas: [new(9, 4)])]),
            new("soul_beam", [new(PrototypeEnemyEffectKind.DamagePlayer, 8,
                Repetitions: 3)]),
            new("weak_tackle_1", [new(PrototypeEnemyEffectKind.DamagePlayer, 14,
                AscensionDeltas: [new(9, 2)])]),
            new("weak_tackle_2", [new(PrototypeEnemyEffectKind.DamagePlayer, 14,
                AscensionDeltas: [new(9, 2)])])
        ], HpAscensionDeltas: [new(8, 12)],
        MoveLoopStartIndex: 2, IsMinion: true,
        StartingPowers: [new("proto.power.minion", 1)]),
        new(QueenId, "Queen", 400, 0,
        [
            new("puppet_strings", [new(PrototypeEnemyEffectKind.ApplyPlayerPower,
                3, PowerId: ChainsId)]),
            new("you_are_mine",
            [
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 99,
                    PowerId: "proto.power.frail"),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 99,
                    PowerId: "proto.power.weak"),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 99,
                    PowerId: "proto.power.vulnerable")
            ]),
            new("burn_bright",
            [
                new(PrototypeEnemyEffectKind.GrantStrengthToOtherLivingEnemies, 1,
                    PowerId: "proto.power.strength"),
                new(PrototypeEnemyEffectKind.GainBlock, 20)
            ]),
            new("off_with_your_head",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 3,
                    Repetitions: 5, AscensionDeltas: [new(9, 1)])]),
            new("execution", [new(PrototypeEnemyEffectKind.DamagePlayer, 15,
                AscensionDeltas: [new(9, 3)])]),
            new("enrage", [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                PowerId: "proto.power.strength")])
        ], HpAscensionDeltas: [new(8, 19)],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("strings",
        [
            new("strings", PrototypeEnemyAiStateKind.Move, MoveIndex: 0, NextStateId: "mine"),
            new("mine", PrototypeEnemyAiStateKind.Move, MoveIndex: 1, NextStateId: "amalgam_branch"),
            new("amalgam_branch", PrototypeEnemyAiStateKind.Conditional,
                ConditionalBranches:
                [
                    new("bright", PrototypeEnemyAiConditionKind.HasLivingEnemyId, AmalgamId),
                    new("heads", PrototypeEnemyAiConditionKind.LacksLivingEnemyId, AmalgamId)
                ]),
            new("bright", PrototypeEnemyAiStateKind.Move, MoveIndex: 2, NextStateId: "amalgam_branch"),
            new("heads", PrototypeEnemyAiStateKind.Move, MoveIndex: 3, NextStateId: "execution"),
            new("execution", PrototypeEnemyAiStateKind.Move, MoveIndex: 4, NextStateId: "enrage"),
            new("enrage", PrototypeEnemyAiStateKind.Move, MoveIndex: 5, NextStateId: "heads")
        ])),
        new(TestSubjectId, "Test Subject", 100, 0,
        [
            new("bite", [new(PrototypeEnemyEffectKind.DamagePlayer, 20,
                AscensionDeltas: [new(9, 2)])]),
            new("skull_bash",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 14,
                    AscensionDeltas: [new(9, 2)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                    PowerId: "proto.power.vulnerable")
            ]),
            new("multi_claw", [new(PrototypeEnemyEffectKind.DamagePlayer, 10,
                Repetitions: 3, AscensionDeltas: [new(9, 1)])]),
            new("respawn", [new(PrototypeEnemyEffectKind.ReviveTestSubject, 0)]),
            new("lacerate", [new(PrototypeEnemyEffectKind.DamagePlayer, 10,
                Repetitions: 3, AscensionDeltas: [new(9, 1)])]),
            new("big_pounce", [new(PrototypeEnemyEffectKind.DamagePlayer, 45)]),
            new("burning_growl",
            [
                new(PrototypeEnemyEffectKind.AddCardsToDiscard, 3,
                    CardId: PrototypeNativeGloryElites.BurnId,
                    AscensionDeltas: [new(9, 2)]),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                    PowerId: "proto.power.strength",
                    AscensionDeltas: [new(9, 1)])
            ])
        ], HpAscensionDeltas: [new(8, 11)],
        StartingPowers: [new(AdaptableId, 1),
            new(EnrageId, 2, AscensionDeltas: [new(9, 1)])],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("bite",
        [
            new("bite", PrototypeEnemyAiStateKind.Move, MoveIndex: 0, NextStateId: "bash"),
            new("bash", PrototypeEnemyAiStateKind.Move, MoveIndex: 1, NextStateId: "bite"),
            new("respawn", PrototypeEnemyAiStateKind.Move, MoveIndex: 3, NextStateId: "claw"),
            new("claw", PrototypeEnemyAiStateKind.Move, MoveIndex: 2, NextStateId: "claw"),
            new("lacerate", PrototypeEnemyAiStateKind.Move, MoveIndex: 4, NextStateId: "pounce"),
            new("pounce", PrototypeEnemyAiStateKind.Move, MoveIndex: 5, NextStateId: "growl"),
            new("growl", PrototypeEnemyAiStateKind.Move, MoveIndex: 6, NextStateId: "lacerate")
        ])),
        new(AeonglassId, "Aeonglass", 512, 0,
        [
            new("ebb",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 22,
                    AscensionDeltas: [new(9, 4)]),
                new(PrototypeEnemyEffectKind.GainBlock, 33)
            ]),
            new("eye_lasers", [new(PrototypeEnemyEffectKind.DamagePlayer, 11,
                Repetitions: 2, AscensionDeltas: [new(9, 1)])]),
            new("increasing_intensity",
            [
                new(PrototypeEnemyEffectKind.IntensifyWither, 0),
                new(PrototypeEnemyEffectKind.AddCardsToDiscard, 1,
                    CardId: WitherId, AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                    PowerId: "proto.power.strength",
                    AscensionDeltas: [new(9, 1)],
                    ExtraAmountPerPriorMoveUse: 1)
            ])
        ], HpAscensionDeltas: [new(8, 23)],
        StartingPowers:
        [
            new(WitheringPresenceId, 6),
            new("proto.power.artifact", 3)
        ])
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(KnightsEncounterId, PrototypeRoomType.Elite,
            [FlailKnightId, SpectralKnightId, MagiKnightId],
            MinAct: 3, MaxAct: 3, Weight: 0,
            Formation:
            [
                new(FlailKnightId, 0, "first"),
                new(SpectralKnightId, 1, "second"),
                new(MagiKnightId, 2, "third")
            ]),
        new(QueenEncounterId, PrototypeRoomType.Boss,
            [AmalgamId, QueenId], MinAct: 3, MaxAct: 3, Weight: 0,
            Formation:
            [
                new(AmalgamId, 0, "amalgam",
                    LeaderFormationPosition: 1),
                new(QueenId, 1, "queen")
            ]),
        new(TestSubjectEncounterId, PrototypeRoomType.Boss,
            [TestSubjectId], MinAct: 3, MaxAct: 3, Weight: 0),
        new(AeonglassEncounterId, PrototypeRoomType.Boss,
            [AeonglassId], MinAct: 3, MaxAct: 3, Weight: 0)
    ];
}
