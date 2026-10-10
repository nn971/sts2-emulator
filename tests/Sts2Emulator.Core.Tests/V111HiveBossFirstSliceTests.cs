using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveBossFirstSliceTests
{
    private static readonly Type Engine = typeof(PrototypeGameEngine);
    private const string Crusher = PrototypeNativeHiveBosses.CrusherId;
    private const string Rocket = PrototypeNativeHiveBosses.RocketId;
    private const string Knowledge = PrototypeNativeHiveBosses.KnowledgeId;
    private const string Insatiable = PrototypeNativeHiveBosses.InsatiableId;

    [Theory]
    [InlineData(0, 209, 199, 379, 321, 12, 31, 17, 8, 28)]
    [InlineData(8, 219, 209, 399, 341, 12, 31, 17, 8, 28)]
    [InlineData(9, 219, 209, 399, 341, 14, 35, 18, 9, 31)]
    public void NativeBossHpAndAttackBreakpointTables(
        int asc, int crusherHp, int rocketHp, int knowledgeHp,
        int insatiableHp, int thrash, int laser, int slap,
        int overwhelming, int bite)
    {
        var left = PrototypeContent.Enemy(Crusher);
        var right = PrototypeContent.Enemy(Rocket);
        var demon = PrototypeContent.Enemy(Knowledge);
        var beast = PrototypeContent.Enemy(Insatiable);
        Assert.Equal((crusherHp, crusherHp), left.HpRangeAt(2, asc));
        Assert.Equal((rocketHp, rocketHp), right.HpRangeAt(2, asc));
        Assert.Equal((knowledgeHp, knowledgeHp), demon.HpRangeAt(2, asc));
        Assert.Equal((insatiableHp, insatiableHp), beast.HpRangeAt(2, asc));
        Assert.Equal(thrash, left.Moves[0].Effects[0].AmountAt(2, asc));
        Assert.Equal(laser, right.Moves[3].Effects[0].AmountAt(2, asc));
        Assert.Equal(slap, demon.Moves[1].Effects[0].AmountAt(2, asc));
        Assert.Equal(overwhelming,
            demon.Moves[2].Effects[0].AmountAt(2, asc));
        Assert.Equal(3, demon.Moves[2].Effects[0].Repetitions);
        Assert.Equal(bite, beast.Moves[2].Effects[0].AmountAt(2, asc));
        Assert.Equal(asc >= 9 ? 3 : 2,
            beast.Moves[3].Effects[0].AmountAt(2, asc));
        Assert.Equal(3, beast.Moves[0].Effects[1].Amount);
        Assert.Equal(3, beast.Moves[0].Effects[2].Amount);
    }

    [Fact]
    public void ThreeNativeBossEncountersStayGated()
    {
        foreach (var (encounterId, expectedCount) in new[]
        {
            (PrototypeNativeHiveBosses.KaiserEncounterId, 2),
            (PrototypeNativeHiveBosses.KnowledgeEncounterId, 1),
            (PrototypeNativeHiveBosses.InsatiableEncounterId, 1)
        })
        {
            var definition = PrototypeContent.Encounter(encounterId);
            Assert.Equal(PrototypeRoomType.Boss, definition.RoomType);
            Assert.Equal((2, 2, 0),
                (definition.MinAct, definition.MaxAct, definition.Weight));
            Assert.Equal(expectedCount, definition.FixedEnemySpecs.Length);
        }
        var crab = PrototypeContent.Encounter(
            PrototypeNativeHiveBosses.KaiserEncounterId);
        Assert.Equal(new[] { "crusher", "rocket" },
            crab.FixedEnemySpecs.Select(spec => spec.SlotName));
        Assert.Equal(new[] { Crusher, Rocket }, crab.EnemyIds);
    }

    [Fact]
    public void CrusherReceivesRageOnceOnRocketDeath()
    {
        var left = Enemy(Crusher, 1, 209,
        [
            new(PrototypeNativeHiveBosses.BackLeftId, 1, 1),
            new(PrototypeNativeHiveBosses.RageId, 1, 2)
        ]);
        var right = Enemy(Rocket, 2, 199,
        [
            new(PrototypeNativeHiveBosses.BackRightId, 1, 3),
            new(PrototypeNativeHiveBosses.RageId, 1, 4)
        ]);
        var combat = EmptyCombat("crab-rage") with
        {
            Enemies = [left, right],
            NextPowerApplicationOrder = 5
        };
        var damage = Engine.GetMethod("DamageEnemy",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(damage);
        var result = damage!.Invoke(null, [combat, 2, 300, 0]);
        Assert.NotNull(result);
        var after = Assert.IsType<CombatState>(
            result!.GetType().GetProperty("Combat")!.GetValue(result));
        var survivor = Assert.Single(after.Enemies,
            enemy => enemy.InstanceId == 1);
        Assert.Equal(99, survivor.Block);
        Assert.Equal(6, survivor.PowerStates
            .Where(p => p.PowerId == "proto.power.strength")
            .Sum(p => p.Stacks));
        Assert.DoesNotContain(survivor.PowerStates,
            p => p.PowerId == PrototypeNativeHiveBosses.RageId);
        Assert.Equal(0, after.Enemies.Single(e => e.InstanceId == 2).Hp);
        Assert.Equal(0, combat.Enemies[0].Block);
    }

    [Fact]
    public void KaiserCrabAppliesSurroundedAndTracksTargetedSide()
    {
        var state = StartBoss(PrototypeNativeHiveBosses.KaiserEncounterId,
            "kaiser-facing");
        var combat = state.World!.Combat!;
        var surrounded = Assert.Single(combat.PlayerPowers,
            p => p.PowerId == PrototypeNativeHiveBosses.SurroundedId);
        Assert.Equal(0, surrounded.StoredValue);

        var method = Engine.GetMethod("ResolveKaiserCrabFacing",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var left = Assert.IsType<CombatState>(
            method!.Invoke(null, [combat, 1]));
        Assert.Equal(1, left.PlayerPowers.Single(
            p => p.PowerId == PrototypeNativeHiveBosses.SurroundedId)
            .StoredValue);
        var right = Assert.IsType<CombatState>(
            method.Invoke(null, [left, 2]));
        Assert.Equal(0, right.PlayerPowers.Single(
            p => p.PowerId == PrototypeNativeHiveBosses.SurroundedId)
            .StoredValue);
        Assert.Equal(0, surrounded.StoredValue); // no mutation
    }

    [Fact]
    public void LiquifyCreatesSixEscapesAndSandpit()
    {
        var engine = new PrototypeGameEngine();
        var state = StartBoss(PrototypeNativeHiveBosses.InsatiableEncounterId,
            "insatiable-liquify");
        state = EndTurn(engine, state);
        var combat = state.World!.Combat!;
        var beast = Assert.Single(combat.Enemies);
        Assert.Equal("liquify_ground", beast.LastMoveId);
        Assert.Equal(4, beast.PowerStates.Single(power =>
            power.PowerId == PrototypeNativeHiveBosses.SandpitId).Stacks);
        var escapes = combat.Cards.Where(card =>
            card.CardId == PrototypeNativeHiveBosses.FranticEscapeId).ToArray();
        Assert.Equal(6, escapes.Length);
        // EndTurn also starts the next player turn, so some generated
        // draw cards may already be in hand. Test the combined zone.
        Assert.Equal(3, combat.DrawPile.Concat(combat.Hand).Count(id =>
            escapes.Any(card => card.InstanceId == id)));
        Assert.Equal(3, combat.DiscardPile.Count(id =>
            escapes.Any(card => card.InstanceId == id)));

        var chosen = escapes.First(card => combat.Hand.Contains(
            card.InstanceId));
        var legal = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == chosen.InstanceId);
        state = engine.Step(state, legal).State;
        var after = state.World!.Combat!;
        Assert.Equal(5, Assert.Single(after.Enemies).PowerStates.Single(
            p => p.PowerId == PrototypeNativeHiveBosses.SandpitId).Stacks);
        Assert.Equal(1, after.Cards.Single(c =>
            c.InstanceId == chosen.InstanceId).CombatEnergyCostDelta);
    }

    [Fact]
    public void KnowledgeDemonMandatoryCurseDoesNotAutoPick()
    {
        var engine = new PrototypeGameEngine();
        var state = StartBoss(PrototypeNativeHiveBosses.KnowledgeEncounterId,
            "demon-knowledge-choice");
        var before = CanonicalJson.Sha256(state);
        var action = engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn");
        var error = Assert.Throws<NotSupportedException>(
            () => engine.Step(state, action));
        Assert.Contains("Curse of Knowledge", error.Message);
        Assert.Equal(before, CanonicalJson.Sha256(state));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void DemonCurseStopsAfterThreeUses(
        int count, bool shouldCurse)
    {
        var enemy = Enemy(Knowledge, 1, 399, []) with
        {
            MoveUseCounts = new Dictionary<string, int>
            {
                ["curse_of_knowledge"] = count
            }
        };
        var state = PrototypeContent.Enemy(Knowledge).Ai!.States
            .Single(s => s.Id == "curse_choice");
        Assert.Equal(shouldCurse,
            PrototypeGameEngine.EnemyAiConditionMatches(
                enemy, [enemy], state.ConditionalBranches![0]));
        Assert.Equal(!shouldCurse,
            PrototypeGameEngine.EnemyAiConditionMatches(
                enemy, [enemy], state.ConditionalBranches![1]));
    }

    private static EnemyCombatState Enemy(
        string enemyId, int instanceId, int hp,
        PrototypePowerInstanceState[] powers) =>
        new(instanceId, enemyId, hp, 0, 0,
            new Dictionary<string, int>(StringComparer.Ordinal),
            Powers: powers);

    private static CombatState EmptyCombat(string seed) =>
        new(1, 3, 0, [], [], [], [], [], 1, [], [], 1,
            Act: 2);

    private static RunState StartBoss(string encounterId, string seed)
    {
        var state = BaseState(seed);
        var method = Engine.GetMethod("StartCombat",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<RunState>(method!.Invoke(null,
        [
            state, PrototypeRoomType.Boss,
            PrototypeContent.Encounter(encounterId)
        ]));
    }

    private static RunState BaseState(string seed) =>
        new("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(9000, 9000, 0, [], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 1,
                PrototypeRoomType.Boss, new MapState([]),
                EmptyCombat(seed), null, null, null, null));

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;
}
