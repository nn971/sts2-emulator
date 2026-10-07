using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSlimeStatusTests
{
    [Fact]
    public void SlimedMatchesPinnedStatusCardSemantics()
    {
        var card = PrototypeContent.Card("proto.status.slimed");

        Assert.Equal("Slimed", card.Name);
        Assert.Equal(1, card.Cost.Amount);
        Assert.Equal(PrototypeCardRarity.Status, card.Rarity);
        Assert.Equal(PrototypeCardType.Status, card.Type);
        Assert.True(card.ExhaustOnUse);
        Assert.False(card.RewardEligible);

        var draw = Assert.Single(card.Effects);
        Assert.Equal(PrototypeCombatEffectKind.DrawCards, draw.Kind);
        Assert.Equal(1, draw.Amount);
    }

    [Fact]
    public void LeafSlimeSmallGoopAddsSlimedAndCannotRepeat()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.leaf_slime_s");

        Assert.Equal((11, 15), enemy.HpRangeAt(1, 0));
        Assert.Equal((12, 16), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.StateMachine,
            enemy.MovePolicy);

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            enemy.Id,
            enemy.HpRangeAt(1, 0).Max,
            aiStateId: "goop_move");

        state = EndTurn(engine, state);

        var combat = state.World!.Combat!;
        var slimedId = Assert.Single(combat.DiscardPile);
        Assert.Equal(
            "proto.status.slimed",
            Assert.Single(
                combat.Cards,
                card => card.InstanceId == slimedId)
                .CardId);
        Assert.Equal(
            "goop",
            Assert.Single(combat.Enemies).LastMoveId);

        state = EndTurn(engine, state);

        Assert.Equal(97, state.Player.Hp);
        Assert.Equal(
            "tackle",
            Assert.Single(
                state.World!.Combat!.Enemies).LastMoveId);
    }

    [Fact]
    public void LeafSlimeMediumAlternatesStickyShotAndClumpShot()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.leaf_slime_m");

        Assert.Equal((32, 35), enemy.HpRangeAt(1, 0));
        Assert.Equal((33, 36), enemy.HpRangeAt(1, 8));

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            enemy.Id,
            enemy.HpRangeAt(1, 0).Max);

        state = EndTurn(engine, state);

        var combat = state.World!.Combat!;
        Assert.Equal(
            "sticky_shot",
            Assert.Single(combat.Enemies).LastMoveId);
        Assert.Equal(2, combat.DiscardPile.Length);
        Assert.All(
            combat.DiscardPile,
            id => Assert.Equal(
                "proto.status.slimed",
                Assert.Single(
                    combat.Cards,
                    card => card.InstanceId == id)
                    .CardId));

        state = EndTurn(engine, state);

        Assert.Equal(92, state.Player.Hp);
        Assert.Equal(
            "clump_shot",
            Assert.Single(
                state.World!.Combat!.Enemies).LastMoveId);

        state = EndTurn(engine, state);

        Assert.Equal(4, state.World!.Combat!.DiscardPile.Length);
        Assert.Equal(
            "sticky_shot",
            Assert.Single(
                state.World.Combat.Enemies).LastMoveId);
    }

    [Fact]
    public void A9LeafSlimeAttacksUseScaledDamage()
    {
        var engine = new PrototypeGameEngine();

        var small = CreateState(
            "proto.enemy.leaf_slime_s",
            16,
            ascension: 9,
            aiStateId: "tackle_move");
        small = EndTurn(engine, small);
        Assert.Equal(96, small.Player.Hp);

        var medium = CreateState(
            "proto.enemy.leaf_slime_m",
            36,
            ascension: 9,
            moveIndex: 1);
        medium = EndTurn(engine, medium);
        Assert.Equal(91, medium.Player.Hp);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState CreateState(
        string enemyId,
        int hp,
        int ascension = 0,
        string? aiStateId = null,
        int moveIndex = 0)
    {
        var player = new PlayerState(
            100,
            100,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var fillerCards = Enumerable.Range(1, 5)
            .Select(instanceId =>
                new CombatCardInstance(
                    instanceId,
                    1000 + instanceId,
                    "proto.silent.strike",
                    0,
                    false,
                    PrototypeJson.EmptyObject()))
            .ToArray();

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [],
            DrawPile: fillerCards
                .Select(card => card.InstanceId)
                .ToArray(),
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    enemyId,
                    hp,
                    0,
                    moveIndex,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    AiStateId: aiStateId,
                    MoveUseCounts:
                        new Dictionary<string, int>(
                            StringComparer.Ordinal))
            ],
            NextCardInstanceId: 6,
            Cards: fillerCards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "slime-status-test",
            "slime-status-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "slime-status-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null),
            Ascension: ascension);
    }
}
