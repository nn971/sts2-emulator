using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveWeakFormationsTests
{
    private const string Rock = "proto.native.hive.bowlbug_rock";
    private const string Silk = "proto.native.hive.bowlbug_silk";
    private const string Exo = "proto.native.hive.exoskeleton";
    private const string RockEncounter = "proto.native.hive.encounter.bowlbugs_weak";
    private const string ExoEncounter = "proto.native.hive.encounter.exoskeletons_weak";

    [Fact]
    public void BowlbugsWeakAlwaysHasRockAndOneRandomDistinctWorker()
    {
        var encounter = PrototypeContent.Encounter(RockEncounter);
        Assert.Equal(0, encounter.Weight);
        Assert.Equal(2, encounter.MinAct);
        Assert.Equal(2, encounter.MaxAct);
        var seen = new HashSet<string>();
        for (var seed = 0; seed < 128; seed++)
        {
            var rng = PrototypeRng.CreateBundle($"hive-bowlbugs-{seed}");
            var group = encounter.ResolveEnemySpecs(rng);
            Assert.Equal(2, group.Length);
            Assert.Equal((Rock, 0, "odd"),
                (group[0].EnemyId, group[0].FormationPosition,
                    group[0].SlotName));
            Assert.Equal((1, "even"),
                (group[1].FormationPosition, group[1].SlotName));
            Assert.Contains(group[1].EnemyId,
                new[] { "proto.native.hive.bowlbug_egg",
                        "proto.native.hive.bowlbug_nectar" });
            seen.Add(group[1].EnemyId);
        }
        Assert.Equal(2, seen.Count);
    }

    [Fact]
    public void RockFullyBlockedHeadbuttCommitsOneDizzyThenRecovers()
    {
        var engine = new PrototypeGameEngine();
        var enemy = Enemy(Rock, 48, "odd") with
        {
            Powers = [Power("proto.native.hive.imbalanced", 1)]
        };
        var state = State([enemy], "rock-full-block") with
        {
            World = State([enemy], "rock-full-block").World! with
            {
                Combat = State([enemy], "rock-full-block").World!.Combat!
                    with { PlayerBlock = 30 }
            }
        };
        state = Commit(state);
        Assert.Equal(0, Assert.Single(state.World!.Combat!.Enemies)
            .PlannedMoveIndex);
        state = EndTurn(engine, state);
        Assert.Equal(100, state.Player.Hp);
        var after = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("headbutt", after.LastMoveId);
        Assert.True(after.IsOffBalance);
        Assert.Equal(1, after.PlannedMoveIndex);

        state = EndTurn(engine, state);
        after = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("dizzy", after.LastMoveId);
        Assert.False(after.IsOffBalance);
        Assert.Equal(0, after.PlannedMoveIndex);

        state = EndTurn(engine, state);
        after = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("headbutt", after.LastMoveId);
        Assert.False(after.IsOffBalance);
        Assert.Equal(85, state.Player.Hp);
    }

    [Fact]
    public void RockUnblockedAttackNeverTriggersDizzy()
    {
        var engine = new PrototypeGameEngine();
        var enemy = Enemy(Rock, 48, "odd") with
        {
            Powers = [Power("proto.native.hive.imbalanced", 1)]
        };
        var state = Commit(State([enemy], "rock-no-block"));
        for (var i = 0; i < 2; i++)
        {
            state = EndTurn(engine, state);
            var actual = Assert.Single(state.World!.Combat!.Enemies);
            Assert.Equal("headbutt", actual.LastMoveId);
            Assert.Equal(0, actual.PlannedMoveIndex);
            Assert.False(actual.IsOffBalance);
        }
        Assert.Equal(70, state.Player.Hp);
    }

    [Fact]
    public void ExoskeletonWeakHasThreeSourceSlotsAndInitialIntents()
    {
        var encounter = PrototypeContent.Encounter(ExoEncounter);
        Assert.Equal(0, encounter.Weight);
        var specs = encounter.ResolveEnemySpecs(
            PrototypeRng.CreateBundle("hive-exo-formation"));
        Assert.Equal(new[] { "first", "second", "third" },
            specs.Select(s => s.SlotName));
        Assert.All(specs, s => Assert.Equal(Exo, s.EnemyId));

        var enemies = specs.Select((spec, i) =>
            Enemy(Exo, 28, spec.SlotName!) with
            {
                InstanceId = i + 1,
                FormationPosition = i,
                Powers = [Power("proto.native.hive.hard_to_kill", 9)]
            }).ToArray();
        var state = Commit(State(enemies, "exo-intents"));
        Assert.Equal(new[] { 0, 1, 2 },
            state.World!.Combat!.Enemies
                .Select(enemy => enemy.PlannedMoveIndex!.Value).ToArray());

        var engine = new PrototypeGameEngine();
        state = EndTurn(engine, state);
        Assert.Equal(new[] { "skitter", "mandibles", "enrage" },
            state.World!.Combat!.Enemies.Select(e => e.LastMoveId));
        var third = state.World.Combat.Enemies[2];
        Assert.Equal(2, Assert.Single(third.PowerStates,
            p => p.PowerId == "proto.power.strength").Stacks);
    }

    [Fact]
    public void HardToKillCapsEachSeparateHitWithoutConsumingPower()
    {
        var enemy = Enemy(Exo, 28, "first") with
        {
            Powers = [Power("proto.native.hive.hard_to_kill", 9)]
        };
        var combat = State([enemy], "exo-cap").World!.Combat!;
        var first = Hit(combat, 100);
        var after1 = Assert.Single(first.Enemies);
        Assert.Equal(19, after1.Hp);
        Assert.Single(after1.PowerStates, p =>
            p.PowerId == "proto.native.hive.hard_to_kill");
        var second = Hit(first, 100);
        Assert.Equal(10, Assert.Single(second.Enemies).Hp);
        var third = Hit(second, 100);
        Assert.Equal(1, Assert.Single(third.Enemies).Hp);
        var final = Hit(third, 100);
        Assert.Equal(0, Assert.Single(final.Enemies).Hp);
        Assert.Equal(28, enemy.Hp);
        Assert.Equal(0, enemy.Block);
    }

    [Theory]
    [InlineData(0, 45, 48, 40, 43, 24, 28)]
    [InlineData(8, 46, 49, 41, 44, 26, 30)]
    public void WorkerHpTracksPinnedToughEnemiesBreakpoint(
        int asc, int rockMin, int rockMax,
        int silkMin, int silkMax, int exoMin, int exoMax)
    {
        Assert.Equal((rockMin, rockMax),
            PrototypeContent.Enemy(Rock).HpRangeAt(2, asc));
        Assert.Equal((silkMin, silkMax),
            PrototypeContent.Enemy(Silk).HpRangeAt(2, asc));
        Assert.Equal((exoMin, exoMax),
            PrototypeContent.Enemy(Exo).HpRangeAt(2, asc));
    }

    [Fact]
    public void SilkAlternatesWeakAndTwoAttacksWithA9Scaling()
    {
        var silk = PrototypeContent.Enemy(Silk);
        Assert.Equal("toxic_spit", silk.Moves[0].Id);
        Assert.Equal("proto.power.weak", silk.Moves[0].Effects[0].PowerId);
        Assert.Equal("thrash", silk.Moves[1].Id);
        Assert.Equal(2, silk.Moves[1].Effects[0].Repetitions);
        Assert.Equal(4, silk.Moves[1].Effects[0].AmountAt(2, 0));
        Assert.Equal(5, silk.Moves[1].Effects[0].AmountAt(2, 9));
        var engine = new PrototypeGameEngine();
        var state = Commit(State([Enemy(Silk, 43, "even")],
            "hive-silk", 9));
        state = EndTurn(engine, state);
        Assert.Equal("toxic_spit",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        state = EndTurn(engine, state);
        Assert.Equal("thrash",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(90, state.Player.Hp);
    }

    [Fact]
    public void TunnelerBurrowPersistsBlockUntilBrokenAndStunsItsNextIntent()
    {
        const string id = "proto.native.hive.tunneler";
        const string encounterId = "proto.native.hive.encounter.tunneler_weak";
        var encounter = PrototypeContent.Encounter(encounterId);
        Assert.Equal([id], encounter.EnemyIds);
        Assert.Equal(0, encounter.Weight);
        var model = PrototypeContent.Enemy(id);
        Assert.Equal((87, 87), model.HpRangeAt(2, 0));
        Assert.Equal((92, 92), model.HpRangeAt(2, 8));
        Assert.Equal(13, model.Moves[0].Effects[0].AmountAt(2, 0));
        Assert.Equal(15, model.Moves[0].Effects[0].AmountAt(2, 9));
        Assert.Equal(23, model.Moves[2].Effects[0].AmountAt(2, 0));
        Assert.Equal(26, model.Moves[2].Effects[0].AmountAt(2, 9));

        var engine = new PrototypeGameEngine();
        var state = Commit(State([Enemy(id, 87, "only")],
            "hive-tunneler"));
        state = EndTurn(engine, state);
        Assert.Equal(87, state.Player.Hp);
        Assert.Equal("bite",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);

        state = EndTurn(engine, state);
        var burrowed = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("burrow", burrowed.LastMoveId);
        Assert.Equal(32, burrowed.Block);
        Assert.Contains(burrowed.PowerStates,
            power => power.PowerId == "proto.native.hive.burrowed");
        Assert.Equal(2, burrowed.PlannedMoveIndex);

        var unbroken = EndTurn(engine, state.Fork());
        var stayingBurrowed = Assert.Single(
            unbroken.World!.Combat!.Enemies);
        Assert.Equal("below", stayingBurrowed.LastMoveId);
        Assert.Equal(32, stayingBurrowed.Block);
        Assert.Equal(64, unbroken.Player.Hp);
        Assert.Equal(2, stayingBurrowed.PlannedMoveIndex);

        // A blocked hit smaller than the shield must not interrupt it.
        var partial = Hit(state.World.Combat!, 7);
        var partiallyBroken = Assert.Single(partial.Enemies);
        Assert.Equal(25, partiallyBroken.Block);
        Assert.Equal(2, partiallyBroken.PlannedMoveIndex);
        Assert.Contains(partiallyBroken.PowerStates,
            p => p.PowerId == "proto.native.hive.burrowed");

        // Breaking the entire remaining Block forces the public next
        // move to Dizzy and removes the persistent protection.
        var broken = Hit(partial, 25);
        var stunned = Assert.Single(broken.Enemies);
        Assert.Equal(0, stunned.Block);
        Assert.Equal(3, stunned.PlannedMoveIndex);
        Assert.DoesNotContain(stunned.PowerStates,
            p => p.PowerId == "proto.native.hive.burrowed");
        Assert.Equal("dizzy", stunned.AiStateId);

        state = state with
        {
            World = state.World! with { Combat = broken }
        };
        state = EndTurn(engine, state);
        var recovered = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("dizzy", recovered.LastMoveId);
        Assert.Equal(0, recovered.PlannedMoveIndex);
        Assert.Equal(87, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal("bite",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(74, state.Player.Hp);
    }

    [Fact]
    public void TunnelerBurrowAscensionBlockScalesAtA8()
    {
        const string id = "proto.native.hive.tunneler";
        var model = PrototypeContent.Enemy(id);
        Assert.Equal(32, model.Moves[1].Effects[1].AmountAt(2, 0));
        Assert.Equal(37, model.Moves[1].Effects[1].AmountAt(2, 8));
        var state = Commit(State([Enemy(id, 92, "only")],
            "hive-tunneler-a8", asc: 8));
        var engine = new PrototypeGameEngine();
        state = EndTurn(engine, state);
        state = EndTurn(engine, state);
        Assert.Equal(37,
            Assert.Single(state.World!.Combat!.Enemies).Block);
    }

    [Fact]
    public void EveryNewNativeWeakEncounterIsExcludedFromPrototypePools()
    {
        foreach (var id in new[] { RockEncounter, ExoEncounter,
                                 "proto.native.hive.encounter.tunneler_weak" })
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal(0, encounter.Weight);
            Assert.Equal(2, encounter.MinAct);
            Assert.Equal(2, encounter.MaxAct);
            Assert.DoesNotContain(id,
                PrototypeContent.OvergrowthWeakEncounterPool);
        }
    }

    private static EnemyCombatState Enemy(
        string id, int hp, string slot) =>
        new(1, id, hp, 0, 0,
            new Dictionary<string, int>(StringComparer.Ordinal),
            SlotName: slot);

    private static PrototypePowerInstanceState Power(
        string id, int stacks) => new(id, stacks, 1);

    private static RunState State(
        EnemyCombatState[] enemies, string seed, int asc = 0)
    {
        var combat = new CombatState(
            1, 3, 0, [], [], [], [], enemies, 1, [], [], 2,
            Act: 2, Ascension: asc);
        return new RunState(
            "prototype-unbound", "prototype-0.1", seed, seed, 0,
            RunPhase.Combat,
            new PlayerState(100, 100, 0, [], [], new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 1,
                PrototypeRoomType.Combat,
                new MapState([]), combat, null, null, null, null),
            Ascension: asc);
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
            .Single(action => action.Kind == "end_turn")).State;

    private static CombatState Hit(CombatState combat, int damage)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "DamageEnemy", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(null, [combat, 1, damage, 0]);
        Assert.NotNull(result);
        var property = result!.GetType().GetProperty("Combat");
        Assert.NotNull(property);
        return Assert.IsType<CombatState>(property!.GetValue(result));
    }
}
