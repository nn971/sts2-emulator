namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped PotionRewardOdds for single-player combat rooms.
/// Native CurrentValue begins at 0.4, increases by 0.1 on a miss,
/// decreases by 0.1 on success, and adds 0.25 * 0.5 = 0.125
/// to the current roll threshold for elites. Configured v111 runs use the
/// persistent player Rewards stream; legacy prototype runs retain the
/// integer-thousandth ticket.
/// </summary>
public static class PrototypeNativePotionRewardOdds
{
    public const int InitialThousandths = 400;
    public const int SuccessPenalty = 100;
    public const int MissBonus = 100;
    public const int EliteBonus = 125;

    public static (bool Offered, int UpdatedThousandths) Roll(
        int currentThousandths, bool elite, RngBundle rng,
        bool forced = false, RngBundle? nativeRunRng = null)
    {
        // Native PotionRewardOdds.Roll calls
        // Hook.ShouldForcePotionReward before sampling Rewards RNG
        // or changing persistent pity. White Beast Statue provides
        // this hook for every combat room, including elites and bosses.
        if (forced)
        {
            return (true, currentThousandths);
        }

        // Do not clamp the odds here: native AbstractOdds.CurrentValue
        // is updated directly, without explicit clipping.
        var threshold = currentThousandths + (elite ? EliteBonus : 0);
        bool offered;
        if (nativeRunRng is null)
        {
            offered = LegacyRandomChoiceV111.ChooseBoundedInteger(
                rng,
                "reward.potion_odds.ticket",
                "reward",
                "run:potion_reward_odds",
                1000).SelectedIndex < threshold;
        }
        else
        {
            var decision = NativePlayerRngV111.NextUnitFloatDecision(
                nativeRunRng,
                StochasticSiteRegistryV111.RewardPotionOddsUnitFloatSiteId,
                NativePlayerRngV111.RewardsStreamId,
                "run:potion_reward_odds");
            offered = decision.SelectedValue < threshold / 1000f;
            if (decision.ProviderMode == "native-v111" &&
                RandomBoundaryJournalV111.HasActiveJournal)
                RandomBoundaryJournalV111.RecordPotionRewardOddsOutcome(
                    SemanticPotionRewardOddsNativeAuditV111.Create(
                        decision, currentThousandths, elite));
        }
        return (
            offered,
            checked(currentThousandths + (offered
                ? -SuccessPenalty
                : MissBonus)));
    }
}
