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

    public static PrototypeEventDefinition[] Definitions { get; } =
    [
        new(InfestedAutomatonId, "Infested Automaton",
        [
            new("study", "Study: gain one random character Power", []),
            new("touch_core",
                "Touch Core: gain one random zero-cost character card", [])
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
