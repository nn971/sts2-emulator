using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Silver Crucible's native once-per-card-reward-group upgrade and
/// once-per-treasure-room suppression lifecycle. Reward option metadata
/// records the upgrade before the player makes a choice.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private sealed record SilverCrucibleProgress(
        int TimesUsed,
        int TreasureRoomsEntered);

    private static int FindSilverCrucibleIndex(PlayerState player)
    {
        var id = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "SilverCrucible");
        return Array.FindIndex(player.Relics,
            relic => relic.RelicId == id);
    }

    private static SilverCrucibleProgress ReadSilverCrucibleProgress(
        JsonElement persistentState)
    {
        if (persistentState.ValueKind != JsonValueKind.Object)
        {
            return new SilverCrucibleProgress(0, 0);
        }

        var times = persistentState.TryGetProperty(
            nameof(SilverCrucibleProgress.TimesUsed), out var used)
            && used.ValueKind == JsonValueKind.Number
            ? used.GetInt32() : 0;
        var treasure = persistentState.TryGetProperty(
            nameof(SilverCrucibleProgress.TreasureRoomsEntered),
            out var entered)
            && entered.ValueKind == JsonValueKind.Number
            ? entered.GetInt32() : 0;
        return new SilverCrucibleProgress(times, treasure);
    }

    private static PlayerState WriteSilverCrucibleProgress(
        PlayerState player, int index, SilverCrucibleProgress progress)
    {
        var relics = (RelicInstance[])player.Relics.Clone();
        relics[index] = relics[index] with
        {
            PersistentState = JsonSerializer.SerializeToElement(progress)
        };
        return player with { Relics = relics };
    }

    private static (PlayerState Player, bool[] UpgradeGroups)
        ApplySilverCrucibleToCardRewardGeneration(
            PlayerState player, int groupCount)
    {
        var upgraded = new bool[groupCount];
        var index = FindSilverCrucibleIndex(player);
        if (index < 0)
        {
            return (player, upgraded);
        }

        var progress = ReadSilverCrucibleProgress(
            player.Relics[index].PersistentState);
        for (var group = 0; group < groupCount; group++)
        {
            if (progress.TimesUsed >= 3)
            {
                break;
            }

            upgraded[group] = true;
            progress = progress with
            {
                TimesUsed = progress.TimesUsed + 1
            };
        }

        return (
            WriteSilverCrucibleProgress(player, index, progress),
            upgraded);
    }

    private static (PlayerState Player, bool SkipTreasure)
        ApplySilverCrucibleTreasureEntry(PlayerState player)
    {
        var index = FindSilverCrucibleIndex(player);
        if (index < 0)
        {
            return (player, false);
        }

        var progress = ReadSilverCrucibleProgress(
            player.Relics[index].PersistentState);
        progress = progress with
        {
            TreasureRoomsEntered = progress.TreasureRoomsEntered + 1
        };
        return (
            WriteSilverCrucibleProgress(player, index, progress),
            progress.TreasureRoomsEntered == 1);
    }

    private static PlayerState AppendNativeCardReward(
        PlayerState player,
        long instanceId,
        string cardId,
        bool upgraded,
        RngBundle rng)
    {
        player = AppendCard(player, instanceId, cardId, rng);
        if (!upgraded)
        {
            return player;
        }

        return player with
        {
            Deck = player.Deck.Select(card =>
                card.InstanceId == instanceId
                    ? card with
                    {
                        UpgradeLevel = Math.Max(1, card.UpgradeLevel)
                    }
                    : card).ToArray()
        };
    }
}
