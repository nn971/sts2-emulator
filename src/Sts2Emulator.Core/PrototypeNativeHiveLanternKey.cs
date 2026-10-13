namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 The Lantern Key event. Returning the key grants
/// 100 Gold; keeping it reveals a single Mysterious Knight encounter.
/// Winning publishes an optional SpecialCardReward for the Lantern Key.
/// The later Glory War Historian Repy quest is a separate unresolved
/// progression mechanic. This event is included in the opt-in Hive route;
/// default v111 event selection remains gated.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeLanternKey(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var current = world.Event
            ?? throw new InvalidOperationException(
                "The Lantern Key has no active event.");
        if (world.Act != 2
            || current.EventId != PrototypeNativeHiveEvents.LanternKeyEventId)
        {
            throw new InvalidOperationException(
                "The Lantern Key event handler received another event.");
        }

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (current.NativePageIndex == 0)
        {
            if (choice == "return_key")
            {
                return PreserveNativeEventCompletionPage(state with
                {
                    Player = ApplyGoldGain(
                        state.Player,
                        100,
                        state.Rng,
                        world,
                        nativeRunRng: state.NativeRunRng)
                }, choice);
            }

            if (choice == "keep_key")
            {
                return state with
                {
                    World = world with
                    {
                        Event = current with { NativePageIndex = 1 }
                    }
                };
            }
        }
        else if (current.NativePageIndex == 1 && choice == "fight")
        {
            var encounter = PrototypeContent.Encounter(
                PrototypeNativeHiveEvents.MysteriousKnightEncounterId);
            return StartCombat(state with
            {
                World = world with
                {
                    Event = current with { NativePageIndex = 2 }
                }
            }, PrototypeRoomType.Combat, encounter);
        }

        throw new InvalidOperationException(
            $"Choice '{choice}' is invalid on The Lantern Key page {current.NativePageIndex}.");
    }
}
