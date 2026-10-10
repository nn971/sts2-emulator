namespace Sts2Emulator.Core;

/// <summary>
/// Source-pinned v0.111.0 Glory Scrolls of Biting, gated to forced tests.
/// Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class PrototypeNativeGloryScrolls
{
    public const string ScrollId = "proto.native.glory.scroll_of_biting";
    public const string PaperCutsId = "proto.native.glory.paper_cuts";
    public const string WeakEncounterId =
        "proto.native.glory.encounter.scrolls_of_biting_weak";
    public const string NormalEncounterId =
        "proto.native.glory.encounter.scrolls_of_biting_normal";

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        // Native PaperCutsPower.AfterDamageGiven:
        // every unblocked powered attack HIT permanently reduces
        // the player's max HP by two; even a blocked attack does nothing.
        new(PaperCutsId, "Paper Cuts", 0, [],
            PlayerMaxHpLossOnUnblockedAttackHitPerStack: 1)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        new(ScrollId, "Scroll of Biting", 37, 0,
        [
            new("chomp",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 14,
                    AscensionDeltas: [new(9, 2)])]),
            new("chew",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 5,
                    Repetitions: 2,
                    AscensionDeltas: [new(9, 1)])]),
            new("more_teeth",
                [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                    PowerId: "proto.power.strength")])
        ],
        MinHp: 30,
        HpAscensionDeltas: [new(8, 2)],
        MinHpAscensionDeltas: [new(8, 3)],
        StartingPowers: [new(PaperCutsId, 2)],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("chomp",
        [
            new("chomp", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "more_teeth"),
            new("chew", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "random"),
            new("more_teeth", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "chew"),
            new("random", PrototypeEnemyAiStateKind.Random,
                Branches:
                [
                    new("chomp",
                        RepeatRule: PrototypeEnemyAiRepeatRule.CannotRepeat),
                    new("chew", Weight: 2)
                ])
        ]))
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        // Source: one encounter-local Rng.NextInt(3), then all three
        // distinct opening move phases are assigned in cyclic order.
        new(WeakEncounterId, PrototypeRoomType.Combat,
            [ScrollId, ScrollId, ScrollId],
            MinAct: 3, MaxAct: 3, Weight: 0,
            CyclicOpeningAiStateIds: ["chomp", "chew", "more_teeth"]),
        // Fourth scroll always opens at MORE_TEETH, independent of
        // the randomized three-monster cycle.
        new(NormalEncounterId, PrototypeRoomType.Combat,
            [ScrollId, ScrollId, ScrollId, ScrollId],
            MinAct: 3, MaxAct: 3, Weight: 0,
            CyclicOpeningAiStateIds: ["chomp", "chew", "more_teeth"],
            FixedTrailingOpeningAiStateIds: ["more_teeth"])
    ];
}
