namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped v0.111.0 Trial event. Each choice is a separate
/// decision boundary, including its abandoned-run confirmation.
/// Random court identity and card/relic pools currently use prototype
/// streams/pools rather than the pinned native context generators.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static IReadOnlyList<GameAction> GetNativeTrialActions(
        EventState evt)
    {
        string[] ids = evt.NativePageIndex switch
        {
            0 => ["accept", "reject"],
            1 => ["accept", "double_down"],
            2 => ["merchant_guilty", "merchant_innocent"],
            3 => ["noble_guilty", "noble_innocent"],
            4 => ["nondescript_guilty", "nondescript_innocent"],
            5 => ["confirm_abandon", "cancel_abandon"],
            _ => throw new InvalidOperationException(
                "Trial has no further event pages.")
        };
        return ids.Select(id => GameAction.Create("event_choice",
            new EventChoicePayload(id))).ToArray();
    }

    private static RunState StepNativeTrial(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var evt = world.Event;
        if (world.Act != 3 ||
            evt?.EventId != PrototypeNativeGloryEvents.TrialId)
            throw new InvalidOperationException(
                "Trial requires its isolated Glory event fixture.");
        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (!GetNativeTrialActions(evt).Any(a =>
            a.ReadPayload<EventChoicePayload>().ChoiceId == choice))
            throw new InvalidOperationException(
                $"Trial choice '{choice}' is unavailable on page {evt.NativePageIndex}.");

        // The rejection page permits the player to reconsider. The
        // native Double Down option opens a run-abandon confirmation UI;
        // in the headless engine that confirmation is an explicit page.
        if (evt.NativePageIndex == 0 && choice == "reject")
            return state with
            {
                World = world with
                {
                    Event = evt with { NativePageIndex = 1 }
                }
            };

        if ((evt.NativePageIndex is 0 or 1) && choice == "accept")
            return state with
            {
                World = world with
                {
                    Event = evt with
                    {
                        NativePageIndex =
                            2 + PrototypeRng.NextInt(state.Rng, "event", 3)
                    }
                }
            };

        if (evt.NativePageIndex == 1 && choice == "double_down")
            return state with
            {
                World = world with
                {
                    Event = evt with { NativePageIndex = 5 }
                }
            };

        if (evt.NativePageIndex == 5)
        {
            if (choice == "cancel_abandon")
                return state with
                {
                    World = world with
                    {
                        Event = evt with { NativePageIndex = 1 }
                    }
                };
            return EndRun(state with
            {
                Player = state.Player with { Hp = 0 }
            }, "defeat");
        }

        var player = state.Player;
        var nextId = world.NextCardInstanceId;
        var choices = new List<PrototypePendingEventDeckChoiceState>();
        RewardState? reward = null;
        switch (choice)
        {
            case "merchant_guilty":
            {
                player = AppendCard(player, nextId++,
                    PrototypeNativeGloryEvents.RegretId, state.Rng,
                    allocateCardId: () => nextId++);
                // Native obtains two front-of-bag relics directly;
                // each can enqueue an acquisition-driven deck choice.
                for (var i = 0; i < 2; i++)
                {
                    var drawn = PrototypeNativeRelicGrabBag.Draw(
                        world, player, state.Rng, merchant: false);
                    world = drawn.World;
                    if (drawn.Id is not { } relicId)
                        continue; // Depleted source-shaped partial bag.
                    player = player with
                    {
                        Relics = player.Relics.Append(new RelicInstance(
                            relicId, PrototypeJson.EmptyObject())).ToArray()
                    };
                    player = ApplyRelicRunEvent(player,
                        PrototypeRunEventKind.RelicAcquired,
                        acquiredRelicId: relicId, rng: state.Rng);
                    var deckChoice = CreateRelicEventDeckChoice(
                        choice, PrototypeContent.Relic(relicId));
                    if (deckChoice is not null)
                        choices.Add(deckChoice);
                }
                break;
            }

            case "merchant_innocent":
                player = AppendCard(player, nextId++,
                    PrototypeNativeGloryEvents.ShameId, state.Rng,
                    allocateCardId: () => nextId++);
                choices.Add(new PrototypePendingEventDeckChoiceState(
                    choice, PrototypePersistentDeckChoiceKind.Upgrade, 2,
                    Array.Empty<long>()));
                break;

            case "noble_guilty":
                player = player with
                {
                    Hp = Math.Min(player.MaxHp, player.Hp + 10)
                };
                break;

            case "noble_innocent":
                player = AppendCard(player, nextId++,
                    PrototypeNativeGloryEvents.RegretId, state.Rng,
                    allocateCardId: () => nextId++);
                player = ApplyGoldGain(player, 300, state.Rng, world,
                    allocateCardId: () => nextId++,
                    nativeRunRng: state.NativeRunRng);
                break;

            case "nondescript_guilty":
            {
                player = AppendCard(player, nextId++,
                    PrototypeNativeGloryEvents.DoubtId, state.Rng,
                    allocateCardId: () => nextId++);
                // Native presents two independently selectable
                // three-card rewards from the character's pool.
                var cardPool = AddDingyRugCardsToRewardPool(
                    player, PrototypeContent.NativeSilentRewardCardPool);
                var cards1 = PickDistinct(
                    cardPool, 3, state.Rng, "reward");
                var cards2 = PickDistinct(
                    cardPool, 3, state.Rng, "reward");
                reward = new RewardState(
                    SourceRoom: "Event:" + evt.EventId,
                    CardOptions: [],
                    PotionOption: null,
                    RelicOption: null,
                    CardResolved: true,
                    PotionResolved: true,
                    RelicResolved: true,
                    EndsAct: false,
                    ExtraCardOptions: [cards1, cards2],
                    CardRewardGenerators:
                    [null,
                        new PrototypeCardRewardGenerator(
                            PrototypeCardRewardGeneratorKind.DistinctRewardPool,
                            world.Act),
                        new PrototypeCardRewardGenerator(
                            PrototypeCardRewardGeneratorKind.DistinctRewardPool,
                            world.Act)])
                {
                    CardRewardLateHooksEnabled = true
                };
                break;
            }

            case "nondescript_innocent":
                player = AppendCard(player, nextId++,
                    PrototypeNativeGloryEvents.DoubtId, state.Rng,
                    allocateCardId: () => nextId++);
                choices.Add(new PrototypePendingEventDeckChoiceState(
                    choice, PrototypePersistentDeckChoiceKind.Transform, 2,
                    Array.Empty<long>()));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unrecognized Trial resolution '{choice}'.");
        }

        // All curse additions precede card-selection and reward prompts.
        // ChosenChoiceId prevents replaying the outcome on resume.
        state = state with
        {
            Player = player,
            World = world with
            {
                NextCardInstanceId = nextId,
                Event = evt with
                {
                    ChosenChoiceId = choice,
                    QueuedDeckChoices = choices.ToArray(),
                    PendingReward = reward
                }
            }
        };
        return AdvanceEventContinuations(state);
    }
}
