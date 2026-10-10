namespace Sts2Emulator.Core;

/// <summary>
/// Narrow source-backed v0.111.0 event subset. Deliberately incomplete:
/// never sample an absent Hive/Glory event as if its effect were modeled.
/// Native source: Bugslayer, InfestedAutomaton, Reflections.
/// </summary>
public static class PrototypeNativeLaterActEvents
{
    public const string InfestedAutomatonId =
        "proto.native.hive.infested_automaton";
    public const string ReflectionsId =
        "proto.native.glory.reflections";
    public const string BadLuckId = "proto.curse.bad_luck";

    public static string[] SupportedIds(int act) => act switch
    {
        2 => [PrototypeNativeLaterActs.HiveBugslayerId,
            InfestedAutomatonId,
            PrototypeNativeLaterActEventExpansion.LostWispEventId,
            PrototypeNativeLaterActEventExpansion.ColossalFlowerEventId],
        3 => [ReflectionsId,
            PrototypeNativeLaterActEventExpansion.RoundTeaPartyEventId],
        _ => throw new ArgumentOutOfRangeException(nameof(act))
    };

    public static bool IsSupported(int act, string eventId) =>
        act is 2 or 3
        && SupportedIds(act).Contains(eventId, StringComparer.Ordinal);

    public static PrototypeCardDefinition[] Cards { get; } =
    [
        new(BadLuckId, "Bad Luck", -1, PrototypeCardTarget.None, [],
            Rarity: PrototypeCardRarity.Curse,
            Type: PrototypeCardType.Curse,
            Eternal: true, Unplayable: true, RewardEligible: false,
            EndTurnDamageIfInHand: 13,
            EndTurnDamageUnblockable: true,
            CanBeGeneratedInCombat: false,
            MaxUpgradeLevel: 0)
    ];

    public static PrototypeEventDefinition[] Events { get; } =
    [
        new(InfestedAutomatonId, "Infested Automaton",
        [
            new("study", "Study — add a random Power card", []),
            new("touch_core", "Touch the core — add a random zero-cost card", [])
        ], MinAct: 2, MaxAct: 2, Weight: 0),
        new(ReflectionsId, "Reflections",
        [
            new("touch_mirror", "Touch a mirror — downgrade 2, upgrade 4", []),
            new("shatter", "Shatter — duplicate deck and receive Bad Luck", [])
        ], MinAct: 3, MaxAct: 3, Weight: 0)
    ];
}

public sealed partial class PrototypeGameEngine
{
    private static bool UsesNativeLaterActEvents(RunState state) =>
        state.Configuration is not null
        && state.World is { Act: 2 or 3 } world
        && world.Map.GenerationProfileId == (world.Act == 2
            ? PrototypeNativeLaterActRouting.HiveMapProfile
            : PrototypeNativeLaterActRouting.GloryMapProfile);

    private static PrototypeEventDefinition[] EligibleNativeLaterActEvents(
        RunState state)
    {
        var world = RequireWorld(state);
        return PrototypeNativeLaterActEvents.SupportedIds(world.Act)
            .Where(id => !world.EventIds.Contains(id, StringComparer.Ordinal))
            .Where(id => PrototypeNativeLaterActEventExpansion.IsEligible(
                id, state.Player))
            .Select(PrototypeContent.Event).ToArray();
    }

    private static RunState StartNativeLaterActEvent(RunState state)
    {
        var world = RequireWorld(state);
        var events = EligibleNativeLaterActEvents(state);
        if (events.Length == 0)
            throw new NotSupportedException(
                $"No implemented native {world.ActIdentity} events remain. " +
                "The complete later-act event bag is not supported.");
        // The native event bag contains many additional entries. This
        // subset selector is explicitly non-parity (event RNG stream).
        var chosen = events[PrototypeRng.NextInt(
            state.Rng, "event", events.Length)];
        return state with
        {
            World = world with
            {
                Event = new EventState(chosen.Id,
                    NativeEventGold: chosen.Id ==
                        PrototypeNativeLaterActEventExpansion.LostWispEventId
                        ? 45 + PrototypeRng.NextInt(state.Rng, "event", 31)
                        : 0),
                EventHistory = world.EventIds.Append(chosen.Id).ToArray()
            },
            Phase = RunPhase.Event
        };
    }

