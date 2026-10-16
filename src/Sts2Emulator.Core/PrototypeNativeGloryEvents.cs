namespace Sts2Emulator.Core;

/// <summary>
/// v0.111.0 Glory event content for the experimental regional route.
/// Default runs remain gated until native later-act policy and RNG parity
/// are complete.
/// Sources: HungryForMushrooms, BigMushroom, FragrantMushroom,
/// TeaMaster, BoneTea, EmberTea and TeaOfDiscourtesy.
/// </summary>
public static class PrototypeNativeGloryEvents
{
    public const string HungryForMushroomsId =
        "proto.native.glory.hungry_for_mushrooms";
    public const string HungryForMushroomsNativeRngEventId =
        "HUNGRY_FOR_MUSHROOMS";
    public const string BigMushroomId =
        "proto.native.glory.big_mushroom";
    public const string FragrantMushroomId =
        "proto.native.glory.fragrant_mushroom";
    public const string ReflectionsId =
        "proto.native.glory.reflections";
    public const string BadLuckId =
        "proto.native.glory.bad_luck";
    public const string RoundTeaPartyId =
        "proto.native.glory.round_tea_party";
    public const string TeaMasterId =
        "proto.native.glory.tea_master";
    public const string BoneTeaId = "proto.relic.bone_tea";
    public const string EmberTeaId = "proto.relic.ember_tea";
    public const string TeaOfDiscourtesyId =
        "proto.relic.tea_of_discourtesy";
    public const string RoyalPoisonId =
        "proto.native.glory.royal_poison";
    public const string GraveOfTheForgottenId =
        "proto.native.glory.grave_of_the_forgotten";
    // Same source Curse model as the Hive Lost Wisp event.
    public const string DecayId = PrototypeNativeHiveEvents.DecayId;
    public const string ForgottenSoulId =
        "proto.relic.forgotten_soul";
    public const string TrialId = "proto.native.glory.trial";
    public const string TanxId = NativeTanxEventV111.EventId;
    public const string VakuuId = NativeVakuuEventV111.EventId;
    public const string RegretId = "proto.native.glory.regret";
    public const string ShameId = "proto.native.glory.shame";
    public const string DoubtId = "proto.native.glory.doubt";
    public const string TinkerTimeId = "proto.native.glory.tinker_time";
    public const string TinkerTimeNativeRngEventId = "TINKER_TIME";
    public const string MadScienceId = "proto.native.glory.mad_science";
    public const string CuriousId = "proto.native.glory.curious";
    public const string ImprovementId = "proto.native.glory.improvement";
    // Reuse the already implemented native Strangle trigger model.
    public const string StrangleId = "proto.power.strangle";
    public const string BattlewornDummyId =
        "proto.native.glory.battleworn_dummy";
    public const string BattlewornDummyNativeRngEventId =
        "BATTLEWORN_DUMMY";
    public const string BattlewornTimeLimitId =
        "proto.native.glory.battleworn_dummy_time_limit";
    public const string BattleFriendV1Id =
        "proto.native.glory.battle_friend_v1";
    public const string BattleFriendV2Id =
        "proto.native.glory.battle_friend_v2";
    public const string BattleFriendV3Id =
        "proto.native.glory.battle_friend_v3";
    public const string BattlewornV1EncounterId =
        "proto.native.glory.battleworn_dummy_event_v1_encounter";
    public const string BattlewornV2EncounterId =
        "proto.native.glory.battleworn_dummy_event_v2_encounter";
    public const string BattlewornV3EncounterId =
        "proto.native.glory.battleworn_dummy_event_v3_encounter";

