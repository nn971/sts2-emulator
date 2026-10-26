namespace Sts2Emulator.Core;

/// <summary>
/// v0.111.0 Underdocks single-player Act 1 content inventory.
/// All 20 native encounter registrations are supported by the experimental
/// Underdocks selector. Native pre-generated queue ordering, encounter-tag
/// filtering and RNG draw parity remain separate fidelity work.
/// </summary>
public static class PrototypeNativeUnderdocks
{
    public const string GenerationProfileId =
        "native-underdocks-map-structure-v0.111.0-v1";
    public const string BossEncounterIndexSiteId =
        "combat.encounter.underdocks.boss.index_draw";

    // Source: Underdocks.GenerateAllEncounters() and IsWeak.
    public static string[] NativeWeakEncounterIds =>
        (string[])NativeWeakEncounterIdValues.Clone();

    private static readonly string[] NativeWeakEncounterIdValues =
    [
        "proto.encounter.corpse_slugs_weak",
        "proto.encounter.seapunk_weak",
        "proto.encounter.sludge_spinner_weak",
        "proto.encounter.toadpoles_weak"
    ];

    public static string[] NativeNormalEncounterIds =>
        (string[])NativeNormalEncounterIdValues.Clone();

    private static readonly string[] NativeNormalEncounterIdValues =
    [
        "proto.encounter.corpse_slugs_normal",
        "proto.encounter.cultists_normal",
        "proto.encounter.fossil_stalker_normal",
        "proto.encounter.gremlin_merc_normal",
        "proto.encounter.haunted_ship_normal",
        "proto.encounter.living_fog_normal",
        "proto.encounter.punch_construct_normal",
        "proto.encounter.seapunk_normal",
        "proto.encounter.sewer_clam_normal",
        "proto.encounter.two_tailed_rats_normal"
    ];

    public static string[] NativeEliteEncounterIds =>
        (string[])NativeEliteEncounterIdValues.Clone();

    private static readonly string[] NativeEliteEncounterIdValues =
    [
        "proto.encounter.phantasmal_gardeners_elite",
        "proto.encounter.skulking_colony_elite",
        "proto.encounter.terror_eel_elite"
    ];

    public static string[] NativeBossEncounterIds =>
        (string[])NativeBossEncounterIdValues.Clone();

    private static readonly string[] NativeBossEncounterIdValues =
    [
        "proto.encounter.waterfall_giant_boss",
        "proto.encounter.soul_fysh_boss",
        "proto.encounter.lagavulin_matriarch_boss"
    ];

    public static string[] SupportedWeakEncounterIds =>
        (string[])SupportedWeakEncounterIdValues.Clone();

    private static readonly string[] SupportedWeakEncounterIdValues =
    [
        "proto.encounter.corpse_slugs_weak",
        "proto.encounter.seapunk_weak",
        "proto.encounter.sludge_spinner_weak",
        "proto.encounter.toadpoles_weak"
    ];

    // Full source registration set. The Act 1 route schedules 12 normal
    // selections, so this bag refills after its ten distinct encounters.
    public static string[] SupportedNormalEncounterIds =>
        (string[])SupportedNormalEncounterIdValues.Clone();

    private static readonly string[] SupportedNormalEncounterIdValues =
    [
        "proto.encounter.corpse_slugs_normal",
        "proto.encounter.punch_construct_normal",
        "proto.encounter.cultists_normal",
        "proto.encounter.fossil_stalker_normal",
        "proto.encounter.seapunk_normal",
        "proto.encounter.haunted_ship_normal",
        "proto.encounter.sewer_clam_normal",
        "proto.encounter.living_fog_normal",
        "proto.encounter.two_tailed_rats_normal",
        "proto.encounter.gremlin_merc_normal"
    ];

    public static string[] SupportedEliteEncounterIds =>
        (string[])SupportedEliteEncounterIdValues.Clone();

    private static readonly string[] SupportedEliteEncounterIdValues =
    [
        "proto.encounter.skulking_colony_elite",
        "proto.encounter.phantasmal_gardeners_elite",
        "proto.encounter.terror_eel_elite"
    ];

    public static string[] SupportedBossEncounterIds =>
        (string[])SupportedBossEncounterIdValues.Clone();

    private static readonly string[] SupportedBossEncounterIdValues =
    [
        "proto.encounter.soul_fysh_boss",
        "proto.encounter.lagavulin_matriarch_boss",
        "proto.encounter.waterfall_giant_boss"
    ];

    public static PrototypePowerDefinition[] Powers => PowerDefinitions
        .Select(PrototypeDefinitionCopiesV111.ClonePowerDefinition)
        .ToArray();

