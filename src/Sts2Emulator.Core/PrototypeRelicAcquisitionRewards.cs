namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private const string ToyBoxRewardSource = "Relic:ToyBox";

    private static RunState ApplyToyBoxAcquisitionReward(RunState before, RunState after)
    {
        var count = after.Player.Relics.Count(relic => relic.RelicId == NativeAncientRewardsV111.ToyBoxId)
            - before.Player.Relics.Count(relic => relic.RelicId == NativeAncientRewardsV111.ToyBoxId);
        if (count <= 0)
            return after;
        var world = RequireWorld(after);
        var prototypeRng = after.Stochastic.PrototypeRng;
        var nativeRunRng = UsesNativeRegionalSystems(world)
            ? after.Stochastic.NativeRunRng
            : null;
        // Event acquisition already suspends the selected option in its
        // ordinary event continuation, before completing the event room.
        if (world.Reward?.ExtraRelicWaxFlags is { Length: 5 } flags && flags.All(flag => flag)
            && world.Reward.SourceRoom.StartsWith("Event:", StringComparison.Ordinal))
            return after;
        for (var index = 0; index < count; index++)
        {
            var returnState = new PrototypeCustomRewardReturnState(
                after.Phase, world.Reward, world.CustomRewardReturn);
            var reward = CreateWaxRelicReward(
                after.Player, ToyBoxRewardSource, ref world,
                prototypeRng, nativeRunRng);
            world = world with { Reward = reward, CustomRewardReturn = returnState };
            var stochastic = RunStochasticStateV111.WithPrototypeRng(
                after.Stochastic, prototypeRng);
            if (nativeRunRng is not null)
                stochastic = RunStochasticStateV111.WithNativeRunRng(
                    stochastic, nativeRunRng);
            after = after with
            {
                Phase = RunPhase.Reward,
                Stochastic = stochastic,
                World = world
            };
        }
        return after;
    }

    private static RewardState CreateWaxRelicReward(
        PlayerState player, string source,
        ref RunWorldState world, RngBundle prototypeRng,
        RngBundle? nativeRunRng = null)
    {
        var ids = new string[5];
        for (var index = 0; index < ids.Length; index++)
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                world, player, prototypeRng, merchant: false,
                nativeRunRng: nativeRunRng);
            world = draw.World;
            ids[index] = draw.Id ?? throw new NotSupportedException(
                "Toy Box requires five available front-bag relic rewards.");
        }
        return new RewardState(source, [], null, null, true, true, true, false,
            ExtraRelicRewardIds: ids, IndependentSelection: true,
            ExtraCardGroupsResolved: [], ExtraRelicGroupsResolved: new bool[5],
            ExtraRelicWaxFlags: [true, true, true, true, true]);
    }
}
