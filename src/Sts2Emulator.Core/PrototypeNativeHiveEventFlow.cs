using System.Text.Json;

namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetColossalFlowerActions(
        EventState eventState)
    {
        var stage = eventState.NativePageIndex;
        if (stage is < 0 or > 2)
        {
            throw new InvalidOperationException(
                "Colossal Flower stage is outside its three source pages.");
        }

        return stage < 2
            ?
            [
                GameAction.Create("event_choice",
                    new EventChoicePayload("extract")),
                GameAction.Create("event_choice",
                    new EventChoicePayload("dig"))
            ]
            :
            [
                GameAction.Create("event_choice",
                    new EventChoicePayload("extract")),
                GameAction.Create("event_choice",
                    new EventChoicePayload("relic"))
            ];
    }

    private static RunState StepNativeColossalFlower(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Colossal Flower requires an active event state.");
        if (eventState.EventId != PrototypeNativeHiveEvents.ColossalFlowerId)
        {
            throw new InvalidOperationException("Wrong event handler.");
        }

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (!GetColossalFlowerActions(eventState).Any(a =>
                a.ReadPayload<EventChoicePayload>().ChoiceId == choice))
        {
            throw new InvalidOperationException(
                $"Colossal Flower choice '{choice}' is unavailable at stage {eventState.NativePageIndex}.");
        }

        int stage = eventState.NativePageIndex;
        int[] prizes = [35, 75, 135];
        int[] damages = [5, 6, 7];
        if (choice == "extract")
        {
            state = state with
            {
                Player = state.Player with
                {
                    Gold = GoldAfterGain(state.Player, prizes[stage])
                }
            };
            return FinishColossalFlower(state, eventState, choice);
        }

        var hp = Math.Max(0, state.Player.Hp - damages[stage]);
        var player = state.Player with { Hp = hp };
        state = state with { Player = player };
        if (hp == 0)
        {
            // Native CreatureCmd.Damage is unpowered and unblockable.
            return EndRun(state, "defeat");
        }

        if (choice == "dig")
        {
            // Pages 0 and 1 are the only legal dig options. Damage is
            // applied before publishing the next page.
            return state with
            {
                World = world with
                {
                    Event = eventState with { NativePageIndex = stage + 1 }
                }
            };
        }

        // On the third page, another 7 HP buys Pollinous Core instead
        // of the final 135 Gold. Direct event rewards use Obtain and do
        // not draw from (or remove entries from) the ordinary relic bag.
        player = player with
        {
            Relics = player.Relics.Append(new RelicInstance(
                PrototypeNativeHiveEvents.PollinousCoreId,
                PrototypeJson.EmptyObject())).ToArray()
        };
        player = ApplyRelicRunEvent(
            player, PrototypeRunEventKind.RelicAcquired,
            acquiredRelicId: PrototypeNativeHiveEvents.PollinousCoreId,
            rng: state.Stochastic.PrototypeRng);
        return FinishColossalFlower(state with { Player = player },
            eventState, choice);
    }

    private static RunState FinishColossalFlower(
        RunState state, EventState eventState, string choice) =>
        state with
        {
            World = RequireWorld(state) with
            {
                Event = eventState with
                {
                    NativePageIndex =
                        PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                            eventState.EventId),
                    ChosenChoiceId = choice
                }
            }
        };

    /// <summary>
    /// PollinousCore.BeforeHandDraw increments its saved TurnsSeen
    /// counter once per HAND DRAW, including the opening hand draw.
    /// ModifyHandDraw adds 2 cards and resets the counter on draw 4.
    /// The saved property intentionally lives in the persistent relic
    /// instance rather than a combat-only trigger count.
    /// </summary>
    private static (PlayerState Player, int Bonus)
        ApplyPollinousCoreHandDraw(PlayerState player)
    {
        var relics = player.Relics;
        RelicInstance[]? modified = null;
        int bonus = 0;
        for (var i = 0; i < relics.Length; i++)
        {
            var relic = relics[i];
            if (relic.RelicId != PrototypeNativeHiveEvents.PollinousCoreId)
            {
                continue;
            }

            var previous = 0;
            if (relic.PersistentState.ValueKind == JsonValueKind.Object
                && relic.PersistentState.TryGetProperty("TurnsSeen",
                    out var turns))
            {
                previous = turns.GetInt32();
            }

            var next = previous + 1;
            if (next >= 4)
            {
                bonus += 2;
                next = 0;
            }

            modified ??= (RelicInstance[])relics.Clone();
            modified[i] = relic with
            {
                PersistentState = JsonSerializer.SerializeToElement(
                    new { TurnsSeen = next })
            };
        }

        return (modified is null
            ? player
            : player with { Relics = modified }, bonus);
    }
}
