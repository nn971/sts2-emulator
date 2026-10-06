namespace Sts2Emulator.Core;

public static class PrototypeContent
{
    public const string CharacterId = "silent";
    public const string RulesetId = "prototype-silent-v0";

    public static PrototypeRuleset Rules { get; } = new(
        Id: RulesetId,
        Acts: 3,
        FloorsPerAct: 6,
        StartingHp: 70,
        StartingGold: 99,
        PotionSlots: 2,
        BaseEnergy: 3,
        HandSize: 5,
        RestHealPercent: 30,
        RestTrainMaxHp: 4,
        MapFloorRules:
        [
            new(
                MinFloor: 1,
                MaxFloor: 1,
                RoomPool:
                [
                    PrototypeRoomType.Combat
                ]),
            new(
                MinFloor: 2,
                MaxFloor: 2,
                RoomPool:
                [
                    PrototypeRoomType.Combat,
                    PrototypeRoomType.Combat,
                    PrototypeRoomType.Event,
                    PrototypeRoomType.Shop
                ]),
            new(
                MinFloor: 3,
                MaxFloor: 3,
                RoomPool:
                [
                    PrototypeRoomType.Combat,
                    PrototypeRoomType.Combat,
                    PrototypeRoomType.Event,
                    PrototypeRoomType.Elite
                ]),
            new(
                MinFloor: 4,
                MaxFloor: 4,
                RoomPool:
                [
                    PrototypeRoomType.Combat,
                    PrototypeRoomType.Event,
                    PrototypeRoomType.Shop,
                    PrototypeRoomType.Rest,
                    PrototypeRoomType.Elite
                ]),
            new(
                MinFloor: 5,
                MaxFloor: 5,
                RoomPool:
                [
                    PrototypeRoomType.Rest
                ],
                AllowDuplicateSpecialRooms: true),
            new(
                MinFloor: 6,
                MaxFloor: 6,
                RoomPool:
                [
                    PrototypeRoomType.Boss
                ])
        ],
        EndTurnPipeline:
        [
            new(
                PrototypeAutomaticStepKind.DispatchCombatEvent,
                EventKind: PrototypeCombatEventKind.PlayerTurnEnded),
            new(PrototypeAutomaticStepKind.DiscardPlayerHand),
            new(PrototypeAutomaticStepKind.DispatchEnemyStatusStage, PrototypeTurnStage.EnemyTurnStart),
            new(PrototypeAutomaticStepKind.ResetEnemyBlock),
            new(PrototypeAutomaticStepKind.ResolveEnemyActions),
            new(PrototypeAutomaticStepKind.DispatchEnemyStatusStage, PrototypeTurnStage.EnemyTurnEnd),
            new(PrototypeAutomaticStepKind.AdvanceTurn),
            new(PrototypeAutomaticStepKind.ResetPlayerBlock),
            new(PrototypeAutomaticStepKind.RefreshPlayerEnergy),
            new(PrototypeAutomaticStepKind.DrawPlayerHand),
            new(
                PrototypeAutomaticStepKind.DispatchCombatEvent,
                EventKind: PrototypeCombatEventKind.PlayerTurnStarted)
        ]);

