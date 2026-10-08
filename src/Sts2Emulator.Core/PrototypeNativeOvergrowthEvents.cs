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
    public const string NeowEventId = "proto.native.event.neow";

    // The default Neow positive/curse subsets from v0.111.0 Neow.cs.
    // This intentionally excludes conditionally-added special choices
    // and unlock-filtering; their exact logic is still pending.
    public static string[] NeowPositiveRelicNames { get; } =
    [
        "ArcaneScroll", "BoomingConch", "FishingRod", "GoldenPearl",
        "Kaleidoscope", "LeadPaperweight", "LostCoffer", "MassiveScroll",
        "NeowsTorment", "NewLeaf", "PhialHolster", "PreciseScissors",
        "ScrollBoxes", "WingedBoots"
    ];

    public static string[] NeowCursedRelicNames { get; } =
    [
        "CursedPearl", "DowsingRod", "HeftyTablet", "LargeCapsule",
        "LeafyPoultice", "NeowsBones", "NeowsSacrifice",
        "PrecariousShears", "SilkenTress", "SilverCrucible"
    ];

    // Three paired selections are appended to the positive pool before
    // its two options are drawn. The Large Capsule curse suppresses the
    // Lava Rock / Small Capsule pair entirely in v0.111.0.
    public static string[] NeowConditionalPositiveRelicNames { get; } =
    [
        "LavaRock", "SmallCapsule", "NutritiousOyster",
        "StoneHumidifier", "NeowsTalisman", "Pomander"
    ];

    public static string NeowRelicId(string nativeClass) =>
        "proto.native.neow." + nativeClass.ToLowerInvariant();

    public static PrototypeRelicDefinition[] NeowRelicDefinitions { get; } =
        NeowPositiveRelicNames
            .Concat(NeowConditionalPositiveRelicNames)
            .Concat(NeowCursedRelicNames)
            .Select(name => new PrototypeRelicDefinition(
                NeowRelicId(name),
                name,
                ExtraPotionSlots: name == "PhialHolster" ? 1 : 0,
                AcquisitionDeckChoice: name switch
                {
                    "Pomander" => new PrototypeRelicDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Upgrade, 1),
                    "PreciseScissors" => new PrototypeRelicDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Remove, 1),
                    "NewLeaf" => new PrototypeRelicDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Transform, 1),
                    "PrecariousShears" => new PrototypeRelicDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Remove, 2),
                    _ => null
                },
                RunTriggers: name switch
                {
                    "SilkenTress" =>
                    [
                        new PrototypeRelicRunTriggerSpec(
                            PrototypeRunEventKind.RelicAcquired,
                            [new(PrototypeRunEffectKind.LoseAllGold)])
                    ],
                    "GoldenPearl" =>
                    [
                        new PrototypeRelicRunTriggerSpec(
                            PrototypeRunEventKind.RelicAcquired,
                            [new(PrototypeRunEffectKind.GainGold, 150)])
                    ],
                    "StoneHumidifier" =>
                    [
                        new PrototypeRelicRunTriggerSpec(
                            PrototypeRunEventKind.RestSiteHealed,
                            [new(PrototypeRunEffectKind.GainMaxHp, 5)])
                    ],
                    "NutritiousOyster" =>
                    [
                        new PrototypeRelicRunTriggerSpec(
                            PrototypeRunEventKind.RelicAcquired,
                            [new(PrototypeRunEffectKind.GainMaxHp, 11)])
                    ],
                    "NeowsTalisman" =>
                    [
                        new PrototypeRelicRunTriggerSpec(
                            PrototypeRunEventKind.RelicAcquired,
                            [
                                new(
                                    PrototypeRunEffectKind.UpgradeLastCardOfId,
                                    CardId: "proto.silent.strike"),
                                new(
                                    PrototypeRunEffectKind.UpgradeLastCardOfId,
                                    CardId: "proto.silent.defend")
                            ])
                    ],
                    _ => []
                }))
            .ToArray();

    public static PrototypeEventDefinition NeowDefinition { get; } =
        new(
            NeowEventId,
            "Neow",
            NeowPositiveRelicNames
                .Concat(NeowConditionalPositiveRelicNames)
                .Concat(NeowCursedRelicNames)
                .Select(name => new PrototypeEventChoiceDefinition(
                    "take_" + NeowRelicId(name),
                    "Choose " + name,
                    name switch
                    {
                        "CursedPearl" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.AddCard,
                                CardId: "proto.native.neow.greed"),
                            new(PrototypeRunEffectKind.GainGold, 333)
                        ],
                        "ArcaneScroll" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.GainRandomRareCard)
                        ],
                        "NeowsBones" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.OfferNeowsBonesRelics)
                        ],
                        "HeftyTablet" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.OfferThreeRareCards)
                        ],
                        "LostCoffer" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.OfferThreeCardsAndPotion)
                        ],
                        "ScrollBoxes" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.OfferCardBundles)
                        ],
                        "SmallCapsule" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.OfferRandomRelic)
                        ],
                        "NeowsTorment" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.AddCard,
                                CardId: "proto.native.neow.neows_fury")
                        ],
                        "DowsingRod" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.AddCard,
                                CardId: "proto.native.neow.dowsing")
                        ],
                        "PhialHolster" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.GainPotionSlots, 1),
                            new(PrototypeRunEffectKind.GainRandomPotion),
                            new(PrototypeRunEffectKind.GainRandomPotion)
                        ],
                        "LargeCapsule" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.GainRandomRelic),
                            new(PrototypeRunEffectKind.GainRandomRelic),
                            new(PrototypeRunEffectKind.AddCard,
                                CardId: "proto.silent.strike"),
                            new(PrototypeRunEffectKind.AddCard,
                                CardId: "proto.silent.defend")
                        ],
                        "NeowsSacrifice" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.GainPotion,
                                PotionId: "proto.native.neow.ambergris"),
                            new(PrototypeRunEffectKind.AddCard,
                                CardId: "proto.native.event.guilty")
                        ],
                        "PrecariousShears" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            // Native damage is resolved after both
                            // persistent-deck removal selections.
                            new(PrototypeRunEffectKind.LoseHpAfterDeckChoices, 16)
                        ],
                        "LeafyPoultice" =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name)),
                            new(PrototypeRunEffectKind.LoseMaxHp, 12),
                            new(PrototypeRunEffectKind.TransformFirstCardOfId,
                                CardId: "proto.silent.strike"),
                            new(PrototypeRunEffectKind.TransformFirstCardOfId,
                                CardId: "proto.silent.defend")
                        ],
                        _ =>
                        [
                            new(PrototypeRunEffectKind.GainRelic,
                                RelicId: NeowRelicId(name))
                        ]
                    }))
                .ToArray(),
            MaxAct: 1);

    public static string[] GenerateNeowOfferedChoiceIds(RngBundle rng)
    {
        // Mirrors the native Neow.GenerateInitialOptions decision graph,
        // while still using the emulator RNG (not native call-order parity).
        // Unlock/IsAllowedAtNeow filtering is not yet translated.
        var cursed = NeowCursedRelicNames[
            PrototypeRng.NextInt(rng, "event", NeowCursedRelicNames.Length)];
        var positives = NeowPositiveRelicNames.ToList();

        // Suppress a positive whose corresponding curse is offered.
        // This pairing is handled before conditional options are added.
        switch (cursed)
        {
            case "CursedPearl":
                positives.Remove("GoldenPearl");
                break;
            case "HeftyTablet":
                positives.Remove("ArcaneScroll");
                break;
            case "LeafyPoultice":
                positives.Remove("NewLeaf");
                break;
            case "PrecariousShears":
                positives.Remove("PreciseScissors");
                break;
            case "NeowsSacrifice":
                positives.Remove("PhialHolster");
                positives.Remove("LostCoffer");
                break;
        }

        if (cursed != "LargeCapsule")
        {
            positives.Add(PrototypeRng.NextInt(rng, "event", 2) == 0
                ? "LavaRock" : "SmallCapsule");
        }

        positives.Add(PrototypeRng.NextInt(rng, "event", 2) == 0
            ? "NutritiousOyster" : "StoneHumidifier");
        positives.Add(PrototypeRng.NextInt(rng, "event", 2) == 0
            ? "NeowsTalisman" : "Pomander");

        // Massive Scroll's native IsAllowed requires more than one
        // player. Retain its catalog entry for reference, but never
        // offer it in this explicitly single-player generator.
        positives.Remove("MassiveScroll");

        // Scope restriction for the Silent-only prototype: Kaleidoscope
        // rewards from other character pools are deliberately postponed.
        // Keep the native identity in the catalog for future parity work,
        // but never offer it in the current restricted training profile.
        positives.Remove("Kaleidoscope");

        var shuffled = positives.ToArray();
        PrototypeRng.Shuffle(rng, "event", shuffled);
        return shuffled.Take(2).Append(cursed)
            .Select(name => "take_" + NeowRelicId(name))
            .ToArray();
    }

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
                new("trudge", "Lose 8 HP, gain 61–99 gold", []),
                new("rest", "Rest, then fight four Wrigglers", [])
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
                new("tribute", "Pay 100–149 gold, gain a relic",
                    [
                        new(PrototypeRunEffectKind.LoseGold, 149),
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
                new("plant", "Enchant a card with Sown", [],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Enchant, 1,
                        EnchantmentKind: PrototypeCardEnchantmentKind.Sown))
            ],
            MaxAct: 1),
        new(
            "proto.native.event.sunken_statue",
            "Sunken Statue",
            [
                new("grab", "Gain Sword of Stone (counter not yet modeled)",
                    [new(PrototypeRunEffectKind.GainRelic,
                        RelicId: "proto.native.event.sword_of_stone")]),
                new("dive", "Lose 7 HP, gain 101–121 gold",
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
                new("decipher", "Decipher Tablet of Truth",
                    []),
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
                new("bathe", "Remove one card, then gain Guilty",
                    [new(PrototypeRunEffectKind.AddCardAfterDeckChoices,
                        CardId: "proto.native.event.guilty")],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Remove, 1))
            ],
            MaxAct: 1),
        new(
            "proto.native.event.whispering_hollow",
            "Whispering Hollow",
            [
                new("gold", "Pay 26–44 gold for two potions",
                    [
                        new(PrototypeRunEffectKind.LoseGold, 35),
                        new(PrototypeRunEffectKind.GainRandomPotion),
                        new(PrototypeRunEffectKind.GainRandomPotion)
                    ]),
                new("hug", "Transform one card, then lose 9 HP",
                    [new(PrototypeRunEffectKind.LoseHpAfterDeckChoices, 9)],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Transform, 1))
            ],
            MaxAct: 1),
        new(
            "proto.native.event.wood_carvings",
            "Wood Carvings",
            [
                new("bird", "Transform a basic card into Peck", [],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Transform, 1,
                        TransformToCardId: "proto.native.event.peck",
                        BasicCardsOnly: true)),
                new("snake", "Enchant a card with Slither", [],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Enchant, 1,
                        EnchantmentKind: PrototypeCardEnchantmentKind.Slither)),
                new("torus", "Transform a basic card into Toric Toughness", [],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Transform, 1,
                        TransformToCardId: "proto.native.event.toric_toughness",
                        BasicCardsOnly: true))
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
