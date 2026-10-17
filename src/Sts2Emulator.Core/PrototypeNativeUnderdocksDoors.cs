namespace Sts2Emulator.Core;

public static class PrototypeNativeUnderdocksDoors
{
    public const string EventId =
        "proto.native.underdocks.doors_of_light_and_dark";
    public const string NativeRngEventId = "DOORS_OF_LIGHT_AND_DARK";
    public const string ShuffleSiteId =
        "event.doors_light.shuffle_swap_index";
}

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
        if (eventState.EventId != PrototypeNativeUnderdocksDoors.EventId
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
        state = NativeEventContextRngV111.ShuffleInPlace(
            state,
            eventState,
            PrototypeNativeUnderdocksDoors.NativeRngEventId,
            shuffled,
            PrototypeNativeUnderdocksDoors.ShuffleSiteId,
            "event:doors_light:eligible_cards:"
                + CanonicalJson.Sha256(shuffled));
        world = RequireWorld(state);
        eventState = world.Event!;

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
