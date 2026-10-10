namespace Sts2Emulator.Core;

/// <summary>
/// Source-pinned Hive normal encounter definitions. These are independently
/// executable in forced combat fixtures but deliberately NOT selectable by
/// the legacy prototype map: Hive encounter pools and act progression are
/// still incomplete in v0.111.0.
/// </summary>
public static class PrototypeNativeHiveNormals
{
    public const string BowlbugsNormalId =
        "proto.native.hive.encounter.bowlbugs_normal";
    public const string ExoskeletonsNormalId =
        "proto.native.hive.encounter.exoskeletons_normal";
    public const string ChompersNormalId =
        "proto.native.hive.encounter.chompers_normal";
    public const string MytesNormalId =
        "proto.native.hive.encounter.mytes_normal";
    public const string SpinyToadNormalId =
        "proto.native.hive.encounter.spiny_toad_normal";
    public const string SlumberingBeetleNormalId =
        "proto.native.hive.encounter.slumbering_beetle_normal";

    public const string ChomperId = "proto.native.hive.chomper";
    public const string MyteId = "proto.native.hive.myte";
    public const string SpinyToadId = "proto.native.hive.spiny_toad";
    public const string SlumberingBeetleId =
        "proto.native.hive.slumbering_beetle";
    public const string ToxicCardId = "proto.native.hive.toxic";

    public static PrototypeCardDefinition[] Cards { get; } =
    [
        // Toxic.cs: cost 1 playable Status, Exhaust on use,
        // 5 damage while held at the end of the owner's turn.
        // It is combat-created only, never a normal reward.
        new(ToxicCardId, "Toxic", 1, PrototypeCardTarget.None,
            [], ExhaustOnUse: true, RewardEligible: false,
            Rarity: PrototypeCardRarity.Status,
            Type: PrototypeCardType.Status,
            EndTurnDamageIfInHand: 5, MaxUpgradeLevel: 0)
    ];

