using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Silken Tress enchants every eligible card in the first generated card
/// reward, then keeps that one-shot state on its relic instance.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private sealed record SilkenTressProgress(bool HasTriggered);

    private static (PlayerState Player, bool EnchantFirstCardReward)
        ConsumeSilkenTressCardReward(PlayerState player, bool hasCardOptions)
    {
        if (!hasCardOptions)
        {
            return (player, false);
        }

        var relicId = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "SilkenTress");
        var relicIndex = Array.FindIndex(player.Relics,
            relic => relic.RelicId == relicId);
        if (relicIndex < 0)
        {
            return (player, false);
        }

        var relic = player.Relics[relicIndex];
        var hasTriggered = relic.PersistentState.ValueKind
                == JsonValueKind.Object
            && relic.PersistentState.TryGetProperty(
                nameof(SilkenTressProgress.HasTriggered), out var value)
            && value.ValueKind == JsonValueKind.True;
        if (hasTriggered)
        {
            return (player, false);
        }

        var relics = (RelicInstance[])player.Relics.Clone();
        relics[relicIndex] = relic with
        {
            PersistentState = JsonSerializer.SerializeToElement(
                new SilkenTressProgress(true))
        };
        return (player with { Relics = relics }, true);
    }
}
