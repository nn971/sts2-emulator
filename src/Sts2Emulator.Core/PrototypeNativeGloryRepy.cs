namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 Lantern Key -> Glory War Historian Repy path.
///
/// Native LanternKey.ModifyUnknownMapPointRoomTypes converts a Glory '?'
/// to Event, and ModifyNextEvent selects WarHistorianRepy even though
/// its own IsAllowed is false. The override is integrated with the opt-in
/// Glory map path; general event selection and native RNG parity remain
/// separate fidelity gates.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static bool ShouldForceWarHistorianRepy(
        RunWorldState world, PlayerState player) =>
        world.Act == 3
        && player.Deck.Any(card =>
            card.CardId == PrototypeNativeHiveEvents.LanternKeyCardId);

    private static RunState StartNativeWarHistorianRepy(RunState state)
    {
        var world = RequireWorld(state);
        if (!ShouldForceWarHistorianRepy(world, state.Player)
            || world.ActiveRoom != PrototypeRoomType.Event)
        {
            throw new InvalidOperationException(
                "War Historian Repy requires a Lantern Key in a Glory event room.");
        }

        var id = PrototypeNativeHiveEvents.WarHistorianRepyId;
        return state with
        {
            World = world.AppendEventId(id) with
            {
                Event = new EventState(id)
            },
            Phase = RunPhase.Event
        };
    }

    private static IReadOnlyList<GameAction>
        GetNativeWarHistorianRepyActions(EventState current)
    {
        var choices = current.NativePageIndex switch
        {
            0 => new[] { "unlock_cage", "unlock_chest" },
            1 when current.RepyFirstChoiceId == "unlock_cage"
                => new[] { "unlock_chest" },
            1 when current.RepyFirstChoiceId == "unlock_chest"
                => new[] { "unlock_cage" },
            _ => Array.Empty<string>()
        };
        return choices.Select(id =>
            GameAction.Create("event_choice",
                new EventChoicePayload(id))).ToArray();
    }

    private static RunState StepNativeWarHistorianRepy(
        RunState state, GameAction action)
    {
        RequireKind(action, "event_choice");
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException(
                "War Historian Repy has no active event.");
        if (evt.EventId != PrototypeNativeHiveEvents.WarHistorianRepyId)
            throw new InvalidOperationException("Unexpected quest event.");

        var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
        if (!GetNativeWarHistorianRepyActions(evt).Any(option =>
            option.ReadPayload<EventChoicePayload>().ChoiceId == choice))
        {
            throw new InvalidOperationException(
                $"Choice '{choice}' is unavailable on Repy page {evt.NativePageIndex}.");
        }

        var keys = state.Player.Deck
            .Where(card =>
                card.CardId == PrototypeNativeHiveEvents.LanternKeyCardId)
            .ToArray();
        if (keys.Length == 0)
            throw new InvalidOperationException(
                "War Historian Repy cannot complete without a Lantern Key.");

        var secondChoice = evt.NativePageIndex == 1;
        // Native solo initial action consumes exactly one key; second
        // choice consumes all remaining keys, with exact instance IDs.
        var consumed = secondChoice
            ? keys.Select(card => card.InstanceId).ToHashSet()
            : new HashSet<long> { keys[0].InstanceId };
        var player = state.Player with
        {
            Deck = state.Player.Deck
                .Where(card => !consumed.Contains(card.InstanceId))
                .ToArray()
        };
        var nextPage = !secondChoice && keys.Length > 1 ? 1 : 2;
        evt = evt with
        {
            NativePageIndex = nextPage,
            RepyFirstChoiceId = secondChoice ? evt.RepyFirstChoiceId : choice
        };
        state = state with
        {
            Player = player,
            World = world with { Event = evt }
        };

        if (choice == "unlock_cage")
        {
            player = player with
            {
                Relics = player.Relics.Append(new RelicInstance(
                    PrototypeNativeHiveEvents.HistoryCourseId,
                    PrototypeJson.EmptyObject())).ToArray()
            };
            var prototypeRng = state.Stochastic.PrototypeRng;
            player = ApplyRelicRunEvent(
                player, PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: PrototypeNativeHiveEvents.HistoryCourseId,
                rng: prototypeRng);
            state = state with
            {
                Player = player,
                Stochastic = RunStochasticStateV111.WithPrototypeRng(
                    state.Stochastic, prototypeRng),
                World = RequireWorld(state) with { FreedRepy = true }
            };
        }
        else
        {
            // The source offers two PotionReward and two RelicReward
            // objects together. Persist all four choices before showing
            // their single shared reward screen.
            var potions = PrototypeContent.PotionPool;
            var available =
                PrototypeNativeHiveEvents.WarHistorianRepyRelicOfferCandidates(
                    player);
            if (potions.Length == 0 || available.Length < 2)
                throw new NotSupportedException(
                    "War Historian chest requires two implemented relic offers and a potion pool.");

            var nativeRunRng = UsesNativeRegionalSystems(world)
                ? state.Stochastic.NativeRunRng
                : null;
            var prototypeRng = state.Stochastic.PrototypeRng;
            string[] offers;
            if (nativeRunRng is { } nativeRelicRng)
            {
                var shuffled = available.ToArray();
                NativeEventRandomProbeV111.CaptureShuffle(
                    PrototypeNativeHiveEvents
                        .WarHistorianRepyRelicShuffleSiteId,
                    "event:war_historian_repy:relic_offer",
                    shuffled, nativeRelicRng,
                    NativePlayerRngV111.RewardsStreamId);
                _ = NativeFisherYatesV111.ShuffleInPlace(
                    shuffled,
                    nativeRelicRng,
                    new NativeRandomIndexProviderV111(),
                    NativePlayerRngV111.RewardsStreamId,
                    "event:war_historian_repy:relic_offer",
                    "event.war_historian.repy.relic_offer.shuffle_swap_index");
                offers = shuffled.Take(2).ToArray();
            }
            else
            {
                offers = PickDistinct(available, 2, prototypeRng, "reward");
            }
            int PotionIndex(int ordinal)
            {
                var ownerId = $"event:war_historian_repy:potion_offer:{ordinal}";
                return nativeRunRng is { } nativeRewardRng
                    ? NativePlayerRngV111.ChooseIndex(
                        nativeRewardRng,
                        PrototypeNativeHiveEvents
                            .WarHistorianRepyPotionOfferSiteId,
                        NativePlayerRngV111.RewardsStreamId,
                        ownerId,
                        potions.Length)
                    : LegacyRandomChoiceV111.ChooseIndexRange(
                        prototypeRng,
                        PrototypeNativeHiveEvents
                            .WarHistorianRepyPotionOfferSiteId,
                        "reward",
                        ownerId,
                        potions.Length).SelectedIndex;
            }
            evt = evt with
            {
                Stochastic =
                    EventStochasticStateV111.WithCommittedRandom(
                        evt.Stochastic,
                        evt.Stochastic.RandomOutcomes with
                        {
                            RepyChestPotionIds =
                            [
                                potions[PotionIndex(0)],
                                potions[PotionIndex(1)]
                            ],
                            RepyChestRelicIds = offers
                        }),
                RepyChestGroupsResolved = 0
            };
            var stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng);
            if (nativeRunRng is not null)
                stochastic = RunStochasticStateV111.WithNativeRunRng(
                    stochastic, nativeRunRng);
            state = state with
            {
                Stochastic = stochastic,
                World = RequireWorld(state) with { Event = evt }
            };
        }

        return ContinueNativeWarHistorianRepy(state);
    }

    private static RunState ContinueNativeWarHistorianRepy(RunState state)
    {
        var world = RequireWorld(state);
        var evt = world.Event
            ?? throw new InvalidOperationException("Missing Repy continuation.");
        if (evt.EventId != PrototypeNativeHiveEvents.WarHistorianRepyId)
            throw new InvalidOperationException("Foreign Repy continuation.");

        if (evt.Stochastic.RandomOutcomes.RepyChestPotionIds
                is { Length: 2 } potions
            && evt.Stochastic.RandomOutcomes.RepyChestRelicIds
                is { Length: 2 } relics
            && evt.RepyChestGroupsResolved == 0)
        {
            var reward = new RewardState(
                SourceRoom: "Event:" + evt.EventId,
                CardOptions: [],
                PotionOption: potions[0],
                RelicOption: relics[0],
                CardResolved: true,
                PotionResolved: false,
                RelicResolved: false,
                EndsAct: false,
                IndependentSelection: true,
                ExtraCardGroupsResolved: [],
                ExtraRelicRewardIds: [relics[1]],
                ExtraRelicGroupsResolved: [false],
                ExtraPotionOptions: [potions[1]],
                ExtraPotionGroupsResolved: [false]);
            return state with
            {
                Phase = RunPhase.Reward,
                World = world with
                {
                    Event = evt with
                    {
                        RepyChestGroupsResolved = 1
                    },
                    Reward = reward
                }
            };
        }

        // The extra key unlocks the opposite choice only after all
        // chest rewards (if any) have been resolved.
        if (evt.NativePageIndex == 1)
        {
            return state with { Phase = RunPhase.Event };
        }

        return CompleteRoomToMap(state);
    }
}
