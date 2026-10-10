namespace Sts2Emulator.Core;

/// <summary>
/// All remaining normal-encounter combat models from the pinned v0.111.0
/// decompiled source. Until native Act 2/3 routing is implemented, these
/// formations are explicitly forced-test-only with Weight=0.
/// </summary>
public static class PrototypeNativeRemainingNormals
{
    public const string AxebotId = "proto.native.glory.axebot";
    public const string AxebotOneStockId = "proto.native.glory.axebot_stock_one";
    public const string AxebotEmptyStockId = "proto.native.glory.axebot_empty_stock";
    public const string StockId = "proto.native.glory.stock";
    public const string FabricatorId = "proto.native.glory.fabricator";
    public const string ZapbotId = "proto.native.glory.zapbot";
    public const string StabbotId = "proto.native.glory.stabbot";
    public const string GuardbotId = "proto.native.glory.guardbot";
    public const string NoisebotId = "proto.native.glory.noisebot";
    public const string HighVoltageId = "proto.native.glory.high_voltage";
    public const string GlobeHeadId = "proto.native.glory.globe_head";
    public const string GalvanicId = "proto.native.glory.galvanic";
    public const string OwlMagistrateId = "proto.native.glory.owl_magistrate";
    public const string SoarId = "proto.native.glory.soar";
    public const string LostId = "proto.native.glory.the_lost";
    public const string ForgottenId = "proto.native.glory.the_forgotten";
    public const string PossessStrengthId = "proto.native.glory.possess_strength";
    public const string PossessSpeedId = "proto.native.glory.possess_speed";

