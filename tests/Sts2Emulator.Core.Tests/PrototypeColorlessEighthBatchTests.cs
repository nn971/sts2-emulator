using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeColorlessEighthBatchTests
{
    [Theory]
    [InlineData("proto.colorless.fisticuffs")]
    [InlineData("proto.colorless.bolas")]
    [InlineData("proto.colorless.thrumming_hatchet")]
    [InlineData("proto.colorless.hidden_gem")]
    [InlineData("proto.colorless.rend")]
    [InlineData("proto.colorless.jackpot")]
    [InlineData("proto.colorless.fasten")]
    [InlineData("proto.colorless.prep_time")]
    public void EighthBatchCardsAreMerchantCardsAndExcludedFromSilentRewards(string id)
    {
        var definition = PrototypeContent.Card(id);
        Assert.True(definition.MechanicsImplemented);
        Assert.False(definition.MultiplayerOnly);
        Assert.Contains(id, PrototypeColorlessCards.NativePoolIds);
        Assert.Contains(id, PrototypeColorlessCards.ImplementedShopPool);
        Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(1, 9)]
    public void FisticuffsConvertsDamageIncludingEnemyBlockToOwnBlock(
        int upgrade, int attack)
    {
        var state = Setup("fisticuffs-" + upgrade,
            ["proto.colorless.fisticuffs"], [1], [], upgrade);
        state = WithEnemyBlock(state, 4);
        state = Play(state, 1, 1);
        var combat = state.World!.Combat!;
        Assert.Equal(100 - (attack - 4), combat.Enemies[0].Hp);
        Assert.Equal(0, combat.Enemies[0].Block);
        Assert.Equal(attack, combat.PlayerBlock);
        Assert.Equal(2, combat.Energy);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData("proto.colorless.bolas", 0, 3, 3)]
    [InlineData("proto.colorless.bolas", 1, 4, 3)]
    [InlineData("proto.colorless.thrumming_hatchet", 0, 11, 2)]
    [InlineData("proto.colorless.thrumming_hatchet", 1, 14, 2)]
    public void ReboundingAttacksReturnThePlayedInstanceBeforeNextHandDraw(
        string id, int upgrade, int damage, int energy)
    {
        var state = Setup("rebound-" + id + upgrade,
            [id, "proto.silent.strike", "proto.silent.defend"],
            [1], [2, 3], upgrade);
        state = Play(state, 1, 1);
        Assert.Equal(100 - damage, state.World!.Combat!.Enemies[0].Hp);
        Assert.Contains(1, state.World.Combat.DiscardPile);
        state = EndTurn(state);
        var combat = state.World!.Combat!;
        Assert.Contains(1, combat.Hand);
        Assert.DoesNotContain(1, combat.DiscardPile);
        Assert.DoesNotContain(1, combat.DrawPile);
        Assert.Equal(2, combat.Turn);
        Assert.Equal(3, combat.Energy);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    public void HiddenGemAddsReplayToOnePreferredDrawCard(int upgrade, int repeats)
    {
        var state = Setup("hidden-gem-" + upgrade,
            ["proto.colorless.hidden_gem", "proto.silent.strike",
             "proto.silent.defend", "proto.colorless.finesse"],
            [1], [2, 3, 4], upgrade);
        var copy = state.Fork();
        state = Play(state, 1);
        copy = Play(copy, 1);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(copy));
        var affected = state.World!.Combat!.Cards
            .Where(c => c.InstanceId != 1 && c.ReplayCount != 0).ToArray();
        var chosen = Assert.Single(affected);
        Assert.Equal(repeats, chosen.ReplayCount);
        Assert.Contains(chosen.InstanceId, state.World.Combat.DrawPile);
        Assert.Equal(2, state.World.Combat.Energy);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void HiddenGemHasNoEffectWhenDrawPileEmpty()
    {
        var state = Setup("empty-gem",
            ["proto.colorless.hidden_gem"], [1], []);
        state = Play(state, 1);
        Assert.Single(state.World!.Combat!.Cards);
        Assert.Contains(1, state.World.Combat.DiscardPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 28)]
    public void RendCountsDistinctEnemyDebuffs(int upgrade, int damage)
    {
        var state = Setup("rend-" + upgrade,
            ["proto.colorless.rend"], [1], [], upgrade);
        var combat = state.World!.Combat!;
        var enemies = combat.Enemies.Select(e => e.Fork()).ToArray();
        enemies[0] = enemies[0] with
        {
            Statuses = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["proto.status.weak"] = 2,
                ["proto.status.poison"] = 8
            }
        };
        state = state with
        {
            World = state.World with
            {
                Combat = combat with { Enemies = enemies }
            }
        };
        state = Play(state, 1, 1);
        Assert.Equal(100 - damage, state.World!.Combat!.Enemies[0].Hp);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 30)]
    public void JackpotCreatesThreeTemporaryZeroCostSilentCards(
        int upgrade, int damage)
    {
        var state = Setup("jackpot-" + upgrade,
            ["proto.colorless.jackpot"], [1], [], upgrade);
        var fork = state.Fork();
        state = Play(state, 1, 1);
        fork = Play(fork, 1, 1);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        var combat = state.World!.Combat!;
        Assert.Equal(100 - damage, combat.Enemies[0].Hp);
        Assert.Equal(0, combat.Energy);
        Assert.Equal(3, combat.Hand.Length);
        Assert.All(combat.Hand, id =>
        {
            var card = combat.Cards.Single(c => c.InstanceId == id);
            Assert.True(card.IsTemporary);
            Assert.Equal(upgrade, card.UpgradeLevel);
            var definition = PrototypeContent.Card(card.CardId);
            Assert.Equal(0, definition.Cost.Amount);
            Assert.Contains(card.CardId, PrototypeContent.RewardCardPool);
        });
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 11)]
    public void FastenBuffsTaggedDefendOnly(int upgrade, int expectedBlock)
    {
        var state = Setup("fasten-" + upgrade,
            ["proto.colorless.fasten", "proto.silent.defend",
             "proto.silent.survivor"],
            [1, 2, 3], [], upgrade);
        state = Play(state, 1);
        Assert.Equal(4 + 2 * upgrade,
            Assert.Single(state.World!.Combat!.PlayerPowers).Stacks);
        state = Play(state, 2);
        Assert.Equal(expectedBlock, state.World!.Combat!.PlayerBlock);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 6)]
    public void PrepTimeGrantsVigorAtNextTurnStart(int upgrade, int strength)
    {
        var state = Setup("prep-" + upgrade,
            ["proto.colorless.prep_time", "proto.silent.strike"],
            [1], [2], upgrade);
        state = Play(state, 1);
        Assert.Equal("proto.power.prep_time",
            Assert.Single(state.World!.Combat!.PlayerPowers).PowerId);
        state = EndTurn(state);
        Assert.Contains(state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.vigor"
                && power.Stacks == strength);
        Assert.Contains(state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.prep_time");
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState WithEnemyBlock(RunState state, int block)
    {
        var combat = state.World!.Combat!;
        var enemies = combat.Enemies.Select(e => e.Fork()).ToArray();
        enemies[0] = enemies[0] with { Block = block };
        return state with
        {
            World = state.World with
            {
                Combat = combat with { Enemies = enemies }
            }
        };
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

    private static RunState EndTurn(RunState state)
    {
        var engine = new PrototypeGameEngine();
        return engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;
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
