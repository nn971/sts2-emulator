using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private const string LizardTailUsedProperty = "WasUsed";

    internal static PlayerState ApplyPlayerHpLossAndDeathPrevention(
        PlayerState player, int amount)
    {
        var hpLoss = ReducePlayerHpLoss(player, amount);
        player = player with
        {
            Hp = Math.Max(0, player.Hp - hpLoss)
        };
        return player.Hp <= 0 ? TryUseLizardTail(player) : player;
    }

    internal static PlayerState TryUseLizardTail(PlayerState player)
    {
        if (player.Hp > 0)
            return player;

        var relics = player.Relics.ToArray();
        for (var index = 0; index < relics.Length; index++)
        {
            var relic = relics[index];
            if (relic.RelicId != PrototypeNativeSharedRelics.LizardTailId
                || relic.IsMelted
                || IsLizardTailUsed(relic))
            {
                continue;
            }

            var persistent = relic.PersistentState.ValueKind
                == JsonValueKind.Object
                ? JsonNode.Parse(relic.PersistentState.GetRawText())
                    as JsonObject ?? new JsonObject()
                : new JsonObject();
            persistent[LizardTailUsedProperty] = true;
            relics[index] = relic with
            {
                PersistentState = JsonSerializer.SerializeToElement(
                    persistent)
            };
            var healedHp = Math.Max(1, player.MaxHp * 50 / 100);
            return player with
            {
                Hp = Math.Min(player.MaxHp, healedHp),
                Relics = relics
            };
        }

        return player;
    }

    internal static bool IsLizardTailUsed(RelicInstance relic) =>
        relic.PersistentState.ValueKind == JsonValueKind.Object
        && relic.PersistentState.TryGetProperty(
            LizardTailUsedProperty, out var used)
        && used.ValueKind == JsonValueKind.True;
}
