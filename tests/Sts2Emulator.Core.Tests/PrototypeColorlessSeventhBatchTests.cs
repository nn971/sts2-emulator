using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeColorlessSeventhBatchTests
{
    [Theory]
    [InlineData("proto.colorless.volley")]
    [InlineData("proto.colorless.splash")]
    [InlineData("proto.colorless.anointed")]
    [InlineData("proto.colorless.gold_axe")]
    public void NewSoloCardsArePlayableMerchantCardsButNotSilentRewards(string id)
    {
        var definition = PrototypeContent.Card(id);
        Assert.Contains(id, PrototypeColorlessCards.NativePoolIds);
        Assert.Contains(id, PrototypeColorlessCards.ImplementedShopPool);
        Assert.True(definition.MechanicsImplemented);
        Assert.False(definition.MultiplayerOnly);
        Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 14)]
    public void VolleySpendsXAndRetargetsEachHit(int upgrade, int damage)
    {
        var state = Setup("volley-" + upgrade,
            ["proto.colorless.volley"], [1], [], upgrade);
        var fork = state.Fork();
        state = Play(state, 1);
        fork = Play(fork, 1);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        var combat = state.World!.Combat!;
        Assert.Equal(0, combat.Energy);
        Assert.Equal(200 - 3 * damage, combat.Enemies.Sum(e => e.Hp));
        Assert.Contains(1, combat.DiscardPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SplashOffersOptionalUpgradedAttacksFreeForTheTurn(int upgrade)
    {
        var state = Setup("splash-" + upgrade,
            ["proto.colorless.splash"], [1], [], upgrade);
        state = Play(state, 1);
        var pending = state.World!.Combat!.PendingChoice!;
        Assert.Equal(3, pending.CandidateCardInstanceIds.Length);
        Assert.Equal(0, pending.Selection.MinSelections);
        var candidates = pending.CandidateCardInstanceIds
            .Select(id => state.World!.Combat!.Cards.Single(c => c.InstanceId == id))
            .ToArray();
        Assert.Equal(3, candidates.Select(c => c.CardId).Distinct().Count());
        Assert.All(candidates, c =>
        {
            Assert.Equal(PrototypeCardType.Attack,
                PrototypeContent.Card(c.CardId).Type);
            Assert.Equal(upgrade, c.UpgradeLevel);
            Assert.True(c.IsTemporary);
        });
        var selectedId = candidates[0].InstanceId;
        state = Select(state, [selectedId]);
        var combat = state.World!.Combat!;
        Assert.Contains(selectedId, combat.Hand);
        Assert.Equal(0, combat.Cards.Single(c =>
            c.InstanceId == selectedId).TemporaryEnergyCost!.Cost);
        Assert.Contains(1, combat.DiscardPile);
        Assert.DoesNotContain(candidates.Skip(1),
            c => combat.Cards.Any(remaining =>
                remaining.InstanceId == c.InstanceId));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void SplashMayBeDeclinedWithoutLeakingTemporaryCards()
    {
        var state = Setup("splash-decline",
            ["proto.colorless.splash"], [1], []);
        state = Play(state, 1);
        var offered = state.World!.Combat!.PendingChoice!.CandidateCardInstanceIds;
        state = Select(state, []);
        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.DoesNotContain(offered,
            id => combat.Cards.Any(c => c.InstanceId == id));
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AnointedMovesOnlyRareCardsWithoutDrawing(int upgrade)
    {
        var state = Setup("anointed-" + upgrade,
            ["proto.colorless.anointed", "proto.silent.strike",
             "proto.colorless.panache", "proto.colorless.the_bomb",
             "proto.colorless.master_of_strategy"],
            [1], [2, 3, 4, 5], upgrade);
        var fork = state.Fork();
        state = Play(state, 1);
        fork = Play(fork, 1);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        var combat = state.World!.Combat!;
        Assert.Equal(new long[] { 3, 4, 5 }, combat.Hand.Order().ToArray());
        Assert.Equal(new long[] { 2 }, combat.DrawPile);
        Assert.Equal(0, combat.CounterState.CardsDrawnThisCombat);
        Assert.Contains(1, combat.ExhaustPile);
        Assert.Equal(upgrade > 0,
            PrototypeContent.Card("proto.colorless.anointed").RetainOnUpgrade
                && upgrade > 0);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void AnointedRespectsHandCapacity()
    {
        var ids = new[] { "proto.colorless.anointed" }
            .Concat(Enumerable.Repeat("proto.silent.strike", 8))
            .Concat(Enumerable.Repeat("proto.colorless.panache", 3))
            .ToArray();
        var state = Setup("anointed-capacity", ids,
            Enumerable.Range(1, 9).Select(i => (long)i).ToArray(),
            [10, 11, 12]);
        state = Play(state, 1);
        var combat = state.World!.Combat!;
        Assert.Equal(10, combat.Hand.Length);
        Assert.Single(combat.DrawPile);
        Assert.Equal(0, combat.CounterState.CardsDrawnThisCombat);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void GoldAxeCountsCompletedCardPlaysAcrossItsOwnExecution()
    {
        var state = Setup("gold-axe",
            ["proto.colorless.gold_axe", "proto.silent.strike"],
            [1, 2], []);
        // Strike completes first; Gold Axe observes that one completed
        // play and counts its own play only after the attack resolves.
        state = Play(state, 2, 1);
        Assert.Equal(94, state.World!.Combat!.Enemies[0].Hp);
        Assert.Equal(1, state.World.Combat.CounterState.CardsPlayedThisCombat);
        state = Play(state, 1, 1);
        Assert.Equal(2, state.World!.Combat!.CounterState.CardsPlayedThisCombat);
        Assert.Equal(93, state.World.Combat.Enemies[0].Hp);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void GoldAxeUpgradesToRetain()
    {
        var card = PrototypeContent.Card("proto.colorless.gold_axe");
        Assert.Equal(PrototypeCardType.Attack, card.Type);
        Assert.True(card.RetainOnUpgrade);
    }

    private static RunState Play(RunState state, long id, int? target = null)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>().CardInstanceId == id
                && (target is null
                    || a.ReadPayload<PlayCardPayload>().TargetEnemyId == target));
        return engine.Step(state, action).State;
    }

    private static RunState Select(RunState state, long[] ids)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(
            a => a.Kind == "select_cards"
                && a.ReadPayload<SelectCardsPayload>().CardInstanceIds.SequenceEqual(ids));
        return engine.Step(state, action).State;
    }

    private static RunState Setup(
        string seed, string[] cardIds, long[] hand, long[] draw,
        int upgrade = 0)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = cardIds.Select((id, i) =>
            new CardInstance(i + 1, id, i == 0 ? upgrade : 0, empty)).ToArray();
        var cards = deck.Select(c => new CombatCardInstance(
            c.InstanceId, c.InstanceId, c.CardId, c.UpgradeLevel,
            false, empty)).ToArray();
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: hand, DrawPile: draw, DiscardPile: [], ExhaustPile: [],
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
