using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111CompleteWeakEncounterTests
{
    private static readonly Type Engine = typeof(PrototypeGameEngine);
    private const string Shield = PrototypeNativeGloryWeak.LivingShieldId;
    private const string Turret = PrototypeNativeGloryWeak.TurretOperatorId;
    private const string Rampart = PrototypeNativeGloryWeak.RampartId;

    [Fact]
    public void AllFifteenPinnedOrdinaryWeakEncountersAreRegistered()
    {
        var report = V111Coverage.Create();
        var weak = report.Items.Where(item => item.Kind == "encounters"
            && item.NativeId.EndsWith("_WEAK", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(15, weak.Length);
        Assert.All(weak, item =>
        {
            Assert.Equal("single-player", item.Scope);
            Assert.True(item.Registered, item.NativeId);
            Assert.Single(item.EngineIds);
            var encounter = PrototypeContent.Encounter(item.EngineIds[0]);
            Assert.Equal(PrototypeRoomType.Combat, encounter.RoomType);
        });
        var glory = weak.Where(item => item.NativeId is
            "DEVOTED_SCULPTOR_WEAK" or "SCROLLS_OF_BITING_WEAK"
            or "TURRET_OPERATOR_WEAK").ToArray();
        Assert.Equal(3, glory.Length);
        Assert.All(glory, item =>
        {
            var encounter = PrototypeContent.Encounter(item.EngineIds[0]);
            Assert.Equal((3, 3, 0),
                (encounter.MinAct, encounter.MaxAct, encounter.Weight));
            Assert.Equal("missing", item.Integration);
            Assert.Equal("unverified", item.Validation);
        });
    }

    [Theory]
    [InlineData(0, 55, 41, 16, 3)]
    [InlineData(8, 65, 51, 16, 3)]
    [InlineData(9, 65, 51, 18, 4)]
    public void TurretOperatorWeakFormationAndAscensionBreakpoints(
        int asc, int shieldHp, int turretHp, int smash, int bullet)
    {
        var model = PrototypeContent.Enemy(Shield);
        var turret = PrototypeContent.Enemy(Turret);
        Assert.Equal((shieldHp, shieldHp), model.HpRangeAt(3, asc));
        Assert.Equal((turretHp, turretHp), turret.HpRangeAt(3, asc));
        Assert.Equal(6, model.Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(smash, model.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(3, model.Moves[1].Effects[1].Amount);
        Assert.Equal(bullet, turret.Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(5, turret.Moves[0].Effects[0].Repetitions);

        var encounter = PrototypeContent.Encounter(
            PrototypeNativeGloryWeak.TurretOperatorWeakId);
        Assert.Equal((3, 3, 0),
            (encounter.MinAct, encounter.MaxAct, encounter.Weight));
        Assert.Equal(new[] { Shield, Turret },
            encounter.EnemyIds);
        Assert.Equal(new[] { 0, 1 },
            encounter.FixedEnemySpecs.Select(s => s.FormationPosition));

        var state = StartEncounter("turret-weak-breakpoint-" + asc, asc);
        Assert.Equal(new[] { shieldHp, turretHp },
            state.World!.Combat!.Enemies.Select(e => e.Hp));
        Assert.Equal(new[] { 0, 0 },
            state.World.Combat.Enemies.Select(e => e.PlannedMoveIndex!.Value));
        Assert.Equal(25, state.World.Combat.Enemies[1].Block);
        Assert.Equal(0, state.World.Combat.Enemies[0].Block);
        Assert.Equal(25, Assert.Single(
            state.World.Combat.Enemies[0].PowerStates).Stacks);
    }

    [Fact]
    public void RampartProtectsTurretOnOpeningAndEveryPlayerSideStart()
    {
        var engine = new PrototypeGameEngine();
        var state = StartEncounter("turret-rampart-timing");
        Assert.Equal(25, state.World!.Combat!.Enemies[1].Block);
        var hash = CanonicalJson.Sha256(state);
        var fork = state.Fork();
        Assert.Equal(hash, CanonicalJson.Sha256(fork));

        state = EndTurn(engine, state);
        var enemies = state.World!.Combat!.Enemies;
        Assert.Equal("shield_slam", enemies[0].LastMoveId);
        Assert.Equal("unload", enemies[1].LastMoveId);
        Assert.Equal(8979, state.Player.Hp);
        Assert.Equal(25, enemies[1].Block);
        Assert.Equal(0, enemies[0].PlannedMoveIndex);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(EndTurn(engine, fork)));

        state = EndTurn(engine, state);
        enemies = state.World!.Combat!.Enemies;
        Assert.Equal("shield_slam", enemies[0].LastMoveId);
        Assert.Equal("unload_2", enemies[1].LastMoveId);
        Assert.Equal(25, enemies[1].Block);
        state = EndTurn(engine, state);
        Assert.Equal("reload", state.World!.Combat!.Enemies[1].LastMoveId);
        Assert.Equal(1, state.World.Combat.Enemies[1].PowerStates.Single(
            p => p.PowerId == "proto.power.strength").Stacks);
    }

    [Fact]
    public void RemovingShieldEndsFutureRampartGrantsWithoutRemovingTurret()
    {
        var engine = new PrototypeGameEngine();
        var state = StartEncounter("turret-owner-defeated");
        state = ReplaceCombat(state, Hit(
            state.World!.Combat!, enemyInstanceId: 1, damage: 999));
        var before = state.Fork();
        Assert.Equal(0, state.World!.Combat!.Enemies[0].Hp);
        Assert.Equal(25, state.World.Combat.Enemies[1].Block);
        state = EndTurn(engine, state);
        Assert.Equal(0, state.World!.Combat!.Enemies[1].Block);
        Assert.Equal("unload", state.World.Combat.Enemies[1].LastMoveId);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(EndTurn(engine, before)));
        state = EndTurn(engine, state);
        Assert.Equal(0, state.World!.Combat!.Enemies[1].Block);
    }

    [Fact]
    public void ShieldSwitchesPermanentlyToSmashWhenTurretIsKilled()
    {
        var engine = new PrototypeGameEngine();
        var state = StartEncounter("turret-ally-defeated");
        state = ReplaceCombat(state, Hit(
            state.World!.Combat!, enemyInstanceId: 2, damage: 999));
        Assert.Equal(0, state.World!.Combat!.Enemies[1].Hp);
        state = EndTurn(engine, state);
        var shield = state.World!.Combat!.Enemies[0];
        Assert.Equal("shield_slam", shield.LastMoveId);
        Assert.Equal(1, shield.PlannedMoveIndex);
        Assert.Equal(8994, state.Player.Hp);

        state = EndTurn(engine, state);
        shield = state.World!.Combat!.Enemies[0];
        Assert.Equal("smash", shield.LastMoveId);
        Assert.Equal(1, shield.PlannedMoveIndex);
        Assert.Equal(3, shield.PowerStates.Single(p =>
            p.PowerId == "proto.power.strength").Stacks);
        Assert.Equal(8978, state.Player.Hp);
        state = EndTurn(engine, state);
        shield = state.World!.Combat!.Enemies[0];
        Assert.Equal("smash", shield.LastMoveId);
        Assert.Equal(6, shield.PowerStates.Single(p =>
            p.PowerId == "proto.power.strength").Stacks);
    }

    [Fact]
    public void IndependentRampartOwnersContributeOnlyWhileAlive()
    {
        var first = new EnemyCombatState(1, Shield, 55, 0, 0,
            new Dictionary<string, int>(),
            Powers: [new(Rampart, 25, 1)]);
        var second = new EnemyCombatState(2, Shield, 55, 0, 0,
            new Dictionary<string, int>(),
            Powers: [new(Rampart, 25, 2)]);
        var turret = new EnemyCombatState(3, Turret, 41, 0, 0,
            new Dictionary<string, int>());
        var combat = new CombatState(1, 3, 0, [], [], [], [],
            [first, second, turret], 1, [], [], 3, Act: 3);
        var both = PrototypeGameEngine
            .GrantEnemyAllyBlockOnPlayerTurnStart(combat);
        Assert.Equal(50, both.Enemies[2].Block);
        Assert.Equal(0, combat.Enemies[2].Block);

        var oneDead = combat with
        {
            Enemies = [first with { Hp = 0 }, second, turret]
        };
        var remaining = PrototypeGameEngine
            .GrantEnemyAllyBlockOnPlayerTurnStart(oneDead);
        Assert.Equal(25, remaining.Enemies[2].Block);
        var targetDead = oneDead with
        {
            Enemies = [first, second, turret with { Hp = 0 }]
        };
        Assert.Equal(0, PrototypeGameEngine
            .GrantEnemyAllyBlockOnPlayerTurnStart(targetDead)
            .Enemies[2].Block);
    }

    private static RunState StartEncounter(string seed, int asc = 0)
    {
        var initial = new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(9000, 9000, 0, [], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 3, 1, 1,
                PrototypeRoomType.Combat, new MapState([]),
                new CombatState(1, 3, 0, [], [], [], [], [], 1,
                    [], [], 1, Act: 3, Ascension: asc),
                null, null, null, null),
            Ascension: asc);
        var method = Engine.GetMethod("StartCombat",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<RunState>(method!.Invoke(null,
        [
            initial, PrototypeRoomType.Combat,
            PrototypeContent.Encounter(
                PrototypeNativeGloryWeak.TurretOperatorWeakId)
        ]));
    }

    private static CombatState Hit(CombatState combat,
        int enemyInstanceId, int damage)
    {
        var method = Engine.GetMethod("DamageEnemy",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = method!.Invoke(null,
            [combat, enemyInstanceId, damage, 0]);
        Assert.NotNull(result);
        return Assert.IsType<CombatState>(
            result!.GetType().GetProperty("Combat")!.GetValue(result));
    }

    private static RunState ReplaceCombat(RunState state,
        CombatState combat) =>
        state with
        {
            World = state.World! with { Combat = combat }
        };

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;
}
