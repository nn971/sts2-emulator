using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeStaticMechanicsExpansionTests
{
    [Fact]
    public void BouncingFlaskRerollsRandomEnemyForEveryApplication()
    {
        var state = CreateState(
            "bouncing-flask-test",
            [
                Card(1, 1001, "proto.silent.bouncing_flask")
            ],
            enemies:
            [
                Enemy(1, 40),
                Enemy(2, 40)
            ]);

        var engine = new PrototypeGameEngine();
        var beforeCalls = StreamCalls(state, "combat_targets");

        var play = Assert.Single(
            engine.GetLegalActions(state),
            action => action.Kind == "play_card");
        state = engine.Step(state, play).State;

        var afterCalls = StreamCalls(state, "combat_targets");
        Assert.Equal<ulong>(3UL, afterCalls - beforeCalls);

        var poison = state.World!.Combat!.Enemies.Sum(enemy =>
            enemy.Statuses.GetValueOrDefault("proto.status.poison"));
        Assert.Equal(9, poison);
    }

    [Fact]
    public void UpgradedBouncingFlaskAddsOneRandomApplication()
    {
        var state = CreateState(
            "bouncing-flask-upgrade-test",
            [
                Card(
                    1,
                    1001,
                    "proto.silent.bouncing_flask",
                    upgradeLevel: 1)
            ],
            enemies:
            [
                Enemy(1, 40),
                Enemy(2, 40)
            ]);

        var engine = new PrototypeGameEngine();
        var beforeCalls = StreamCalls(state, "combat_targets");

        var play = Assert.Single(
            engine.GetLegalActions(state),
            action => action.Kind == "play_card");
        state = engine.Step(state, play).State;

        var afterCalls = StreamCalls(state, "combat_targets");
        Assert.Equal<ulong>(4UL, afterCalls - beforeCalls);

        var poison = state.World!.Combat!.Enemies.Sum(enemy =>
            enemy.Statuses.GetValueOrDefault("proto.status.poison"));
        Assert.Equal(12, poison);
    }

    [Fact]
    public void SerpentFormDoesNotTriggerOnTheCardThatCreatesIt()
    {
        var state = CreateState(
            "serpent-form-test",
            [
                Card(1, 1001, "proto.silent.serpent_form"),
                Card(2, 1002, "proto.silent.strike")
            ],
            enemies:
            [
                Enemy(1, 40)
            ]);

        var engine = new PrototypeGameEngine();

        var serpentForm = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, serpentForm).State;

        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(40, enemy.Hp);
        Assert.Equal(
            4,
            Assert.Single(
                state.World.Combat.PlayerPowers,
                power => power.PowerId == "proto.power.serpent_form").Stacks);
        Assert.Equal<ulong>(0UL, StreamCalls(state, "combat_targets"));

        var strike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2);
        state = engine.Step(state, strike).State;

        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(30, enemy.Hp);
        Assert.Equal<ulong>(1UL, StreamCalls(state, "combat_targets"));
    }

    [Fact]
    public void CorrosiveWaveTriggersPerCardDrawAndExpiresAtTurnEnd()
    {
        var state = CreateState(
            "corrosive-wave-test",
            [
                Card(1, 1001, "proto.silent.corrosive_wave"),
                Card(2, 1002, "proto.silent.quick_slash")
            ],
            enemies:
            [
                Enemy(1, 40),
                Enemy(2, 40)
            ],
            drawPile:
            [
                Card(3, 1003, "proto.silent.defend")
            ]);

        var engine = new PrototypeGameEngine();

        var corrosiveWave = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, corrosiveWave).State;

        Assert.Equal(
            2,
            Assert.Single(
                state.World!.Combat!.PlayerPowers,
                power => power.PowerId == "proto.power.corrosive_wave").Stacks);

        var quickSlash = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId == 1);
        state = engine.Step(state, quickSlash).State;

        Assert.All(
            state.World!.Combat!.Enemies,
            enemy => Assert.Equal(
                2,
                enemy.Statuses.GetValueOrDefault("proto.status.poison")));
        Assert.Equal(
            1,
            state.World.Combat.CounterState.CardsDrawnThisCombat);

        state = engine.Step(state, GameAction.Empty("end_turn")).State;

        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.corrosive_wave");
    }

    private static ulong StreamCalls(RunState state, string streamId) =>
        state.Rng.Streams
            .Single(stream => stream.StreamId == streamId)
            .CallCount
        ?? 0;

    private static CombatCardInstance Card(
        long instanceId,
        long persistentId,
        string cardId,
        int upgradeLevel = 0) =>
        new(
            instanceId,
            persistentId,
            cardId,
            upgradeLevel,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(int id, int hp) =>
        new(
            id,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            new Dictionary<string, int>(StringComparer.Ordinal));

    private static RunState CreateState(
        string seed,
        CombatCardInstance[] hand,
        EnemyCombatState[] enemies,
        CombatCardInstance[]? drawPile = null)
    {
        drawPile ??= [];
        var empty = PrototypeJson.EmptyObject();
        var allCards = hand.Concat(drawPile).ToArray();
        var player = new PlayerState(
            70,
            70,
            0,
            allCards.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                empty)).ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: hand.Select(card => card.InstanceId).ToArray(),
            DrawPile: drawPile.Select(card => card.InstanceId).ToArray(),
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId: allCards.Max(card => card.InstanceId) + 1,
            Cards: allCards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            seed,
            seed,
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(seed),
            empty,
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
                null));
    }
}
