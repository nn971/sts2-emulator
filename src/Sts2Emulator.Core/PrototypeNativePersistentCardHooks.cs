using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Persistent deck-card hooks that run once as a newly chosen map point
/// is entered. Use the *visible* map-point type rather than its resolved
/// random room type: Dowsing counts question-mark map visits in native STS2.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private sealed record DowsingProgress(int RoomsEntered);

    private static PlayerState ApplyPersistentCardRoomEntry(
        PlayerState player,
        MapNodeState node)
    {
        if (node.RoomType != PrototypeRoomType.Unknown
            || !player.Deck.Any(card =>
                StringComparer.Ordinal.Equals(
                    card.CardId, "proto.native.neow.dowsing")))
        {
            return player;
        }

        var deck = player.Deck.Select(card =>
        {
            if (!StringComparer.Ordinal.Equals(
                    card.CardId, "proto.native.neow.dowsing"))
            {
                return card;
            }

            var entered = 0;
            if (card.PersistentState.ValueKind == JsonValueKind.Object
                && card.PersistentState.TryGetProperty(
                    nameof(DowsingProgress.RoomsEntered), out var value)
                && value.ValueKind == JsonValueKind.Number)
            {
                entered = value.GetInt32();
            }

            var next = entered + 1;
            if (next >= 5)
            {
                // The native transformation keeps this deck card as the
                // quest's replacement; no fresh reward-card roll occurs.
                return card with
                {
                    CardId = "proto.native.neow.abundance",
                    UpgradeLevel = 0,
                    PersistentState = PrototypeJson.EmptyObject(),
                    Enchantment = null
                };
            }

            return card with
            {
                PersistentState = JsonSerializer.SerializeToElement(
                    new DowsingProgress(next))
            };
        }).ToArray();

        return player with { Deck = deck };
    }
}
