namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 Hive normal fights. Kept force-test-only until
/// native Hive encounter bags and map transitions are integrated.
/// </summary>
public static class PrototypeNativeHiveRemainingNormals
{
    public const string HunterKillerNormalId =
        "proto.native.hive.encounter.hunter_killer_normal";
    public const string LouseProgenitorNormalId =
        "proto.native.hive.encounter.louse_progenitor_normal";
    public const string HunterKillerId = "proto.native.hive.hunter_killer";
    public const string LouseProgenitorId =
        "proto.native.hive.louse_progenitor";
    public const string TenderId = "proto.native.hive.tender";
    public const string CurlUpId = "proto.native.hive.curl_up";

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        // TenderPower.AfterCardPlayed/AfterSideTurnEnd: reduce
        // Strength/Dexterity after each completed card, restoring only
        // reductions actually applied at the player side-turn end.
        new(TenderId, "Tender", 0, [],
            DoesNotStack: true, IsDebuff: true,
            PlayerStrengthDexterityLossPerCardPlayedPerStack: 1),
        // CurlUpPower.AfterDamageReceived/AfterCardPlayed: remember
        // the first powered card source and grant block on completion.
        new(CurlUpId, "Curl Up", 0, [],
            DoesNotStack: true,
            EnemyBlockAfterAttackingCardPlayedPerStack: 1)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // HunterKiller.cs: initial Tenderizing Goop, then a weighted
        // random Bite (cannot repeat) / Puncture (weight 2) branch.
        new(HunterKillerId, "Hunter Killer", 121, 0,
            [
                new("tenderizing_goop",
                    [new(PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                        PowerId: TenderId)]),
                new("bite",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 17,
                        AscensionDeltas: [new(9, 2)])]),
                new("puncture",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 7,
                        Repetitions: 3,
                        AscensionDeltas: [new(9, 1)])])
            ],
            HpAscensionDeltas: [new(8, 5)],
            MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
            Ai: new("goop",
            [
                new("goop", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 0, NextStateId: "random"),
                new("bite", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 1, NextStateId: "random"),
                new("puncture", PrototypeEnemyAiStateKind.Move,
                    MoveIndex: 2, NextStateId: "random"),
                new("random", PrototypeEnemyAiStateKind.Random,
                    Branches:
                    [
                        new("bite", RepeatRule:
                            PrototypeEnemyAiRepeatRule.CannotRepeat),
                        new("puncture", Weight: 2)
                    ])
            ])),
        // LouseProgenitor.cs: initial Curl Up and fixed
        // Web -> Curl and Grow -> Pounce -> Web cycle.
        new(LouseProgenitorId, "Louse Progenitor", 136, 0,
            [
                new("web_cannon",
                [
                    new(PrototypeEnemyEffectKind.DamagePlayer, 9,
                        AscensionDeltas: [new(9, 1)]),
                    new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                        PowerId: "proto.power.frail")
                ]),
                new("curl_and_grow",
                [
                    new(PrototypeEnemyEffectKind.GainBlock, 14,
                        AscensionDeltas: [new(8, 4)]),
                    new(PrototypeEnemyEffectKind.ApplyEnemyPower, 5,
                        PowerId: "proto.power.strength",
                        AscensionDeltas: [new(9, 2)])
                ]),
                new("pounce",
                    [new(PrototypeEnemyEffectKind.DamagePlayer, 14,
                        AscensionDeltas: [new(9, 2)])])
            ],
            MinHp: 134,
            HpAscensionDeltas: [new(8, 5)],
            MinHpAscensionDeltas: [new(8, 4)],
            StartingPowers:
            [
                new(CurlUpId, 14, AscensionDeltas: [new(8, 4)])
            ],
            MoveLoopStartIndex: 0)
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(HunterKillerNormalId, PrototypeRoomType.Combat,
            [HunterKillerId], MinAct: 2, MaxAct: 2, Weight: 0),
        new(LouseProgenitorNormalId, PrototypeRoomType.Combat,
            [LouseProgenitorId], MinAct: 2, MaxAct: 2, Weight: 0)
    ];
}
