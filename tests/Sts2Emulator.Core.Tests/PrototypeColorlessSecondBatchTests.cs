using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeColorlessSecondBatchTests
{
    [Theory]
    [InlineData("proto.colorless.shockwave", PrototypeCardRarity.Uncommon)]
    [InlineData("proto.colorless.impatience", PrototypeCardRarity.Uncommon)]
    [InlineData("proto.colorless.mind_blast", PrototypeCardRarity.Uncommon)]
    [InlineData("proto.colorless.secret_technique", PrototypeCardRarity.Rare)]
    [InlineData("proto.colorless.secret_weapon", PrototypeCardRarity.Rare)]
    public void NewNativeColorlessCardsRemainSeparateFromSilentRewards(
        string cardId, PrototypeCardRarity rarity)
    {
        Assert.Contains(cardId, PrototypeColorlessCards.NativePoolIds);
        var definition = PrototypeContent.Card(cardId);
        Assert.Equal(rarity, definition.Rarity);
        Assert.True(definition.MechanicsImplemented);
        Assert.False(definition.MultiplayerOnly);
        Assert.Contains(cardId, PrototypeColorlessCards.ImplementedShopPool);
        Assert.DoesNotContain(cardId, PrototypeContent.RewardCardPool);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 5)]
    public void ShockwaveAppliesBothDebuffsToEveryLivingEnemy(
        int upgrade, int expectedStacks)
    {
        var state = BuildCombat(
            "shockwave-" + upgrade,
            ["proto.colorless.shockwave"],
            [1], [], upgrade, enemies: 2);
        state = PlaySource(state);
        var combat = state.World!.Combat!;
        Assert.Equal(1, combat.Energy);
        Assert.Contains(1, combat.ExhaustPile);
        Assert.All(combat.Enemies, enemy =>
        {
            Assert.Equal(expectedStacks,
                enemy.Statuses.GetValueOrDefault("proto.status.weak"));
            Assert.Equal(expectedStacks,
                enemy.Statuses.GetValueOrDefault("proto.status.vulnerable"));
        });
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, false, 2)]
    [InlineData(1, false, 3)]
    [InlineData(0, true, 0)]
    [InlineData(1, true, 0)]
    public void ImpatienceChecksForRemainingAttacksInHand(
        int upgrade, bool hasAttack, int drawn)
    {
        var state = BuildCombat(
            "impatience-" + upgrade + "-" + hasAttack,
            [
                "proto.colorless.impatience",
                hasAttack ? "proto.silent.strike" : "proto.silent.defend",
                "proto.silent.backflip",
                "proto.silent.defend",
                "proto.silent.strike"
            ], [1, 2], [3, 4, 5], upgrade);
        state = PlaySource(state);
        var combat = state.World!.Combat!;
        Assert.Equal(3, combat.Energy);
        Assert.Equal(1 + drawn, combat.Hand.Length);
        Assert.Equal(3 - drawn, combat.DrawPile.Length);
        Assert.Contains(1, combat.DiscardPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    public void MindBlastUsesDrawPileCardCountAndUpgradesToZeroCost(
        int upgrade, int energy)
    {
        var state = BuildCombat(
            "mind-blast-" + upgrade,
            [
                "proto.colorless.mind_blast",
                "proto.silent.strike",
                "proto.silent.defend",
                "proto.silent.backflip"
            ], [1], [2, 3, 4], upgrade);
        state = PlaySource(state);
        var combat = state.World!.Combat!;
        Assert.Equal(97, Assert.Single(combat.Enemies).Hp);
        Assert.Equal(energy, combat.Energy);
        Assert.Contains(1, combat.DiscardPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData("proto.colorless.secret_technique", 0, 3, true)]
    [InlineData("proto.colorless.secret_technique", 1, 4, false)]
    [InlineData("proto.colorless.secret_weapon", 0, 2, true)]
    [InlineData("proto.colorless.secret_weapon", 1, 5, false)]
    public void SecretCardsChooseExactlyMatchingCardsAndResumePlay(
        string cardId, int upgrade, long chosen, bool exhaust)
    {
        var state = BuildCombat("secret-" + cardId + "-" + upgrade,
            [
                cardId,
                "proto.silent.strike",
                "proto.silent.defend",
                "proto.silent.backflip",
                "proto.silent.strike"
            ], [1], [2, 3, 4, 5], upgrade);
        state = PlaySource(state);
        var engine = new PrototypeGameEngine();
        var combat = state.World!.Combat!;
        Assert.NotNull(combat.PendingChoice);

        var expectedCandidates =
            cardId.EndsWith("technique", StringComparison.Ordinal)
                ? new long[] { 3, 4 }
                : new long[] { 2, 5 };
        Assert.Equal(expectedCandidates,
            combat.PendingChoice!.CandidateCardInstanceIds);

        var fork = state.Fork();
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));

        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "select_cards"
                && a.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([chosen]));
        state = engine.Step(state, action).State;
        combat = state.World!.Combat!;

        Assert.Null(combat.PendingChoice);
        Assert.Contains(chosen, combat.Hand);
        Assert.DoesNotContain(chosen, combat.DrawPile);
        Assert.Equal(exhaust, combat.ExhaustPile.Contains(1));
        Assert.Equal(!exhaust, combat.DiscardPile.Contains(1));
        Assert.Equal(3, combat.Energy);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData("proto.colorless.secret_technique",
        "proto.silent.strike")]
    [InlineData("proto.colorless.secret_weapon",
        "proto.silent.defend")]
    public void SecretCardsWithNoMatchingDrawPileCardResolveCleanly(
        string cardId, string otherCard)
    {
        var state = BuildCombat("secret-empty-" + cardId,
            [cardId, otherCard], [1], [2]);
        state = PlaySource(state);
        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Contains(2, combat.DrawPile);
        Assert.Contains(1, combat.ExhaustPile);
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState PlaySource(RunState state)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1);
        return engine.Step(state, action).State;
    }

    private static RunState BuildCombat(
        string seed,
        string[] cardIds,
        long[] hand,
        long[] draw,
        int sourceUpgrade = 0,
        int enemies = 1)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = cardIds.Select((id, index) =>
            new CardInstance(
                index + 1, id, index == 0 ? sourceUpgrade : 0, empty))
            .ToArray();
        var cards = cardIds.Select((id, index) =>
            new CombatCardInstance(
                index + 1, index + 1, id,
                index == 0 ? sourceUpgrade : 0, false, empty))
            .ToArray();
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: hand,
            DrawPile: draw,
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: Enumerable.Range(1, enemies)
                .Select(id => new EnemyCombatState(
                    id, "proto.enemy.crawler", 100, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal)))
                .ToArray(),
            NextCardInstanceId: cards.Length + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);
        return new RunState(
            "prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(70, 70, 100, deck, [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed), empty,
            new RunWorldState(
                PrototypeContent.RulesetId, PrototypeContent.CharacterId,
                1, 1, cards.Length + 1, PrototypeRoomType.Combat,
                new MapState([]), combat, null, null, null, null));
    }
}
