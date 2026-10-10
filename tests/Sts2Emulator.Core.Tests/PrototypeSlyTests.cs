using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSlyTests
{
    [Fact]
    public void SurvivorDiscardingTacticianAutoPlaysItForFreeBeforeSurvivorCompletes()
    {
        var state = CreateState(
            energy: 3,
            hand:
            [
                Card(1, "proto.silent.survivor"),
                Card(2, "proto.silent.tactician"),
                Card(3, "proto.silent.strike")
            ],
            drawPile: []);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);

        Assert.NotNull(state.World!.Combat!.PendingChoice);
        Assert.Equal(2, state.World.Combat.Energy);

        var selectTactician = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([2]));

        state = engine.Step(state, selectTactician).State;
        var combat = state.World!.Combat!;

        Assert.Null(combat.PendingChoice);
        Assert.Equal(3, combat.Energy);
        Assert.Equal(new long[] { 3 }, combat.Hand);
        Assert.Equal(new long[] { 2, 1 }, combat.DiscardPile);
    }

    [Fact]
    public void SurvivorDiscardingReflexAutoPlaysItAfterDiscardAndDrawsCards()
    {
        var state = CreateState(
            energy: 3,
            hand:
            [
                Card(1, "proto.silent.survivor"),
                Card(2, "proto.silent.reflex"),
                Card(3, "proto.silent.strike")
            ],
            drawPile:
            [
                Card(4, "proto.silent.defend"),
                Card(5, "proto.silent.defend")
            ]);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);

        var selectReflex = engine.GetLegalActions(state)
            .Single(action =>
                action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([2]));
        state = engine.Step(state, selectReflex).State;

        var combat = state.World!.Combat!;
        Assert.Equal(new long[] { 3, 5, 4 }, combat.Hand);
        Assert.Equal(new long[] { 2, 1 }, combat.DiscardPile);
        Assert.Empty(combat.DrawPile);
        Assert.Equal(2, combat.Energy);
    }

    [Fact]
    public void SlySurvivorCanSuspendForNestedDiscardAndResumeOuterCard()
    {
        var slySurvivor = Card(
            2,
            "proto.silent.survivor") with
        {
            KeywordOverrides =
            [
                new PrototypeCardKeywordOverride(
                    PrototypeCardKeyword.Sly,
                    true,
                    PrototypeCardKeywordOverrideExpiry.None)
            ]
        };
        var state = CreateState(
            energy: 3,
            hand:
            [
                Card(1, "proto.silent.survivor"),
                slySurvivor,
                Card(3, "proto.silent.strike")
            ],
            drawPile: []);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);

        var discardSlySurvivor = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([2]));
        state = engine.Step(state, discardSlySurvivor).State;

        var nested = state.World!.Combat!;
        Assert.NotNull(nested.PendingChoice);
        Assert.Equal(16, nested.PlayerBlock);
        PrototypeStateInvariants.Validate(state);

        var discardStrike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([3]));
        state = engine.Step(state, discardStrike).State;

        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Equal(16, combat.PlayerBlock);
        Assert.Equal(2, combat.Energy);
        Assert.Empty(combat.Hand);
        Assert.Equal(new long[] { 3, 2, 1 }, combat.DiscardPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void HiddenDaggersDiscardingSlyCardsAutoPlaysAllBeforeGeneratingShivs()
    {
        var state = CreateState(
            energy: 3,
            hand:
            [
                Card(1, "proto.silent.hidden_daggers"),
                Card(2, "proto.silent.tactician"),
                Card(3, "proto.silent.untouchable")
            ],
            drawPile: []);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);

        var selectBoth = Assert.Single(engine.GetLegalActions(state));
        Assert.Equal(new long[] { 2, 3 }, selectBoth.ReadPayload<SelectCardsPayload>().CardInstanceIds);

        state = engine.Step(state, selectBoth).State;
        var combat = state.World!.Combat!;

        Assert.Equal(4, combat.Energy);
        Assert.Equal(6, combat.PlayerBlock);
        Assert.Equal(new long[] { 2, 3, 1 }, combat.DiscardPile);

        var shivs = combat.Hand
            .Select(id => combat.Cards.Single(card => card.InstanceId == id))
            .ToArray();
        Assert.Equal(2, shivs.Length);
        Assert.All(shivs, card => Assert.Equal("proto.silent.shiv", card.CardId));
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var play = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == cardInstanceId);
        return engine.Step(state, play).State;
    }

    private static CombatCardInstance Card(long instanceId, string cardId) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateState(
        int energy,
        CombatCardInstance[] hand,
        CombatCardInstance[] drawPile)
    {
        var empty = PrototypeJson.EmptyObject();
        var cards = hand.Concat(drawPile).ToArray();
        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                empty)).ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            1,
            energy,
            0,
            hand.Select(card => card.InstanceId).ToArray(),
            drawPile.Select(card => card.InstanceId).ToArray(),
            Array.Empty<long>(),
            Array.Empty<long>(),
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            cards.Max(card => card.InstanceId) + 1,
            cards,
            Array.Empty<PrototypePowerInstanceState>(),
            1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "sly-test",
            "sly-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("sly-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Combat,
                new MapState(Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiscardedSlySnakebiteRandomlyTargetsOnlyLivingEnemy(
        bool killFirstEnemy)
    {
        var slySnakebite = Card(2, "proto.silent.snakebite") with
        {
            KeywordOverrides =
            [
                new PrototypeCardKeywordOverride(
                    PrototypeCardKeyword.Sly, true,
                    PrototypeCardKeywordOverrideExpiry.None)
            ]
        };

        RunState Setup()
        {
            var initial = CreateState(
                energy: 3,
                hand:
                [
                    Card(1, "proto.silent.survivor"),
                    slySnakebite,
                    Card(3, "proto.silent.strike")
                ],
                drawPile: []);
            var world = initial.World!;
            var combat = world.Combat!;
            var first = combat.Enemies[0] with
            {
                Hp = killFirstEnemy ? 0 : 100,
                InstanceId = 1
            };
            var second = first with { InstanceId = 2, Hp = 100 };
            return initial with
            {
                World = world with
                {
                    Combat = combat with
                    {
                        Enemies = [first, second],
                        NextEnemyInstanceId = 3
                    }
                }
            };
        }

        RunState Resolve(RunState state)
        {
            var engine = new PrototypeGameEngine();
            state = PlayCard(engine, state, 1);
            var choice = engine.GetLegalActions(state).Single(action =>
                action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([2]));
            return engine.Step(state, choice).State;
        }

        var before = Setup();
        var calls = before.Rng.Streams.Single(stream =>
            stream.StreamId == "combat_targets").CallCount ?? 0UL;
        var result = Resolve(before);
        var combat = result.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Equal(2, combat.Energy);
        Assert.Contains(2L, combat.DiscardPile);
        var afterCalls = result.Rng.Streams.Single(stream =>
            stream.StreamId == "combat_targets").CallCount ?? 0UL;
        Assert.Equal<ulong>(1UL, afterCalls - calls);
        var statuses = combat.Enemies.Select(enemy =>
            enemy.Statuses.GetValueOrDefault("proto.status.poison")).ToArray();
        Assert.Equal(7, statuses.Sum());
        if (killFirstEnemy)
        {
            Assert.Equal(0, statuses[0]);
            Assert.Equal(7, statuses[1]);
        }

        var replay = Resolve(Setup());
        Assert.Equal(
            CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(replay));
        PrototypeStateInvariants.Validate(result);
    }

}
