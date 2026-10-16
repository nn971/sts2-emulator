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
                var index = LegacyRandomChoiceV111.ChooseIndexRange(
                    state.Rng,
                    "event.glory_reflections.downgrade_card.index_draw",
                    "event",
                    $"event:reflections:downgrade:{i}:pool:"
                        + CanonicalJson.Sha256(downgradable),
                    downgradable.Count).SelectedIndex;
                var id = downgradable[index];
                downgradable.RemoveAt(index);
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
                var index = LegacyRandomChoiceV111.ChooseIndexRange(
                    state.Rng,
                    "event.glory_reflections.upgrade_card.index_draw",
                    "event",
                    $"event:reflections:upgrade:{i}:pool:"
                        + CanonicalJson.Sha256(upgradable),
                    upgradable.Count).SelectedIndex;
                var id = upgradable[index];
                upgradable.RemoveAt(index);
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
                rng: state.Rng);
        }

        player = AppendCard(player, nextId++,
            PrototypeNativeGloryEvents.BadLuckId, state.Rng);
        return PreserveNativeEventCompletionPage(state with
        {
            Player = player,
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
            var player = state.Player with
            {
                Relics = state.Player.Relics.Append(
                    new RelicInstance(relicId,
                        PrototypeJson.EmptyObject())).ToArray()
            };
            player = ApplyRelicRunEvent(player,
                PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: relicId, rng: state.Rng);
            player = player with { Hp = player.MaxHp };
            return PreserveNativeEventCompletionPage(
                state with { Player = player }, choice);
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
        var nativeRunRng = state.NativeRunRng?.Fork();
        state = state with
        {
            Player = playerAfterHit,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng
        };
        if (playerAfterHit.Hp == 0)
            return EndRun(state, "defeat");

        var draw = PrototypeNativeRelicGrabBag.Draw(
            world, playerAfterHit, state.Rng, merchant: false,
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
                acquiredRelicId: draw.Id, rng: state.Rng,
                nativeRunRng: nativeRunRng);
        }

        return PreserveNativeEventCompletionPage(state with
        {
            Player = playerAfterHit,
            NativeRunRng = nativeRunRng ?? state.NativeRunRng,
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