    // SlumberPower: three enemy-side turns of Snore or wake early on
    // unblocked incoming attack damage. Companion Plating is removed
    // on either wake route. This is NOT the Underdocks Asleep power:
    // their post-wake first moves have different identifiers.
    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new("proto.native.hive.slumber", "Slumber", 0, [],
            EnemyStacksDecayAtSideTurnEnd: 1,
            WakeOwnerOnUnblockedAttackDamage: true,
            OwnerAiStateOnWake: "rollout",
            RemoveOwnerPowersOnWake:
                ["proto.native.hive.slumber", "proto.power.plating"],
            StunOwnerOnWake: true,
            OwnerAiStateOnPowerExpiry: "rollout",
            RemoveOwnerPowersOnPowerExpiry: ["proto.power.plating"])
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // SpinyToad.cs: Protruding Spikes grants 5 Thorns,
        // Spike Explosion hits BEFORE removing those 5 stacks,
        // then Tongue Lash and repeat.
        new(SpinyToadId, "Spiny Toad", 119, 0,
            [
                new("protruding_spikes",
                    [new(PrototypeEnemyEffectKind.ApplyEnemyPower,
                        5, PowerId: "proto.power.thorns")]),
                new("spike_explosion",
                [
                    new(PrototypeEnemyEffectKind.DamagePlayer, 23,
                        AscensionDeltas: [new(9, 2)]),
                    new(PrototypeEnemyEffectKind.ApplyEnemyPower, -5,
                        PowerId: "proto.power.thorns")
                ]),
                new("tongue_lash",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 17,
                        AscensionDeltas: [new(9, 2)])])
            ],
            MinHp: 116, HpAscensionDeltas: [new(8, 5)]),
        // SlumberingBeetle.cs: native 86/89 HP, Plating 15/18
        // and three Slumber charges. Snore until the count expires
        // at enemy-side turn end; Roll Out then attacks and gains
        // Strength 2 every enemy turn. An unblocked attack hit
        // interrupts Slumber and stuns the current planned action.
        new(SlumberingBeetleId, "Slumbering Beetle", 86, 0,
            [
                new("snore", []),
                new("rollout",
                [
                    new(PrototypeEnemyEffectKind.DamagePlayer, 16,
                        AscensionDeltas: [new(9, 2)]),
                    new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                        PowerId: "proto.power.strength")
                ])
            ],
            HpAscensionDeltas: [new(8, 3)],
            StartingPowers:
            [
                new("proto.power.plating", 15,
                    AscensionDeltas: [new(8, 3)]),
                new("proto.native.hive.slumber", 3)
            ],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("snore",
            [
                new("snore", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 0, NextStateId: "post_snore"),
                new("post_snore", PrototypeEnemyAiStateKind.Conditional,
                    ConditionalBranches:
                    [
                        new("snore",
                            PrototypeEnemyAiConditionKind.HasEnemyPower,
                            "proto.native.hive.slumber"),
                        new("rollout",
                            PrototypeEnemyAiConditionKind.LacksEnemyPower,
                            "proto.native.hive.slumber")
                    ]),
                new("rollout", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 1, NextStateId: "rollout")
            ])),
        // Myte.cs: pair opens at different points in the cycle.
        // Toxic creates two temporary status cards directly in hand.
        new(MyteId, "Myte", 67, 0,
            [
                new("toxic", [new(
                    PrototypeEnemyEffectKind.AddCardsToHand, 2,
                    CardId: ToxicCardId)]),
                new("bite", [new(
                    PrototypeEnemyEffectKind.DamagePlayer, 13,
                    AscensionDeltas: [new(9, 2)])]),
                new("suck",
                [
                    new(PrototypeEnemyEffectKind.DamagePlayer, 4,
                        AscensionDeltas: [new(9, 2)]),
                    new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                        PowerId: "proto.power.strength",
                        AscensionDeltas: [new(9, 1)])
                ])
            ],
            MinHp: 61,
            HpAscensionDeltas: [new(8, 2)],
            MinHpAscensionDeltas: [new(8, 3)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("opening",
            [
                new("opening", PrototypeEnemyAiStateKind.Conditional,
                    ConditionalBranches:
                    [
                        new("toxic",
                            PrototypeEnemyAiConditionKind.SlotNameEquals,
                            "first"),
                        new("suck",
                            PrototypeEnemyAiConditionKind.SlotNameEquals,
                            "second")
                    ]),
                new("toxic", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 0, NextStateId: "bite"),
                new("bite", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 1, NextStateId: "suck"),
                new("suck", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 2, NextStateId: "toxic")
            ])),
        // Chomper.cs: each enemy has 2 Artifact.
        // ChompersNormal.cs sets ScreamFirst on the second instance,
        // represented by an internal slot label; the label does not
        // identify a new species and remains private to the formation.
        new(ChomperId, "Chomper", 64, 0,
            [
                new("clamp", [new(
                    PrototypeEnemyEffectKind.DamagePlayer, 8,
                    Repetitions: 2,
                    AscensionDeltas: [new(9, 1)])]),
                new("screech", [new(
                    PrototypeEnemyEffectKind.AddCardsToDiscard, 3,
                    CardId: "proto.status.dazed")])
            ],
            MinHp: 60,
            HpAscensionDeltas: [new(8, 3)],
            StartingPowers: [new("proto.power.artifact", 2)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("opening",
            [
                new("opening", PrototypeEnemyAiStateKind.Conditional,
                    ConditionalBranches:
                    [
                        new("clamp",
                            PrototypeEnemyAiConditionKind.SlotNameEquals,
                            "clamp-first"),
                        new("screech",
                            PrototypeEnemyAiConditionKind.SlotNameEquals,
                            "screech-first")
                    ]),
                new("clamp", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 0, NextStateId: "screech"),
                new("screech", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 1, NextStateId: "clamp")
            ]))
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        // SpinyToadNormal.cs: a single isolated Spiny Toad.
        new(SpinyToadNormalId, PrototypeRoomType.Combat,
            [SpinyToadId], MinAct: 2, MaxAct: 2, Weight: 0),
        // SlumberingBeetleNormal.cs: fixed named slots, not a
        // random worker selection. Rock and Silk accompany the
        // sleeping Beetle and can be targeted independently.
        new(SlumberingBeetleNormalId, PrototypeRoomType.Combat,
            [
                "proto.native.hive.bowlbug_rock",
                "proto.native.hive.bowlbug_silk",
                SlumberingBeetleId
            ],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new("proto.native.hive.bowlbug_rock", 0, "first"),
                new("proto.native.hive.bowlbug_silk", 1, "second"),
                new(SlumberingBeetleId, 2, "third")
            ]),
        // BowlbugsNormal.cs fixes Rock in "first" and independently
        // samples two DIFFERENT workers in "middle" and "last".
        // The pool order matches the source dictionary insertion order:
        // Egg, Silk, Nectar. This is not the BowlbugsWeak pool.
        new(BowlbugsNormalId, PrototypeRoomType.Combat,
            ["proto.native.hive.bowlbug_rock"],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new("proto.native.hive.bowlbug_rock", 0, "first")
            ],
            SelectionGroups:
            [
                new(
                    [
                        "proto.native.hive.bowlbug_egg",
                        "proto.native.hive.bowlbug_silk",
                        "proto.native.hive.bowlbug_nectar"
                    ],
                    [1, 2],
                    ChooseDistinct: true,
                    SlotNames: ["middle", "last"])
            ]),
        // ExoskeletonsNormal.cs adds a fourth Exoskeleton. Slot names
        // are native gameplay data: their first intents differ.
        // "fourth" follows a random branch with no extra enrage.
        new(ExoskeletonsNormalId, PrototypeRoomType.Combat,
            [
                "proto.native.hive.exoskeleton",
                "proto.native.hive.exoskeleton",
                "proto.native.hive.exoskeleton",
                "proto.native.hive.exoskeleton"
            ],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new("proto.native.hive.exoskeleton", 0, "first"),
                new("proto.native.hive.exoskeleton", 1, "second"),
                new("proto.native.hive.exoskeleton", 2, "third"),
                new("proto.native.hive.exoskeleton", 3, "fourth")
            ]),
        // MytesNormal.cs: two Mytes at native first/second slots,
        // with different starting moves and independent persistent AI.
        new(MytesNormalId, PrototypeRoomType.Combat,
            [MyteId, MyteId],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new(MyteId, 0, "first"),
                new(MyteId, 1, "second")
            ]),
        // ChompersNormal.cs has two identical species with different
        // initial move flags. Internal slot labels encode this initial
        // instance-local flag in the typed state machine.
        new(ChompersNormalId, PrototypeRoomType.Combat,
            [ChomperId, ChomperId],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new(ChomperId, 0, "clamp-first"),
                new(ChomperId, 1, "screech-first")
            ])
    ];
}
