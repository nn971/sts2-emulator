namespace Sts2Emulator.Core;

public static class PrototypeContent
{
    public const string CharacterId = "silent";
    public const string RulesetId = "prototype-silent-v0";
    public const string MapGenerationProfileId =
        "prototype-strategic-map-v1";

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
                ],
                MinNodes: 3,
                MaxNodes: 3),
            new(
                MinFloor: 2,
                MaxFloor: 2,
                RoomPool:
                [
                    PrototypeRoomType.Combat,
                    PrototypeRoomType.Combat,
                    PrototypeRoomType.Event,
                    PrototypeRoomType.Shop
                ],
                MinNodes: 3,
                MaxNodes: 4,
                RequiredRoomTypes:
                [
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
                ],
                MinNodes: 3,
                MaxNodes: 4,
                RequiredRoomTypes:
                [
                    PrototypeRoomType.Event,
                    PrototypeRoomType.Elite
                ],
                AvoidMatchingSpecialPredecessors: true),
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
                ],
                MinNodes: 3,
                MaxNodes: 4,
                RequiredRoomTypes:
                [
                    PrototypeRoomType.Shop,
                    PrototypeRoomType.Rest
                ],
                AvoidMatchingSpecialPredecessors: true),
            new(
                MinFloor: 5,
                MaxFloor: 5,
                RoomPool:
                [
                    PrototypeRoomType.Rest
                ],
                AllowDuplicateSpecialRooms: true,
                MinNodes: 2,
                MaxNodes: 3),
            new(
                MinFloor: 6,
                MaxFloor: 6,
                RoomPool:
                [
                    PrototypeRoomType.Boss
                ],
                MinNodes: 1,
                MaxNodes: 1,
                RequiredRoomTypes:
                [
                    PrototypeRoomType.Boss
                ])
        ],
        EndTurnPipeline:
        [
            new(
                PrototypeAutomaticStepKind.ResolvePlayerEndTurnHandEffects),
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
            // Native v0.111.0 common starter card, outside SilentCardPool.
            // If it is the final card in hand when played, draw 2 (3)
            // and gain 2 (3) energy. Both operations snapshot the
            // condition before any of the draws execute.
            // Wood Carvings transforms existing basic cards into these
            // specific, non-reward-pool event cards.
            new PrototypeCardDefinition(
                "proto.native.event.peck",
                "Peck",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy,
                        2, Repetitions: 3, RepetitionUpgradeDelta: 1)
                ],
                Rarity: PrototypeCardRarity.Token,
                RewardEligible: false,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.native.event.toric_toughness",
                "Toric Toughness",
                2,
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.GainToricToughnessBlock,
                        5, UpgradeDelta: 2)
                ],
                Rarity: PrototypeCardRarity.Token,
                RewardEligible: false,
                Type: PrototypeCardType.Skill),

            // Byrdonis Egg supplies a rest-site Hatch option. The
            // obtained Byrdpip relic converts each persistent Egg into
            // this zero-cost event Attack (14 / upgraded 18 damage).
            new PrototypeCardDefinition(
                "proto.native.event.byrd_swoop",
                "Byrd Swoop",
                0,
                PrototypeCardTarget.Enemy,
                [
                    new PrototypeCombatEffectSpec(
                        PrototypeCombatEffectKind.DamageEnemy,
                        14, UpgradeDelta: 4)
                ],
                RewardEligible: false,
                Rarity: PrototypeCardRarity.Token,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.native.event.byrdonis_egg",
                "Byrdonis Egg",
                0,
                PrototypeCardTarget.None,
                [],
                Unplayable: true,
                RewardEligible: false,
                Rarity: PrototypeCardRarity.Quest,
                Type: PrototypeCardType.Quest,
                MaxUpgradeLevel: 0),
            new PrototypeCardDefinition(
                "proto.native.event.spore_mind",
                "Spore Mind",
                1,
                PrototypeCardTarget.None,
                [],
                ExhaustOnUse: true,
                RewardEligible: false,
                Rarity: PrototypeCardRarity.Curse,
                Type: PrototypeCardType.Curse,
                MaxUpgradeLevel: 0),
            new PrototypeCardDefinition(
                "proto.native.event.poor_sleep",
                "Poor Sleep",
                0,
                PrototypeCardTarget.None,
                [],
                Retain: true,
                Unplayable: true,
                RewardEligible: false,
                Rarity: PrototypeCardRarity.Curse,
                Type: PrototypeCardType.Curse,
                MaxUpgradeLevel: 0),
            // Neow's Cursed Pearl grants Greed: a permanent Eternal,
            // unplayable curse, not eligible for ordinary card rewards.
            new PrototypeCardDefinition(
                "proto.native.neow.greed",
                "Greed",
                0,
                PrototypeCardTarget.None,
                [],
                Eternal: true,
                Unplayable: true,
                RewardEligible: false,
                Rarity: PrototypeCardRarity.Curse,
                Type: PrototypeCardType.Curse),
            // Neow's Torment grants Neow's Fury. Native v0.111.0:
            // deal 10 (14), then choose up to 2 (3) discard-pile cards
            // to return to hand, exhausting Fury afterward.
            new PrototypeCardDefinition(
                "proto.native.neow.neows_fury",
                "Neow's Fury",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 10, 4),
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.DiscardPile,
                            0,
                            2,
                            PrototypeCardSelectionResolutionKind.MoveToHand,
                            MaxSelectionsUpgradeDelta: 1))
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Ancient,
                RewardEligible: false,
                CanBeGeneratedInCombat: false,
                Type: PrototypeCardType.Attack),
            // Dowsing Rod adds the native Dowsing quest card.
            // Visits to visible Unknown nodes persist as card state;
            // the fifth visit transforms it into Abundance.
            new PrototypeCardDefinition(
                "proto.native.neow.dowsing",
                "Dowsing",
                -1,
                PrototypeCardTarget.None,
                [],
                Unplayable: true,
                Rarity: PrototypeCardRarity.Quest,
                RewardEligible: false,
                MechanicsImplemented: false,
                Type: PrototypeCardType.Quest),
            // Dowsing's quest completion transforms into Abundance:
            // choose one of three upgraded, free-this-turn Powers, Exhaust.
            new PrototypeCardDefinition(
                "proto.native.neow.abundance",
                "Abundance",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed, 1, UpgradeDelta: -1),
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ChooseGeneratedCards,
                        3,
                        GeneratedChoiceCardType: PrototypeCardType.Power,
                        GeneratedChoiceCardsUpgraded: true,
                        GeneratedChoiceMustPick: true)
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Ancient,
                RewardEligible: false,
                CanBeGeneratedInCombat: false,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.native.neow.injury",
                "Injury",
                -1,
                PrototypeCardTarget.None,
                [],
                Unplayable: true,
                RewardEligible: false,
                Rarity: PrototypeCardRarity.Curse,
                Type: PrototypeCardType.Curse),
            new PrototypeCardDefinition(
                "proto.native.event.guilty",
                "Guilty",
                0,
                PrototypeCardTarget.None,
                [],
                Unplayable: true,
                RewardEligible: false,
                Rarity: PrototypeCardRarity.Curse,
                Type: PrototypeCardType.Curse),
            new PrototypeCardDefinition(
                "proto.common.restlessness",
                "Restlessness",
                0,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DrawCards,
                        2,
                        UpgradeDelta: 1,
                        Condition: new PrototypeCombatPredicateSpec(
                            PrototypeCombatPredicateKind.HandEmptyAtEnqueue)),
                    new(
                        PrototypeCombatEffectKind.GainEnergy,
                        2,
                        UpgradeDelta: 1,
                        Condition: new PrototypeCombatPredicateSpec(
                            PrototypeCombatPredicateKind.HandEmptyAtEnqueue))
                ],
                Retain: true,
                RewardEligible: false,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.status.infection",
                "Infection",
                0,
                PrototypeCardTarget.None,
                [],
                Rarity: PrototypeCardRarity.Status,
                Unplayable: true,
                RewardEligible: false,
                Type: PrototypeCardType.Status,
                EndTurnDamageIfInHand: 3),
            new PrototypeCardDefinition(
                "proto.status.slimed",
                "Slimed",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DrawCards,
                        1)
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Status,
                RewardEligible: false,
                Type: PrototypeCardType.Status),
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
                [new(PrototypeCombatEffectKind.DamageEnemy, 4, 2)],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Token,
                RewardEligible: false,
                Type: PrototypeCardType.Attack,
                Tags: ["Shiv"]),
            new PrototypeCardDefinition(
                "proto.status.wound",
                "Wound",
                -1,
                PrototypeCardTarget.None,
                [],
                Rarity: PrototypeCardRarity.Status,
                Unplayable: true,
                RewardEligible: false,
                Type: PrototypeCardType.Status),
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
                            3,
                            ClearAfflictionFromPayload: true,
                            RestoreSuppressedUpgradesInPayload: true))
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
,
            // v0.111.0 native Silent catalog entries whose combat semantics
            // still need implementation. Keeping them typed here makes
            // inventory completeness independent from mechanics completeness.
            new PrototypeCardDefinition(
                "proto.silent.accelerant",
                "Accelerant",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        1,
                        PowerId: "proto.power.accelerant")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.blade_of_ink",
                "Blade of Ink",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.CreateCardsInHand,
                        2,
                        1,
                        CardId: "proto.silent.shiv",
                        GeneratedCardEnchantment:
                            new PrototypeCardEnchantment(
                                PrototypeCardEnchantmentKind.Inky))
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.blade_symphony",
                "Blade Symphony",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    2,
                    -1),
                PrototypeCardTarget.None,
                [],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill,
                MechanicsImplemented: false,
                MultiplayerOnly: true),
            new PrototypeCardDefinition(
                "proto.silent.calculated_gamble",
                "Calculated Gamble",
                0,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DiscardHand,
                        0),
                    new(
                        PrototypeCombatEffectKind.DrawCards,
                        0,
                        CountKind:
                            PrototypeCombatCountKind.OtherCardsInHand,
                        AmountPerCount: 1)
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill,
                RetainOnUpgrade: true),
            new PrototypeCardDefinition(
                "proto.silent.concoct",
                "Concoct",
                0,
                PrototypeCardTarget.None,
                [],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill,
                MechanicsImplemented: false,
                MultiplayerOnly: true),
            new PrototypeCardDefinition(
                "proto.silent.echoing_slash",
                "Echoing Slash",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DamageAllEnemiesRepeatPerKill,
                        10,
                        3)
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.expose",
                "Expose",
                0,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.RemoveEnemyBlock,
                        0),
                    new(
                        PrototypeCombatEffectKind.RemoveEnemyPower,
                        0,
                        PowerId: "proto.power.artifact"),
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        2,
                        1,
                        StatusId: "proto.status.vulnerable")
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.fade",
                "Fade",
                0,
                PrototypeCardTarget.None,
                [],
                Rarity: PrototypeCardRarity.Uncommon,
                Retain: true,
                Type: PrototypeCardType.Skill,
                MechanicsImplemented: false,
                MultiplayerOnly: true),
            new PrototypeCardDefinition(
                "proto.silent.fan_of_knives",
                "Fan of Knives",
                2,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.fan_of_knives"),
                    new(
                        PrototypeCombatEffectKind.CreateCardsInHand,
                        4,
                        1,
                        CardId: "proto.silent.shiv")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.flanking",
                "Flanking",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    2,
                    -1),
                PrototypeCardTarget.Enemy,
                [],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill,
                MechanicsImplemented: false,
                MultiplayerOnly: true),
            new PrototypeCardDefinition(
                "proto.silent.knife_trap",
                "Knife Trap",
                2,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.AutoPlayTaggedCardsFromZone,
                        0,
                        AutoPlaySourceZone: PrototypeCardZone.ExhaustPile,
                        RequiredCardTag: "Shiv",
                        UpgradeAutoPlayedCardsOnSourceUpgrade: true)
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.malaise",
                "Malaise",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.X),
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyPower,
                        0,
                        -1,
                        PowerId: "proto.power.strength",
                        AmountPerEnergySpent: -1),
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        0,
                        1,
                        StatusId: "proto.status.weak",
                        AmountPerEnergySpent: 1)
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.outbreak",
                "Outbreak",
                3,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyStatus,
                        9,
                        3,
                        StatusId: "proto.status.poison",
                        Target: PrototypeEffectTarget.AllEnemies),
                    new(
                        PrototypeCombatEffectKind.TriggerEnemyStatus,
                        0,
                        StatusId: "proto.status.poison",
                        Target: PrototypeEffectTarget.AllEnemies)
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.shadow_step",
                "Shadow Step",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    1,
                    -1),
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DiscardHand,
                        0),
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.shadow_step")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.shadowmeld",
                "Shadowmeld",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    1,
                    -1),
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.shadowmeld")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.sneaky",
                "Sneaky",
                2,
                PrototypeCardTarget.None,
                [],
                Rarity: PrototypeCardRarity.Rare,
                Sly: true,
                Type: PrototypeCardType.Power,
                MechanicsImplemented: false,
                MultiplayerOnly: true),
            new PrototypeCardDefinition(
                "proto.silent.storm_of_steel",
                "Storm of Steel",
                1,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.DiscardHand,
                        0),
                    new(
                        PrototypeCombatEffectKind.CreateCardsInHand,
                        0,
                        CardId: "proto.silent.shiv",
                        CountKind:
                            PrototypeCombatCountKind.OtherCardsInHand,
                        AmountPerCount: 1,
                        GeneratedCardUpgradePerSourceUpgrade: 1)
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Skill),
            new PrototypeCardDefinition(
                "proto.silent.strangle",
                "Strangle",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        8,
                        2),
                    new(
                        PrototypeCombatEffectKind.ApplyEnemyPower,
                        2,
                        1,
                        PowerId: "proto.power.strangle")
                ],
                Rarity: PrototypeCardRarity.Uncommon,
                Type: PrototypeCardType.Attack),
            new PrototypeCardDefinition(
                "proto.silent.the_hunt",
                "The Hunt",
                1,
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        10,
                        5,
                        ExtraCardRewardsOnFatal: 1,
                        PlayerPowerOnFatalId:
                            "proto.power.the_hunt",
                        PlayerPowerOnFatalAmount: 1)
                ],
                ExhaustOnUse: true,
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Attack,
                CanBeGeneratedInCombat: false),
            new PrototypeCardDefinition(
                "proto.silent.well_laid_plans",
                "Well-Laid Plans",
                new PrototypeCardCostSpec(
                    PrototypeCardCostKind.Fixed,
                    2,
                    -1),
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.well_laid_plans")
                ],
                Rarity: PrototypeCardRarity.Rare,
                Type: PrototypeCardType.Power),
            new PrototypeCardDefinition(
                "proto.silent.wraith_form",
                "Wraith Form",
                3,
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        2,
                        1,
                        PowerId: "proto.power.intangible"),
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.wraith_form")
                ],
                Rarity: PrototypeCardRarity.Ancient,
                Type: PrototypeCardType.Power)
        }.ToDictionary(card => card.Id, StringComparer.Ordinal);


    // Exact SilentCardPool.GenerateAllCards() inventory from the pinned
    // v0.111.0 reference build. Source order is preserved intentionally.
    public static string[] NativeSilentCardPool { get; } =
    [
        "proto.silent.abrasive",
        "proto.silent.accelerant",
        "proto.silent.accuracy",
        "proto.silent.acrobatics",
        "proto.silent.adrenaline",
        "proto.silent.afterimage",
        "proto.silent.anticipate",
        "proto.silent.assassinate",
        "proto.silent.backflip",
        "proto.silent.backstab",
        "proto.silent.blade_of_ink",
        "proto.silent.blade_dance",
        "proto.silent.blade_symphony",
        "proto.silent.blur",
        "proto.silent.bouncing_flask",
        "proto.silent.bubble_bubble",
        "proto.silent.bullet_time",
        "proto.silent.burst",
        "proto.silent.calculated_gamble",
        "proto.silent.cloak_and_dagger",
        "proto.silent.corrosive_wave",
        "proto.silent.concoct",
        "proto.silent.dagger_spray",
        "proto.silent.dagger_throw",
        "proto.silent.dash",
        "proto.silent.deadly_poison",
        "proto.silent.defend",
        "proto.silent.deflect",
        "proto.silent.dodge_and_roll",
        "proto.silent.echoing_slash",
        "proto.silent.envenom",
        "proto.silent.escape_plan",
        "proto.silent.expertise",
        "proto.silent.expose",
        "proto.silent.fade",
        "proto.silent.fan_of_knives",
        "proto.silent.finisher",
        "proto.silent.flanking",
        "proto.silent.flechettes",
        "proto.silent.flick_flack",
        "proto.silent.sidestep",
        "proto.silent.footwork",
        "proto.silent.grand_finale",
        "proto.silent.hand_trick",
        "proto.silent.haze",
        "proto.silent.hidden_daggers",
        "proto.silent.infinite_blades",
        "proto.silent.knife_trap",
        "proto.silent.leading_strike",
        "proto.silent.leg_sweep",
        "proto.silent.malaise",
        "proto.silent.master_planner",
        "proto.silent.memento_mori",
        "proto.silent.mirage",
        "proto.silent.murder",
        "proto.silent.neutralize",
        "proto.silent.nightmare",
        "proto.silent.noxious_fumes",
        "proto.silent.outbreak",
        "proto.silent.phantom_blades",
        "proto.silent.piercing_wail",
        "proto.silent.pinpoint",
        "proto.silent.poisoned_stab",
        "proto.silent.pounce",
        "proto.silent.precise_cut",
        "proto.silent.predator",
        "proto.silent.prepared",
        "proto.silent.reflex",
        "proto.silent.ricochet",
        "proto.silent.serpent_form",
        "proto.silent.shadow_step",
        "proto.silent.shadowmeld",
        "proto.silent.skewer",
        "proto.silent.slice",
        "proto.silent.snakebite",
        "proto.silent.sneaky",
        "proto.silent.speedster",
        "proto.silent.storm_of_steel",
        "proto.silent.strangle",
        "proto.silent.strike",
        "proto.silent.sucker_punch",
        "proto.silent.suppress",
        "proto.silent.survivor",
        "proto.silent.tactician",
        "proto.silent.the_hunt",
        "proto.silent.tools_of_the_trade",
        "proto.silent.tracking",
        "proto.silent.untouchable",
        "proto.silent.up_my_sleeve",
        "proto.silent.well_laid_plans",
        "proto.silent.wraith_form",    ];

    private static HashSet<string> NativeSilentCardIdSet { get; } =
        new(NativeSilentCardPool, StringComparer.Ordinal);


    public static IReadOnlyDictionary<string, PrototypePotionDefinition> Potions { get; } =
        new[]
        {
            // Neow's Sacrifice grants this event-only potion. The
            // out-of-combat 50% max-HP heal is supported; its in-combat
            // extra-turn power requires dedicated turn scheduling.
            new PrototypePotionDefinition(
                "proto.native.neow.ambergris",
                "Ambergris",
                PrototypeCardTarget.None,
                [],
                UsableOutsideCombat: true,
                RunEffects: [new(
                    PrototypeRunEffectKind.HealPercentMaxHp, 50)]),
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
                [new(PrototypeCombatEffectKind.GainEnergy, 2)]),
            new PrototypePotionDefinition(
                "proto.potion.strength",
                "Strength Potion",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        2,
                        PowerId: "proto.power.strength")
                ]),
            new PrototypePotionDefinition(
                "proto.potion.regen",
                "Regen Potion",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        5,
                        PowerId: "proto.power.regen")
                ]),
            new PrototypePotionDefinition(
                "proto.potion.duplicator",
                "Duplicator",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        1,
                        PowerId: "proto.power.duplication")
                ]),
            new PrototypePotionDefinition(
                "proto.potion.flex",
                "Flex Potion",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        5,
                        PowerId: "proto.power.temporary_strength")
                ]),
            new PrototypePotionDefinition(
                "proto.potion.attack",
                "Attack Potion",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ChooseGeneratedCards,
                        3,
                        GeneratedChoiceCardType:
                            PrototypeCardType.Attack)
                ]),
            new PrototypePotionDefinition(
                "proto.potion.skill",
                "Skill Potion",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ChooseGeneratedCards,
                        3,
                        GeneratedChoiceCardType:
                            PrototypeCardType.Skill)
                ]),
            new PrototypePotionDefinition(
                "proto.potion.power",
                "Power Potion",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ChooseGeneratedCards,
                        3,
                        GeneratedChoiceCardType:
                            PrototypeCardType.Power)
                ]),
            new PrototypePotionDefinition(
                "proto.potion.liquid_memories",
                "Liquid Memories",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.DiscardPile,
                            1,
                            1,
                            PrototypeCardSelectionResolutionKind
                                .MoveToHand),
                        SelectedCardTemporaryCost:
                            new PrototypeTemporaryCardCost(
                                0,
                                PrototypeTemporaryCardCostExpiry
                                    .EndOfTurn
                                | PrototypeTemporaryCardCostExpiry
                                    .WhenPlayed))
                ]),
            new PrototypePotionDefinition(
                "proto.potion.blessing_of_the_forge",
                "Blessing of the Forge",
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.UpgradeHandCards, 0)]),
            new PrototypePotionDefinition(
                "proto.potion.speed",
                "Speed Potion",
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.ApplyPlayerPower, 5, PowerId: "proto.power.temporary_dexterity")]),
            new PrototypePotionDefinition(
                "proto.potion.vulnerable",
                "Vulnerable Potion",
                PrototypeCardTarget.Enemy,
                [new(PrototypeCombatEffectKind.ApplyEnemyStatus, 3, StatusId: "proto.status.vulnerable")]),
            new PrototypePotionDefinition(
                "proto.potion.liquid_bronze",
                "Liquid Bronze",
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.ApplyPlayerPower, 3, PowerId: "proto.power.thorns")]),
            new PrototypePotionDefinition(
                "proto.potion.ghost_in_a_jar",
                "Ghost in a Jar",
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.ApplyPlayerPower, 1, PowerId: "proto.power.intangible")]),
            new PrototypePotionDefinition(
                "proto.potion.fruit_juice",
                "Fruit Juice",
                PrototypeCardTarget.None,
                [],
                UsableOutsideCombat: true,
                RunEffects: [new(PrototypeRunEffectKind.GainMaxHp, 5)]),
            new PrototypePotionDefinition(
                "proto.potion.blood",
                "Blood Potion",
                PrototypeCardTarget.None,
                [],
                UsableOutsideCombat: true,
                RunEffects: [new(PrototypeRunEffectKind.HealPercentMaxHp, 20)]),
            new PrototypePotionDefinition(
                "proto.potion.entropic_brew",
                "Entropic Brew",
                PrototypeCardTarget.None,
                [],
                UsableOutsideCombat: true,
                RunEffects: [new(PrototypeRunEffectKind.FillPotionSlots)]),
            new PrototypePotionDefinition(
                "proto.potion.cure_all",
                "Cure All",
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.GainEnergy, 1),
                    new(PrototypeCombatEffectKind.DrawCards, 2)
                ]),
            new PrototypePotionDefinition(
                "proto.potion.fortifier",
                "Fortifier",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.MultiplyPlayerBlock,
                        3)
                ]),
            new PrototypePotionDefinition(
                "proto.potion.stable_serum",
                "Stable Serum",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ApplyPlayerPower,
                        2,
                        PowerId: "proto.power.retain_hand")
                ]),
            new PrototypePotionDefinition(
                "proto.potion.touch_of_insanity",
                "Touch of Insanity",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.Hand,
                            1,
                            1,
                            PrototypeCardSelectionResolutionKind.Preserve,
                            RequireEnergyCostingCard: true),
                        SelectedCardTemporaryCost:
                            new PrototypeTemporaryCardCost(
                                0,
                                PrototypeTemporaryCardCostExpiry.None))
                ]),
            new PrototypePotionDefinition(
                "proto.potion.snecko_oil",
                "Snecko Oil",
                PrototypeCardTarget.None,
                [
                    new(PrototypeCombatEffectKind.DrawCards, 7),
                    new(
                        PrototypeCombatEffectKind.RandomizeHandCardEnergyCostsUntilTurnEndOrPlayed,
                        4)
                ]),
            new PrototypePotionDefinition(
                "proto.potion.fairy_in_a_bottle",
                "Fairy in a Bottle",
                PrototypeCardTarget.None,
                [],
                AutomaticUsage: true,
                DeathPreventionHealPercent: 30),
            new PrototypePotionDefinition(
                "proto.potion.gamblers_brew",
                "Gambler's Brew",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.ChooseCards,
                        0,
                        Selection: new(
                            PrototypeCardZone.Hand,
                            0,
                            999999999,
                            PrototypeCardSelectionResolutionKind.MoveToDiscard,
                            SequentialOptional: true,
                            DrawEqualToSelectionsOnCompletion: true))
                ]),
            new PrototypePotionDefinition(
                "proto.potion.distilled_chaos",
                "Distilled Chaos",
                PrototypeCardTarget.None,
                [
                    new(
                        PrototypeCombatEffectKind.AutoPlayTopDrawCards,
                        3)
                ])
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
                        EveryNth: 3,
                        RequiredSourceCardType:
                            PrototypeCardType.Attack,
                        ResetCounterEachTurn: true)
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
                        EveryNth: 10,
                        RequiredSourceCardType:
                            PrototypeCardType.Attack)
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
                        EveryNth: 3,
                        RequiredSourceCardType:
                            PrototypeCardType.Skill,
                        ResetCounterEachTurn: true)
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
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.anchor",
                "Anchor",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CombatStarted,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.GainPlayerBlock,
                                10)
                        ])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.vajra",
                "Vajra",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CombatStarted,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ApplyPlayerPower,
                                1,
                                PowerId: "proto.power.strength")
                        ])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.blood_vial",
                "Blood Vial",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CombatStarted,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.HealPlayer,
                                2)
                        ])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.meat_on_the_bone",
                "Meat on the Bone",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CombatWon,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.HealPlayer,
                                12)
                        ],
                        MaxPlayerHpPercent: 50)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.reptile_trinket",
                "Reptile Trinket",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.PotionUsed,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ApplyPlayerPower,
                                3,
                                PowerId:
                                    "proto.power.temporary_strength")
                        ])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.old_coin",
                "Old Coin",
                RunTriggers:
                [
                    new PrototypeRelicRunTriggerSpec(
                        PrototypeRunEventKind.RelicAcquired,
                        [new(PrototypeRunEffectKind.GainGold, 300)])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.mango",
                "Mango",
                RunTriggers:
                [
                    new PrototypeRelicRunTriggerSpec(
                        PrototypeRunEventKind.RelicAcquired,
                        [new(PrototypeRunEffectKind.GainMaxHp, 14)])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.lees_waffle",
                "Lee's Waffle",
                RunTriggers:
                [
                    new PrototypeRelicRunTriggerSpec(
                        PrototypeRunEventKind.RelicAcquired,
                        [
                            new(PrototypeRunEffectKind.GainMaxHp, 7),
                            new(PrototypeRunEffectKind.HealToFull)
                        ])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.meal_ticket",
                "Meal Ticket",
                RunTriggers:
                [
                    new PrototypeRelicRunTriggerSpec(
                        PrototypeRunEventKind.ShopEntered,
                        [new(PrototypeRunEffectKind.Heal, 15)])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.trail_ledger",
                "Trail Ledger",
                RunTriggers:
                [
                    new PrototypeRelicRunTriggerSpec(
                        PrototypeRunEventKind.RoomCompleted,
                        [new(PrototypeRunEffectKind.GainGold, 20)],
                        RouteCondition:
                            new PrototypeRouteConditionSpec(
                                RoomType: PrototypeRoomType.Combat,
                                EveryNthMatchingVisit: 2,
                                CurrentActOnly: true))
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.lucky_fysh",
                "Lucky Fysh",
                RunTriggers:
                [
                    new PrototypeRelicRunTriggerSpec(
                        PrototypeRunEventKind.CardAdded,
                        [new(PrototypeRunEffectKind.GainGold, 15)])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.darkstone_periapt",
                "Darkstone Periapt",
                RunTriggers:
                [
                    new PrototypeRelicRunTriggerSpec(
                        PrototypeRunEventKind.CardAdded,
                        [new(PrototypeRunEffectKind.GainMaxHp, 6)],
                        RequiredCardType: PrototypeCardType.Curse)
                ]),
            new PrototypeRelicDefinition(
                "proto.native.event.byrdpip",
                "Byrdpip"),
            new PrototypeRelicDefinition(
                "proto.native.event.sword_of_stone",
                "Sword of Stone"),
            new PrototypeRelicDefinition(
                "proto.native.event.sword_of_jade",
                "Sword of Jade",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CombatStarted,
                        [new PrototypeCombatEffectSpec(
                            PrototypeCombatEffectKind.ApplyPlayerPower,
                            3,
                            PowerId: "proto.power.strength")])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.molten_egg",
                "Molten Egg",
                UpgradeAddedCardType: PrototypeCardType.Attack),
            new PrototypeRelicDefinition(
                "proto.relic.toxic_egg",
                "Toxic Egg",
                UpgradeAddedCardType: PrototypeCardType.Skill),
            new PrototypeRelicDefinition(
                "proto.relic.frozen_egg",
                "Frozen Egg",
                UpgradeAddedCardType: PrototypeCardType.Power),
            new PrototypeRelicDefinition(
                "proto.relic.bronze_scales",
                "Bronze Scales",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CombatStarted,
                        [new(
                            PrototypeCombatEffectKind.ApplyPlayerPower,
                            3,
                            PowerId: "proto.power.thorns")])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.oddly_smooth_stone",
                "Oddly Smooth Stone",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CombatStarted,
                        [new(
                            PrototypeCombatEffectKind.ApplyPlayerPower,
                            1,
                            PowerId: "proto.power.dexterity")])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.bag_of_marbles",
                "Bag of Marbles",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CombatStarted,
                        [new(
                            PrototypeCombatEffectKind.ApplyEnemyStatus,
                            1,
                            StatusId: "proto.status.vulnerable",
                            Target: PrototypeEffectTarget.AllEnemies)])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.gremlin_horn",
                "Gremlin Horn",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.EnemyDefeated,
                        [
                            new(PrototypeCombatEffectKind.GainEnergy, 1),
                            new(PrototypeCombatEffectKind.DrawCards, 1)
                        ])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.mercury_hourglass",
                "Mercury Hourglass",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnStarted,
                        [new(
                            PrototypeCombatEffectKind.DamageEnemy,
                            3,
                            Target: PrototypeEffectTarget.AllEnemies)])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.orichalcum",
                "Orichalcum",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnEnded,
                        [new(
                            PrototypeCombatEffectKind.GainPlayerBlock,
                            6)],
                        RequiresZeroPlayerBlock: true)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.horn_cleat",
                "Horn Cleat",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnStarted,
                        [new(
                            PrototypeCombatEffectKind.GainPlayerBlock,
                            14)],
                        TurnEquals: 2)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.captains_wheel",
                "Captain's Wheel",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnStarted,
                        [new(
                            PrototypeCombatEffectKind.GainPlayerBlock,
                            18)],
                        TurnEquals: 3)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.kunai",
                "Kunai",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [new(
                            PrototypeCombatEffectKind.ApplyPlayerPower,
                            1,
                            PowerId: "proto.power.dexterity")],
                        EveryNth: 3,
                        RequiredSourceCardType:
                            PrototypeCardType.Attack,
                        ResetCounterEachTurn: true)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.shuriken",
                "Shuriken",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [new(
                            PrototypeCombatEffectKind.ApplyPlayerPower,
                            1,
                            PowerId: "proto.power.strength")],
                        EveryNth: 3,
                        RequiredSourceCardType:
                            PrototypeCardType.Attack,
                        ResetCounterEachTurn: true)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.ice_cream",
                "Ice Cream",
                PreserveUnusedEnergy: true),
            new PrototypeRelicDefinition(
                "proto.relic.tough_bandages",
                "Tough Bandages",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardDiscarded,
                        [
                            new(
                                PrototypeCombatEffectKind.GainPlayerBlock,
                                3)
                        ])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.tingsha",
                "Tingsha",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardDiscarded,
                        [
                            new(
                                PrototypeCombatEffectKind.DamageEnemy,
                                3,
                                Target:
                                    PrototypeEffectTarget.RandomEnemy)
                        ])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.hovering_kite",
                "Hovering Kite",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardDiscarded,
                        [
                            new(
                                PrototypeCombatEffectKind.GainEnergy,
                                1)
                        ],
                        ResetCounterEachTurn: true,
                        MaxTriggersPerCounterWindow: 1)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.question_card",
                "Question Card",
                RewardCardChoiceBonus: 1),
            new PrototypeRelicDefinition(
                "proto.relic.prayer_wheel",
                "Prayer Wheel",
                ExtraNormalCombatCardRewardGroups: 1),
            new PrototypeRelicDefinition(
                "proto.relic.white_beast_statue",
                "White Beast Statue",
                ForceCombatPotionReward: true),
            new PrototypeRelicDefinition(
                "proto.relic.membership_card",
                "Membership Card",
                ShopPriceNumerator: 1,
                ShopPriceDenominator: 2),
            new PrototypeRelicDefinition(
                "proto.relic.smiling_mask",
                "Smiling Mask",
                ShopRemovalPriceOverride: 50),
            new PrototypeRelicDefinition(
                "proto.relic.sozu",
                "Sozu",
                EnergyPerTurnBonus: 1,
                PreventPotionAcquisition: true),
            new PrototypeRelicDefinition(
                "proto.relic.empty_cage",
                "Empty Cage",
                AcquisitionDeckChoice:
                    new PrototypeRelicDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Remove,
                        2)),
            new PrototypeRelicDefinition(
                "proto.relic.astrolabe",
                "Astrolabe",
                AcquisitionDeckChoice:
                    new PrototypeRelicDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Transform,
                        3,
                        UpgradeTransformedCards: true)),
            new PrototypeRelicDefinition(
                "proto.relic.coffee_dripper",
                "Coffee Dripper",
                EnergyPerTurnBonus: 1,
                PreventRestHealing: true),
            new PrototypeRelicDefinition(
                "proto.relic.fusion_hammer",
                "Fusion Hammer",
                EnergyPerTurnBonus: 1,
                PreventRestUpgrade: true),
            new PrototypeRelicDefinition(
                "proto.relic.velvet_choker",
                "Velvet Choker",
                EnergyPerTurnBonus: 1,
                MaxCardsPlayablePerTurn: 6),
            new PrototypeRelicDefinition(
                "proto.relic.runic_dome",
                "Runic Dome",
                EnergyPerTurnBonus: 1,
                HideEnemyIntents: true),
            new PrototypeRelicDefinition(
                "proto.relic.art_of_war",
                "Art of War",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnStarted,
                        [new(
                            PrototypeCombatEffectKind.GainEnergy,
                            1)],
                        MinTurn: 2,
                        MaxAttacksPlayedLastTurn: 0)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.chemical_x",
                "Chemical X",
                XValueBonus: 2),
            new PrototypeRelicDefinition(
                "proto.relic.mummified_hand",
                "Mummified Hand",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [new(
                            PrototypeCombatEffectKind.SetRandomHandCardEnergyCostUntilTurnEndOrPlayed,
                            0)],
                        RequiredSourceCardType:
                            PrototypeCardType.Power)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.unceasing_top",
                "Unceasing Top",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [new(
                            PrototypeCombatEffectKind.DrawCards,
                            1)],
                        RequiresEmptyHand: true)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.runic_pyramid",
                "Runic Pyramid",
                PreventsHandDiscard: true),
            new PrototypeRelicDefinition(
                "proto.relic.snecko_eye",
                "Snecko Eye",
                HandDrawBonus: 2,
                RandomizeDrawnCardCostMaxExclusive: 4),
            new PrototypeRelicDefinition(
                "proto.relic.charons_ashes",
                "Charon's Ashes",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardExhausted,
                        [new(
                            PrototypeCombatEffectKind.DamageEnemy,
                            3,
                            Target:
                                PrototypeEffectTarget.AllEnemies)])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.joss_paper",
                "Joss Paper",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardExhausted,
                        [new(
                            PrototypeCombatEffectKind.DrawCards,
                            1)],
                        EveryNth: 5)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.burning_sticks",
                "Burning Sticks",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardExhausted,
                        [new(
                            PrototypeCombatEffectKind.CreateEventSourceCardCopyInHand,
                            1)],
                        RequiredSourceCardType:
                            PrototypeCardType.Skill,
                        MaxTriggersPerCounterWindow: 1)
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.forgotten_soul",
                "Forgotten Soul",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CardExhausted,
                        [new(
                            PrototypeCombatEffectKind.DamageEnemy,
                            1,
                            Target:
                                PrototypeEffectTarget.RandomEnemy)])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.toolbox",
                "Toolbox",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CombatStarted,
                        [new(
                            PrototypeCombatEffectKind.ChooseGeneratedCards,
                            3,
                            GeneratedChoiceCardsFreeThisTurn:
                                false)])
                ]),
            new PrototypeRelicDefinition(
                "proto.relic.gambling_chip",
                "Gambling Chip",
                Triggers:
                [
                    new PrototypeRelicTriggerSpec(
                        PrototypeCombatEventKind.CombatStarted,
                        [
                            new(
                                PrototypeCombatEffectKind.ChooseCards,
                                0,
                                Selection: new(
                                    PrototypeCardZone.Hand,
                                    0,
                                    999999999,
                                    PrototypeCardSelectionResolutionKind.MoveToDiscard,
                                    SequentialOptional: true,
                                    DrawEqualToSelectionsOnCompletion: true))
                        ])
                ])
        }.Concat(PrototypeNativeOvergrowthEvents.NeowRelicDefinitions)
            .ToDictionary(relic => relic.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PrototypePowerDefinition> Powers { get; } =
        new[]
        {
            new PrototypePowerDefinition(
                "proto.power.dexterity",
                "Dexterity",
                BlockBonusPerStack: 1,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                AllowNegative: true,
                NegativeApplicationIsDebuff: true),
            new PrototypePowerDefinition(
                "proto.power.regen",
                "Regen",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnStarted,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.HealPlayer,
                                0,
                                AmountPerPowerStack: 1)
                        ])
                ],
                DecrementAfterPlayerTurnStart: true),
            new PrototypePowerDefinition(
                "proto.power.temporary_strength",
                "Temporary Strength",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                AllowNegative: true,
                PlayerAttackDamageBonusPerStack: 1,
                RemoveAtPlayerTurnEnd: true),
            new PrototypePowerDefinition(
                "proto.power.duplication",
                "Duplication",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                ReplayAnyCardType: true,
                AdditionalPlayCount: 1,
                ConsumeOnMatchingPlayCountModification: true,
                RemoveAtPlayerTurnEnd: true),
            new PrototypePowerDefinition(
                "proto.power.accelerant",
                "Accelerant",
                BlockBonusPerStack: 0,
                Triggers:
                    Array.Empty<PrototypePowerTriggerSpec>(),
                ExtraEnemyStatusTriggerStatusId:
                    "proto.status.poison",
                ExtraEnemyStatusTriggersPerStack: 1),
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
                "proto.power.shadowmeld",
                "Shadowmeld",
                BlockBonusPerStack: 0,
                Triggers:
                    Array.Empty<PrototypePowerTriggerSpec>(),
                RemoveAtPlayerTurnEnd: true,
                PlayerBlockGainNumeratorPerStack: 2),
            new PrototypePowerDefinition(
                "proto.power.intangible",
                "Intangible",
                BlockBonusPerStack: 0,
                Triggers:
                    Array.Empty<PrototypePowerTriggerSpec>(),
                PlayerIncomingDamageCap: 1,
                DecrementAtEnemyTurnEnd: true),
            new PrototypePowerDefinition(
                "proto.power.wraith_form",
                "Wraith Form",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnStarted,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ApplyPlayerPower,
                                0,
                                PowerId: "proto.power.dexterity",
                                AmountPerPowerStack: -1)
                        ])
                ],
                IsDebuff: true),
            new PrototypePowerDefinition(
                "proto.power.shadow_step",
                "Shadow Step",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnStarted,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.ApplyPlayerPower,
                                0,
                                PowerId: "proto.power.double_damage",
                                AmountPerPowerStack: 1)
                        ],
                        RemoveSourcePowerAfterTrigger: true)
                ]),
            new PrototypePowerDefinition(
                "proto.power.double_damage",
                "Double Damage",
                BlockBonusPerStack: 0,
                Triggers:
                    Array.Empty<PrototypePowerTriggerSpec>(),
                DecrementAtPlayerTurnEnd: true,
                PlayerAttackDamageNumerator: 2),
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
                "proto.power.retain_hand",
                "Retain Hand",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                PreventsHandDiscard: true,
                DecrementAfterHandCleanup: true),
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
                "proto.power.strangle",
                "Strangle",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.CardPlayed,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.LoseEnemyHp,
                                0,
                                Target:
                                    PrototypeEffectTarget.SourcePowerOwnerEnemy,
                                AmountPerPowerStack: 1)
                        ])
                ],
                IsInstanced: true,
                IsDebuff: true,
                RemoveAtEnemyTurnEnd: true),
            new PrototypePowerDefinition(
                "proto.power.vulnerable",
                "Vulnerable",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                IsDebuff: true,
                DecrementAtPlayerTurnEnd: true,
                PlayerIncomingAttackDamageNumerator: 3,
                PlayerIncomingAttackDamageDenominator: 2),
            new PrototypePowerDefinition(
                "proto.power.weak",
                "Weak",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                IsDebuff: true,
                DecrementAtPlayerTurnEnd: true,
                PlayerAttackDamageNumerator: 3,
                PlayerAttackDamageDenominator: 4),
            new PrototypePowerDefinition(
                "proto.power.frail",
                "Frail",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                IsDebuff: true,
                DecrementAtPlayerTurnEnd: true,
                PlayerCardBlockNumerator: 3,
                PlayerCardBlockDenominator: 4),
            new PrototypePowerDefinition(
                "proto.power.ringing",
                "Ringing",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                DoesNotStack: true,
                IsDebuff: true,
                DecrementAtPlayerTurnEnd: true,
                MaxCardsPlayablePerTurn: 1,
                // RingingPower.AfterApplied/AfterCardEnteredCombat
                // afflicts otherwise-unafflicted combat cards; its
                // AfterRemoved clears only its own afflictions.
                AppliedCardAffliction: PrototypeCardAfflictionKind.Ringing,
                SkipCardsWithExistingAffliction: true,
                ClearAppliedCardAfflictionWhenRemoved: true),
            new PrototypePowerDefinition(
                "proto.power.plow",
                "Plow",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                DoesNotStack: true,
                TriggerOwnerAtHpAtOrBelowStacks: true,
                ClearOwnerStrengthOnHpThresholdTrigger: true,
                OwnerAiStateOnHpThresholdTrigger: "stun_move"),
            new PrototypePowerDefinition(
                "proto.power.slippery",
                "Slippery",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                EnemyHpLossCapPerTrigger: 1,
                ConsumeOnEnemyHpLoss: true),
            new PrototypePowerDefinition(
                "proto.power.infested",
                "Infested",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                DoesNotStack: true),
            new PrototypePowerDefinition(
                "proto.power.slow",
                "Slow",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                DoesNotStack: true,
                EnemyIncomingAttackDamagePercentPerCardPlayed: 10),
            new PrototypePowerDefinition(
                "proto.power.territorial",
                "Territorial",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                DoesNotStack: true,
                EnemyStrengthGainAtTurnEndPerStack: 1),
            new PrototypePowerDefinition(
                "proto.power.illusion",
                "Illusion",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                DoesNotStack: true),
            new PrototypePowerDefinition(
                "proto.power.minion",
                "Minion",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                DoesNotStack: true,
                OwnerDeathTriggersFatal: false),
            new PrototypePowerDefinition(
                "proto.power.tangled",
                "Tangled",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                IsDebuff: true,
                AppliedCardAffliction:
                    PrototypeCardAfflictionKind.Entangled,
                AppliedCardAfflictionRequiredCardType:
                    PrototypeCardType.Attack,
                AfflictedCardEnergyCostPerStack: 1,
                SkipCardsWithExistingAffliction: true,
                ClearAppliedCardAfflictionWhenRemoved: true,
                DecrementAtPlayerTurnEnd: true),
            new PrototypePowerDefinition(
                "proto.power.constrict",
                "Constrict",
                BlockBonusPerStack: 0,
                Triggers:
                [
                    new PrototypePowerTriggerSpec(
                        PrototypeCombatEventKind.PlayerTurnEnded,
                        [
                            new PrototypeCombatEffectSpec(
                                PrototypeCombatEffectKind.DamagePlayer,
                                0,
                                AmountPerPowerStack: 1)
                        ])
                ],
                SourceBoundToEnemy: true,
                IsDebuff: true),
            new PrototypePowerDefinition(
                "proto.power.shrink",
                "Shrink",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                SourceBoundToEnemy: true,
                DoesNotStack: true,
                IsDebuff: true,
                PlayerAttackDamageNumerator: 7,
                PlayerAttackDamageDenominator: 10),
            new PrototypePowerDefinition(
                "proto.power.artifact",
                "Artifact",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                BlocksNextDebuff: true),
            new PrototypePowerDefinition(
                "proto.power.dampen",
                "Dampen",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                SourceBoundToEnemy: true,
                DoesNotStack: true,
                IsDebuff: true,
                DowngradeExistingCardsOnApply: true,
                RestoreDowngradedCardsWhenLastSourceRemoved: true),
            new PrototypePowerDefinition(
                "proto.power.hex",
                "Hex",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                SourceBoundToEnemy: true,
                DoesNotStack: true,
                IsDebuff: true,
                SourceBoundCardAffliction:
                    PrototypeCardAfflictionKind.Hexed,
                SkipCardsWithExistingAffliction: true,
                ClearSourceAfflictionWhenRemoved: true),
            new PrototypePowerDefinition(
                "proto.power.strength",
                "Strength",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                AllowNegative: true,
                NegativeApplicationIsDebuff: true,
                PlayerAttackDamageBonusPerStack: 1,
                EnemyAttackDamageBonusPerStack: 1),
            new PrototypePowerDefinition(
                "proto.power.thorns",
                "Thorns",
                BlockBonusPerStack: 0,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>(),
                AttackRetaliationPerStack: 1)
