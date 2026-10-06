using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePowerTests
{
    [Fact]
    public void FootworkAddsDexterityThatModifiesLaterBlockEffects()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(40, "proto.silent.footwork", 0, empty),
                new CardInstance(41, "proto.silent.defend", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 3,
            Cards:
            [
                new CombatCardInstance(1, 40, "proto.silent.footwork", 0, false, empty),
                new CombatCardInstance(2, 41, "proto.silent.defend", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = CreateState("dex-test", player, combat, nextPersistentCardId: 42);
        var engine = new PrototypeGameEngine();

        var footwork = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, footwork).State;

        Assert.Equal(
            2,
            Assert.Single(state.World!.Combat!.PlayerPowers).Stacks);

        var defend = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2);
        state = engine.Step(state, defend).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Equal(7, state.World!.Combat!.PlayerBlock);
        Assert.Equal(1, state.World.Combat.Energy);
    }

    [Fact]
    public void NoxiousFumesTriggersAtRulesetPlayerTurnStartStep()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [new CardInstance(50, "proto.silent.noxious_fumes", 0, empty)],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)),
                new EnemyCombatState(
                    2,
                    "proto.enemy.raider",
                    32,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(1, 50, "proto.silent.noxious_fumes", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = CreateState("fumes-test", player, combat, nextPersistentCardId: 51);
        var engine = new PrototypeGameEngine();

        var fumes = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");
        state = engine.Step(state, fumes).State;

        var endTurn = engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn");
        state = engine.Step(state, endTurn).State;
        PrototypeStateInvariants.Validate(state);

        combat = state.World!.Combat!;
        Assert.Equal(2, combat.Turn);
        Assert.All(
            combat.Enemies,
            enemy => Assert.Equal(1, enemy.Statuses["proto.status.poison"]));
        Assert.Equal(1, Assert.Single(combat.PlayerPowers).Stacks);
    }


    [Fact]
    public void CardPlayedEventWaitsForSuspendedChoiceCompletion()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(70, "proto.silent.survivor", 0, empty),
                new CardInstance(71, "proto.silent.strike", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 3,
            Cards:
            [
                new CombatCardInstance(1, 70, "proto.silent.survivor", 0, false, empty),
                new CombatCardInstance(2, 71, "proto.silent.strike", 0, false, empty)
            ],
            PlayerPowers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.afterimage",
                    1,
                    1)
            ],
            NextPowerApplicationOrder: 2);

        var state = CreateState("event-continuation-test", player, combat, 72);
        var engine = new PrototypeGameEngine();

        var play = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);

        state = engine.Step(state, play).State;
        PrototypeStateInvariants.Validate(state);

        Assert.NotNull(state.World!.Combat!.PendingChoice);
        Assert.Equal(8, state.World.Combat.PlayerBlock);

        var chooseDiscard = Assert.Single(engine.GetLegalActions(state));
        Assert.Equal("select_cards", chooseDiscard.Kind);

        state = engine.Step(state, chooseDiscard).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Null(state.World!.Combat!.PendingChoice);
        Assert.Equal(9, state.World.Combat.PlayerBlock);
        Assert.Equal(new long[] { 2, 1 }, state.World.Combat.DiscardPile);
    }


    [Fact]
    public void DexterityOnlyModifiesCardSourcedBlock()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(75, "proto.silent.strike", 0, empty),
                new CardInstance(76, "proto.silent.defend", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 3,
            Cards:
            [
                new CombatCardInstance(1, 75, "proto.silent.strike", 0, false, empty),
                new CombatCardInstance(2, 76, "proto.silent.defend", 0, false, empty)
            ],
            PlayerPowers:
            [
                new PrototypePowerInstanceState("proto.power.dexterity", 2, 1),
                new PrototypePowerInstanceState("proto.power.afterimage", 1, 2)
            ],
            NextPowerApplicationOrder: 3);

        var state = CreateState("dex-provenance-test", player, combat, 77);
        var engine = new PrototypeGameEngine();

        var strike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId == 1);
        state = engine.Step(state, strike).State;

        Assert.Equal(1, state.World!.Combat!.PlayerBlock);

        var defend = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2);
        state = engine.Step(state, defend).State;

        Assert.Equal(8, state.World!.Combat!.PlayerBlock);
    }

    [Fact]
    public void AbrasiveThornsRetaliatesBeforeDamageAndStopsLaterHits()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [new CardInstance(78, "proto.silent.abrasive", 0, empty)],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

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
                    "proto.enemy.assassin",
                    4,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(1, 78, "proto.silent.abrasive", 0, false, empty)
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState("abrasive-thorns-test", player, combat, 79);
        var engine = new PrototypeGameEngine();

        var abrasive = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");
        state = engine.Step(state, abrasive).State;

        var powers = state.World!.Combat!.PlayerPowers;
        Assert.Equal(
            1,
            powers.Single(power => power.PowerId == "proto.power.dexterity").Stacks);
        Assert.Equal(
            4,
            powers.Single(power => power.PowerId == "proto.power.thorns").Stacks);

        state = engine.Step(state, GameAction.Empty("end_turn")).State;

        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.Equal(65, state.Player.Hp);
    }

    [Fact]
    public void EnemyDamagedEventCarriesTargetIntoReactivePower()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [new CardInstance(80, "proto.silent.strike", 0, empty)],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(1, 80, "proto.silent.strike", 0, false, empty)
            ],
            PlayerPowers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.envenom",
                    2,
                    1)
            ],
            NextPowerApplicationOrder: 2);

        var state = CreateState("damage-event-test", player, combat, 81);
        var engine = new PrototypeGameEngine();

        var play = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");

        state = engine.Step(state, play).State;
        PrototypeStateInvariants.Validate(state);

        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(18, enemy.Hp);
        Assert.Equal(2, enemy.Statuses["proto.status.poison"]);
    }


    [Fact]
    public void EnemyReactivePowerUsesOwnerTargetAndSharedEventOrder()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(90, "proto.silent.strike", 0, empty),
                new CardInstance(91, "proto.silent.strike", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.elite",
                    55,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal),
                    [
                        new PrototypePowerInstanceState(
                            "proto.power.thorns",
                            2,
                            1)
                    ]),
                new EnemyCombatState(
                    2,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 3,
            Cards:
            [
                new CombatCardInstance(1, 90, "proto.silent.strike", 0, false, empty),
                new CombatCardInstance(2, 91, "proto.silent.strike", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 2);

        var state = CreateState("enemy-power-test", player, combat, 92);
        var engine = new PrototypeGameEngine();

        var hitCrawler = engine.GetLegalActions(state)
            .Single(action =>
            {
                if (action.Kind != "play_card")
                {
                    return false;
                }

                var payload = action.ReadPayload<PlayCardPayload>();
                return payload.CardInstanceId == 1 && payload.TargetEnemyId == 2;
            });
        state = engine.Step(state, hitCrawler).State;
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(70, state.Player.Hp);

        var hitElite = engine.GetLegalActions(state)
            .Single(action =>
            {
                if (action.Kind != "play_card")
                {
                    return false;
                }

                var payload = action.ReadPayload<PlayCardPayload>();
                return payload.CardInstanceId == 2 && payload.TargetEnemyId == 1;
            });
        state = engine.Step(state, hitElite).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Equal(68, state.Player.Hp);
        Assert.Equal(
            49,
            state.World!.Combat!.Enemies.Single(enemy => enemy.InstanceId == 1).Hp);
        Assert.Equal(
            18,
            state.World.Combat.Enemies.Single(enemy => enemy.InstanceId == 2).Hp);
    }

    private static RunState CreateState(
        string seed,
        PlayerState player,
        CombatState combat,
        long nextPersistentCardId)
    {
        var empty = PrototypeJson.EmptyObject();
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
                nextPersistentCardId,
                PrototypeRoomType.Combat,
                new MapState(Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));
    }
}
