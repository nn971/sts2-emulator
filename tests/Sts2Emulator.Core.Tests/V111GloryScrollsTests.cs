using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryScrollsTests
{
    private static readonly Type Engine = typeof(PrototypeGameEngine);
    private const string Scroll = PrototypeNativeGloryScrolls.ScrollId;

    [Theory]
    [InlineData(0, 30, 37, 14, 5)]
    [InlineData(8, 33, 39, 14, 5)]
    [InlineData(9, 33, 39, 16, 6)]
    public void SourceHpAndAttackScaling(int asc, int minHp,
        int maxHp, int chomp, int chew)
    {
        var enemy = PrototypeContent.Enemy(Scroll);
        Assert.Equal((minHp, maxHp), enemy.HpRangeAt(3, asc));
        Assert.Equal(chomp, enemy.Moves[0].Effects[0].AmountAt(3, asc));
        Assert.Equal(chew, enemy.Moves[1].Effects[0].AmountAt(3, asc));
        Assert.Equal(2, enemy.Moves[1].Effects[0].Repetitions);
        Assert.Equal(2, enemy.Moves[2].Effects[0].Amount);
        Assert.Equal(2, Assert.Single(enemy.StartingPowers!).Stacks);
        Assert.Equal(1, PrototypeContent.Power(
            PrototypeNativeGloryScrolls.PaperCutsId)
            .PlayerMaxHpLossOnUnblockedAttackHitPerStack);
    }

    [Fact]
    public void BothScrollEncountersRemainForcedOnly()
    {
        foreach (var (id, count) in new[]
        {
            (PrototypeNativeGloryScrolls.WeakEncounterId, 3),
            (PrototypeNativeGloryScrolls.NormalEncounterId, 4)
        })
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal(PrototypeRoomType.Combat, encounter.RoomType);
            Assert.Equal((3, 3, 0),
                (encounter.MinAct, encounter.MaxAct, encounter.Weight));
            Assert.Equal(count, encounter.FixedEnemySpecs.Length);
            Assert.All(encounter.FixedEnemySpecs, spec =>
                Assert.Equal(Scroll, spec.EnemyId));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactlyOneCoordinatedRotationAndFixedNormalTail(bool normal)
    {
        var id = normal
            ? PrototypeNativeGloryScrolls.NormalEncounterId
            : PrototypeNativeGloryScrolls.WeakEncounterId;
        var observedFirstPhases = new HashSet<int>();
        for (var seed = 0; seed < 48; seed++)
        {
            var state = StartCombat(id, $"glory-scroll-formation-{seed}");
            var enemies = state.World!.Combat!.Enemies;
            Assert.Equal(normal ? 4 : 3, enemies.Length);
            var first = enemies.Take(3).Select(e =>
                e.PlannedMoveIndex!.Value).ToArray();
            Assert.Equal(new[] { 0, 1, 2 }, first.Order().ToArray());
            Assert.Equal((first[0] + 1) % 3, first[1]);
            Assert.Equal((first[0] + 2) % 3, first[2]);
            observedFirstPhases.Add(first[0]);
            if (normal)
            {
                Assert.Equal(2, enemies[3].PlannedMoveIndex);
                Assert.Equal("more_teeth", enemies[3].AiStateId);
            }
            Assert.All(enemies, e =>
            {
                Assert.Equal(2, Assert.Single(e.PowerStates).Stacks);
                Assert.InRange(e.MaxHp, 30, 37);
                Assert.Equal(e.MaxHp, e.Hp);
            });
        }
        Assert.Equal(3, observedFirstPhases.Count);
    }

    [Fact]
    public void PaperCutsCostsTwoMaxHpPerUnblockedHitNotPerMove()
    {
        var engine = new PrototypeGameEngine();
        var state = StartCombat(PrototypeNativeGloryScrolls.WeakEncounterId,
            "scrolls-paper-cuts");
        var before = state.Player.MaxHp;
        state = EndTurn(engine, state);

        Assert.Equal(before - 6, state.Player.MaxHp);
        // One Chomp (14), two Chew hits (5 + 5),
        // and one non-attacking More Teeth.
        Assert.Equal(9000 - 24, state.Player.Hp);
        var enemy = Assert.Single(state.World!.Combat!.Enemies,
            item => item.LastMoveId == "more_teeth");
        Assert.Equal(2, enemy.PowerStates.Single(p =>
            p.PowerId == "proto.power.strength").Stacks);

        // Permanent run HP loss also survives an independent state fork.
        var fork = state.Fork();
        Assert.Equal(state.Player.MaxHp, fork.Player.MaxHp);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(fork));
    }

    [Fact]
    public void FullyBlockedScrollAttacksPreserveMaximumHp()
    {
        var engine = new PrototypeGameEngine();
        var state = StartCombat(PrototypeNativeGloryScrolls.WeakEncounterId,
            "scrolls-block");
        var combat = state.World!.Combat!;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with { PlayerBlock = 1000 }
            }
        };
        state = EndTurn(engine, state);
        Assert.Equal(9000, state.Player.MaxHp);
        Assert.Equal(9000, state.Player.Hp);
        Assert.Equal(3, state.World!.Combat!.Enemies.Length);
    }

    [Fact]
    public void FourScrollFirstRoundAndRandomFollowUpsRemainDeterministic()
    {
        var engine = new PrototypeGameEngine();
        var state = StartCombat(PrototypeNativeGloryScrolls.NormalEncounterId,
            "scrolls-normal-replay");
        var original = state.Fork();
        state = EndTurn(engine, state);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(EndTurn(engine, original)));
        Assert.Equal(2, Assert.Single(
            state.World!.Combat!.Enemies,
            e => e.FormationPosition == 3).PowerStates.Single(
            p => p.PowerId == "proto.power.strength").Stacks);
        Assert.Equal(8994, state.Player.MaxHp);
        // A second turn exercises the Chomp/Chew random branch.
        var next = state.Fork();
        state = EndTurn(engine, state);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(EndTurn(engine, next)));
        Assert.Equal(4, state.World!.Combat!.Enemies.Length);
    }

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
            .Single(a => a.Kind == "end_turn")).State;
}
