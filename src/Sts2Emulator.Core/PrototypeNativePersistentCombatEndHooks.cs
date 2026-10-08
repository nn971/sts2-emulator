using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// Native-shaped persistent card and relic callbacks on a victorious
/// combat's completion. These update the run's persistent deck/relic
/// instances before the combat reward is generated.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private sealed record NativeCombatsSeen(int CombatsSeen);

    private static PlayerState ApplyNativePersistentCombatEnd(
        PlayerState player,
        PrototypeRoomType? roomType,
        RngBundle rng)
    {
        // Guilty counts every completed combat and leaves the deck
        // after the fifth combat, including Elite/Boss combats.
        var deck = player.Deck.Select(card =>
        {
            if (card.CardId != "proto.native.event.guilty")
            {
                return card;
            }

            var count = GetNativeCombatsSeen(card.PersistentState) + 1;
            return card with
            {
                PersistentState = JsonSerializer.SerializeToElement(
                    new NativeCombatsSeen(count))
            };
        }).Where(card =>
            card.CardId != "proto.native.event.guilty"
            || GetNativeCombatsSeen(card.PersistentState) < 5)
            .ToArray();

        player = player with { Deck = deck };

        // Fishing Rod counts only ordinary monster encounters. Every
        // third one upgrades one random currently upgradable deck card.
        if (roomType != PrototypeRoomType.Combat)
        {
            return player;
        }

        var rodId = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "FishingRod");
        var rodIndex = Array.FindIndex(player.Relics,
            relic => relic.RelicId == rodId);
        if (rodIndex < 0)
        {
            return player;
        }

        var relics = (RelicInstance[])player.Relics.Clone();
        var countAfterCombat =
            GetNativeCombatsSeen(relics[rodIndex].PersistentState) + 1;
        relics[rodIndex] = relics[rodIndex] with
        {
            PersistentState = JsonSerializer.SerializeToElement(
                new NativeCombatsSeen(countAfterCombat))
        };
        player = player with { Relics = relics };

        if (countAfterCombat % 3 != 0)
        {
            return player;
        }

        var candidates = player.Deck
            .Where(card =>
            {
                var definition = PrototypeContent.Card(card.CardId);
                return card.UpgradeLevel == 0
                    && !definition.Unplayable
                    && definition.Rarity is not
                        PrototypeCardRarity.Curse
                        and not PrototypeCardRarity.Status
                        and not PrototypeCardRarity.Quest;
            })
            .Select(card => card.InstanceId)
            .ToArray();

        if (candidates.Length == 0)
        {
            return player;
        }

        var selected = candidates[
            PrototypeRng.NextInt(rng, "event", candidates.Length)];
        return player with
        {
            Deck = player.Deck.Select(card =>
                card.InstanceId == selected
                    ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                    : card).ToArray()
        };
    }

    private static int GetNativeCombatsSeen(JsonElement state) =>
        state.ValueKind == JsonValueKind.Object
        && state.TryGetProperty(
            nameof(NativeCombatsSeen.CombatsSeen), out var count)
        && count.ValueKind == JsonValueKind.Number
            ? count.GetInt32()
            : 0;
}
