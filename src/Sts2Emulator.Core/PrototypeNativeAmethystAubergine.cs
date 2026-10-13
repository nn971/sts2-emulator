namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 Amethyst Aubergine combat-gold reward behavior.
/// </summary>
public static class PrototypeNativeAmethystAubergine
{
    public static int GoldBonus(
        PlayerState player,
        PrototypeRoomType room,
        int act,
        bool endsAct)
    {
        if (room is not (PrototypeRoomType.Combat
            or PrototypeRoomType.Elite
            or PrototypeRoomType.Boss)
            || player.ActiveRelics.Any(relic =>
                PrototypeContent.Relic(relic.RelicId).PreventGoldGain)
            || room == PrototypeRoomType.Boss && act == 3 && endsAct)
        {
            return 0;
        }

        return checked(player.ActiveRelics.Sum(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .CombatVictoryGoldReward));
    }

    public static int[] ExtraRewardOptions(
        PlayerState player,
        int recoveredGold,
        PrototypeRoomType room,
        int act,
        bool endsAct)
    {
        if (player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId).PreventGoldGain))
        {
            return [];
        }

        var rewards = new List<int>(2);
        if (recoveredGold > 0)
        {
            rewards.Add(recoveredGold);
        }

        var amethystGold = GoldBonus(player, room, act, endsAct);
        if (amethystGold > 0)
        {
            rewards.Add(amethystGold);
        }

        return rewards.ToArray();
    }
}
