namespace Sts2Emulator.Core;

/// <summary>
/// First pinned v0.111.0 Hive/Glory executable content slice.
///
/// These mechanics are available in the typed registry for deterministic
/// testing, but native Act 2/3 route selection is NOT enabled. In particular,
/// do not silently replace unimplemented later-act encounters with prototype
/// enemies. See Hive.GenerateAllEncounters and Glory.GenerateAllEncounters.
/// </summary>
public static class PrototypeNativeLaterActs
{
    public const string HiveBugslayerId = "proto.native.hive.bugslayer";
    public const string ExterminateId = "proto.native.hive.exterminate";
    public const string SquashId = "proto.native.hive.squash";
    public const string GlorySculptorWeakId =
        "proto.native.glory.encounter.devoted_sculptor_weak";

    // Pinned Hive.cs (Act.Index = 1); this is a complete native encounter
    // NAME inventory, not an implementation/integration declaration.
    public static string[] HiveEncounterNames { get; } =
    [
        "BowlbugsNormal", "BowlbugsWeak", "ChompersNormal",
        "DecimillipedeElite", "EntomancerElite", "ExoskeletonsNormal",
        "ExoskeletonsWeak", "HunterKillerNormal", "KaiserCrabBoss",
        "InfestedPrismsElite", "KnowledgeDemonBoss",
        "LouseProgenitorNormal", "MytesNormal", "OvicopterNormal",
        "SlumberingBeetleNormal", "SpinyToadNormal",
        "TheInsatiableBoss", "TheObscuraNormal",
        "ThievingHopperWeak", "TunnelerWeak"
    ];

    // Pinned Glory.cs (Act.Index = 2).
    public static string[] GloryEncounterNames { get; } =
    [
        "AxebotsNormal", "ConstructMenagerieNormal",
        "DevotedSculptorWeak", "AeonglassBoss", "FabricatorNormal",
        "FrogKnightNormal", "GlobeHeadNormal", "KnightsElite",
        "MechaKnightElite", "OwlMagistrateNormal", "QueenBoss",
        "ScrollsOfBitingNormal", "ScrollsOfBitingWeak",
        "SlimedBerserkerNormal", "SoulNexusElite",
        "TestSubjectBoss", "TheLostAndForgottenNormal",
        "TurretOperatorWeak"
    ];

