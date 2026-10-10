namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static bool UsesNativeActOneSystems(RunWorldState world) =>
        world.Act == 1 && (world.Map.GenerationProfileId == PrototypeNativeOvergrowthMap.GenerationProfileId
            || world.Map.GenerationProfileId == PrototypeNativeUnderdocks.GenerationProfileId);

    private static PrototypeRoomType ResolveUnknownMapRoom(
        ref RunWorldState world,
        RngBundle rng)
    {
        var supportedLaterMap = world.Act switch
        {
            2 => world.Map.GenerationProfileId ==
                PrototypeNativeLaterActRouting.HiveMapProfile,
            3 => world.Map.GenerationProfileId ==
                PrototypeNativeLaterActRouting.GloryMapProfile,
            _ => false
        };
        if (!UsesNativeActOneSystems(world) && !supportedLaterMap)
        {
            throw new InvalidOperationException(
                "Unknown map rooms require a native-structure generation profile.");
        }

        var odds = world.UnknownRoomOdds
            ?? new PrototypeUnknownRoomOddsState();
        var roll = PrototypeRng.NextInt(
            rng, "event", PrototypeUnknownRoomOddsState.Scale);

        PrototypeRoomType selected;
        if (roll < odds.MonsterWeight)
        {
            selected = PrototypeRoomType.Combat;
        }
        else if (roll < odds.MonsterWeight + odds.TreasureWeight)
        {
            selected = PrototypeRoomType.Treasure;
        }
        else if (roll < odds.MonsterWeight + odds.TreasureWeight
                 + odds.ShopWeight)
        {
            selected = PrototypeRoomType.Shop;
        }
        else
        {
            selected = PrototypeRoomType.Event;
        }

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
        string? relicId;
        if (UsesNativeActOneSystems(world))
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
