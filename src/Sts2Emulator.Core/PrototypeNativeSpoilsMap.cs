using System.Text.Json;

namespace Sts2Emulator.Core;

public static class PrototypeNativeSpoilsMap
{
    public const string CardId = "proto.native.event.spoils_map";

    internal sealed record PersistentState(
        int SpoilsActIndex,
        string? SpoilsNodeId);

    public static JsonElement InitialPersistentState() =>
        JsonSerializer.SerializeToElement(
            new PersistentState(SpoilsActIndex: 1, SpoilsNodeId: null));

    internal static PersistentState? ReadPersistentState(
        CardInstance card)
    {
        if (card.PersistentState.ValueKind != JsonValueKind.Object
            || !card.PersistentState.TryGetProperty(
                nameof(PersistentState.SpoilsActIndex), out var actIndex)
            || actIndex.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var nodeId = card.PersistentState.TryGetProperty(
                nameof(PersistentState.SpoilsNodeId), out var node)
            && node.ValueKind == JsonValueKind.String
                ? node.GetString()
                : null;
        return new PersistentState(actIndex.GetInt32(), nodeId);
    }
}

public sealed partial class PrototypeGameEngine
{
    internal static (PlayerState Player, MapState Map)
        AssignSpoilsMapQuestToGeneratedMap(
            PlayerState player,
            MapState map,
            int act)
    {
        var treasure = map.Nodes.FirstOrDefault(node =>
            node.RoomType == PrototypeRoomType.Treasure);
        if (treasure is null)
            return (player, map);

        var deck = player.Deck.Select(card =>
        {
            if (card.CardId != PrototypeNativeSpoilsMap.CardId)
                return card;

            var saved = PrototypeNativeSpoilsMap.ReadPersistentState(card)
                ?? new PrototypeNativeSpoilsMap.PersistentState(1, null);
            if (saved.SpoilsActIndex != act - 1)
                return card;

            return card with
            {
                PersistentState = JsonSerializer.SerializeToElement(
                    saved with { SpoilsNodeId = treasure.NodeId })
            };
        }).ToArray();
        return (player with { Deck = deck }, map);
    }

    internal static PlayerState CompleteSpoilsMapQuestAtNode(
        PlayerState player,
        MapNodeState node)
    {
        if (node.RoomType != PrototypeRoomType.Treasure)
            return player;

        var completed = player.Deck.Where(card =>
        {
            if (card.CardId != PrototypeNativeSpoilsMap.CardId)
                return false;
            var saved = PrototypeNativeSpoilsMap.ReadPersistentState(card);
            return saved?.SpoilsActIndex == node.Act - 1
                && saved.SpoilsNodeId == node.NodeId;
        }).Select(card => card.InstanceId).ToHashSet();
        if (completed.Count == 0)
            return player;

        var gold = player.Gold;
        foreach (var _ in completed)
            gold = GoldAfterGain(player with { Gold = gold }, 600);
        return player with
        {
            Gold = gold,
            Deck = player.Deck.Where(card =>
                !completed.Contains(card.InstanceId)).ToArray()
        };
    }
}
