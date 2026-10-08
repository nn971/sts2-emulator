using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Lava Rock's one-time Act 1 boss reward modifier. The pinned native
/// game adds two independent relic rewards, in addition to the normal
/// three-option boss relic reward; the prototype draws unowned unique
/// items from its existing ordinary relic pool.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private sealed record LavaRockProgress(bool HasTriggered);

    private static (PlayerState Player, string[] AdditionalRelicIds)
        ApplyLavaRockBossRewards(
            PlayerState player,
            PrototypeRoomType room,
            int act,
            string[]? bossRelicOptions,
            RngBundle rng)
    {
        if (room != PrototypeRoomType.Boss || act != 1)
        {
            return (player, []);
        }

        var lavaId = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "LavaRock");
        var index = Array.FindIndex(player.Relics, relic =>
            relic.RelicId == lavaId);
        if (index < 0)
        {
            return (player, []);
        }

        var existing = player.Relics[index].PersistentState;
        if (existing.ValueKind == JsonValueKind.Object
            && existing.TryGetProperty(
                nameof(LavaRockProgress.HasTriggered), out var value)
            && value.ValueKind == JsonValueKind.True)
        {
            return (player, []);
        }

        var excluded = new HashSet<string>(
            player.Relics.Select(relic => relic.RelicId),
            StringComparer.Ordinal);
        excluded.UnionWith(
            bossRelicOptions ?? Array.Empty<string>());
        var available = PrototypeContent.RelicPool
            .Where(id => !excluded.Contains(id)).ToArray();
        var selected = PickDistinct(
            available, Math.Min(2, available.Length),
            rng, "reward");

        var relics = (RelicInstance[])player.Relics.Clone();
        relics[index] = relics[index] with
        {
            PersistentState = JsonSerializer.SerializeToElement(
                new LavaRockProgress(true))
        };
        return (
            player with { Relics = relics },
            selected);
    }
}
