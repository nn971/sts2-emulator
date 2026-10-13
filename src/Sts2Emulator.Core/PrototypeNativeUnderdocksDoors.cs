namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeDoorsLight(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Doors of Light and Dark requires an active event.");
        var choiceId = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (eventState.EventId !=
                "proto.native.underdocks.doors_of_light_and_dark"
            || eventState.NativePageIndex != 0
            || choiceId != "light"
            || !CanTakeEventChoice(state.Player,
                PrototypeContent.Event(eventState.EventId).Choices
                    .Single(choice => choice.Id == choiceId), world))
        {
            throw new InvalidOperationException(
                "The Light option is unavailable in this event state.");
        }

        // The source builds one stable-shuffled candidate list, then takes
        // up to two cards. Removing selected IDs prevents a multi-upgrade
        // card from receiving both upgrades in the same event.
        var shuffled = state.Player.Deck
            .Where(card => CanSelectEventDeckCard(
                card, PrototypePersistentDeckChoiceKind.Upgrade,
                null, null, false))
            .Select(card => card.InstanceId)
            .ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = PrototypeRng.NextInt(state.Rng, "event", i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var selected = shuffled.Take(2).ToHashSet();
        var player = state.Player with
        {
            Deck = state.Player.Deck.Select(card => selected.Contains(
                card.InstanceId)
                ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                : card).ToArray()
        };
        return AdvanceEventContinuations(state with
        {
            Player = player,
            World = world with
            {
                Event = eventState with { ChosenChoiceId = "light" }
            }
        });
    }
}
