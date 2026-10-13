namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static bool UsesNativeActOneSystems(RunWorldState world) =>
        world.Act == 1 && (world.Map.GenerationProfileId == PrototypeNativeOvergrowthMap.GenerationProfileId
            || world.Map.GenerationProfileId == PrototypeNativeUnderdocks.GenerationProfileId);

    // The Act 1 economy/Unknown machinery is structurally reusable for
    // configured Hive and Glory. Its encounter/event RNG distributions
    // still need native oracle comparison, so legacy and isolated
    // later-act fixtures are kept on their historical code paths.
    private static bool UsesNativeRegionalSystems(RunWorldState world) =>
        UsesNativeActOneSystems(world)
        || (world.RulesetId == V111Build.RulesetId
            && world.Act is 2 or 3
            && (world.Map.GenerationProfileId
                == PrototypeNativeLaterActMap.ProfileId(world.Act)
                || world.Map.GenerationProfileId == PrototypeGoldenPathMapV111.ProfileId));

    private static PrototypeRoomType ResolveUnknownMapRoom(
        ref RunWorldState world,
        RngBundle rng,
        PlayerState player,
        bool eventOnly = false)
    {
        if (!UsesNativeRegionalSystems(world))
        {
            throw new InvalidOperationException(
                "Unknown map rooms require a native-structure generation profile.");
        }

        var odds = world.UnknownRoomOdds
            ?? new PrototypeUnknownRoomOddsState();
        var monsterRoomAvailable = !player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .ExcludesMonsterRoomsFromUnknownMap);
        var roll = PrototypeRng.NextInt(
            rng, "event", PrototypeUnknownRoomOddsState.Scale);

        // UnknownMapPointOdds.Roll consumes NextFloat even when a map
        // modifier permits only Event. Disallowed non-event odds do not grow.
        if (eventOnly)
            return PrototypeRoomType.Event;

        PrototypeRoomType selected;
        var monsterWeight = monsterRoomAvailable
            ? odds.MonsterWeight : 0;
        if (roll < monsterWeight)
        {
            selected = PrototypeRoomType.Combat;
        }
        else if (roll < monsterWeight + odds.TreasureWeight)
        {
            selected = PrototypeRoomType.Treasure;
        }
        else if (roll < monsterWeight + odds.TreasureWeight
                 + odds.ShopWeight)
        {
            selected = PrototypeRoomType.Shop;
        }
        else
        {
            selected = PrototypeRoomType.Event;
        }

        world = world with
        {
            UnknownRoomOdds = odds.After(selected, monsterRoomAvailable)
        };
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
        string? relicId;
        if (UsesNativeRegionalSystems(world))
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                world, state.Player, state.Rng, merchant: false);
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
                : available[PrototypeRng.NextInt(
                    state.Rng, "reward", available.Length)];
        }

        if (relicId is null)
        {
            // Constrained catalog exhaustion. Native uses Circlet.
            return CompleteRoomToMap(state with { World = world });
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
            World = world with { Reward = reward },
            Phase = RunPhase.Reward
        };
    }
}
