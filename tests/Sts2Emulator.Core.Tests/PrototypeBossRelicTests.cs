using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeBossRelicTests
{
    [Fact]
    public void BossVictoryOffersThreeDistinctBossRelics()
    {
        var engine = new PrototypeGameEngine();
        var state = BossCombatState();

        var strike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == 1);
        state = engine.Step(state, strike).State;

        Assert.Equal(RunPhase.Reward, state.Phase);
        PrototypeStateInvariants.Validate(state);

        while (!state.World!.Reward!.CardResolved)
        {
            state = engine.Step(
                state,
                engine.GetLegalActions(state)
                    .Single(action =>
                        action.Kind == "skip_reward_card")).State;
        }

        while (!state.World!.Reward!.PotionResolved)
        {
            state = engine.Step(
                state,
                engine.GetLegalActions(state)
                    .Single(action =>
                        action.Kind == "skip_reward_potion")).State;
        }

        var reward = state.World!.Reward!;
        Assert.False(reward.RelicResolved);
        Assert.Equal(3, reward.CurrentRelicOptions.Length);
        Assert.Equal(
            3,
            reward.CurrentRelicOptions
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(
            reward.CurrentRelicOptions,
            relicId => Assert.Contains(
                relicId,
                PrototypeContent.BossRelicPool));

        var actions = engine.GetLegalActions(state)
            .Where(action =>
                action.Kind == "take_reward_relic")
            .ToArray();
        Assert.Equal(3, actions.Length);
        Assert.Equal(
            new[] { 0, 1, 2 },
            actions
                .Select(action =>
                    action.ReadPayload<ChooseRelicPayload>()
                        .Index)
                .Order()
                .ToArray());

        var frame = new PrototypeAiEnvironment()
            .Observe(state);
        Assert.Equal(
            reward.CurrentRelicOptions,
            frame.Observation.Reward!.RelicOptions);

        var selectedIndex = Array.FindIndex(
            reward.CurrentRelicOptions,
            relicId =>
                PrototypeContent.Relic(relicId)
                    .AcquisitionDeckChoice is null);
        Assert.True(selectedIndex >= 0);
        var selected =
            reward.CurrentRelicOptions[selectedIndex];
        var selectedAction = actions.Single(action =>
            action.ReadPayload<ChooseRelicPayload>()
                .Index == selectedIndex);
        state = engine.Step(
            state,
            selectedAction).State;
        Assert.Contains(
            state.Player.Relics,
            relic => relic.RelicId == selected);
        Assert.True(state.World!.Reward!.RelicResolved);

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state),
                action => action.Kind == "leave_reward")).State;
        Assert.Equal(RunPhase.ActTransition, state.Phase);
    }

    [Fact]
    public void CoffeeDripperAndFusionHammerRestrictRestChoices()
    {
        var engine = new PrototypeGameEngine();

        var coffee = RestState(
            ["proto.relic.coffee_dripper"]);
        var coffeeActions = engine.GetLegalActions(coffee);
        Assert.DoesNotContain(
            coffeeActions,
            action => action.Kind == "rest_heal");
        Assert.Contains(
            coffeeActions,
            action => action.Kind == "rest_train");
        Assert.Contains(
            coffeeActions,
            action => action.Kind == "rest_upgrade");

        var hammer = RestState(
            ["proto.relic.fusion_hammer"]);
        var hammerActions = engine.GetLegalActions(hammer);
        Assert.Contains(
            hammerActions,
            action => action.Kind == "rest_heal");
        Assert.Contains(
            hammerActions,
            action => action.Kind == "rest_train");
        Assert.DoesNotContain(
            hammerActions,
            action => action.Kind == "rest_upgrade");

        var both = RestState(
            [
                "proto.relic.coffee_dripper",
                "proto.relic.fusion_hammer"
            ]);
        var bothActions = engine.GetLegalActions(both);
        Assert.Equal(
            new[] { "rest_train" },
            bothActions.Select(action => action.Kind).ToArray());
    }

    [Fact]
    public void VelvetChokerStopsCardPlayAfterSixCards()
    {
        var engine = new PrototypeGameEngine();
        var state = SevenShivCombat(
            "proto.relic.velvet_choker");

        for (var id = 1L; id <= 6L; id++)
        {
            var action = engine.GetLegalActions(state)
                .Single(item =>
                    item.Kind == "play_card"
                    && item.ReadPayload<PlayCardPayload>()
                        .CardInstanceId == id);
            state = engine.Step(state, action).State;
        }

        var combat = state.World!.Combat!;
        Assert.Equal(
            6,
            combat.CounterState.CardsPlayedThisTurn);
        Assert.Contains(7L, combat.Hand);
        Assert.DoesNotContain(
            engine.GetLegalActions(state),
            action => action.Kind == "play_card");
        Assert.Contains(
            engine.GetLegalActions(state),
            action => action.Kind == "end_turn");
    }

    [Fact]
    public void RunicDomeHidesEnemyIntentsFromAiObservation()
    {
        var hidden = SevenShivCombat(
            "proto.relic.runic_dome",
            shivCount: 1);
        var visible = SevenShivCombat(
            relicId: null,
            shivCount: 1);
        // Synthetic combat fixtures do not pass through StartCombat.
        // Initialize their public intent explicitly, just as the normal
        // engine does before exposing the first player decision.
        hidden = hidden with
        {
            World = hidden.World! with
            {
                Combat = PrototypeGameEngine.CommitEnemyIntents(
                    hidden.World.Combat!, hidden.Rng)
            }
        };
        visible = visible with
        {
            World = visible.World! with
            {
                Combat = PrototypeGameEngine.CommitEnemyIntents(
                    visible.World.Combat!, visible.Rng)
            }
        };
        var environment = new PrototypeAiEnvironment();

        Assert.Null(
            Assert.Single(
                environment.Observe(hidden)
                    .Observation.Combat!.Enemies)
                .MoveId);
        Assert.NotNull(
            Assert.Single(
                environment.Observe(visible)
                    .Observation.Combat!.Enemies)
                .MoveId);

        Assert.Equal(
            hidden.World!.Combat!.Enemies.Single().MoveIndex,
            visible.World!.Combat!.Enemies.Single().MoveIndex);
    }

    [Fact]
    public void EnergyBossRelicsGrantOneEnergyPerTurn()
    {
        var energyRelics = new[]
        {
            "proto.relic.sozu",
            "proto.relic.coffee_dripper",
            "proto.relic.fusion_hammer",
            "proto.relic.velvet_choker",
            "proto.relic.runic_dome"
        };

        Assert.All(
            energyRelics,
            relicId => Assert.Equal(
                1,
                PrototypeContent.Relic(relicId)
                    .EnergyPerTurnBonus));
    }

    private static RunState BossCombatState()
    {
        var empty = PrototypeJson.EmptyObject();
        var card = new CombatCardInstance(
            1,
            1001,
            "proto.silent.strike",
            0,
            false,
            empty);
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(
                    1001,
                    card.CardId,
                    0,
                    empty)
            ],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    1,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards: [card],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "boss-relic-choice",
            "boss-relic-choice",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "boss-relic-choice"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                PrototypeContent.Rules.FloorsPerAct,
                5000,
                PrototypeRoomType.Boss,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }

    private static RunState RestState(
        string[] relicIds)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            35,
            70,
            0,
            [
                new CardInstance(
                    1,
                    "proto.silent.strike",
                    0,
                    empty)
            ],
            relicIds.Select(id =>
                new RelicInstance(id, empty))
                .ToArray(),
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "boss-rest",
            "boss-rest",
            0,
            RunPhase.Rest,
            player,
            PrototypeRng.CreateBundle("boss-rest"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                2,
                4,
                2,
                PrototypeRoomType.Rest,
                new MapState([]),
                null,
                null,
                null,
                null,
                null));
    }

    private static RunState SevenShivCombat(
        string? relicId,
        int shivCount = 7)
    {
        var empty = PrototypeJson.EmptyObject();
        var relics = relicId is null
            ? Array.Empty<RelicInstance>()
            : [new RelicInstance(relicId, empty)];
        var combatRelics = relicId is null
            ? Array.Empty<CombatRelicState>()
            : [
                new CombatRelicState(
                    0,
                    relicId,
                    1,
                    new int[
                        (PrototypeContent.Relic(relicId)
                            .Triggers
                            ?? Array.Empty<
                                PrototypeRelicTriggerSpec>())
                        .Length])
            ];
        var cards = Enumerable.Range(1, shivCount)
            .Select(index =>
                new CombatCardInstance(
                    index,
                    null,
                    "proto.silent.shiv",
                    0,
                    true,
                    empty))
            .ToArray();
        var player = new PlayerState(
            70,
            70,
            0,
            [],
            relics,
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: cards.Select(card =>
                card.InstanceId).ToArray(),
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: shivCount + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder:
                combatRelics.Length + 1,
            Relics: combatRelics);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "boss-combat-cap",
            "boss-combat-cap",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "boss-combat-cap"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                2,
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
