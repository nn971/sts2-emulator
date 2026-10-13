namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed Room Full of Cheese event and its event-only Chosen Cheese
/// relic for Overgrowth and Underdocks.
/// </summary>
public static class PrototypeNativeRoomFullOfCheese
{
    public const string EventId = "proto.native.event.room_full_of_cheese";
    public const string ChosenCheeseId = "proto.native.event.chosen_cheese";
    public const int CardOfferCount = 8;
    public const int CardsToChoose = 2;
    public const int SearchHpLoss = 14;

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Room Full of Cheese",
        [
            new("gorge", "Choose two Common cards", []),
            new("search", "Lose 14 HP and gain Chosen Cheese", [])
        ],
        MaxAct: 1);

    public static PrototypeRelicDefinition[] Relics { get; } =
    [
        new(ChosenCheeseId, "Chosen Cheese",
            MaxHpOnCombatVictory: 1)
    ];

    public static bool IsCardReward(RewardState reward) =>
        StringComparer.Ordinal.Equals(
            reward.SourceRoom, "Event:" + EventId);

    public static RewardState CreateCardReward(RngBundle rng)
    {
        var pool = PrototypeContent.NativeSilentRewardCardPool
            .Where(id => PrototypeContent.Card(id).Rarity
                == PrototypeCardRarity.Common)
            .ToArray();
        if (pool.Length == 0)
        {
            throw new InvalidOperationException(
                "Room Full of Cheese has no implemented Common Silent cards.");
        }

        var cards = Enumerable.Range(0, CardOfferCount)
            .Select(_ => pool[PrototypeRng.NextInt(
                rng, "reward", pool.Length)])
            .ToArray();
        return new RewardState(
            SourceRoom: "Event:" + EventId,
            CardOptions: cards,
            PotionOption: null,
            RelicOption: null,
            CardResolved: false,
            PotionResolved: true,
            RelicResolved: true,
            EndsAct: false,
            ExtraCardOptions: [(string[])cards.Clone()]);
    }

    internal static RewardState RemoveChosenCardOffer(
        RewardState reward, int selectedIndex)
    {
        var groups = reward.ExtraCardOptions
            ?? throw new InvalidOperationException(
                "Room Full of Cheese's second card offer is missing.");
        if (groups.Length != 1
            || selectedIndex < 0
            || selectedIndex >= groups[0].Length)
        {
            throw new InvalidOperationException(
                "Room Full of Cheese's selected card offer is invalid.");
        }

        return reward with
        {
            ExtraCardOptions =
            [groups[0].Where((_, index) => index != selectedIndex).ToArray()]
        };
    }
}

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetRoomFullOfCheeseActions(
        EventState eventState)
    {
        if (PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                eventState.EventId)
            && eventState.NativePageIndex
                == PrototypeNativeEventCompletionPages
                    .TerminalChoicePageIndex(eventState.EventId))
        {
            return [GameAction.Empty("continue_event")];
        }

        if (eventState.NativePageIndex != 0
            || eventState.ChosenChoiceId is not null)
        {
            throw new InvalidOperationException(
                "Room Full of Cheese is on an unsupported event page.");
        }

        return
        [
            GameAction.Create("event_choice", new EventChoicePayload("gorge")),
            GameAction.Create("event_choice", new EventChoicePayload("search"))
        ];
    }

    private static RunState StepRoomFullOfCheese(
        RunState state, GameAction action)
    {
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Room Full of Cheese has no active event.");

        if (PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                eventState.EventId)
            && eventState.NativePageIndex
                == PrototypeNativeEventCompletionPages
                    .TerminalChoicePageIndex(eventState.EventId))
        {
            RequireKind(action, "continue_event");
            return CompleteRoomToMap(state);
        }

        RequireKind(action, "event_choice");
        if (eventState.NativePageIndex != 0
            || eventState.ChosenChoiceId is not null)
        {
            throw new InvalidOperationException(
                "Room Full of Cheese is on an unsupported event page.");
        }

        var choiceId = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (choiceId == "gorge")
        {
            var reward = PrototypeNativeRoomFullOfCheese.CreateCardReward(
                state.Rng);
            return AdvanceEventContinuations(state with
            {
                World = world with
                {
                    Event = eventState with
                    {
                        ChosenChoiceId = choiceId,
                        PendingReward = reward
                    },
                    Reward = null
                }
            });
        }

        if (choiceId != "search")
        {
            throw new InvalidOperationException(
                $"Room Full of Cheese choice '{choiceId}' is unavailable.");
        }

        var player = ApplyPlayerHpLossAndDeathPrevention(
            state.Player, PrototypeNativeRoomFullOfCheese.SearchHpLoss);
        if (player.Hp == 0)
        {
            return EndRun(state with { Player = player }, "defeat");
        }

        player = player with
        {
            Relics = player.Relics.Append(new RelicInstance(
                PrototypeNativeRoomFullOfCheese.ChosenCheeseId,
                PrototypeJson.EmptyObject())).ToArray()
        };
        var nextCardInstanceId = world.NextCardInstanceId;
        var nativeRunRng = state.NativeRunRng?.Fork();
        player = ApplyRelicRunEvent(
            player,
            PrototypeRunEventKind.RelicAcquired,
            acquiredRelicId: PrototypeNativeRoomFullOfCheese.ChosenCheeseId,
            rng: state.Rng,
            world: world,
            allocateCardId: () => nextCardInstanceId++,
            nativeRunRng: nativeRunRng);
        state = state with
        {
            Player = player,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
            World = world with
            {
                NextCardInstanceId = nextCardInstanceId,
                Event = eventState with { ChosenChoiceId = choiceId }
            }
        };
        return PreserveNativeEventCompletionPage(state, choiceId);
    }
}
