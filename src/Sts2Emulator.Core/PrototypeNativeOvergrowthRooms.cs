namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static PrototypeRoomType ResolveUnknownMapRoom(
        ref RunWorldState world,
        RngBundle rng)
    {
        if (world.Map.GenerationProfileId
            != PrototypeNativeOvergrowthMap.GenerationProfileId)
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
        var world = RequireWorld(state);
        var available = PrototypeContent.RelicPool
            .Where(relicId => !state.Player.Relics.Any(relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, relicId)))
            .ToArray();

        if (available.Length == 0)
        {
            // This is a constrained prototype fallback; the native game's
            // treasure reward system has broader relic classes and hooks.
            return CompleteRoomToMap(state);
        }

        var relicId = available[
            PrototypeRng.NextInt(state.Rng, "reward", available.Length)];
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
