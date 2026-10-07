using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeVineShamblerTests
{
    [Fact]
    public void DefinitionMatchesPinnedOvergrowthData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.vine_shambler");

        Assert.Equal("Vine Shambler", enemy.Name);
        Assert.Equal((61, 61), enemy.HpRangeAt(1, 0));
        Assert.Equal((64, 64), enemy.HpRangeAt(1, 8));

        Assert.Collection(
            enemy.Moves,
            swipe =>
            {
                Assert.Equal("swipe", swipe.Id);
                var damage = Assert.Single(swipe.Effects);
                Assert.Equal(2, damage.Repetitions);
                Assert.Equal(6, damage.AmountAt(1, 8));
                Assert.Equal(7, damage.AmountAt(1, 9));
            },
            vines =>
            {
                Assert.Equal("grasping_vines", vines.Id);
                Assert.Collection(
                    vines.Effects,
                    damage =>
                    {
                        Assert.Equal(8, damage.AmountAt(1, 8));
                        Assert.Equal(9, damage.AmountAt(1, 9));
                    },
                    tangled =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.ApplyPlayerPower,
                            tangled.Kind);
                        Assert.Equal("proto.power.tangled", tangled.PowerId);
                    });
            },
            chomp =>
            {
                Assert.Equal("chomp", chomp.Id);
                var damage = Assert.Single(chomp.Effects);
                Assert.Equal(16, damage.AmountAt(1, 8));
                Assert.Equal(18, damage.AmountAt(1, 9));
            });

        var tangled = PrototypeContent.Power(
            "proto.power.tangled");
        Assert.True(tangled.IsDebuff);
        Assert.Equal(
            PrototypeCardAfflictionKind.Entangled,
            tangled.AppliedCardAffliction);
        Assert.Equal(
            PrototypeCardType.Attack,
            tangled.AppliedCardAfflictionRequiredCardType);
        Assert.Equal(1, tangled.AfflictedCardEnergyCostPerStack);
        Assert.True(tangled.DecrementAtPlayerTurnEnd);
        Assert.True(tangled.ClearAppliedCardAfflictionWhenRemoved);
    }

    [Fact]
    public void TangledTaxesAttacksForExactlyNextPlayerTurn()
    {
        var engine = new PrototypeGameEngine();
        var strike = new CombatCardInstance(
            1,
            null,
            "proto.silent.strike",
            0,
            true,
            PrototypeJson.EmptyObject());
        var state = CreateState(
            aiMoveIndex: 1,
            cards: [strike],
            drawPile: [1]);

        state = EndTurn(engine, state);

        var combat = state.World!.Combat!;
        var drawnStrike = Assert.Single(
            combat.Cards,
            card => card.InstanceId == 1);
        Assert.Equal(
            PrototypeCardAfflictionKind.Entangled,
            drawnStrike.Affliction?.Kind);
        Assert.Equal(
            1,
            Assert.Single(
                combat.PlayerPowers,
                power => power.PowerId == "proto.power.tangled")
                .Stacks);

        var play = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1);
        state = engine.Step(state, play).State;
        Assert.Equal(1, state.World!.Combat!.Energy);

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.tangled");
        Assert.DoesNotContain(
            combat.Cards,
            card => card.Affliction?.Kind
                == PrototypeCardAfflictionKind.Entangled);
    }

    [Fact]
    public void NativeCycleIsSwipeVinesChomp()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        state = EndTurn(engine, state);
        Assert.Equal("swipe", LastMove(state));

        state = EndTurn(engine, state);
        Assert.Equal("grasping_vines", LastMove(state));

        state = EndTurn(engine, state);
        Assert.Equal("chomp", LastMove(state));

        state = EndTurn(engine, state);
        Assert.Equal("swipe", LastMove(state));
    }

    [Fact]
    public void ReferenceEncounterIsSingleton()
    {
        var specs = PrototypeContent.Encounter(
                "proto.encounter.vine_shambler_normal")
            .ResolveEnemySpecs(
                PrototypeRng.CreateBundle("vine-formation"));
        var spec = Assert.Single(specs);
        Assert.Equal("proto.enemy.vine_shambler", spec.EnemyId);
        Assert.Equal(0, spec.FormationPosition);
    }

    private static string LastMove(RunState state) =>
        Assert.Single(state.World!.Combat!.Enemies).LastMoveId!;

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState CreateState(
        int aiMoveIndex = 0,
        CombatCardInstance[]? cards = null,
        long[]? drawPile = null)
    {
        cards ??= [];
        drawPile ??= [];
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
            Hand: [],
            DrawPile: drawPile,
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.vine_shambler",
                    61,
                    0,
                    aiMoveIndex,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId:
                cards.Length == 0 ? 1 : cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "vine-shambler-test",
            "vine-shambler-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "vine-shambler-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                4,
                5000,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null),
            Ascension: 0);
    }
}
