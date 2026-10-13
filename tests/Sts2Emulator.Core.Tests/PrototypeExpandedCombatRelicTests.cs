using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeExpandedCombatRelicTests
{
    [Fact]
    public void CombatStartRelicsReusePowerAndStatusMechanics()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("expanded-relic-start");
        state = engine.Step(
            state,
            Assert.Single(engine.GetLegalActions(state))).State;

        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Relics =
                [
                    new RelicInstance("proto.relic.bronze_scales", empty),
                    new RelicInstance("proto.relic.oddly_smooth_stone", empty),
                    new RelicInstance("proto.relic.bag_of_marbles", empty)
                ]
            },
            World = state.World! with
            {
                Map = new MapState(
                    [
                        new MapNodeState(
                            "combat",
                            1,
                            1,
                            PrototypeRoomType.Combat,
                            [])
                    ],
                    EntryNodeIds: ["combat"])
            }
        };

        state = engine.Step(
            state,
            GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload("combat"))).State;

        var combat = state.World!.Combat!;
        Assert.Equal(
            3,
            Assert.Single(
                combat.PlayerPowers,
                p => p.PowerId == "proto.power.thorns").Stacks);
        Assert.Equal(
            1,
            Assert.Single(
                combat.PlayerPowers,
                p => p.PowerId == "proto.power.dexterity").Stacks);
        Assert.All(
            combat.Enemies.Where(enemy => enemy.Hp > 0),
            enemy => Assert.Equal(
                1,
                enemy.Statuses.GetValueOrDefault(
                    "proto.status.vulnerable")));
    }

    [Fact]
    public void GremlinHornGainsEnergyAndDrawsWhenEnemyDies()
    {
        var state = CombatStateWith(
            relicIds: ["proto.relic.gremlin_horn"],
            cards:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend")
            ],
            hand: [1],
            drawPile: [2],
            enemies:
            [
                Enemy(1, 1),
                Enemy(2, 100)
            ],
            energy: 3);
        var engine = new PrototypeGameEngine();

        var strike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == 1);
        state = engine.Step(state, strike).State;

        var combat = state.World!.Combat!;
        Assert.Equal(3, combat.Energy);
        Assert.Contains(2L, combat.Hand);
        Assert.Empty(combat.DrawPile);
    }

    [Fact]
    public void MercuryHourglassDamagesAllEnemiesAtPlayerTurnStart()
    {
        var state = CombatStateWith(
            relicIds: ["proto.relic.mercury_hourglass"],
            cards: [Card(1, "proto.silent.defend")],
            hand: [1],
            enemies:
            [
                Enemy(1, 20),
                Enemy(2, 20)
            ],
            playerBlock: 999);
        var engine = new PrototypeGameEngine();

        state = EndTurn(engine, state);

        Assert.Equal(2, state.World!.Combat!.Turn);
        Assert.All(
            state.World.Combat.Enemies,
            enemy => Assert.Equal(17, enemy.Hp));
    }

    [Fact]
    public void TurnConditionalBlockRelicsTriggerOnTurnsTwoAndThree()
    {
        var state = CombatStateWith(
            relicIds:
            [
                "proto.relic.horn_cleat",
                "proto.relic.captains_wheel"
            ],
            cards: [Card(1, "proto.silent.defend")],
            hand: [1],
            enemies: [Enemy(1, 200)],
            playerBlock: 999);
        var engine = new PrototypeGameEngine();

        state = EndTurn(engine, state);
        Assert.Equal(2, state.World!.Combat!.Turn);
        Assert.Equal(14, state.World.Combat.PlayerBlock);

        state = EndTurn(engine, state);
        Assert.Equal(3, state.World!.Combat!.Turn);
        Assert.Equal(18, state.World.Combat.PlayerBlock);
    }

    [Fact]
    public void OrichalcumMitigatesEnemyTurnOnlyWhenEndingWithZeroBlock()
    {
        var engine = new PrototypeGameEngine();
        var withRelic = CombatStateWith(
            relicIds: ["proto.relic.orichalcum"],
            cards: [Card(1, "proto.silent.defend")],
            hand: [1],
            enemies: [Enemy(1, 100)]);
        var withoutRelic = CombatStateWith(
            relicIds: [],
            cards: [Card(1, "proto.silent.defend")],
            hand: [1],
            enemies: [Enemy(1, 100)]);

        withRelic = EndTurn(engine, withRelic);
        withoutRelic = EndTurn(engine, withoutRelic);

        Assert.True(
            withRelic.Player.Hp > withoutRelic.Player.Hp);
        Assert.InRange(
            withRelic.Player.Hp - withoutRelic.Player.Hp,
            1,
            6);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state) =>
        engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

    private static CombatCardInstance Card(
        long id,
        string cardId) =>
        new(
            id,
            1000 + id,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(
        int id,
        int hp) =>
        new(
            id,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal));

    private static RunState CombatStateWith(
        string[] relicIds,
        CombatCardInstance[] cards,
        long[] hand,
        EnemyCombatState[] enemies,
        long[]? drawPile = null,
        int energy = 3,
        int playerBlock = 0)
    {
        drawPile ??= [];
        var empty = PrototypeJson.EmptyObject();
        var relics = relicIds
            .Select(id => new RelicInstance(id, empty))
            .ToArray();
        var combatRelics = relicIds
            .Select((id, index) =>
            {
                var definition = PrototypeContent.Relic(id);
                return new CombatRelicState(
                    index,
                    id,
                    index + 1L,
                    new int[
                        (definition.Triggers
                            ?? Array.Empty<PrototypeRelicTriggerSpec>())
                        .Length]);
            })
            .ToArray();
        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card =>
                new CardInstance(
                    card.PersistentCardInstanceId!.Value,
                    card.CardId,
                    card.UpgradeLevel,
                    empty))
                .ToArray(),
            relics,
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: playerBlock,
            Hand: hand,
            DrawPile: drawPile,
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId:
                cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder:
                combatRelics.Length + 1,
            Relics: combatRelics);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "expanded-combat-relic-test",
            "expanded-combat-relic-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "expanded-combat-relic-test"),
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
