using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeCeremonialBeastTests
{
    [Fact]
    public void DefinitionMatchesPinnedV01110Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.ceremonial_beast");

        Assert.Equal((252, 252), enemy.HpRangeAt(1, 0));
        Assert.Equal((262, 262), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.StateMachine,
            enemy.MovePolicy);

        Assert.Collection(
            enemy.Moves,
            stamp =>
            {
                Assert.Equal("stamp", stamp.Id);
                var plow = Assert.Single(stamp.Effects);
                Assert.Equal("proto.power.plow", plow.PowerId);
                Assert.Equal(150, plow.AmountAt(1, 8));
                Assert.Equal(160, plow.AmountAt(1, 9));
            },
            plow =>
            {
                Assert.Equal("plow", plow.Id);
                Assert.Equal(18, plow.Effects[0].AmountAt(1, 8));
                Assert.Equal(20, plow.Effects[0].AmountAt(1, 9));
                Assert.Equal(2, plow.Effects[1].Amount);
            },
            stun =>
            {
                Assert.Equal("stun", stun.Id);
                Assert.Empty(stun.Effects);
            },
            cry =>
            {
                Assert.Equal("beast_cry", cry.Id);
                Assert.Equal(
                    "proto.power.ringing",
                    Assert.Single(cry.Effects).PowerId);
            },
            stomp =>
            {
                Assert.Equal("stomp", stomp.Id);
                Assert.Equal(15, Assert.Single(stomp.Effects).AmountAt(1, 8));
                Assert.Equal(17, Assert.Single(stomp.Effects).AmountAt(1, 9));
            },
            crush =>
            {
                Assert.Equal("crush", crush.Id);
                Assert.Equal(17, crush.Effects[0].AmountAt(1, 8));
                Assert.Equal(19, crush.Effects[0].AmountAt(1, 9));
                Assert.Equal(3, crush.Effects[1].AmountAt(1, 8));
                Assert.Equal(4, crush.Effects[1].AmountAt(1, 9));
            });

        var plowPower = PrototypeContent.Power(
            "proto.power.plow");
        Assert.True(plowPower.TriggerOwnerAtHpAtOrBelowStacks);
        Assert.True(plowPower.ClearOwnerStrengthOnHpThresholdTrigger);
        Assert.Equal(
            "stun_move",
            plowPower.OwnerAiStateOnHpThresholdTrigger);

        var ringing = PrototypeContent.Power(
            "proto.power.ringing");
        Assert.True(ringing.IsDebuff);
        Assert.True(ringing.DecrementAtPlayerTurnEnd);
        Assert.Equal(1, ringing.MaxCardsPlayablePerTurn);
        Assert.Equal(PrototypeCardAfflictionKind.Ringing,
            ringing.AppliedCardAffliction);
        Assert.True(ringing.SkipCardsWithExistingAffliction);
        Assert.True(ringing.ClearAppliedCardAfflictionWhenRemoved);
    }

    [Fact]
    public void CrossingPlowThresholdForcesStunAndClearsStrength()
    {
        var cards = new[]
        {
            Slice(1),
            Slice(2)
        };
        var engine = new PrototypeGameEngine();
        var state = CreateState(cards);

        state = EndTurn(engine, state);
        var beast = Beast(state);
        Assert.Equal("stamp", beast.LastMoveId);
        Assert.Equal(
            150,
            Assert.Single(
                beast.PowerStates,
                power => power.PowerId == "proto.power.plow")
                .Stacks);

        state = EndTurn(engine, state);
        Assert.Equal(82, state.Player.Hp);
        beast = Beast(state);
        Assert.Equal("plow", beast.LastMoveId);
        Assert.Equal(2, StrengthOf(beast));

        state = SetBeastHp(state, 156);
        state = PlayCard(engine, state, 1);

        beast = Beast(state);
        Assert.Equal(150, beast.Hp);
        Assert.Equal("stun_move", beast.AiStateId);
        Assert.Equal(0, StrengthOf(beast));
        Assert.DoesNotContain(
            beast.PowerStates,
            power => power.PowerId == "proto.power.plow");

        state = EndTurn(engine, state);
        Assert.Equal(82, state.Player.Hp);
        Assert.Equal("stun", Beast(state).LastMoveId);
    }

    [Fact]
    public void BeastCryLimitsNextPlayerTurnToOneCard()
    {
        var cards = new[]
        {
            Slice(1),
            Slice(2)
        };
        var engine = new PrototypeGameEngine();
        var state = CreateState(cards);

        state = EndTurn(engine, state);
        state = EndTurn(engine, state);
        state = SetBeastHp(state, 156);
        state = PlayCard(engine, state, 1);
        state = EndTurn(engine, state);

        state = EndTurn(engine, state);
        Assert.Equal("beast_cry", Beast(state).LastMoveId);
        Assert.Single(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.ringing");
        Assert.All(state.World.Combat.Cards, card =>
            Assert.Equal(PrototypeCardAfflictionKind.Ringing,
                card.Affliction?.Kind));

        Assert.Equal(
            2,
            engine.GetLegalActions(state)
                .Count(action => action.Kind == "play_card"));

        var playable = engine.GetLegalActions(state)
            .First(action => action.Kind == "play_card");
        state = engine.Step(state, playable).State;

        Assert.DoesNotContain(
            engine.GetLegalActions(state),
            action => action.Kind == "play_card");

        state = EndTurn(engine, state);
        Assert.Equal("stomp", Beast(state).LastMoveId);
        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.ringing");
        Assert.All(state.World.Combat.Cards,
            card => Assert.Null(card.Affliction));
        Assert.Contains(
            engine.GetLegalActions(state),
            action => action.Kind == "play_card");
    }


    [Fact]
    public void RingingAfflictsGeneratedShivsAndPreventsTheirPlay()
    {
        var cards = new[]
        {
            Slice(1),
            new CombatCardInstance(
                2, null, "proto.silent.blade_dance",
                0, true, PrototypeJson.EmptyObject())
        };
        var engine = new PrototypeGameEngine();
        var state = CreateState(cards);

        state = EndTurn(engine, state); // Stamp
        state = EndTurn(engine, state); // Plow
        state = SetBeastHp(state, 156);
        state = PlayCard(engine, state, 1); // Trigger phase change
        state = EndTurn(engine, state); // Stunned
        state = EndTurn(engine, state); // Beast Cry

        var dance = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2);
        state = engine.Step(state, dance).State;

        var combat = state.World!.Combat!;
        var shivs = combat.Cards.Where(card =>
            card.CardId == "proto.silent.shiv").ToArray();
        Assert.Equal(3, shivs.Length);
        Assert.All(shivs, shiv =>
        {
            Assert.Contains(shiv.InstanceId, combat.Hand);
            Assert.Equal(PrototypeCardAfflictionKind.Ringing,
                shiv.Affliction?.Kind);
        });
        Assert.Equal(1, combat.CounterState.CardsPlayedThisTurn);
        Assert.DoesNotContain(engine.GetLegalActions(state), action =>
            action.Kind == "play_card");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void RingingDoesNotBlockCardsWithAnotherAffliction()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState([Slice(1), Slice(2), Slice(3)]);

        state = EndTurn(engine, state);
        state = EndTurn(engine, state);
        state = SetBeastHp(state, 156);
        state = PlayCard(engine, state, 1);
        state = EndTurn(engine, state); // Stunned

        // Native RingingPower.AfterApplied skips cards already afflicted.
        // Mark the second card before Beast Cry; the card continues to
        // be playable despite a Ringing-afflicted card having been played.
        var combat = state.World!.Combat!;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Cards = combat.Cards.Select(card =>
                        card.InstanceId == 2
                            ? card with
                            {
                                Affliction = new PrototypeCardAffliction(
                                    PrototypeCardAfflictionKind.Smog)
                            }
                            : card).ToArray()
                }
            }
        };
        state = EndTurn(engine, state); // Beast Cry

        var cry = state.World!.Combat!;
        Assert.Equal(PrototypeCardAfflictionKind.Ringing,
            cry.Cards.Single(card => card.InstanceId == 1).Affliction?.Kind);
        Assert.Equal(PrototypeCardAfflictionKind.Smog,
            cry.Cards.Single(card => card.InstanceId == 2).Affliction?.Kind);
        Assert.Equal(PrototypeCardAfflictionKind.Ringing,
            cry.Cards.Single(card => card.InstanceId == 3).Affliction?.Kind);

        state = PlayCard(engine, state, 1);
        Assert.Contains(engine.GetLegalActions(state), action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2);
        Assert.DoesNotContain(engine.GetLegalActions(state), action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == 3);
        Assert.Throws<InvalidOperationException>(
            () => PlayCard(engine, state, 3));

        state = PlayCard(engine, state, 2);
        Assert.Equal(2, state.World!.Combat!.CounterState.CardsPlayedThisTurn);
        state = EndTurn(engine, state);
        Assert.Null(state.World!.Combat!.Cards.Single(
            card => card.InstanceId == 1).Affliction);
        Assert.Equal(PrototypeCardAfflictionKind.Smog,
            state.World.Combat.Cards.Single(
                card => card.InstanceId == 2).Affliction?.Kind);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void BossReferenceFormationIsSingleton()
    {
        var spec = Assert.Single(
            PrototypeContent.Encounter(
                    "proto.encounter.ceremonial_beast_boss")
                .ResolveEnemySpecs(
                    PrototypeRng.CreateBundle(
                        "ceremonial-beast-formation")));

        Assert.Equal(
            "proto.enemy.ceremonial_beast",
            spec.EnemyId);
        Assert.Equal(0, spec.FormationPosition);
    }

    private static CombatCardInstance Slice(long id) =>
        new(
            id,
            null,
            "proto.silent.slice",
            0,
            true,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Beast(
        RunState state) =>
        Assert.Single(state.World!.Combat!.Enemies);

    private static int StrengthOf(
        EnemyCombatState enemy) =>
        enemy.PowerStates
            .SingleOrDefault(power =>
                power.PowerId == "proto.power.strength")
            ?.Stacks ?? 0;

    private static RunState SetBeastHp(
        RunState state,
        int hp)
    {
        var world = state.World!;
        var combat = world.Combat!;
        combat = combat with
        {
            Enemies = combat.Enemies
                .Select(enemy =>
                    enemy.InstanceId == 1
                        ? enemy with { Hp = hp }
                        : enemy)
                .ToArray()
        };
        return state with
        {
            World = world with { Combat = combat }
        };
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
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
                return payload.CardInstanceId
                        == cardInstanceId
                    && payload.TargetEnemyId == 1;
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

    private static RunState CreateState(
        CombatCardInstance[] cards)
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
            Hand: cards
                .Select(card => card.InstanceId)
                .ToArray(),
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.ceremonial_beast",
                    252,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    AiStateId: "stamp_move")
            ],
            NextCardInstanceId:
                cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1,
            Act: 1,
            Ascension: 0);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "ceremonial-beast-test",
            "ceremonial-beast-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "ceremonial-beast-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Boss,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
