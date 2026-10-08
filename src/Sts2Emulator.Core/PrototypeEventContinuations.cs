namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// Resume event rewards in a stable order: outstanding potions, then
    /// acquisition-driven relic deck choices, then the event's own deck choice.
    /// The event's immediate effects and prices are never replayed here.
    /// Queued deck choices are refreshed against the *current* deck so a prior
    /// removal/upgrade cannot leave invalid instance IDs in a later prompt.
    /// </summary>
    private static RunState AdvanceEventContinuations(RunState state)
    {
        while (true)
        {
            var world = RequireWorld(state);
            var eventState = world.Event
                ?? throw new InvalidOperationException(
                    "Event continuation has no active event.");
            if (eventState.PendingPotionReplacement is not null
                || eventState.PendingDeckChoice is not null)
            {
                return state;
            }

            if (eventState.RemainingPotionIds.Length > 0)
            {
                var nextPotionId = eventState.RemainingPotionIds[0];
                _ = PrototypeContent.Potion(nextPotionId);
                var remaining = eventState.RemainingPotionIds.Skip(1).ToArray();
                eventState = eventState with { QueuedPotionIds = remaining };

                var emptySlot = Array.IndexOf(state.Player.PotionSlots, null);
                var nativeOffer = PrototypeNativeOvergrowthEvents.UsesNativePotionOffer(eventState.EventId);
                if (emptySlot >= 0 && !nativeOffer)
                {
                    var slots = (PotionInstance?[])state.Player.PotionSlots.Clone();
                    slots[emptySlot] = new PotionInstance(
                        nextPotionId,
                        PrototypeJson.EmptyObject());
                    state = state with
                    {
                        Player = state.Player with { PotionSlots = slots },
                        World = world with { Event = eventState }
                    };
                    continue;
                }

                // Native reward UI offers "take" or "skip" even with
                // room in the potion belt. If full, offer all occupied
                // slots as replacement destinations.
                var candidateSlots = emptySlot >= 0
                    ? new[] { emptySlot }
                    : Enumerable.Range(0, state.Player.PotionSlots.Length)
                        .ToArray();
                if (candidateSlots.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Event potion acquisition has no usable potion slots.");
                }

                return state with
                {
                    World = world with
                    {
                        Event = eventState with
                        {
                            PendingPotionReplacement =
                                new PrototypePendingEventPotionReplacementState(
                                    eventState.ChosenChoiceId!,
                                    nextPotionId,
                                    candidateSlots)
                        }
                    }
                };
            }

            if (eventState.RemainingDeckChoices.Length > 0)
            {
                var nextChoice = eventState.RemainingDeckChoices[0];
                eventState = eventState with
                {
                    QueuedDeckChoices =
                        eventState.RemainingDeckChoices.Skip(1).ToArray()
                };
                var refreshed = RefreshQueuedEventDeckChoice(
                    state.Player,
                    nextChoice);
                state = state with
                {
                    World = world with
                    {
                        Event = eventState with
                        {
                            PendingDeckChoice = refreshed
                        }
                    }
                };
                if (refreshed is not null)
                {
                    return state;
                }

                // An earlier choice may have removed/upgraded every candidate.
                // An exhausted request is skipped rather than offering
                // references to cards that no longer qualify.
                continue;
            }

            if (eventState.DeferredCardId is { } deferredCardId)
            {
                var player = AppendCard(
                    state.Player,
                    world.NextCardInstanceId,
                    deferredCardId,
                    state.Rng);
                state = state with
                {
                    Player = player,
                    World = world with
                    {
                        NextCardInstanceId = world.NextCardInstanceId + 1,
                        Event = eventState with { DeferredCardId = null }
                    }
                };
                continue;
            }

            if (eventState.DeferredHpLoss > 0)
            {
                var hp = Math.Max(
                    0, state.Player.Hp - eventState.DeferredHpLoss);
                state = state with
                {
                    Player = state.Player with { Hp = hp },
                    World = world with
                    {
                        Event = eventState with { DeferredHpLoss = 0 }
                    }
                };
                if (hp <= 0)
                {
                    return EndRun(state, "defeat");
                }

                continue;
            }

            if (eventState.PendingReward is { } pendingReward)
            {
                // A selected event choice can temporarily enter the
                // ordinary reward phase without losing the event
                // continuation or counting an extra map room.
                return state with
                {
                    World = world with
                    {
                        Event = eventState with { PendingReward = null },
                        Reward = pendingReward
                    },
                    Phase = RunPhase.Reward
                };
            }

            if (StringComparer.Ordinal.Equals(
                    eventState.EventId,
                    PrototypeNativeOvergrowthEvents.NeowEventId)
                && world.Act == 1
                && world.Map.GenerationProfileId
                    == PrototypeNativeOvergrowthMap.GenerationProfileId
                && world.Map.CurrentNodeId is null)
            {
                // The Ancient opening precedes all selectable map rows.
                // It does not count as a completed map room.
                return state with
                {
                    World = world with { Event = null },
                    Phase = RunPhase.MapChoice
                };
            }

            return CompleteRoomToMap(state);
        }
    }

    private static PrototypePendingEventDeckChoiceState?
        RefreshQueuedEventDeckChoice(
            PlayerState player,
            PrototypePendingEventDeckChoiceState requested)
    {
        var candidates = player.Deck
            .Where(card => CanSelectEventDeckCard(
                card, requested.Kind,
                requested.TransformToCardId,
                requested.EnchantmentKind,
                requested.BasicCardsOnly))
            .Select(card => card.InstanceId)
            .ToArray();
        var selections = Math.Min(
            requested.RemainingSelections,
            candidates.Length);
        return selections == 0
            ? null
            : requested with
            {
                RemainingSelections = selections,
                CandidateCardInstanceIds = candidates
            };
    }
}
