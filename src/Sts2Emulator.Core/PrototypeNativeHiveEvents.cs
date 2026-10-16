namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 Hive event inventory and executable event models.
/// The experimental three-act route opts in to supported Hive events;
/// default run selection remains gated pending native parity.
/// Source: Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20,
/// Hive.cs and InfestedAutomaton.cs.
/// </summary>
public static class PrototypeNativeHiveEvents
{
    public const string InfestedAutomatonId =
        "proto.native.hive.infested_automaton";
    public const string InfestedAutomatonNativeRngEventId =
        "INFESTED_AUTOMATON";
    public const string StoneOfAllTimeId =
        PrototypeNativeStoneOfAllTime.EventId;

    // The ten native Hive.AllEvents entries, in source order. The opted-in
    // regional pool excludes Colorful Philosophers until its foreign card
    // pools and acquisition mechanics are implemented.
    public static string[] NativeEventNames { get; } =
    [
        "Amalgamator", "Bugslayer", "ColorfulPhilosophers",
        "ColossalFlower", "FieldOfManSizedHoles", "InfestedAutomaton",
        "LostWisp", "SpiritGrafter", "TheLanternKey", "ZenWeaver"
    ];

    // The isolated two-node Glory quest fixture is never a production
    // map-generation profile, and is validated independently.
    public const string RepyFixtureMapProfileId =
        "v111-glory-repy-quest-fixture-v1";

    public const string WarHistorianRepyId =
        "proto.native.glory.war_historian_repy";
    public const string HistoryCourseId =
        "proto.native.glory.history_course";

    public const string LanternKeyEventId = "proto.native.hive.the_lantern_key";
    public const string LanternKeyCardId = "proto.native.hive.lantern_key";
    public const string MysteriousKnightId = "proto.native.hive.mysterious_knight";
    public const string MysteriousKnightEncounterId = "proto.native.hive.encounter.mysterious_knight_event_encounter";
    public const string PlatingPowerId = "proto.native.hive.plating";

    public const string FieldOfManSizedHolesId =
        "proto.native.hive.field_of_man_sized_holes";
    public const string NormalityId = "proto.native.hive.normality";

    public const string AmalgamatorId = "proto.native.hive.amalgamator";
    public const string UltimateStrikeId = "proto.colorless.ultimate_strike";
    public const string UltimateDefendId = "proto.colorless.ultimate_defend";

    public const string ColossalFlowerId = "proto.native.hive.colossal_flower";
    public const string SpiritGrafterId = "proto.native.hive.spirit_grafter";
    public const string PollinousCoreId = "proto.native.hive.pollinous_core";
    public const string MetamorphosisId = "proto.native.hive.metamorphosis";

    public const string LostWispId = "proto.native.hive.lost_wisp";
    public const string LostWispNativeRngEventId = "LOST_WISP";
    public const string ZenWeaverId = "proto.native.hive.zen_weaver";
    public const string DecayId = "proto.native.hive.decay";
    public const string EnlightenmentId = "proto.native.hive.enlightenment";
    public const string LostWispRelicId = "proto.native.hive.lost_wisp_relic";

    public static bool IsNativeEvent(string id) =>
        id is LanternKeyEventId or FieldOfManSizedHolesId or AmalgamatorId
            or InfestedAutomatonId or LostWispId or ZenWeaverId
            or ColossalFlowerId or SpiritGrafterId
            or StoneOfAllTimeId
            or PrototypeNativeLaterActs.HiveBugslayerId;

    public static bool AllowsEmptyDeckChoice(string eventId) =>
        eventId is SpiritGrafterId or FieldOfManSizedHolesId;

    public static bool IsEligible(PrototypeEventDefinition evt,
        PlayerState player) =>
        (evt.Id != AmalgamatorId
            || (CountBasic(player, "Strike") >= 2
                && CountBasic(player, "Defend") >= 2))
        && (evt.Id != FieldOfManSizedHolesId
            || player.Deck.Any(card =>
                PrototypeGameEngine.CanSelectEventDeckCard(
                    card, PrototypePersistentDeckChoiceKind.Enchant,
                    null, PrototypeCardEnchantmentKind.PerfectFit, false)))
        && (evt.Id != ZenWeaverId || player.Gold >= 125)
        && (evt.Id != ColossalFlowerId || player.Hp >= 19)
        && (evt.Id != StoneOfAllTimeId
            || player.PotionSlots.Any(potion => potion is not null));

