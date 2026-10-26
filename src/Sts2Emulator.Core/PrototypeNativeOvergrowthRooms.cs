namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    internal static bool UsesNativeActOneSystems(RunWorldState world) =>
        world.Act == 1 && (PrototypeNativeOvergrowthMap.IsGenerationProfile(
                world.Map.GenerationProfileId)
            || world.Map.GenerationProfileId == PrototypeNativeUnderdocks.GenerationProfileId);

    // The Act 1 economy/Unknown machinery is structurally reusable for
    // configured Hive and Glory. Its encounter/event RNG distributions
    // still need native oracle comparison, so legacy and isolated
    // later-act fixtures are kept on their historical code paths.
    internal static bool UsesNativeRegionalSystems(RunWorldState world) =>
        UsesNativeActOneSystems(world)
        || (world.RulesetId == V111Build.RulesetId
            && world.Act is 2 or 3
            && (world.Map.GenerationProfileId
                == PrototypeNativeLaterActMap.ProfileId(world.Act)
                || world.Map.GenerationProfileId == PrototypeGoldenPathMapV111.ProfileId));

    private static PrototypeRoomType ResolveUnknownMapRoom(
        ref RunWorldState world,
        RngBundle rng,
        bool eventOnly = false)
    {
        if (!UsesNativeRegionalSystems(world))
        {
            throw new InvalidOperationException(
                "Unknown map rooms require a native-structure generation profile.");
        }

        var odds = world.UnknownRoomOdds
            ?? new PrototypeUnknownRoomOddsState();
        var oddsSupport = PrototypeUnknownRoomOddsSupportV111
            .ForRoomResolution(world, eventOnly);
        var ticketLabels = ControlledStochasticCandidateLabelsV111.IsActive &&
            oddsSupport.TotalTicketCount <=
                ControlledRunActionEngineV111.MaximumInlineOutcomeActions
            ? oddsSupport.TicketOutcomeIds()
            : [];
        int roll;
        using (ControlledStochasticCandidateLabelsV111.Describe(ticketLabels))
        {
            roll = LegacyRandomChoiceV111.ChooseBoundedInteger(
                rng,
                "event.unknown_room.odds.ticket",
                "event",
                oddsSupport.RequestOwnerId,
                oddsSupport.TotalTicketCount).SelectedIndex;
        }

        // UnknownMapPointOdds.Roll consumes the same ticket even when a map
        // modifier permits only Event. Disallowed non-event odds do not grow.
        var selected = oddsSupport.RoomAtTicket(roll);
        if (eventOnly)
            return selected;
        world = world with { UnknownRoomOdds = odds.After(selected) };
        return selected;
    }

    private static RunState StartTreasureRoom(RunState state)
    {
        var treasure = ApplySilverCrucibleTreasureEntry(state.Player);
        state = state with { Player = treasure.Player };
        if (treasure.SkipTreasure)
        {
            // The first chest entered with Silver Crucible contains
            // no treasure; the room is still visited and recorded.
            return CompleteRoomToMap(state);
        }

        var world = RequireWorld(state);
        var nativeRunRng = state.Stochastic.NativeRunRng;
        var prototypeRng = state.Stochastic.PrototypeRng;
        string? relicId;
        if (UsesNativeRegionalSystems(world))
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                world, state.Player, prototypeRng, merchant: false,
                nativeRunRng: nativeRunRng);
            world = draw.World;
            relicId = draw.Id;
        }
        else
        {
            var available = PrototypeContent.RelicPool
                .Where(id => !state.Player.Relics.Any(relic =>
                    StringComparer.Ordinal.Equals(relic.RelicId, id)))
                .ToArray();
            relicId = available.Length == 0
                ? null
                : available[ResolveLegacyRandomIndex(
                    prototypeRng,
                    "treasure.relic_model.index_draw",
                    $"act:{world.Act}:floor:{world.Floor}:pool:"
                        + CanonicalJson.Sha256(available),
                    Enumerable.Range(0, available.Length),
                    streamId: "reward",
                    controlledCandidateLabels: (string[])available.Clone())];
        }

        var stochastic = RunStochasticStateV111.WithPrototypeRng(
            state.Stochastic, prototypeRng);
        if (nativeRunRng is not null)
            stochastic = RunStochasticStateV111.WithNativeRunRng(
                stochastic, nativeRunRng);
        if (relicId is null)
        {
            // Constrained catalog exhaustion. Native uses Circlet.
            return CompleteRoomToMap(state with
            {
                Stochastic = stochastic,
                World = world
            });
        }
        var reward = new RewardState(
            SourceRoom: "Treasure",
            CardOptions: [],
            PotionOption: null,
            RelicOption: relicId,
            CardResolved: true,
            PotionResolved: true,
            RelicResolved: false,
            EndsAct: false);
        return state with
        {
            Stochastic = stochastic,
            World = world with { Reward = reward },
            Phase = RunPhase.Reward
        };
    }
}
