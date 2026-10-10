using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeOvergrowthSimpleEliteTests
{
    [Fact]
    public void BygoneEffigyDefinitionMatchesPinnedData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.bygone_effigy");

        Assert.Equal("Bygone Effigy", enemy.Name);
        Assert.Equal((127, 127), enemy.HpRangeAt(1, 0));
        Assert.Equal((132, 132), enemy.HpRangeAt(1, 8));
        Assert.Equal(2, enemy.MoveLoopStartIndex);

        Assert.Collection(
            enemy.Moves,
            sleep =>
            {
                Assert.Equal("sleep", sleep.Id);
                Assert.Empty(sleep.Effects);
            },
            wake =>
            {
                Assert.Equal("wake", wake.Id);
                var strength = Assert.Single(wake.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyEnemyPower,
                    strength.Kind);
                Assert.Equal("proto.power.strength", strength.PowerId);
                Assert.Equal(10, strength.Amount);
            },
            slashes =>
            {
                Assert.Equal("slashes", slashes.Id);
                var damage = Assert.Single(slashes.Effects);
                Assert.Equal(13, damage.AmountAt(1, 8));
                Assert.Equal(15, damage.AmountAt(1, 9));
            });

        var slow = Assert.Single(enemy.StartingPowers!);
        Assert.Equal("proto.power.slow", slow.PowerId);
    }

    [Fact]
    public void BygoneEffigySleepsWakesThenSlashesForever()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEnemyState(
            "proto.enemy.bygone_effigy",
            hp: 127);

        state = EndTurn(engine, state);
        Assert.Equal(100, state.Player.Hp);
        AssertMove(state, "sleep");

        state = EndTurn(engine, state);
        Assert.Equal(100, state.Player.Hp);
        AssertMove(state, "wake");
        Assert.Equal(
            10,
            StrengthOf(
                Assert.Single(
                    state.World!.Combat!.Enemies)));

        state = EndTurn(engine, state);
        Assert.Equal(77, state.Player.Hp);
        AssertMove(state, "slashes");

        state = EndTurn(engine, state);
        Assert.Equal(54, state.Player.Hp);
        AssertMove(state, "slashes");
    }

    [Fact]
    public void SlowUsesCardsPlayedThisTurnForAttackScaling()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEnemyState(
            "proto.enemy.bygone_effigy",
            hp: 127,
            cards:
            [
                new CombatCardInstance(
                    1,
                    null,
                    "proto.silent.slice",
                    0,
                    true,
                    PrototypeJson.EmptyObject()),
                new CombatCardInstance(
                    2,
                    null,
                    "proto.silent.slice",
                    0,
                    true,
                    PrototypeJson.EmptyObject())
            ]);

        state = PlayCard(engine, state, 1, 1);
        Assert.Equal(
            121,
            Assert.Single(
                state.World!.Combat!.Enemies).Hp);

        state = PlayCard(engine, state, 2, 1);
        // The second Slice sees just one previously completed play.
        Assert.Equal(
            115,
            Assert.Single(
                state.World!.Combat!.Enemies).Hp);
    }

    [Fact]
    public void ByrdonisDefinitionAndTerritorialCycleMatchPinnedData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.byrdonis");

        Assert.Equal("Byrdonis", enemy.Name);
        Assert.Equal((81, 84), enemy.HpRangeAt(1, 0));
        Assert.Equal((90, 90), enemy.HpRangeAt(1, 8));

        Assert.Collection(
            enemy.Moves,
            swoop =>
            {
                Assert.Equal("swoop", swoop.Id);
                var damage = Assert.Single(swoop.Effects);
                Assert.Equal(17, damage.AmountAt(1, 8));
                Assert.Equal(19, damage.AmountAt(1, 9));
            },
            peck =>
            {
                Assert.Equal("peck", peck.Id);
                var damage = Assert.Single(peck.Effects);
                Assert.Equal(3, damage.Repetitions);
                Assert.Equal(3, damage.AmountAt(1, 8));
                Assert.Equal(4, damage.AmountAt(1, 9));
            });

        var territorial = Assert.Single(enemy.StartingPowers!);
        Assert.Equal("proto.power.territorial", territorial.PowerId);
        Assert.Equal(1, territorial.Stacks);

        var engine = new PrototypeGameEngine();
        var state = CreateEnemyState(
            enemy.Id,
            hp: 84);

        state = EndTurn(engine, state);
        Assert.Equal(83, state.Player.Hp);
        AssertMove(state, "swoop");
        Assert.Equal(
            1,
            StrengthOf(
                Assert.Single(
                    state.World!.Combat!.Enemies)));

        state = EndTurn(engine, state);
        Assert.Equal(71, state.Player.Hp);
        AssertMove(state, "peck");
        Assert.Equal(
            2,
            StrengthOf(
                Assert.Single(
                    state.World!.Combat!.Enemies)));

        state = EndTurn(engine, state);
        Assert.Equal(52, state.Player.Hp);
        AssertMove(state, "swoop");
        Assert.Equal(
            3,
            StrengthOf(
                Assert.Single(
                    state.World!.Combat!.Enemies)));
    }

    [Fact]
    public void EliteReferenceFormationsAreSingletons()
    {
        var rng = PrototypeRng.CreateBundle(
            "overgrowth-simple-elites");

        var effigy = Assert.Single(
            PrototypeContent.Encounter(
                    "proto.encounter.bygone_effigy_elite")
                .ResolveEnemySpecs(rng));
        Assert.Equal(
            "proto.enemy.bygone_effigy",
            effigy.EnemyId);
        Assert.Equal(0, effigy.FormationPosition);

        var byrdonis = Assert.Single(
            PrototypeContent.Encounter(
                    "proto.encounter.byrdonis_elite")
                .ResolveEnemySpecs(rng));
        Assert.Equal(
            "proto.enemy.byrdonis",
            byrdonis.EnemyId);
        Assert.Equal(0, byrdonis.FormationPosition);
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardId,
        int enemyId)
    {
        var action = engine.GetLegalActions(state)
            .Single(candidate =>
            {
                if (candidate.Kind != "play_card")
                {
                    return false;
                }

                var payload =
                    candidate.ReadPayload<PlayCardPayload>();
                return payload.CardInstanceId == cardId
                    && payload.TargetEnemyId == enemyId;
            });

        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static void AssertMove(
        RunState state,
        string moveId) =>
        Assert.Equal(
            moveId,
            Assert.Single(
                state.World!.Combat!.Enemies)
                .LastMoveId);

    private static int StrengthOf(
        EnemyCombatState enemy) =>
        enemy.PowerStates
            .SingleOrDefault(power =>
                power.PowerId == "proto.power.strength")
            ?.Stacks ?? 0;

    private static RunState CreateEnemyState(
        string enemyId,
        int hp,
        CombatCardInstance[]? cards = null)
    {
        cards ??= [];
        var definition = PrototypeContent.Enemy(enemyId);
        var startingPowers =
            (definition.StartingPowers
                ?? Array.Empty<PrototypeStartingPowerSpec>())
            .Select((power, index) =>
                new PrototypePowerInstanceState(
                    power.PowerId,
                    power.Stacks,
                    index + 1L))
            .ToArray();

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
            Hand: cards.Select(card => card.InstanceId).ToArray(),
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    enemyId,
                    hp,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    Powers: startingPowers)
            ],
            NextCardInstanceId:
                cards.Length == 0
                    ? 1
                    : cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder:
                startingPowers.Length + 1L);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "overgrowth-simple-elite-test",
            "overgrowth-simple-elite-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "overgrowth-simple-elite-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Elite,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