    public static PrototypeCardDefinition[] Cards { get; } =
    [
        // Source: Bugslayer.cs, Exterminate.cs, Squash.cs.
        // Event-only acquisitions; neither belongs in Silent reward pools.
        new(ExterminateId, "Exterminate", 1, PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.DamageEnemy, 3, 1,
                Target: PrototypeEffectTarget.AllEnemies,
                Repetitions: 4)],
            Rarity: PrototypeCardRarity.Event,
            RewardEligible: false, Type: PrototypeCardType.Attack),
        new(SquashId, "Squash", 1, PrototypeCardTarget.Enemy,
            [
                new(PrototypeCombatEffectKind.DamageEnemy, 10, 2),
                new(PrototypeCombatEffectKind.ApplyEnemyPower, 2, 1,
                    PowerId: "proto.power.vulnerable")
            ],
            Rarity: PrototypeCardRarity.Event,
            RewardEligible: false, Type: PrototypeCardType.Attack)
    ];

    public static PrototypeEventDefinition[] Events { get; } =
    [
        new(HiveBugslayerId, "Bugslayer",
            [
                new("extermination", "Take Exterminate",
                    [new(PrototypeRunEffectKind.AddCard,
                        CardId: ExterminateId)]),
                new("squash", "Take Squash",
                    [new(PrototypeRunEffectKind.AddCard,
                        CardId: SquashId)])
            ],
            MinAct: 2, MaxAct: 2,
            // Registered for explicit event-step tests only. Native Hive
            // room/event bags are not yet implemented; never mix this
            // event into the legacy prototype Act 2 random selector.
            Weight: 0)
    ];

    // Source: ImbalancedPower.cs and HardToKillPower.cs. Imbalanced
    // reacts to a fully blocked attack; Hard to Kill caps HP loss PER hit,
    // without consuming stacks or limiting damage across a whole turn.
    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new("proto.native.hive.imbalanced", "Imbalanced", 0, [],
            DoesNotStack: true),
        new("proto.native.hive.hard_to_kill", "Hard to Kill", 0, [],
            DoesNotStack: true, EnemyHpLossCapPerTrigger: 9),
        new("proto.native.hive.burrowed", "Burrowed", 0, [],
            DoesNotStack: true, PreventsEnemyBlockClear: true),
        // Escape Artist is a display countdown; Swipe is an instanced
        // carrier of the stolen persistent card (stored on enemy state).
        new("proto.native.hive.escape_artist", "Escape Artist", 0, [],
            DoesNotStack: true, EnemyStacksDecayAtSideTurnEnd: 1),
        new("proto.native.hive.swipe", "Swipe", 0, [],
            IsInstanced: true),
        new("proto.native.hive.flutter", "Flutter", 0, [],
            DoesNotStack: true)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // Source: BowlbugEgg.cs. Bite applies player damage THEN
        // self-block, and repeats with no RNG decision.
        new("proto.native.hive.bowlbug_egg", "Bowlbug Egg", 22, 0,
            [
                new("bite",
                    [
                        new(PrototypeEnemyEffectKind.DamagePlayer, 7,
                            AscensionDeltas: [new(9, 1)]),
                        new(PrototypeEnemyEffectKind.GainBlock, 7,
                            AscensionDeltas: [new(9, 1)])
                    ])
            ],
            MinHp: 21,
            HpAscensionDeltas: [new(8, 2)]),
        // Source: BowlbugNectar.cs. Thrash -> Strength 15/16 ->
        // Thrash forever, rather than repeating the Strength gain.
        new("proto.native.hive.bowlbug_nectar", "Bowlbug Nectar", 38, 0,
            [
                new("thrash", [new(PrototypeEnemyEffectKind.DamagePlayer, 3)]),
                new("buff",
                    [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 15,
                        PowerId: "proto.power.strength",
                        AscensionDeltas: [new(9, 1)])]),
                new("thrash_2",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 3)])
            ],
            MoveLoopStartIndex: 2,
            MinHp: 35,
            HpAscensionDeltas: [new(8, 1)]),
        // BowlbugRock.cs: its Imbalanced power marks a fully blocked
        // Headbutt, forcing one Dizzy turn. The flag clears on Dizzy.
        new("proto.native.hive.bowlbug_rock", "Bowlbug Rock", 48, 0,
            [
                new("headbutt",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 15,
                        AscensionDeltas: [new(9, 1)])]),
                new("dizzy", [])
            ],
            MinHp: 45,
            HpAscensionDeltas: [new(8, 1)],
            StartingPowers: [new("proto.native.hive.imbalanced", 1)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("headbutt",
                [
                    new("headbutt", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "post_headbutt"),
                    new("dizzy", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "headbutt"),
                    new("post_headbutt",
                        PrototypeEnemyAiStateKind.Conditional,
                        ConditionalBranches:
                        [
                            new("dizzy",
                                PrototypeEnemyAiConditionKind.IsOffBalance),
                            new("headbutt",
                                PrototypeEnemyAiConditionKind.IsNotOffBalance)
                        ])
                ])),
        // BowlbugSilk.cs: Toxic Spit opens and alternates with Thrash.
        new("proto.native.hive.bowlbug_silk", "Bowlbug Silk", 43, 0,
            [
                new("toxic_spit",
                    [new(PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                        PowerId: "proto.power.weak")]),
                new("thrash",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 4,
                        Repetitions: 2,
                        AscensionDeltas: [new(9, 1)])])
            ],
            MinHp: 40,
            HpAscensionDeltas: [new(8, 1)],
            OpeningMoveIndex: 0,
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("spit",
                [
                    new("spit", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "thrash"),
                    new("thrash", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "spit")
                ])),
        // Exoskeleton.cs: its native opening is slot-dependent, followed
        // by a random nonrepeating Skitter/Mandibles branch.
        new("proto.native.hive.exoskeleton", "Exoskeleton", 28, 0,
            [
                new("skitter", [new(PrototypeEnemyEffectKind.DamagePlayer,
                    1, Repetitions: 3,
                    RepetitionAscensionDeltas: [new(9, 1)])]),
                new("mandibles", [new(PrototypeEnemyEffectKind.DamagePlayer,
                    8, AscensionDeltas: [new(9, 1)])]),
                new("enrage", [new(PrototypeEnemyEffectKind.ApplyEnemyPower,
                    2, PowerId: "proto.power.strength")])
            ],
            MinHp: 24,
            HpAscensionDeltas: [new(8, 2)],
            StartingPowers: [new("proto.native.hive.hard_to_kill", 9)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("init",
                [
                    new("init", PrototypeEnemyAiStateKind.Conditional,
                        ConditionalBranches:
                        [
                            new("skitter",
                                PrototypeEnemyAiConditionKind.SlotNameEquals,
                                "first"),
                            new("mandibles",
                                PrototypeEnemyAiConditionKind.SlotNameEquals,
                                "second"),
                            new("enrage",
                                PrototypeEnemyAiConditionKind.SlotNameEquals,
                                "third"),
                            // Native ExoskeletonsNormal adds a fourth
                            // slot starting at RAND. Unlike the weak
                            // encounter this first move is stochastic.
                            new("random",
                                PrototypeEnemyAiConditionKind.SlotNameEquals,
                                "fourth")
                        ]),
                    new("skitter", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "random"),
                    new("mandibles", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "enrage"),
                    new("enrage", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "random"),
                    new("random", PrototypeEnemyAiStateKind.Random,
                        Branches:
                        [
                            new("skitter", RepeatRule:
                                PrototypeEnemyAiRepeatRule.CannotRepeat),
                            new("mandibles", RepeatRule:
                                PrototypeEnemyAiRepeatRule.CannotRepeat)
                        ])
                ])),
        // Tunneler.cs: Bite -> Burrow (32/37 Block, retains Block)
        // -> Below forever. Breaking its Block while burrowed forces
        // a stunned Dizzy recovery action, then returns to Bite.
        new("proto.native.hive.tunneler", "Tunneler", 87, 0,
            [
                new("bite", [new(PrototypeEnemyEffectKind.DamagePlayer,
                    13, AscensionDeltas: [new(9, 2)])]),
                new("burrow",
                    [
                        new(PrototypeEnemyEffectKind.ApplyEnemyPower, 1,
                            PowerId: "proto.native.hive.burrowed"),
                        new(PrototypeEnemyEffectKind.GainBlock, 32,
                            AscensionDeltas: [new(8, 5)])
                    ]),
                new("below", [new(PrototypeEnemyEffectKind.DamagePlayer,
                    23, AscensionDeltas: [new(9, 3)])]),
                new("dizzy", [])
            ],
            HpAscensionDeltas: [new(8, 5)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("bite",
                [
                    new("bite", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "burrow"),
                    new("burrow", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "below"),
                    new("below", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "below"),
                    new("dizzy", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 3, NextStateId: "bite")
                ])),
        // ThievingHopper.cs: thievery steals the highest-priority
        // card still in draw/discard, then attacks. On death Swipe
        // restores that exact deck version; escape forfeits it.
        new("proto.native.hive.thieving_hopper", "Thieving Hopper",
            79, 0,
            [
                new("thievery",
                [
                    new(PrototypeEnemyEffectKind.StealPlayerCard, 1),
                    new(PrototypeEnemyEffectKind.DamagePlayer, 17,
                        AscensionDeltas: [new(9, 2)])
                ]),
                new("flutter", [new(
                    PrototypeEnemyEffectKind.ApplyEnemyPower, 5,
                    PowerId: "proto.native.hive.flutter")]),
                new("hat_trick", [new(
                    PrototypeEnemyEffectKind.DamagePlayer, 21,
                    AscensionDeltas: [new(9, 2)])]),
                new("nab", [new(
                    PrototypeEnemyEffectKind.DamagePlayer, 14,
                    AscensionDeltas: [new(9, 2)])]),
                new("escape", [new(
                    PrototypeEnemyEffectKind.EscapeEnemy, 0)]),
                new("stunned", [])
            ],
            HpAscensionDeltas: [new(8, 5)],
            StartingPowers: [new("proto.native.hive.escape_artist", 5)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("thievery",
                [
                    new("thievery", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "flutter"),
                    new("flutter", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "hat_trick"),
                    new("hat_trick", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "nab"),
                    new("nab", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 3, NextStateId: "escape"),
                    new("escape", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 4, NextStateId: "escape"),
                    new("stunned", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 5, NextStateId: "hat_trick")
                ])),
        // Source: DevotedSculptor.cs. An opening +9 Ritual, then
        // 12/15-damage Savage repeatedly; HP 162 (A8 172).
        new("proto.native.glory.devoted_sculptor", "Devoted Sculptor",
            162, 0,
            [
                new("forbidden_incantation",
                    [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 9,
                        PowerId: "proto.power.ritual")]),
                new("savage",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 12,
                        AscensionDeltas: [new(9, 3)])])
            ],
            MoveLoopStartIndex: 1,
            HpAscensionDeltas: [new(8, 10)]),
        // Source: TurretOperator.cs. Its weak formation with Living
        // Shield and Rampart is in PrototypeNativeGloryWeak, still
        // forced-test-only until native Glory routing is implemented.
        new("proto.native.glory.turret_operator", "Turret Operator",
            41, 0,
            [
                new("unload", [new(PrototypeEnemyEffectKind.DamagePlayer,
                    3, Repetitions: 5,
                    AscensionDeltas: [new(9, 1)])]),
                new("unload_2", [new(PrototypeEnemyEffectKind.DamagePlayer,
                    3, Repetitions: 5,
                    AscensionDeltas: [new(9, 1)])]),
                new("reload", [new(PrototypeEnemyEffectKind.ApplyEnemyPower,
                    1, PowerId: "proto.power.strength")])
            ],
            HpAscensionDeltas: [new(8, 10)])
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        // BowlbugsWeak.cs: fixed Rock in "odd" and a single Egg or
        // Nectar in "even". Selection is on the combat RNG substream.
        new("proto.native.hive.encounter.bowlbugs_weak",
            PrototypeRoomType.Combat,
            ["proto.native.hive.bowlbug_rock"],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new("proto.native.hive.bowlbug_rock", 0, "odd")
            ],
            SelectionGroups:
            [
                new(
                    ["proto.native.hive.bowlbug_egg",
                     "proto.native.hive.bowlbug_nectar"],
                    [1], SlotNames: ["even"])
            ]),
        // ExoskeletonsWeak.cs: three named slots define distinct opening
        // intents, including a guaranteed third-slot Enrage.
        new("proto.native.hive.encounter.exoskeletons_weak",
            PrototypeRoomType.Combat,
            [
                "proto.native.hive.exoskeleton",
                "proto.native.hive.exoskeleton",
                "proto.native.hive.exoskeleton"
            ],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new("proto.native.hive.exoskeleton", 0, "first"),
                new("proto.native.hive.exoskeleton", 1, "second"),
                new("proto.native.hive.exoskeleton", 2, "third")
            ]),
        // TunnelerWeak.cs is exactly one Tunneler. The Burrowed
        // power's persistence and block-break interrupt are supported.
        new("proto.native.hive.encounter.tunneler_weak",
            PrototypeRoomType.Combat,
            ["proto.native.hive.tunneler"],
            MinAct: 2, MaxAct: 2, Weight: 0),
        new("proto.native.hive.encounter.thieving_hopper_weak",
            PrototypeRoomType.Combat,
            ["proto.native.hive.thieving_hopper"],
            MinAct: 2, MaxAct: 2, Weight: 0),
        // Source: DevotedSculptorWeak.cs. Weight zero intentionally
        // prevents an incomplete native Glory map from sampling this
        // via the legacy prototype encounter selector. Forced/synthetic
        // deterministic combat tests can still exercise this formation.
        new(GlorySculptorWeakId, PrototypeRoomType.Combat,
            ["proto.native.glory.devoted_sculptor"],
            MinAct: 3, MaxAct: 3, Weight: 0)
    ];
}