    /// <summary>
    /// Source IsAllowed predicates applied before a Glory event enters the
    /// experimental route's event draw. Round Tea Party checks current HP;
    /// Grave of the Forgotten needs a card that can receive Souls Power.
    /// Other currently registered Glory events have no audited
    /// player-state eligibility predicate.
    /// </summary>
    public static bool IsEligible(PrototypeEventDefinition evt,
        PlayerState player) =>
        (evt.Id != RoundTeaPartyId || player.Hp >= 12)
        && (evt.Id != TeaMasterId || player.Gold >= 150)
        && (evt.Id != GraveOfTheForgottenId
            || player.Deck.Any(card =>
                PrototypeGameEngine.CanSelectEventDeckCard(card,
                    PrototypePersistentDeckChoiceKind.Enchant, null,
                    PrototypeCardEnchantmentKind.SoulsPower, false)));


    public static PrototypeCardDefinition[] Cards { get; } =
    [
        // The single native MadScience model persists its selected
        // Type/Rider inside CardInstance.PersistentState. Runtime card
        // definitions are synthesized from that state on every play.
        new(MadScienceId, "Mad Science", 1, PrototypeCardTarget.Enemy,
            [], Rarity: PrototypeCardRarity.Event,
            Type: PrototypeCardType.Attack, RewardEligible: false,
            InnateOnUpgrade: true, CanBeGeneratedInCombat: false),
        // Native Trial curses; combat hook handles Regret's hand-size
        // unblockable damage and Shame/Doubt's turn-end debuffs.
        new(RegretId, "Regret", -1, PrototypeCardTarget.None, [],
            Unplayable: true, RewardEligible: false,
            MaxUpgradeLevel: 0,
            Rarity: PrototypeCardRarity.Curse, Type: PrototypeCardType.Curse),
        new(ShameId, "Shame", -1, PrototypeCardTarget.None, [],
            Unplayable: true, RewardEligible: false,
            MaxUpgradeLevel: 0,
            Rarity: PrototypeCardRarity.Curse, Type: PrototypeCardType.Curse),
        new(DoubtId, "Doubt", -1, PrototypeCardTarget.None, [],
            Unplayable: true, RewardEligible: false,
            MaxUpgradeLevel: 0,
            Rarity: PrototypeCardRarity.Curse, Type: PrototypeCardType.Curse),
        // Native BadLuck: unplayable Eternal Curse, 13 unblockable
        // damage while in hand when the player ends their turn.
        new(BadLuckId, "Bad Luck", -1, PrototypeCardTarget.None, [],
            Unplayable: true, Eternal: true, RewardEligible: false,
            MaxUpgradeLevel: 0,
            Rarity: PrototypeCardRarity.Curse, Type: PrototypeCardType.Curse,
            EndTurnDamageIfInHand: 13, EndTurnDamageUnblockable: true)
    ];

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        // BigMushroom.AfterObtained grants 20 Max HP (also heals
        // 20), then ModifyHandDraw subtracts two on turn one only.
        // The pickup callback is implemented by the event handler;
        // this relic is not in the ordinary relic reward pool.
        new(BigMushroomId, "Big Mushroom", FirstTurnDrawBonus: -2),
        // FragrantMushroom.AfterObtained deals 15 unblocked,
        // unpowered damage and upgrades two different eligible cards.
        new(FragrantMushroomId, "Fragrant Mushroom"),
        // RoundTeaParty.EnjoyTea grants this relic and heals to full.
        // RoyalPoison.AfterPlayerTurnStart deals 4 unblockable HP
        // damage to its owner on turn one of every combat.
        new(RoyalPoisonId, "Royal Poison"),
        new(BoneTeaId, "Bone Tea"),
        new(EmberTeaId, "Ember Tea"),
        new(TeaOfDiscourtesyId, "Tea of Discourtesy")
    ];

    public static PrototypeEventDefinition[] Events { get; } =
    [
        NativeTanxEventV111.Definition,
        NativeVakuuEventV111.Definition,
        NativeNonupeipeEventV111.Definition,
        new(HungryForMushroomsId, "Hungry for Mushrooms",
        [
            new("big_mushroom", "Obtain Big Mushroom", []),
            new("fragrant_mushroom", "Obtain Fragrant Mushroom", [])
        ], MinAct: 3, MaxAct: 3, Weight: 0),
        new(ReflectionsId, "Reflections",
        [
            new("touch_mirror", "Touch a mirror", []),
            new("shatter", "Shatter the mirrors", [])
        ], MinAct: 3, MaxAct: 3, Weight: 0),
        new(RoundTeaPartyId, "Round Tea Party",
        [
            new("enjoy_tea", "Enjoy tea and take Royal Poison", []),
            new("pick_fight", "Pick a fight", [])
        ], MinAct: 3, MaxAct: 3, Weight: 0),
        new(TeaMasterId, "Tea Master",
        [
            new("bone_tea", "Buy Bone Tea for 50 gold", []),
            new("ember_tea", "Buy Ember Tea for 150 gold", []),
            new("tea_of_discourtesy", "Take Tea of Discourtesy", [])
        ], MinAct: 3, MaxAct: 3, Weight: 0),
        // Native IsAllowed requires at least one exhaust-keyword
        // local deck card. Confront grants Decay before selecting one
        // qualifying card for Souls Power, removing its Exhaust.
        new(GraveOfTheForgottenId, "Grave of the Forgotten",
        [
            new("confront", "Take Decay and enchant an Exhaust card",
                [new(PrototypeRunEffectKind.AddCard, CardId: DecayId)],
                DeckChoice: new(
                    PrototypePersistentDeckChoiceKind.Enchant,
                    1, EnchantmentKind:
                        PrototypeCardEnchantmentKind.SoulsPower)),
            new("accept", "Obtain Forgotten Soul",
                [new(PrototypeRunEffectKind.GainRelic,
                    RelicId: ForgottenSoulId)])
        ], MinAct: 3, MaxAct: 3, Weight: 0),
        // The event registry includes every follow-up branch for
        // invariant validation. Trial's page resolver exposes only the
        // two current-page choices at any decision boundary.
        new(TinkerTimeId, "Tinker Time",
        [
            new("choose_card_type", "Choose a card type", []),
            new("attack", "Make an Attack", []),
            new("skill", "Make a Skill", []),
            new("power", "Make a Power", []),
            new("sapping", "Add Sapping", []),
            new("violence", "Add Violence", []),
            new("choking", "Add Choking", []),
            new("energized", "Add Energized", []),
            new("wisdom", "Add Wisdom", []),
            new("chaos", "Add Chaos", []),
            new("expertise", "Add Expertise", []),
            new("curious", "Add Curious", []),
            new("improvement", "Add Improvement", [])
        ], MinAct: 3, MaxAct: 3, Weight: 0),
        new(BattlewornDummyId, "Battleworn Dummy",
        [
            new("setting_1", "Train against a 75 HP dummy", []),
            new("setting_2", "Train against a 150 HP dummy", []),
            new("setting_3", "Train against a 300 HP dummy", [])
        ], MinAct: 3, MaxAct: 3, Weight: 0),
        new(TrialId, "Trial",
        [
            new("accept", "Stand trial", []),
            new("reject", "Reject the trial", []),
            new("double_down", "Open run-abandon confirmation", []),
            new("confirm_abandon", "Abandon the run", []),
            new("cancel_abandon", "Keep playing", []),
            new("merchant_guilty", "Find merchant guilty", []),
            new("merchant_innocent", "Find merchant innocent", [],
                DeckChoice: new(PrototypePersistentDeckChoiceKind.Upgrade, 2)),
            new("noble_guilty", "Find noble guilty", []),
            new("noble_innocent", "Find noble innocent", []),
            new("nondescript_guilty", "Find stranger guilty", []),
            new("nondescript_innocent", "Find stranger innocent", [],
                DeckChoice: new(PrototypePersistentDeckChoiceKind.Transform, 2))
        ], MinAct: 3, MaxAct: 3, Weight: 0)
    ];

    // Native Battleworn Time Limit counts down after the enemy side
    // ends, starting at three. Expiry escapes and yields no prizes.
    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new(BattlewornTimeLimitId, "Battleworn Dummy Time Limit",
            0, [], EnemyStacksDecayAtSideTurnEnd: 1),
        new(CuriousId, "Curious", 0, []),
        new(ImprovementId, "Improvement", 0, []) 
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        Dummy(BattleFriendV1Id, "Battle Friend V1", 75),
        Dummy(BattleFriendV2Id, "Battle Friend V2", 150),
        Dummy(BattleFriendV3Id, "Battle Friend V3", 300)
    ];

    private static PrototypeEnemyDefinition Dummy(string id, string name,
        int hp) => new(id, name, hp, 0,
        [new("nothing", [])],
        StartingPowers: [new(BattlewornTimeLimitId, 3)]);

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(BattlewornV1EncounterId, PrototypeRoomType.Combat,
            [BattleFriendV1Id], MinAct: 3, MaxAct: 3, Weight: 0),
        new(BattlewornV2EncounterId, PrototypeRoomType.Combat,
            [BattleFriendV2Id], MinAct: 3, MaxAct: 3, Weight: 0),
        new(BattlewornV3EncounterId, PrototypeRoomType.Combat,
            [BattleFriendV3Id], MinAct: 3, MaxAct: 3, Weight: 0)
    ];
}

