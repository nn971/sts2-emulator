namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 Abyssal Baths event: first immersion and every linger
/// increase current/max HP by two, then deal 3, 4, 5, ... unblocked HP
/// damage. NativePageIndex counts immersions (the native descriptive
/// LINGER page text caps at nine, but its damage continues increasing).
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private const string AbyssalBathsEventId =
        "proto.native.underdocks.abyssal_baths";

    private static IReadOnlyList<GameAction> GetAbyssalBathsActions(
        EventState evt) =>
        (evt.NativePageIndex == 0
            ? new[] { "immerse", "abstain" }
            : new[] { "linger", "exit" })
        .Select(choice => GameAction.Create(
            "event_choice", new EventChoicePayload(choice)))
        .ToArray();

    private static RunState StepAbyssalBaths(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var evt = world.Event ??
            throw new InvalidOperationException("Missing Abyssal Baths event.");
        if (evt.EventId != AbyssalBathsEventId)
        {
            throw new InvalidOperationException(
                "Abyssal Baths received another event's state.");
        }

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        var isInitial = evt.NativePageIndex == 0;
        if ((isInitial && choice == "abstain")
            || (!isInitial && choice == "exit"))
        {
            var healed = choice == "abstain"
                ? Math.Min(state.Player.MaxHp, state.Player.Hp + 10)
                : state.Player.Hp;
            return AdvanceEventContinuations(state with
            {
                Player = state.Player with { Hp = healed },
                World = world with
                {
                    Event = evt with { ChosenChoiceId = choice }
                }
            });
        }

        if (choice != (isInitial ? "immerse" : "linger"))
        {
            throw new InvalidOperationException(
                $"Unavailable Abyssal Baths choice '{choice}'.");
        }

        // CreatureCmd.GainMaxHp heals by the increase before Damage.
        // Damage grows by one on each immersion; a fatal choice remains
        // legal, as in the source event's death-warning page.
        var damage = checked(3 + evt.NativePageIndex);
        var player = state.Player with
        {
            MaxHp = checked(state.Player.MaxHp + 2),
            Hp = Math.Max(0, state.Player.Hp + 2 - damage)
        };
        var next = state with
        {
            Player = player,
            World = world with
            {
                Event = evt with
                {
                    NativePageIndex = checked(evt.NativePageIndex + 1)
                }
            }
        };
        return player.Hp <= 0 ? EndRun(next, "defeat") : next;
    }
}
