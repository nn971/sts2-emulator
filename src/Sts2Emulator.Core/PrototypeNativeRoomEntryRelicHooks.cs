namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    internal static int RelicStrengthOnRoomEntry(
        PlayerState player, PrototypeRoomType room) =>
        room == PrototypeRoomType.Elite
            ? player.ActiveRelics.Sum(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .StrengthOnEliteRoomEntry)
            : 0;
}
