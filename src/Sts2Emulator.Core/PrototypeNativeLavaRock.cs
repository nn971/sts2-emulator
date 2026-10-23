using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Lava Rock's one-time Act 1 boss reward modifier. The pinned native
/// game adds two independent relic rewards, in addition to the normal
/// three-option boss relic reward. The source-shaped supported pool uses
/// the native player Rewards stream in configured runs and the legacy
/// reward stream otherwise; exact native pool order remains open.
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
            RngBundle rng,
            RngBundle? nativeRunRng = null)
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
        string[] selected;
        var count = Math.Min(2, available.Length);
        if (nativeRunRng is null)
        {
            selected = PickDistinct(available, count, rng, "reward");
        }
        else
        {
            var originalIdentities = RandomBoundaryJournalV111.HasActiveJournal
                ? (string[])available.Clone()
                : null;
            var decisions = NativeFisherYatesV111.ShuffleInPlace(
                available,
                nativeRunRng,
                new NativeRandomIndexProviderV111(),
                NativePlayerRngV111.RewardsStreamId,
                "run:act:1:boss:lava_rock_extra_relics",
                StochasticSiteRegistryV111
                    .RewardLavaRockRelicOfferShuffleSiteId);
            if (originalIdentities is not null)
                SemanticPoolShuffleChoiceV111.RecordNativeAudit(
                    originalIdentities, available, decisions, count,
                    SemanticPoolShuffleChoiceV111
                        .NativeLavaRockRelicOfferOperation,
                    null);
            selected = available.Take(count).ToArray();
        }

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
