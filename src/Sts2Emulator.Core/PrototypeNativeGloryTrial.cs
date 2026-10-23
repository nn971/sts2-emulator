namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped v0.111.0 Trial event. Each choice is a separate
/// decision boundary, including its abandoned-run confirmation.
/// Random court identity uses the event-local context on native v111 runs;
/// card/relic pools still use prototype streams/pools.
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
        var nativeRunRng = UsesNativeRegionalSystems(world)
            ? state.Stochastic.NativeRunRng
            : null;
        var evt = world.Event;
        if (world.Act != 3 ||
            evt?.EventId != PrototypeNativeGloryEvents.TrialId)
            throw new InvalidOperationException(
                "Trial requires its isolated Glory event fixture.");
        if (evt.NativePageIndex is 0 or 1)
        {
            evt = evt with
            {
                Stochastic = EventStochasticStateV111.WithNativeRng(
                    evt.Stochastic,
                    evt.Stochastic.NativeRng
                        ?? NativeEventContextRngV111.CreateAtEntry(
                            state,
                            PrototypeNativeGloryEvents.TrialNativeRngEventId))
            };
            world = world with { Event = evt };
            state = state with { World = world };
        }
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
        {
            var draw = NativeEventContextRngV111.ResolveBoundedInteger(
                state,
                evt,
                PrototypeNativeGloryEvents.TrialNativeRngEventId,
                StochasticSiteRegistryV111.NativeGloryTrialDamageSiteId,
                "event:glory_trial:trial_damage",
                3);
            state = draw.State;
            world = RequireWorld(state);
            evt = world.Event!;
            return state with
            {
                World = world with
                {
                    Event = evt with { NativePageIndex = 2 + draw.SelectedValue }
                }
            };
        }

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
        var prototypeRng = state.Stochastic.PrototypeRng;
        var nextId = world.NextCardInstanceId;
        var choices = new List<PrototypePendingEventDeckChoiceState>();
        RewardState? reward = null;
        switch (choice)
        {
            case "merchant_guilty":
            {
                player = AppendCard(player, nextId++,
                    PrototypeNativeGloryEvents.RegretId, prototypeRng);
                // Native obtains two front-of-bag relics directly;
                // each can enqueue an acquisition-driven deck choice.
                for (var i = 0; i < 2; i++)
                {
                    var drawn = PrototypeNativeRelicGrabBag.Draw(
                        world, player, prototypeRng, merchant: false,
                        nativeRunRng: nativeRunRng);
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
                        acquiredRelicId: relicId, rng: prototypeRng,
                        nativeRunRng: nativeRunRng);
                    var deckChoice = CreateRelicEventDeckChoice(
                        choice, PrototypeContent.Relic(relicId));
                    if (deckChoice is not null)
                        choices.Add(deckChoice);
                }
                break;
            }

            case "merchant_innocent":
                player = AppendCard(player, nextId++,
                    PrototypeNativeGloryEvents.ShameId, prototypeRng);
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
                    PrototypeNativeGloryEvents.RegretId, prototypeRng);
                player = player with
                {
                    Gold = GoldAfterGain(player, 300)
                };
                break;

            case "nondescript_guilty":
            {
                player = AppendCard(player, nextId++,
                    PrototypeNativeGloryEvents.DoubtId, prototypeRng);
                // Native presents two independently selectable
                // three-card rewards from the character's pool.
                var cards1 = PickDistinct(
                    PrototypeContent.NativeSilentRewardCardPool, 3,
                    prototypeRng, "reward");
                var cards2 = PickDistinct(
                    PrototypeContent.NativeSilentRewardCardPool, 3,
                    prototypeRng, "reward");
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
                            world.Act)]);
                break;
            }

            case "nondescript_innocent":
                player = AppendCard(player, nextId++,
                    PrototypeNativeGloryEvents.DoubtId, prototypeRng);
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
        var stochastic = RunStochasticStateV111.WithPrototypeRng(
            state.Stochastic, prototypeRng);
        if (nativeRunRng is not null)
            stochastic = RunStochasticStateV111.WithNativeRunRng(
                stochastic, nativeRunRng);
        state = state with
        {
            Player = player,
            Stochastic = stochastic,
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
