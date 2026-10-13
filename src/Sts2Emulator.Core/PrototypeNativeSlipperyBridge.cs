namespace Sts2Emulator.Core;

/// <summary>
/// Headless state and choices for the v0.111.0 Slippery Bridge event.
/// </summary>
public static class PrototypeNativeSlipperyBridge
{
    public const string EventId = "proto.native.event.slippery_bridge";
    private const int InitialHpLoss = 3;
    private const int HoldChoiceCount = 7;

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Slippery Bridge",
        CreateChoices(),
        MinAct: 1,
        MaxAct: 4);

    public static bool IsEligible(
        PlayerState player,
        RunWorldState world) =>
        world.CompletedRooms.Length + 1 > 6
        && player.Deck.Any(card => IsRemovable(card));

    public static EventState Initialize(
        EventState state,
        PlayerState player,
        RngBundle rng)
    {
        var cardInstanceId = RollInitialCard(player, rng);
        return state with
        {
            NativeSlipperyBridgeHoldCount = 0,
            NativeSlipperyBridgeCardInstanceId = cardInstanceId,
            NativeSlipperyBridgeSkippedCardInstanceIds = []
        };
    }

    internal static string HoldChoiceId(int holdCount) =>
        holdCount >= HoldChoiceCount
            ? "hold_on_loop"
            : $"hold_on_{holdCount}";

    internal static long? RollNextCard(
        PlayerState player,
        RngBundle rng,
        long previousCardInstanceId,
        long[] skipped)
    {
        var previous = player.Deck.FirstOrDefault(card =>
            card.InstanceId == previousCardInstanceId);
        if (previous is null)
        {
            throw new InvalidOperationException(
                "Slippery Bridge's current card is missing from the deck.");
        }

        var candidates = player.Deck
            .Where(IsRemovable)
            .Where(card => !StringComparer.Ordinal.Equals(
                card.CardId, previous.CardId))
            .Where(card => !skipped.Contains(card.InstanceId))
            .ToArray();
        if (candidates.Length == 0)
        {
            // Native GetNewRandomCard falls back to every removable card
            // when excluding the previous type and skipped cards empties
            // its candidate list.
            candidates = player.Deck.Where(IsRemovable).ToArray();
        }

        return Pick(candidates, rng);
    }

    internal static bool IsValidHoldChoice(EventState state, string choiceId) =>
        StringComparer.Ordinal.Equals(
            choiceId, HoldChoiceId(state.NativeSlipperyBridgeHoldCount));

    private static PrototypeEventChoiceDefinition[] CreateChoices()
    {
        var choices = new List<PrototypeEventChoiceDefinition>
        {
            new("overcome", "Remove the highlighted card", [])
        };
        for (var hold = 0; hold < HoldChoiceCount; hold++)
        {
            var hpLoss = InitialHpLoss + hold;
            choices.Add(new PrototypeEventChoiceDefinition(
                $"hold_on_{hold}",
                $"Lose {hpLoss} HP and choose another card", []));
        }

        choices.Add(new PrototypeEventChoiceDefinition(
            "hold_on_loop", "Keep holding on", []));
        return choices.ToArray();
    }

    private static long? RollInitialCard(
        PlayerState player,
        RngBundle rng)
    {
        var candidates = player.Deck
            .Where(IsRemovable)
            .Where(card => PrototypeContent.Card(card.CardId).Rarity
                != PrototypeCardRarity.Basic);
        var eligible = candidates.ToArray();
        if (eligible.Length == 0)
        {
            eligible = player.Deck.Where(IsRemovable).ToArray();
        }

        return Pick(eligible, rng);
    }

    private static long? Pick(CardInstance[] cards, RngBundle rng) =>
        cards.Length == 0
            ? null
            : cards[PrototypeRng.NextInt(rng, "event", cards.Length)]
                .InstanceId;

    private static bool IsRemovable(CardInstance card) =>
        PrototypeGameEngine.CanSelectEventDeckCard(
            card, PrototypePersistentDeckChoiceKind.Remove,
            transformToCardId: null, enchantmentKind: null,
            basicCardsOnly: false);
}

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetSlipperyBridgeActions(
        EventState eventState)
    {
        if (eventState.NativeSlipperyBridgeCardInstanceId is null)
        {
            throw new InvalidOperationException(
                "Slippery Bridge has no highlighted deck card.");
        }

        return
        [
            GameAction.Create(
                "event_choice",
                new EventChoicePayload("overcome")),
            GameAction.Create(
                "event_choice",
                new EventChoicePayload(
                    PrototypeNativeSlipperyBridge.HoldChoiceId(
                        eventState.NativeSlipperyBridgeHoldCount)))
        ];
    }

    private static RunState StepSlipperyBridge(
        RunState state,
        GameAction action)
    {
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Slippery Bridge has no active event state.");

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
        var cardInstanceId = eventState
            .NativeSlipperyBridgeCardInstanceId
            ?? throw new InvalidOperationException(
                "Slippery Bridge has no highlighted deck card.");
        var choiceId = action.ReadPayload<EventChoicePayload>().ChoiceId;

        if (choiceId == "overcome")
        {
            var card = state.Player.Deck.FirstOrDefault(item =>
                item.InstanceId == cardInstanceId)
                ?? throw new InvalidOperationException(
                    "Slippery Bridge's highlighted card is missing.");
            if (!PrototypeGameEngine.CanSelectEventDeckCard(
                    card, PrototypePersistentDeckChoiceKind.Remove,
                    transformToCardId: null, enchantmentKind: null,
                    basicCardsOnly: false))
            {
                throw new InvalidOperationException(
                    "Slippery Bridge's highlighted card cannot be removed.");
            }

            var player = state.Player with
            {
                Deck = state.Player.Deck
                    .Where(item => item.InstanceId != cardInstanceId)
                    .ToArray()
            };
            return PreserveNativeEventCompletionPage(state with
            {
                Player = player,
                World = world with
                {
                    Event = eventState with
                    {
                        ChosenChoiceId = "overcome",
                        NativeSlipperyBridgeCardInstanceId = null,
                        NativeSlipperyBridgeSkippedCardInstanceIds = []
                    }
                }
            }, "overcome");
        }

        if (!PrototypeNativeSlipperyBridge.IsValidHoldChoice(
                eventState, choiceId))
        {
            throw new InvalidOperationException(
                "Slippery Bridge choice does not match the current page.");
        }

        var hpLoss = ReducePlayerHpLoss(state.Player,
            3 + eventState.NativeSlipperyBridgeHoldCount);
        var hp = Math.Max(0, state.Player.Hp - hpLoss);
        var skipped = (eventState
                .NativeSlipperyBridgeSkippedCardInstanceIds
            ?? Array.Empty<long>())
            .Append(cardInstanceId)
            .Distinct()
            .ToArray();
        var nextCardInstanceId =
            PrototypeNativeSlipperyBridge.RollNextCard(
                state.Player, state.Rng, cardInstanceId, skipped)
            ?? throw new InvalidOperationException(
                "Slippery Bridge has no removable card to highlight.");
        var nextEvent = eventState with
        {
            ChosenChoiceId = choiceId,
            NativeSlipperyBridgeHoldCount =
                eventState.NativeSlipperyBridgeHoldCount + 1,
            NativeSlipperyBridgeCardInstanceId = nextCardInstanceId,
            NativeSlipperyBridgeSkippedCardInstanceIds = skipped
        };
        var next = state with
        {
            Player = state.Player with { Hp = hp },
            World = world with { Event = nextEvent }
        };
        return hp == 0 ? EndRun(next, "defeat") : next;
    }
}
