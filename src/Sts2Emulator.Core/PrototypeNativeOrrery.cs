namespace Sts2Emulator.Core;

/// <summary>
/// Orrery's five separately populated three-card rewards on relic acquisition.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal const string OrreryRewardSource = "Relic:Orrery";

    internal static RunState ApplyOrreryAcquisitionReward(
        RunState before, RunState after)
    {
        var gained = after.Player.Relics.Count(relic =>
                relic.RelicId == PrototypeNativeSharedRelics.OrreryId)
            - before.Player.Relics.Count(relic =>
                relic.RelicId == PrototypeNativeSharedRelics.OrreryId);
        if (gained <= 0)
            return after;

        for (var instance = 0; instance < gained; instance++)
        {
            var world = RequireWorld(after);
            var returnState = new PrototypeCustomRewardReturnState(
                after.Phase, world.Reward, world.CustomRewardReturn);
            var cardGroups = new string[5][];
            var upgradeGroups = new bool[5][];
            var generators = new PrototypeCardRewardGenerator?[5];
            var rarityOffset = world.CardRarityOffsetBasisPoints;
            for (var groupIndex = 0;
                 groupIndex < cardGroups.Length;
                 groupIndex++)
            {
                var generated = PrototypeNativeCardRarityOdds
                    .GenerateEncounterCards(
                        count: 3,
                        ascension: after.Ascension,
                        room: PrototypeRoomType.Combat,
                        currentOffsetBasisPoints: rarityOffset,
                        rng: after.Rng,
                        act: world.Act);
                cardGroups[groupIndex] = generated.Cards;
                upgradeGroups[groupIndex] = generated.UpgradeFlags;
                rarityOffset = generated.NextOffsetBasisPoints;
                generators[groupIndex] = new(
                    PrototypeCardRewardGeneratorKind.Encounter,
                    world.Act,
                    PrototypeRoomType.Combat);
            }

            var reward = new RewardState(
                SourceRoom: OrreryRewardSource,
                CardOptions: cardGroups[0],
                PotionOption: null,
                RelicOption: null,
                CardResolved: false,
                PotionResolved: true,
                RelicResolved: true,
                EndsAct: false,
                ExtraCardOptions: cardGroups[1..],
                IndependentSelection: true,
                ExtraCardGroupsResolved: new bool[cardGroups.Length - 1],
                ExtraRelicGroupsResolved: [],
                CardOptionUpgradeFlags: upgradeGroups[0],
                ExtraCardOptionUpgradeFlags: upgradeGroups[1..],
                CardRewardGenerators: generators);
            after = after with
            {
                Phase = RunPhase.Reward,
                World = world with
                {
                    CardRarityOffsetBasisPoints = rarityOffset,
                    Reward = reward,
                    CustomRewardReturn = returnState
                }
            };
        }

        return after;
    }
}
