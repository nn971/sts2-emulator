namespace Sts2Emulator.Core;

/// <summary>
/// Cauldron's five separately populated PotionRewards on relic acquisition.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal const string CauldronRewardSource = "Relic:Cauldron";

    internal static RunState ApplyCauldronAcquisitionReward(
        RunState before, RunState after)
    {
        var gained = after.Player.Relics.Count(relic =>
                relic.RelicId == PrototypeNativeSharedRelics.CauldronId)
            - before.Player.Relics.Count(relic =>
                relic.RelicId == PrototypeNativeSharedRelics.CauldronId);
        if (gained <= 0)
            return after;

        var nativeRunRng = after.NativeRunRng?.Fork();
        for (var instance = 0; instance < gained; instance++)
        {
            var world = RequireWorld(after);
            var returnState = new PrototypeCustomRewardReturnState(
                after.Phase, world.Reward, world.CustomRewardReturn);
            var potions = new string[
                PrototypeContent.Relic(
                    PrototypeNativeSharedRelics.CauldronId)
                    .AcquisitionPotionRewardCount];
            for (var index = 0; index < potions.Length; index++)
            {
                potions[index] = nativeRunRng is null
                    ? PrototypeNativePotionShop.PickWeighted(
                        after.Rng, "reward")
                    : PrototypeNativePotionShop.PickWeightedNative(
                        nativeRunRng, "player.rewards");
            }

            var reward = new RewardState(
                SourceRoom: CauldronRewardSource,
                CardOptions: [],
                PotionOption: potions[0],
                RelicOption: null,
                CardResolved: true,
                PotionResolved: false,
                RelicResolved: true,
                EndsAct: false,
                IndependentSelection: true,
                ExtraCardGroupsResolved: [],
                ExtraRelicGroupsResolved: [],
                ExtraPotionOptions: potions[1..],
                ExtraPotionGroupsResolved:
                    new bool[potions.Length - 1]);
            after = after with
            {
                Phase = RunPhase.Reward,
                NativeRunRng = nativeRunRng,
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
