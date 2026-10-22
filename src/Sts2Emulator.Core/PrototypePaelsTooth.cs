namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    public const string PaelsToothCardReturnSiteId =
        "relic.paels_tooth.card_return.index_draw";

    internal static RunState ApplyPaelsToothAfterCombat(RunState state)
    {
        if (state.Player.Hp <= 0 || state.World is not { } world)
            return state;

        var player = state.Player;
        var nextCardId = world.NextCardInstanceId;
        var toothIndexes = player.Relics
            .Select((relic, index) => (Relic: relic, Index: index))
            .Where(item => !item.Relic.IsMelted
                && item.Relic.RelicId == NativePaelRewardsV111.ToothId)
            .Select(item => item.Index)
            .ToArray();

        foreach (var relicIndex in toothIndexes)
        {
            var saved = NativePaelRewardsV111.ToothCards(player.Relics[relicIndex]);
            if (saved.Length == 0)
                continue;

            var selectedIndex = LegacyRandomChoiceV111.ChooseIndexRange(
                state.Stochastic.PrototypeRng,
                PaelsToothCardReturnSiteId,
                "reward",
                $"player:0:relic:{relicIndex}:saved_cards:"
                    + CanonicalJson.Sha256(saved),
                saved.Length).SelectedIndex;
            var selected = saved[selectedIndex];
            player = NativePaelRewardsV111.RemoveToothCard(player, relicIndex, selectedIndex);
            var definition = PrototypeContent.Card(selected.CardId);
            var returned = selected with
            {
                InstanceId = nextCardId++,
                UpgradeLevel = selected.UpgradeLevel < definition.MaxUpgradeLevel
                    ? selected.UpgradeLevel + 1
                    : selected.UpgradeLevel,
                PersistentState = selected.PersistentState.Clone()
            };
            player = player with { Deck = player.Deck.Append(returned).ToArray() };
            player = ApplyRelicRunEvent(player, PrototypeRunEventKind.CardAdded,
                addedCard: returned, rng: state.Stochastic.PrototypeRng);
        }

        return state with
        {
            Player = player,
            World = world with { NextCardInstanceId = nextCardId }
        };
    }
}
