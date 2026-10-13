using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveReactiveNormalEncounterTests
{
    private const string Toad = PrototypeNativeHiveNormals.SpinyToadId;
    private const string Beetle = PrototypeNativeHiveNormals.SlumberingBeetleId;
    private const string Slumber = "proto.native.hive.slumber";

    [Theory]
    [InlineData(0, 116, 119, 17, 23)]
    [InlineData(8, 121, 124, 17, 23)]
    [InlineData(9, 121, 124, 19, 25)]
    public void SpinyToadHpAndAttackScalesFromPinnedSource(
        int asc, int minHp, int maxHp, int lash, int explosion)
    {
        var enemy = PrototypeContent.Enemy(Toad);
        Assert.Equal((minHp, maxHp), enemy.HpRangeAt(2, asc));
        Assert.Equal(new[] { "protruding_spikes", "spike_explosion",
            "tongue_lash" }, enemy.Moves.Select(m => m.Id));
        Assert.Equal(5, enemy.Moves[0].Effects[0].Amount);
        Assert.Equal("proto.power.thorns",
            enemy.Moves[0].Effects[0].PowerId);
        Assert.Equal(explosion,
            enemy.Moves[1].Effects[0].AmountAt(2, asc));
        Assert.Equal(-5, enemy.Moves[1].Effects[1].Amount);
        Assert.Equal(lash, enemy.Moves[2].Effects[0].AmountAt(2, asc));
        Assert.Equal(0, enemy.MoveLoopStartIndex);

        var encounter = PrototypeContent.Encounter(
            PrototypeNativeHiveNormals.SpinyToadNormalId);
        Assert.Equal(0, encounter.Weight);
        Assert.Equal(new[] { Toad }, encounter.EnemyIds);
    }

    [Fact]
    public void SpinyToadProtrudesExplodesRemovesThornsThenLashes()
    {
        var engine = new PrototypeGameEngine();
        var state = Commit(State([Spec(Toad, 0)], "hive-spiny-cycle"));

        state = EndTurn(engine, state);
        Assert.Equal("protruding_spikes",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(5, Assert.Single(
            Assert.Single(state.World.Combat.Enemies).PowerStates,
            p => p.PowerId == "proto.power.thorns").Stacks);
        Assert.Equal(500, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal("spike_explosion",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.DoesNotContain(
            Assert.Single(state.World.Combat.Enemies).PowerStates,
            p => p.PowerId == "proto.power.thorns");
        Assert.Equal(477, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal("tongue_lash",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(460, state.Player.Hp);
        state = EndTurn(engine, state);
        Assert.Equal("protruding_spikes",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(5, Assert.Single(
            Assert.Single(state.World.Combat.Enemies).PowerStates,
            p => p.PowerId == "proto.power.thorns").Stacks);
        Assert.Equal(460, state.Player.Hp);
    }

    [Fact]
    public void SpinyThornsRetaliateBeforePoweredCardAttack()
    {
        var engine = new PrototypeGameEngine();
        var state = State([Spec(Toad, 0)], "hive-spiny-retaliation");
        var card = new CardInstance(1, "proto.silent.strike", 0,
            PrototypeJson.EmptyObject());
        var combatCard = new CombatCardInstance(1, 1,
            card.CardId, 0, false, card.PersistentState);
        var enemy = state.World!.Combat!.Enemies.Single() with
        {
            Powers =
            [
                new PrototypePowerInstanceState(
                    "proto.power.thorns", 5, 1)
            ]
        };
        state = state with
        {
            Player = state.Player with { Deck = [card] },
            World = state.World with
            {
                Combat = state.World.Combat with
                {
                    Hand = [1], Cards = [combatCard],
                    NextCardInstanceId = 2, Enemies = [enemy],
                    NextPowerApplicationOrder = 2
                }
            }
        };
        state = Commit(state);
        var play = Assert.Single(engine.GetLegalActions(state),
            a => a.Kind == "play_card");
        var before = CanonicalJson.Sha256(state);
        var next = engine.Step(state, play).State;
        Assert.Equal(495, next.Player.Hp);
        Assert.Equal(113,
            Assert.Single(next.World!.Combat!.Enemies).Hp);
        Assert.Equal(before, CanonicalJson.Sha256(state));
    }

    [Theory]
    [InlineData(0, 86, 15, 16)]
    [InlineData(8, 89, 18, 16)]
    [InlineData(9, 89, 18, 18)]
    public void BeetleStartPowerHpAndRolloutScaleCorrectly(
        int asc, int hp, int plating, int rollout)
    {
        var model = PrototypeContent.Enemy(Beetle);
        Assert.Equal((hp, hp), model.HpRangeAt(2, asc));
        Assert.Equal(2, model.Moves.Length);
        Assert.Equal(new[] { "snore", "rollout" },
            model.Moves.Select(m => m.Id));
        Assert.Equal(rollout,
            model.Moves[1].Effects[0].AmountAt(2, asc));
        Assert.Equal("proto.power.strength",
            model.Moves[1].Effects[1].PowerId);
        Assert.Equal(2, model.Moves[1].Effects[1].Amount);
        Assert.Equal(plating, model.StartingPowers!
            .Single(p => p.PowerId == "proto.power.plating")
            .StacksAt(asc));
        Assert.Equal(3, model.StartingPowers!
            .Single(p => p.PowerId == Slumber)
            .StacksAt(asc));
    }

    [Fact]
    public void BeetleFormationContainsExactThreeNativeNamedSlots()
    {
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeHiveNormals.SlumberingBeetleNormalId);
        Assert.Equal((2, 2, 0), (encounter.MinAct,
            encounter.MaxAct, encounter.Weight));
        var specs = encounter.ResolveEnemySpecs(
            PrototypeRng.CreateBundle("hive-beetle-formation"));
        Assert.Equal(new[]
        {
            "proto.native.hive.bowlbug_rock",
            "proto.native.hive.bowlbug_silk",
            Beetle
        }, specs.Select(s => s.EnemyId));
        Assert.Equal(new[] { "first", "second", "third" },
            specs.Select(s => s.SlotName));
        Assert.Equal(new[] { 0, 1, 2 },
            specs.Select(s => s.FormationPosition));
    }

    [Fact]
    public void BeetleSleepsThreeEnemyTurnsThenRollsWithStrengthScaling()
    {
        var engine = new PrototypeGameEngine();
        var state = Commit(State([Spec(Beetle, 0)], "hive-beetle-natural"));
        var beginning = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(15, beginning.Block);
        Assert.Equal(0, beginning.PlannedMoveIndex);
        for (var remaining = 2; remaining >= 0; remaining--)
        {
            state = EndTurn(engine, state);
            var snoring = Assert.Single(state.World!.Combat!.Enemies);
            Assert.Equal("snore", snoring.LastMoveId);
            Assert.Equal(500, state.Player.Hp);
            if (remaining > 0)
            {
                Assert.Equal(remaining, Assert.Single(
                    snoring.PowerStates, p => p.PowerId == Slumber).Stacks);
            }
            else
            {
                Assert.DoesNotContain(snoring.PowerStates,
                    p => p.PowerId == Slumber
                        || p.PowerId == "proto.power.plating");
                Assert.Equal(1, snoring.PlannedMoveIndex);
            }
        }

        state = EndTurn(engine, state);
        Assert.Equal(484, state.Player.Hp);
        var rolling = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("rollout", rolling.LastMoveId);
        Assert.Equal(2, Assert.Single(rolling.PowerStates,
            p => p.PowerId == "proto.power.strength").Stacks);

        state = EndTurn(engine, state);
        Assert.Equal(466, state.Player.Hp);
        Assert.Equal(4, Assert.Single(
            Assert.Single(state.World!.Combat!.Enemies).PowerStates,
            p => p.PowerId == "proto.power.strength").Stacks);
    }

    [Fact]
    public void FirstUnblockedHitInterruptsSleepAndRemovesPlating()
    {
        var engine = new PrototypeGameEngine();
        var original = Commit(State([Spec(Beetle, 0)],
            "hive-beetle-early-wake"));
        var before = CanonicalJson.Sha256(original);
        var blocked = Hit(original.World!.Combat!, 15);
        Assert.Equal(0, Assert.Single(blocked.Enemies).Block);
        Assert.Contains(Assert.Single(blocked.Enemies).PowerStates,
            p => p.PowerId == Slumber);
        Assert.Equal(0, Assert.Single(blocked.Enemies).PlannedMoveIndex);

        var awakened = Hit(original.World!.Combat!, 20);
        var awake = Assert.Single(awakened.Enemies);
        Assert.Equal(81, awake.Hp);
        Assert.Equal(0, awake.Block);
        Assert.DoesNotContain(awake.PowerStates, p =>
            p.PowerId == Slumber || p.PowerId == "proto.power.plating");
        Assert.Equal("rollout", awake.AiStateId);
        Assert.Equal(1, awake.EnemyActionSkipsRemaining);
        Assert.Equal(before, CanonicalJson.Sha256(original));

        var state = original with
        {
            World = original.World with { Combat = awakened }
        };
        state = EndTurn(engine, state);
        Assert.Equal(500, state.Player.Hp);
        // The interrupted action was skipped; the next Roll Out
        // intent is already committed for the following player turn.
        Assert.Equal(1, Assert.Single(state.World!.Combat!.Enemies)
            .PlannedMoveIndex);
        state = EndTurn(engine, state);
        Assert.Equal(484, state.Player.Hp);
        Assert.Equal("rollout",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
    }

    [Fact]
    public void BothNewNormalEncountersRemainGatedFromPrototypeRoutes()
    {
        foreach (var id in new[]
        {
            PrototypeNativeHiveNormals.SpinyToadNormalId,
            PrototypeNativeHiveNormals.SlumberingBeetleNormalId
        })
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal(2, encounter.MinAct);
            Assert.Equal(2, encounter.MaxAct);
            Assert.Equal(0, encounter.Weight);
            Assert.DoesNotContain(id,
                PrototypeContent.OvergrowthNormalEncounterPool);
            Assert.DoesNotContain(id,
                PrototypeNativeUnderdocks.NativeNormalEncounterIds);
        }
    }

    private static PrototypeEncounterEnemySpec Spec(
        string id, int position) => new(id, position);

    private static RunState State(
        PrototypeEncounterEnemySpec[] specs, string seed,
        int ascension = 0)
    {
        long order = 1;
        var enemies = specs.Select((spec, index) =>
        {
            var def = PrototypeContent.Enemy(spec.EnemyId);
            var powers = (def.StartingPowers
                ?? Array.Empty<PrototypeStartingPowerSpec>())
                .Select(power => new PrototypePowerInstanceState(
                    power.PowerId, power.StacksAt(ascension), order++))
                .ToArray();
            var initialBlock = powers.Sum(power => power.Stacks *
                PrototypeContent.Power(power.PowerId)
                    .EnemyStartingBlockPerStack);
            return new EnemyCombatState(index + 1, spec.EnemyId,
                def.HpAt(2, ascension), initialBlock, 0,
                new Dictionary<string, int>(StringComparer.Ordinal),
                SlotName: spec.SlotName,
                FormationPosition: spec.FormationPosition,
                Powers: powers, AiStateId: def.Ai?.InitialStateId);
        }).ToArray();
        var combat = new CombatState(1, 3, 0, [], [], [], [],
            enemies, 1, [], [], order, Act: 2, Ascension: ascension);
        return new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(500, 500, 0, [], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 1,
                PrototypeRoomType.Combat, new MapState([]),
                combat, null, null, null, null), Ascension: ascension);
    }

    private static RunState Commit(RunState state) =>
        state with
        {
            World = state.World! with
            {
                Combat = PrototypeGameEngine.CommitEnemyIntents(
                    state.World.Combat!, state.Rng)
            }
        };

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;

    private static CombatState Hit(CombatState combat, int amount)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "DamageEnemy", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(null,
            [combat, 1, amount, 0])!;
        var property = result.GetType().GetProperty("Combat");
        Assert.NotNull(property);
        return Assert.IsType<CombatState>(property!.GetValue(result));
    }
}
