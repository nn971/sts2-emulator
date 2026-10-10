using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111FabricatorNormalTests
{
    private const string Fabricator = PrototypeNativeGloryNewNormals.FabricatorId;
    private const string Zap = PrototypeNativeGloryNewNormals.ZapbotId;
    private const string Stab = PrototypeNativeGloryNewNormals.StabbotId;
    private const string Guard = PrototypeNativeGloryNewNormals.GuardbotId;
    private const string Noise = PrototypeNativeGloryNewNormals.NoisebotId;

    [Fact]
    public void EveryPinnedOrdinaryNormalEncounterIsRegistered()
    {
        var normals = V111Coverage.Create().Items
            .Where(item => item.Kind == "encounters"
                && item.Scope == "single-player"
                && item.NativeId.EndsWith("_NORMAL",
                    StringComparison.Ordinal)).ToArray();
        Assert.Equal(41, normals.Length);
        Assert.All(normals, item =>
        {
            Assert.True(item.Registered, item.NativeId);
            Assert.NotEmpty(item.EngineIds);
        });
        Assert.All(normals.Where(item =>
            PrototypeContent.Encounter(item.EngineIds[0]).MinAct >= 2),
            item => Assert.Equal("missing", item.Integration));
    }

    [Theory]
    [InlineData(0, 150, 18, 11, 14, 11, 16, 23)]
    [InlineData(8, 155, 18, 11, 14, 11, 17, 24)]
    [InlineData(9, 155, 21, 13, 15, 12, 17, 24)]
    public void FabricatorAndBotNumbersFollowPinnedSource(
        int asc, int fabricatorHp, int strike, int disintegrate,
        int zapDamage, int stabDamage, int guardMin, int botMax)
    {
        var boss = PrototypeContent.Enemy(Fabricator);
        Assert.Equal((fabricatorHp, fabricatorHp),
            boss.HpRangeAt(3, asc));
        Assert.Equal(new[] { "fabricate", "fabricating_strike",
            "disintegrate" }, boss.Moves.Select(m => m.Id));
        Assert.Equal(strike, boss.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(disintegrate, boss.Moves[2].Effects[0].AmountAt(3, asc));
        Assert.Equal(zapDamage,
            PrototypeContent.Enemy(Zap).Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(stabDamage,
            PrototypeContent.Enemy(Stab).Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(guardMin,
            PrototypeContent.Enemy(Guard).HpRangeAt(3, asc).Min);
        Assert.Equal(botMax,
            PrototypeContent.Enemy(Noise).HpRangeAt(3, asc).Max);
        Assert.All(new[] { Zap, Stab, Guard, Noise },
            id => Assert.True(PrototypeContent.Enemy(id).IsMinion));
        Assert.Equal(2, PrototypeContent.Enemy(Zap)
            .StartingPowers!.Single(p =>
                p.PowerId ==
                    PrototypeNativeGloryNewNormals.HighVoltageId).Stacks);
        Assert.Equal(1, PrototypeContent.Power(
            PrototypeNativeGloryNewNormals.HighVoltageId)
            .EnemyStrengthAtSideTurnEndPerStack);
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeGloryNewNormals.FabricatorEncounterId);
        Assert.Equal((3, 3, 0),
            (encounter.MinAct, encounter.MaxAct, encounter.Weight));
        Assert.Equal("fabricator",
            Assert.Single(encounter.FixedEnemySpecs).SlotName);
        Assert.Equal(2, encounter.FixedEnemySpecs[0].FormationPosition);
    }

    [Fact]
    public void FabricateCreatesOneDefenderThenAggressorInNativeSlots()
    {
        var engine = new PrototypeGameEngine();
        var state = Start("fabricator-spawn");
        state = ForceBossMove(state, 0);
        state = EndTurn(engine, state);
        var formation = state.World!.Combat!.Enemies;
        Assert.Equal(3, formation.Length);
        Assert.Equal(Fabricator, formation[0].EnemyId);
        Assert.Contains(formation[1].EnemyId, new[] { Guard, Noise });
        Assert.Contains(formation[2].EnemyId, new[] { Zap, Stab });
        Assert.Equal(new[] { "fabricator", "bot1", "bot2" },
            formation.Select(e => e.SlotName));
        Assert.Equal(new[] { 2, 0, 1 },
            formation.Select(e => e.FormationPosition));
        Assert.All(formation.Skip(1),
            e =>
            {
                Assert.Equal(formation[0].InstanceId,
                    e.LeaderEnemyInstanceId);
                Assert.True(e.SkipNextEnemyAction);
            });
        var fork = state.Fork();
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(fork));
    }

    [Fact]
    public void RepeatedAggressiveSpawnExcludesPreviousBotType()
    {
        var engine = new PrototypeGameEngine();
        var state = Start("fabricator-last-spawned");
        state = EndTurn(engine, ForceBossMove(state, 0));
        var last = state.World!.Combat!.Enemies.Last().EnemyId;
        Assert.Contains(last, new[] { Zap, Stab });
        state = EndTurn(engine, ForceBossMove(state, 1));
        var bots = state.World!.Combat!.Enemies
            .Where(e => e.LeaderEnemyInstanceId == 1).ToArray();
        Assert.Equal(3, bots.Length);
        Assert.NotEqual(last, bots[^1].EnemyId);
        Assert.Equal("bot3", bots[^1].SlotName);
        Assert.Equal(3, bots[^1].FormationPosition);
    }

    [Fact]
    public void FourLivingBotsForceDisintegrateIntent()
    {
        var state = Start("fabricator-full-formation");
        var combat = state.World!.Combat!;
        var parent = combat.Enemies[0] with
        {
            PlannedMoveIndex = null,
            PlannedNextAiStateId = null,
            AiStateId = "branch"
        };
        var bots = new[] { Guard, Noise, Zap, Stab }
            .Select((id, i) => new EnemyCombatState(
                i + 2, id, 20, 0, 0,
                new Dictionary<string, int>(),
                FormationPosition:
                    PrototypeNativeGloryNewNormals.FabricatorBotPositions[i],
                SlotName:
                    PrototypeNativeGloryNewNormals.FabricatorBotSlots[i],
                LeaderEnemyInstanceId: 1,
                SkipNextEnemyAction: true)).ToArray();
        var full = combat with
        {
            Enemies = new[] { parent }.Concat(bots).ToArray()
        };
        var committed = PrototypeGameEngine.CommitEnemyIntents(
            full, state.Rng.Fork());
        Assert.Equal(2, committed.Enemies[0].PlannedMoveIndex);
        Assert.Equal("branch", committed.Enemies[0].PlannedNextAiStateId);
    }

    [Fact]
    public void AllBotEffectsAreTypedAndTargetTheCorrectSide()
    {
        var guard = Assert.Single(PrototypeContent.Enemy(Guard).Moves);
        Assert.Equal(PrototypeEnemyEffectKind.GrantBlockToEnemyType,
            guard.Effects[0].Kind);
        Assert.Equal(Fabricator, guard.Effects[0].EnemyId);
        Assert.Equal(15, guard.Effects[0].Amount);
        var noise = Assert.Single(PrototypeContent.Enemy(Noise).Moves);
        Assert.Equal(new[] {
            PrototypeEnemyEffectKind.AddCardsToDiscard,
            PrototypeEnemyEffectKind.AddCardsToRandomDraw
        }, noise.Effects.Select(e => e.Kind));
        Assert.All(noise.Effects, e =>
            Assert.Equal("proto.status.dazed", e.CardId));
    }

    private static RunState Start(string seed)
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
                    [], [], 1, Act: 3), null, null, null, null));
        var method = typeof(PrototypeGameEngine).GetMethod(
            "StartCombat", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<RunState>(method!.Invoke(null,
        [
            initial, PrototypeRoomType.Combat,
            PrototypeContent.Encounter(
                PrototypeNativeGloryNewNormals.FabricatorEncounterId)
        ]));
    }

    private static RunState ForceBossMove(RunState state, int moveIndex)
    {
        var combat = state.World!.Combat!;
        return state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Enemies = combat.Enemies.Select((enemy, i) =>
                        i == 0
                            ? enemy with
                            {
                                PlannedMoveIndex = moveIndex,
                                PlannedNextAiStateId = "branch"
                            }
                            : enemy).ToArray()
                }
            }
        };
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;
}
