namespace Sts2Emulator.Core;

/// <summary>
/// Glass Eye's acquisition reward: two Common, two Uncommon and one Rare
/// three-card reward group, each independently selectable or skippable.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private const string GlassEyeRewardSource = "Relic:GlassEye";

    private static RunState ApplyGlassEyeAcquisitionReward(
        RunState before, RunState after)
    {
        var gained = after.Player.Relics.Count(relic =>
                relic.RelicId == PrototypeNativeOrobasRelics.GlassEyeId)
            - before.Player.Relics.Count(relic =>
                relic.RelicId == PrototypeNativeOrobasRelics.GlassEyeId);
        if (gained <= 0)
            return after;

        for (var instance = 0; instance < gained; instance++)
        {
            var world = RequireWorld(after);
            var returnState = new PrototypeCustomRewardReturnState(
                after.Phase, world.Reward, world.CustomRewardReturn);
            var rarities = new[]
            {
                PrototypeCardRarity.Common,
                PrototypeCardRarity.Common,
                PrototypeCardRarity.Uncommon,
                PrototypeCardRarity.Uncommon,
                PrototypeCardRarity.Rare
            };
            var groups = rarities.Select(rarity =>
            {
                var pool = PrototypeContent.RewardCardPool
                    .Where(id => PrototypeContent.Card(id).Rarity == rarity)
                    .ToArray();
                if (pool.Length < 3)
                    throw new NotSupportedException(
                        $"Glass Eye requires three {rarity} Silent cards.");
                return PickDistinct(pool, 3, after.Rng, "reward");
            }).ToArray();
            var generators = rarities.Select(rarity =>
                (PrototypeCardRewardGenerator?)new(
                    PrototypeCardRewardGeneratorKind.RarityPool,
                    world.Act, Rarity: rarity)).ToArray();
            var reward = new RewardState(
                SourceRoom: GlassEyeRewardSource,
                CardOptions: groups[0],
                PotionOption: null,
                RelicOption: null,
                CardResolved: false,
                PotionResolved: true,
                RelicResolved: true,
                EndsAct: false,
                ExtraCardOptions: groups[1..],
                IndependentSelection: true,
                ExtraCardGroupsResolved: new bool[groups.Length - 1],
                ExtraRelicGroupsResolved: [],
                CardRewardGenerators: generators);
            after = after with
            {
                Phase = RunPhase.Reward,
                World = world with
                {
                    Reward = reward,
                    CustomRewardReturn = returnState
                }
            };
        }

        return after;
    }
}
