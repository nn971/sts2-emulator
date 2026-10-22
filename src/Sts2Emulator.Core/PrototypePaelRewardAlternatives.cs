namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static RunState SacrificePaelCardReward(RunState state, GameAction action)
    {
        if (!GetRewardActions(state).Any(legal => legal.Kind == action.Kind
            && (action.Kind == "sacrifice_reward_card"
                || legal.ReadPayload<ChooseRewardCardGroupPayload>() == action.ReadPayload<ChooseRewardCardGroupPayload>())))
            throw new InvalidOperationException("Card reward sacrifice is unavailable.");
        // Reuse the existing primary/extra reward resolver. Sacrifice is
        // distinct from ordinary skipping; only its own action increments.
        state = StepReward(state, action with { Kind = action.Kind == "sacrifice_reward_card"
            ? "skip_reward_card" : "skip_reward_card_group" });
        var world = RequireWorld(state);
        var player = state.Player;
        var nativeRunRng = UsesNativeRegionalSystems(world)
            ? state.Stochastic.NativeRunRng?.Fork()
            : null;
        var prototypeRng = state.Stochastic.PrototypeRng;
        var wingIndex = Array.FindIndex(player.Relics, relic =>
            relic.RelicId == NativePaelRewardsV111.WingId && !relic.IsMelted);
        var count = checked(NativePaelRewardsV111.RewardsSacrificed(player.Relics[wingIndex]) + 1);
        player = NativePaelRewardsV111.SetRewardsSacrificed(player, wingIndex, count);
        if (count % 2 == 0)
        {
            var draw = PrototypeNativeRelicGrabBag.Draw(
                world, player, prototypeRng, merchant: false,
                nativeRunRng: nativeRunRng);
            world = draw.World;
            var id = draw.Id ?? throw new NotSupportedException("Pael's Wing requires an available front-bag relic.");
            player = player with { Relics = [..player.Relics, new(id, PrototypeJson.EmptyObject())] };
            var nextId = world.NextCardInstanceId;
            player = ApplyRelicRunEvent(player, PrototypeRunEventKind.RelicAcquired,
                acquiredRelicId: id, rng: prototypeRng,
                allocateCardId: () => nextId++,
                nativeRunRng: nativeRunRng);
            world = world with { NextCardInstanceId = nextId, Reward = world.Reward! with
            { PendingDeckChoice = CreateRelicDeckChoice(player, id) } };
        }
        return state with
        {
            Player = player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                RunStochasticStateV111.WithNativeRunRng(
                    state.Stochastic,
                    nativeRunRng ?? state.Stochastic.NativeRunRng),
                prototypeRng),
            World = world
        };
    }
}
