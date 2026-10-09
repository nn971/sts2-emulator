using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeColorlessThirdBatchTests
{
    [Theory]
    [InlineData("proto.colorless.purity", PrototypeCardRarity.Uncommon)]
    [InlineData("proto.colorless.thinking_ahead", PrototypeCardRarity.Uncommon)]
    [InlineData("proto.colorless.hand_of_greed", PrototypeCardRarity.Rare)]
    public void ThirdBatchIsPlayableButOutsideSilentRewards(
        string id, PrototypeCardRarity rarity)
    {
        var card = PrototypeContent.Card(id);
        Assert.Contains(id, PrototypeColorlessCards.NativePoolIds);
        Assert.Contains(id, PrototypeColorlessCards.ImplementedShopPool);
        Assert.Equal(rarity, card.Rarity);
        Assert.True(card.MechanicsImplemented);
        Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 5)]
    public void PurityExhaustsSelectedHandCardsAndDispatchesCardExhausted(
        int upgrade, int maxSelection)
    {
        var state = Setup("purity-" + upgrade,
            ["proto.colorless.purity", "proto.silent.strike",
             "proto.silent.defend", "proto.silent.backflip",
             "proto.silent.strike", "proto.silent.defend"],
            [1, 2, 3, 4, 5, 6], [], upgrade,
            relics: [new RelicInstance("proto.relic.charons_ashes",
                PrototypeJson.EmptyObject())]);
        Assert.True(PrototypeContent.Card("proto.colorless.purity").Retain);
        state = Play(state);
        var pending = state.World!.Combat!.PendingChoice!;
        Assert.Equal(0, pending.Selection.MinSelections);
        Assert.Equal(maxSelection, pending.Selection.MaxSelections);
        Assert.DoesNotContain(1, pending.CandidateCardInstanceIds);

        var chosen = Enumerable.Range(2, maxSelection)
            .Select(i => (long)i).ToArray();
        var fork = state.Fork();
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        state = Select(state, chosen);
        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.All(chosen, id => Assert.Contains(id, combat.ExhaustPile));
        Assert.Equal(maxSelection + 1, combat.ExhaustPile.Length);
        // Charon's Ashes deals 3 to each enemy per exhaustion,
        // including the Purity card itself when resolution completes.
        Assert.Equal(100 - 3 * (maxSelection + 1), combat.Enemies[0].Hp);
        Assert.Equal(3, combat.Energy);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void PurityMaySelectNoCardsAndStillExhaustItself()
    {
        var state = Setup("purity-zero",
            ["proto.colorless.purity", "proto.silent.strike"],
            [1, 2], []);
        state = Play(state);
        state = Select(state, []);
        var combat = state.World!.Combat!;
        Assert.Equal(new long[] { 2 }, combat.Hand);
        Assert.Equal(new long[] { 1 }, combat.ExhaustPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void ThinkingAheadDrawsThenReturnsOneCardToDrawTop(
        int upgrade, bool exhaust)
    {
        var state = Setup("thinking-" + upgrade,
            ["proto.colorless.thinking_ahead",
             "proto.silent.strike", "proto.silent.defend",
             "proto.silent.backflip"],
            [1, 2], [3, 4], upgrade);
        state = Play(state);
        var pending = state.World!.Combat!.PendingChoice!;
        Assert.Equal(new long[] { 2, 4, 3 },
            pending.CandidateCardInstanceIds);
        state = Select(state, [4]);
        var combat = state.World!.Combat!;
        Assert.Equal(new long[] { 2, 3 }, combat.Hand);
        Assert.Equal(new long[] { 4 }, combat.DrawPile);
        Assert.Equal(exhaust, combat.ExhaustPile.Contains(1));
        Assert.Equal(!exhaust, combat.DiscardPile.Contains(1));
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 20, 120)]
    [InlineData(1, 25, 125)]
    public void HandOfGreedAwardsGoldOnFatal(
        int upgrade, int expectedDamage, int expectedGold)
    {
        var state = Setup("greed-" + upgrade,
            ["proto.colorless.hand_of_greed", "proto.silent.strike"],
            [1], [2], upgrade, targetHp: expectedDamage);
        state = Play(state, 1);
        Assert.Equal(expectedGold, state.Player.Gold);
        var combat = state.World!.Combat!;
        Assert.Equal(0, combat.Enemies[0].Hp);
        Assert.Equal(100, combat.Enemies[1].Hp);
        Assert.Contains(1, combat.DiscardPile);
        Assert.Equal(1, combat.Energy);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void HandOfGreedRequiresActualFatalKill()
    {
        var state = Setup("greed-nonfatal",
            ["proto.colorless.hand_of_greed", "proto.silent.strike"],
            [1], [2]);
        state = Play(state, 1);
        Assert.Equal(100, state.Player.Gold);
        Assert.Equal(80, state.World!.Combat!.Enemies[0].Hp);
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState Play(RunState state, int? enemyId = null)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>().CardInstanceId == 1
                && (enemyId is null
                    || a.ReadPayload<PlayCardPayload>().TargetEnemyId == enemyId));
        return engine.Step(state, action).State;
    }

    private static RunState Select(RunState state, long[] selected)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "select_cards"
                && a.ReadPayload<SelectCardsPayload>().CardInstanceIds
                    .SequenceEqual(selected));
        return engine.Step(state, action).State;
    }

    private static RunState Setup(
        string seed, string[] cardIds, long[] hand, long[] draw,
        int sourceUpgrade = 0, int targetHp = 100,
        RelicInstance[]? relics = null)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = cardIds.Select((id, index) =>
            new CardInstance(index + 1, id, index == 0 ? sourceUpgrade : 0, empty))
            .ToArray();
        var cards = cardIds.Select((id, index) =>
            new CombatCardInstance(index + 1, index + 1, id,
                index == 0 ? sourceUpgrade : 0, false, empty))
            .ToArray();
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0, Hand: hand,
            DrawPile: draw, DiscardPile: [], ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(1, "proto.enemy.crawler",
                    targetHp, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal)),
                new EnemyCombatState(2, "proto.enemy.crawler",
                    100, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: cards.Length + 1, Cards: cards,
            PlayerPowers: [], NextPowerApplicationOrder: 1);
        return new RunState(
            "prototype-unbound", "prototype-0.1", seed, seed, 0,
            RunPhase.Combat,
            new PlayerState(70, 70, 100, deck,
                relics ?? [], new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed), empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1, 1, cards.Length + 1, PrototypeRoomType.Combat,
                new MapState([]), combat, null, null, null, null));
    }
}