,
            new PrototypePowerDefinition(
                "proto.power.fan_of_knives",
                "Fan of Knives",
                0,
                [],
                DoesNotStack: true,
                AllEnemyTargetCardTag: "Shiv"),
            new PrototypePowerDefinition(
                "proto.power.the_hunt",
                "The Hunt",
                0,
                []),
            new PrototypePowerDefinition(
                "proto.power.well_laid_plans",
                "Well-Laid Plans",
                0,
                [],
                DoesNotStack: true,
                PreventsHandDiscard: true)
        }.ToDictionary(power => power.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PrototypeEnemyDefinition> Enemies { get; } =
        new[]
        {
            new PrototypeEnemyDefinition(
                "proto.enemy.flail_knight",
                "Flail Knight",
                101,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "war_chant",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                3,
                                PowerId: "proto.power.strength")
                        ],
                        MaxConsecutiveUses: 1),
                    new PrototypeEnemyMoveDefinition(
                        "flail",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                9,
                                Repetitions: 2,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ],
                        MaxConsecutiveUses: 2),
                    new PrototypeEnemyMoveDefinition(
                        "ram",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                15,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ],
                        MaxConsecutiveUses: 2)
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.UniformRandomAfterOpener,
                OpeningMoveIndex: 2,
                HpAscensionDeltas:
                [
                    new(8, 7)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.magi_knight",
                "Magi Knight",
                82,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "power_shield",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                6,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.GainBlock,
                                5,
                                AscensionDeltas:
                                [
                                    new(8, 4)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "dampen",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                1,
                                PowerId: "proto.power.dampen")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "ram",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                10,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "prep",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.GainBlock,
                                5,
                                AscensionDeltas:
                                [
                                    new(8, 4)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "magic_bomb",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                35,
                                AscensionDeltas:
                                [
                                    new(9, 5)
                                ])
                        ])
                ],
                MoveLoopStartIndex: 2,
                HpAscensionDeltas:
                [
                    new(8, 7)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.spectral_knight",
                "Spectral Knight",
                93,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "hex",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                2,
                                PowerId: "proto.power.hex")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "soul_slash",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                15,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ],
                        MaxConsecutiveUses: 2),
                    new PrototypeEnemyMoveDefinition(
                        "soul_flame",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                3,
                                Repetitions: 3,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ],
                        MaxConsecutiveUses: 1)
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.UniformRandomAfterOpener,
                OpeningMoveIndices: [0, 1],
                RandomMovePoolStartIndex: 1,
                HpAscensionDeltas:
                [
                    new(8, 4)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.seapunk",
                "Seapunk",
                46,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "sea_kick",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                11,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "spinning_kick",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                2,
                                Repetitions: 4)
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "bubble_burp",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.GainBlock,
                                7),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                1,
                                PowerId: "proto.power.strength")
                        ])
                ],
                HpAscensionDeltas:
                [
                    new(8, 3)
                ],
                MinHp: 44),
            new PrototypeEnemyDefinition(
                "proto.enemy.assassin_ruby_raider",
                "Assassin Raider",
                23,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "killshot",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                10,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 18),
            new PrototypeEnemyDefinition(
                "proto.enemy.inklet",
                "Inklet",
                17,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "jab",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                3,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "whirlwind",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                2,
                                Repetitions: 3,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "piercing_gaze",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                10,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 11,
                MinHpAscensionDeltas:
                [
                    new(8, 1)
                ],
                StartingPowers:
                [
                    new PrototypeStartingPowerSpec(
                        "proto.power.slippery",
                        1)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "init",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "jab_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "whirlwind_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "jab_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "piercing_gaze_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "jab_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "rand",
                            PrototypeEnemyAiStateKind.Random,
                            Branches:
                            [
                                new(
                                    "piercing_gaze_move",
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CannotRepeat),
                                new(
                                    "whirlwind_move",
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CannotRepeat)
                            ]),
                        new PrototypeEnemyAiStateDefinition(
                            "init",
                            PrototypeEnemyAiStateKind.Conditional,
                            ConditionalBranches:
                            [
                                new(
                                    "whirlwind_move",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    Value: "middle"),
                                new(
                                    "jab_move",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    Value: "left"),
                                new(
                                    "jab_move",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    Value: "right")
                            ])
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.ceremonial_beast",
                "Ceremonial Beast",
                252,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "stamp",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                150,
                                PowerId: "proto.power.plow",
                                AscensionDeltas:
                                [
                                    new(9, 10)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "plow",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                18,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.strength")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "stun",
                        []),
                    new PrototypeEnemyMoveDefinition(
                        "beast_cry",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                1,
                                PowerId: "proto.power.ringing")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "stomp",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                15,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "crush",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                17,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                3,
                                PowerId: "proto.power.strength",
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 10)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "stamp_move",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "stamp_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "plow_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "plow_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "plow_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "stun_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "beast_cry_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "beast_cry_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 3,
                            NextStateId: "stomp_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "stomp_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 4,
                            NextStateId: "crush_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "crush_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 5,
                            NextStateId: "beast_cry_move")
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.kin_follower",
                "Kin Follower",
                59,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "quick_slash",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                5)
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "boomerang",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                2,
                                Repetitions: 2)
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "power_dance",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.strength",
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                StartingPowers:
                [
                    new("proto.power.minion", 1)
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 4)
                ],
                MinHp: 58,
                MinHpAscensionDeltas:
                [
                    new(8, 4)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "init",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "quick_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "boomerang_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "boomerang_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "dance_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "dance_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "quick_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "init",
                            PrototypeEnemyAiStateKind.Conditional,
                            ConditionalBranches:
                            [
                                new(
                                    "quick_move",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    "quick"),
                                new(
                                    "dance_move",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    "dance")
                            ])
                    ]),
                IsMinion: true),
            new PrototypeEnemyDefinition(
                "proto.enemy.kin_priest",
                "Kin Priest",
                190,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "orb_of_frailty",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                8,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                1,
                                PowerId: "proto.power.frail")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "orb_of_weakness",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                8,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                1,
                                PowerId: "proto.power.weak")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "soul_beam",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                3,
                                Repetitions: 3)
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "dark_ritual",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.strength",
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                HpAscensionDeltas:
                [
                    new(8, 9)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.vantom",
                "Vantom",
                173,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "ink_blot",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                7,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "inky_lance",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                6,
                                Repetitions: 2,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "dismember",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                26,
                                AscensionDeltas:
                                [
                                    new(9, 4)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.AddCardsToDiscard,
                                3,
                                CardId: "proto.status.wound")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "prepare",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.strength")
                        ])
                ],
                StartingPowers:
                [
                    new(
                        "proto.power.slippery",
                        8,
                        AscensionDeltas:
                        [
                            new(8, 1)
                        ])
                ],
                HpAscensionDeltas:
                [
                    new(8, 10)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.wriggler",
                "Wriggler",
                21,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "nasty_bite",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                6,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "wriggle",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.AddCardsToDiscard,
                                1,
                                CardId: "proto.status.infection"),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.strength")
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 17,
                MinHpAscensionDeltas:
                [
                    new(8, 1)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "init",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "bite_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "wriggle_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "wriggle_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "bite_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "init",
                            PrototypeEnemyAiStateKind.Conditional,
                            ConditionalBranches:
                            [
                                // Native Wriggler.INIT_MOVE branches on the
                                // actual encounter slot names, not abstract
                                // move labels. Odd slots bite first; even
                                // slots wriggle first.
                                new(
                                    "bite_move",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    "wriggler1"),
                                new(
                                    "wriggle_move",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    "wriggler2"),
                                new(
                                    "bite_move",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    "wriggler3"),
                                new(
                                    "wriggle_move",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    "wriggler4")
                            ])
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.phrog_parasite",
                "Phrog Parasite",
                64,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "infect",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.AddCardsToDiscard,
                                3,
                                CardId: "proto.status.infection")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "lash",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                4,
                                Repetitions: 4,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                StartingPowers:
                [
                    new("proto.power.infested", 1)
                ],
                HpAscensionDeltas:
                [
                    new(8, 4)
                ],
                MinHp: 61,
                MinHpAscensionDeltas:
                [
                    new(8, 5)
                ],
                DeathSummons:
                [
                    new(
                        "proto.enemy.wriggler",
                        0,
                        "wriggler1"),
                    new(
                        "proto.enemy.wriggler",
                        1,
                        "wriggler2"),
                    new(
                        "proto.enemy.wriggler",
                        2,
                        "wriggler3"),
                    new(
                        "proto.enemy.wriggler",
                        3,
                        "wriggler4")
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.bygone_effigy",
                "Bygone Effigy",
                127,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "sleep",
                        []),
                    new PrototypeEnemyMoveDefinition(
                        "wake",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                10,
                                PowerId: "proto.power.strength")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "slashes",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                13,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ])
                ],
                StartingPowers:
                [
                    new("proto.power.slow", 1)
                ],
                MoveLoopStartIndex: 2,
                HpAscensionDeltas:
                [
                    new(8, 5)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.byrdonis",
                "Byrdonis",
                84,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "swoop",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                17,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "peck",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                3,
                                Repetitions: 3,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                StartingPowers:
                [
                    new("proto.power.territorial", 1)
                ],
                HpAscensionDeltas:
                [
                    new(8, 6)
                ],
                MinHp: 81,
                MinHpAscensionDeltas:
                [
                    new(8, 9)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.eye_with_teeth",
                "Eye With Teeth",
                6,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "distract",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.AddCardsToDiscard,
                                3,
                                CardId: "proto.status.dazed")
                        ])
                ],
                StartingPowers:
                [
                    new("proto.power.illusion", 1),
                    new("proto.power.minion", 1)
                ],
                IsMinion: true,
                RevivesOnEnemyTurn: true),
            new PrototypeEnemyDefinition(
                "proto.enemy.fogmog",
                "Fogmog",
                74,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "illusory_spores",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.SummonEnemy,
                                1,
                                EnemyId: "proto.enemy.eye_with_teeth")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "thwack",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                8,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                1,
                                PowerId: "proto.power.strength")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "headbutt",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                14,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 4)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "summon_move",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "summon_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "forced_thwack"),
                        new PrototypeEnemyAiStateDefinition(
                            "forced_thwack",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "random_thwack",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "forced_headbutt"),
                        new PrototypeEnemyAiStateDefinition(
                            "forced_headbutt",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "post_headbutt_thwack"),
                        new PrototypeEnemyAiStateDefinition(
                            "random_headbutt",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "post_headbutt_thwack"),
                        new PrototypeEnemyAiStateDefinition(
                            "post_headbutt_thwack",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "rand",
                            PrototypeEnemyAiStateKind.Random,
                            Branches:
                            [
                                new(
                                    "random_thwack",
                                    Weight: 2),
                                new(
                                    "random_headbutt",
                                    Weight: 3)
                            ])
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.flyconid",
                "Flyconid",
                49,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "vulnerable_spores",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                2,
                                PowerId: "proto.power.vulnerable")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "frail_spores",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                8,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                2,
                                PowerId: "proto.power.frail")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "smash",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                11,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 4)
                ],
                MinHp: 47,
                MinHpAscensionDeltas:
                [
                    new(8, 4)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "initial",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "vulnerable_spores_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "frail_spores_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "smash_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "initial",
                            PrototypeEnemyAiStateKind.Random,
                            Branches:
                            [
                                new(
                                    "frail_spores_move",
                                    Weight: 2),
                                new(
                                    "smash_move",
                                    Weight: 1)
                            ]),
                        new PrototypeEnemyAiStateDefinition(
                            "rand",
                            PrototypeEnemyAiStateKind.Random,
                            Branches:
                            [
                                new(
                                    "vulnerable_spores_move",
                                    Weight: 3,
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CannotRepeat),
                                new(
                                    "frail_spores_move",
                                    Weight: 2,
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CannotRepeat),
                                new(
                                    "smash_move",
                                    Weight: 1,
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CannotRepeat)
                            ])
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.snapping_jaxfruit",
                "Snapping Jaxfruit",
                33,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "energy_orb",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                3,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.strength")
                        ])
                ],
                HpAscensionDeltas:
                [
                    new(8, 3)
                ],
                MinHp: 31),
            new PrototypeEnemyDefinition(
                "proto.enemy.mawler",
                "Mawler",
                72,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "rip_and_tear",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                14,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "roar",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                3,
                                PowerId: "proto.power.vulnerable")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "claw",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                4,
                                Repetitions: 2,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 4)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "claw_move",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "rip_and_tear_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "roar_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "claw_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "rand",
                            PrototypeEnemyAiStateKind.Random,
                            Branches:
                            [
                                new(
                                    "rip_and_tear_move",
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CannotRepeat),
                                new(
                                    "roar_move",
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .UseOnlyOnce),
                                new(
                                    "claw_move",
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CannotRepeat)
                            ])
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.cubex_construct",
                "Cubex Construct",
                65,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "charge_up",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.strength")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "repeater_blast",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                7,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.strength")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "repeater_blast_2",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                7,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.strength")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "expel",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                5,
                                Repetitions: 2,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 5)
                ],
                StartingPowers:
                [
                    new PrototypeStartingPowerSpec(
                        "proto.power.artifact",
                        1)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "charge_up",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "charge_up",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "repeater_blast"),
                        new PrototypeEnemyAiStateDefinition(
                            "repeater_blast",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "repeater_blast_2"),
                        new PrototypeEnemyAiStateDefinition(
                            "repeater_blast_2",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "expel"),
                        new PrototypeEnemyAiStateDefinition(
                            "expel",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 3,
                            NextStateId: "repeater_blast")
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.punch_construct",
                "Punch Construct",
                55,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "ready",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.GainBlock,
                                10)
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "fast_punch",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                5,
                                Repetitions: 2,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                1,
                                PowerId: "proto.power.frail")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "strong_punch",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                14,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.SequentialLoop,
                HpAscensionDeltas:
                [
                    new(8, 5)
                ],
                StartingPowers:
                [
                    new PrototypeStartingPowerSpec(
                        "proto.power.artifact",
                        1)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.axe_ruby_raider",
                "Axe Raider",
                22,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "swing_1",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                5,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.GainBlock,
                                5,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "swing_2",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                5,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.GainBlock,
                                5,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "big_swing",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                12,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.SequentialLoop,
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 20,
                MinHpAscensionDeltas:
                [
                    new(8, 1)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.tracker_ruby_raider",
                "Tracker Raider",
                25,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "track",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                2,
                                PowerId: "proto.power.frail")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "hounds",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                1,
                                Repetitions: 8,
                                RepetitionAscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 21,
                MinHpAscensionDeltas:
                [
                    new(8, 1)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "track",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "track",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "hounds"),
                        new PrototypeEnemyAiStateDefinition(
                            "hounds",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "hounds")
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.crossbow_ruby_raider",
                "Crossbow Raider",
                21,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "reload",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.GainBlock,
                                3)
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "fire",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                14,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.SequentialLoop,
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 18,
                MinHpAscensionDeltas:
                [
                    new(8, 1)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.brute_ruby_raider",
                "Brute Raider",
                33,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "beat",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                7,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "roar",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                3,
                                PowerId: "proto.power.strength")
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.SequentialLoop,
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 30,
                MinHpAscensionDeltas:
                [
                    new(8, 1)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.vine_shambler",
                "Vine Shambler",
                61,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "swipe",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                6,
                                Repetitions: 2,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "grasping_vines",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                8,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                1,
                                PowerId: "proto.power.tangled")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "chomp",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                16,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ])
                ],
                HpAscensionDeltas:
                [
                    new(8, 3)
                ]),
            new PrototypeEnemyDefinition(
                "proto.enemy.slithering_strangler",
                "Slithering Strangler",
                55,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "constrict",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                3,
                                PowerId: "proto.power.constrict")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "thwack",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                7,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.GainBlock,
                                5)
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "lash",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                12,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 53,
                MinHpAscensionDeltas:
                [
                    new(8, 1)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "constrict_move",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "constrict_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "rand_attack"),
                        new PrototypeEnemyAiStateDefinition(
                            "thwack_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "constrict_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "lash_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "constrict_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "rand_attack",
                            PrototypeEnemyAiStateKind.Random,
                            Branches:
                            [
                                new("thwack_move"),
                                new("lash_move")
                            ])
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.shrinker_beetle",
                "Shrinker Beetle",
                40,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "shrinker",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyPlayerPower,
                                1,
                                PowerId: "proto.power.shrink")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "chomp",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                7,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "stomp",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                13,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                HpAscensionDeltas:
                [
                    new(8, 2)
                ],
                MinHp: 38,
                MinHpAscensionDeltas:
                [
                    new(8, 2)
                ],
                MoveLoopStartIndex: 1),
            new PrototypeEnemyDefinition(
                "proto.enemy.fuzzy_wurm_crawler",
                "Fuzzy Wurm Crawler",
                57,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "acid_goop",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                4,
                                AscensionDeltas:
                                [
                                    new(9, 2)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "inhale",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                7,
                                PowerId: "proto.power.strength")
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 2)
                ],
                MinHp: 55,
                MinHpAscensionDeltas:
                [
                    new(8, 3)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "acid_1",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "acid_1",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "inhale"),
                        new PrototypeEnemyAiStateDefinition(
                            "inhale",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "acid_2"),
                        new PrototypeEnemyAiStateDefinition(
                            "acid_2",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "acid_1")
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.nibbit",
                "Nibbit",
                46,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "butt",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                12,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "slice",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                6,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ]),
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.GainBlock,
                                5,
                                AscensionDeltas:
                                [
                                    new(8, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "hiss",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.strength",
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 2)
                ],
                MinHp: 42,
                MinHpAscensionDeltas:
                [
                    new(8, 2)
                ],
                Ai: new PrototypeEnemyAiDefinition(
                    "init",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "butt",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "slice"),
                        new PrototypeEnemyAiStateDefinition(
                            "slice",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "hiss"),
                        new PrototypeEnemyAiStateDefinition(
                            "hiss",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "butt"),
                        new PrototypeEnemyAiStateDefinition(
                            "init",
                            PrototypeEnemyAiStateKind.Conditional,
                            ConditionalBranches:
                            [
                                new(
                                    "butt",
                                    PrototypeEnemyAiConditionKind
                                        .IsAlone),
                                new(
                                    "hiss",
                                    PrototypeEnemyAiConditionKind
                                        .IsNotFront),
                                new(
                                    "slice",
                                    PrototypeEnemyAiConditionKind
                                        .IsFront)
                            ])
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.toadpole",
                "Toadpole",
                25,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "spike_spit",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                3,
                                Repetitions: 3,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "whirl",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                7,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "spiken",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.ApplyEnemyPower,
                                2,
                                PowerId: "proto.power.thorns")
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 21,
                Ai: new PrototypeEnemyAiDefinition(
                    "init",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "spike_spit_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "whirl_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "whirl_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "spiken_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "spiken_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 2,
                            NextStateId: "spike_spit_move"),
                        new PrototypeEnemyAiStateDefinition(
                            "init",
                            PrototypeEnemyAiStateKind.Conditional,
                            ConditionalBranches:
                            [
                                new(
                                    "whirl_move",
                                    PrototypeEnemyAiConditionKind
                                        .IsNotFront),
                                new(
                                    "spiken_move",
                                    PrototypeEnemyAiConditionKind
                                        .IsFront)
                            ])
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.leaf_slime_s",
                "Leaf Slime (S)",
                15,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "tackle",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                3,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "goop",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.AddCardsToDiscard,
                                1,
                                CardId: "proto.status.slimed")
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 11,
                Ai: new PrototypeEnemyAiDefinition(
                    "rand",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "tackle_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "goop_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "rand",
                            PrototypeEnemyAiStateKind.Random,
                            Branches:
                            [
                                new(
                                    "tackle_move",
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CannotRepeat),
                                new(
                                    "goop_move",
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CannotRepeat)
                            ])
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.leaf_slime_m",
                "Leaf Slime (M)",
                35,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "sticky_shot",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.AddCardsToDiscard,
                                2,
                                CardId: "proto.status.slimed")
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "clump_shot",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                8,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 32),
            new PrototypeEnemyDefinition(
                "proto.enemy.twig_slime_m",
                "Twig Slime (M)",
                28,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "pokey_pounce",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                11,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ]),
                    new PrototypeEnemyMoveDefinition(
                        "sticky_shot",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.AddCardsToDiscard,
                                1,
                                CardId: "proto.status.slimed")
                        ])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 26,
                Ai: new PrototypeEnemyAiDefinition(
                    "sticky_shot_move",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "pokey_pounce_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "sticky_shot_move",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1,
                            NextStateId: "rand"),
                        new PrototypeEnemyAiStateDefinition(
                            "rand",
                            PrototypeEnemyAiStateKind.Random,
                            Branches:
                            [
                                new(
                                    "pokey_pounce_move",
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CanRepeatXTimes,
                                    MaxTimes: 2),
                                new(
                                    "sticky_shot_move",
                                    RepeatRule:
                                        PrototypeEnemyAiRepeatRule
                                            .CannotRepeat)
                            ])
                    ])),
            new PrototypeEnemyDefinition(
                "proto.enemy.twig_slime_s",
                "Twig Slime (S)",
                11,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "tackle",
                        [
                            new PrototypeEnemyEffectSpec(
                                PrototypeEnemyEffectKind.DamagePlayer,
                                4,
                                AscensionDeltas:
                                [
                                    new(9, 1)
                                ])
                        ])
                ],
                HpAscensionDeltas:
                [
                    new(8, 1)
                ],
                MinHp: 7),
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
                IsDebuff: true,
                TriggerStage: PrototypeTurnStage.EnemyTurnStart,
                TriggerKind: PrototypeStatusTriggerKind.DamageSelfByStacks,
                DecayOnTrigger: 1),
            new PrototypeStatusDefinition(
                "proto.status.weak",
                IsDebuff: true,
                DecayStage: PrototypeTurnStage.EnemyTurnEnd,
                DecayAtStage: 1,
                OutgoingDamageNumerator: 3,
                OutgoingDamageDenominator: 4),
            new PrototypeStatusDefinition(
                "proto.status.vulnerable",
                IsDebuff: true,
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
                "proto.event.path_broker",
                "Path Broker",
                [
                    new PrototypeEventChoiceDefinition(
                        "elite_contract",
                        "Collect 75 gold for a completed elite fight",
                        [new(PrototypeRunEffectKind.GainGold, 75)],
                        RouteCondition:
                            new PrototypeRouteConditionSpec(
                                RoomType: PrototypeRoomType.Elite,
                                MinimumCompletedVisits: 1,
                                CurrentActOnly: true)),
                    new PrototypeEventChoiceDefinition(
                        "combat_streak",
                        "Gain 5 Max HP after two consecutive combats",
                        [new(PrototypeRunEffectKind.GainMaxHp, 5)],
                        RouteCondition:
                            new PrototypeRouteConditionSpec(
                                RoomType: PrototypeRoomType.Combat,
                                MinimumConsecutiveCompleted: 2,
                                CurrentActOnly: true)),
                    new PrototypeEventChoiceDefinition(
                        "ordinary_trade",
                        "Take 15 gold",
                        [new(PrototypeRunEffectKind.GainGold, 15)])
                ],
                Weight: 2),
            new PrototypeEventDefinition(
                "proto.event.forgotten_altar",
                "Forgotten Altar",
                [
                    new PrototypeEventChoiceDefinition(
                        "purge",
                        "Pay 50 gold to purge a card",
                        [
                            new(
                                PrototypeRunEffectKind.LoseGold,
                                50)
                        ],
                        DeckChoice:
                            new PrototypeEventDeckChoiceSpec(
                                PrototypePersistentDeckChoiceKind.Remove,
                                1)),
                    new PrototypeEventChoiceDefinition(
                        "refine",
                        "Lose 7 HP to upgrade a card",
                        [
                            new(
                                PrototypeRunEffectKind.LoseHp,
                                7)
                        ],
                        DeckChoice:
                            new PrototypeEventDeckChoiceSpec(
                                PrototypePersistentDeckChoiceKind.Upgrade,
                                1)),
                    new PrototypeEventChoiceDefinition(
                        "leave",
                        "Leave with the loose coins",
                        [
                            new(
                                PrototypeRunEffectKind.GainGold,
                                20)
                        ])
                ],
                Weight: 2),
            new PrototypeEventDefinition(
                "proto.event.warped_mirror",
                "Warped Mirror",
                [
                    new PrototypeEventChoiceDefinition(
                        "reshape",
                        "Lose 9 HP to transform two cards",
                        [
                            new(
                                PrototypeRunEffectKind.LoseHp,
                                9)
                        ],
                        DeckChoice:
                            new PrototypeEventDeckChoiceSpec(
                                PrototypePersistentDeckChoiceKind.Transform,
                                2)),
                    new PrototypeEventChoiceDefinition(
                        "polish",
                        "Pay 35 gold to transform and upgrade one card",
                        [
                            new(
                                PrototypeRunEffectKind.LoseGold,
                                35)
                        ],
                        DeckChoice:
                            new PrototypeEventDeckChoiceSpec(
                                PrototypePersistentDeckChoiceKind.Transform,
                                1,
                                UpgradeTransformedCards: true)),
                    new PrototypeEventChoiceDefinition(
                        "ignore",
                        "Ignore the mirror",
                        [])
                ],
                MinAct: 2,
                Weight: 2),
            new PrototypeEventDefinition(
                "proto.event.caged_vault",
                "Caged Vault",
                [
                    new PrototypeEventChoiceDefinition(
                        "open",
                        "Lose 5 Max HP and take the cage",
                        [
                            new(
                                PrototypeRunEffectKind.LoseMaxHp,
                                5),
                            new(
                                PrototypeRunEffectKind.GainRelic,
                                RelicId: "proto.relic.empty_cage")
                        ]),
                    new PrototypeEventChoiceDefinition(
                        "coffer",
                        "Lose 15 HP and take the old coffer",
                        [
                            new(
                                PrototypeRunEffectKind.LoseHp,
                                15),
                            new(
                                PrototypeRunEffectKind.GainRelic,
                                RelicId: "proto.relic.old_coin")
                        ]),
                    new PrototypeEventChoiceDefinition(
                        "tonic",
                        "Pay 30 gold for a Strength Potion",
                        [
                            new(
                                PrototypeRunEffectKind.LoseGold,
                                30),
                            new(
                                PrototypeRunEffectKind.GainPotion,
                                PotionId: "proto.potion.strength")
                        ]),
                    new PrototypeEventChoiceDefinition(
                        "leave",
                        "Take loose change and leave",
                        [
                            new(
                                PrototypeRunEffectKind.GainGold,
                                20)
                        ])
                ],
                Weight: 2),
            new PrototypeEventDefinition(
                "proto.event.collectors_annex",
                "Collector's Annex",
                [
                    new PrototypeEventChoiceDefinition(
                        "bundle",
                        "Pay 25 gold for relics, two potions and a final upgrade",
                        [
                            new(PrototypeRunEffectKind.LoseGold, 25),
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: "proto.relic.empty_cage"),
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: "proto.relic.astrolabe"),
                            new(PrototypeRunEffectKind.GainPotion,
                                PotionId: "proto.potion.strength"),
                            new(PrototypeRunEffectKind.GainPotion,
                                PotionId: "proto.potion.fire")
                        ],
                        DeckChoice: new PrototypeEventDeckChoiceSpec(
                            PrototypePersistentDeckChoiceKind.Upgrade, 1)),
                    new PrototypeEventChoiceDefinition(
                        "late_card",
                        "Receive a relic before a new card is added",
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: "proto.relic.empty_cage"),
                            new(PrototypeRunEffectKind.AddCard,
                                CardId: "proto.silent.backflip")
                        ]),
                    new PrototypeEventChoiceDefinition(
                        "leave",
                        "Leave the annex",
                        [])
                ],
                MinAct: 2,
                Weight: 1),
            new PrototypeEventDefinition(
                "proto.event.forbidden_archive",
                "Forbidden Archive",
                [
                    new PrototypeEventChoiceDefinition(
                        "read",
                        "Take Wraith Form and an Infection",
                        [
                            new(
                                PrototypeRunEffectKind.AddCard,
                                CardId: "proto.silent.wraith_form"),
                            new(
                                PrototypeRunEffectKind.AddCard,
                                CardId: "proto.status.infection")
                        ]),
                    new PrototypeEventChoiceDefinition(
                        "sell",
                        "Sell the sealed folio",
                        [
                            new(
                                PrototypeRunEffectKind.GainGold,
                                70)
                        ]),
                    new PrototypeEventChoiceDefinition(
                        "leave",
                        "Leave the archive untouched",
                        [])
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
        }.Concat(PrototypeNativeOvergrowthEvents.Definitions)
            .Append(PrototypeNativeOvergrowthEvents.NeowDefinition)
            .ToDictionary(evt => evt.Id, StringComparer.Ordinal);

    public static string[] OvergrowthWeakEncounterPool { get; } =
    [
        "proto.encounter.nibbits_weak",
        "proto.encounter.slimes_weak",
        "proto.encounter.fuzzy_wurm_crawler_weak",
        "proto.encounter.shrinker_beetle_weak"
    ];

    public static string[] OvergrowthNormalEncounterPool { get; } =
    [
        "proto.encounter.cubex_construct_normal",
        "proto.encounter.flyconid_normal",
        "proto.encounter.fogmog_normal",
        "proto.encounter.inklets_normal",
        "proto.encounter.mawler_normal",
        "proto.encounter.nibbits_normal",
        "proto.encounter.overgrowth_crawlers",
        "proto.encounter.ruby_raiders_normal",
        "proto.encounter.slimes_normal",
        "proto.encounter.slithering_strangler_normal",
        "proto.encounter.snapping_jaxfruit_normal",
        "proto.encounter.vine_shambler_normal"
    ];

    public static string[] OvergrowthEliteEncounterPool { get; } =
    [
        "proto.encounter.bygone_effigy_elite",
        "proto.encounter.byrdonis_elite",
        "proto.encounter.phrog_parasite_elite"
    ];

    public static string[] OvergrowthBossEncounterPool { get; } =
    [
        "proto.encounter.ceremonial_beast_boss",
        "proto.encounter.the_kin_boss",
        "proto.encounter.vantom_boss"
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        // Event-only combat: four non-stunned Wrigglers. Never included
        // in normal map-combat selection (Weight=0).
        new(
            "proto.native.encounter.dense_vegetation_event",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.wriggler",
                "proto.enemy.wriggler",
                "proto.enemy.wriggler",
                "proto.enemy.wriggler"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            // DenseVegetationEventEncounter.GenerateMonsters creates
            // four non-stunned Wrigglers, one in each native slot.
            Formation:
            [
                new("proto.enemy.wriggler", 0, "wriggler1"),
                new("proto.enemy.wriggler", 1, "wriggler2"),
                new("proto.enemy.wriggler", 2, "wriggler3"),
                new("proto.enemy.wriggler", 3, "wriggler4")
            ]),
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
            "proto.encounter.ruby_raiders_normal",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.assassin_ruby_raider",
                "proto.enemy.axe_ruby_raider",
                "proto.enemy.brute_ruby_raider",
                "proto.enemy.crossbow_ruby_raider",
                "proto.enemy.tracker_ruby_raider"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            FormationPolicy:
                PrototypeEncounterFormationPolicy.ChooseDistinct,
            EnemyPool:
            [
                "proto.enemy.assassin_ruby_raider",
                "proto.enemy.axe_ruby_raider",
                "proto.enemy.brute_ruby_raider",
                "proto.enemy.crossbow_ruby_raider",
                "proto.enemy.tracker_ruby_raider"
            ],
            EnemyCount: 3),
        new(
            "proto.encounter.mawler_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.mawler"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.mawler",
                    0)
            ]),
        new(
            "proto.encounter.cubex_construct_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.cubex_construct"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.cubex_construct",
                    0)
            ]),
        new(
            "proto.encounter.slithering_strangler_normal",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.slithering_strangler",
                "proto.enemy.snapping_jaxfruit",
                "proto.enemy.leaf_slime_m",
                "proto.enemy.twig_slime_m",
                "proto.enemy.leaf_slime_s",
                "proto.enemy.twig_slime_s"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            FormationVariants:
            [
                new PrototypeEncounterFormationVariant(
                    Formation:
                    [
                        new(
                            "proto.enemy.slithering_strangler",
                            0),
                        new(
                            "proto.enemy.snapping_jaxfruit",
                            1)
                    ]),
                new PrototypeEncounterFormationVariant(
                    Formation:
                    [
                        new(
                            "proto.enemy.slithering_strangler",
                            0)
                    ],
                    SelectionGroups:
                    [
                        new PrototypeEncounterSelectionGroup(
                            [
                                "proto.enemy.leaf_slime_m",
                                "proto.enemy.twig_slime_m"
                            ],
                            [1])
                    ]),
                new PrototypeEncounterFormationVariant(
                    Formation:
                    [
                        new(
                            "proto.enemy.slithering_strangler",
                            0),
                        new(
                            "proto.enemy.leaf_slime_s",
                            1),
                        new(
                            "proto.enemy.twig_slime_s",
                            2)
                    ])
            ]),
        new(
            "proto.encounter.vine_shambler_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.vine_shambler"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.vine_shambler",
                    0)
            ]),
        new(
            "proto.encounter.shrinker_beetle_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.shrinker_beetle"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.shrinker_beetle",
                    0)
            ]),
        new(
            "proto.encounter.overgrowth_crawlers",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.fuzzy_wurm_crawler",
                "proto.enemy.shrinker_beetle"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.fuzzy_wurm_crawler",
                    0),
                new(
                    "proto.enemy.shrinker_beetle",
                    1)
            ]),
        new(
            "proto.encounter.fuzzy_wurm_crawler_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.fuzzy_wurm_crawler"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.fuzzy_wurm_crawler",
                    0)
            ]),
        new(
            "proto.encounter.nibbits_weak",
            PrototypeRoomType.Combat,
            ["proto.enemy.nibbit"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.nibbit",
                    0)
            ]),
        new(
            "proto.encounter.nibbits_normal",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.nibbit",
                "proto.enemy.nibbit"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.nibbit",
                    0),
                new(
                    "proto.enemy.nibbit",
                    1)
            ]),
        new(
            "proto.encounter.slimes_weak",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.leaf_slime_s",
                "proto.enemy.twig_slime_s",
                "proto.enemy.leaf_slime_m",
                "proto.enemy.twig_slime_m"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            SelectionGroups:
            [
                new PrototypeEncounterSelectionGroup(
                    [
                        "proto.enemy.leaf_slime_s",
                        "proto.enemy.twig_slime_s"
                    ],
                    [0, 2],
                    ChooseDistinct: true),
                new PrototypeEncounterSelectionGroup(
                    [
                        "proto.enemy.leaf_slime_m",
                        "proto.enemy.twig_slime_m"
                    ],
                    [1])
            ]),
        new(
            "proto.encounter.inklets_normal",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.inklet",
                "proto.enemy.inklet",
                "proto.enemy.inklet"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.inklet",
                    0,
                    "left"),
                new(
                    "proto.enemy.inklet",
                    1,
                    "middle"),
                new(
                    "proto.enemy.inklet",
                    2,
                    "right")
            ]),
        new(
            "proto.encounter.ceremonial_beast_boss",
            PrototypeRoomType.Boss,
            ["proto.enemy.ceremonial_beast"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.ceremonial_beast",
                    0)
            ]),
        new(
            "proto.encounter.the_kin_boss",
            PrototypeRoomType.Boss,
            [
                "proto.enemy.kin_follower",
                "proto.enemy.kin_priest"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new PrototypeEncounterEnemySpec(
                    "proto.enemy.kin_follower",
                    0,
                    SlotName: "quick",
                    LeaderFormationPosition: 1),
                new PrototypeEncounterEnemySpec(
                    "proto.enemy.kin_priest",
                    1),
                new PrototypeEncounterEnemySpec(
                    "proto.enemy.kin_follower",
                    2,
                    SlotName: "dance",
                    LeaderFormationPosition: 1)
            ]),
        new(
            "proto.encounter.vantom_boss",
            PrototypeRoomType.Boss,
            ["proto.enemy.vantom"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.vantom",
                    0)
            ]),
        new(
            "proto.encounter.phrog_parasite_elite",
            PrototypeRoomType.Elite,
            ["proto.enemy.phrog_parasite", "proto.enemy.wriggler"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.phrog_parasite",
                    0)
            ]),
        new(
            "proto.encounter.bygone_effigy_elite",
            PrototypeRoomType.Elite,
            ["proto.enemy.bygone_effigy"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.bygone_effigy",
                    0)
            ]),
        new(
            "proto.encounter.byrdonis_elite",
            PrototypeRoomType.Elite,
            ["proto.enemy.byrdonis"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.byrdonis",
                    0)
            ]),
        new(
            "proto.encounter.fogmog_normal",
            PrototypeRoomType.Combat,
            ["proto.enemy.fogmog", "proto.enemy.eye_with_teeth"],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.fogmog",
                    0)
            ]),
        new(
            "proto.encounter.flyconid_normal",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.leaf_slime_m",
                "proto.enemy.twig_slime_m",
                "proto.enemy.flyconid"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.flyconid",
                    1)
            ],
            SelectionGroups:
            [
                new PrototypeEncounterSelectionGroup(
                    [
                        "proto.enemy.leaf_slime_m",
                        "proto.enemy.twig_slime_m"
                    ],
                    [0])
            ]),
        new(
            "proto.encounter.snapping_jaxfruit_normal",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.snapping_jaxfruit",
                "proto.enemy.flyconid"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.snapping_jaxfruit",
                    0),
                new(
                    "proto.enemy.flyconid",
                    1)
            ]),
        new(
            "proto.encounter.slimes_normal",
            PrototypeRoomType.Combat,
            [
                "proto.enemy.twig_slime_m",
                "proto.enemy.leaf_slime_m",
                "proto.enemy.leaf_slime_s",
                "proto.enemy.twig_slime_s"
            ],
            MinAct: 1,
            MaxAct: 1,
            Weight: 0,
            Formation:
            [
                new(
                    "proto.enemy.twig_slime_m",
                    0),
                new(
                    "proto.enemy.leaf_slime_m",
                    1)
            ],
            SelectionGroups:
            [
                new PrototypeEncounterSelectionGroup(
                    [
                        "proto.enemy.leaf_slime_s",
                        "proto.enemy.twig_slime_s"
                    ],
                    [2, 3],
                    ChooseDistinct: true)
            ]),
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
            "proto.encounter.knight_gang",
            PrototypeRoomType.Elite,
            [
                "proto.enemy.flail_knight",
                "proto.enemy.spectral_knight",
                "proto.enemy.magi_knight"
            ],
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
            NativeSilentCardIdSet.Contains(card.Id)
            && card.MechanicsImplemented
            && !card.MultiplayerOnly
            && card.RewardEligible
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

    public static string[] PotionPool { get; } = Potions.Keys
        .Where(id => id != "proto.native.neow.ambergris")
        .Order(StringComparer.Ordinal).ToArray();
    public static string[] RelicPool { get; } =
    [
        "proto.relic.lantern",
        "proto.relic.ink_bottle",
        "proto.relic.bag_of_preparation",
        "proto.relic.ornamental_fan",
        "proto.relic.nunchaku",
        "proto.relic.letter_opener",
        "proto.relic.happy_flower",
        "proto.relic.anchor",
        "proto.relic.vajra",
        "proto.relic.blood_vial",
        "proto.relic.meat_on_the_bone",
        "proto.relic.reptile_trinket",
        "proto.relic.old_coin",
        "proto.relic.mango",
        "proto.relic.lees_waffle",
        "proto.relic.meal_ticket",
        "proto.relic.lucky_fysh",
        "proto.relic.trail_ledger",
        "proto.relic.darkstone_periapt",
        "proto.relic.molten_egg",
        "proto.relic.toxic_egg",
        "proto.relic.frozen_egg",
        "proto.relic.bronze_scales",
        "proto.relic.oddly_smooth_stone",
        "proto.relic.bag_of_marbles",
        "proto.relic.gremlin_horn",
        "proto.relic.mercury_hourglass",
        "proto.relic.orichalcum",
        "proto.relic.horn_cleat",
        "proto.relic.captains_wheel",
        "proto.relic.kunai",
        "proto.relic.shuriken",
        "proto.relic.ice_cream",
        "proto.relic.tough_bandages",
        "proto.relic.tingsha",
        "proto.relic.hovering_kite",
        "proto.relic.question_card",
        "proto.relic.prayer_wheel",
        "proto.relic.white_beast_statue",
        "proto.relic.membership_card",
        "proto.relic.smiling_mask",
        "proto.relic.art_of_war",
        "proto.relic.charons_ashes",
        "proto.relic.joss_paper",
        "proto.relic.burning_sticks",
        "proto.relic.forgotten_soul",
        "proto.relic.toolbox",
        "proto.relic.gambling_chip"
    ];

    public static string[] BossRelicPool { get; } =
    [
        "proto.relic.sozu",
        "proto.relic.empty_cage",
        "proto.relic.astrolabe",
        "proto.relic.coffee_dripper",
        "proto.relic.fusion_hammer",
        "proto.relic.velvet_choker",
        "proto.relic.runic_dome",
        "proto.relic.snecko_eye"
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

    public static PrototypeEncounterDefinition Encounter(
        string id) =>
        Encounters.FirstOrDefault(encounter =>
            StringComparer.Ordinal.Equals(
                encounter.Id,
                id))
        ?? throw new KeyNotFoundException(
            $"Unknown prototype encounter '{id}'.");

    public static PrototypeStatusDefinition Status(string id) =>
        Statuses.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown prototype status '{id}'.");

    public static PrototypeEventDefinition Event(string id) =>
        Events.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown prototype event '{id}'.");
}
