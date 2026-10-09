namespace Sts2Emulator.Core;

/// <summary>
/// First source-backed v0.111.0 Underdocks event slice.
/// Unimplemented region events remain excluded rather than being
/// represented by unrelated Overgrowth or synthetic prototype events.
/// </summary>
public static class PrototypeNativeUnderdocksEvents
{
    public static string[] NativeRegionEventIds { get; } =
    [
        "proto.native.underdocks.abyssal_baths",
        "proto.native.underdocks.drowning_beacon",
        "proto.native.underdocks.endless_conveyor",
        "proto.native.underdocks.punch_off",
        "proto.native.underdocks.spiraling_whirlpool",
        "proto.native.underdocks.sunken_statue",
        "proto.native.underdocks.sunken_treasury",
        "proto.native.underdocks.doors_of_light_and_dark",
        "proto.native.underdocks.trash_heap",
        "proto.native.underdocks.waterlogged_scriptorium"
    ];

    public static string[] SupportedRegionEventIds { get; } =
    [
        "proto.native.underdocks.abyssal_baths",
        "proto.native.underdocks.drowning_beacon",
        "proto.native.underdocks.endless_conveyor",
        "proto.native.underdocks.punch_off",
        "proto.native.underdocks.trash_heap",
        "proto.native.underdocks.waterlogged_scriptorium",
        "proto.native.underdocks.spiraling_whirlpool",
        "proto.native.underdocks.sunken_treasury",
        "proto.native.underdocks.doors_of_light_and_dark",
        "proto.native.underdocks.sunken_statue"
    ];

    /// <summary>
    /// Each native event supplies its own eligibility when it has
    /// prerequisites. Spiral requires an enchantable basic Strike/Defend.
    /// </summary>
    public static bool IsEligible(
        PrototypeEventDefinition evt, PlayerState player) =>
        evt.Id switch
        {
            PrototypeNativeEndlessConveyor.EventId =>
                player.Gold >= 120,
            "proto.native.underdocks.waterlogged_scriptorium" =>
                player.Gold >= 55,
            PrototypeNativeUnderdocksTrashHeap.EventId =>
                player.Hp > 5,
            "proto.native.underdocks.spiraling_whirlpool" =>
                player.Deck.Any(card =>
                    PrototypeGameEngine.CanSelectEventDeckCard(
                        card, PrototypePersistentDeckChoiceKind.Enchant,
                        null, PrototypeCardEnchantmentKind.Spiral, true)),
            _ => true
        };

    public static bool IsFloorEligible(
        PrototypeEventDefinition evt, RunWorldState world) =>
        evt.Id != PrototypeNativePunchOff.EventId || world.Floor >= 6;

    public static bool AllowsEmptyDeckChoice(string eventId) =>
        StringComparer.Ordinal.Equals(
            eventId, "proto.native.underdocks.waterlogged_scriptorium");

