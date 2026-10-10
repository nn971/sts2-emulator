namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 Glory normals, gated from native route generation.
/// Source: Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class PrototypeNativeGloryNormals
{
    public const string FrogKnightId = "proto.native.glory.frog_knight";
    public const string FrogKnightEncounterId =
        "proto.native.glory.encounter.frog_knight_normal";
    public const string SlimedBerserkerId =
        "proto.native.glory.slimed_berserker";
    public const string SlimedBerserkerEncounterId =
        "proto.native.glory.encounter.slimed_berserker_normal";

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // Tongue Lash -> Strike Down Evil -> For the Queen ->
        // Tongue Lash, except for one Beetle Charge below half HP.
        new(FrogKnightId, "Frog Knight", 191, 0,
        [
            new("tongue_lash",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 13,
                    AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                    PowerId: "proto.power.frail")
            ]),
            new("strike_down_evil",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 21,
                    AscensionDeltas: [new(9, 2)])]),
            new("for_the_queen",
                [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 5,
                    PowerId: "proto.power.strength")]),
            new("beetle_charge",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 35,
                    AscensionDeltas: [new(9, 5)])])
        ],
        HpAscensionDeltas: [new(8, 8)],
        StartingPowers:
        [
            new("proto.power.plating", 15,
                AscensionDeltas: [new(8, 4)])
        ],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("lash",
        [
            new("lash", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "strike"),
            new("strike", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "queen"),
            new("queen", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "half_health"),
            new("charge", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 3, NextStateId: "lash"),
            new("half_health", PrototypeEnemyAiStateKind.Conditional,
                ConditionalBranches:
                [
                    new("lash", PrototypeEnemyAiConditionKind.MoveUsedAtLeast,
                        "beetle_charge:1"),
                    new("lash", PrototypeEnemyAiConditionKind.HpAtLeastHalf),
                    new("charge", PrototypeEnemyAiConditionKind.HpBelowHalf)
                ])
        ])),
        // Vomit Ichor -> Furious Pummeling -> Leeching Hug -> Smother.
        new(SlimedBerserkerId, "Slimed Berserker", 261, 0,
        [
            new("vomit_ichor",
                [new(PrototypeEnemyEffectKind.AddCardsToDiscard, 10,
                    CardId: "proto.status.slimed")]),
            new("furious_pummeling",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 4,
                    Repetitions: 4,
                    AscensionDeltas: [new(9, 1)])]),
            new("leeching_hug",
            [
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 3,
                    PowerId: "proto.power.weak"),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 3,
                    PowerId: "proto.power.strength")
            ]),
            new("smother",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 30,
                    AscensionDeltas: [new(9, 3)])])
        ],
        HpAscensionDeltas: [new(8, 20)])
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(FrogKnightEncounterId, PrototypeRoomType.Combat,
            [FrogKnightId], MinAct: 3, MaxAct: 3, Weight: 0),
        new(SlimedBerserkerEncounterId, PrototypeRoomType.Combat,
            [SlimedBerserkerId], MinAct: 3, MaxAct: 3, Weight: 0)
    ];
}
