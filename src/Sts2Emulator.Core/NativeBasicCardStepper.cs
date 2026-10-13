using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// A deliberately restricted independent rule projection from a recorded native
/// combat *pre-state*. This is NOT a complete game engine or fidelity verdict:
/// relic hooks, card enchantments, pending effects, and RNG streams are absent
/// from the current native snapshot. None of the predictions are copied from
/// the observed native post-state.
/// </summary>
public sealed record NativeBasicCardProjection(
    string Status, string Reason, string? CardModel, long? CardInstanceId,
    string? TargetId, int? Energy, int? PlayerBlock, int? TargetHp,
    int? TargetBlock, int? HandCount, int? DiscardCount,
    string[] UnverifiedContinuation)
{
    public static NativeBasicCardProjection Unsupported(string reason) =>
        new("UNSUPPORTED", reason, null, null, null,
            null, null, null, null, null, null, Array.Empty<string>());
}

public static class NativeBasicCardStepper
{
    /// <summary>
    /// Independently models the documented *basic effect* of Silent Strike
    /// (1 energy, deal 6/9) and Defend (1 energy, gain 5/8 block).
    /// This is a rule-level projection, not an audited full native replay.
    /// </summary>
    public static NativeBasicCardProjection Project(
        NativeCombatSnapshot before, GameAction action)
    {
        if (action.Kind != "play_card")
            return NativeBasicCardProjection.Unsupported("Only ordinary play_card supported");
        if (action.Payload.ValueKind != JsonValueKind.Object ||
            !action.Payload.TryGetProperty("card_instance_id", out var idValue) ||
            idValue.ValueKind != JsonValueKind.Number ||
            !idValue.TryGetInt64(out long cardId))
            return NativeBasicCardProjection.Unsupported("Missing or invalid card instance");
        var cards = before.Hand.Where(c => c.InstanceId == cardId).ToArray();
        if (cards.Length != 1)
            return NativeBasicCardProjection.Unsupported("Selected card is not unique in hand");
        var card = cards[0];
        string model = card.ModelId.ToUpperInvariant();
        bool strike = model == "STRIKE_SILENT";
        bool defend = model == "DEFEND_SILENT";
        if (!strike && !defend)
            return NativeBasicCardProjection.Unsupported("Only STRIKE_SILENT and DEFEND_SILENT supported");
        if (card.UpgradeLevel is < 0 or > 1)
            return NativeBasicCardProjection.Unsupported("Card upgrade level unsupported");
        if (card.Cost != 1 || before.Energy < card.Cost)
            return NativeBasicCardProjection.Unsupported("Unmodified 1-energy card play required");
        if (!NoPowers(before.PlayerPowers) || before.Creatures.Any(c => !NoPowers(c.Powers)))
            return NativeBasicCardProjection.Unsupported("Powers may modify card effects");
        if (before.DiscardPile.Any(c => c.InstanceId == cardId) ||
            before.DrawPile.Any(c => c.InstanceId == cardId) ||
            before.ExhaustPile.Any(c => c.InstanceId == cardId))
            return NativeBasicCardProjection.Unsupported("Card instance duplicated in zones");

        string? targetId = null;
        int? targetHp = null;
        int? targetBlock = null;
        int? playerBlock = null;
        if (strike)
        {
            if (card.TargetType != "AnyEnemy")
                return NativeBasicCardProjection.Unsupported("Strike target type changed");
            if (!action.Payload.TryGetProperty("target_id", out var targetValue) ||
                targetValue.ValueKind != JsonValueKind.String)
                return NativeBasicCardProjection.Unsupported("Target required for Strike");
            targetId = targetValue.GetString();
            var targets = before.Creatures.Where(c => c.InstanceId == targetId).ToArray();
            if (targets.Length != 1 || !targets[0].Alive)
                return NativeBasicCardProjection.Unsupported("Unique live enemy target required");
            var target = targets[0];
            if (target.Block != 0)
                return NativeBasicCardProjection.Unsupported("Target block resolution not in initial slice");
            int damage = 6 + 3 * card.UpgradeLevel;
            if (target.Hp <= damage)
                return NativeBasicCardProjection.Unsupported("Possible lethal hit and end-combat hooks");
            targetHp = target.Hp - damage;
            targetBlock = target.Block;
        }
        else
        {
            if (card.TargetType != "Self")
                return NativeBasicCardProjection.Unsupported("Defend target type changed");
            if (action.Payload.TryGetProperty("target_id", out var targetValue) &&
                targetValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                return NativeBasicCardProjection.Unsupported("Defend cannot target an enemy");
            if (before.Block > int.MaxValue - (5 + 3 * card.UpgradeLevel))
                return NativeBasicCardProjection.Unsupported("Block overflow");
            playerBlock = before.Block + 5 + 3 * card.UpgradeLevel;
        }
        return new NativeBasicCardProjection(
            "RULE_PROJECTION_ONLY",
            "Independent unmodified basic card effect only; no full combat continuation",
            model, cardId, targetId,
            before.Energy - 1, playerBlock, targetHp, targetBlock,
            before.Hand.Length - 1, before.DiscardPile.Length + 1,
            [
                "CARD_ENCHANTMENTS_UNOBSERVED",
                "RELIC_AND_POTION_EFFECTS_UNOBSERVED",
                "PENDING_EFFECTS_UNOBSERVED",
                "DAMAGE_HOOK_ORDER_UNVERIFIED",
                "RNG_CONTINUATION_UNOBSERVED"
            ]);
    }

    private static bool NoPowers(JsonElement powers) =>
        powers.ValueKind == JsonValueKind.Array && powers.GetArrayLength() == 0;
}
