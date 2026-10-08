namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 Overgrowth region-event identity and choice catalogue.
/// Native IDs, headline options and uncomplicated numeric rewards derive
/// from Overgrowth.AllEvents and the individual event models.
/// Complex options are explicit runnable approximations until their
/// multi-page, combat, enchantment, pet, and relic-pool machinery is ported.
/// </summary>
public static class PrototypeNativeOvergrowthEvents
{
    public static string[] RegionEventIds { get; } =
    [
        "proto.native.event.aroma_of_chaos",
        "proto.native.event.byrdonis_nest",
        "proto.native.event.dense_vegetation",
        "proto.native.event.jungle_maze_adventure",
        "proto.native.event.luminous_choir",
        "proto.native.event.morphic_grove",
        "proto.native.event.sapphire_seed",
        "proto.native.event.sunken_statue",
        "proto.native.event.tablet_of_truth",
        "proto.native.event.unrest_site",
        "proto.native.event.wellspring",
        "proto.native.event.whispering_hollow",
        "proto.native.event.wood_carvings"
    ];

    public static PrototypeEventDefinition[] Definitions { get; } =
    [
        new(
            "proto.native.event.aroma_of_chaos",
            "Aroma of Chaos",
            [
                new("let_go", "Transform one card", [],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Transform, 1)),
                new("maintain_control", "Upgrade one card", [],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Upgrade, 1))
            ],
            MaxAct: 1),
        new(
            "proto.native.event.byrdonis_nest",
            "Byrdonis Nest",
            [
                new("eat", "Gain 7 Max HP",
                    [new(PrototypeRunEffectKind.GainMaxHp, 7)]),
                new("take", "Take Byrdonis Egg",
                    [new(PrototypeRunEffectKind.AddCard,
                        CardId: "proto.native.event.byrdonis_egg")])
            ],
            MaxAct: 1),
        new(
            "proto.native.event.dense_vegetation",
            "Dense Vegetation",
            [
                new("trudge", "Lose 8 HP, gain gold (approximate)",
                    [
                        new(PrototypeRunEffectKind.LoseHp, 8),
                        new(PrototypeRunEffectKind.GainGold, 80)
                    ]),
                new("rest", "Rest (follow-up fight not yet simulated)",
                    [new(PrototypeRunEffectKind.HealPercentMaxHp, 30)])
            ],
            MaxAct: 1),
        new(
            "proto.native.event.jungle_maze_adventure",
            "Jungle Maze Adventure",
            [
                new("solo", "Lose 18 HP, gain gold (approximate)",
                    [
                        new(PrototypeRunEffectKind.LoseHp, 18),
                        new(PrototypeRunEffectKind.GainGold, 150)
                    ]),
                new("join", "Gain gold (approximate)",
                    [new(PrototypeRunEffectKind.GainGold, 50)])
            ],
            MaxAct: 1),
        new(
            "proto.native.event.luminous_choir",
            "Luminous Choir",
            [
                new("reach", "Remove two cards and gain Spore Mind",
                    [new(PrototypeRunEffectKind.AddCard,
                        CardId: "proto.native.event.spore_mind")],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Remove, 2)),
                new("tribute", "Pay gold, gain a relic (price approximate)",
                    [
                        new(PrototypeRunEffectKind.LoseGold, 120),
                        new(PrototypeRunEffectKind.GainRandomRelic)
                    ])
            ],
            MaxAct: 1),
        new(
            "proto.native.event.morphic_grove",
            "Morphic Grove",
            [
                new("group", "Lose all gold, transform two cards",
                    [new(PrototypeRunEffectKind.LoseAllGold)],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Transform, 2)),
                new("loner", "Gain 5 Max HP",
                    [new(PrototypeRunEffectKind.GainMaxHp, 5)])
            ],
            MaxAct: 1),
        new(
            "proto.native.event.sapphire_seed",
            "Sapphire Seed",
            [
                new("eat", "Heal 9 HP and upgrade one card",
                    [new(PrototypeRunEffectKind.Heal, 9)],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Upgrade, 1)),
                new("plant", "Plant Sown enchantment (not yet modeled)", [])
            ],
            MaxAct: 1),
        new(
            "proto.native.event.sunken_statue",
            "Sunken Statue",
            [
                new("grab", "Gain Sword of Stone (counter not yet modeled)",
                    [new(PrototypeRunEffectKind.GainRelic,
                        RelicId: "proto.native.event.sword_of_stone")]),
                new("dive", "Lose 7 HP, gain gold (approximate)",
                    [
                        new(PrototypeRunEffectKind.LoseHp, 7),
                        new(PrototypeRunEffectKind.GainGold, 111)
                    ])
            ],
            MaxAct: 1),
        new(
            "proto.native.event.tablet_of_truth",
            "Tablet of Truth",
            [
                new("decipher", "Lose 3 Max HP and upgrade a random card (first page only)",
                    [
                        new(PrototypeRunEffectKind.LoseMaxHp, 3),
                        new(PrototypeRunEffectKind.UpgradeRandomCard)
                    ]),
                new("smash", "Heal 20 HP",
                    [new(PrototypeRunEffectKind.Heal, 20)])
            ],
            MaxAct: 1),
        new(
            "proto.native.event.unrest_site",
            "Unrest Site",
            [
                new("rest", "Heal to full and gain Poor Sleep",
                    [
                        new(PrototypeRunEffectKind.HealToFull),
                        new(PrototypeRunEffectKind.AddCard,
                            CardId: "proto.native.event.poor_sleep")
                    ]),
                new("kill", "Lose 8 Max HP and gain a relic",
                    [
                        new(PrototypeRunEffectKind.LoseMaxHp, 8),
                        new(PrototypeRunEffectKind.GainRandomRelic)
                    ])
            ],
            MaxAct: 1),
        new(
            "proto.native.event.wellspring",
            "Wellspring",
            [
                new("bottle", "Gain one random potion",
                    [new(PrototypeRunEffectKind.GainRandomPotion)]),
                new("bathe", "Remove one card and gain Guilty (effect order approximate)",
                    [new(PrototypeRunEffectKind.AddCard,
                        CardId: "proto.native.event.guilty")],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Remove, 1))
            ],
            MaxAct: 1),
        new(
            "proto.native.event.whispering_hollow",
            "Whispering Hollow",
            [
                new("gold", "Pay gold for two potions (price approximate)",
                    [
                        new(PrototypeRunEffectKind.LoseGold, 35),
                        new(PrototypeRunEffectKind.GainRandomPotion),
                        new(PrototypeRunEffectKind.GainRandomPotion)
                    ]),
                new("hug", "Transform one card and lose 9 HP",
                    [new(PrototypeRunEffectKind.LoseHp, 9)],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Transform, 1))
            ],
            MaxAct: 1),
        new(
            "proto.native.event.wood_carvings",
            "Wood Carvings",
            [
                new("bird", "Transform a basic card to Peck (random-transform approximation)",
                    [],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Transform, 1)),
                new("snake", "Apply Slither enchantment (not yet modeled)", []),
                new("torus", "Transform a basic card to Toric Toughness (random-transform approximation)",
                    [],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Transform, 1))
            ],
            MaxAct: 1)
    ];

    public static bool IsNativeRegionEvent(string id) =>
        RegionEventIds.Contains(id, StringComparer.Ordinal);

    public static bool IsEligible(
        PrototypeEventDefinition evt,
        PlayerState player)
    {
        var id = evt.Id;
        if (id == "proto.native.event.unrest_site")
        {
            return player.Hp * 10 <= player.MaxHp * 7;
        }

        if (id == "proto.native.event.whispering_hollow")
        {
            return player.Gold >= 44;
        }

        if (id == "proto.native.event.morphic_grove")
        {
            return player.Gold >= 100
                && player.Deck.Count(card =>
                    !PrototypeContent.Card(card.CardId).Eternal) >= 2;
        }

        if (id == "proto.native.event.luminous_choir")
        {
            return player.Gold >= 100
                && PrototypeContent.RelicPool.Any(relicId =>
                    !player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(
                            relicId, relic.RelicId)));
        }

        if (id == "proto.native.event.wood_carvings")
        {
            return player.Deck.Any(card =>
                PrototypeContent.Card(card.CardId).Rarity
                    == PrototypeCardRarity.Basic
                && !PrototypeContent.Card(card.CardId).Eternal);
        }

        if (id == "proto.native.event.byrdonis_nest")
        {
            return !player.Deck.Any(card =>
                card.CardId == "proto.native.event.byrdonis_egg");
        }

        return true;
    }
}
