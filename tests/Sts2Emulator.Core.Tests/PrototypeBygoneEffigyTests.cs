using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeBygoneEffigyTests
{
    [Fact]
    public void DefinitionMatchesPinnedOvergrowthData()
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
        Assert.Equal(
            10,
            PrototypeContent.Power("proto.power.slow")
                .EnemyIncomingAttackDamagePercentPerCardPlayed);
    }

    [Fact]
    public void SleepWakeThenSlashesForever()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        state = EndTurn(engine, state);
        Assert.Equal(100, state.Player.Hp);
        AssertEnemy(state, "sleep", strength: 0);

        state = EndTurn(engine, state);
        Assert.Equal(100, state.Player.Hp);
        AssertEnemy(state, "wake", strength: 10);

        state = EndTurn(engine, state);
        Assert.Equal(77, state.Player.Hp);
        AssertEnemy(state, "slashes", strength: 10);

        state = EndTurn(engine, state);
        Assert.Equal(54, state.Player.Hp);
        AssertEnemy(state, "slashes", strength: 10);
    }

    [Fact]
    public void A9SlashesUsesScaledBaseDamagePlusWakeStrength()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 9);

        state = EndTurn(engine, state);
        state = EndTurn(engine, state);
        state = EndTurn(engine, state);

        Assert.Equal(75, state.Player.Hp);
    }

    [Fact]
    public void SlowCountsOnlyCompletedCardPlaysBeforeAttackDamage()
    {
        var dash = Card(
            1,
            "proto.silent.dash");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            cards: [dash],
            hand: [1]);

        var play = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1
                && action.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == 1);
        state = engine.Step(state, play).State;

        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(117, enemy.Hp);
        Assert.Equal(
            1,
            state.World.Combat.CounterState
                .CardsPlayedThisTurn);
    }

    [Fact]
    public void SlowCountsAllCardTypesPlayedEarlierThisTurn()
    {
        var defend = Card(
            1,
            "proto.silent.defend");
        var sucker = Card(
            2,
            "proto.silent.sucker_punch");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            cards: [defend, sucker],
            hand: [1, 2]);

        var defendAction = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1);
        state = engine.Step(state, defendAction).State;

        var suckerAction = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 2
                && action.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == 1);
        state = engine.Step(state, suckerAction).State;

        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(119, enemy.Hp);
        Assert.Equal(
            2,
            state.World.Combat.CounterState
                .CardsPlayedThisTurn);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(2, 8)]
    [InlineData(3, 10)]
    public void CapturedA10SlowDaggerSprayHitsUsePreviouslyCompletedPlays(
        int previousDefends, int expectedDamage)
    {
        // Oracle combat #6, Bygone Effigy, A10: Dagger Spray hits
        // 4+4 when the 1st or 3rd card played, 5+5 when the 4th.
        // Native SlowPower increments on AfterCardPlayed rather
        // than before the current card's attack resolves.
        var cards = new[]
        {
            Card(1, "proto.silent.defend"),
            Card(2, "proto.silent.defend"),
            Card(3, "proto.silent.defend"),
            Card(4, "proto.silent.dagger_spray")
        };
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            ascension: 10,
            cards: cards,
            hand: cards.Select(card => card.InstanceId).ToArray());
        state = state with
        {
            World = state.World! with
            {
                Combat = state.World.Combat! with { Energy = 4 }
            }
        };
        for (var i = 0; i < previousDefends; i++)
        {
            var id = cards[i].InstanceId;
            var defend = engine.GetLegalActions(state).Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == id);
            state = engine.Step(state, defend).State;
        }

        var before = Assert.Single(state.World!.Combat!.Enemies).Hp;
        var spray = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == 4);
        state = engine.Step(state, spray).State;

        var after = Assert.Single(state.World!.Combat!.Enemies).Hp;
        Assert.Equal(expectedDamage, before - after);
        Assert.Equal(previousDefends + 1,
            state.World.Combat.CounterState.CardsPlayedThisTurn);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ReferenceEliteFormationIsSingleton()
    {
        var specs = PrototypeContent.Encounter(
                "proto.encounter.bygone_effigy_elite")
            .ResolveEnemySpecs(
                PrototypeRng.CreateBundle(
                    "effigy-formation"));

        var spec = Assert.Single(specs);
        Assert.Equal(
            "proto.enemy.bygone_effigy",
            spec.EnemyId);
        Assert.Equal(0, spec.FormationPosition);
    }

    private static CombatCardInstance Card(
        long instanceId,
        string cardId) =>
        new(
            instanceId,
            null,
            cardId,
            0,
            true,
            PrototypeJson.EmptyObject());

    private static void AssertEnemy(
        RunState state,
        string moveId,
        int strength)
    {
        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(moveId, enemy.LastMoveId);
        Assert.Equal(
            strength,
            enemy.PowerStates
                .SingleOrDefault(power =>
                    power.PowerId == "proto.power.strength")
                ?.Stacks ?? 0);
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
        int ascension = 0,
        CombatCardInstance[]? cards = null,
        long[]? hand = null)
    {
        cards ??= [];
        hand ??= [];

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
            Hand: hand,
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.bygone_effigy",
                    ascension >= 8 ? 132 : 127,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    Powers:
                    [
                        new PrototypePowerInstanceState(
                            "proto.power.slow",
                            1,
                            1)
                    ])
            ],
            NextCardInstanceId:
                cards.Length == 0
                    ? 1
                    : cards.Max(card =>
                        card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 2);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "bygone-effigy-test",
            "bygone-effigy-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "bygone-effigy-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                6,
                5000,
                PrototypeRoomType.Elite,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null),
            Ascension: ascension);
    }
}
