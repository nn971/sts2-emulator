namespace Sts2Emulator.Core;

/// <summary>
/// Source-backed Glory event choices outside the still-disabled
/// regional random event bag. Pins: Reflections.cs, BadLuck.cs,
/// RoundTeaParty.cs and RoyalPoison.cs, v0.111.0.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static RunState StepNativeGloryReflections(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        if (world.Act != 3 ||
            world.Event?.EventId != PrototypeNativeGloryEvents.ReflectionsId)
            throw new InvalidOperationException(
                "Reflections requires its Glory event fixture.");
        var evt = world.Event!;

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        var player = state.Player;
        if (choice == "touch_mirror")
        {
            // Native: sample up to two *distinct* currently upgraded
            // cards, downgrade once; then RECOMPUTE eligibility and
            // sample up to four distinct upgradable cards.
            var downgradable = player.Deck
                .Where(card => card.UpgradeLevel > 0)
                .Select(card => card.InstanceId).ToList();
            for (var i = 0; i < 2 && downgradable.Count > 0; i++)
            {
                var draw = NativeEventContextRngV111.ResolveIndex(
                    state, evt,
                    PrototypeNativeGloryEvents
                        .ReflectionsNativeRngEventId,
                    PrototypeNativeGloryEvents
                        .ReflectionsDowngradeSiteId,
                    $"event:reflections:downgrade:{i}:pool:"
                        + CanonicalJson.Sha256(downgradable),
                    downgradable.Count);
                state = draw.State;
                world = RequireWorld(state);
                evt = world.Event!;
                var id = downgradable[draw.SelectedIndex];
                downgradable.RemoveAt(draw.SelectedIndex);
                player = player with
                {
                    Deck = player.Deck.Select(card =>
                        card.InstanceId == id
                            ? card with { UpgradeLevel = card.UpgradeLevel - 1 }
                            : card).ToArray()
                };
            }

            var upgradable = player.Deck.Where(card =>
                    CanSelectEventDeckCard(card,
                        PrototypePersistentDeckChoiceKind.Upgrade,
                        null, null, false))
                .Select(card => card.InstanceId).ToList();
            for (var i = 0; i < 4 && upgradable.Count > 0; i++)
            {
                var draw = NativeEventContextRngV111.ResolveIndex(
                    state, evt,
                    PrototypeNativeGloryEvents
                        .ReflectionsNativeRngEventId,
                    PrototypeNativeGloryEvents.ReflectionsUpgradeSiteId,
                    $"event:reflections:upgrade:{i}:pool:"
                        + CanonicalJson.Sha256(upgradable),
                    upgradable.Count);
                state = draw.State;
                world = RequireWorld(state);
                evt = world.Event!;
                var id = upgradable[draw.SelectedIndex];
                upgradable.RemoveAt(draw.SelectedIndex);
                player = player with
                {
                    Deck = player.Deck.Select(card =>
                        card.InstanceId == id
                            ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                            : card).ToArray()
                };
            }

            return PreserveNativeEventCompletionPage(
                state with { Player = player }, choice);
        }

        if (choice != "shatter")
            throw new InvalidOperationException(
                $"Unknown Reflections choice '{choice}'.");

        // Native Shatter uses original deck length, so never clone a
        // newly created clone. Preserve upgrade, card state and enchantment
        // while assigning new persistent identities.
        var originals = player.Deck.ToArray();
        var nextId = world.NextCardInstanceId;
        var prototypeRng = state.Stochastic.PrototypeRng;
        foreach (var original in originals)
        {
            var copy = original with
            {
                InstanceId = nextId++,
                PersistentState = original.PersistentState.Clone()
            };
            player = player with { Deck = player.Deck.Append(copy).ToArray() };
            player = ApplyRelicRunEvent(player,
                PrototypeRunEventKind.CardAdded, addedCard: copy,
                rng: prototypeRng);
        }

        player = AppendCard(player, nextId++,
            PrototypeNativeGloryEvents.BadLuckId, prototypeRng);
        var stochastic = RunStochasticStateV111.WithPrototypeRng(
            state.Stochastic, prototypeRng);
        return PreserveNativeEventCompletionPage(state with
        {
            Player = player,
            Stochastic = stochastic,
            World = world with { NextCardInstanceId = nextId }
        }, choice);
    }

    private static IReadOnlyList<GameAction>
        GetNativeRoundTeaPartyActions(EventState evt)
    {
        if (evt.NativePageIndex == 1)
            return [GameAction.Create("event_choice",
                new EventChoicePayload("continue_fight"))];
        if (evt.NativePageIndex != 0)
            throw new InvalidOperationException(
                "Round Tea Party has no further event pages.");
        return PrototypeContent.Event(evt.EventId).Choices
            .Select(choice => GameAction.Create("event_choice",
                new EventChoicePayload(choice.Id))).ToArray();
    }

    private static RunState StepNativeRoundTeaParty(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var evt = world.Event;
        if (world.Act != 3 || evt?.EventId !=
                PrototypeNativeGloryEvents.RoundTeaPartyId)
            throw new InvalidOperationException(
                "Round Tea Party requires its Glory event fixture.");

        if (evt.NativePageIndex == 0 && state.Player.Hp < 12)
            throw new InvalidOperationException(
                "Round Tea Party requires at least 12 current HP.");

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (evt.NativePageIndex == 0 && choice == "pick_fight")
            return state with
            {
                World = world with
                {
                    Event = evt with { NativePageIndex = 1 }
                }
            };

        if (evt.NativePageIndex == 0 && choice == "enjoy_tea")
        {
            const string relicId = PrototypeNativeGloryEvents.RoyalPoisonId;
            var teaPrototypeRng = state.Stochastic.PrototypeRng;
            var player = state.Player with
            {
                Relics = state.Player.Relics.Append(
                    new RelicInstance(relicId,
                        PrototypeJson.EmptyObject())).ToArray()
            };
            player = ApplyRelicRunEvent(player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId, rng: teaPrototypeRng);
            player = player with { Hp = player.MaxHp };
            var teaStochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, teaPrototypeRng);
            return PreserveNativeEventCompletionPage(
                state with
                {
                    Player = player,
                    Stochastic = teaStochastic
                }, choice);
        }

        if (evt.NativePageIndex != 1 || choice != "continue_fight")
            throw new InvalidOperationException(
                $"Invalid Round Tea Party choice '{choice}'.");

        // The first click only opens a follow-up page. The second
        // causes eleven unblockable, unpowered damage, THEN obtains
        // one relic from the front of the persistent reward grab bag.
        var playerAfterHit = state.Player with
        {
            Hp = Math.Max(0, state.Player.Hp - 11)
        };
        var nativeRunRng = UsesNativeRegionalSystems(world)
            ? state.Stochastic.NativeRunRng
            : null;
        var prototypeRng = state.Stochastic.PrototypeRng;
        if (playerAfterHit.Hp == 0)
            return EndRun(state with { Player = playerAfterHit }, "defeat");

        var draw = PrototypeNativeRelicGrabBag.Draw(
            world, playerAfterHit, prototypeRng, merchant: false,
            nativeRunRng: nativeRunRng);
        world = draw.World;
        if (draw.Id is not null)
        {
            playerAfterHit = playerAfterHit with
            {
                Relics = playerAfterHit.Relics.Append(
                    new RelicInstance(draw.Id,
                        PrototypeJson.EmptyObject())).ToArray()
            };
            playerAfterHit = ApplyRelicRunEvent(playerAfterHit,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: draw.Id, rng: prototypeRng,
                nativeRunRng: nativeRunRng);
        }

        var stochastic = RunStochasticStateV111.WithPrototypeRng(
            state.Stochastic, prototypeRng);
        if (nativeRunRng is not null)
            stochastic = RunStochasticStateV111.WithNativeRunRng(
                stochastic, nativeRunRng);
        return PreserveNativeEventCompletionPage(state with
        {
            Player = playerAfterHit,
            Stochastic = stochastic,
            World = world
        }, choice);
    }

    private static RunState ApplyOpeningRoyalPoison(RunState state)
    {
        var count = state.Player.Relics.Count(relic =>
            relic.RelicId == PrototypeNativeGloryEvents.RoyalPoisonId);
        if (count == 0)
            return state;

        // Initial combat creation starts directly in player turn one
        // (there is no PlayerTurnStarted pipeline dispatch on turn one).
        // Implement only RoyalPoison's pinned owner-scoped hook here;
        // the native callback is triggered once per relic instance.
        var player = state.Player with
        {
            Hp = Math.Max(0, state.Player.Hp - checked(4 * count))
        };
        var damaged = state with { Player = player };
        if (player.Hp <= 0)
        {
            damaged = TryUseAutomaticDeathPrevention(damaged);
            return damaged.Player.Hp <= 0
                ? EndRun(damaged, "defeat") : damaged;
        }
        return damaged;
    }
}
