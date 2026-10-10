using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeColorlessFinalBatchTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void EntropyHasNativeUpgradeAndRareSoloMerchantMetadata(
        int upgrade, bool innate)
    {
        var card = PrototypeContent.Card("proto.colorless.entropy");
        Assert.True(card.MechanicsImplemented);
        Assert.Equal(PrototypeCardRarity.Rare, card.Rarity);
        Assert.Equal(PrototypeCardType.Power, card.Type);
        Assert.Equal(1, card.Cost.AmountAt(upgrade));
        Assert.Equal(innate, card.Innate || (upgrade > 0 && card.InnateOnUpgrade));
        Assert.Contains(card.Id, PrototypeColorlessCards.ImplementedShopPool);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void StratagemHasNativeCostUpgrade(int upgrade, int cost)
    {
        var card = PrototypeContent.Card("proto.colorless.stratagem");
        Assert.True(card.MechanicsImplemented);
        Assert.Equal(PrototypeCardRarity.Uncommon, card.Rarity);
        Assert.Equal(cost, card.Cost.AmountAt(upgrade));
    }

    [Fact]
    public void EntropyChoosesAHandCardAndTransformsWithoutMutatingPersistentDeck()
    {
        var state = Setup("entropy-turn",
            ["proto.colorless.entropy", "proto.silent.strike",
             "proto.silent.defend", "proto.silent.neutralize"],
            [1], [2, 3, 4]);
        state = Play(state, 1);
        state = EndTurn(state);
        var pending = Assert.IsType<PendingCombatChoiceState>(
            state.World!.Combat!.PendingChoice);
        Assert.Equal(PrototypeCardZone.Hand, pending.Selection.SourceZone);
        Assert.Equal(PrototypeCardSelectionResolutionKind.TransformRandom,
            pending.Selection.Resolution);
        Assert.Equal(1, pending.Selection.MinSelections);
        Assert.Equal(1, pending.Selection.MaxSelections);

        var before = state.Fork();
        var candidate = pending.CandidateCardInstanceIds
            .Single(id => id == 2);
        state = Select(state, candidate);
        before = Select(before, candidate);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(before));

        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.DoesNotContain(combat.Cards,
            card => card.InstanceId == candidate);
        var transformedId = Assert.Single(combat.Hand.Where(id =>
            !pending.CandidateCardInstanceIds.Contains(id)));
        var transformed = combat.Cards.Single(card =>
            card.InstanceId == transformedId);
        Assert.NotEqual("proto.silent.strike", transformed.CardId);
        Assert.True(transformed.IsTemporary);
        Assert.Null(transformed.PersistentCardInstanceId);
        Assert.Contains(transformed.CardId, PrototypeContent.RewardCardPool);
        Assert.Contains(candidate, combat.TransformedPersistentCardIds!);
        Assert.Equal("proto.silent.strike",
            state.Player.Deck.Single(card => card.InstanceId == candidate).CardId);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void EntropyDoesNotPromptWhenHandIsEmpty()
    {
        var state = Setup("entropy-no-hand",
            ["proto.colorless.entropy"], [1], []);
        state = Play(state, 1);
        state = EndTurn(state);
        Assert.Null(state.World!.Combat!.PendingChoice);
        Assert.Contains(state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.entropy");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void StratagemInterruptsNormalHandDrawAtShuffleBoundary()
    {
        var state = Setup("stratagem-hand-draw",
            ["proto.colorless.stratagem", "proto.silent.strike",
             "proto.silent.defend", "proto.silent.neutralize"],
            [1], [], [2, 3, 4]);
        state = Play(state, 1);
        state = EndTurn(state);
        var combat = state.World!.Combat!;
        var pending = Assert.IsType<PendingCombatChoiceState>(
            combat.PendingChoice);
        Assert.Equal(PrototypeCardZone.DrawPile, pending.Selection.SourceZone);
        Assert.Equal(PrototypeCardSelectionResolutionKind.MoveToHand,
            pending.Selection.Resolution);
        Assert.Equal(1, pending.Selection.MinSelections);
        Assert.Equal(1, pending.Selection.MaxSelections);
        Assert.Empty(combat.Hand); // Choice must happen before any draw.

        var copy = state.Fork();
        var selected = pending.CandidateCardInstanceIds[0];
        state = Select(state, selected);
        copy = Select(copy, selected);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(copy));
        combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Contains(selected, combat.Hand);
        Assert.DoesNotContain(selected, combat.DrawPile);
        Assert.Equal(2, combat.Turn);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void StratagemCanSuspendAndResumeCardEffectDraw()
    {
        var state = Setup("stratagem-effect-draw",
            ["proto.colorless.stratagem", "proto.colorless.finesse",
             "proto.silent.strike", "proto.silent.defend"],
            [1, 2], [], [3, 4]);
        state = Play(state, 1);
        state = Play(state, 2);
        var pending = Assert.IsType<PendingCombatChoiceState>(
            state.World!.Combat!.PendingChoice);
        Assert.Equal(PrototypeCardZone.DrawPile, pending.Selection.SourceZone);
        Assert.Equal(1, pending.Selection.MaxSelections);

        state = Select(state, pending.CandidateCardInstanceIds[0]);
        Assert.Null(state.World!.Combat!.PendingChoice);
        Assert.Contains(pending.CandidateCardInstanceIds[0],
            state.World.Combat.Hand);
        Assert.Contains(2, state.World.Combat.DiscardPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void StratagemIsInactiveUntilTheDrawPileActuallyShuffles()
    {
        var state = Setup("stratagem-no-shuffle",
            ["proto.colorless.stratagem", "proto.colorless.finesse",
             "proto.silent.strike"],
            [1, 2], [3]);
        state = Play(state, 1);
        state = Play(state, 2);
        Assert.Null(state.World!.Combat!.PendingChoice);
        Assert.Contains(3, state.World.Combat.Hand);
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState Select(RunState state, params long[] cards)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "select_cards"
                && a.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual(cards));
        return engine.Step(state, action).State;
    }

    private static RunState Play(RunState state, long instanceId)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>().CardInstanceId == instanceId);
        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(RunState state)
    {
        var engine = new PrototypeGameEngine();
        return engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;
    }

    private static RunState Setup(
        string seed, string[] cardIds, long[] hand,
        long[] draw, long[]? discard = null)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = cardIds.Select((id, i) =>
            new CardInstance(i + 1, id, 0, empty)).ToArray();
        var cards = deck.Select(card => new CombatCardInstance(
            card.InstanceId, card.InstanceId, card.CardId,
            card.UpgradeLevel, false, empty)).ToArray();
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: hand, DrawPile: draw,
            DiscardPile: discard ?? [], ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(1, "proto.enemy.crawler",
                    100, 0, 0, new Dictionary<string, int>(StringComparer.Ordinal)),
                new EnemyCombatState(2, "proto.enemy.crawler",
                    100, 0, 0, new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: cards.Length + 1,
            Cards: cards, PlayerPowers: [], NextPowerApplicationOrder: 1);
        return new RunState(
            "prototype-unbound", "prototype-0.1", seed, seed, 0,
            RunPhase.Combat,
            new PlayerState(70, 70, 100, deck, [], new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed), empty,
            new RunWorldState(
                PrototypeContent.RulesetId, PrototypeContent.CharacterId,
                1, 1, cards.Length + 1, PrototypeRoomType.Combat,
                new MapState([]), combat, null, null, null, null));
    }
}
