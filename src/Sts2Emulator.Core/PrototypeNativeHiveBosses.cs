namespace Sts2Emulator.Core;

/// <summary>
/// The three pinned v0.111 Hive bosses, forced-combat only. Source:
/// Flaaax/STS2-source @706ef1c9fa2219220065849fd5265328607ece20.
/// Knowledge Demon's mandatory CurseOfKnowledge selection deliberately
/// fails closed until the enemy-owned choice pipeline is implemented.
/// </summary>
public static class PrototypeNativeHiveBosses
{
    public const string KaiserEncounterId =
        "proto.native.hive.encounter.kaiser_crab_boss";
    public const string KnowledgeEncounterId =
        "proto.native.hive.encounter.knowledge_demon_boss";
    public const string InsatiableEncounterId =
        "proto.native.hive.encounter.the_insatiable_boss";
    public const string CrusherId = "proto.native.hive.crusher";
    public const string RocketId = "proto.native.hive.rocket";
    public const string KnowledgeId = "proto.native.hive.knowledge_demon";
    public const string InsatiableId = "proto.native.hive.the_insatiable";
    public const string BackLeftId = "proto.native.hive.back_attack_left";
    public const string BackRightId = "proto.native.hive.back_attack_right";
    public const string RageId = "proto.native.hive.crab_rage";
    public const string SurroundedId = "proto.native.hive.surrounded";
    public const string SandpitId = "proto.native.hive.sandpit";
    public const string FranticEscapeId = "proto.native.hive.frantic_escape";

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new(BackLeftId, "Back Attack Left", 0, [], DoesNotStack: true),
        new(BackRightId, "Back Attack Right", 0, [], DoesNotStack: true),
        new(RageId, "Crab Rage", 0, [], DoesNotStack: true,
            AllyDeathStrengthPerStack: 6, AllyDeathBlockPerStack: 99,
            ConsumeOnAllyDeath: true),
        new(SurroundedId, "Surrounded", 0, [], IsDebuff: true,
            DoesNotStack: true),
        // Native Sandpit sits on The Insatiable but targets the player.
        // It decrements after every enemy-side start and kills its target
        // when it expires. Frantic Escape increases the same counter.
        new(SandpitId, "Sandpit", 0, [], DoesNotStack: false,
            EnemyStacksDecayAtSideTurnStartAfterFirst: 1)
    ];

    public static PrototypeCardDefinition[] Cards { get; } =
    [
        new(FranticEscapeId, "Frantic Escape", 1,
            PrototypeCardTarget.None, [],
            Rarity: PrototypeCardRarity.Status, Type: PrototypeCardType.Status,
            RewardEligible: false, MaxUpgradeLevel: 0)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // Crusher: Thrash -> Enlarging Strike -> Bug Sting ->
        // Adapt -> Guarded Strike -> repeat. Crab Rage fires once when
        // Rocket dies; BackAttackLeft interacts with Surrounded facing.
        new(CrusherId, "Crusher", 209, 0,
        [
            new("thrash", [new(PrototypeEnemyEffectKind.DamagePlayer, 12,
                AscensionDeltas: [new(9, 2)])]),
            new("enlarging_strike",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 4)]),
            new("bug_sting",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 6,
                    Repetitions: 2, AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                    PowerId: "proto.power.weak"),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                    PowerId: "proto.power.frail")
            ]),
            new("adapt",
                [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                    PowerId: "proto.power.strength",
                    AscensionDeltas: [new(9, 1)])]),
            new("guarded_strike",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 12,
                    AscensionDeltas: [new(9, 2)]),
                new(PrototypeEnemyEffectKind.GainBlock, 18)
            ])
        ],
        HpAscensionDeltas: [new(8, 10)],
        StartingPowers: [new(BackLeftId, 1), new(RageId, 1)]),
        // Rocket: Reticle -> Beam -> Charge -> Laser -> Recharge.
        new(RocketId, "Rocket", 199, 0,
        [
            new("targeting_reticle",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 3,
                    AscensionDeltas: [new(9, 1)])]),
            new("precision_beam",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 18,
                    AscensionDeltas: [new(9, 2)])]),
            new("charge_up",
                [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                    PowerId: "proto.power.strength",
                    AscensionDeltas: [new(9, 1)])]),
            new("laser",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 31,
                    AscensionDeltas: [new(9, 4)])]),
            new("recharge", [])
        ],
        HpAscensionDeltas: [new(8, 10)],
        StartingPowers: [new(BackRightId, 1), new(RageId, 1)]),
        // The opening CurseOfKnowledge needs a blocking choice between
        // Disintegration and a level-specific negative card. Native
        // attack/heal loop is executable in isolated models, but curse
        // explicitly rejects rather than silently picking for the player.
        new(KnowledgeId, "Knowledge Demon", 379, 0,
        [
            new("curse_of_knowledge",
                [new(PrototypeEnemyEffectKind.RequireNativeChoice, 0)]),
            new("slap",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 17,
                    AscensionDeltas: [new(9, 1)])]),
            new("knowledge_overwhelming",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 8,
                    Repetitions: 3, AscensionDeltas: [new(9, 1)])]),
            new("ponder",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 11,
                    AscensionDeltas: [new(9, 2)]),
                new(PrototypeEnemyEffectKind.HealSelf, 30),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                    PowerId: "proto.power.strength",
                    AscensionDeltas: [new(9, 1)])
            ])
        ],
        HpAscensionDeltas: [new(8, 20)],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("curse",
        [
            new("curse", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "slap"),
            new("slap", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "overwhelming"),
            new("overwhelming", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "ponder"),
            new("ponder", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 3, NextStateId: "curse_choice"),
            new("curse_choice", PrototypeEnemyAiStateKind.Conditional,
                ConditionalBranches:
                [
                    new("curse",
                        PrototypeEnemyAiConditionKind.MoveUsedFewerThan,
                        "curse_of_knowledge:3"),
                    new("slap",
                        PrototypeEnemyAiConditionKind.MoveUsedAtLeast,
                        "curse_of_knowledge:3")
                ])
        ])),
        // The Insatiable: Liquify once, then Thrash -> Bite ->
        // Salivate -> Thrash2 -> loop at Thrash.
        new(InsatiableId, "The Insatiable", 321, 0,
        [
            new("liquify_ground",
            [
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 4,
                    PowerId: SandpitId),
                new(PrototypeEnemyEffectKind.AddCardsToRandomDraw, 3,
                    CardId: FranticEscapeId),
                new(PrototypeEnemyEffectKind.AddCardsToRandomDiscard, 3,
                    CardId: FranticEscapeId)
            ]),
            new("thrash",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 8,
                    Repetitions: 2, AscensionDeltas: [new(9, 1)])]),
            new("lunging_bite",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 28,
                    AscensionDeltas: [new(9, 3)])]),
            new("salivate",
                [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                    PowerId: "proto.power.strength",
                    AscensionDeltas: [new(9, 1)])]),
            new("thrash_2",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 8,
                    Repetitions: 2, AscensionDeltas: [new(9, 1)])])
        ],
        MoveLoopStartIndex: 1,
        HpAscensionDeltas: [new(8, 20)])
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(KaiserEncounterId, PrototypeRoomType.Boss,
            [CrusherId, RocketId], MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new(CrusherId, 0, "crusher"),
                new(RocketId, 1, "rocket")
            ]),
        new(KnowledgeEncounterId, PrototypeRoomType.Boss,
            [KnowledgeId], MinAct: 2, MaxAct: 2, Weight: 0),
        new(InsatiableEncounterId, PrototypeRoomType.Boss,
            [InsatiableId], MinAct: 2, MaxAct: 2, Weight: 0)
    ];
}
