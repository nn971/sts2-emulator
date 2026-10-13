using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeColorlessNinthBatchTests
{
    [Theory]
    [InlineData("proto.colorless.alchemize")]
    [InlineData("proto.colorless.automation")]
    [InlineData("proto.colorless.beat_down")]
    [InlineData("proto.colorless.calamity")]
    [InlineData("proto.colorless.catastrophe")]
    [InlineData("proto.colorless.eternal_armor")]
    [InlineData("proto.colorless.nostalgia")]
    [InlineData("proto.colorless.omnislice")]
    [InlineData("proto.colorless.rolling_boulder")]
    [InlineData("proto.colorless.the_gambit")]
    public void NewCardsArePlayableSoloAndNotSilentRewards(string id)
    {
        var card = PrototypeContent.Card(id);
        Assert.True(card.MechanicsImplemented);
        Assert.False(card.MultiplayerOnly);
        Assert.Contains(id, PrototypeColorlessCards.NativePoolIds);
        Assert.Contains(id, PrototypeColorlessCards.ImplementedShopPool);
        Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    public void AlchemizeProcuresPotionAndExhausts(int upgrade, int energy)
    {
        var state = Setup("alchemize-" + upgrade,
            ["proto.colorless.alchemize"], [1], [], upgrade: upgrade);
        var fork = state.Fork();
        state = Play(state, 1);
        fork = Play(fork, 1);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        Assert.NotNull(state.Player.PotionSlots[0]);
        Assert.Contains(state.Player.PotionSlots[0]!.PotionId,
            PrototypeContent.PotionPool);
        Assert.Equal(energy, state.World!.Combat!.Energy);
        Assert.Contains(1, state.World.Combat.ExhaustPile);
        Assert.False(PrototypeContent.Card("proto.colorless.alchemize")
            .CanBeGeneratedInCombat);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void AutomationPaysEnergyOnTenthDrawAndKeepsIndependentCounter()
    {
        var state = Setup("automation",
            ["proto.colorless.finesse", "proto.silent.strike"],
            [1], [2]);
        var combat = state.World!.Combat!;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    PlayerPowers =
                    [
                        new PrototypePowerInstanceState(
                            "proto.power.automation", 1, 1,
                            TriggerCounts: [9])
                    ],
                    NextPowerApplicationOrder = 2
                }
            }
        };
        state = Play(state, 1);
        Assert.Equal(4, state.World!.Combat!.Energy);
        Assert.Contains(2, state.World.Combat.Hand);
        Assert.Equal(0, Assert.Single(
            state.World.Combat.PlayerPowers).TriggerCounts![0]);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void CalamityGeneratesAttackOnlyAfterAttackPlays()
    {
        var state = Setup("calamity",
            ["proto.colorless.calamity", "proto.silent.strike"],
            [1, 2], [], upgrade: 1);
        state = Play(state, 1);
        Assert.Equal(1, state.World!.Combat!.Energy);
        Assert.Single(state.World.Combat.Hand);
        state = Play(state, 2, 1);
        var combat = state.World!.Combat!;
        var generatedId = Assert.Single(combat.Hand);
        var generated = combat.Cards.Single(c => c.InstanceId == generatedId);
        Assert.True(generated.IsTemporary);
        Assert.Equal(PrototypeCardType.Attack,
            PrototypeContent.Card(generated.CardId).Type);
        Assert.Contains(combat.PlayerPowers, p =>
            p.PowerId == "proto.power.calamity");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void CatastropheAutoPlaysRandomDrawCardsWithoutPayingTheirCosts()
    {
        var state = Setup("catastrophe",
            ["proto.colorless.catastrophe",
             "proto.silent.strike", "proto.silent.defend"],
            [1], [2, 3]);
        state = Play(state, 1);
        var combat = state.World!.Combat!;
        Assert.Equal(1, combat.Energy);
        Assert.Empty(combat.DrawPile);
        Assert.Contains(2, combat.DiscardPile);
        Assert.Contains(3, combat.DiscardPile);
        Assert.Equal(194, combat.Enemies.Sum(e => e.Hp));
        Assert.Equal(5, combat.PlayerBlock);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void BeatDownAutoPlaysThreeDiscardAttacksWithRandomTargets()
    {
        var state = Setup("beat-down",
            ["proto.colorless.beat_down",
             "proto.silent.strike", "proto.silent.strike",
             "proto.silent.strike"],
            [1], [], [2, 3, 4]);
        var fork = state.Fork();
        state = Play(state, 1);
        fork = Play(fork, 1);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        var combat = state.World!.Combat!;
        Assert.Equal(0, combat.Energy);
        Assert.Equal(182, combat.Enemies.Sum(e => e.Hp));
        Assert.Contains(2, combat.DiscardPile);
        Assert.Contains(3, combat.DiscardPile);
        Assert.Contains(4, combat.DiscardPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 12)]
    public void EternalArmorAddsPlatingThatDecaysAfterTurnOne(
        int upgrade, int stacks)
    {
        var state = Setup("eternal-armor-" + upgrade,
            ["proto.colorless.eternal_armor"], [1], [], upgrade: upgrade);
        state = Play(state, 1);
        Assert.Equal(stacks,
            Assert.Single(state.World!.Combat!.PlayerPowers).Stacks);
        state = EndTurn(state);
        Assert.Equal(stacks - 1, Assert.Single(
            state.World!.Combat!.PlayerPowers).Stacks);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void NostalgiaMovesFirstAttackToTopDrawAndSecondSkillToDiscard()
    {
        var state = Setup("nostalgia",
            ["proto.colorless.nostalgia",
             "proto.silent.strike", "proto.silent.defend"],
            [1, 2, 3], []);
        state = Play(state, 1);
        state = Play(state, 2, 1);
        Assert.Contains(2, state.World!.Combat!.DrawPile);
        Assert.DoesNotContain(2, state.World.Combat.DiscardPile);
        state = Play(state, 3);
        Assert.Contains(3, state.World!.Combat!.DiscardPile);
        Assert.DoesNotContain(3, state.World.Combat.DrawPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 96, 92)]
    [InlineData(1, 93, 89)]
    public void OmnislicePrimaryBlockedHitEchoesUnpoweredDamage(
        int upgrade, int primaryHp, int secondaryHp)
    {
        var state = Setup("omnislice-" + upgrade,
            ["proto.colorless.omnislice"], [1], [], upgrade: upgrade);
        var combat = state.World!.Combat!;
        var enemies = combat.Enemies.Select(e => e.Fork()).ToArray();
        enemies[0] = enemies[0] with { Block = 4 };
        state = state with
        {
            World = state.World with
            {
                Combat = combat with { Enemies = enemies }
            }
        };
        state = Play(state, 1, 1);
        Assert.Equal(primaryHp, state.World!.Combat!.Enemies[0].Hp);
        Assert.Equal(secondaryHp, state.World.Combat.Enemies[1].Hp);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void RollingBoulderRampsEverySubsequentPlayerTurn()
    {
        var state = Setup("rolling-boulder",
            ["proto.colorless.rolling_boulder"], [1], []);
        state = Play(state, 1);
        state = EndTurn(state);
        var combat = state.World!.Combat!;
        Assert.Equal(190, combat.Enemies.Sum(e => e.Hp));
        Assert.Equal(10, Assert.Single(combat.PlayerPowers).Stacks);
        state = EndTurn(state);
        Assert.Equal(170, state.World!.Combat!.Enemies.Sum(e => e.Hp));
        Assert.Equal(15, Assert.Single(
            state.World.Combat.PlayerPowers).Stacks);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 75)]
    public void TheGambitGrantsBlockAndAppliesLethalDownside(
        int upgrade, int block)
    {
        var state = Setup("gambit-" + upgrade,
            ["proto.colorless.the_gambit"], [1], [], upgrade: upgrade);
        state = Play(state, 1);
        Assert.Equal(block, state.World!.Combat!.PlayerBlock);
        Assert.Contains(state.World.Combat.PlayerPowers, power =>
            power.PowerId == "proto.power.the_gambit");
        var combat = state.World.Combat;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with { PlayerBlock = 0 }
            }
        };
        state = EndTurn(state);
        Assert.Equal(0, state.Player.Hp);
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
        long[]? discard = null, int upgrade = 0)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = cardIds.Select((id, i) =>
            new CardInstance(i + 1, id, i == 0 ? upgrade : 0, empty)).ToArray();
        var cards = deck.Select(c => new CombatCardInstance(
            c.InstanceId, c.InstanceId, c.CardId, c.UpgradeLevel,
            false, empty)).ToArray();
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: hand, DrawPile: draw, DiscardPile: discard ?? [],
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
