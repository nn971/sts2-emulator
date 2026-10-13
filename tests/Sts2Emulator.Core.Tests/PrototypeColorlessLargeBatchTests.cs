using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeColorlessLargeBatchTests
{
    [Theory]
    [InlineData("proto.colorless.discovery")]
    [InlineData("proto.colorless.jack_of_all_trades")]
    [InlineData("proto.colorless.scrawl")]
    [InlineData("proto.colorless.restlessness")]
    [InlineData("proto.colorless.prowess")]
    [InlineData("proto.colorless.equilibrium")]
    [InlineData("proto.colorless.production")]
    [InlineData("proto.colorless.prolong")]
    [InlineData("proto.colorless.salvo")]
    [InlineData("proto.colorless.seeker_strike")]
    public void SourceBackedCardsEnterShopButNotOrdinarySilentRewards(string id)
    {
        var definition = PrototypeContent.Card(id);
        Assert.Contains(id, PrototypeColorlessCards.NativePoolIds);
        Assert.Contains(id, PrototypeColorlessCards.ImplementedShopPool);
        Assert.True(definition.MechanicsImplemented);
        Assert.False(definition.MultiplayerOnly);
        Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
    }

    [Fact]
    public void NativeEpochGatesRestrictConservativeGenerationPool()
    {
        Assert.Equal(15, PrototypeColorlessCards.EpochGatedIds.Length);
        Assert.Equal(15, PrototypeColorlessCards.EpochGatedIds
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(PrototypeColorlessCards.EpochGatedIds, id =>
            Assert.Contains(id, PrototypeColorlessCards.NativePoolIds));
        Assert.DoesNotContain("proto.colorless.prowess",
            PrototypeColorlessCards.ImplementedCombatGenerationPool);
        Assert.DoesNotContain("proto.colorless.scrawl",
            PrototypeColorlessCards.ImplementedCombatGenerationPool);
        Assert.DoesNotContain("proto.colorless.hand_of_greed",
            PrototypeColorlessCards.ImplementedCombatGenerationPool);
        Assert.Contains("proto.colorless.discovery",
            PrototypeColorlessCards.ImplementedCombatGenerationPool);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void DiscoveryOffersDistinctOptionalClassCardsWithTemporaryZeroCost(
        int upgrade, bool exhausted)
    {
        var state = Setup("discovery-" + upgrade,
            ["proto.colorless.discovery", "proto.silent.strike",
             "proto.silent.defend"], [1, 2], [3], upgrade);
        state = Play(state, 1);
        var choice = state.World!.Combat!.PendingChoice!;
        Assert.Equal(3, choice.CandidateCardInstanceIds.Length);
        Assert.All(choice.CandidateCardInstanceIds, id =>
            Assert.Contains(state.World!.Combat!.Cards.Single(c =>
                c.InstanceId == id).CardId, PrototypeContent.RewardCardPool));
        Assert.Equal(3, choice.CandidateCardInstanceIds
            .Select(id => state.World!.Combat!.Cards.Single(c =>
                c.InstanceId == id).CardId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(0, choice.Selection.MinSelections);
        Assert.Equal(1, choice.Selection.MaxSelections);
        var preserved = state.Fork();
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(preserved));

        var selectedId = choice.CandidateCardInstanceIds[0];
        state = Select(state, [selectedId]);
        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Contains(selectedId, combat.Hand);
        var selected = combat.Cards.Single(c => c.InstanceId == selectedId);
        Assert.True(selected.IsTemporary);
        Assert.Equal(0, selected.TemporaryEnergyCost!.Cost);
        Assert.DoesNotContain(choice.CandidateCardInstanceIds.Skip(1),
            id => combat.Cards.Any(c => c.InstanceId == id));
        Assert.Equal(exhausted, combat.ExhaustPile.Contains(1));
        Assert.Equal(!exhausted, combat.DiscardPile.Contains(1));
        Assert.Equal(2, combat.Energy);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void DiscoveryMayBeSkippedWithoutLeakingTemporaryCards()
    {
        var state = Setup("discovery-skip",
            ["proto.colorless.discovery"], [1], []);
        state = Play(state, 1);
        var ids = state.World!.Combat!.PendingChoice!.CandidateCardInstanceIds;
        state = Select(state, []);
        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.DoesNotContain(ids, id => combat.Cards.Any(c => c.InstanceId == id));
        Assert.Contains(1, combat.ExhaustPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void JackGeneratesDistinctSupportedSoloCardsDeterministically(
        int upgrade, int count)
    {
        var state = Setup("jack-" + upgrade,
            ["proto.colorless.jack_of_all_trades"], [1], [], upgrade);
        var fork = state.Fork();
        state = Play(state, 1);
        fork = Play(fork, 1);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));

        var combat = state.World!.Combat!;
        Assert.Equal(count, combat.Hand.Length);
        var generated = combat.Hand
            .Select(id => combat.Cards.Single(c => c.InstanceId == id))
            .ToArray();
        Assert.All(generated, card =>
        {
            Assert.True(card.IsTemporary);
            Assert.Null(card.PersistentCardInstanceId);
            Assert.Contains(card.CardId,
                PrototypeColorlessCards.ImplementedCombatGenerationPool);
            Assert.NotEqual("proto.colorless.jack_of_all_trades", card.CardId);
        });
        Assert.Equal(count,
            generated.Select(c => c.CardId).Distinct().Count());
        Assert.Contains(1, combat.ExhaustPile);
        Assert.Equal(3, combat.Energy);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void ScrawlFillsHandAndGainsRetainOnUpgrade(
        int upgrade, bool retains)
    {
        var ids = new[] { "proto.colorless.scrawl" }
            .Concat(Enumerable.Repeat("proto.silent.strike", 12))
            .ToArray();
        var state = Setup("scrawl-" + upgrade, ids, [1, 2],
            Enumerable.Range(3, 11).Select(i => (long)i).ToArray(),
            upgrade);
        state = Play(state, 1);
        var combat = state.World!.Combat!;
        Assert.Equal(10, combat.Hand.Length);
        Assert.Equal(2, combat.DrawPile.Length);
        Assert.Contains(1, combat.ExhaustPile);
        Assert.Equal(retains, upgrade > 0 && PrototypeContent.Card(
            "proto.colorless.scrawl").RetainOnUpgrade);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 2, 5)]
    [InlineData(1, 3, 6)]
    public void RestlessnessOnlyActivatesWithNoOtherHandCards(
        int upgrade, int drawn, int energy)
    {
        var state = Setup("restless-" + upgrade,
            ["proto.colorless.restlessness", "proto.silent.strike",
             "proto.silent.defend", "proto.silent.strike"], [1],
            [2, 3, 4], upgrade);
        state = Play(state, 1);
        Assert.Equal(drawn, state.World!.Combat!.Hand.Length);
        Assert.Equal(energy, state.World.Combat.Energy);
        Assert.True(PrototypeContent.Card(
            "proto.colorless.restlessness").Retain);
        PrototypeStateInvariants.Validate(state);

        var blocked = Setup("restless-blocked-" + upgrade,
            ["proto.colorless.restlessness", "proto.silent.strike",
             "proto.silent.defend"], [1, 2], [3], upgrade);
        blocked = Play(blocked, 1);
        Assert.Equal(new long[] { 2 }, blocked.World!.Combat!.Hand);
        Assert.Equal(3, blocked.World.Combat.Energy);
        PrototypeStateInvariants.Validate(blocked);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void ProwessAppliesEqualStrengthAndDexterity(
        int upgrade, int amount)
    {
        var state = Setup("prowess-" + upgrade,
            ["proto.colorless.prowess"], [1], [], upgrade);
        state = Play(state, 1);
        Assert.Equal(2, state.World!.Combat!.PlayerPowers.Length);
        Assert.Equal(amount,
            state.World.Combat.PlayerPowers.Single(p =>
                p.PowerId == "proto.power.strength").Stacks);
        Assert.Equal(amount,
            state.World.Combat.PlayerPowers.Single(p =>
                p.PowerId == "proto.power.dexterity").Stacks);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 13)]
    [InlineData(1, 16)]
    public void EquilibriumGrantsBlockAndRetainsHandForTurn(
        int upgrade, int block)
    {
        var state = Setup("equilibrium-" + upgrade,
            ["proto.colorless.equilibrium", "proto.silent.strike"],
            [1, 2], [], upgrade);
        state = Play(state, 1);
        Assert.Equal(block, state.World!.Combat!.PlayerBlock);
        Assert.Equal(1, Assert.Single(state.World.Combat.PlayerPowers).Stacks);
        Assert.Equal("proto.power.retain_hand",
            Assert.Single(state.World.Combat.PlayerPowers).PowerId);
        state = EndTurn(state);
        Assert.Contains(2, state.World!.Combat!.Hand);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 6)]
    public void ProductionGainsEnergyThenExhausts(
        int upgrade, int energy)
    {
        var state = Setup("production-" + upgrade,
            ["proto.colorless.production"], [1], [], upgrade);
        state = Play(state, 1);
        Assert.Equal(energy, state.World!.Combat!.Energy);
        Assert.Contains(1, state.World.Combat.ExhaustPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void ProlongSavesCurrentBlockForNextTurn(
        int upgrade, bool exhausted)
    {
        var state = Setup("prolong-" + upgrade,
            ["proto.colorless.prolong"], [1], [], upgrade,
            startingBlock: 17);
        state = Play(state, 1);
        var combat = state.World!.Combat!;
        Assert.Equal(17,
            Assert.Single(combat.PlayerPowers).Stacks);
        Assert.Equal("proto.power.block_next_turn",
            Assert.Single(combat.PlayerPowers).PowerId);
        Assert.Equal(exhausted, combat.ExhaustPile.Contains(1));
        state = EndTurn(state);
        Assert.Equal(17, state.World!.Combat!.PlayerBlock);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 16)]
    public void SalvoDealsDamageAndRetainsRemainingHand(
        int upgrade, int damage)
    {
        var state = Setup("salvo-" + upgrade,
            ["proto.colorless.salvo", "proto.silent.defend"],
            [1, 2], [], upgrade);
        state = Play(state, 1, 1);
        Assert.Equal(100 - damage, state.World!.Combat!.Enemies[0].Hp);
        Assert.Equal("proto.power.retain_hand",
            Assert.Single(state.World.Combat.PlayerPowers).PowerId);
        state = EndTurn(state);
        Assert.Contains(2, state.World!.Combat!.Hand);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 12)]
    public void SeekerStrikeOffersThreeRandomDrawCardsToChooseOne(
        int upgrade, int damage)
    {
        var deck = new[] { "proto.colorless.seeker_strike" }
            .Concat(Enumerable.Repeat("proto.silent.strike", 6))
            .ToArray();
        var state = Setup("seeker-" + upgrade, deck, [1],
            [2, 3, 4, 5, 6, 7], upgrade);
        state = Play(state, 1, 1);
        var pending = state.World!.Combat!.PendingChoice!;
        Assert.Equal(3, pending.CandidateCardInstanceIds.Length);
        Assert.Equal(3, pending.CandidateCardInstanceIds.Distinct().Count());
        Assert.All(pending.CandidateCardInstanceIds,
            id => Assert.Contains(id, new long[] { 2, 3, 4, 5, 6, 7 }));
        Assert.Equal(1, pending.Selection.MinSelections);
        Assert.Equal(100 - damage, state.World.Combat.Enemies[0].Hp);
        var chosen = pending.CandidateCardInstanceIds[0];
        state = Select(state, [chosen]);
        Assert.Contains(chosen, state.World!.Combat!.Hand);
        Assert.DoesNotContain(chosen, state.World.Combat.DrawPile);
        Assert.Contains(1, state.World.Combat.DiscardPile);
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState Play(RunState state, long cardId, int? target = null)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>().CardInstanceId == cardId
                && (target is null
                    || a.ReadPayload<PlayCardPayload>().TargetEnemyId == target));
        return engine.Step(state, action).State;
    }

    private static RunState Select(RunState state, long[] ids)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(
            a => a.Kind == "select_cards"
                && a.ReadPayload<SelectCardsPayload>().CardInstanceIds
                    .SequenceEqual(ids));
        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(RunState state)
    {
        var engine = new PrototypeGameEngine();
        return engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;
    }

    private static RunState Setup(string seed, string[] cardIds,
        long[] hand, long[] draw, int sourceUpgrade = 0,
        int startingBlock = 0)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = cardIds.Select((id, i) =>
            new CardInstance(i + 1, id, i == 0 ? sourceUpgrade : 0, empty))
            .ToArray();
        var cards = deck.Select(c => new CombatCardInstance(
            c.InstanceId, c.InstanceId, c.CardId, c.UpgradeLevel,
            false, empty)).ToArray();
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: startingBlock,
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
            new PlayerState(70, 70, 100, deck, [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed), empty,
            new RunWorldState(
                PrototypeContent.RulesetId, PrototypeContent.CharacterId,
                1, 1, cards.Length + 1, PrototypeRoomType.Combat,
                new MapState([]), combat, null, null, null, null));
    }
}