    private static RunState StepNativeLaterActEvent(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var eventState = world.Event
            ?? throw new InvalidOperationException(
                "Later-act event has no state.");
        if (!PrototypeNativeLaterActEvents.IsSupported(
                world.Act, eventState.EventId))
            throw new NotSupportedException(
                "The later-act event is not implemented.");
        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (!PrototypeContent.Event(eventState.EventId).Choices.Any(
                option => option.Id == choice))
            throw new InvalidOperationException(
                $"Unknown later-act event choice '{choice}'.");

        if (IsLaterActExpandedEvent(eventState.EventId))
            return StepLaterActExpandedEvent(state, choice);

        var player = state.Player;
        var nextId = world.NextCardInstanceId;
        if (eventState.EventId ==
            PrototypeNativeLaterActs.HiveBugslayerId)
        {
            var cardId = choice switch
            {
                "extermination" => PrototypeNativeLaterActs.ExterminateId,
                "squash" => PrototypeNativeLaterActs.SquashId,
                _ => throw new InvalidOperationException(
                    "Bugslayer choice is invalid.")
            };
            player = AppendCard(player, nextId++, cardId, state.Rng);
        }
        else if (eventState.EventId ==
            PrototypeNativeLaterActEvents.InfestedAutomatonId)
        {
            var pool = PrototypeContent.NativeSilentCardPool
                .Where(cardId =>
                {
                    var card = PrototypeContent.Card(cardId);
                    return card.RewardEligible && (choice switch
                    {
                        "study" => card.Type == PrototypeCardType.Power,
                        "touch_core" => card.Cost.Kind ==
                            PrototypeCardCostKind.Fixed
                            && card.Cost.Amount == 0,
                        _ => throw new InvalidOperationException(
                            "Infested Automaton choice is invalid.")
                    });
                })
                .ToArray();
            if (pool.Length == 0)
                throw new InvalidOperationException(
                    "Native Silent event reward filter is empty.");
            var selected = pool[PrototypeRng.NextInt(
                state.Rng, "event", pool.Length)];
            player = AppendCard(player, nextId++, selected, state.Rng);
        }
        else if (eventState.EventId ==
            PrototypeNativeLaterActEvents.ReflectionsId)
        {
            if (choice == "touch_mirror")
            {
                var deck = player.Deck.ToArray();
                var upgraded = deck.Where(card => card.UpgradeLevel > 0)
                    .Select(card => card.InstanceId).ToList();
                for (var i = 0; i < 2 && upgraded.Count > 0; i++)
                {
                    var index = PrototypeRng.NextInt(
                        state.Rng, "event", upgraded.Count);
                    var selected = upgraded[index];
                    upgraded.RemoveAt(index);
                    deck = deck.Select(card => card.InstanceId == selected
                        ? card with
                        {
                            UpgradeLevel = Math.Max(0,
                                card.UpgradeLevel - 1)
                        }
                        : card).ToArray();
                }

                var upgradeable = deck.Where(card =>
                    card.UpgradeLevel <
                    PrototypeContent.Card(card.CardId).MaxUpgradeLevel)
                    .Select(card => card.InstanceId).ToList();
                for (var i = 0; i < 4 && upgradeable.Count > 0; i++)
                {
                    var index = PrototypeRng.NextInt(
                        state.Rng, "event", upgradeable.Count);
                    var selected = upgradeable[index];
                    upgradeable.RemoveAt(index);
                    deck = deck.Select(card => card.InstanceId == selected
                        ? card with
                        {
                            UpgradeLevel = card.UpgradeLevel + 1
                        }
                        : card).ToArray();
                }
                player = player with { Deck = deck };
            }
            else if (choice == "shatter")
            {
                var originals = player.Deck.ToArray();
                foreach (var card in originals)
                {
                    var clone = card with
                    {
                        InstanceId = nextId++,
                        PersistentState = card.PersistentState.Clone()
                    };
                    player = player with
                    {
                        Deck = player.Deck.Append(clone).ToArray()
                    };
                    player = ApplyRelicRunEvent(
                        player, PrototypeRunEventKind.CardAdded,
                        addedCard: clone, rng: state.Rng);
                }
                player = AppendCard(player, nextId++,
                    PrototypeNativeLaterActEvents.BadLuckId,
                    state.Rng);
            }
            else
            {
                throw new InvalidOperationException(
                    "Reflections choice is invalid.");
            }
        }

        state = state with
        {
            Player = player,
            World = world with
            {
                NextCardInstanceId = nextId,
                Event = null
            }
        };
        return CompleteRoomToMap(state);
    }
}
