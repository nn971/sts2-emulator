namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped PotionRewardOdds for single-player combat rooms.
/// Native CurrentValue begins at 0.4, increases by 0.1 on a miss,
/// decreases by 0.1 on success, and adds 0.25 * 0.5 = 0.125
/// to the current roll threshold for elites. The prototype
/// uses integer thousandths and the reward stream rather than
/// asserting exact native floating-point RNG parity.
/// </summary>
public static class PrototypeNativePotionRewardOdds
{
    public const int InitialThousandths = 400;
    public const int SuccessPenalty = 100;
    public const int MissBonus = 100;
    public const int EliteBonus = 125;

    public static (bool Offered, int UpdatedThousandths) Roll(
        int currentThousandths, bool elite, RngBundle rng)
    {
        // Do not clamp the odds here: native AbstractOdds.CurrentValue
        // is updated directly, without explicit clipping.
        var threshold = currentThousandths + (elite ? EliteBonus : 0);
        var roll = PrototypeRng.NextInt(rng, "reward", 1000);
        var offered = roll < threshold;
        return (
            offered,
            checked(currentThousandths + (offered
                ? -SuccessPenalty
                : MissBonus)));
    }
}