    public static IReadOnlyDictionary<string, PrototypeCardDefinition> Cards { get; } =
        new[]
        {
            new PrototypeCardDefinition(
                "proto.silent.strike",
                "Strike",
                1,
                PrototypeCardTarget.Enemy,
                [new(PrototypeCombatEffectKind.DamageEnemy, 6, 3)],
                Rarity: PrototypeCardRarity.Basic,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.defend",
                "Defend",
                1,
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.GainPlayerBlock, 5, 3)],
                Rarity: PrototypeCardRarity.Basic,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.neutralize",
                "Neutralize",
                0,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 3, 1),
                    new(PrototypeCombatEffectKind.ApplyEnemyStatus, 1, 1, "proto.status.weak")
                ],
                Rarity: PrototypeCardRarity.Basic,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.survivor",
                "Survivor",
                1,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.GainPlayerBlock, 8, 3),
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.Hand,
                            1,
                            1,
                            PrototypeCardSelectionResolutionKind.MoveToDiscard))
                ],
                Rarity: PrototypeCardRarity.Basic,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.backflip",
                "Backflip",
                1,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.GainPlayerBlock, 5, 3),
                    new(PrototypeCombatEffectKind.DrawCards, 2)
                ],
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.poisoned_stab",
                "Poisoned Stab",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 6, 2),
                    new(PrototypeCombatEffectKind.ApplyEnemyStatus, 3, 2, "proto.status.poison")
                ],
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.slice",
                "Slice",
                0,
                PrototypeCardTarget.Enemy,
                [new(PrototypeCombatEffectKind.DamageEnemy, 6, 3)],
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.acrobatics",
                "Acrobatics",
                1,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.DrawCards, 3, 1),
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.Hand,
                            1,
                            1,
                            PrototypeCardSelectionResolutionKind.MoveToDiscard))
                ],
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.expertise",
                "Expertise",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DrawCards,
                        2,
                        1,
                        DrawnCardKeyword: new(
                            PrototypeCardKeyword.Retain,
                            Expiry:
                                PrototypeCardKeywordOverrideExpiry.EndOfTurn))
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),

            new PrototypeCardDefinition(
                "proto.silent.shiv",
                "Shiv",
                0,
                PrototypeCardTarget.Enemy,
                [new(PrototypeCombatEffectKind.DamageEnemy, 4)],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Token,
                RewardEligible: false,
                Type: PrototypeCardType.Attack,
                Tags: ["Shiv"]),
            new PrototypeCardDefinition(
                "proto.status.dazed",
                "Dazed",
                -1,
                PrototypeCardTarget.None,
                [],
                Rarity: PrototypeCardRarity.Status,
                Ethereal: true,
                Unplayable: true,
                RewardEligible: false,
                Type: PrototypeCardType.Status),
            new PrototypeCardDefinition(
                "proto.curse.ascenders_bane",
                "Ascender's Bane",
                -1,
                PrototypeCardTarget.None,
                [],
                Rarity: PrototypeCardRarity.Curse,
                Ethereal: true,
                Unplayable: true,
                Eternal: true,
                RewardEligible: false,
                Type: PrototypeCardType.Curse),
            new PrototypeCardDefinition(
                "proto.silent.blade_dance",
                "Blade Dance",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.CreateCardsInHand,
                        3,
                        1,
                        CardId: "proto.silent.shiv")
                ],
                ExhaustOnUse: true,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.dagger_spray",
                "Dagger Spray",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        4,
                        2,
                        Target: PrototypeEffectTarget.AllEnemies,
                        Repetitions: 2)
                ],
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.deadly_poison",
                "Deadly Poison",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        5,
                        2,
                        StatusId: "proto.status.poison")
                ],
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.mirage",
                "Mirage",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.GainPlayerBlockFromEnemyStatusTotal,
                        0,
                        StatusId: "proto.status.poison")
                ],
                ExhaustOnUse: true,
                LoseExhaustOnUpgrade: true,
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.bouncing_flask",
                "Bouncing Flask",
                2,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        3,
                        StatusId: "proto.status.poison",
                        Target: PrototypeEffectTarget.RandomEnemy,
                        Repetitions: 3,
                        RepetitionUpgradeDelta: 1)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.quick_slash",
                "Quick Slash",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 8, 3),
                    new(PrototypeCombatEffectKind.DrawCards, 1)
                ],
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.dagger_throw",
                "Dagger Throw",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 9, 3),
                    new(PrototypeCombatEffectKind.DrawCards, 1),
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.Hand,
                            1,
                            1,
                            PrototypeCardSelectionResolutionKind.MoveToDiscard))
                ],
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.cloak_and_dagger",
                "Cloak and Dagger",
                1,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.GainPlayerBlock, 6, 3),
                    new(
                        PrototypeCombatEffectKind.CreateCardsInHand,
                        1,
                        1,
                        CardId: "proto.silent.shiv")
                ],
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.deflect",
                "Deflect",
                0,
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.GainPlayerBlock, 4, 3)],
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.sucker_punch",
                "Sucker Punch",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 8, 2),
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        1,
                        1,
                        StatusId: "proto.status.weak")
                ],
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.dash",
                "Dash",
                2,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 10, 3),
                    new(PrototypeCombatEffectKind.GainPlayerBlock, 10, 3)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.leg_sweep",
                "Leg Sweep",
                2,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.GainPlayerBlock, 11, 3),
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        2,
                        1,
                        StatusId: "proto.status.weak")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.skewer",
                "Skewer",
                new PrototypeCardCostSpec(PrototypeCardCostKind.X),
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        8,
                        3,
                        Repetitions: 0,
                        RepetitionsPerEnergySpent: 1)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.footwork",
                "Footwork",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        2,
                        1,
                        PowerId: "proto.power.dexterity")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.accuracy",
                "Accuracy",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        4,
                        2,
                        PowerId: "proto.power.accuracy")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.phantom_blades",
                "Phantom Blades",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        9,
                        3,
                        PowerId: "proto.power.phantom_blades")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.tracking",
                "Tracking",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    2,
                    -1),
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.tracking")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.noxious_fumes",
                "Noxious Fumes",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        1,
                        PowerId: "proto.power.noxious_fumes")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.infinite_blades",
                "Infinite Blades",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.infinite_blades")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                InnateOnUpgrade: true,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.suppress",
                "Suppress",
                0,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        11,
                        6),
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        3,
                        2,
                        StatusId: "proto.status.weak")
                ],
                Rarity: PrototypeCardRarity.Ancient,
                Innate: true,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.anticipate",
                "Anticipate",
                0,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        2,
                        2,
                        PowerId: "proto.power.temporary_dexterity")
                ],
                Rarity: PrototypeCardRarity.Common,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.corrosive_wave",
                "Corrosive Wave",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        2,
                        1,
                        PowerId: "proto.power.corrosive_wave")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.speedster",
                "Speedster",
                2,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        2,
                        PowerId: "proto.power.speedster")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                InnateOnUpgrade: true,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.serpent_form",
                "Serpent Form",
                3,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        4,
                        2,
                        PowerId: "proto.power.serpent_form")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.envenom",
                "Envenom",
                2,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        0,
                        PowerId: "proto.power.envenom")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.afterimage",
                "Afterimage",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        0,
                        PowerId: "proto.power.afterimage")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.master_planner",
                "Master Planner",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    2,
                    -1),
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.master_planner")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.prepared",
                "Prepared",
                0,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.DrawCards, 1, 1),
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.Hand,
                            1,
                            1,
                            PrototypeCardSelectionResolutionKind.MoveToDiscard))
                ],
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.hand_trick",
                "Hand Trick",
                1,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.GainPlayerBlock, 7, 3),
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.Hand,
                            1,
                            1,
                            PrototypeCardSelectionResolutionKind.Preserve,
                            RequiredCardType: PrototypeCardType.Skill),
                        SelectedCardKeyword: new(
                            PrototypeCardKeyword.Sly,
                            Expiry:
                                PrototypeCardKeywordOverrideExpiry.EndOfTurn))
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),

            new PrototypeCardDefinition(
                "proto.silent.concentrate",
                "Concentrate",
                0,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.Hand,
                            3,
                            3,
                            PrototypeCardSelectionResolutionKind.MoveToDiscard)),
                    new(PrototypeCombatEffectKind.GainEnergy, 2, 1)
                ],
                Rarity: PrototypeCardRarity.Uncommon),
            new PrototypeCardDefinition(
                "proto.silent.reflex",
                "Reflex",
                3,
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.DrawCards, 2, 1)],
                Rarity: PrototypeCardRarity.Uncommon,
                Sly: true,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.tactician",
                "Tactician",
                3,
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.GainEnergy, 1, 1)],
                Rarity: PrototypeCardRarity.Uncommon,
                Sly: true,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.untouchable",
                "Untouchable",
                2,
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.GainPlayerBlock, 6, 3)],
                Rarity: PrototypeCardRarity.Common,
                Sly: true,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.flick_flack",
                "Flick-Flack",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        7,
                        2,
                        Target: PrototypeEffectTarget.AllEnemies)
                ],
                Rarity: PrototypeCardRarity.Common,
                Sly: true,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.abrasive",
                "Abrasive",
                3,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.dexterity"),
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        4,
                        2,
                        PowerId: "proto.power.thorns")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Sly: true,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.ricochet",
                "Ricochet",
                2,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        3,
                        Target: PrototypeEffectTarget.RandomEnemy,
                        Repetitions: 4,
                        RepetitionUpgradeDelta: 1)
                ],
                Rarity: PrototypeCardRarity.Common,
                Sly: true,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.hidden_daggers",
                "Hidden Daggers",
                0,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.Hand,
                            2,
                            2,
                            PrototypeCardSelectionResolutionKind.MoveToDiscard)),
                    new(
                        PrototypeCombatEffectKind.CreateCardsInHand,
                        2,
                        CardId: "proto.silent.shiv",
                        GeneratedCardUpgradePerSourceUpgrade: 1)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.memento_mori",
                "Memento Mori",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        9,
                        2,
                        CountKind: PrototypeCombatCountKind.CardsDiscardedThisTurn,
                        AmountPerCount: 4,
                        AmountPerCountUpgradeDelta: 1)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.murder",
                "Murder",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    3,
                    UpgradeDelta: -1),
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        1,
                        CountKind: PrototypeCombatCountKind.CardsDrawnThisCombat,
                        AmountPerCount: 1)
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.precise_cut",
                "Precise Cut",
                0,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        13,
                        3,
                        CountKind: PrototypeCombatCountKind.OtherCardsInHand,
                        AmountPerCount: -2)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.pinpoint",
                "Pinpoint",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    3,
                    ReductionCountKind:
                        PrototypeCombatCountKind.SkillsPlayedThisTurn,
                    ReductionPerCount: 1,
                    MinimumAmount: 0),
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        15,
                        4)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.tools_of_the_trade",
                "Tools of the Trade",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    1,
                    UpgradeDelta: -1),
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.tools_of_the_trade")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.burst",
                "Burst",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        1,
                        PowerId: "proto.power.burst")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.nightmare",
                "Nightmare",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    3,
                    UpgradeDelta: -1),
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.Hand,
                            1,
                            1,
                            PrototypeCardSelectionResolutionKind.Preserve),
                        SelectedCardPower: new(
                            "proto.power.nightmare",
                            3))
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.bullet_time",
                "Bullet Time",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    3,
                    UpgradeDelta: -1),
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.SetHandCardsEnergyCostUntilTurnEndOrPlayed,
                        0),
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.no_draw")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.up_my_sleeve",
                "Up My Sleeve",
                2,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.CreateCardsInHand,
                        3,
                        1,
                        CardId: "proto.silent.shiv"),
                    new(
                        PrototypeCombatEffectKind.ModifySourceCardEnergyCost,
                        -1)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.pounce",
                "Pounce",
                2,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        14,
                        6),
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.free_next_skill")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.finisher",
                "Finisher",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        6,
                        2,
                        Repetitions: 0,
                        CountKind: PrototypeCombatCountKind.AttacksPlayedThisTurn,
                        RepetitionsPerCount: 1)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.flechettes",
                "Flechettes",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        5,
                        2,
                        Repetitions: 0,
                        CountKind: PrototypeCombatCountKind.SkillsInHand,
                        RepetitionsPerCount: 1)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.bubble_bubble",
                "Bubble Bubble",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        9,
                        3,
                        StatusId: "proto.status.poison",
                        Condition: new(
                            PrototypeCombatPredicateKind.TargetHasStatus,
                            "proto.status.poison"))
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.haze",
                "Haze",
                2,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        4,
                        2,
                        StatusId: "proto.status.poison",
                        Target: PrototypeEffectTarget.AllEnemies),
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        1,
                        1,
                        StatusId: "proto.status.weak",
                        Target: PrototypeEffectTarget.AllEnemies)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.leading_strike",
                "Leading Strike",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 3, 3),
                    new(
                        PrototypeCombatEffectKind.CreateCardsInHand,
                        2,
                        CardId: "proto.silent.shiv")
                ],
                Rarity: PrototypeCardRarity.Common,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.assassinate",
                "Assassinate",
                0,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        10,
                        3),
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        1,
                        1,
                        StatusId: "proto.status.vulnerable")
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Rare,
                Innate: true,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.backstab",
                "Backstab",
                0,
                PrototypeCardTarget.Enemy,
                [new(PrototypeCombatEffectKind.DamageEnemy, 11, 4)],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Uncommon,
                Innate: true,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.snakebite",
                "Snakebite",
                2,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        7,
                        3,
                        StatusId: "proto.status.poison")
                ],
                Rarity: PrototypeCardRarity.Common,
                Retain: true,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.adrenaline",
                "Adrenaline",
                0,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.GainEnergy, 1, 1),
                    new(PrototypeCombatEffectKind.DrawCards, 2)
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.catalyst",
                "Catalyst",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.MultiplyEnemyStatus,
                        2,
                        1,
                        StatusId: "proto.status.poison")
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Uncommon),
            new PrototypeCardDefinition(
                "proto.silent.escape_plan",
                "Escape Plan",
                0,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.DrawCards, 1),
                    new(PrototypeCombatEffectKind.GainPlayerBlock, 3, 2)
                ],
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.piercing_wail",
                "Piercing Wail",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        2,
                        1,
                        StatusId: "proto.status.weak",
                        Target: PrototypeEffectTarget.AllEnemies)
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.crippling_cloud",
                "Crippling Cloud",
                2,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        4,
                        3,
                        StatusId: "proto.status.poison",
                        Target: PrototypeEffectTarget.AllEnemies),
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        2,
                        0,
                        StatusId: "proto.status.weak",
                        Target: PrototypeEffectTarget.AllEnemies)
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Uncommon),
            new PrototypeCardDefinition(
                "proto.silent.grand_finale",
                "Grand Finale",
                0,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        60,
                        15,
                        Target: PrototypeEffectTarget.AllEnemies)
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Attack,
                PlayCondition: new(
                    PrototypeCombatPredicateKind.DrawPileEmpty)),
            new PrototypeCardDefinition(
                "proto.silent.die_die_die",
                "Die Die Die",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        13,
                        4,
                        Target: PrototypeEffectTarget.AllEnemies)
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.blur",
                "Blur",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.GainPlayerBlock,
                        5,
                        3),
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.blur")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.dodge_and_roll",
                "Dodge and Roll",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.GainPlayerBlockAndApplyPowerFromActualGain,
                        4,
                        2,
                        PowerId: "proto.power.block_next_turn")
                ],
                Rarity: PrototypeCardRarity.Common,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.sidestep",
                "Sidestep",
                0,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        1,
                        PowerId: "proto.power.energy_next_turn")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.predator",
                "Predator",
                2,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 15, 5),
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        2,
                        PowerId: "proto.power.draw_cards_next_turn")
                ],
                Rarity: PrototypeCardRarity.Common,
                Type: PrototypeCardType.Attack)
        }.ToDictionary(card => card.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PrototypePotionDefinition> Potions { get; } =
        new[]
        {
            new PrototypePotionDefinition(
                "proto.potion.block",
                "Block Potion",
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.GainPlayerBlock, 12)]),
            new PrototypePotionDefinition(
                "proto.potion.fire",
                "Fire Potion",
                PrototypeCardTarget.Enemy,
                [new(PrototypeCombatEffectKind.DamageEnemy, 20)]),
            new PrototypePotionDefinition(
                "proto.potion.poison",
                "Poison Potion",
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        6,
                        StatusId: "proto.status.poison")
                ]),
            new PrototypePotionDefinition(
                "proto.potion.swift",
                "Swift Potion",
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.DrawCards, 3)]),
            new PrototypePotionDefinition(
                "proto.potion.weak",
                "Weak Potion",
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        3,
                        StatusId: "proto.status.weak")
                ]),
            new PrototypePotionDefinition(
                "proto.potion.dexterity",
                "Dexterity Potion",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        2,
                        PowerId: "proto.power.dexterity")
                ]),
            new PrototypePotionDefinition(
                "proto.potion.explosive",
                "Explosive Potion",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        10,
                        Target: PrototypeEffectTarget.AllEnemies)
                ]),
            new PrototypePotionDefinition(
                "proto.potion.energy",
                "Energy Potion",
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.GainEnergy, 2)])
        }.ToDictionary(potion => potion.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PrototypeRelicDefinition> Relics { get; } =
        new[]
        {
            new PrototypeRelicDefinition(
                "proto.relic.silent_ring",
                "Ring of the Snake",
                FirstTurnDrawBonus: 2),
            new PrototypeRelicDefinition(
                "proto.relic.lantern",
                "Lantern",
                EnergyPerTurnBonus: 1),
            new PrototypeRelicDefinition(
                "proto.relic.ink_bottle",
                "Ink Bottle",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.DrawCards,
                                1)
                        ],
                        EveryNth: 3)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.bag_of_preparation",
                "Bag of Preparation",
                FirstTurnDrawBonus: 2),
            new PrototypeRelicDefinition(
                "proto.relic.ornamental_fan",
                "Ornamental Fan",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [new PrototypeCombatEffectSpec(
                            PrototypeCombatEffectKind.GainPlayerBlock,
                            4)],
                        EveryNth: 3)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.nunchaku",
                "Nunchaku",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [new PrototypeCombatEffectSpec(
                            PrototypeCombatEffectKind.GainEnergy,
                            1)],
                        EveryNth: 4)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.letter_opener",
                "Letter Opener",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.DamageEnemy,
                                5,
                                Target: PrototypeEffectTarget.AllEnemies)
                        ],
                        EveryNth: 3)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.happy_flower",
                "Happy Flower",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnStarted,
                        [new PrototypeCombatEffectSpec(
                            PrototypeCombatEffectKind.GainEnergy,
                            1)],
                        EveryNth: 3)
                ])
        }.ToDictionary(relic => relic.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PrototypePowerDefinition> Powers { get; } =
        new[]
        {
            new PrototypePowerDefinition(
                "proto.power.dexterity",
                "Dexterity",
                BlockBonusPerStack: 1,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                AllowNegative: true),
            new PrototypePowerDefinition(
                "proto.power.accuracy",
                "Accuracy",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                AttackDamageBonusRequiredCardTag: "Shiv",
                AttackDamageBonusPerStack: 1),
            new PrototypePowerDefinition(
                "proto.power.phantom_blades",
                "Phantom Blades",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                GrantedCardKeyword: PrototypeCardKeyword.Retain,
                GrantedCardKeywordRequiredCardTag: "Shiv",
                FirstAttackDamageBonusRequiredCardTag: "Shiv",
                FirstAttackDamageBonusPerStack: 1),
            new PrototypePowerDefinition(
                "proto.power.tracking",
                "Tracking",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                AttackDamageBonusRequiredTargetStatus: "proto.status.weak",
                AttackDamageBonusTargetNumeratorPerStack: 1,
                AttackDamageBonusTargetDenominator: 2),
            new PrototypePowerDefinition(
                "proto.power.noxious_fumes",
                "Noxious Fumes",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnStarted,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ApplyEnemyStatus,
                                0,
                                StatusId: "proto.status.poison",
                                Target: PrototypeEffectTarget.AllEnemies,
                                AmountPerPowerStack: 1)
                        ])
                ]),
            new PrototypePowerDefinition(
                "proto.power.afterimage",
                "Afterimage",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.GainPlayerBlock,
                                0,
                                AmountPerPowerStack: 1)
                        ])
                ]),
            new PrototypePowerDefinition(
                "proto.power.master_planner",
                "Master Planner",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ModifyEventSourceCardKeyword,
                                0,
                                EventSourceCardKeyword: new(
                                    PrototypeCardKeyword.Sly,
                                    Expiry:
                                        PrototypeCardKeywordOverrideExpiry.None))
                        ],
                        RequiredSourceCardType: PrototypeCardType.Skill)
                ]),
            new PrototypePowerDefinition(
                "proto.power.infinite_blades",
                "Infinite Blades",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.BeforeHandDraw,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.CreateCardsInHand,
                                0,
                                CardId: "proto.silent.shiv",
                                AmountPerPowerStack: 1)
                        ])
                ]),
            new PrototypePowerDefinition(
                "proto.power.nightmare",
                "Nightmare",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.BeforeHandDraw,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.CreateCardsInHandFromPowerCardPayload,
                                0,
                                AmountPerPowerStack: 1)
                        ],
                        RemoveSourcePowerAfterTrigger: true)
                ],
                IsInstanced: true,
                RequiresCardPayload: true),
            new PrototypePowerDefinition(
                "proto.power.no_draw",
                "No Draw",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                RemoveAtPlayerTurnEnd: true,
                PreventsAdditionalDraw: true),
            new PrototypePowerDefinition(
                "proto.power.temporary_dexterity",
                "Temporary Dexterity",
                BlockBonusPerStack: 1,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                AllowNegative: true,
                RemoveAtPlayerTurnEnd: true),
            new PrototypePowerDefinition(
                "proto.power.tools_of_the_trade",
                "Tools of the Trade",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnStarted,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ChooseCards,
                                0,
                                Selection: new(
                                    PrototypeCardZone.Hand,
                                    0,
                                    0,
                                    PrototypeCardSelectionResolutionKind
                                        .MoveToDiscard,
                                    SelectionsPerPowerStack: 1))
                        ])
                ],
                HandDrawBonusPerStack: 1),
            new PrototypePowerDefinition(
                "proto.power.burst",
                "Burst",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                RemoveAtPlayerTurnEnd: true,
                ReplayCardType: PrototypeCardType.Skill,
                AdditionalPlayCount: 1,
                ConsumeOnMatchingPlayCountModification: true),
            new PrototypePowerDefinition(
                "proto.power.free_next_skill",
                "Free Skill",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                FreeCardType: PrototypeCardType.Skill,
                ConsumeOnMatchingCardPlay: true),
            new PrototypePowerDefinition(
                "proto.power.blur",
                "Blur",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                PreventsPlayerBlockClear: true,
                DecrementAfterPlayerTurnStart: true),
            new PrototypePowerDefinition(
                "proto.power.block_next_turn",
                "Block Next Turn",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                BlockAfterClearPerStack: 1,
                RemoveAfterBlockClear: true),
            new PrototypePowerDefinition(
                "proto.power.energy_next_turn",
                "Energy Next Turn",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                EnergyAfterResetPerStack: 1,
                RemoveAfterEnergyReset: true),
            new PrototypePowerDefinition(
                "proto.power.draw_cards_next_turn",
                "Draw Cards Next Turn",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                HandDrawBonusPerStack: 1,
                RemoveAfterHandDraw: true),
            new PrototypePowerDefinition(
                "proto.power.corrosive_wave",
                "Corrosive Wave",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.CardDrawn,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ApplyEnemyStatus,
                                0,
                                StatusId: "proto.status.poison",
                                Target: PrototypeEffectTarget.AllEnemies,
                                AmountPerPowerStack: 1)
                        ])
                ],
                RemoveAtPlayerTurnEnd: true),
            new PrototypePowerDefinition(
                "proto.power.speedster",
                "Speedster",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.CardDrawn,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.DamageEnemy,
                                0,
                                Target: PrototypeEffectTarget.AllEnemies,
                                AmountPerPowerStack: 1)
                        ],
                        ExcludeHandDraw: true,
                        RequiresPlayerTurn: true)
                ]),
            new PrototypePowerDefinition(
                "proto.power.serpent_form",
                "Serpent Form",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.DamageEnemy,
                                0,
                                Target: PrototypeEffectTarget.RandomEnemy,
                                AmountPerPowerStack: 1)
                        ])
                ]),
            new PrototypePowerDefinition(
                "proto.power.envenom",
                "Envenom",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.EnemyDamaged,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ApplyEnemyStatus,
                                0,
                                StatusId: "proto.status.poison",
                                AmountPerPowerStack: 1)
                        ])
                ]),
            new PrototypePowerDefinition(
                "proto.power.thorns",
                "Thorns",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                AttackRetaliationPerStack: 1)
        }.ToDictionary(power => power.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PrototypeEnemyDefinition> Enemies { get; } =
        new[]
        {
            new PrototypeEnemyDefinition(
                "proto.enemy.crawler",
                "Crawler",
                24,
                4,
                [
                    new("bite", [new(PrototypeEnemyEffectKind.DamagePlayer, 7, 1)]),
                    new("lunge", [new(PrototypeEnemyEffectKind.DamagePlayer, 9, 1)])
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.raider",
                "Raider",
                32,
                5,
                [
                    new("slash", [new(PrototypeEnemyEffectKind.DamagePlayer, 9, 1)]),
                    new(
                        "guard_slash",
                        [
                            new(PrototypeEnemyEffectKind.GainBlock, 6),
                            new(PrototypeEnemyEffectKind.DamagePlayer, 6, 1)
                        ])
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.elite",
                "Prototype Elite",
                55,
                8,
                [
                    new("pressure", [new(PrototypeEnemyEffectKind.DamagePlayer, 11, 2)]),
                    new(
                        "guard_burst",
                        [
                            new(PrototypeEnemyEffectKind.GainBlock, 8),
                            new(PrototypeEnemyEffectKind.DamagePlayer, 9, 2)
                        ])
                ],
                StartingPowers:
                [
                    new("proto.power.thorns", 2)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.boss",
                "Prototype Boss",
                80,
                15,
                [
                    new("opening", [new(PrototypeEnemyEffectKind.DamagePlayer, 12, 2)]),
                    new("crush", [new(PrototypeEnemyEffectKind.DamagePlayer, 18, 3)]),
                    new(
                        "fortify",
                        [
                            new(PrototypeEnemyEffectKind.GainBlock, 12),
                            new(PrototypeEnemyEffectKind.DamagePlayer, 10, 2)
                        ])
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.sentry",
                "Prototype Sentry",
                38,
                4,
                [
                    new(
                        "guard",
                        [
                            new(PrototypeEnemyEffectKind.GainBlock, 8),
                            new(PrototypeEnemyEffectKind.DamagePlayer, 7, 1)
                        ]),
                    new("beam", [new(PrototypeEnemyEffectKind.DamagePlayer, 12, 1)])
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.assassin",
                "Prototype Assassin",
                34,
                5,
                [
                    new(
                        "flurry",
                        [new(PrototypeEnemyEffectKind.DamagePlayer, 5, 1, Repetitions: 2)]),
                    new("lunge", [new(PrototypeEnemyEffectKind.DamagePlayer, 13, 2)])
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.brute",
                "Prototype Brute",
                48,
                6,
                [
                    new("smash", [new(PrototypeEnemyEffectKind.DamagePlayer, 15, 2)]),
                    new(
                        "brace",
                        [
                            new(PrototypeEnemyEffectKind.GainBlock, 10),
                            new(PrototypeEnemyEffectKind.DamagePlayer, 9, 1)
                        ])
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.elite_guardian",
                "Prototype Guardian",
                70,
                7,
                [
                    new(
                        "brace",
                        [
                            new(PrototypeEnemyEffectKind.GainBlock, 12),
                            new(PrototypeEnemyEffectKind.DamagePlayer, 8, 1)
                        ]),
                    new(
                        "double_strike",
                        [
                            new(PrototypeEnemyEffectKind.DamagePlayer, 7, 1, Repetitions: 2)
                        ])
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.boss_two",
                "Prototype Act Two Boss",
                100,
                0,
                [
                    new(
                        "assault",
                        [new(PrototypeEnemyEffectKind.DamagePlayer, 14, Repetitions: 2)]),
                    new(
                        "guarded_hit",
                        [
                            new(PrototypeEnemyEffectKind.GainBlock, 16),
                            new(PrototypeEnemyEffectKind.DamagePlayer, 18)
                        ])
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.boss_three",
                "Prototype Act Three Boss",
                125,
                0,
                [
                    new("heavy", [new(PrototypeEnemyEffectKind.DamagePlayer, 24)]),
                    new(
                        "barrage",
                        [new(PrototypeEnemyEffectKind.DamagePlayer, 9, Repetitions: 3)]),
                    new(
                        "fortress",
                        [
                            new(PrototypeEnemyEffectKind.GainBlock, 20),
                            new(PrototypeEnemyEffectKind.DamagePlayer, 16)
                        ])
                ])
        }.ToDictionary(enemy => enemy.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PrototypeStatusDefinition> Statuses { get; } =
        new[]
        {
            new PrototypeStatusDefinition(
                "proto.status.poison",
                TriggerStage: PrototypeTurnStage.EnemyTurnStart,
                TriggerKind: PrototypeStatusTriggerKind.DamageSelfByStacks,
                DecayOnTrigger: 1),
            new PrototypeStatusDefinition(
                "proto.status.weak",
                DecayStage: PrototypeTurnStage.EnemyTurnEnd,
                DecayAtStage: 1,
                OutgoingDamageNumerator: 3,
                OutgoingDamageDenominator: 4),
            new PrototypeStatusDefinition(
                "proto.status.vulnerable",
                DecayStage: PrototypeTurnStage.EnemyTurnEnd,
                DecayAtStage: 1,
                IncomingAttackDamageNumerator: 3,
                IncomingAttackDamageDenominator: 2)
        }.ToDictionary(status => status.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PrototypeEventDefinition> Events { get; } =
        new[]
        {
            new PrototypeEventDefinition(
                "proto.event.fountain",
                "Quiet Fountain",
                [
                    new PrototypeEventChoiceDefinition(
                        "rest",
                        "Recover",
                        [new(PrototypeRunEffectKind.Heal, 14)]),
                    new PrototypeEventChoiceDefinition(
                        "search",
                        "Search the basin",
                        [
                            new(PrototypeRunEffectKind.LoseHp, 6),
                            new(PrototypeRunEffectKind.GainGold, 45)
                        ])
                ],
                Weight: 4,
                OncePerRun: false),
            new PrototypeEventDefinition(
                "proto.event.cache",
                "Abandoned Cache",
                [
                    new PrototypeEventChoiceDefinition(
                        "gold",
                        "Take the coins",
                        [new(PrototypeRunEffectKind.GainGold, 60)]),
                    new PrototypeEventChoiceDefinition(
                        "technique",
                        "Study the notes",
                        [new(PrototypeRunEffectKind.AddCard, CardId: "proto.silent.backflip")])
                ],
                Weight: 3),
            new PrototypeEventDefinition(
                "proto.event.laboratory",
                "Toxic Laboratory",
                [
                    new PrototypeEventChoiceDefinition(
                        "sample",
                        "Take the sample",
                        [
                            new(PrototypeRunEffectKind.LoseHp, 8),
                            new(PrototypeRunEffectKind.AddCard, CardId: "proto.silent.poisoned_stab")
                        ]),
                    new PrototypeEventChoiceDefinition(
                        "supplies",
                        "Take the supplies",
                        [new(PrototypeRunEffectKind.GainGold, 75)])
                ],
                MinAct: 2,
                Weight: 2),
            new PrototypeEventDefinition(
                "proto.event.knife_shrine",
                "Knife Shrine",
                [
                    new PrototypeEventChoiceDefinition(
                        "practice",
                        "Practice the pattern",
                        [
                            new(PrototypeRunEffectKind.LoseHp, 7),
                            new(PrototypeRunEffectKind.AddCard, CardId: "proto.silent.blade_dance")
                        ]),
                    new PrototypeEventChoiceDefinition(
                        "leave",
                        "Take an offering",
                        [new(PrototypeRunEffectKind.GainGold, 35)])
                ],
                Weight: 2),
            new PrototypeEventDefinition(
                "proto.event.alchemist",
                "Back-Alley Alchemist",
                [
                    new PrototypeEventChoiceDefinition(
                        "remedy",
                        "Accept the remedy",
                        [new(PrototypeRunEffectKind.Heal, 18)]),
                    new PrototypeEventChoiceDefinition(
                        "formula",
                        "Buy the formula with blood",
                        [
                            new(PrototypeRunEffectKind.LoseHp, 10),
                            new(PrototypeRunEffectKind.AddCard, CardId: "proto.silent.catalyst")
                        ])
                ],
                MinAct: 2,
                Weight: 2),
            new PrototypeEventDefinition(
                "proto.event.gambling_den",
                "Gambling Den",
                [
                    new PrototypeEventChoiceDefinition(
                        "safe",
                        "Take the safe purse",
                        [new(PrototypeRunEffectKind.GainGold, 30)]),
                    new PrototypeEventChoiceDefinition(
                        "risk",
                        "Take the dangerous table",
                        [
                            new(PrototypeRunEffectKind.LoseHp, 12),
                            new(PrototypeRunEffectKind.GainGold, 110)
                        ])
                ],
                Weight: 2,
                OncePerRun: false)
        }.ToDictionary(evt => evt.Id, StringComparer.Ordinal);

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(
            "proto.encounter.crawler",
            PrototypeRoomType.Combat,
            ["proto.enemy.crawler"],
            MinAct: 1,
            MaxAct: 1,
            MinFloor: 1,
            MaxFloor: 3,
            Weight: 4),
        new(
            "proto.encounter.raider",
            PrototypeRoomType.Combat,
            ["proto.enemy.raider"],
            MinAct: 1,
            MaxAct: 3,
            MinFloor: 2,
            MaxFloor: 6,
            Weight: 3),
        new(
            "proto.encounter.raiders",
            PrototypeRoomType.Combat,
            ["proto.enemy.raider", "proto.enemy.crawler"],
            MinAct: 2,
            MaxAct: 3,
            MinFloor: 1,
            MaxFloor: 6,
            Weight: 4),
        new(
            "proto.encounter.sentry",
            PrototypeRoomType.Combat,
            ["proto.enemy.sentry"],
            MinAct: 2,
            MaxAct: 3,
            MinFloor: 2,
            MaxFloor: 4,
            Weight: 3),
        new(
            "proto.encounter.assassin_pair",
            PrototypeRoomType.Combat,
            ["proto.enemy.assassin", "proto.enemy.raider"],
            MinAct: 2,
            MaxAct: 3,
            MinFloor: 2,
            MaxFloor: 4,
            Weight: 2),
        new(
            "proto.encounter.brute",
            PrototypeRoomType.Combat,
            ["proto.enemy.brute"],
            MinAct: 3,
            MaxAct: 3,
            MinFloor: 2,
            MaxFloor: 4,
            Weight: 3),
        new(
            "proto.encounter.elite",
            PrototypeRoomType.Elite,
            ["proto.enemy.elite"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 1),
        new(
            "proto.encounter.elite_guardian",
            PrototypeRoomType.Elite,
            ["proto.enemy.elite_guardian"],
            MinAct: 2,
            MaxAct: 2,
            Weight: 1),
        new(
            "proto.encounter.elite_assassins",
            PrototypeRoomType.Elite,
            ["proto.enemy.assassin", "proto.enemy.assassin"],
            MinAct: 3,
            MaxAct: 3,
            Weight: 1),
        new(
            "proto.encounter.boss",
            PrototypeRoomType.Boss,
            ["proto.enemy.boss"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 1),
        new(
            "proto.encounter.boss_two",
            PrototypeRoomType.Boss,
            ["proto.enemy.boss_two"],
            MinAct: 2,
            MaxAct: 2,
            Weight: 1),
        new(
            "proto.encounter.boss_three",
            PrototypeRoomType.Boss,
            ["proto.enemy.boss_three"],
            MinAct: 3,
            MaxAct: 3,
            Weight: 1)
    ];

    public static string[] StartingDeck { get; } =
    [
        "proto.silent.strike",
        "proto.silent.strike",
        "proto.silent.strike",
        "proto.silent.strike",
        "proto.silent.strike",
        "proto.silent.defend",
        "proto.silent.defend",
        "proto.silent.defend",
        "proto.silent.defend",
        "proto.silent.defend",
        "proto.silent.neutralize",
        "proto.silent.survivor"
    ];

    public static string[] StartingRelics { get; } =
    [
        "proto.relic.silent_ring"
    ];

    public static string[] RewardCardPool { get; } = Cards.Values
        .Where(card =>
            card.RewardEligible
            && card.Rarity is
                PrototypeCardRarity.Common
                or PrototypeCardRarity.Uncommon
                or PrototypeCardRarity.Rare)
        .Select(card => card.Id)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static (PrototypeCardRarity Rarity, int Weight)[] RewardRarityWeights(int act) =>
        act switch
        {
            <= 1 =>
            [
                (PrototypeCardRarity.Common, 65),
                (PrototypeCardRarity.Uncommon, 30),
                (PrototypeCardRarity.Rare, 5)
            ],
            2 =>
            [
                (PrototypeCardRarity.Common, 55),
                (PrototypeCardRarity.Uncommon, 35),
                (PrototypeCardRarity.Rare, 10)
            ],
            _ =>
            [
                (PrototypeCardRarity.Common, 45),
                (PrototypeCardRarity.Uncommon, 35),
                (PrototypeCardRarity.Rare, 20)
            ]
        };

    public static (PrototypeCardRarity Rarity, int Weight)[] ShopRarityWeights(int act) =>
        act switch
        {
            <= 1 =>
            [
                (PrototypeCardRarity.Common, 55),
                (PrototypeCardRarity.Uncommon, 35),
                (PrototypeCardRarity.Rare, 10)
            ],
            _ =>
            [
                (PrototypeCardRarity.Common, 45),
                (PrototypeCardRarity.Uncommon, 35),
                (PrototypeCardRarity.Rare, 20)
            ]
        };

    public static string[] PotionPool { get; } = Potions.Keys.Order(StringComparer.Ordinal).ToArray();
    public static string[] RelicPool { get; } =
    [
        "proto.relic.lantern",
        "proto.relic.ink_bottle",
        "proto.relic.bag_of_preparation",
        "proto.relic.ornamental_fan",
        "proto.relic.nunchaku",
        "proto.relic.letter_opener",
        "proto.relic.happy_flower"
    ];

    public static PrototypeCardDefinition Card(string id) =>
        Cards.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown prototype card '{id}'.");

    public static PrototypePotionDefinition Potion(string id) =>
        Potions.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown prototype potion '{id}'.");

    public static PrototypeRelicDefinition Relic(string id) =>
        Relics.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown prototype relic '{id}'.");

    public static PrototypePowerDefinition Power(string id) =>
        Powers.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown prototype power '{id}'.");

    public static PrototypeEnemyDefinition Enemy(string id) =>
        Enemies.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown prototype enemy '{id}'.");

    public static PrototypeStatusDefinition Status(string id) =>
        Statuses.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown prototype status '{id}'.");

    public static PrototypeEventDefinition Event(string id) =>
        Events.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown prototype event '{id}'.");
}
