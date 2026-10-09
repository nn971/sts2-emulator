using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeColorlessTemporaryDebuffTests
{
    [Theory]
    [InlineData("proto.colorless.panic_button")]
    [InlineData("proto.colorless.dark_shackles")]
    public void NewColorlessCardsAreShopPlayableButNotSilentRewards(string id)
    {
        var definition = PrototypeContent.Card(id);
        Assert.Contains(id, PrototypeColorlessCards.NativePoolIds);
        Assert.Contains(id, PrototypeColorlessCards.ImplementedShopPool);
        Assert.Equal(PrototypeCardRarity.Uncommon, definition.Rarity);
        Assert.True(definition.MechanicsImplemented);
        Assert.True(definition.ExhaustOnUse);
        Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 40)]
    public void PanicButtonGrantsBlockBeforeDisablingFurtherCardBlock(
        int upgrade, int initialBlock)
    {
        var state = Setup("panic-" + upgrade,
            ["proto.colorless.panic_button", "proto.silent.defend"],
            [1, 2], [], upgrade);
        state = Play(state, 1);
        Assert.Equal(initialBlock, state.World!.Combat!.PlayerBlock);
        Assert.Equal(2, Assert.Single(state.World.Combat.PlayerPowers).Stacks);
        Assert.Equal("proto.power.no_block",
            Assert.Single(state.World.Combat.PlayerPowers).PowerId);

        state = Play(state, 2);
        Assert.Equal(initialBlock, state.World!.Combat!.PlayerBlock);
        Assert.Equal(3, state.World.Combat.Energy);
        Assert.Contains(1, state.World.Combat.ExhaustPile);
        Assert.Contains(2, state.World.Combat.DiscardPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void PanicButtonDoesNotPreventBlockGrantedByOtherPowers()
    {
        var state = Setup("panic-with-afterimage",
            ["proto.colorless.panic_button", "proto.silent.defend"],
            [1, 2], [],
            initialPlayerPowers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.afterimage", 1, 1)
            ]);
        state = Play(state, 1);
        Assert.Equal(31, state.World!.Combat!.PlayerBlock);
        state = Play(state, 2);
        // Defend supplies zero; Afterimage still adds one.
        Assert.Equal(32, state.World!.Combat!.PlayerBlock);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void NoBlockExpiresAfterTwoEnemySideTurnEndings()
    {
        var state = Setup("panic-countdown",
            ["proto.colorless.panic_button", "proto.silent.defend"],
            [1], [2]);
        state = Play(state, 1);
        state = EndTurn(state);
        Assert.Equal(1,
            Assert.Single(state.World!.Combat!.PlayerPowers).Stacks);
        state = EndTurn(state);
        Assert.DoesNotContain(state.World!.Combat!.PlayerPowers,
            p => p.PowerId == "proto.power.no_block");
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 15)]
    public void DarkShacklesTemporarilyReducesAndThenRestoresStrength(
        int upgrade, int strengthLoss)
    {
        var state = Setup("shackles-" + upgrade,
            ["proto.colorless.dark_shackles", "proto.silent.defend"],
            [1], [2], upgrade, firstEnemyStrength: 3);
        state = Play(state, 1, 1);
        var target = state.World!.Combat!.Enemies[0];
        Assert.Equal(3 - strengthLoss,
            target.PowerStates.Single(p => p.PowerId == "proto.power.strength").Stacks);
        Assert.Equal(strengthLoss,
            target.PowerStates.Single(p => p.PowerId == "proto.power.dark_shackles").Stacks);
        Assert.DoesNotContain(state.World.Combat.Enemies[1].PowerStates,
            p => p.PowerId == "proto.power.dark_shackles");
        Assert.Contains(1, state.World.Combat.ExhaustPile);

        state = EndTurn(state);
        target = state.World!.Combat!.Enemies[0];
        Assert.Equal(3,
            target.PowerStates.Single(p => p.PowerId == "proto.power.strength").Stacks);
        Assert.DoesNotContain(target.PowerStates,
            p => p.PowerId == "proto.power.dark_shackles");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void DarkShacklesStacksAndRestoresTheFullAppliedAmount()
    {
        var state = Setup("shackles-stack",
            ["proto.colorless.dark_shackles", "proto.colorless.dark_shackles"],
            [1, 2], [], firstEnemyStrength: 3);
        state = Play(state, 1, 1);
        state = Play(state, 2, 1);
        var enemy = state.World!.Combat!.Enemies[0];
        Assert.Equal(-15, enemy.PowerStates.Single(p =>
            p.PowerId == "proto.power.strength").Stacks);
        Assert.Equal(18, enemy.PowerStates.Single(p =>
            p.PowerId == "proto.power.dark_shackles").Stacks);
        state = EndTurn(state);
        enemy = state.World!.Combat!.Enemies[0];
        Assert.Equal(3, enemy.PowerStates.Single(p =>
            p.PowerId == "proto.power.strength").Stacks);
        Assert.DoesNotContain(enemy.PowerStates,
            p => p.PowerId == "proto.power.dark_shackles");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ArtifactBlocksDarkShacklesWithoutApplyingStrengthLoss()
    {
        var state = Setup("shackles-artifact",
            ["proto.colorless.dark_shackles"],
            [1], [], firstEnemyStrength: 3,
            firstEnemyArtifact: 1);
        state = Play(state, 1, 1);
        var target = state.World!.Combat!.Enemies[0];
        Assert.Equal(3, target.PowerStates.Single(p =>
            p.PowerId == "proto.power.strength").Stacks);
        Assert.DoesNotContain(target.PowerStates,
            p => p.PowerId == "proto.power.dark_shackles");
        Assert.DoesNotContain(target.PowerStates,
            p => p.PowerId == "proto.power.artifact");
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState Play(RunState state, long cardId, int? target = null)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>().CardInstanceId == cardId
                && (target is null ||
                    a.ReadPayload<PlayCardPayload>().TargetEnemyId == target));
        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(RunState state)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState Setup(
        string seed, string[] cardIds, long[] hand, long[] draw,
        int upgrade = 0, int firstEnemyStrength = 0,
        int firstEnemyArtifact = 0,
        PrototypePowerInstanceState[]? initialPlayerPowers = null)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = cardIds.Select((id, i) =>
            new CardInstance(i + 1, id, i == 0 ? upgrade : 0, empty))
            .ToArray();
        var cards = deck.Select(c =>
            new CombatCardInstance(c.InstanceId, c.InstanceId,
                c.CardId, c.UpgradeLevel, false, empty)).ToArray();
        var enemyPowers = new List<PrototypePowerInstanceState>();
        if (firstEnemyStrength != 0)
        {
            enemyPowers.Add(new PrototypePowerInstanceState(
                "proto.power.strength", firstEnemyStrength, 2));
        }
        if (firstEnemyArtifact != 0)
        {
            enemyPowers.Add(new PrototypePowerInstanceState(
                "proto.power.artifact", firstEnemyArtifact, 3));
        }
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: hand, DrawPile: draw, DiscardPile: [], ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(1, "proto.enemy.crawler", 100, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal),
                    Powers: enemyPowers.ToArray()),
                new EnemyCombatState(2, "proto.enemy.crawler", 100, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: cards.Length + 1,
            Cards: cards,
            PlayerPowers: initialPlayerPowers ?? [],
            NextPowerApplicationOrder: 4);
        return new RunState(
            "prototype-unbound", "prototype-0.1", seed, seed, 0,
            RunPhase.Combat,
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
