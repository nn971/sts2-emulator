namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static RunState PreserveNativeEventCompletionPage(
        RunState state, string choiceId)
    {
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "A native event result page requires an active event.");
        if (!PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                eventState.EventId)
            || !PrototypeNativeEventCompletionPages.HasTerminalChoice(
                eventState.EventId, choiceId))
        {
            throw new InvalidOperationException(
                $"Event '{eventState.EventId}' has no finished page for '{choiceId}'.");
        }

        return state with
        {
            World = world with
            {
                Event = eventState with
                {
                    NativePageIndex =
                        PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                            eventState.EventId),
                    ChosenChoiceId = choiceId
                }
            }
        };
    }

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
                var nativeOffer =
                    PrototypeNativeOvergrowthEvents.UsesNativePotionOffer(
                        eventState.EventId)
                    || PrototypeNativeUnderdocksEvents.UsesNativePotionOffer(
                        eventState.EventId);
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

            if (eventState.DeferredGoldLoss > 0)
            {
                state = state with
                {
                    Player = state.Player with
                    {
                        Gold = state.Player.Gold - eventState.DeferredGoldLoss
                    },
                    World = world with
                    {
                        Event = eventState with { DeferredGoldLoss = 0 }
                    }
                };
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

            if (PrototypeNativeEventCompletionPages.HasTerminalChoicePage(
                    eventState.EventId)
                && eventState.NativePageIndex == 0
                && PrototypeNativeEventCompletionPages.HasTerminalChoice(
                    eventState.EventId, eventState.ChosenChoiceId))
            {
                // These events call SetEventFinished after their effects
                // resolve. Keep the native completion page resumable.
                return state with
                {
                    World = world with
                    {
                        Event = eventState with { NativePageIndex = 1 }
                    }
                };
            }

            if (eventState.EventId == PrototypeNativeHiveEvents.WarHistorianRepyId)
                return ContinueNativeWarHistorianRepy(state);

            if (NativeAncientRewardsV111.IsOpeningEvent(world))
                return state with
                {
                    World = world with { Event = null },
                    Phase = RunPhase.MapChoice
                };

            if (NativeTanxEventV111.IsOpeningEvent(world))
                return state with
                {
                    World = world with { Event = null },
                    Phase = RunPhase.MapChoice
                };

            if (NativeVakuuEventV111.IsOpeningEvent(world))
                return state with
                {
                    World = world with { Event = null },
                    Phase = RunPhase.MapChoice
                };

            if (NativeNonupeipeEventV111.IsOpeningEvent(world))
                return state with
                {
                    World = world with { Event = null },
                    Phase = RunPhase.MapChoice
                };

            if (StringComparer.Ordinal.Equals(
                    eventState.EventId,
                    PrototypeNativeOvergrowthEvents.NeowEventId)
                && world.Act == 1
                && world.Map.GenerationProfileId
                    is PrototypeNativeOvergrowthMap.GenerationProfileId
                    or PrototypeNativeUnderdocks.GenerationProfileId
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

            if (eventState.EventId == PrototypeNativeEndlessConveyor.EventId
                && eventState.ChosenChoiceId == "grab")
            {
                // All pending dish effects have settled. The source rolls
                // the next dish here, after Jelly Liver transformation or
                // Suspicious Condiment's optional potion reward completes.
                if (eventState.NativeDishRollPending)
                {
                    var next = PrototypeNativeEndlessConveyor.RollNextDish(
                        eventState with { NativeDishRollPending = false },
                        state.Player, state.Rng);
                    return state with
                    {
                        World = world with { Event = next }
                    };
                }
                return state;
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
                requested.BasicCardsOnly)
                && (requested.RequiredCardTag is null
                    || PrototypeContent.Card(card.CardId).Tags?.Contains(
                        requested.RequiredCardTag, StringComparer.Ordinal) == true)
                && (!requested.RequiresUpgradable
                    || card.UpgradeLevel < PrototypeContent.Card(card.CardId).MaxUpgradeLevel))
            .OrderBy(card => requested.RequiresUpgradable ? card.CardId : string.Empty, StringComparer.Ordinal)
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
                CandidateCardInstanceIds = candidates,
                MinimumSelectionsRemaining = Math.Min(
                    requested.MinimumSelectionsRemaining
                        ?? requested.RemainingSelections,
                    selections)
            };
    }
}