/// <summary>
/// Pinned singleton Glory event fixture: relic pickup effects are source
/// ordered; Fragrant Mushroom selects TWO DISTINCT card instances, not
/// two independent upgrade rolls that might revisit a multiupgrade card.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeHungryForMushrooms(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        if (world.Act != 3 || world.Event?.EventId !=
            PrototypeNativeGloryEvents.HungryForMushroomsId)
        {
            throw new InvalidOperationException(
                "Hungry for Mushrooms requires its active Glory event.");
        }
        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        var relicId = choice switch
        {
            "big_mushroom" => PrototypeNativeGloryEvents.BigMushroomId,
            "fragrant_mushroom" =>
                PrototypeNativeGloryEvents.FragrantMushroomId,
            _ => throw new InvalidOperationException(
                $"Unsupported Hungry for Mushrooms choice '{choice}'.")
        };

        var player = state.Player with
        {
            Relics = state.Player.Relics.Append(
                new RelicInstance(relicId,
                    PrototypeJson.EmptyObject())).ToArray()
        };
        player = ApplyRelicRunEvent(
            player, PrototypeRunEventKind.RelicAcquired,
            acquiredRelicId: relicId, rng: state.Rng);

        if (choice == "big_mushroom")
        {
            player = player with
            {
                MaxHp = checked(player.MaxHp + 20),
                Hp = checked(player.Hp + 20)
            };
        }
        else
        {
            // Pinned AfterObtained performs the damage before any
            // upgrades. A fatal acquisition therefore terminates
            // immediately without upgrading cards.
            player = player with { Hp = Math.Max(0, player.Hp - 15) };
            if (player.Hp <= 0)
                return EndRun(state with { Player = player }, "defeat");

            var ids = player.Deck
                .Where(card => CanSelectEventDeckCard(card,
                    PrototypePersistentDeckChoiceKind.Upgrade,
                    null, null, false))
                .Select(card => card.InstanceId).ToArray();
            state = NativeEventContextRngV111.ShuffleInPlace(
                state,
                world.Event!,
                PrototypeNativeGloryEvents
                    .HungryForMushroomsNativeRngEventId,
                ids,
                "event.glory.random_upgrade.shuffle_swap_index",
                $"event:{world.Event?.EventId}:eligible_cards");
            world = RequireWorld(state);
            var upgrades = ids.Take(2).ToHashSet();
            player = player with
            {
                Deck = player.Deck.Select(card =>
                    upgrades.Contains(card.InstanceId)
                        ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                        : card).ToArray()
            };
        }

        return PreserveNativeEventCompletionPage(
            state with { Player = player }, choice);
    }
}
