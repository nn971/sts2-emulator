namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped single-player implementation of Ranwid the Elder for Hive
/// and Glory. Relic draw parity remains part of the later RNG audit.
/// </summary>
public static class PrototypeNativeRanwidTheElder
{
    public const string EventId = "proto.native.shared.ranwid_the_elder";
    internal const int GoldCost = 100;

    public static PrototypeEventDefinition Definition { get; } = new(
        EventId,
        "Ranwid the Elder",
        [
            new("potion", "Trade the selected potion", []),
            new("potion_locked", "Trade the selected potion", []),
            new("gold", "Give 100 gold", []),
            new("relic", "Trade the selected relic", []),
            new("relic_locked", "Trade the selected relic", [])
        ],
        MinAct: 2,
        MaxAct: 3,
        Weight: 0);

    public static bool IsEligible(PlayerState player) =>
        player.Gold >= GoldCost
        && player.PotionSlots.Any(potion => potion is not null)
        && player.Relics.Any(PrototypeNativeRelicTrader.IsTradable);

    public static EventState RollOffers(
        EventState eventState, PlayerState player, RngBundle rng)
    {
        var potionSlots = player.PotionSlots
            .Select((potion, slot) => (Potion: potion, Slot: slot))
            .Where(item => item.Potion is not null)
            .ToArray();
        var potion = potionSlots[PrototypeRng.NextInt(
            rng, "event", potionSlots.Length)];

        var relics = player.Relics
            .Select((relic, index) => (Relic: relic, Index: index))
            .Where(item => PrototypeNativeRelicTrader.IsTradable(item.Relic))
            .ToArray();
        var relic = relics[PrototypeRng.NextInt(
            rng, "event", relics.Length)];

        return eventState with
        {
            NativeRanwidPotionSlot = potion.Slot,
            NativeRanwidPotionId = potion.Potion!.PotionId,
            NativeRanwidRelicIndex = relic.Index,
            NativeRanwidRelicId = relic.Relic.RelicId
        };
    }

    internal static bool HasValidOffer(EventState eventState) =>
        eventState.NativeRanwidPotionSlot >= 0
        && eventState.NativeRanwidPotionId is not null
        && eventState.NativeRanwidRelicIndex >= 0
        && eventState.NativeRanwidRelicId is not null;
}

public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetNativeRanwidActions(
        EventState eventState)
    {
        if (!PrototypeNativeRanwidTheElder.HasValidOffer(eventState))
        {
            throw new InvalidOperationException(
                "Ranwid the Elder's offers are missing.");
        }

        return
        [
            GameAction.Create("event_choice", new EventChoicePayload("potion")),
            GameAction.Create("event_choice", new EventChoicePayload("gold")),
            GameAction.Create("event_choice", new EventChoicePayload("relic"))
        ];
    }

    private static RunState StepNativeRanwidTheElder(
        RunState state, GameAction action)
    {
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Ranwid the Elder has no active event.");

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
        if (!PrototypeNativeRanwidTheElder.HasValidOffer(eventState))
        {
            throw new InvalidOperationException(
                "Ranwid the Elder's offers are missing.");
        }

        var choiceId = action.ReadPayload<EventChoicePayload>().ChoiceId;
        var player = state.Player;
        var nextWorld = world;
        var relicCount = choiceId == "relic" ? 2 : 1;
        switch (choiceId)
        {
            case "potion":
            {
                var slot = eventState.NativeRanwidPotionSlot;
                if ((uint)slot >= (uint)player.PotionSlots.Length
                    || player.PotionSlots[slot]?.PotionId
                        != eventState.NativeRanwidPotionId)
                {
                    throw new InvalidOperationException(
                        "Ranwid's selected potion no longer matches its slot.");
                }

                var slots = (PotionInstance?[])player.PotionSlots.Clone();
                slots[slot] = null;
                player = player with { PotionSlots = slots };
                break;
            }
            case "gold":
                if (player.Gold
                    < PrototypeNativeRanwidTheElder.GoldCost)
                {
                    throw new InvalidOperationException(
                        "Ranwid's gold offer is no longer affordable.");
                }
                player = player with
                {
                    Gold = player.Gold
                        - PrototypeNativeRanwidTheElder.GoldCost
                };
                break;
            case "relic":
            {
                var index = eventState.NativeRanwidRelicIndex;
                if ((uint)index >= (uint)player.Relics.Length
                    || player.Relics[index].RelicId
                        != eventState.NativeRanwidRelicId
                    || !PrototypeNativeRelicTrader.IsTradable(
                        player.Relics[index]))
                {
                    throw new InvalidOperationException(
                        "Ranwid's selected relic no longer matches its inventory.");
                }

                player = player with
                {
                    Relics = player.Relics
                        .Where((_, relicIndex) => relicIndex != index)
                        .ToArray()
                };
                break;
            }
            default:
                throw new InvalidOperationException(
                    $"Ranwid the Elder choice '{choiceId}' is unavailable.");
        }

        var nextCardInstanceId = world.NextCardInstanceId;
        var nativeRunRng = state.NativeRunRng?.Fork();
        for (var index = 0; index < relicCount; index++)
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                nextWorld, player, state.Rng, merchant: false);
            nextWorld = draw.World;
            var relicId = draw.Id ?? "proto.relic.circlet";
            _ = PrototypeContent.Relic(relicId);
            player = player with
            {
                Relics = player.Relics.Append(new RelicInstance(
                    relicId, PrototypeJson.EmptyObject())).ToArray()
            };
            player = ApplyRelicRunEvent(
                player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId,
                rng: state.Rng,
                world: nextWorld,
                allocateCardId: () => nextCardInstanceId++,
                nativeRunRng: nativeRunRng);
            nextWorld = PrototypeNativeRelicGrabBag.PruneOwned(
                nextWorld, player);
        }

        var nextEvent = eventState with
        {
            ChosenChoiceId = choiceId,
            NativeRanwidPotionSlot = -1,
            NativeRanwidPotionId = null,
            NativeRanwidRelicIndex = -1,
            NativeRanwidRelicId = null
        };
        state = state with
        {
            Player = player,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
            World = nextWorld with
            {
                NextCardInstanceId = nextCardInstanceId,
                Event = nextEvent
            }
        };
        return PreserveNativeEventCompletionPage(state, choiceId);
    }
}
