namespace Sts2Emulator.Core;

/// <summary>
/// Source-pinned Glory normal enemy slice. Synthetic fixtures only until
/// the native Act 3 map/encounter selector is complete.
/// Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20.
/// </summary>
public static class PrototypeNativeGloryNewNormals
{
    public const string OwlId = "proto.native.glory.owl_magistrate";
    public const string OwlEncounterId =
        "proto.native.glory.encounter.owl_magistrate_normal";
    public const string SoarId = "proto.native.glory.soar";
    public const string GlobeHeadId = "proto.native.glory.globe_head";
    public const string GalvanicId = "proto.native.glory.galvanic";
    public const string GlobeHeadEncounterId =
        "proto.native.glory.encounter.globe_head_normal";
    public const string AxebotId = "proto.native.glory.axebot";
    public const string AxebotSecondId = "proto.native.glory.axebot_respawn_1";
    public const string AxebotThirdId = "proto.native.glory.axebot_respawn_2";
    public const string StockId = "proto.native.glory.stock";
    public const string AxebotsEncounterId =
        "proto.native.glory.encounter.axebots_normal";
    public const string TheLostId = "proto.native.glory.the_lost";
    public const string TheForgottenId = "proto.native.glory.the_forgotten";
    public const string PossessStrengthId =
        "proto.native.glory.possess_strength";
    public const string PossessSpeedId =
        "proto.native.glory.possess_speed";
    public const string LostAndForgottenEncounterId =
        "proto.native.glory.encounter.the_lost_and_forgotten_normal";

    public static PrototypePowerDefinition[] Powers { get; } =
    [
        // SoarPower multiplies powered attack damage dealt to its
        // owner by 0.5. Poison and non-attack loss are unaffected.
        new(SoarId, "Soar", 0, [],
            DoesNotStack: true,
            EnemyIncomingPoweredAttackNumerator: 1,
            EnemyIncomingPoweredAttackDenominator: 2),
        new(GalvanicId, "Galvanic", 0, [],
            GalvanizePowerCardsPerStack: 1),
        // Death/summon semantics are carried by typed stage definitions;
        // this power retains the visible Stock counter.
        new(StockId, "Stock", 0, []),
        new(PossessStrengthId, "Possess Strength", 0, [],
            DoesNotStack: true),
        new(PossessSpeedId, "Possess Speed", 0, [],
            DoesNotStack: true)
    ];

    // Native Axebot respawns twice: remaining Stock 2 → 1 → 0,
    // each replacement with +10 max HP and a Boot Up opener.
    // The original opens with Hammer Uppercut rather than Boot Up.
    private static PrototypeEnemyDefinition AxebotStage(
        string id, int respawnsUsed) =>
        new(id, "Axebot", 78 + 10 * respawnsUsed, 0,
        [
            new("boot_up",
            [
                new(PrototypeEnemyEffectKind.GainBlock, 10,
                    AscensionDeltas: [new(9, 5)]),
                new(PrototypeEnemyEffectKind.ApplyEnemyPower,
                    respawnsUsed * 3,
                    PowerId: "proto.power.strength",
                    AscensionDeltas: [new(9, respawnsUsed)])
            ]),
            new("one_two", [new(PrototypeEnemyEffectKind.DamagePlayer,
                10, Repetitions: 2,
                AscensionDeltas: [new(9, 1)])]),
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
        MinHp: 70 + 10 * respawnsUsed,
        HpAscensionDeltas: [new(8, 8)],
        MinHpAscensionDeltas: [new(8, 6)],
        StartingPowers: respawnsUsed < 2
            ? [new(StockId, 2 - respawnsUsed)]
            : [],
        MovePolicy: PrototypeEnemyMovePolicy.StateMachine,
        Ai: new(respawnsUsed == 0 ? "uppercut" : "boot",
        [
            new("boot", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 0, NextStateId: "uppercut"),
            new("one_two", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 1, NextStateId: "uppercut"),
            new("uppercut", PrototypeEnemyAiStateKind.Move,
                MoveIndex: 2, NextStateId: "one_two")
        ]),
        DeathSummons: respawnsUsed < 2
            ? [new(respawnsUsed == 0 ? AxebotSecondId : AxebotThirdId,
                0, SlotName: "front", SkipEnemyActions: 0)]
            : null);

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        // Scrutiny -> six-hit Peck -> take flight / Soar ->
        // Verdict / Vulnerable 4 / remove Soar, looping.
        new(OwlId, "Owl Magistrate", 231, 0,
        [
            new("scrutiny",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 16,
                    AscensionDeltas: [new(9, 1)])]),
            new("peck_assault",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 4,
                    Repetitions: 6)]),
            new("judicial_flight",
                [new(PrototypeEnemyEffectKind.ApplyEnemyPower, 1,
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
        ],
        HpAscensionDeltas: [new(8, 16)]),
        // GlobeHead.cs: Shocking Slap -> Thunder Strike ->
        // Galvanic Burst -> Shocking Slap. Galvanic inflicts
        // Power cards and damages the player on their play.
        // Lost opens with Strength theft then uses two Eye Lasers.
        new(TheLostId, "The Lost", 93, 0,
        [
            new("debilitating_smog",
                [new(PrototypeEnemyEffectKind.StealPlayerPower, 2,
                    PowerId: "proto.power.strength")]),
            new("eye_lasers",
                [new(PrototypeEnemyEffectKind.DamagePlayer, 4,
                    Repetitions: 2,
                    AscensionDeltas: [new(9, 1)])])
        ],
        HpAscensionDeltas: [new(8, 6)],
        StartingPowers: [new(PossessStrengthId, 1)]),
        // Forgotten's Dread scales by its current Dexterity,
        // unlike ordinary attacks, where Dexterity modifies Block.
        new(TheForgottenId, "The Forgotten", 106, 0,
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
                    ExtraAmountFromOwnerPowerId:
                        "proto.power.dexterity")])
        ],
        HpAscensionDeltas: [new(8, 5)],
        StartingPowers: [new(PossessSpeedId, 1)]),
        AxebotStage(AxebotId, 0),
        AxebotStage(AxebotSecondId, 1),
        AxebotStage(AxebotThirdId, 2),
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
                    Repetitions: 3,
                    AscensionDeltas: [new(9, 1)])]),
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
            AscensionDeltas: [new(9, 2)])])
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(OwlEncounterId, PrototypeRoomType.Combat,
            [OwlId], MinAct: 3, MaxAct: 3, Weight: 0),
        new(GlobeHeadEncounterId, PrototypeRoomType.Combat,
            [GlobeHeadId], MinAct: 3, MaxAct: 3, Weight: 0),
        new(AxebotsEncounterId, PrototypeRoomType.Combat,
            [AxebotId], MinAct: 3, MaxAct: 3, Weight: 0,
            Formation: [new(AxebotId, 0, "front")]),
        new(LostAndForgottenEncounterId, PrototypeRoomType.Combat,
            [TheLostId, TheForgottenId],
            MinAct: 3, MaxAct: 3, Weight: 0,
            Formation: [new(TheLostId, 0), new(TheForgottenId, 1)])
    ];
}
