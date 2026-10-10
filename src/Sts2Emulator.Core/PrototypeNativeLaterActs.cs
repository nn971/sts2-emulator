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
            MinAct: 2, MaxAct: 2)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // Source: BowlbugEgg.cs. Bite applies player damage THEN
        // self-block, and repeats with no RNG decision.
        new("proto.native.hive.bowlbug_egg", "Bowlbug Egg", 24, 0,
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
        new("proto.native.hive.bowlbug_nectar", "Bowlbug Nectar", 39, 0,
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
        // Source: TurretOperator.cs. Its native Weak encounter includes
        // a Living Shield, whose Rampart/ally-death interactions must be
        // implemented before the encounter is made available.
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
        // Source: DevotedSculptorWeak.cs. Weight zero intentionally
        // prevents an incomplete native Glory map from sampling this
        // via the legacy prototype encounter selector. Forced/synthetic
        // deterministic combat tests can still exercise this formation.
        new(GlorySculptorWeakId, PrototypeRoomType.Combat,
            ["proto.native.glory.devoted_sculptor"],
            MinAct: 3, MaxAct: 3, Weight: 0)
    ];
}