    private static int CountBasic(PlayerState player, string tag) =>
        player.Deck.Count(card =>
            !PrototypeCardRules.IsEternal(card)
            && PrototypeContent.Card(card.CardId).Rarity
                == PrototypeCardRarity.Basic
            && (PrototypeContent.Card(card.CardId).Tags?.Contains(
                tag, StringComparer.Ordinal) ?? false));

    public static PrototypeCardDefinition[] Cards { get; } =
    [
        // Quest card from The Lantern Key event combat; the Glory
        // continuation is explicitly deferred with native routing.
        new(LanternKeyCardId, "Lantern Key", -1,
            PrototypeCardTarget.None, [],
            Rarity: PrototypeCardRarity.Quest,
            Type: PrototypeCardType.Quest, Unplayable: true,
            RewardEligible: false, MaxUpgradeLevel: 0),
        // Normality.ShouldPlay: while one is in hand, at most three
        // cards can be started per turn. An unplayable, unupgradable
        // Curse which is never offered in normal rewards.
        new(NormalityId, "Normality", -1,
            PrototypeCardTarget.None, [],
            Rarity: PrototypeCardRarity.Curse,
            Type: PrototypeCardType.Curse, Unplayable: true,
            RewardEligible: false, MaxUpgradeLevel: 0),
        // Metamorphosis: 3 (5) random character Attacks, each made
        // free for this combat and inserted at a random draw-pile index.
        new(MetamorphosisId, "Metamorphosis", 2,
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile,
                3, UpgradeDelta: 2)],
            ExhaustOnUse: true, Rarity: PrototypeCardRarity.Event,
            Type: PrototypeCardType.Skill, RewardEligible: false),
        // Source: Decay.cs. Each unplayable Curse deals 2 unpowered
        // blockable damage if held at turn end.
        new(DecayId, "Decay", -1, PrototypeCardTarget.None, [],
            Rarity: PrototypeCardRarity.Curse, Type: PrototypeCardType.Curse,
            RewardEligible: false, Unplayable: true,
            EndTurnDamageIfInHand: 2, MaxUpgradeLevel: 0),
        // Enlightenment.cs. Its upgraded cost modification persists
        // for the rest of combat, unlike the base turn-scoped version.
        new(EnlightenmentId, "Enlightenment", 0,
            PrototypeCardTarget.None,
            [new(PrototypeCombatEffectKind.ReduceHandCardCostsToOne,
                0, UpgradeDelta: 1)],
            ExhaustOnUse: true, Rarity: PrototypeCardRarity.Event,
            RewardEligible: false, Type: PrototypeCardType.Skill)
    ];

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        // Event-only. Its native after-auto-preplay replay hook lives
        // in PrototypeNativeGloryHistoryCourse, with a per-turn Attack
        // history snapshot and a genuine auto-play duplicate.
        new(HistoryCourseId, "History Course"),
        // PollinousCore.TurnsSeen persists across combats and supplies
        // +2 hand draws on each fourth hand draw (not fourth card drawn).
        new(PollinousCoreId, "Pollinous Core"),
        // LostWisp.AfterCardPlayed is triggered only by Power cards.
        // Relic damage is unpowered, hitting all living enemies.
        new(LostWispRelicId, "Lost Wisp",
            Triggers:
            [
                new(PrototypeCombatEventKind.CardPlayed,
                    [new(PrototypeCombatEffectKind.DamageEnemy, 8,
                        Target: PrototypeEffectTarget.AllEnemies)],
                    RequiredSourceCardType: PrototypeCardType.Power)
            ])
    ];

    // The Mysterious Knight inherits Flail Knight's move program.
    // Source MysteriousKnight.AfterAddedToRoom adds Strength 6 and
    // Plating 6; Plating grants opening and enemy-end Block, then
    // decays by one at later enemy-side starts in solo combat.
    public static PrototypePowerDefinition[] Powers { get; } =
    [
        new(PlatingPowerId, "Plating", 0, [],
            EnemyStartingBlockPerStack: 1,
            EnemyBlockAtSideTurnEndPerStack: 1,
            EnemyStacksDecayAtSideTurnStartAfterFirst: 1)
    ];

    public static PrototypeEnemyDefinition[] Enemies { get; } =
    [
        PrototypeNativeGloryBosses.Enemies.Single(enemy =>
            enemy.Id == PrototypeNativeGloryBosses.FlailKnightId) with
        {
            Id = MysteriousKnightId,
            Name = "Mysterious Knight",
            StartingPowers =
            [
                new("proto.power.strength", 6),
                new(PlatingPowerId, 6)
            ]
        }
    ];

    public static PrototypeEncounterDefinition[] Encounters { get; } =
    [
        new(MysteriousKnightEncounterId, PrototypeRoomType.Combat,
            [MysteriousKnightId], MinAct: 2, MaxAct: 2, Weight: 0)
    ];

    public static PrototypeEventDefinition[] Definitions { get; } =
    [
        // IsAllowed=false in native source: only the Lantern Key's
        // ModifyNextEvent hook can install this event in Glory.
        new(WarHistorianRepyId, "War Historian Repy",
        [
            new("unlock_cage", "Free Repy and gain History Course", []),
            new("unlock_chest", "Open the four-reward chest", [])
        ], MinAct: 3, MaxAct: 3, Weight: 0,
           OncePerRun: false),
        // Native event: return the key for 100 gold, or select Keep,
        // then enter a single Mysterious Knight combat. On victory,
        // Lantern Key is an optional special card reward.
        new(LanternKeyEventId, "The Lantern Key",
        [
            new("return_key", "Return the key for 100 gold", []),
            new("keep_key", "Keep the key and face the knight", [])
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        // Field of Man-Sized Holes: two removals then a curse, or
        // enchant one eligible persistent card with Perfect Fit.
        // The source's GoldVar(75) is display-only, not a reward.
        new(FieldOfManSizedHolesId, "Field of Man-Sized Holes",
        [
            new("resist", "Remove up to two cards and gain Normality",
            [new(PrototypeRunEffectKind.AddCardAfterDeckChoices,
                CardId: NormalityId)],
            new PrototypeEventDeckChoiceSpec(
                PrototypePersistentDeckChoiceKind.Remove, 2)),
            new("enter_your_hole", "Enchant a card with Perfect Fit", [],
            new PrototypeEventDeckChoiceSpec(
                PrototypePersistentDeckChoiceKind.Enchant, 1,
                EnchantmentKind: PrototypeCardEnchantmentKind.PerfectFit))
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        new(AmalgamatorId, "Amalgamator",
        [
            new("combine_strikes", "Combine two Strikes",
            [new(PrototypeRunEffectKind.AddCardAfterDeckChoices,
                CardId: UltimateStrikeId)],
            new PrototypeEventDeckChoiceSpec(
                PrototypePersistentDeckChoiceKind.Remove, 2,
                BasicCardsOnly: true, RequiredCardTag: "Strike")),
            new("combine_defends", "Combine two Defends",
            [new(PrototypeRunEffectKind.AddCardAfterDeckChoices,
                CardId: UltimateDefendId)],
            new PrototypeEventDeckChoiceSpec(
                PrototypePersistentDeckChoiceKind.Remove, 2,
                BasicCardsOnly: true, RequiredCardTag: "Defend"))
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        new(InfestedAutomatonId, "Infested Automaton",
        [
            new("study", "Study: gain one random character Power", []),
            new("touch_core",
                "Touch Core: gain one random zero-cost character card", [])
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        // Colossal Flower is a multistage event. At page 0 or 1 the
        // player may extract the current prize, or dig for 5/6 damage.
        // At page 2, extract 135 gold or lose 7 HP for Pollinous Core.
        // Stage legality/transition is handled by StepNativeColossalFlower.
        new(ColossalFlowerId, "Colossal Flower",
        [
            new("extract", "Extract the current gold prize", []),
            new("dig", "Reach deeper at the cost of HP", []),
            new("relic", "Take Pollinous Core and lose 7 HP", [])
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        // Spirit Grafter's optional upgrade is resolved before damage.
        // When nothing is upgradable, native still deals the 10 damage.
        new(SpiritGrafterId, "Spirit Grafter",
        [
            new("let_it_in", "Heal 25 and gain Metamorphosis",
            [
                new(PrototypeRunEffectKind.Heal, 25),
                new(PrototypeRunEffectKind.AddCard, CardId: MetamorphosisId)
            ]),
            new("rejection", "Upgrade one card then lose 10 HP",
                [new(PrototypeRunEffectKind.LoseHpAfterDeckChoices, 10)],
                new PrototypeEventDeckChoiceSpec(
                    PrototypePersistentDeckChoiceKind.Upgrade, 1))
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        // LostWisp.CalculateVars rolls 60 +/- 15 on event entry.
        new(LostWispId, "Lost Wisp",
        [
            new("claim", "Claim Lost Wisp and gain Decay",
            [
                new(PrototypeRunEffectKind.AddCard, CardId: DecayId),
                new(PrototypeRunEffectKind.GainRelic, RelicId: LostWispRelicId)
            ]),
            new("search", "Search: gain 45-75 gold",
                [new(PrototypeRunEffectKind.GainGold, 60)])
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        // ZenWeaver.IsAllowed: gold >= 125. Choice-level affordability
        // still uses the exact individual option prices.
        new(ZenWeaverId, "Zen Weaver",
        [
            new("breathing", "Breathing Techniques: 50 gold for two Enlightenment",
            [
                new(PrototypeRunEffectKind.LoseGold, 50),
                new(PrototypeRunEffectKind.AddCard, CardId: EnlightenmentId),
                new(PrototypeRunEffectKind.AddCard, CardId: EnlightenmentId)
            ]),
            new("emotional", "Emotional Awareness: remove one for 125 gold",
                [new(PrototypeRunEffectKind.LoseGoldAfterDeckChoices, 125)],
                new PrototypeEventDeckChoiceSpec(
                    PrototypePersistentDeckChoiceKind.Remove, 1)),
            new("arachnid", "Arachnid Acupuncture: remove two for 250 gold",
                [new(PrototypeRunEffectKind.LoseGoldAfterDeckChoices, 250)],
                new PrototypeEventDeckChoiceSpec(
                    PrototypePersistentDeckChoiceKind.Remove, 2))
        ], MinAct: 2, MaxAct: 2, Weight: 0)
    ];
}

public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeInfestedAutomaton(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        if (world.Act != 2 || world.Event?.EventId !=
                PrototypeNativeHiveEvents.InfestedAutomatonId
            || world.Event.NativePageIndex != 0)
        {
            throw new InvalidOperationException(
                "Infested Automaton requires its initial Hive event page.");
        }

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (choice != "study" && choice != "touch_core")
        {
            throw new InvalidOperationException(
                $"Unknown Infested Automaton choice '{choice}'.");
        }

        // CardFactory in the native game draws from the unlocked character
        // pool using its noncombat rarity odds. Until the remaining
        // character/profile and native card-generation streams land,
        // restrict this routed event to the implemented, solo-safe Silent
        // reward pool. This is a deliberate approximation of the
        // selection distribution, not of either eligibility predicate.
        var eligible = PrototypeContent.NativeSilentRewardCardPool
            .Where(id =>
            {
                var card = PrototypeContent.Card(id);
                return choice == "study"
                    ? card.Type == PrototypeCardType.Power
                    : card.Cost.Kind == PrototypeCardCostKind.Fixed
                        && card.Cost.AmountAt(0) == 0;
            })
            .ToArray();

        if (eligible.Length == 0)
        {
            return PreserveNativeEventCompletionPage(state, choice);
        }

        var draw = NativeEventContextRngV111.ResolveIndex(
            state,
            world.Event!,
            PrototypeNativeHiveEvents
                .InfestedAutomatonNativeRngEventId,
            "event.hive.card_offer.index_draw",
            $"event:{world.Event!.EventId}:choice:{choice}:pool:"
                + CanonicalJson.Sha256(eligible),
            eligible.Length);
        state = draw.State;
        world = RequireWorld(state);
        var selected = eligible[draw.SelectedIndex];
        var player = AppendCard(
            state.Player, world.NextCardInstanceId, selected, state.Rng);
        return PreserveNativeEventCompletionPage(state with
        {
            Player = player,
            World = world with
            {
                NextCardInstanceId = world.NextCardInstanceId + 1
            }
        }, choice);
    }
}
