namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 Hive event inventory and the first additional executable
/// Hive event. Later-act random event selection is intentionally gated.
/// Source: Flaaax/STS2-source@706ef1c9fa2219220065849fd5265328607ece20,
/// Hive.cs and InfestedAutomaton.cs.
/// </summary>
public static class PrototypeNativeHiveEvents
{
    public const string InfestedAutomatonId =
        "proto.native.hive.infested_automaton";

    // The ten native Hive.AllEvents entries, in source order.
    // Only Bugslayer and Infested Automaton have executable options here.
    public static string[] NativeEventNames { get; } =
    [
        "Amalgamator", "Bugslayer", "ColorfulPhilosophers",
        "ColossalFlower", "FieldOfManSizedHoles", "InfestedAutomaton",
        "LostWisp", "SpiritGrafter", "TheLanternKey", "ZenWeaver"
    ];

    public const string LostWispId = "proto.native.hive.lost_wisp";
    public const string ZenWeaverId = "proto.native.hive.zen_weaver";
    public const string DecayId = "proto.native.hive.decay";
    public const string EnlightenmentId = "proto.native.hive.enlightenment";
    public const string LostWispRelicId = "proto.native.hive.lost_wisp_relic";

    public static bool IsNativeEvent(string id) =>
        id is InfestedAutomatonId or LostWispId or ZenWeaverId;

    public static bool IsEligible(PrototypeEventDefinition evt,
        PlayerState player) =>
        evt.Id != ZenWeaverId || player.Gold >= 125;

    public static PrototypeCardDefinition[] Cards { get; } =
    [
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

    public static PrototypeEventDefinition[] Definitions { get; } =
    [
        new(InfestedAutomatonId, "Infested Automaton",
        [
            new("study", "Study: gain one random character Power", []),
            new("touch_core",
                "Touch Core: gain one random zero-cost character card", [])
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
        if (world.Event?.EventId !=
            PrototypeNativeHiveEvents.InfestedAutomatonId)
        {
            throw new InvalidOperationException(
                "Infested Automaton requires its active event state.");
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
        // restrict this forced-event fixture to the implemented, solo-safe
        // Silent reward pool. This is a deliberate approximation of the
        // selection distribution, not of either eligibility predicate.
        var eligible = PrototypeContent.RewardCardPool
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
            throw new NotSupportedException(
                $"Infested Automaton '{choice}' has no implemented eligible character cards.");
        }

        var selected = eligible[
            PrototypeRng.NextInt(state.Rng, "event", eligible.Length)];
        var player = AppendCard(
            state.Player, world.NextCardInstanceId, selected, state.Rng);
        return CompleteRoomToMap(state with
        {
            Player = player,
            World = world with
            {
                NextCardInstanceId = world.NextCardInstanceId + 1
            }
        });
    }
}
