namespace Sts2Emulator.Core;

/// <summary>
/// v0.111.0 Underdocks single-player Act 1 content inventory.
/// All four native weak encounters and a restricted normal-encounter
/// subset are playable. Other normals, elites and bosses remain gated
/// rather than replaced with Overgrowth content.
/// </summary>
public static class PrototypeNativeUnderdocks
{
    public const string GenerationProfileId =
        "native-underdocks-map-structure-v0.111.0-v1";

    // Source: Underdocks.GenerateAllEncounters() and IsWeak.
    public static string[] NativeWeakEncounterIds { get; } =
    [
        "proto.encounter.corpse_slugs_weak",
        "proto.encounter.seapunk_weak",
        "proto.encounter.sludge_spinner_weak",
        "proto.encounter.toadpoles_weak"
    ];

    public static string[] NativeNormalEncounterIds { get; } =
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

    public static string[] NativeEliteEncounterIds { get; } =
    [
        "proto.encounter.phantasmal_gardeners_elite",
        "proto.encounter.skulking_colony_elite",
        "proto.encounter.terror_eel_elite"
    ];

    public static string[] NativeBossEncounterIds { get; } =
    [
        "proto.encounter.waterfall_giant_boss",
        "proto.encounter.soul_fysh_boss",
        "proto.encounter.lagavulin_matriarch_boss",
        "proto.encounter.waterfall_giant_boss"
    ];

    public static string[] SupportedWeakEncounterIds { get; } =
    [
        "proto.encounter.corpse_slugs_weak",
        "proto.encounter.seapunk_weak",
        "proto.encounter.sludge_spinner_weak",
        "proto.encounter.toadpoles_weak"
    ];

    // Initial native-shaped normal slice. This is *not* the full ten-
    // encounter normal distribution; unsupported entries remain cataloged.
    public static string[] SupportedNormalEncounterIds { get; } =
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

    public static string[] SupportedEliteEncounterIds { get; } =
    [
        "proto.encounter.skulking_colony_elite",
        "proto.encounter.phantasmal_gardeners_elite",
        "proto.encounter.terror_eel_elite"
    ];

    public static string[] SupportedBossEncounterIds { get; } =
    [
        "proto.encounter.soul_fysh_boss",
        "proto.encounter.lagavulin_matriarch_boss"
    ];

    public static PrototypePowerDefinition[] Powers { get; } =
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
            SkipInitialEnemySideTurnEnd: true),
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
            EnemyStacksDecayAtSideTurnStartAfterFirst: 1),
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
            ConsumeAfterEnemyAttack: true)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
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
    public static PrototypeEncounterDefinition[] Encounters { get; } =
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
    public static RunState Create(string seed, int ascension = 0)
    {
        // The existing native-shaped Act 1 starter handles Neow, 13-card
        // starter deck, Ascension, potion slots and the shared map geometry.
        var baseState = PrototypeNativeOvergrowthRunFactory.Create(
            seed, ascension);
        var world = baseState.World
            ?? throw new InvalidOperationException("Missing Act 1 world.");
        var rng = baseState.Rng.Fork();

        // The prototype StartRun had already rolled an Overgrowth boss.
        // Replace its combat stream with the clean initial stream before
        // rolling this region's selected boss, avoiding a hidden extra
        // Overgrowth draw.
        var clean = PrototypeRng.CreateBundle(seed);
        var combatIndex = Array.FindIndex(rng.Streams, stream =>
            stream.StreamId == "combat");
        var cleanCombatIndex = Array.FindIndex(clean.Streams, stream =>
            stream.StreamId == "combat");
        if (combatIndex < 0 || cleanCombatIndex < 0)
        {
            throw new InvalidOperationException("Missing combat RNG.");
        }
        rng.Streams[combatIndex] = clean.Streams[cleanCombatIndex].Fork();
        var bosses = PrototypeNativeUnderdocks.NativeBossEncounterIds;
        var boss = bosses[PrototypeRng.NextInt(
            rng, "combat", bosses.Length)];

        // Both native Act 1 regions use the same StandardActMap room
        // constraints, with a distinct region profile for routing.
        var map = world.Map with
        {
            GenerationProfileId =
                PrototypeNativeUnderdocks.GenerationProfileId
        };
        return baseState with
        {
            Rng = rng,
            World = world with
            {
                ActOneRegion = PrototypeActOneRegion.Underdocks,
                ActOneEncounterPool = new PrototypeActOneEncounterPoolState(
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
                    BossEncounterId: boss),
                Map = map
            }
        };
    }
}
