using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeNeowAdditionalTests
{
    private static string OptionId(string name) =>
        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(name);

    private static RunState Offer(string name, string seed)
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed);
        var eventState = state.World!.Event!;
        var otherPositive = name == "ArcaneScroll"
            ? "BoomingConch" : "ArcaneScroll";
        var otherCurse = name == "CursedPearl"
            ? "DowsingRod" : "CursedPearl";
        return state with
        {
            World = state.World with
            {
                Event = eventState with
                {
                    OfferedChoiceIds =
                    [
                        OptionId(name),
                        OptionId(otherPositive),
                        OptionId(otherCurse)
                    ]
                }
            }
        };
    }

    private static RunState Pick(RunState state, string name)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(choice =>
            choice.Kind == "event_choice"
            && choice.ReadPayload<EventChoicePayload>().ChoiceId == OptionId(name));
        return engine.Step(state, action).State;
    }

    [Theory]
    [InlineData("NeowsTorment", "proto.native.neow.neows_fury",
        PrototypeCardType.Attack)]
    [InlineData("DowsingRod", "proto.native.neow.dowsing",
        PrototypeCardType.Quest)]
    public void SourceBackedNeowAcquisitionAddsOneCard(
        string name, string cardId, PrototypeCardType expectedType)
    {
        var state = Offer(name, "neow-card-" + name);
        var deck = state.Player.Deck;
        state = Pick(state, name);

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(14, state.Player.Deck.Length);
        Assert.Equal(deck.Select(c => c.InstanceId),
            state.Player.Deck.Take(13).Select(c => c.InstanceId));
        Assert.Equal(deck.Select(c => c.CardId),
            state.Player.Deck.Take(13).Select(c => c.CardId));
        Assert.Equal(cardId, state.Player.Deck[^1].CardId);
        Assert.Equal(14, state.Player.Deck[^1].InstanceId);
        Assert.Equal(15, state.World!.NextCardInstanceId);
        Assert.Equal(expectedType, PrototypeContent.Card(cardId).Type);
        Assert.False(PrototypeContent.Card(cardId).RewardEligible);
        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == PrototypeNativeOvergrowthEvents.NeowRelicId(name));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void PrecariousShearsRemovesTwoCardsAndCostsSixteenHp()
    {
        var state = Offer("PrecariousShears", "neow-shears");
        var before = state.Player.Deck;
        state = Pick(state, "PrecariousShears");
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(70, state.Player.Hp);
        Assert.Equal(70, state.Player.MaxHp);
        Assert.Equal(16, state.World!.Event!.DeferredHpLoss);
        var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            state.World!.Event!.PendingDeckChoice);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Remove, pending.Kind);
        Assert.Equal(2, pending.RemainingSelections);
        Assert.Equal(13, pending.CandidateCardInstanceIds.Length);
        Assert.Equal(PrototypeNativeOvergrowthEvents.NeowRelicId("PrecariousShears"),
            pending.SourceRelicId);
        PrototypeStateInvariants.Validate(state);

        var engine = new PrototypeGameEngine();
        foreach (var id in new long[] { 1, 2 })
        {
            var action = engine.GetLegalActions(state).Single(candidate =>
                candidate.Kind == "choose_event_deck_card"
                && candidate.ReadPayload<ChooseEventDeckCardPayload>()
                    .CardInstanceId == id);
            state = engine.Step(state, action).State;
            if (id == 1)
            {
                Assert.Equal(RunPhase.Event, state.Phase);
                Assert.Equal(70, state.Player.Hp);
                Assert.Equal(16, state.World!.Event!.DeferredHpLoss);
            }
            PrototypeStateInvariants.Validate(state);
        }

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(11, state.Player.Deck.Length);
        Assert.Equal(before.Skip(2).Select(c => c.InstanceId),
            state.Player.Deck.Select(c => c.InstanceId));
        Assert.Equal(54, state.Player.Hp);
        Assert.Equal(14, state.World!.NextCardInstanceId);
    }

    [Fact]
    public void LeafyPoulticeLosesMaxHpAndTransformsFirstStrikeAndDefend()
    {
        var state = Offer("LeafyPoultice", "neow-leafy");
        var before = state.Player.Deck;
        state = Pick(state, "LeafyPoultice");

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(58, state.Player.MaxHp);
        Assert.Equal(58, state.Player.Hp);
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.Equal(before.Select(c => c.InstanceId),
            state.Player.Deck.Select(c => c.InstanceId));
        Assert.NotEqual(before[0].CardId, state.Player.Deck[0].CardId);
        Assert.NotEqual(before[5].CardId, state.Player.Deck[5].CardId);
        Assert.Equal(PrototypeCardRarity.Basic,
            PrototypeContent.Card(before[0].CardId).Rarity);
        Assert.NotEqual(PrototypeCardRarity.Basic,
            PrototypeContent.Card(state.Player.Deck[0].CardId).Rarity);
        Assert.NotEqual(PrototypeCardRarity.Basic,
            PrototypeContent.Card(state.Player.Deck[5].CardId).Rarity);
        Assert.Equal(before.Skip(1).Take(4).Select(c => c.CardId),
            state.Player.Deck.Skip(1).Take(4).Select(c => c.CardId));
        Assert.Equal(before.Skip(6).Select(c => c.CardId),
            state.Player.Deck.Skip(6).Select(c => c.CardId));
        Assert.Equal(14, state.World!.NextCardInstanceId);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void NeowsFuryCannotRetrieveMoreThanTheHandHasRoomFor()
    {
        var engine = new PrototypeGameEngine();
        var state = Pick(Offer("NeowsTorment", "neow-fury-hand-cap"),
            "NeowsTorment");
        state = engine.Step(state, engine.GetLegalActions(state)[0]).State;

        var combat = state.World!.Combat!;
        var fury = Assert.Single(combat.Cards, card =>
            card.CardId == "proto.native.neow.neows_fury");
        var others = combat.Cards.Select(card => card.InstanceId)
            .Where(id => id != fury.InstanceId).ToArray();
        combat = combat with
        {
            Hand = new[] { fury.InstanceId }.Concat(others.Take(9)).ToArray(),
            DiscardPile = others.Skip(9).ToArray(),
            DrawPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3
        };
        state = state with { World = state.World with { Combat = combat } };
        PrototypeStateInvariants.Validate(state);

        var play = engine.GetLegalActions(state).First(candidate =>
            candidate.Kind == "play_card"
            && candidate.ReadPayload<PlayCardPayload>().CardInstanceId
                == fury.InstanceId);
        state = engine.Step(state, play).State;
        var pending = Assert.IsType<PendingCombatChoiceState>(
            state.World!.Combat!.PendingChoice);
        Assert.Equal(1, pending.Selection.MaxSelections);
        Assert.DoesNotContain(engine.GetLegalActions(state), action =>
            action.Kind == "select_cards"
            && action.ReadPayload<SelectCardsPayload>().CardInstanceIds.Length > 1);

        var choice = engine.GetLegalActions(state).Single(action =>
            action.Kind == "select_cards"
            && action.ReadPayload<SelectCardsPayload>()
                .CardInstanceIds.SequenceEqual(new[] { others[9] }));
        state = engine.Step(state, choice).State;
        Assert.Equal(10, state.World!.Combat!.Hand.Length);
        Assert.Contains(fury.InstanceId, state.World.Combat.ExhaustPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void NeowsFuryDamageAndDiscardRetrievalUseCombatChoiceContinuation(
        bool upgraded, int expectedMax)
    {
        var engine = new PrototypeGameEngine();
        var state = Pick(Offer("NeowsTorment",
            "neow-fury-combat-" + upgraded), "NeowsTorment");
        state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        var combat = state.World!.Combat!;
        var fury = Assert.Single(combat.Cards,
            card => card.CardId == "proto.native.neow.neows_fury");
        var others = combat.Cards.Select(card => card.InstanceId)
            .Where(id => id != fury.InstanceId).ToArray();
        combat = combat with
        {
            Hand = [fury.InstanceId],
            DiscardPile = others.Take(4).ToArray(),
            DrawPile = others.Skip(4).ToArray(),
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3,
            Cards = combat.Cards.Select(card =>
                upgraded && card.InstanceId == fury.InstanceId
                    ? card with { UpgradeLevel = 1 }
                    : card).ToArray()
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };
        PrototypeStateInvariants.Validate(state);

        var action = engine.GetLegalActions(state).First(candidate =>
            candidate.Kind == "play_card"
            && candidate.ReadPayload<PlayCardPayload>()
                .CardInstanceId == fury.InstanceId);
        state = engine.Step(state, action).State;
        var pending = Assert.IsType<PendingCombatChoiceState>(
            state.World!.Combat!.PendingChoice);
        Assert.Equal(PrototypeCardZone.DiscardPile, pending.Selection.SourceZone);
        Assert.Equal(0, pending.Selection.MinSelections);
        Assert.Equal(expectedMax, pending.Selection.MaxSelections);
        PrototypeStateInvariants.Validate(state);

        var selected = others.Take(expectedMax).ToArray();
        var choose = engine.GetLegalActions(state).Single(candidate =>
            candidate.Kind == "select_cards"
            && candidate.ReadPayload<SelectCardsPayload>()
                .CardInstanceIds.SequenceEqual(selected));
        state = engine.Step(state, choose).State;
        Assert.Null(state.World!.Combat!.PendingChoice);
        Assert.All(selected, id =>
            Assert.Contains(id, state.World.Combat.Hand));
        Assert.Contains(fury.InstanceId, state.World.Combat.ExhaustPile);
        PrototypeStateInvariants.Validate(state);
    }
}