    public static PrototypeEventDefinition[] Definitions { get; } =
    [
        // Specialized handlers resolve the native staged choices and
        // source-ordered weighted/random items.
        new(
            PrototypeNativeEndlessConveyor.EventId,
            "Endless Conveyor",
            [
                new("grab", "Take the current conveyor dish", []),
                new("observe", "Observe the chef (upgrade one random card)",
                    []),
                new("leave", "Leave the conveyor", [])
            ],
            MaxAct: 1),
        new(
            PrototypeNativePunchOff.EventId,
            "Punch Off",
            [
                new("nab", "Take an Injury and offer one relic", []),
                new("take_them", "Challenge the Punch Constructs", []),
                new("fight", "Fight both Punch Constructs", [])
            ],
            MaxAct: 1),
        // Source: WaterloggedScriptorium.cs + Steady.cs (v0.111.0).
        // The initial event requires 55 gold, but Bloody Ink does
        // not spend any. Both paid choices use the shared deck
        // enchantment continuation, including fewer eligible cards
        // than the selected count (native auto-selects all of them).
        new(
            "proto.native.underdocks.waterlogged_scriptorium",
            "Waterlogged Scriptorium",
            [
                new("bloody_ink", "Gain 6 maximum HP",
                    [new(PrototypeRunEffectKind.GainMaxHp, 6)]),
                new("tentacle_quill", "Spend 55 gold to enchant one card with Steady",
                    [new(PrototypeRunEffectKind.LoseGold, 55)],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Enchant, 1,
                        EnchantmentKind: PrototypeCardEnchantmentKind.Steady)),
                new("prickly_sponge", "Spend 99 gold to enchant two cards with Steady",
                    [new(PrototypeRunEffectKind.LoseGold, 99)],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Enchant, 2,
                        EnchantmentKind: PrototypeCardEnchantmentKind.Steady))
            ],
            MaxAct: 1),
        // Source: TrashHeap.cs; random acquisition is routed through the
        // source-backed specialized handler rather than fixed Effects.
        new(
            PrototypeNativeUnderdocksTrashHeap.EventId,
            "Trash Heap",
            [
                new("dive", "Lose 8 HP and find one of five relics", []),
                new("grab", "Gain 100 gold and one of ten event cards", [])
            ],
            MaxAct: 1),
        // Source: DrowningBeacon.cs. Bottle offers a fixed potion
        // through a native-style optional potion reward prompt;
        // climb loses 13 max HP before granting the Fresnel Lens.
        new(
            "proto.native.underdocks.drowning_beacon",
            "Drowning Beacon",
            [
                new("bottle", "Bottle the water (offer Glowwater Potion)",
                    [new(PrototypeRunEffectKind.GainPotion,
                        PotionId: "proto.native.underdocks.glowwater_potion")]),
                new("climb", "Climb (-13 max HP, gain Fresnel Lens)",
                    [
                        new(PrototypeRunEffectKind.LoseMaxHp, 13),
                        new(PrototypeRunEffectKind.GainRelic,
                            RelicId: "proto.native.underdocks.fresnel_lens")
                    ])
            ],
            MaxAct: 1),
        // Source: SpiralingWhirlpool.cs. The native Spiral enchantment
        // targets basic Strike- or Defend-tagged cards. The common event
        // deck-choice continuation handles player selection.
        new(
            "proto.native.underdocks.spiraling_whirlpool",
            "Spiraling Whirlpool",
            [
                new("observe", "Enchant a basic Strike or Defend with Spiral",
                    [],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Enchant, 1,
                        EnchantmentKind: PrototypeCardEnchantmentKind.Spiral,
                        BasicCardsOnly: true)),
                new("drink", "Heal 33% of maximum HP",
                    [new(PrototypeRunEffectKind.HealPercentMaxHp, 33)])
            ],
            MaxAct: 1),
        // Source: AbyssalBaths.cs. These describe the initial page;
        // the engine's staged handler generates Linger/Exit afterward,
        // with progressively increasing unblockable damage.
        new(
            "proto.native.underdocks.abyssal_baths",
            "Abyssal Baths",
            [
                new("immerse", "Immerse (+2 max HP; take 3 damage)",
                    [
                        new(PrototypeRunEffectKind.GainMaxHp, 2),
                        new(PrototypeRunEffectKind.LoseHp, 3)
                    ]),
                new("abstain", "Abstain (heal 10)",
                    [new(PrototypeRunEffectKind.Heal, 10)])
            ],
            MaxAct: 1),
        // Source: SunkenStatue.cs. The gold roll is 111 + [-10,10]
        // and is captured once when the event is selected.
        new(
            "proto.native.underdocks.sunken_statue",
            "Sunken Statue",
            [
                new("sword", "Take Sword of Stone",
                    [new(PrototypeRunEffectKind.GainRelic,
                        RelicId: "proto.native.event.sword_of_stone")]),
                new("dive", "Dive for gold (lose 7 HP)",
                    [
                        new(PrototypeRunEffectKind.GainGold, 111),
                        new(PrototypeRunEffectKind.LoseHp, 7)
                    ])
            ],
            MaxAct: 1),
        new(
            "proto.native.underdocks.sunken_treasury",
            "Sunken Treasury",
            [
                new("first_chest", "Open the first chest (52–67 gold)",
                    [new(PrototypeRunEffectKind.GainGold, 60)]),
                new("second_chest", "Open the second chest (303–363 gold and Greed)",
                    [
                        new(PrototypeRunEffectKind.GainGold, 333),
                        new(PrototypeRunEffectKind.AddCard,
                            CardId: "proto.native.underdocks.greed")
                    ])
            ],
            MaxAct: 1),
        new(
            "proto.native.underdocks.doors_of_light_and_dark",
            "Doors of Light and Dark",
            [
                new("light", "Upgrade up to two random cards",
                    [
                        new(PrototypeRunEffectKind.UpgradeRandomCard),
                        new(PrototypeRunEffectKind.UpgradeRandomCard)
                    ]),
                new("dark", "Remove one card from the deck", [],
                    new PrototypeEventDeckChoiceSpec(
                        PrototypePersistentDeckChoiceKind.Remove, 1))
            ],
            MaxAct: 1)
    ];

    public static bool IsNativeRegionEvent(string eventId) =>
        NativeRegionEventIds.Contains(eventId, StringComparer.Ordinal);

    public static bool UsesNativePotionOffer(string eventId) =>
        StringComparer.Ordinal.Equals(
            eventId, "proto.native.underdocks.drowning_beacon")
        || StringComparer.Ordinal.Equals(
            eventId, PrototypeNativeEndlessConveyor.EventId);

    public static bool IsSupported(string eventId) =>
        SupportedRegionEventIds.Contains(eventId, StringComparer.Ordinal);
}
