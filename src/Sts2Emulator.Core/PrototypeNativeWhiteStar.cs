namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 White Star elite card reward behavior.
/// </summary>
public static class PrototypeNativeWhiteStar
{
    public static int AdditionalRareCardRewardGroups(
        PlayerState player,
        PrototypeRoomType room) =>
        room == PrototypeRoomType.Elite
            ? checked(player.ActiveRelics.Sum(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .ExtraEliteRareCardRewardCount))
            : 0;
}
