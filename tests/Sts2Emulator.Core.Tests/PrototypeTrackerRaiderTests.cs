using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeTrackerRaiderTests
{
    [Fact]
    public void DefinitionMatchesPinnedAct1Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.tracker_ruby_raider");

        Assert.Equal("Tracker Raider", enemy.Name);
        Assert.Equal((21, 25), enemy.HpRangeAt(1, 0));
        Assert.Equal((22, 26), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.StateMachine,
            enemy.MovePolicy);

        Assert.Collection(
            enemy.Moves,
            track =>
            {
                Assert.Equal("track", track.Id);
                var frail = Assert.Single(track.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyPlayerPower,
                    frail.Kind);
                Assert.Equal("proto.power.frail", frail.PowerId);
                Assert.Equal(2, frail.Amount);
            },
            hounds =>
            {
                Assert.Equal("hounds", hounds.Id);
                var damage = Assert.Single(hounds.Effects);
                Assert.Equal(1, damage.AmountAt(1, 9));
                Assert.Equal(8, damage.RepetitionsAt(8));
                Assert.Equal(9, damage.RepetitionsAt(9));
            });

        var ai = Assert.IsType<
            PrototypeEnemyAiDefinition>(enemy.Ai);
        Assert.Equal("track", ai.InitialStateId);
        Assert.Equal(
            "hounds",
            Assert.Single(
                ai.States,
                state => state.Id == "track")
                .NextStateId);
        Assert.Equal(
            "hounds",
            Assert.Single(
                ai.States,
                state => state.Id == "hounds")
                .NextStateId);
    }

    [Fact]
    public void TracksOnceThenUsesHoundsForever()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 0);

        state = EndTurn(engine, state);

        Assert.Equal(100, state.Player.Hp);
        Assert.Equal(
            "track",
            Assert.Single(
                state.World!.Combat!.Enemies)
                .LastMoveId);
        Assert.Equal(
            2,
            Assert.Single(
                state.World.Combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.frail")
                .Stacks);

        state = EndTurn(engine, state);

        Assert.Equal(92, state.Player.Hp);
        Assert.Equal(
            "hounds",
            Assert.Single(
                state.World!.Combat!.Enemies)
                .LastMoveId);
        Assert.Equal(
            1,
            Assert.Single(
                state.World.Combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.frail")
                .Stacks);

        state = EndTurn(engine, state);

        Assert.Equal(84, state.Player.Hp);
        Assert.Equal(
            "hounds",
            Assert.Single(
                state.World!.Combat!.Enemies)
                .LastMoveId);
        Assert.DoesNotContain(
            state.World.Combat.PlayerPowers,
            power => power.PowerId
                == "proto.power.frail");
    }

    [Fact]
    public void A9HoundsUsesNineHits()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 9);

        state = EndTurn(engine, state);
        state = EndTurn(engine, state);

        Assert.Equal(91, state.Player.Hp);
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
                    "proto.enemy.tracker_ruby_raider",
                    ascension >= 8 ? 26 : 25,
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
            "tracker-raider-test",
            "tracker-raider-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "tracker-raider-test"),
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
