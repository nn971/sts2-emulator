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
        RoomPool:
        [
            PrototypeRoomType.Combat,
            PrototypeRoomType.Combat,
            PrototypeRoomType.Combat,
            PrototypeRoomType.Combat,
            PrototypeRoomType.Event,
            PrototypeRoomType.Event,
            PrototypeRoomType.Shop,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Elite
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
                [new(PrototypeCombatEffectKind.DamageEnemy, 6, 3)]),
            new PrototypeCardDefinition(
                "proto.silent.defend",
                "Defend",
                1,
                PrototypeCardTarget.None,
                [new(PrototypeCombatEffectKind.GainPlayerBlock, 5, 3)]),
            new PrototypeCardDefinition(
                "proto.silent.neutralize",
                "Neutralize",
                0,
                PrototypeCardTarget.Enemy,
                [
                    new(PrototypeCombatEffectKind.DamageEnemy, 3, 1),
                    new(PrototypeCombatEffectKind.ApplyEnemyStatus, 1, 1, "proto.status.weak")
                ]),
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
                ]),
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
                [new(PrototypeCombatEffectKind.DamageEnemy, 5, 3)]),
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
                ExhaustOnUse: true),
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
                ]),
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
                "proto.silent.skewer",
                "Skewer",
                new PrototypeCardCostSpec(PrototypeCardCostKind.X),
                PrototypeCardTarget.Enemy,
                [
                    new(
                        PrototypeCombatEffectKind.DamageEnemy,
                        7,
                        3,
                        Repetitions: 0,
                        RepetitionsPerEnergySpent: 1)
                ]),
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
                ]),
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
                ]),
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
                ])
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
                [new(PrototypeCombatEffectKind.DamageEnemy, 20)])
        }.ToDictionary(potion => potion.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PrototypeRelicDefinition> Relics { get; } =
        new[]
        {
            new PrototypeRelicDefinition(
                "proto.relic.silent_ring",
                "Silent Ring",
                FirstTurnDrawBonus: 2),
            new PrototypeRelicDefinition(
                "proto.relic.lantern",
                "Lantern",
                EnergyPerTurnBonus: 1)
        }.ToDictionary(relic => relic.Id, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PrototypePlayerPowerDefinition> Powers { get; } =
        new[]
        {
            new PrototypePlayerPowerDefinition(
                "proto.power.dexterity",
                "Dexterity",
                BlockBonusPerStack: 1,
                Triggers: Array.Empty<PrototypePowerTriggerSpec>()),
            new PrototypePlayerPowerDefinition(
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
            new PrototypePlayerPowerDefinition(
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
            new PrototypePlayerPowerDefinition(
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
                ]),
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
                ])
        }.ToDictionary(evt => evt.Id, StringComparer.Ordinal);

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new("proto.encounter.crawler", PrototypeRoomType.Combat, ["proto.enemy.crawler"]),
        new("proto.encounter.raiders", PrototypeRoomType.Combat, ["proto.enemy.raider", "proto.enemy.crawler"]),
        new("proto.encounter.elite", PrototypeRoomType.Elite, ["proto.enemy.elite"]),
        new("proto.encounter.boss", PrototypeRoomType.Boss, ["proto.enemy.boss"])
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

    public static string[] RewardCardPool { get; } =
    [
        "proto.silent.strike",
        "proto.silent.defend",
        "proto.silent.backflip",
        "proto.silent.poisoned_stab",
        "proto.silent.slice",
        "proto.silent.acrobatics",
        "proto.silent.blade_dance",
        "proto.silent.dagger_spray",
        "proto.silent.skewer",
        "proto.silent.footwork",
        "proto.silent.noxious_fumes",
        "proto.silent.envenom"
    ];

    public static string[] PotionPool { get; } = Potions.Keys.Order(StringComparer.Ordinal).ToArray();
    public static string[] RelicPool { get; } = ["proto.relic.lantern"];

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

    public static PrototypePlayerPowerDefinition Power(string id) =>
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
