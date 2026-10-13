using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111TanxEventTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void OffersThreeRelicsFromTheConditionalPoolUsingNativeEventShuffle()
    {
        const string seed = "tanx-offer-shuffle";
        var opening = PrototypeNativeLaterActFixture.Create(seed, 3);
        var deck = new[]
        {
            new CardInstance(1, "proto.silent.strike", 0,
                PrototypeJson.EmptyObject()),
            new CardInstance(2, "proto.silent.shiv", 0,
                PrototypeJson.EmptyObject()),
            new CardInstance(3, "proto.silent.defend", 0,
                PrototypeJson.EmptyObject())
        };
        opening = opening with
        {
            Player = opening.Player with { Deck = deck }
        };

        var stream = NativeRngV111Contexts.ForEvent(seed, "TANX");
        var bundle = new RngBundle([stream]);
        var pool = new[]
        {
            NativeGloryAncientsV111.ClawsId,
            NativeGloryAncientsV111.CrossbowId,
            NativeGloryAncientsV111.IronClubId,
            NativeGloryAncientsV111.MeatCleaverId,
            NativeGloryAncientsV111.SaiId,
            NativeGloryAncientsV111.SpikedGauntletsId,
            NativeGloryAncientsV111.TanxsWhistleId,
            NativeGloryAncientsV111.ThrowingAxeId,
            NativeGloryAncientsV111.WarHammerId
        };
        NativeRngV111.Shuffle(bundle, stream.StreamId, pool);

        var state = NativeTanxEventV111.BeginOpening(opening);

        Assert.Equal(pool.Take(3).Select(id => "take_" + id),
            state.World!.Event!.OfferedChoiceIds);
        Assert.Equal(8, NativeRngV111.ToSnapshot(
            state.World.Event.NativeEventRng!).Counter);
        Assert.Equal(NativeRngV111.ToSnapshot(bundle.Streams[0]),
            NativeRngV111.ToSnapshot(state.World.Event.NativeEventRng!));
        Assert.Equal(state.World.Event.OfferedChoiceIds,
            Engine.GetLegalActions(state).Select(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId));
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(
            RunSnapshot.Load(RunSnapshot.Save(state))));
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeStateInvariants.Validate(state with
            {
                World = state.World with
                {
                    Event = state.World.Event with { NativeEventRng = null }
                }
            }));
    }

    [Fact]
    public void TriBoomerangEventBranchStartsAndResolvesItsThreeCardChoice()
    {
        RunState? selectedOpening = null;
        for (var i = 0; i < 250; i++)
        {
            var candidate = PrototypeNativeLaterActFixture.Create(
                "tanx-tri-event-branch-" + i, 3);
            candidate = candidate with
            {
                Player = candidate.Player with
                {
                    Deck =
                    [
                        new CardInstance(1, "proto.silent.strike", 0,
                            PrototypeJson.EmptyObject()),
                        new CardInstance(2, "proto.silent.shiv", 0,
                            PrototypeJson.EmptyObject()),
                        new CardInstance(3, "proto.silent.shiv", 0,
                            PrototypeJson.EmptyObject())
                    ]
                },
                World = candidate.World! with { NextCardInstanceId = 4 }
            };
            candidate = NativeTanxEventV111.BeginOpening(candidate);
            if (candidate.World!.Event!.OfferedChoiceIds!.Contains(
                    "take_" + NativeGloryAncientsV111.TriBoomerangId,
                    StringComparer.Ordinal))
            {
                selectedOpening = candidate;
                break;
            }
        }
        var state = Assert.IsType<RunState>(selectedOpening);
        var triChoice = Engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + NativeGloryAncientsV111.TriBoomerangId);
        state = Engine.Step(state, triChoice).State;
        Assert.Equal(3, state.World!.Event!.PendingDeckChoice!.RemainingSelections);
        Assert.Equal(3, state.World.Event.PendingDeckChoice.MinimumSelectionsRemaining);
        state = RunSnapshot.Load(RunSnapshot.Save(state));

        foreach (var cardId in new long[] { 1, 2, 3 })
        {
            var choose = Engine.GetLegalActions(state).Single(action =>
                action.Kind == "choose_event_deck_card"
                && action.ReadPayload<ChooseEventDeckCardPayload>()
                    .CardInstanceId == cardId);
            state = Engine.Step(state, choose).State;
            state = RunSnapshot.Load(RunSnapshot.Save(state));
        }

        Assert.Null(state.World!.Event);
        Assert.Equal(3, state.Player.Deck.Count(card =>
            card.Enchantment?.Kind == PrototypeCardEnchantmentKind.Instinct));
        Assert.Equal(RunPhase.MapChoice, state.Phase);
    }

    [Fact]
    public void TriBoomerangJoinsThePoolOnlyWithThreeEligibleAttackCards()
    {
        var state = PrototypeNativeLaterActFixture.Create(
            "tanx-tri-condition", 3);
        var attacks = state.Player.Deck.Where(card =>
            PrototypeContent.Card(card.CardId).Type == PrototypeCardType.Attack)
            .Take(2).ToArray();
        state = state with
        {
            Player = state.Player with { Deck = attacks }
        };
        Assert.DoesNotContain("take_" + NativeGloryAncientsV111.TriBoomerangId,
            NativeTanxEventV111.BeginOpening(state).World!.Event!.OfferedChoiceIds!);

        var threeEligible = PrototypeNativeLaterActFixture.Create(
            "tanx-tri-condition-eligible", 3);
        var eligibleDeck = new[]
        {
            new CardInstance(1, "proto.silent.strike", 0,
                PrototypeJson.EmptyObject()),
            new CardInstance(2, "proto.silent.shiv", 0,
                PrototypeJson.EmptyObject()),
            new CardInstance(3, "proto.silent.shiv", 0,
                PrototypeJson.EmptyObject())
        };
        threeEligible = threeEligible with
        { Player = threeEligible.Player with { Deck = eligibleDeck } };
        var offered = NativeTanxEventV111.BeginOpening(threeEligible);
        var eligibleCount = threeEligible.Player.Deck.Count(card =>
            PrototypeContent.Card(card.CardId).Type == PrototypeCardType.Attack
            && PrototypeGameEngine.CanSelectEventDeckCard(card,
                PrototypePersistentDeckChoiceKind.Enchant, null,
                PrototypeCardEnchantmentKind.Instinct, false));
        Assert.True(eligibleCount >= 3);
        var tri = NativeGloryAncientsV111.TriBoomerangId;
        var eligiblePool = new[]
        {
            NativeGloryAncientsV111.ClawsId,
            NativeGloryAncientsV111.CrossbowId,
            NativeGloryAncientsV111.IronClubId,
            NativeGloryAncientsV111.MeatCleaverId,
            NativeGloryAncientsV111.SaiId,
            NativeGloryAncientsV111.SpikedGauntletsId,
            NativeGloryAncientsV111.TanxsWhistleId,
            NativeGloryAncientsV111.ThrowingAxeId,
            NativeGloryAncientsV111.WarHammerId,
            tri
        };
        var expectedStream = NativeRngV111Contexts.ForEvent(
            threeEligible.RunSeed, "TANX");
        var expectedBundle = new RngBundle([expectedStream]);
        NativeRngV111.Shuffle(expectedBundle, expectedStream.StreamId,
            eligiblePool);
        Assert.Equal(eligiblePool.Take(3).Select(id => "take_" + id),
            offered.World!.Event!.OfferedChoiceIds);
        Assert.Equal(9, NativeRngV111.ToSnapshot(
            offered.World.Event.NativeEventRng!).Counter);
        Assert.False(ExperimentalSilentThreeActRouteV111.ContainsEvent(
            NativeTanxEventV111.EventId, 3));
    }

    [Fact]
    public void ClawsEventAcquisitionCanTransformZeroOrMoreCardsAndPreservesMetadata()
    {
        RunState? selectedOpening = null;
        for (var i = 0; i < 200; i++)
        {
            var candidate = PrototypeNativeLaterActFixture.Create(
                "tanx-claws-flow-" + i, 3);
            candidate = candidate with
            {
                Player = candidate.Player with
                {
                    Deck =
                    [
                        new CardInstance(1, "proto.silent.strike", 1,
                            PrototypeJson.EmptyObject(),
                            new(PrototypeCardEnchantmentKind.Instinct)),
                        new CardInstance(2, "proto.silent.defend", 0,
                            PrototypeJson.EmptyObject())
                    ]
                },
                World = candidate.World! with { NextCardInstanceId = 3 }
            };
            candidate = NativeTanxEventV111.BeginOpening(candidate);
            if (candidate.World!.Event!.OfferedChoiceIds!.Contains(
                    "take_" + NativeGloryAncientsV111.ClawsId,
                    StringComparer.Ordinal))
            {
                selectedOpening = candidate;
                break;
            }
        }
        var state = Assert.IsType<RunState>(selectedOpening);

        var chooseClaws = Engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + NativeGloryAncientsV111.ClawsId);
        state = Engine.Step(state, chooseClaws).State;
        var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            state.World!.Event!.PendingDeckChoice);
        Assert.Equal(0, pending.MinimumSelectionsRemaining);
        Assert.Contains(Engine.GetLegalActions(state), action =>
            action.Kind == "finish_relic_deck_choice");

        var transformed = Engine.GetLegalActions(state).Single(action =>
            action.Kind == "choose_event_deck_card"
            && action.ReadPayload<ChooseEventDeckCardPayload>()
                .CardInstanceId == 1);
        state = Engine.Step(state, transformed).State;
        var maul = Assert.Single(state.Player.Deck, card =>
            card.CardId == NativeGloryAncientsV111.MaulCardId);
        Assert.Equal(NativeGloryAncientsV111.MaulCardId, maul.CardId);
        Assert.Equal(1, maul.UpgradeLevel);
        Assert.Equal(PrototypeCardEnchantmentKind.Instinct,
            maul.Enchantment!.Kind);
        var finish = Engine.GetLegalActions(state).Single(action =>
            action.Kind == "finish_relic_deck_choice");
        state = Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), finish).State;
        Assert.Null(state.World!.Event);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
    }
}
