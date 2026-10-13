namespace Sts2Emulator.Core;

/// <summary>
/// Lava Lamp marks each combat card reward for late upgrades unless its
/// owner took damage during that combat.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static RewardState CaptureLavaLampCombatReward(
        PlayerState player,
        CombatState combat,
        RewardState reward)
    {
        var active = player.ActiveRelics.Any(relic =>
            relic.RelicId == PrototypeNativeSharedRelics.LavaLampId)
            && !combat.TookDamageThisCombat;
        return ApplyLavaLampRewardModifier(
            player,
            reward with { LavaLampRewardActive = active });
    }

    internal static RewardState ApplyLavaLampRewardModifier(
        PlayerState player,
        RewardState reward)
    {
        if (!reward.LavaLampRewardActive
            || !player.ActiveRelics.Any(relic =>
                relic.RelicId == PrototypeNativeSharedRelics.LavaLampId))
        {
            return reward;
        }

        var cardFlags = UpgradeEligibleOptions(
            reward.CardOptions,
            reward.CardOptionUpgradeFlags);
        var extraOptions = reward.ExtraCardOptions
            ?? Array.Empty<string[]>();
        var currentExtraFlags = reward.ExtraCardOptionUpgradeFlags;
        var extraFlags = currentExtraFlags is null
            ? extraOptions.Select(options =>
                new bool[options.Length]).ToArray()
            : currentExtraFlags
                .Select(flags => (bool[])flags.Clone())
                .ToArray();
        for (var index = 0; index < extraOptions.Length; index++)
        {
            var flags = index < extraFlags.Length
                ? extraFlags[index]
                : Array.Empty<bool>();
            if (flags.Length != extraOptions[index].Length)
            {
                flags = new bool[extraOptions[index].Length];
            }
            extraFlags[index] = UpgradeEligibleOptions(
                extraOptions[index], flags);
        }

        return reward with
        {
            CardOptionUpgradeFlags = cardFlags,
            ExtraCardOptionUpgradeFlags = extraOptions.Length == 0
                && currentExtraFlags is null
                    ? null
                    : extraFlags
        };
    }

    private static bool[] UpgradeEligibleOptions(
        IReadOnlyList<string> options,
        bool[]? existingFlags)
    {
        var flags = existingFlags is { Length: var length }
            && length == options.Count
                ? (bool[])existingFlags.Clone()
                : new bool[options.Count];
        for (var index = 0; index < options.Count; index++)
        {
            if (PrototypeContent.Card(options[index]).MaxUpgradeLevel > 0)
            {
                flags[index] = true;
            }
        }
        return flags;
    }
}
