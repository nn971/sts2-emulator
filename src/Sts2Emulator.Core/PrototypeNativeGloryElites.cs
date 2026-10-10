namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 Glory elite combat fixtures, deliberately excluded from
/// configured Act 3 encounter selection pending native map/room integration.
/// Source: Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class PrototypeNativeGloryElites
{
    public const string SoulNexusId = "proto.native.glory.soul_nexus";
    public const string MechaKnightId = "proto.native.glory.mecha_knight";
    public const string SoulNexusEncounterId =
        "proto.native.glory.encounter.soul_nexus_elite";
    public const string MechaKnightEncounterId =
        "proto.native.glory.encounter.mecha_knight_elite";
    public const string BurnId = "proto.status.burn";

    // Burn.OnTurnEndInHand deals two unpowered damage. A status instance
    // is retained in combat piles until normal exhaust/discard handling.
    public static PrototypeCardDefinition[] Cards { get; } =
    [
        new(BurnId, "Burn", -1, PrototypeCardTarget.None, [],
            Rarity: PrototypeCardRarity.Status,
            Type: PrototypeCardType.Status,
            Unplayable: true, RewardEligible: false,
            EndTurnDamageIfInHand: 2,
            MaxUpgradeLevel: 0)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // Soul Nexus: Soul Burn is the forced opener. Every subsequent
        // decision chooses uniformly among all three moves except the
        // immediately previous move, with intent committed before Observe.
        new(SoulNexusId, "Soul Nexus", 234, 0,
        [
            new("soul_burn",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 29,
                    AscensionDeltas: [new(9, 2)])]),
            new("maelstrom",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 6,
                    Repetitions: 4,
                    AscensionDeltas: [new(9, 1)])]),
            new("drain_life",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 18,
                    AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                    PowerId: "proto.power.vulnerable"),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                    PowerId: "proto.power.weak")
            ])
        ],
        HpAscensionDeltas: [new(8, 20)],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("burn",
        [
            new("burn", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "random"),
            new("maelstrom", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "random"),
            new("drain", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "random"),
            new("random", PrototypeEnemyAiStateKind.Random,
                Branches:
                [
                    new("burn", RepeatRule:
                        PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("maelstrom", RepeatRule:
                        PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("drain", RepeatRule:
                        PrototypeEnemyAiRepeatRule.CannotRepeat)
                ])
        ])),
        // Mecha Knight: Charge once, then loop Flamethrower, Windup,
        // Heavy Cleave. Flamethrower deals an attack hit BEFORE putting
        // four unplayable Burn cards in the player's hand.
        new(MechaKnightId, "Mecha Knight", 300, 0,
        [
            new("charge",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 25,
                    AscensionDeltas: [new(9, 5)])]),
            new("flamethrower",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 8,
                    AscensionDeltas: [new(9, 4)]),
                new(PrototypeEnemyEffectKind.AddCardsToHand, 4,
                    CardId: BurnId)
            ]),
            new("windup",
            [
                new(PrototypeEnemyEffectKind.GainBlock, 15),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 5,
                    PowerId: "proto.power.strength")
            ]),
            new("heavy_cleave",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 35,
                    AscensionDeltas: [new(9, 5)])])
        ],
        StartingPowers: [new("proto.power.artifact", 3)],
        MoveLoopStartIndex: 1,
        HpAscensionDeltas: [new(8, 20)])
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(SoulNexusEncounterId, PrototypeRoomType.Elite,
            [SoulNexusId], MinAct: 3, MaxAct: 3, Weight: 0),
        new(MechaKnightEncounterId, PrototypeRoomType.Elite,
            [MechaKnightId], MinAct: 3, MaxAct: 3, Weight: 0)
    ];
}
