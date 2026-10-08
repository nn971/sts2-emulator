namespace Sts2Emulator.Core;

/// <summary>
/// The v0.111.0 Tablet of Truth is a five-page event. Decipher costs
/// 3, 6, 12, 24 and finally (current max HP minus 1), upgrading one
/// eligible card after each of the first four pages and every eligible
/// deck card on the fifth. Only pages 1-4 allow Give Up.
/// </summary>
public static class PrototypeNativeTabletOfTruth
{
    public const string EventId = "proto.native.event.tablet_of_truth";
}

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetNativeTabletFollowupActions(
        EventState eventState)
    {
        if (eventState.NativePageIndex is < 1 or > 4)
        {
            throw new InvalidOperationException(
                "Tablet of Truth has no pending followup page.");
        }

        return
        [
            GameAction.Create("event_choice",
                new EventChoicePayload("decipher")),
            GameAction.Create("event_choice",
                new EventChoicePayload("give_up"))
        ];
    }

    private static RunState StepNativeTabletOfTruth(
        RunState state,
        GameAction action)
    {
        RequireKind(action, "event_choice");
        var payload = action.ReadPayload<EventChoicePayload>();
        var world = RequireWorld(state);
        var current = world.Event
            ?? throw new InvalidOperationException(
                "Tablet of Truth has no active event state.");
        if (current.EventId != PrototypeNativeTabletOfTruth.EventId
            || current.NativePageIndex is < 0 or > 4)
        {
            throw new InvalidOperationException(
                "Invalid Tablet of Truth page state.");
        }

        if (payload.ChoiceId == "smash"
            && current.NativePageIndex == 0)
        {
            var player = state.Player;
            return CompleteRoomToMap(state with
            {
                Player = player with
                {
                    Hp = Math.Min(player.MaxHp, player.Hp + 20)
                }
            });
        }

        if (payload.ChoiceId == "give_up"
            && current.NativePageIndex > 0)
        {
            return CompleteRoomToMap(state);
        }

        if (payload.ChoiceId != "decipher")
        {
            throw new InvalidOperationException(
                $"Choice '{payload.ChoiceId}' is unavailable on Tablet page {current.NativePageIndex}.");
        }

        var page = current.NativePageIndex;
        var cost = page switch
        {
            0 => 3,
            1 => 6,
            2 => 12,
            3 => 24,
            4 => Math.Max(0, state.Player.MaxHp - 1),
            _ => throw new InvalidOperationException(
                "Invalid Tablet of Truth decipher progress.")
        };

        if (cost >= state.Player.MaxHp)
        {
            // Native flags the lethal option and kills the player
            // if selected. It does not silently clamp a lethal
            // maximum-HP reduction to one HP.
            return EndRun(state, "defeat");
        }

        var newMaxHp = state.Player.MaxHp - cost;
        var playerAfterCost = state.Player with
        {
            MaxHp = newMaxHp,
            Hp = Math.Min(state.Player.Hp, newMaxHp)
        };

        var eligible = playerAfterCost.Deck
            .Where(card =>
            {
                var definition = PrototypeContent.Card(card.CardId);
                return card.UpgradeLevel == 0
                    && !definition.Unplayable
                    && definition.Rarity is not
                        PrototypeCardRarity.Curse
                        and not PrototypeCardRarity.Status
                        and not PrototypeCardRarity.Quest;
            })
            .Select(card => card.InstanceId)
            .ToArray();

        if (page == 4)
        {
            var all = eligible.ToHashSet();
            playerAfterCost = playerAfterCost with
            {
                Deck = playerAfterCost.Deck.Select(card =>
                    all.Contains(card.InstanceId)
                        ? card with { UpgradeLevel = 1 }
                        : card).ToArray()
            };
            return CompleteRoomToMap(state with
            {
                Player = playerAfterCost
            });
        }

        if (eligible.Length > 0)
        {
            var id = eligible[
                PrototypeRng.NextInt(state.Rng, "event", eligible.Length)];
            playerAfterCost = playerAfterCost with
            {
                Deck = playerAfterCost.Deck.Select(card =>
                    card.InstanceId == id
                        ? card with { UpgradeLevel = 1 }
                        : card).ToArray()
            };
        }

        return state with
        {
            Player = playerAfterCost,
            World = world with
            {
                Event = current with
                {
                    NativePageIndex = page + 1
                }
            }
        };
    }
}
