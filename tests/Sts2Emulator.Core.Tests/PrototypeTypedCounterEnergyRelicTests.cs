using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeTypedCounterEnergyRelicTests
{
    [Fact]
    public void KunaiAndShurikenCountOnlyAttacks()
    {
        var state = CombatStateWith(
            relicIds:
            [
                "proto.relic.kunai",
                "proto.relic.shuriken"
            ],
            cards:
            [
                Card(1, "proto.silent.defend"),
                Card(2, "proto.silent.strike"),
                Card(3, "proto.silent.strike"),
                Card(4, "proto.silent.strike")
            ],
            hand: [1, 2, 3, 4],
            enemies: [Enemy(1, 100)],
            energy: 4);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);
        Assert.Empty(state.World!.Combat!.PlayerPowers);

        state = Play(engine, state, 2);
        state = Play(engine, state, 3);
        Assert.Empty(state.World!.Combat!.PlayerPowers);

        state = Play(engine, state, 4);

        var combat = state.World!.Combat!;
        Assert.Equal(
            1,
            Assert.Single(
                combat.PlayerPowers,
                p => p.PowerId == "proto.power.dexterity").Stacks);
        Assert.Equal(
            1,
            Assert.Single(
                combat.PlayerPowers,
                p => p.PowerId == "proto.power.strength").Stacks);
    }

    [Fact]
    public void PerTurnAttackCountersResetAtNextPlayerTurn()
    {
        var state = CombatStateWith(
            relicIds: ["proto.relic.kunai"],
            cards:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.strike"),
                Card(3, "proto.silent.strike")
            ],
            hand: [1, 2],
            drawPile: [3],
            enemies: [Enemy(1, 200)],
            playerBlock: 999);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);
        state = Play(engine, state, 2);
        Assert.Equal(
            2,
            Assert.Single(
                state.World!.Combat!.RelicStates)
                .TriggerCounts[0]);

        state = EndTurn(engine, state);

        Assert.Equal(
            0,
            Assert.Single(
                state.World!.Combat!.RelicStates)
                .TriggerCounts[0]);
        Assert.DoesNotContain(
            state.World.Combat.PlayerPowers,
            p => p.PowerId == "proto.power.dexterity");

        var attack = state.World.Combat.Hand
            .Select(id => state.World.Combat.Cards
                .Single(card => card.InstanceId == id))
            .First(card =>
                PrototypeContent.Card(card.CardId).Type
                    == PrototypeCardType.Attack);
        state = Play(engine, state, attack.InstanceId);

        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            p => p.PowerId == "proto.power.dexterity");
    }

    [Fact]
    public void IceCreamCarriesUnusedEnergyAcrossTurnBoundary()
    {
        var state = CombatStateWith(
            relicIds: ["proto.relic.ice_cream"],
            cards: [Card(1, "proto.silent.defend")],
            hand: [1],
            enemies: [Enemy(1, 200)],
            energy: 2,
            playerBlock: 999);
        var engine = new PrototypeGameEngine();

        state = EndTurn(engine, state);

        Assert.Equal(2, state.World!.Combat!.Turn);
        Assert.Equal(5, state.World.Combat.Energy);
    }

    [Fact]
    public void ArtOfWarRewardsTurnWithNoAttacks()
    {
        var engine = new PrototypeGameEngine();

        var noAttack = CombatStateWith(
            relicIds: ["proto.relic.art_of_war"],
            cards: [Card(1, "proto.silent.defend")],
            hand: [1],
            enemies: [Enemy(1, 200)],
            playerBlock: 999);
        noAttack = EndTurn(engine, noAttack);
        Assert.Equal(4, noAttack.World!.Combat!.Energy);

        var attacked = CombatStateWith(
            relicIds: ["proto.relic.art_of_war"],
            cards: [Card(1, "proto.silent.strike")],
            hand: [1],
            enemies: [Enemy(1, 200)],
            playerBlock: 999);
        attacked = Play(engine, attacked, 1);
        attacked = EndTurn(engine, attacked);
        Assert.Equal(3, attacked.World!.Combat!.Energy);
    }

    [Fact]
    public void LetterOpenerIgnoresAttacksAndCountsSkillsPerTurn()
    {
        var state = CombatStateWith(
            relicIds: ["proto.relic.letter_opener"],
            cards:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend"),
                Card(3, "proto.silent.defend"),
                Card(4, "proto.silent.defend")
            ],
            hand: [1, 2, 3, 4],
            enemies:
            [
                Enemy(1, 30),
                Enemy(2, 30)
            ],
            energy: 4);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);
        state = Play(engine, state, 2);
        state = Play(engine, state, 3);
        Assert.All(
            state.World!.Combat!.Enemies,
            enemy => Assert.Equal(
                enemy.InstanceId == 1 ? 24 : 30,
                enemy.Hp));

        state = Play(engine, state, 4);
        Assert.Equal(19, state.World!.Combat!.Enemies[0].Hp);
        Assert.Equal(25, state.World.Combat.Enemies[1].Hp);
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long cardId)
    {
        var actions = engine.GetLegalActions(state)
            .Where(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == cardId)
            .ToArray();
        var action = actions.First();
        return engine.Step(state, action).State;
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
            "typed-counter-energy-relic-test",
            "typed-counter-energy-relic-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "typed-counter-energy-relic-test"),
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
