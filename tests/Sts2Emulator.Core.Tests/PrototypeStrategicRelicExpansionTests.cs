using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeStrategicRelicExpansionTests
{
    [Fact]
    public void ChemicalXAddsTwoToXValueWithoutAddingEnergy()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.skewer")
            ],
            hand: [1],
            enemies: [Enemy(1, 50)],
            energy: 2,
            relicIds: ["proto.relic.chemical_x"]);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1, targetEnemyId: 1);

        var combat = state.World!.Combat!;
        Assert.Equal(18, combat.Enemies[0].Hp);
        Assert.Equal(0, combat.Energy);
    }

    [Fact]
    public void MummifiedHandMakesOnePositiveCostHandCardFreeForTurn()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.accuracy"),
                Card(2, "proto.silent.defend")
            ],
            hand: [1, 2],
            enemies: [Enemy(1, 50)],
            energy: 3,
            relicIds: ["proto.relic.mummified_hand"]);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);

        var defend = state.World!.Combat!.Cards
            .Single(card => card.InstanceId == 2);
        Assert.NotNull(defend.TemporaryEnergyCost);
        Assert.Equal(0, defend.TemporaryEnergyCost!.Cost);

        var defendAction = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 2);
        state = engine.Step(state, defendAction).State;

        Assert.Equal(2, state.World!.Combat!.Energy);
    }

    [Fact]
    public void UnceasingTopDrawsWhenCardPlayLeavesHandEmpty()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend")
            ],
            hand: [1],
            draw: [2],
            enemies: [Enemy(1, 50)],
            energy: 3,
            relicIds: ["proto.relic.unceasing_top"]);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1, targetEnemyId: 1);

        Assert.Equal(new long[] { 2 }, state.World!.Combat!.Hand);
    }

    [Fact]
    public void RunicPyramidPreservesNonEtherealHandAtEndOfTurn()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.defend"),
                Card(2, "proto.status.dazed")
            ],
            hand: [1, 2],
            enemies: [Enemy(1, 100)],
            energy: 3,
            relicIds: ["proto.relic.runic_pyramid"]);
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        var combat = state.World!.Combat!;
        Assert.Contains(1L, combat.Hand);
        Assert.Contains(2L, combat.ExhaustPile);
    }

    [Fact]
    public void SneckoEyeDrawsSevenAndRandomizesFixedCosts()
    {
        var cards = Enumerable.Range(1, 7)
            .Select(id =>
                Card(id, "proto.silent.defend"))
            .ToArray();
        var state = CreateState(
            cards: cards,
            hand: [],
            draw: cards
                .Select(card => card.InstanceId)
                .ToArray(),
            enemies: [Enemy(1, 100)],
            energy: 3,
            relicIds: ["proto.relic.snecko_eye"]);
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        var combat = state.World!.Combat!;
        Assert.Equal(7, combat.Hand.Length);
        Assert.All(
            combat.Hand,
            instanceId =>
            {
                var card = combat.Cards.Single(
                    item => item.InstanceId == instanceId);
                Assert.NotNull(card.TemporaryEnergyCost);
                Assert.InRange(
                    card.TemporaryEnergyCost!.Cost,
                    0,
                    3);
                Assert.Equal(
                    PrototypeTemporaryCardCostExpiry.None,
                    card.TemporaryEnergyCost.Expiry);
            });
    }

    [Fact]
    public void SneckoEyeLeavesXCostCardsUnrandomized()
    {
        var skewer = Card(
            1,
            "proto.silent.skewer");
        var state = CreateState(
            cards: [skewer],
            hand: [],
            draw: [1],
            enemies: [Enemy(1, 100)],
            energy: 3,
            relicIds: ["proto.relic.snecko_eye"]);
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        var combat = state.World!.Combat!;
        Assert.Contains(1L, combat.Hand);
        Assert.Null(
            combat.Cards.Single(
                    card => card.InstanceId == 1)
                .TemporaryEnergyCost);
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId,
        int? targetEnemyId = null)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
            {
                if (item.Kind != "play_card")
                {
                    return false;
                }

                var payload = item.ReadPayload<PlayCardPayload>();
                return payload.CardInstanceId == cardInstanceId
                    && payload.TargetEnemyId == targetEnemyId;
            });
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long id,
        string cardId,
        int upgrade = 0) =>
        new(
            id,
            1000 + id,
            cardId,
            upgrade,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(int id, int hp) =>
        new(
            id,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal));

    private static RunState CreateState(
        CombatCardInstance[] cards,
        long[] hand,
        EnemyCombatState[] enemies,
        int energy,
        string[] relicIds,
        long[]? draw = null)
    {
        draw ??= [];
        var persistent = cards
            .Select(card =>
                new CardInstance(
                    card.PersistentCardInstanceId!.Value,
                    card.CardId,
                    card.UpgradeLevel,
                    PrototypeJson.EmptyObject()))
            .ToArray();
        var relics = relicIds
            .Select(id =>
                new RelicInstance(
                    id,
                    PrototypeJson.EmptyObject()))
            .ToArray();
        var player = new PlayerState(
            70,
            70,
            0,
            persistent,
            relics,
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combatRelics = relicIds
            .Select((id, index) =>
            {
                var definition =
                    PrototypeContent.Relic(id);
                return new CombatRelicState(
                    index,
                    id,
                    index + 1,
                    new int[
                        (definition.Triggers
                         ?? Array.Empty<
                             PrototypeRelicTriggerSpec>())
                        .Length]);
            })
            .ToArray();
        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: 0,
            Hand: hand,
            DrawPile: draw,
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId:
                cards.Max(c => c.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder:
                combatRelics.Length + 1,
            Relics: combatRelics);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "strategic-relic-test",
            "strategic-relic-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "strategic-relic-test"),
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
                null));
    }
}