    private static readonly PrototypePowerDefinition[] PowerDefinitions =
    [
        // Steam Eruption survives the first lethal blow, replacing it
        // with a protected telegraph followed by a lethal explosion.
        new(
            "proto.power.steam_eruption",
            "Steam Eruption",
            BlockBonusPerStack: 0,
            Triggers: [],
            LastStandAiStateId: "about_to_blow",
            LastStandHp: 999999999),
        // Native Lagavulin Matriarch wakes either on the first
        // unblocked attack hit or after three asleep enemy turns. Waking
        // removes Plating; premature attack wake stuns one upcoming
        // action and preserves Slash as the first real intent.
        new(
            "proto.power.asleep",
            "Asleep",
            BlockBonusPerStack: 0,
            Triggers: [],
            EnemyStacksDecayAtSideTurnEnd: 1,
            WakeOwnerOnUnblockedAttackDamage: true,
            OwnerAiStateOnWake: "slash",
            RemoveOwnerPowersOnWake:
                ["proto.power.asleep", "proto.power.plating"],
            StunOwnerOnWake: true,
            OwnerAiStateOnPowerExpiry: "slash",
            RemoveOwnerPowersOnPowerExpiry: ["proto.power.plating"]),
        // Intangible limits each HP-loss event to one and expires
        // once per enemy-side turn, including the turn it is applied.
        new(
            "proto.power.enemy_intangible",
            "Intangible",
            BlockBonusPerStack: 0,
            Triggers: [],
            EnemyHpLossCapPerTrigger: 1,
            EnemyStacksDecayAtSideTurnEnd: 1),

        // Native RavenousPower.AfterDeath: when another ally dies, gain
        // Strength equal to Ravenous stacks and replace the next action
        // with one stunned turn. Death of the owner never triggers itself.
        new(
            "proto.power.ravenous",
            "Ravenous",
            BlockBonusPerStack: 0,
            Triggers: [],
            AllyDeathStrengthPerStack: 1,
            StunOnAllyDeath: true),
        // Ritual: gain Strength after each own side turn, except the turn
        // on which the enemy first applies it.
        new(
            "proto.power.ritual",
            "Ritual",
            BlockBonusPerStack: 0,
            Triggers: [],
            EnemyStrengthAtSideTurnEndPerStack: 1,
            PlayerStrengthAtTurnEndPerStack: 1,
            SkipInitialEnemySideTurnEnd: true),
        new(
            "proto.power.radiance",
            "Radiance",
            BlockBonusPerStack: 0,
            Triggers: [],
            EnergyBonusWhileActive: 1,
            DecrementAfterEnergyReset: true),
        // Suck: Strength per unblocked powered attack hit, including
        // multiple hits in a single monster attack command.
        new(
            "proto.power.suck",
            "Suck",
            BlockBonusPerStack: 0,
            Triggers: [],
            StrengthPerUnblockedAttackHitPerStack: 1),
        // Single-player Plating: begin with Block, gain remaining stacks
        // as Block at each enemy turn end, lose 1 stack at the beginning
        // of enemy turns after the first.
        new(
            "proto.power.plating",
            "Plating",
            BlockBonusPerStack: 0,
            Triggers: [],
            EnemyStartingBlockPerStack: 1,
            EnemyBlockAtSideTurnEndPerStack: 1,
            EnemyStacksDecayAtSideTurnStartAfterFirst: 1,
            PlayerBlockAtTurnEndPerStack: 1,
            DecrementAfterPlayerTurnStart: true),
        // Smoggy: after a Skill play, afflict all otherwise-unafflicted
        // Skills with Smog until the player's turn ends. Such Skills
        // cannot be played while the debuff is active.
        new(
            "proto.power.smoggy",
            "Smoggy",
            BlockBonusPerStack: 0,
            Triggers: [],
            IsDebuff: true,
            DoesNotStack: true,
            SkillPlayAffliction: PrototypeCardAfflictionKind.Smog,
            BlockPlayOfMatchingAffliction: true,
            ClearMatchingAfflictionAtPlayerTurnEnd: true),
        new("proto.power.surprise", "Surprise",
            BlockBonusPerStack: 0, Triggers: []),
        new("proto.power.thievery", "Thievery",
            BlockBonusPerStack: 0, Triggers: [], IsInstanced: true),
        new("proto.power.heist", "Heist",
            BlockBonusPerStack: 0, Triggers: [], IsInstanced: true),
        new(
            "proto.power.hardened_shell",
            "Hardened Shell",
            BlockBonusPerStack: 0,
            Triggers: [],
            EnemyHpLossLimitedPerSideTurnByStacks: true),
        new(
            "proto.power.skittish",
            "Skittish",
            BlockBonusPerStack: 0,
            Triggers: [],
            EnemyBlockAfterFirstUnblockedCardAttackPerStack: 1),
        new(
            "proto.power.shriek",
            "Shriek",
            BlockBonusPerStack: 0,
            Triggers: [],
            IsDebuff: true,
            AllowNegative: true,
            TriggerOwnerAtHpAtOrBelowStacks: true,
            OwnerAiStateOnHpThresholdTrigger: "stun"),
        new(
            "proto.power.vigor",
            "Vigor",
            BlockBonusPerStack: 0,
            Triggers: [],
            EnemyAttackDamageBonusPerStack: 1,
            PlayerAttackDamageBonusPerStack: 1,
            ConsumeAfterEnemyAttack: true)
    ];

    public static PrototypeEnemyDefinition[] Enemies => EnemyDefinitions
        .Select(PrototypeDefinitionCopiesV111.CloneEnemyDefinition)
        .ToArray();

