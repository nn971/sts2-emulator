namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    private static RunState ClonePaelRestCards(RunState state)
    {
        if (!state.Player.ActiveRelics.Any(relic => relic.RelicId == NativePaelRewardsV111.GrowthId))
            throw new InvalidOperationException("Clone rest requires an active Pael's Growth.");
        var world = RequireWorld(state);
        var player = state.Player;
        var prototypeRng = state.Stochastic.PrototypeRng;
        var nextId = world.NextCardInstanceId;
        // Snapshot the original pile; newly added copies are for future rests.
        foreach (var original in player.Deck.Where(card =>
            card.Enchantment?.Kind == PrototypeCardEnchantmentKind.Clone).ToArray())
        {
            var definition = PrototypeContent.Card(original.CardId);
            var upgrade = original.UpgradeLevel;
            if (upgrade == 0 && definition.MaxUpgradeLevel > 0 && player.ActiveRelics.Any(relic =>
                PrototypeContent.Relic(relic.RelicId).UpgradeAddedCardType == definition.Type))
                upgrade = 1;
            var card = ApplyNewCardEnchantments(player, original with
            { InstanceId = nextId++, UpgradeLevel = upgrade, PersistentState = original.PersistentState.Clone() });
            player = player with { Deck = [..player.Deck, card] };
            player = ApplyRelicRunEvent(player, PrototypeRunEventKind.CardAdded, addedCard: card, rng: prototypeRng);
        }
        return CompleteRoomToMap(state with
        {
            Player = player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { NextCardInstanceId = nextId }
        });
    }
}
