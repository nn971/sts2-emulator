using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeColorlessPowerBatchTests
{
    [Theory]
    [InlineData("proto.colorless.panache", PrototypeCardRarity.Uncommon)]
    [InlineData("proto.colorless.the_bomb", PrototypeCardRarity.Uncommon)]
    [InlineData("proto.colorless.mayhem", PrototypeCardRarity.Rare)]
    public void PowerBatchIsPlayableButNotInSilentRewardPool(
        string id, PrototypeCardRarity rarity)
    {
        Assert.Contains(id, PrototypeColorlessCards.NativePoolIds);
        Assert.Contains(id, PrototypeColorlessCards.ImplementedShopPool);
        Assert.Equal(rarity, PrototypeContent.Card(id).Rarity);
        Assert.True(PrototypeContent.Card(id).MechanicsImplemented);
        Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 14)]
    public void PanacheIgnoresOwnPlayThenTriggersOnEveryFifthCard(
        int upgrade, int damage)
    {
        var deck = new[] { "proto.colorless.panache" }
            .Concat(Enumerable.Repeat("proto.colorless.finesse", 6))
            .ToArray();
        var state = Setup("panache-" + upgrade, deck,
            [1, 2, 3, 4, 5, 6, 7], [], upgrade);
        state = Play(state, 1);
        Assert.Equal(100, state.World!.Combat!.Enemies[0].Hp);
        var first = Assert.Single(state.World!.Combat!.PlayerPowers);
        Assert.Equal("proto.power.panache", first.PowerId);
        Assert.Equal(damage, first.Stacks);
        Assert.Equal(0, first.TriggerCounts![0]);

        for (long card = 2; card <= 7; card++)
        {
            state = Play(state, card);
            var combat = state.World!.Combat!;
            var expectedHp = card >= 6 ? 100 - damage : 100;
            Assert.Equal(expectedHp, combat.Enemies[0].Hp);
            Assert.Equal(expectedHp, combat.Enemies[1].Hp);
        }

        var power = Assert.Single(state.World!.Combat!.PlayerPowers);
        Assert.Equal(1, power.TriggerCounts![0]);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void MultiplePanachesHaveIndependentCounters()
    {
        var deck = new[] { "proto.colorless.panache",
                           "proto.colorless.panache" }
            .Concat(Enumerable.Repeat("proto.colorless.finesse", 5))
            .ToArray();
        var state = Setup("panache-multiple", deck,
            [1, 2, 3, 4, 5, 6, 7], []);
        state = Play(state, 1);
        state = Play(state, 2);
        Assert.Equal(2, state.World!.Combat!.PlayerPowers.Length);
        for (long card = 3; card <= 7; card++)
        {
            state = Play(state, card);
        }
        var combat = state.World!.Combat!;
        Assert.Equal(80, combat.Enemies[0].Hp);
        Assert.Equal(80, combat.Enemies[1].Hp);
        Assert.Equal(new[] { 1, 0 },
            combat.PlayerPowers.Select(p => p.TriggerCounts![0]));
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 40)]
    [InlineData(1, 50)]
    public void BombCountdownExplodesAfterThreePlayerTurns(
        int upgrade, int damage)
    {
        var state = Setup("bomb-" + upgrade,
            ["proto.colorless.the_bomb", "proto.silent.defend"],
            [1], [2], upgrade);
        state = Play(state, 1);
        Assert.Equal(1, state.World!.Combat!.Energy);
        var bomb = Assert.Single(state.World!.Combat!.PlayerPowers);
        Assert.Equal("proto.power.the_bomb", bomb.PowerId);
        Assert.Equal(3, bomb.Stacks);
        Assert.Equal(damage, bomb.StoredValue);
        for (var turn = 1; turn <= 3; turn++)
        {
            state = EndTurn(state);
            var combat = state.World!.Combat!;
            if (turn < 3)
            {
                Assert.Equal(3 - turn,
                    Assert.Single(combat.PlayerPowers).Stacks);
                Assert.Equal(100, combat.Enemies[0].Hp);
            }
            else
            {
                Assert.DoesNotContain(combat.PlayerPowers,
                    p => p.PowerId == "proto.power.the_bomb");
                Assert.Equal(100 - damage, combat.Enemies[0].Hp);
                Assert.Equal(100 - damage, combat.Enemies[1].Hp);
            }
            PrototypeStateInvariants.Validate(state);
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void MayhemAutoPlaysTopDrawCardAfterHandDraw(
        int upgrade, int energyAfterPlay)
    {
        var deck = new[] { "proto.colorless.mayhem",
                          "proto.colorless.ultimate_strike" }
            .Concat(Enumerable.Repeat("proto.silent.defend", 5))
            .ToArray();
        var state = Setup("mayhem-" + upgrade, deck,
            [1], [2, 3, 4, 5, 6, 7], upgrade);
        state = Play(state, 1);
        Assert.Equal(energyAfterPlay, state.World!.Combat!.Energy);
        Assert.Equal(1, Assert.Single(state.World!.Combat!.PlayerPowers).Stacks);
        state = EndTurn(state);
        var combat = state.World!.Combat!;
        Assert.Contains(2, combat.DiscardPile);
        Assert.Equal(14, 200 - combat.Enemies.Sum(enemy => enemy.Hp));
        Assert.Equal(2, combat.Turn);
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState Play(RunState state, long cardInstanceId)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>().CardInstanceId
                    == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(RunState state)
    {
        var engine = new PrototypeGameEngine();
        return engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;
    }

    private static RunState Setup(string seed, string[] cardIds,
        long[] hand, long[] draw, int sourceUpgrade = 0)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = cardIds.Select((id, i) =>
            new CardInstance(i + 1, id, i == 0 ? sourceUpgrade : 0, empty))
            .ToArray();
        var cards = deck.Select(c =>
            new CombatCardInstance(c.InstanceId, c.InstanceId,
                c.CardId, c.UpgradeLevel, false, empty)).ToArray();
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: hand, DrawPile: draw, DiscardPile: [],
            ExhaustPile: [],
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
            "prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(70, 70, 100, deck, [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed), empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1, 1, cards.Length + 1, PrototypeRoomType.Combat,
                new MapState([]), combat, null, null, null, null));
    }
}
