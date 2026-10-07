using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeBruteRaiderTests
{
    [Fact]
    public void DefinitionMatchesPinnedAct1Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.brute_ruby_raider");

        Assert.Equal("Brute Raider", enemy.Name);
        Assert.Equal((30, 33), enemy.HpRangeAt(1, 0));
        Assert.Equal((31, 34), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.SequentialLoop,
            enemy.MovePolicy);

        var beat = Assert.Single(enemy.Moves);
        Assert.Equal("beat", beat.Id);
        var damage = Assert.Single(beat.Effects);
        Assert.Equal(7, damage.AmountAt(1, 8));
        Assert.Equal(8, damage.AmountAt(1, 9));
    }

    [Fact]
    public void BeatRepeatsEveryTurnAndScalesAtA9()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 9);

        state = EndTurn(engine, state);
        Assert.Equal(92, state.Player.Hp);
        Assert.Equal(
            "beat",
            Assert.Single(
                state.World!.Combat!.Enemies)
                .LastMoveId);

        state = EndTurn(engine, state);
        Assert.Equal(84, state.Player.Hp);
        Assert.Equal(
            "beat",
            Assert.Single(
                state.World!.Combat!.Enemies)
                .LastMoveId);
    }

    private static RunState CreateState(int ascension)
    {
        var player = new PlayerState(
            100,
            100,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.brute_ruby_raider",
                    ascension >= 8 ? 34 : 33,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "brute-raider-test",
            "brute-raider-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "brute-raider-test"),
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

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }
}
