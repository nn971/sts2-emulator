using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Neow's Winged Boots permit three map travels to any point in the next
/// row. A use is spent only when the selected point is not a child of the
/// previously visited point.
/// Reference: Flaaax/STS2-source@706ef1c9, WingedBoots.cs and MapTravel.cs.
/// </summary>
public static class PrototypeNativeWingedBoots
{
    public const int MaximumUses = 3;
    public const string RelicId = "proto.native.neow.wingedboots";

    private sealed record Progress(int TimesUsed);

    public static int TimesUsed(RelicInstance relic)
    {
        var state = relic.PersistentState;
        return state.ValueKind == JsonValueKind.Object
            && state.TryGetProperty(nameof(Progress.TimesUsed), out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var timesUsed)
                ? timesUsed
                : 0;
    }

    public static bool HasFreeTravel(PlayerState player) =>
        player.ActiveRelics.Any(relic => relic.RelicId == RelicId
            && TimesUsed(relic) < MaximumUses);

    public static MapNodeState[] AvailableNodes(
        RunWorldState world, PlayerState player)
    {
        if (!HasFreeTravel(player)
            || world.Map.CurrentNodeId is not { } currentNodeId)
        {
            return world.Map.AvailableNodes();
        }

        var current = world.Map.Nodes.FirstOrDefault(node =>
            StringComparer.Ordinal.Equals(node.NodeId, currentNodeId))
            ?? throw new InvalidOperationException(
                "Winged Boots cannot find the current map point.");
        return world.Map.Nodes
            .Where(node => node.Act == current.Act
                && node.Floor == current.Floor + 1)
            .ToArray();
    }

    public static PlayerState ConsumeIfSkippingEdge(
        PlayerState player, MapState map, string chosenNodeId)
    {
        if (map.CurrentNodeId is not { } previousNodeId)
            return player;

        var previous = map.Nodes.FirstOrDefault(node =>
            StringComparer.Ordinal.Equals(node.NodeId, previousNodeId))
            ?? throw new InvalidOperationException(
                "Winged Boots cannot find the previous map point.");
        if ((previous.NextNodeIds ?? Array.Empty<string>()).Contains(
                chosenNodeId, StringComparer.Ordinal))
        {
            return player;
        }

        var relicIndex = Array.FindIndex(player.Relics, relic =>
            relic.RelicId == RelicId
            && !relic.IsMelted
            && TimesUsed(relic) < MaximumUses);
        if (relicIndex < 0)
        {
            throw new InvalidOperationException(
                "A map travel skipped an edge without an available Winged Boots use.");
        }

        var relics = (RelicInstance[])player.Relics.Clone();
        relics[relicIndex] = relics[relicIndex] with
        {
            PersistentState = JsonSerializer.SerializeToElement(
                new Progress(TimesUsed(relics[relicIndex]) + 1))
        };
        return player with { Relics = relics };
    }

    public static bool HasValidState(RelicInstance relic)
    {
        var state = relic.PersistentState;
        if (state.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return true;
        if (state.ValueKind != JsonValueKind.Object
            || !state.TryGetProperty(nameof(Progress.TimesUsed), out var value))
            return state.ValueKind == JsonValueKind.Object;
        return value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var timesUsed)
            && timesUsed is >= 0 and <= MaximumUses;
    }
}
