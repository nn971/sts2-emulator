using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryNormalFirstSliceTests
{
    private static readonly Type Engine = typeof(PrototypeGameEngine);

    [Theory]
    [InlineData(0, 191, 261, 13, 21, 35, 4, 30, 15)]
    [InlineData(8, 199, 281, 13, 21, 35, 4, 30, 19)]
    [InlineData(9, 199, 281, 14, 23, 40, 5, 33, 19)]
    public void NativeHpDamageAndPlatingHaveCorrectAscensionBreakpoints(
        int asc, int frogHp, int berserkerHp, int lash, int strike,
        int charge, int pummel, int smother, int plating)
    {
        var frog = PrototypeContent.Enemy(
            PrototypeNativeGloryNormals.FrogKnightId);
        var berserker = PrototypeContent.Enemy(
            PrototypeNativeGloryNormals.SlimedBerserkerId);
        Assert.Equal((frogHp, frogHp), frog.HpRangeAt(3, asc));
        Assert.Equal((berserkerHp, berserkerHp),
            berserker.HpRangeAt(3, asc));
        Assert.Equal(lash, frog.Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(strike, frog.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(charge, frog.Moves[3].Effects[0].AmountAt(3, asc));
        Assert.Equal(plating, frog.StartingPowers![0].StacksAt(asc));
        Assert.Equal(pummel,
            berserker.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(4, berserker.Moves[1].Effects[0].Repetitions);
        Assert.Equal(smother,
            berserker.Moves[3].Effects[0].AmountAt(3, asc));
    }

    [Fact]
    public void EncountersAreGatedFromNativeGloryProgression()
    {
        foreach (var id in new[]
        {
            PrototypeNativeGloryNormals.FrogKnightEncounterId,
            PrototypeNativeGloryNormals.SlimedBerserkerEncounterId
        })
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal(PrototypeRoomType.Combat, encounter.RoomType);
            Assert.Equal((3, 3, 0),
                (encounter.MinAct, encounter.MaxAct, encounter.Weight));
            Assert.Single(encounter.FixedEnemySpecs);
        }
    }

    [Fact]
    public void HalfHealthUsesRolledMaxHpAndOneTimeChargeGuard()
    {
        var model = PrototypeContent.Enemy(
            PrototypeNativeGloryNormals.FrogKnightId);
        var halfHealth = model.Ai!.States.Single(
            x => x.Id == "half_health");
        var high = Enemy(96);
        var low = Enemy(94);
        var threshold = Enemy(95);
        Assert.True(PrototypeGameEngine.EnemyAiConditionMatches(
            high, [high], halfHealth.ConditionalBranches![1]));
        Assert.True(PrototypeGameEngine.EnemyAiConditionMatches(
            low, [low], halfHealth.ConditionalBranches![2]));
        Assert.True(PrototypeGameEngine.EnemyAiConditionMatches(
            threshold, [threshold], halfHealth.ConditionalBranches![1]));
        var once = low with
        {
            MoveUseCounts = new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                ["beetle_charge"] = 1
            }
        };
        Assert.True(PrototypeGameEngine.EnemyAiConditionMatches(
            once, [once], halfHealth.ConditionalBranches![0]));
        // The same current HP has different meanings for different
        // rolled maximums, including when the native HP range varies.
        var stronger = low with { MaxHp = 170 };
        Assert.True(PrototypeGameEngine.EnemyAiConditionMatches(
            stronger, [stronger], halfHealth.ConditionalBranches![1]));
    }

    [Fact]
    public void FrogKnightPlatingAndHealthDecisionSurviveFork()
    {
        var engine = new PrototypeGameEngine();
        var state = StartCombat(
            PrototypeNativeGloryNormals.FrogKnightEncounterId,
            "glory-frog");
        var frog = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal((191, 15, 0),
            (frog.MaxHp, frog.Block, frog.PlannedMoveIndex));

        state = EndTurn(engine, state);
        Assert.Equal("tongue_lash", Assert.Single(
            state.World!.Combat!.Enemies).LastMoveId);
        state = EndTurn(engine, state);
        Assert.Equal("strike_down_evil", Assert.Single(
            state.World!.Combat!.Enemies).LastMoveId);

        var healthy = state.Fork();
        var combat = state.World!.Combat!;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Enemies = [combat.Enemies[0] with { Hp = 94 }]
                }
            }
        };
        var source = state.Fork();
        state = EndTurn(engine, state);
        frog = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("for_the_queen", frog.LastMoveId);
        Assert.Equal(3, frog.PlannedMoveIndex);
        Assert.Equal(5, frog.PowerStates.Single(
            p => p.PowerId == "proto.power.strength").Stacks);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(EndTurn(engine, source)));

        var committed = state.Fork();
        state = EndTurn(engine, state);
        frog = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("beetle_charge", frog.LastMoveId);
        Assert.Equal(0, frog.PlannedMoveIndex);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(EndTurn(engine, committed)));

        var healthyNext = EndTurn(engine, healthy);
        Assert.Equal(0, Assert.Single(
            healthyNext.World!.Combat!.Enemies).PlannedMoveIndex);
    }

    [Fact]
    public void BerserkerGeneratedSlimedAndMoveCycle()
    {
        var engine = new PrototypeGameEngine();
        var state = StartCombat(
            PrototypeNativeGloryNormals.SlimedBerserkerEncounterId,
            "glory-slimed");
        state = EndTurn(engine, state);
        var combat = state.World!.Combat!;
        Assert.Equal("vomit_ichor", Assert.Single(combat.Enemies).LastMoveId);
        var slimes = combat.Cards.Where(
            c => c.CardId == "proto.status.slimed").ToArray();
        Assert.Equal(10, slimes.Length);
        Assert.Equal(10, slimes.Count(c =>
            combat.DiscardPile.Contains(c.InstanceId)
            || combat.DrawPile.Contains(c.InstanceId)
            || combat.Hand.Contains(c.InstanceId)));

        state = EndTurn(engine, state);
        Assert.Equal("furious_pummeling", Assert.Single(
            state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(8984, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal("leeching_hug", Assert.Single(
            state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(3, Assert.Single(
            state.World!.Combat!.Enemies).PowerStates.Single(
                p => p.PowerId == "proto.power.strength").Stacks);
        Assert.Contains(state.World!.Combat!.PlayerPowers,
            p => p.PowerId == "proto.power.weak" && p.Stacks > 0);

        state = EndTurn(engine, state);
        Assert.Equal("smother", Assert.Single(
            state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(8951, state.Player.Hp);
        state = EndTurn(engine, state);
        Assert.Equal("vomit_ichor", Assert.Single(
            state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(20, state.World!.Combat!.Cards.Count(
            c => c.CardId == "proto.status.slimed"));
    }

    private static EnemyCombatState Enemy(int hp) =>
        new(1, PrototypeNativeGloryNormals.FrogKnightId,
            hp, 0, 0, new Dictionary<string, int>(), MaxHp: 191);

    private static RunState StartCombat(string encounterId, string seed)
    {
        var state = new RunState("prototype-unbound", "prototype-0.1",
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
        var start = Engine.GetMethod("StartCombat",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(start);
        return Assert.IsType<RunState>(start!.Invoke(null,
        [
            state, PrototypeRoomType.Combat,
            PrototypeContent.Encounter(encounterId)
        ]));
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;
}
