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
            new(
                PrototypeAutomaticStepKind.DispatchCombatEvent,
                EventKind: PrototypeCombatEventKind.PlayerTurnStarted),
            new(PrototypeAutomaticStepKind.DrawPlayerHand)
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
                Rarity: PrototypeCardRarity.Basic),
            new PrototypeCardDefinition(
                "proto.silent.defend",
                "Defend",
                1,
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.GainPlayerBlock, 5, 3)],
                Rarity: PrototypeCardRarity.Basic),
            new PrototypeCardDefinition(
                "proto.silent.neutralize",
                "Neutralize",
                0,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 3, 1),
                    new(PrototypeCombatEffectKind.ApplyEnemyStatus, 1, 1, "proto.status.weak")
                ],
                Rarity: PrototypeCardRarity.Basic),
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
                Rarity: PrototypeCardRarity.Basic),
            new PrototypeCardDefinition(
                "proto.silent.backflip",
                "Backflip",
                1,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.GainPlayerBlock, 5, 3),
                    new(PrototypeCombatEffectKind.DrawCards, 2)
                ]),
            new PrototypeCardDefinition(
                "proto.silent.poisoned_stab",
                "Poisoned Stab",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 6, 2),
                    new(PrototypeCombatEffectKind.ApplyEnemyStatus, 3, 2, "proto.status.poison")
                ]),
            new PrototypeCardDefinition(
                "proto.silent.slice",
                "Slice",
                0,
                PrototypeCardTarget.Enemy,
                [new(PrototypeCombatEffectKind.DamageEnemy, 6, 3)]),
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
                ]),
            new PrototypeCardDefinition(
                "proto.silent.shiv",
                "Shiv",
                0,
                PrototypeCardTarget.Enemy,
                [new(PrototypeCombatEffectKind.DamageEnemy, 4)],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Basic),
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
                ExhaustOnUse: true),
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
                ]),
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
                ]),
            new PrototypeCardDefinition(
                "proto.silent.quick_slash",
                "Quick Slash",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 8, 3),
                    new(PrototypeCombatEffectKind.DrawCards, 1)
                ]),
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
                ]),
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
                ]),
            new PrototypeCardDefinition(
                "proto.silent.deflect",
                "Deflect",
                0,
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.GainPlayerBlock, 4, 3)]),
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
                ]),
            new PrototypeCardDefinition(
                "proto.silent.dash",
                "Dash",
                2,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 10, 3),
                    new(PrototypeCombatEffectKind.GainPlayerBlock, 10, 3)
                ],
                Rarity: PrototypeCardRarity.Uncommon),
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
                Rarity: PrototypeCardRarity.Uncommon),
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
                Rarity: PrototypeCardRarity.Uncommon),
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
                Rarity: PrototypeCardRarity.Uncommon),
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
                Rarity: PrototypeCardRarity.Uncommon),
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
                Rarity: PrototypeCardRarity.Rare),
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
                Rarity: PrototypeCardRarity.Rare),
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
                ]),
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
                "proto.silent.adrenaline",
                "Adrenaline",
                0,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.GainEnergy, 1, 1),
                    new(PrototypeCombatEffectKind.DrawCards, 2)
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Rare),
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
                ]),
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
                Rarity: PrototypeCardRarity.Uncommon),
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
                Rarity: PrototypeCardRarity.Rare),
            new PrototypeCardDefinition(
                "proto.silent.predator",
                "Predator",
                2,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 15, 5)
                ],
                Rarity: PrototypeCardRarity.Uncommon)
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
                Triggers: Array.Empty<PrototypePowerTriggerSpec>()),
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
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.EnemyDamaged,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.DamagePlayer,
                                0,
                                AmountPerPowerStack: 2)
                        ],
                        RequiresOwnerTarget: true)
                ])
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
                    new("proto.power.thorns", 1)
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
                OutgoingDamageDenominator: 4)
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
            card.Rarity != PrototypeCardRarity.Basic
            && !StringComparer.Ordinal.Equals(card.Id, "proto.silent.shiv"))
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
