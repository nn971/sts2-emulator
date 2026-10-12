using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111PaelsToothTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void AcquisitionRemovesFiveUpgradableCardsAndStoresTheirFullInstances()
    {
        var state = NativePaelRewardsV111.CreateRewardFixture("paels-tooth-acquisition",
            NativePaelRewardsV111.ToothId);
        var acquire = Assert.Single(Engine.GetLegalActions(state));
        state = Engine.Step(state, acquire).State;
        var pending = state.World!.Event!.PendingDeckChoice!;
        Assert.Equal(5, pending.RemainingSelections);
        Assert.True(pending.CandidateCardInstanceIds.Length >= 5);
        Assert.All(pending.CandidateCardInstanceIds, id =>
        {
            var card = state.Player.Deck.Single(card => card.InstanceId == id);
            Assert.True(card.UpgradeLevel < PrototypeContent.Card(card.CardId).MaxUpgradeLevel);
        });
        PrototypeStateInvariants.Validate(state);

        var selectedCards = new List<CardInstance>();
        for (var i = 0; i < 5; i++)
        {
            var action = Engine.GetLegalActions(state).First(action => action.Kind == "choose_event_deck_card");
            var id = action.ReadPayload<ChooseDeckCardPayload>().CardInstanceId;
            selectedCards.Add(state.Player.Deck.Single(card => card.InstanceId == id));
            var next = Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State;
            Assert.Equal(CanonicalJson.Sha256(next), CanonicalJson.Sha256(Engine.Step(state, action).State));
            state = next;
        }

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        var tooth = state.Player.Relics.Single(relic => relic.RelicId == NativePaelRewardsV111.ToothId);
        Assert.Equal(selectedCards.OrderBy(card => card.CardId, StringComparer.Ordinal)
                .Select(card => card.InstanceId), NativePaelRewardsV111.ToothCards(tooth)
                .OrderBy(card => card.CardId, StringComparer.Ordinal).Select(card => card.InstanceId));
        Assert.All(selectedCards, card => Assert.DoesNotContain(state.Player.Deck, remaining => remaining.InstanceId == card.InstanceId));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void SurvivingCombatReturnsOneSavedCardUpgradedAndConsumesItsRecord()
    {
        var state = NativePaelRewardsV111.CreateRewardFixture("paels-tooth-combat",
            NativePaelRewardsV111.ToothId);
        state = Engine.Step(state, Assert.Single(Engine.GetLegalActions(state))).State;
        for (var i = 0; i < 5; i++)
        {
            var action = Engine.GetLegalActions(state).First(action => action.Kind == "choose_event_deck_card");
            state = Engine.Step(state, action).State;
        }

        var beforeRelic = state.Player.Relics.Single(relic => relic.RelicId == NativePaelRewardsV111.ToothId);
        var beforeCards = NativePaelRewardsV111.ToothCards(beforeRelic);
        var expectedSource = beforeCards[PrototypeRng.NextInt(state.Rng.Fork(), "reward", beforeCards.Length)];
        var beforeRewardCalls = state.Rng.Streams.Single(stream => stream.StreamId == "reward").CallCount;
        var returnedState = NativeAncientRewardsV111.ApplyCombatEnd(state with
        {
            DecisionIndex = state.DecisionIndex + 1,
            World = state.World! with { LastCombatEndDecisionIndex = null }
        });

        var afterRelic = returnedState.Player.Relics.Single(relic => relic.RelicId == NativePaelRewardsV111.ToothId);
        var afterCards = NativePaelRewardsV111.ToothCards(afterRelic);
        var returned = Assert.Single(returnedState.Player.Deck, card =>
            card.InstanceId >= state.World!.NextCardInstanceId);
        var source = expectedSource;
        Assert.Equal(source.CardId, returned.CardId);
        Assert.Equal(source.UpgradeLevel + 1, returned.UpgradeLevel);
        Assert.Equal(4, afterCards.Length);
        Assert.DoesNotContain(afterCards, card => card.InstanceId == source.InstanceId);
        Assert.Equal(source.Enchantment, returned.Enchantment);
        Assert.Equal(source.PersistentState.GetRawText(), returned.PersistentState.GetRawText());
        Assert.Equal(beforeRewardCalls + 1,
            returnedState.Rng.Streams.Single(stream => stream.StreamId == "reward").CallCount);
        Assert.Equal(returnedState.DecisionIndex, returnedState.World!.LastCombatEndDecisionIndex);
        PrototypeStateInvariants.Validate(returnedState);
        Assert.Equal(CanonicalJson.Sha256(returnedState), CanonicalJson.Sha256(
            RunSnapshot.Load(RunSnapshot.Save(returnedState))));
    }
}