    private static readonly PrototypeEnemyDefinition[] EnemyDefinitions =
    [
        // Waterfall Giant pressures through a predictable six-stage
        // sequence. Its Pressure Gun increases damage by five each use;
        // Steam Eruption accumulates the eventual death-blow damage.
        // When first killed, the last-stand power preserves the boss
        // for a telegraphed About to Blow turn before its explosion.
        new(
            "proto.enemy.waterfall_giant",
            "Waterfall Giant",
            240,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "pressurize",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.ApplyEnemyPower, 15,
                        PowerId: "proto.power.steam_eruption",
                        AscensionDeltas: [new(9, 5)])]),
                new PrototypeEnemyMoveDefinition(
                    "stomp",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 15,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                            PowerId: "proto.power.weak"),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                            PowerId: "proto.power.steam_eruption")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "ram",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 10,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                            PowerId: "proto.power.steam_eruption")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "siphon",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.HealSelf, 10,
                            AscensionDeltas: [new(8, 5)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                            PowerId: "proto.power.steam_eruption")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "pressure_gun",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 20,
                            AscensionDeltas: [new(9, 3)],
                            ExtraAmountPerPriorMoveUse: 5),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                            PowerId: "proto.power.steam_eruption")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "pressure_up",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 13,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                            PowerId: "proto.power.steam_eruption")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "about_to_blow",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.StoreEnemyPowerAsDamage, 0,
                        PowerId: "proto.power.steam_eruption")]),
                new PrototypeEnemyMoveDefinition(
                    "explode",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 0,
                            UseStoredEnemyDamage: true),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.KillSelf, 0)
                    ])
            ],
            HpAscensionDeltas: [new(8, 10)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "pressurize",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "pressurize", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "stomp"),
                    new PrototypeEnemyAiStateDefinition(
                        "stomp", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "ram"),
                    new PrototypeEnemyAiStateDefinition(
                        "ram", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "siphon"),
                    new PrototypeEnemyAiStateDefinition(
                        "siphon", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 3, NextStateId: "pressure_gun"),
                    new PrototypeEnemyAiStateDefinition(
                        "pressure_gun", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 4, NextStateId: "pressure_up"),
                    new PrototypeEnemyAiStateDefinition(
                        "pressure_up", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 5, NextStateId: "stomp"),
                    new PrototypeEnemyAiStateDefinition(
                        "about_to_blow", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 6, NextStateId: "explode"),
                    new PrototypeEnemyAiStateDefinition(
                        "explode", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 7, NextStateId: "explode")
                ])),
        // Source Lagavulin Matriarch: asleep for at most three turns,
        // starting with Plating 12; wakes on an unblocked attack.
        // Awake move cycle: Slash -> Disembowel -> Slash2 -> Soul
        // Siphon -> Slash. Both powers and the AI cycle are declarative.
        new(
            "proto.enemy.lagavulin_matriarch",
            "Lagavulin Matriarch",
            222,
            0,
            [
                new PrototypeEnemyMoveDefinition("sleep", []),
                new PrototypeEnemyMoveDefinition(
                    "slash",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 19,
                        AscensionDeltas: [new(9, 2)])]),
                new PrototypeEnemyMoveDefinition(
                    "disembowel",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 9,
                        Repetitions: 2,
                        AscensionDeltas: [new(9, 1)])]),
                new PrototypeEnemyMoveDefinition(
                    "slash2",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 12,
                            AscensionDeltas: [new(9, 2)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.GainBlock, 12,
                            AscensionDeltas: [new(8, 2)])
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "soul_siphon",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyPlayerPower, -2,
                            PowerId: "proto.power.strength"),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyPlayerPower, -2,
                            PowerId: "proto.power.dexterity"),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                            PowerId: "proto.power.strength")
                    ])
            ],
            HpAscensionDeltas: [new(8, 11)],
            StartingPowers:
            [
                new("proto.power.plating", 12),
                new("proto.power.asleep", 3)
            ],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "sleep",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "sleep", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "sleep"),
                    new PrototypeEnemyAiStateDefinition(
                        "slash", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "disembowel"),
                    new PrototypeEnemyAiStateDefinition(
                        "disembowel", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "slash2"),
                    new PrototypeEnemyAiStateDefinition(
                        "slash2", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 3, NextStateId: "soul_siphon"),
                    new PrototypeEnemyAiStateDefinition(
                        "soul_siphon", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 4, NextStateId: "slash")
                ])),
        // Source SoulFysh: five-turn loop. Beckon deposits one status
        // randomly in the draw pile and one in discard, Gaze deposits
        // another in discard. Fade grants Intangible for two enemy side
        // turns, then Scream ends its final protected turn.
        new(
            "proto.enemy.soul_fysh",
            "Soul Fysh",
            211,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "beckon",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.AddCardsToRandomDraw,
                            1, CardId: "proto.status.beckon"),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.AddCardsToDiscard,
                            1, CardId: "proto.status.beckon")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "de_gas",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 16,
                        AscensionDeltas: [new(9, 2)])]),
                new PrototypeEnemyMoveDefinition(
                    "gaze",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 7,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.AddCardsToDiscard,
                            1, CardId: "proto.status.beckon")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "fade",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.ApplyEnemyPower,
                        2, PowerId: "proto.power.enemy_intangible")]),
                new PrototypeEnemyMoveDefinition(
                    "scream",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 13,
                            AscensionDeltas: [new(9, 2)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyPlayerPower, 3,
                            PowerId: "proto.power.vulnerable")
                    ])
            ],
            HpAscensionDeltas: [new(8, 10)]),
        // Native CorpseSlug: 25–27 HP (A8: 27–29), rotating
        // whip/slap, glomp, goop. The encounter coordinates openers.
        new(
            "proto.enemy.corpse_slug",
            "Corpse Slug",
            27,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "whip_slap",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 3,
                        Repetitions: 2)]),
                new PrototypeEnemyMoveDefinition(
                    "glomp",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 8,
                        AscensionDeltas: [new(9, 1)])]),
                new PrototypeEnemyMoveDefinition(
                    "goop",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                        PowerId: "proto.power.frail")])
            ],
            StartingPowers:
            [
                new("proto.power.ravenous", 4,
                    [new(9, 1)])
            ],
            MinHp: 25,
            HpAscensionDeltas: [new(8, 2)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "whip",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "whip", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "glomp"),
                    new PrototypeEnemyAiStateDefinition(
                        "glomp", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "goop"),
                    new PrototypeEnemyAiStateDefinition(
                        "goop", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "whip")
                ])),
        // CultistsNormal consists of one Calcified and one Damp Cultist.
        // Both begin with Incantation and then repeat Dark Strike.
        new(
            "proto.enemy.calcified_cultist",
            "Calcified Cultist",
            41,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "incantation",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                        PowerId: "proto.power.ritual")]),
                new PrototypeEnemyMoveDefinition(
                    "dark_strike",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 9,
                        AscensionDeltas: [new(9, 2)])])
            ],
            MoveLoopStartIndex: 1,
            MinHp: 38,
            HpAscensionDeltas: [new(8, 1)]),
        new(
            "proto.enemy.damp_cultist",
            "Damp Cultist",
            53,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "incantation",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.ApplyEnemyPower, 5,
                        PowerId: "proto.power.ritual",
                        AscensionDeltas: [new(9, 1)])]),
                new PrototypeEnemyMoveDefinition(
                    "dark_strike",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 1,
                        AscensionDeltas: [new(9, 2)])])
            ],
            MoveLoopStartIndex: 1,
            MinHp: 51,
            HpAscensionDeltas: [new(8, 1)]),
        // Fossil Stalker opens with Latch; subsequently selects among
        // Tackle, Latch and Lash at equal weights, including repeats.
        new(
            "proto.enemy.fossil_stalker",
            "Fossil Stalker",
            53,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "tackle",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 9,
                            AscensionDeltas: [new(9, 2)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                            PowerId: "proto.power.frail")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "latch",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 12,
                        AscensionDeltas: [new(9, 2)])]),
                new PrototypeEnemyMoveDefinition(
                    "lash",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 3,
                        Repetitions: 2,
                        AscensionDeltas: [new(9, 1)])])
            ],
            StartingPowers: [new("proto.power.suck", 3)],
            MinHp: 51,
            HpAscensionDeltas: [new(8, 3)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "latch",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "latch", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "tackle", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "lash", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "random", PrototypeEnemyAiStateKind.Random,
                        Branches: [new("tackle", 2), new("latch", 2), new("lash", 2)])
                ])),
        // Haunted Ship: forced Haunt, then Swipe / Stomp alternation.
        // Haunt applies 3 Weak and deposits 5 Ethereal Dazed status
        // cards in the discard pile. The state machine never repeats
        // the opener or returns to it after the first turn.
        new(
            "proto.enemy.haunted_ship",
            "Haunted Ship",
            63,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "haunt",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyPlayerPower, 3,
                            PowerId: "proto.power.weak"),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.AddCardsToDiscard, 5,
                            CardId: "proto.status.dazed")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "swipe",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 13,
                        AscensionDeltas: [new(9, 1)])]),
                new PrototypeEnemyMoveDefinition(
                    "stomp",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 4,
                        Repetitions: 3,
                        AscensionDeltas: [new(9, 1)])])
            ],
            HpAscensionDeltas: [new(8, 4)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "haunt",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "haunt", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "swipe"),
                    new PrototypeEnemyAiStateDefinition(
                        "swipe", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "stomp"),
                    new PrototypeEnemyAiStateDefinition(
                        "stomp", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "swipe")
                ])),
        // Native Sewer Clam opens with Jet, then alternates Pressurize
        // (+4 Strength) and Jet. Starting Plating: 8 (A8: 9).
        new(
            "proto.enemy.sewer_clam",
            "Sewer Clam",
            56,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "jet",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 10,
                        AscensionDeltas: [new(9, 1)])]),
                new PrototypeEnemyMoveDefinition(
                    "pressurize",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.ApplyEnemyPower, 4,
                        PowerId: "proto.power.strength")])
            ],
            StartingPowers: [new("proto.power.plating", 8, [new(8, 1)])],
            HpAscensionDeltas: [new(8, 2)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "jet",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "jet", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "pressurize"),
                    new PrototypeEnemyAiStateDefinition(
                        "pressurize", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "jet")
                ])),
        // Living Fog opens with Advanced Gas (+Smoggy 1), then
        // alternates Bloat (one Gas Bomb plus attack) and Super Gas Blast.
        // Bombs occupy the last available named bomb slot and explode
        // (attack then self-kill) on their next enemy turn.
        new(
            "proto.enemy.living_fog",
            "Living Fog",
            80,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "advanced_gas",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 8,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                            PowerId: "proto.power.smoggy")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "bloat",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.SummonEnemy, 1,
                            EnemyId: "proto.enemy.gas_bomb",
                            SummonSlotNames:
                            ["bomb1", "bomb2", "bomb3", "bomb4", "bomb5"]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 5,
                            AscensionDeltas: [new(9, 1)])
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "super_gas_blast",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 8,
                        AscensionDeltas: [new(9, 1)])])
            ],
            HpAscensionDeltas: [new(8, 2)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "advanced",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "advanced", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "bloat"),
                    new PrototypeEnemyAiStateDefinition(
                        "bloat", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "blast"),
                    new PrototypeEnemyAiStateDefinition(
                        "blast", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "bloat")
                ])),
        new(
            "proto.enemy.gas_bomb",
            "Gas Bomb",
            7,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "explode",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 8,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.KillSelf, 0)
                    ])
            ],
            HpAscensionDeltas: [new(8, 1)],
            StartingPowers: [new("proto.power.minion", 1)],
            IsMinion: true),
        // Two-Tailed Rats have staggered first moves and then choose
        // weighted no-repeat Scratch/Bite/Screech attacks. Backup is
        // eligible only after two non-summoning actions, a free slot,
        // no other squad summons that turn and fewer than three prior
        // coordinated summons. The native random-branch weights,
        // normalized to integers, are 1:1:3:9 when summonable.
        new(
            "proto.enemy.two_tailed_rat",
            "Two-Tailed Rat",
            21,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "scratch",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 8,
                        AscensionDeltas: [new(9, 1)])]),
                new PrototypeEnemyMoveDefinition(
                    "disease_bite",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 6,
                        AscensionDeltas: [new(9, 1)])]),
                new PrototypeEnemyMoveDefinition(
                    "screech",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                        PowerId: "proto.power.frail")]),
                new PrototypeEnemyMoveDefinition(
                    "call_for_backup",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.SummonEnemy, 1,
                        EnemyId: "proto.enemy.two_tailed_rat",
                        SummonSlotNames:
                            ["first", "second", "third", "fourth", "fifth"])])
            ],
            MinHp: 17,
            HpAscensionDeltas: [new(8, 1)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            NonSummonMovesBeforeEligible: 2,
            MaxCoordinatedSummons: 3,
            Ai: new PrototypeEnemyAiDefinition(
                "random",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "scratch", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "bite", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "screech", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "backup", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 3, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "random", PrototypeEnemyAiStateKind.Random,
                        Branches:
                        [
                            new("scratch", 1,
                                PrototypeEnemyAiRepeatRule.CannotRepeat),
                            new("bite", 1,
                                PrototypeEnemyAiRepeatRule.CannotRepeat),
                            new("screech", 3,
                                PrototypeEnemyAiRepeatRule.CannotRepeat),
                            new("backup", 9,
                                PrototypeEnemyAiRepeatRule.UseOnlyOnce,
                                RequiresAvailableSummon: true)
                        ])
                ])),
        // Gremlin Merc: opens alone with Surprise and Thievery 20.
        // Steal happens once per completed move, not once per hit.
        // At death it summons a Fat and Sneaky Gremlin; the Fat
        // receives any stolen gold and returns it if defeated before
        // escaping. This is modeled through general death summons.
        new(
            "proto.enemy.gremlin_merc",
            "Gremlin Merc",
            49,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "gimme",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 7,
                            Repetitions: 2,
                            AscensionDeltas: [new(8, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.StealPlayerGold, 20)
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "double_smash",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 6,
                            Repetitions: 2,
                            AscensionDeltas: [new(8, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.StealPlayerGold, 20),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                            PowerId: "proto.power.weak")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "hehe",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 8,
                            AscensionDeltas: [new(8, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.StealPlayerGold, 20),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                            PowerId: "proto.power.strength")
                    ])
            ],
            MinHp: 47,
            HpAscensionDeltas: [new(8, 4)],
            StartingPowers:
            [
                new("proto.power.surprise", 1),
                new("proto.power.thievery", 20)
            ],
            DeathSummons:
            [
                new("proto.enemy.sneaky_gremlin", 1, "sneaky",
                    SkipEnemyActions: 0),
                new("proto.enemy.fat_gremlin", 2, "fat",
                    TransferStolenGold: true, SkipEnemyActions: 0)
            ]),
        new(
            "proto.enemy.sneaky_gremlin",
            "Sneaky Gremlin",
            14,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "spawned", []),
                new PrototypeEnemyMoveDefinition(
                    "tackle",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 9,
                        AscensionDeltas: [new(9, 1)])])
            ],
            MoveLoopStartIndex: 1,
            MinHp: 10,
            HpAscensionDeltas: [new(8, 1)]),
        new(
            "proto.enemy.fat_gremlin",
            "Fat Gremlin",
            17,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "spawned", []),
                new PrototypeEnemyMoveDefinition(
                    "flee",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.EscapeEnemy, 0)])
            ],
            MoveLoopStartIndex: 1,
            MinHp: 13,
            HpAscensionDeltas: [new(8, 1)],
            RecoverCarriedGoldOnDeath: true,
            EscapedRewardProportionWithGold: 0f,
            EscapedRewardProportionWithoutGold: 0.5f),
        // Elite Skulking Colony: 75 HP (A8: 80), Hardened Shell 20
        // caps HP loss per enemy side turn, and a four-move cycle.
        new(
            "proto.enemy.skulking_colony",
            "Skulking Colony",
            75,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "zoom",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 14,
                        AscensionDeltas: [new(9, 2)])]),
                new PrototypeEnemyMoveDefinition(
                    "zoom_2",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 14,
                        AscensionDeltas: [new(9, 2)])]),
                new PrototypeEnemyMoveDefinition(
                    "inertia",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 9,
                            AscensionDeltas: [new(9, 2)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                            PowerId: "proto.power.strength",
                            AscensionDeltas: [new(9, 2)])
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "piercing_stabs",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 7,
                        Repetitions: 2,
                        AscensionDeltas: [new(9, 1)])])
            ],
            HpAscensionDeltas: [new(8, 5)],
            StartingPowers: [new("proto.power.hardened_shell", 20)]),
        // Four native Phantasmal Gardeners are identical in stats but
        // use their slot names for distinct opening moves, then each
        // rotates Flail -> Enlarge -> Bite -> Lash -> Flail.
        new(
            "proto.enemy.phantasmal_gardener",
            "Phantasmal Gardener",
            31,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "bite",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 5)]),
                new PrototypeEnemyMoveDefinition(
                    "lash",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 7)]),
                new PrototypeEnemyMoveDefinition(
                    "flail",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 1,
                        Repetitions: 3)]),
                new PrototypeEnemyMoveDefinition(
                    "enlarge",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                        PowerId: "proto.power.strength",
                        AscensionDeltas: [new(9, 1)])])
            ],
            MinHp: 26,
            HpAscensionDeltas: [new(8, 1)],
            StartingPowers: [new("proto.power.skittish", 6, [new(8, 1)])],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "opening",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "opening", PrototypeEnemyAiStateKind.Conditional,
                        ConditionalBranches:
                        [
                            new("flail", PrototypeEnemyAiConditionKind.SlotNameEquals,
                                "first"),
                            new("bite", PrototypeEnemyAiConditionKind.SlotNameEquals,
                                "second"),
                            new("lash", PrototypeEnemyAiConditionKind.SlotNameEquals,
                                "third"),
                            new("enlarge", PrototypeEnemyAiConditionKind.SlotNameEquals,
                                "fourth")
                        ]),
                    new PrototypeEnemyAiStateDefinition(
                        "bite", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "lash"),
                    new PrototypeEnemyAiStateDefinition(
                        "lash", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "flail"),
                    new PrototypeEnemyAiStateDefinition(
                        "flail", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "enlarge"),
                    new PrototypeEnemyAiStateDefinition(
                        "enlarge", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 3, NextStateId: "bite")
                ])),
        // Terror Eel: 140 HP (A8: 150) with Shriek 70 (A8: 75).
        // After receiving unblocked HP damage at or below Shriek's
        // threshold, its pending action changes to Stun, then Terror
        // (99 Vulnerable), before returning to Crash/Thrash.
        new(
            "proto.enemy.terror_eel",
            "Terror Eel",
            140,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "crash",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 16,
                        AscensionDeltas: [new(9, 2)])]),
                new PrototypeEnemyMoveDefinition(
                    "thrash",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 3,
                            Repetitions: 3,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 6,
                            PowerId: "proto.power.vigor")
                    ]),
                new PrototypeEnemyMoveDefinition("stun", []),
                new PrototypeEnemyMoveDefinition(
                    "terror",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.ApplyPlayerPower, 99,
                        PowerId: "proto.power.vulnerable")])
            ],
            HpAscensionDeltas: [new(8, 10)],
            StartingPowers: [new("proto.power.shriek", 70, [new(8, 5)])],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "crash",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "crash", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "thrash"),
                    new PrototypeEnemyAiStateDefinition(
                        "thrash", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "crash"),
                    new PrototypeEnemyAiStateDefinition(
                        "stun", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "terror"),
                    new PrototypeEnemyAiStateDefinition(
                        "terror", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 3, NextStateId: "crash")
                ])),
        // Native SludgeSpinner: 37–39 HP (A8: 41–42).
        // Forced Oil Spray, then uniform no-immediate-repeat branch
        // between Oil Spray, Slam and Rage.
        new(
            "proto.enemy.sludge_spinner",
            "Sludge Spinner",
            39,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "oil_spray",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 8,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                            PowerId: "proto.power.weak")
                    ]),
                new PrototypeEnemyMoveDefinition(
                    "slam",
                    [new PrototypeEnemyEffectSpec(
                        PrototypeEnemyEffectKind.DamagePlayer, 11,
                        AscensionDeltas: [new(9, 1)])]),
                new PrototypeEnemyMoveDefinition(
                    "rage",
                    [
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.DamagePlayer, 6,
                            AscensionDeltas: [new(9, 1)]),
                        new PrototypeEnemyEffectSpec(
                            PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                            PowerId: "proto.power.strength")
                    ])
            ],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            HpAscensionDeltas: [new(8, 3)],
            MinHp: 37,
            MinHpAscensionDeltas: [new(8, 4)],
            Ai: new PrototypeEnemyAiDefinition(
                "oil",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "oil", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "slam", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "rage", PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2, NextStateId: "random"),
                    new PrototypeEnemyAiStateDefinition(
                        "random", PrototypeEnemyAiStateKind.Random,
                        Branches:
                        [
                            new("oil",
                                RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat),
                            new("slam",
                                RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat),
                            new("rage",
                                RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat)
                        ])
                ]))
    ];

    // Pinned native encounter membership and formation. All encounters
    // have Weight=0 outside the opt-in Underdocks pool.
    public static PrototypeEncounterDefinition[] Encounters =>
        EncounterDefinitions
            .Select(PrototypeDefinitionCopiesV111.CloneEncounterDefinition)
            .ToArray();

    private static readonly PrototypeEncounterDefinition[] EncounterDefinitions =
    [
        new(
            "proto.encounter.waterfall_giant_boss",
            PrototypeRoomType.Boss,
            ["proto.enemy.waterfall_giant"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.waterfall_giant", 0)]),
        new(
            "proto.encounter.lagavulin_matriarch_boss",
            PrototypeRoomType.Boss,
            ["proto.enemy.lagavulin_matriarch"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.lagavulin_matriarch", 0)]),
        new(
            "proto.encounter.soul_fysh_boss",
            PrototypeRoomType.Boss,
            ["proto.enemy.soul_fysh"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.soul_fysh", 0)]),

        new(
            "proto.encounter.corpse_slugs_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.corpse_slug", "proto.enemy.corpse_slug"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation:
            [
                new("proto.enemy.corpse_slug", 0),
                new("proto.enemy.corpse_slug", 1)
            ],
            CyclicOpeningAiStateIds: ["whip", "glomp", "goop"]),
        new(
            "proto.encounter.corpse_slugs_normal",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.corpse_slug",
                "proto.enemy.corpse_slug",
                "proto.enemy.corpse_slug"
            ],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation:
            [
                new("proto.enemy.corpse_slug", 0),
                new("proto.enemy.corpse_slug", 1),
                new("proto.enemy.corpse_slug", 2)
            ],
            CyclicOpeningAiStateIds: ["whip", "glomp", "goop"]),
        new(
            "proto.encounter.cultists_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.calcified_cultist", "proto.enemy.damp_cultist"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation:
            [
                new("proto.enemy.calcified_cultist", 0),
                new("proto.enemy.damp_cultist", 1)
            ]),
        new(
            "proto.encounter.fossil_stalker_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.fossil_stalker"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.fossil_stalker", 0)]),
        new(
            "proto.encounter.seapunk_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.calcified_cultist", "proto.enemy.seapunk"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation:
            [
                new("proto.enemy.calcified_cultist", 0),
                new("proto.enemy.seapunk", 1)
            ]),
        new(
            "proto.encounter.haunted_ship_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.haunted_ship"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.haunted_ship", 0)]),
        new(
            "proto.encounter.sewer_clam_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.sewer_clam"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.sewer_clam", 0)]),
        new(
            "proto.encounter.living_fog_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.living_fog"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.living_fog", 5, "livingFog")]),
        new(
            "proto.encounter.two_tailed_rats_normal",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.two_tailed_rat",
                "proto.enemy.two_tailed_rat",
                "proto.enemy.two_tailed_rat"
            ],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation:
            [
                new("proto.enemy.two_tailed_rat", 2, "third"),
                new("proto.enemy.two_tailed_rat", 3, "fourth"),
                new("proto.enemy.two_tailed_rat", 4, "fifth")
            ],
            CyclicOpeningAiStateIds: ["scratch", "bite", "screech"]),
        new(
            "proto.encounter.gremlin_merc_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.gremlin_merc"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.gremlin_merc", 0, "merc")]),
        new(
            "proto.encounter.phantasmal_gardeners_elite",
            PrototypeRoomType.Elite,
            [
                "proto.enemy.phantasmal_gardener",
                "proto.enemy.phantasmal_gardener",
                "proto.enemy.phantasmal_gardener",
                "proto.enemy.phantasmal_gardener"
            ],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation:
            [
                new("proto.enemy.phantasmal_gardener", 0, "first"),
                new("proto.enemy.phantasmal_gardener", 1, "second"),
                new("proto.enemy.phantasmal_gardener", 2, "third"),
                new("proto.enemy.phantasmal_gardener", 3, "fourth")
            ]),
        new(
            "proto.encounter.skulking_colony_elite",
            PrototypeRoomType.Elite,
            ["proto.enemy.skulking_colony"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.skulking_colony", 0)]),
        new(
            "proto.encounter.terror_eel_elite",
            PrototypeRoomType.Elite,
            ["proto.enemy.terror_eel"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.terror_eel", 0)]),
        new(
            "proto.encounter.punch_construct_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.punch_construct"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.punch_construct", 0)]),
        new(
            "proto.encounter.seapunk_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.seapunk"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.seapunk", 0)]),
        new(
            "proto.encounter.sludge_spinner_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.sludge_spinner"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation: [new("proto.enemy.sludge_spinner", 0)]),
        new(
            "proto.encounter.toadpoles_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.toadpole", "proto.enemy.toadpole"],
            MinAct: 1, MaxAct: 1, Weight: 0,
            Formation:
            [
                new("proto.enemy.toadpole", 0),
                new("proto.enemy.toadpole", 1)
            ])
    ];
}

/// <summary>
/// Opt-in native-shaped Act 1 Underdocks start. Reuses the Act 1
/// StandardActMap approximation and the shared Neow opening, not
/// Overgrowth encounter or event pools. Source v0.111.0: both
/// Act 1 regions have 15 map rooms and three initial weak fights.
/// </summary>
public static class PrototypeNativeUnderdocksRunFactory
{
    public static RunState Create(string seed, int ascension = 0) =>
        CreateCore(seed, ascension, null);

    public static ReferenceNatureUnderdocksRunResultV111
        CreateWithReferenceNature(
            string seed, int ascension,
            ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(initialNatureState);
        initialNatureState.Validate();
        var provider = new ReferenceNatureUnderdocksBossIndexProviderV111(
            initialNatureState);
        var createdRun = CreateCore(seed, ascension, provider);
        var run = createdRun with
        {
            Stochastic = RunStochasticStateV111.WithReferenceNature(
                createdRun.Stochastic, provider.NatureState)
        };
        var result = new ReferenceNatureUnderdocksRunResultV111(
            run, initialNatureState.Fork(), provider.NatureState,
            provider.ChanceLaws, provider.DecisionTranscript);
        result.Validate();
        return result;
    }

    private static RunState CreateCore(string seed, int ascension,
        IRandomIndexProviderV111? bossIndexProvider)
    {
        // The existing native-shaped Act 1 starter handles Neow, 13-card
        // starter deck, Ascension, potion slots and the shared map geometry.
        var baseState = PrototypeNativeOvergrowthRunFactory.Create(
            seed, ascension);
        var world = baseState.World
            ?? throw new InvalidOperationException("Missing Act 1 world.");
        var rng = baseState.Stochastic.PrototypeRng;

        // The prototype StartRun had already rolled an Overgrowth boss.
        // Replace its combat stream with the clean initial stream before
        // rolling this region's selected boss, avoiding a hidden extra
        // Overgrowth draw.
        var clean = PrototypeRng.CreateBundle(seed);
        rng.ReplaceStream("combat", clean.GetStream("combat").Fork());
        var bosses = PrototypeNativeUnderdocks.NativeBossEncounterIds;
        RandomIndexDecisionV111 bossDecision;
        using (ControlledEventChoiceReplayV111.DescribeCandidates(
            (string[])bosses.Clone()))
        {
            if (bossIndexProvider is null)
            {
                bossDecision = LegacyRandomChoiceV111.ChooseIndexRange(
                    rng,
                    PrototypeNativeUnderdocks.BossEncounterIndexSiteId,
                    "combat",
                    "act:1:underdocks:boss_pool:" + CanonicalJson.Sha256(bosses),
                    bosses.Length);
            }
            else
            {
                using var providerScope = StochasticProviderScopeV111.Enter(
                    indexProvider: bossIndexProvider,
                    overrideExplicitProviders: true);
                bossDecision = LegacyRandomChoiceV111.ChooseIndexRange(
                    rng,
                    PrototypeNativeUnderdocks.BossEncounterIndexSiteId,
                    "combat",
                    "act:1:underdocks:boss_pool:" + CanonicalJson.Sha256(bosses),
                    bosses.Length, ordinal: 0);
            }
        }
        var boss = bosses[bossDecision.SelectedIndex];

        // Both native Act 1 regions use the same StandardActMap room
        // constraints, with a distinct region profile for routing.
        var map = world.Map with
        {
            GenerationProfileId =
                PrototypeNativeUnderdocks.GenerationProfileId
        };
        return baseState with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                baseState.Stochastic, rng),
            World = world with
            {
                ActOneRegion = PrototypeActOneRegion.Underdocks,
                Stochastic = world.Stochastic with
                {
                    ActOneEncounterPool =
                        new PrototypeActOneEncounterPoolState(
                            PrototypeActOneRegion.Underdocks,
                            OrdinaryCombatsStarted: 0,
                            RemainingWeakEncounterIds:
                                (string[])PrototypeNativeUnderdocks
                                    .SupportedWeakEncounterIds.Clone(),
                            RemainingNormalEncounterIds:
                                (string[])PrototypeNativeUnderdocks
                                    .SupportedNormalEncounterIds.Clone(),
                            RemainingEliteEncounterIds:
                                (string[])PrototypeNativeUnderdocks
                                    .SupportedEliteEncounterIds.Clone(),
                            BossEncounterId: boss)
                },
                Map = map
            }
        };
    }
}

internal sealed class ReferenceNatureUnderdocksBossIndexProviderV111(
    ReferenceNatureStateV111 initialNatureState) : IRandomIndexProviderV111
{
    private ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        _stochastic = new(
            initialNatureState?.Fork()
                ?? throw new ArgumentNullException(nameof(initialNatureState)),
            initialNatureState?.Fork()
                ?? throw new ArgumentNullException(nameof(initialNatureState)),
            []);
    private readonly List<ChanceLawV111> _chanceLaws = [];

    public string Mode => ReferenceNatureV111.ProviderId;
    public ReferenceNatureStateV111 NatureState =>
        _stochastic.NatureState.Fork();
    public ChanceLawV111[] ChanceLaws => _chanceLaws
        .Select(law => law.Fork()).ToArray();
    public ReferenceNatureDecisionV111[] DecisionTranscript =>
        _stochastic.DecisionTranscript;

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.ValidateForLegacyProvider();
        var bosses = PrototypeNativeUnderdocks.NativeBossEncounterIds;
        var expectedOwner = "act:1:underdocks:boss_pool:" +
            CanonicalJson.Sha256(bosses);
        if (_stochastic.DecisionTranscript.Length != 0 ||
            request.SiteId != PrototypeNativeUnderdocks
                .BossEncounterIndexSiteId ||
            request.StreamId != "combat" ||
            request.OwnerId != expectedOwner || request.Ordinal != 0 ||
            request.OperationKind != "choose_index" ||
            request.SupportCount != bosses.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, bosses.Length)) ||
            !streams.TryGetStream("combat", out var combatStream) ||
            combatStream.Codec != PrototypeRng.Codec)
            throw new InvalidDataException(
                "Underdocks boss request differs from its run-start boundary.");

        var law = ChanceLawEngineV111.QueryUnderdocksBossEncounter(0);
        if (law.SiteId != request.SiteId ||
            law.OperationKind != request.OperationKind ||
            law.OwnerId != request.OwnerId ||
            law.Ordinal != request.Ordinal ||
            law.Outcomes.Length != request.SupportCount)
            throw new InvalidDataException(
                "Underdocks boss Chance Law differs from its source request.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, _stochastic.NatureState);
        _chanceLaws.Add(law.Fork());
        _stochastic = _stochastic.Advance(selection.NextState,
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex));
        var decision = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, selection.OutcomeIndex, Mode);
        decision.Validate();
        return decision;
    }
}

public sealed record ReferenceNatureUnderdocksRunResultV111(
    RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ChanceLawV111[] ChanceLaws,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private RunState? _run = Run?.Fork();
    private ChanceLawV111[]? _chanceLaws = ChanceLaws?.Select(law =>
        law.Fork()).ToArray();

    [System.Text.Json.Serialization.JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic { get; init; } = new(InitialNatureState, NatureState,
            DecisionTranscript);

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Stochastic.InitialNatureState;
        init => Stochastic = Stochastic with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Stochastic.NatureState;
        init => Stochastic = Stochastic with { NatureState = value };
    }

    public RunState Run
    {
        get => _run?.Fork()!;
        init => _run = value?.Fork();
    }

    public ChanceLawV111[] ChanceLaws
    {
        get => _chanceLaws?.Select(law => law.Fork()).ToArray()!;
        init => _chanceLaws = value?.Select(law => law.Fork()).ToArray();
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    public void Validate()
    {
        var run = Run;
        ArgumentNullException.ThrowIfNull(run);
        Stochastic.Validate();
        var stochastic = Stochastic;
        var laws = ChanceLaws;
        var transcript = stochastic.DecisionTranscript;
        var world = run.World;
        if (laws is null || transcript is null ||
            laws.Length != 1 || transcript.Length != 1 ||
            run.Stochastic.ReferenceNature is not { } savedCursor ||
            !savedCursor.SameCursor(stochastic.NatureState) ||
            world is null ||
            world.ActOneRegion != PrototypeActOneRegion.Underdocks ||
            world.Stochastic.ActOneEncounterPool is not { } encounterPool ||
            encounterPool.Region != PrototypeActOneRegion.Underdocks ||
            string.IsNullOrWhiteSpace(encounterPool.BossEncounterId))
            throw new InvalidDataException(
                "Invalid ReferenceNature Underdocks run result.");
        PrototypeStateInvariants.Validate(run);
        var law = laws[0];
        var decision = transcript[0];
        law.Validate();
        decision.Validate();
        var expectedLaw = ChanceLawEngineV111.QueryUnderdocksBossEncounter(0);
        if (CanonicalJson.Sha256(expectedLaw) !=
            CanonicalJson.Sha256(law))
            throw new InvalidDataException(
                "Underdocks boss Chance Law differs from its source query.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, stochastic.InitialNatureState);
        var expectedDecision = new ReferenceNatureDecisionV111(
            law.LawId, law.EventId, selection.OutcomeId,
            selection.OutcomeIndex);
        if (CanonicalJson.Sha256(expectedDecision) !=
                CanonicalJson.Sha256(decision) ||
            !StringComparer.Ordinal.Equals(
                selection.OutcomeId,
                "encounter:" + encounterPool.BossEncounterId) ||
            !selection.NextState.SameCursor(stochastic.NatureState) ||
            stochastic.NatureState.SampleCount !=
                checked(stochastic.InitialNatureState.SampleCount + 1))
            throw new InvalidDataException(
                "Underdocks boss result does not replay from its Nature cursor.");

        var cleanCombat = PrototypeRng.CreateBundle(run.RunSeed)
            .GetStream("combat");
        var resultCombat = run.Stochastic.PrototypeRng.GetStream("combat");
        if (CanonicalJson.Sha256(cleanCombat) !=
            CanonicalJson.Sha256(resultCombat))
            throw new InvalidDataException(
                "ReferenceNature Underdocks creation advanced the legacy combat cursor.");
    }
}