    public const string AxebotsNormalId = "proto.native.glory.encounter.axebots_normal";
    public const string MenagerieNormalId = "proto.native.glory.encounter.construct_menagerie_normal";
    public const string FabricatorNormalId = "proto.native.glory.encounter.fabricator_normal";
    public const string GlobeHeadNormalId = "proto.native.glory.encounter.globe_head_normal";
    public const string OwlNormalId = "proto.native.glory.encounter.owl_magistrate_normal";
    public const string LostNormalId = "proto.native.glory.encounter.the_lost_and_forgotten_normal";
    public const string TunnelerNormalId = "proto.native.hive.encounter.tunneler_normal";

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new(StockId, "Stock", 0, []),
        new(HighVoltageId, "High Voltage", 0, [],
            EnemyStrengthAtSideTurnEndPerStack: 1),
        new(GalvanicId, "Galvanic", 0, []),
        new(SoarId, "Soar", 0, [],
            EnemyIncomingPoweredAttackDamageNumerator: 1,
            EnemyIncomingPoweredAttackDamageDenominator: 2,
            DoesNotStack: true),
        new(PossessStrengthId, "Possess Strength", 0, [],
            RestoreStolenPlayerPowerOnOwnerDeathId: "proto.power.strength",
            DoesNotStack: true),
        new(PossessSpeedId, "Possess Speed", 0, [],
            RestoreStolenPlayerPowerOnOwnerDeathId: "proto.power.dexterity",
            DoesNotStack: true)
    ];

    private static PrototypeEnemyDefinition Axebot(int stock)
    {
        var id = stock == 2 ? AxebotId :
            stock == 1 ? AxebotOneStockId : AxebotEmptyStockId;
        var initialState = stock == 2 ? "hammer" : "boot";
        return new PrototypeEnemyDefinition(id, "Axebot", 78 + 10 * (2-stock), 0,
        [
            new("boot_up",
            [
                new(PrototypeEnemyEffectKind.GainBlock, 10,
                    AscensionDeltas: [new(9, 5)]),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower,
                    3 * (2-stock),
                    PowerId: "proto.power.strength",
                    AscensionDeltas: [new(9, (2-stock))])
            ]),
            new("one_two", [new(PrototypeEnemyEffectKind.DamagePlayer,
                10, Repetitions: 2, AscensionDeltas: [new(9, 1)])]),
            new("hammer_uppercut",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 14,
                    AscensionDeltas: [new(9, 4)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                    PowerId: "proto.power.weak"),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                    PowerId: "proto.power.frail")
            ])
        ],
        MinHp: 70 + 10 * (2-stock),
        HpAscensionDeltas: [new(8, 8)],
        MinHpAscensionDeltas: [new(8, 6)],
        StartingPowers: stock > 0
            ? [new(StockId, stock)] : null,
        DeathSummons: stock > 0
            ? [new(stock == 2 ? AxebotOneStockId : AxebotEmptyStockId,
                0, "front")] : null,
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new(initialState,
        [
            new("boot", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "one_two"),
            new("one_two", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "hammer"),
            new("hammer", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "one_two")
        ]));
    }

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        Axebot(2),
        Axebot(1),
        Axebot(0),
        new(FabricatorId, "Fabricator", 150, 0,
        [
            new("fabricate",
            [
                new(PrototypeEnemyEffectKind.SummonEnemy, 0,
                    EnemyPool: [GuardbotId, NoisebotId],
                    SummonSlotNames: ["bot1", "bot2", "bot3", "bot4"]),
                new(PrototypeEnemyEffectKind.SummonEnemy, 0,
                    EnemyPool: [ZapbotId, StabbotId],
                    SummonSlotNames: ["bot1", "bot2", "bot3", "bot4"])
            ]),
            new("fabricating_strike",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 18,
                    AscensionDeltas: [new(9, 3)]),
                new(PrototypeEnemyEffectKind.SummonEnemy, 0,
                    EnemyPool: [ZapbotId, StabbotId],
                    SummonSlotNames: ["bot1", "bot2", "bot3", "bot4"])
            ]),
            new("disintegrate",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 11,
                    AscensionDeltas: [new(9, 2)])])
        ],
        HpAscensionDeltas: [new(8, 5)],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new("check",
        [
            new("check", PrototypeEnemyAiStateKind.Conditional,
                ConditionalBranches:
                [
                    new("random",
                        PrototypeEnemyAiConditionKind.LivingEnemiesAtMost,"4"),
                    new("disintegrate",
                        PrototypeEnemyAiConditionKind.LivingEnemiesGreaterThan,"4")
                ]),
            new("random", PrototypeEnemyAiStateKind.Random,
                Branches: [new("fabricate"), new("fabricating_strike")]),
            new("fabricate", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "check"),
            new("fabricating_strike", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "check"),
            new("disintegrate", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "check")
        ])),
        new(ZapbotId, "Zapbot", 23, 0,
            [new("zap", [new(PrototypeEnemyEffectKind.DamagePlayer, 14,
                AscensionDeltas: [new(9, 1)])])],
            MinHp: 18,
            HpAscensionDeltas: [new(8, 1)],
            MinHpAscensionDeltas: [new(8, 1)],
            StartingPowers: [new(HighVoltageId, 2)],
            IsMinion: true),
        new(StabbotId, "Stabbot", 23, 0,
            [new("stab",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 11,
                    AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 1,
                    PowerId: "proto.power.frail")
            ])],
            MinHp: 18,
            HpAscensionDeltas: [new(8, 1)],
            MinHpAscensionDeltas: [new(8, 1)],
            IsMinion: true),
        new(GuardbotId, "Guardbot", 20, 0,
            [new("guard", [new(
                PrototypeEnemyEffectKind.GiveBlockToEnemyType, 15,
                EnemyId: FabricatorId)])],
            MinHp: 16,
            HpAscensionDeltas: [new(8, 1)],
            MinHpAscensionDeltas: [new(8, 1)],
            IsMinion: true),
        new(NoisebotId, "Noisebot", 23, 0,
            [new("noise",
            [
                new(PrototypeEnemyEffectKind.AddCardsToDiscard, 1,
                    CardId: "proto.status.dazed"),
                new(PrototypeEnemyEffectKind.AddCardsToRandomDraw, 1,
                    CardId: "proto.status.dazed")
            ])],
            MinHp: 18,
            HpAscensionDeltas: [new(8, 1)],
            MinHpAscensionDeltas: [new(8, 1)],
            IsMinion: true),
        new(GlobeHeadId, "Globe Head", 148, 0,
        [
            new("shocking_slap",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 13,
                    AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 2,
                    PowerId: "proto.power.frail")
            ]),
            new("thunder_strike",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 6,
                    Repetitions: 3, AscensionDeltas: [new(9, 1)])]),
            new("galvanic_burst",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 16,
                    AscensionDeltas: [new(9, 1)]),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower, 2,
                    PowerId: "proto.power.strength")
            ])
        ],
        HpAscensionDeltas: [new(8, 10)],
        StartingPowers: [new(GalvanicId, 6,
            AscensionDeltas: [new(9, 2)])],
        MovePolicy: PrototypeEnemyMovePolicy.SequentialLoop),
        new(OwlMagistrateId, "Owl Magistrate", 231, 0,
        [
            new("scrutiny", [new(PrototypeEnemyEffectKind.DamagePlayer, 16,
                AscensionDeltas: [new(9, 1)])]),
            new("peck_assault",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 4,
                    Repetitions: 6)]),
            new("judicial_flight", [new(
                PrototypeEnemyEffectKind.ApplyEnemyPower, 1,
                PowerId: SoarId)]),
            new("verdict",
            [
                new(PrototypeEnemyEffectKind.DamagePlayer, 33,
                    AscensionDeltas: [new(9, 3)]),
                new(PrototypeEnemyEffectKind.ApplyPlayerPower, 4,
                    PowerId: "proto.power.vulnerable"),
                new(PrototypeEnemyEffectKind.RemoveEnemyPower, 0,
                    PowerId: SoarId)
            ])
        ], HpAscensionDeltas: [new(8, 16)]),
        new(LostId, "The Lost", 93, 0,
        [
            new("debilitating_smog",
                [new(PrototypeEnemyEffectKind.StealPlayerPower, 2,
                    PowerId: "proto.power.strength")]),
            new("eye_lasers",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 4,
                    Repetitions: 2, AscensionDeltas: [new(9, 1)])])
        ], HpAscensionDeltas: [new(8, 6)],
           StartingPowers: [new(PossessStrengthId, 1)]),
        new(ForgottenId, "The Forgotten", 106, 0,
        [
            new("miasma",
            [
                new(PrototypeEnemyEffectKind.StealPlayerPower, 2,
                    PowerId: "proto.power.dexterity"),
                new(PrototypeEnemyEffectKind.GainBlock, 8)
            ]),
            new("dread",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 13,
                    AscensionDeltas: [new(9, 2)],
                    OwnerPowerDamageBonusId: "proto.power.dexterity")])
        ], HpAscensionDeltas: [new(8, 5)],
            StartingPowers: [new(PossessSpeedId, 1)])
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(AxebotsNormalId, PrototypeRoomType.Combat,
            [AxebotId], MinAct: 3, MaxAct: 3, Weight: 0,
            Formation: [new(AxebotId, 0, "front")]),
        new(MenagerieNormalId, PrototypeRoomType.Combat,
            ["proto.enemy.punch_construct",
             "proto.enemy.cubex_construct",
             "proto.enemy.cubex_construct"],
            MinAct: 3, MaxAct: 3, Weight: 0),
        new(FabricatorNormalId, PrototypeRoomType.Combat,
            [FabricatorId], MinAct: 3, MaxAct: 3, Weight: 0,
            Formation: [new(FabricatorId, 2, "fabricator")]),
        new(GlobeHeadNormalId, PrototypeRoomType.Combat,
            [GlobeHeadId], MinAct: 3, MaxAct: 3, Weight: 0),
        new(OwlNormalId, PrototypeRoomType.Combat,
            [OwlMagistrateId], MinAct: 3, MaxAct: 3, Weight: 0),
        new(LostNormalId, PrototypeRoomType.Combat,
            [LostId, ForgottenId], MinAct: 3, MaxAct: 3, Weight: 0),
        // Pinned TunnelerNormal.cs exists, but Hive.GenerateAllEncounters
        // does NOT include it; keep this unreachable by native bags.
        new(TunnelerNormalId, PrototypeRoomType.Combat,
            [PrototypeNativeHiveNormals.ChomperId,
             "proto.native.hive.tunneler"],
            MinAct: 2, MaxAct: 2, Weight: 0,
            Formation:
            [
                new(PrototypeNativeHiveNormals.ChomperId, 0, "screech-first"),
                new("proto.native.hive.tunneler", 1)
            ])
    ];
}
