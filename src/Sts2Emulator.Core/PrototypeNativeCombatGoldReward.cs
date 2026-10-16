namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 EncounterModel gold bounds and GoldReward.Populate.
/// Ordinary single-player Overgrowth encounters use the native defaults.
/// Legacy calls use the emulator Rewards stream; configured v111 runs can
/// route the draw through the persistent native player Rewards stream. The
/// caller owns whether and when an offered amount is credited.
/// </summary>
public static class PrototypeNativeCombatGoldReward
{
    public const int PovertyAscension = 3;

    public static (int Min, int Max)? Bounds(
        PrototypeRoomType room, int ascension, float goldProportion = 1f)
    {
        if (ascension < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ascension));
        }
        if (!float.IsFinite(goldProportion)
            || goldProportion < 0f || goldProportion > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(goldProportion));
        }

        var (minimum, maximum) = room switch
        {
            PrototypeRoomType.Combat => (10, 20),
            PrototypeRoomType.Elite => (35, 45),
            PrototypeRoomType.Boss => (100, 100),
            _ => throw new ArgumentOutOfRangeException(nameof(room))
        };
        // EncounterModel casts the Ascension Poverty multiplier to int.
        if (ascension >= PovertyAscension)
        {
            minimum = (int)(minimum * 0.75);
            maximum = (int)(maximum * 0.75);
        }

        if (room == PrototypeRoomType.Combat)
        {
            // RewardsSet omits the normal-combat GoldReward when no
            // enemies contributed to the gold proportion.
            if (goldProportion == 0f)
            {
                return null;
            }
            // Source uses float multiplication then Math.Round, which
            // defaults to MidpointRounding.ToEven.
            minimum = (int)Math.Round((float)minimum * goldProportion);
            maximum = (int)Math.Round((float)maximum * goldProportion);
        }

        return (minimum, maximum);
    }

    public static int? Roll(
        PrototypeRoomType room, int ascension, RngBundle rng,
        float goldProportion = 1f, RngBundle? nativeRunRng = null)
    {
        var bounds = Bounds(room, ascension, goldProportion);
        if (bounds is null)
        {
            return null;
        }

        var (minimum, maximum) = bounds.Value;
        // Native GoldReward.Populate calls Rewards.NextInt(min, max+1).
        var ownerId =
            $"room:{room}:ascension:{ascension}:bounds:{minimum}:{maximum}";
        var index = nativeRunRng is null
            ? LegacyRandomChoiceV111.ChooseBoundedInteger(
                rng, "reward.combat_gold.index_draw", "reward",
                ownerId, checked(maximum - minimum + 1)).SelectedIndex
            : NativePlayerRngV111.ChooseBoundedInteger(
                nativeRunRng,
                "reward.combat_gold.index_draw",
                NativePlayerRngV111.RewardsStreamId,
                ownerId,
                checked(maximum - minimum + 1));
        return minimum + index;
    }
}
