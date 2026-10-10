using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryBossRosterTests
{
    private static readonly (string Id, PrototypeRoomType Type)[] Remaining =
    [
        (PrototypeNativeGloryBosses.KnightsEncounterId, PrototypeRoomType.Elite),
        (PrototypeNativeGloryBosses.QueenEncounterId, PrototypeRoomType.Boss),
        (PrototypeNativeGloryBosses.TestSubjectEncounterId, PrototypeRoomType.Boss),
        (PrototypeNativeGloryBosses.AeonglassEncounterId, PrototypeRoomType.Boss)
    ];

    [Fact]
    public void EveryGloryEliteAndBossHasAnExecutableGatedFixture()
    {
        var ids = new[]
        {
            PrototypeNativeGloryElites.SoulNexusEncounterId,
            PrototypeNativeGloryElites.MechaKnightEncounterId
        }.Concat(Remaining.Select(item => item.Id)).ToArray();
        Assert.Equal(6, ids.Length);
        Assert.Equal(6, ids.Distinct().Count());
        foreach (var id in ids)
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal((3, 3, 0),
                (encounter.MinAct, encounter.MaxAct, encounter.Weight));
            Assert.All(encounter.EnemyIds, enemyId =>
                Assert.NotNull(PrototypeContent.Enemy(enemyId)));
        }

        var native = V111Coverage.Create().Items.Where(item =>
            item.Kind == "encounters" && item.Scope == "single-player"
            && (item.NativeId.EndsWith("_ELITE", StringComparison.Ordinal)
                || item.NativeId.EndsWith("_BOSS", StringComparison.Ordinal)))
            .ToArray();
        Assert.All(native.Where(item =>
            item.NativeId is "KNIGHTS_ELITE" or "QUEEN_BOSS"
                or "TEST_SUBJECT_BOSS" or "AEONGLASS_BOSS"
                or "SOUL_NEXUS_ELITE" or "MECHA_KNIGHT_ELITE"),
            item =>
            {
                Assert.True(item.Registered);
                Assert.Equal("missing", item.Integration);
                Assert.Equal("unverified", item.Validation);
            });
    }

    [Theory]
    [InlineData(0, 101, 93, 82, 199, 400, 100, 512)]
    [InlineData(8, 108, 97, 89, 211, 419, 111, 535)]
    [InlineData(9, 108, 97, 89, 211, 419, 111, 535)]
    public void AllNewModelsHavePinnedToughEnemyBreakpoints(int asc,
        int flail, int spectral, int magi, int amalgam,
        int queen, int subject, int aeonglass)
    {
        var ids = new[]
        {
            PrototypeNativeGloryBosses.FlailKnightId,
            PrototypeNativeGloryBosses.SpectralKnightId,
            PrototypeNativeGloryBosses.MagiKnightId,
            PrototypeNativeGloryBosses.AmalgamId,
            PrototypeNativeGloryBosses.QueenId,
            PrototypeNativeGloryBosses.TestSubjectId,
            PrototypeNativeGloryBosses.AeonglassId
        };
        var hp = new[] { flail, spectral, magi, amalgam,
            queen, subject, aeonglass };
        for (var i = 0; i < ids.Length; i++)
            Assert.Equal((hp[i], hp[i]),
                PrototypeContent.Enemy(ids[i]).HpRangeAt(3, asc));
    }

    [Fact]
    public void KnightFormationMoveOrderAndHexDampenOwnership()
    {
        var formation = PrototypeContent.Encounter(
            PrototypeNativeGloryBosses.KnightsEncounterId).FixedEnemySpecs;
        Assert.Equal(new[] { "first", "second", "third" },
            formation.Select(item => item.SlotName));
        Assert.Equal(new[] { 0, 1, 2 },
            formation.Select(item => item.FormationPosition));

        var flail = PrototypeContent.Enemy(PrototypeNativeGloryBosses.FlailKnightId);
        Assert.Equal(2, flail.Moves[1].Effects[0].Repetitions);
        Assert.Equal(2, flail.Ai!.States.Single(item =>
            item.Id == "random").Branches!.Single(item =>
            item.TargetStateId == "flail").Weight);
        var spectral = PrototypeContent.Enemy(
            PrototypeNativeGloryBosses.SpectralKnightId);
        Assert.Equal(PrototypeNativeGloryBosses.HexId,
            spectral.Moves[0].Effects[0].PowerId);
        var magi = PrototypeContent.Enemy(PrototypeNativeGloryBosses.MagiKnightId);
        Assert.Equal(2, magi.MoveLoopStartIndex);
        Assert.Equal(new[] { "power_shield", "dampen", "ram", "prep", "magic_bomb" },
            magi.Moves.Select(move => move.Id));
        Assert.Equal(PrototypeNativeGloryBosses.DampenId,
            magi.Moves[1].Effects[0].PowerId);
        Assert.True(PrototypeContent.Power(PrototypeNativeGloryBosses.DampenId)
            .RestoreDowngradedCardsWhenLastSourceRemoved);
        Assert.True(PrototypeContent.Power(PrototypeNativeGloryBosses.HexId)
            .ClearSourceAfflictionWhenRemoved);
    }

    [Fact]
    public void QueenChangesToEnrageImmediatelyWhenAmalgamDiesDuringBrightIntent()
    {
        var engine = new PrototypeGameEngine();
        var state = Start(PrototypeNativeGloryBosses.QueenEncounterId, "queen");
        var formation = state.World!.Combat!.Enemies;
        Assert.Equal(new[] {
            PrototypeNativeGloryBosses.AmalgamId,
            PrototypeNativeGloryBosses.QueenId
        }, formation.Select(enemy => enemy.EnemyId));
        Assert.Equal(formation[1].InstanceId,
            formation[0].LeaderEnemyInstanceId);
        Assert.Equal(0, formation[1].PlannedMoveIndex);

        state = EndTurn(engine, state);
        state = EndTurn(engine, state);
        Assert.Equal(2, state.World!.Combat!.Enemies[1].PlannedMoveIndex);
        state = HitEnemy(state, formation[0].InstanceId, 10000);
        var queen = state.World!.Combat!.Enemies[1];
        Assert.Equal(5, queen.PlannedMoveIndex);
        Assert.Equal("enrage", queen.AiStateId);
        var fork = state.Fork();
        state = EndTurn(engine, state);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(EndTurn(engine, fork)));
        queen = state.World!.Combat!.Enemies[1];
        Assert.Equal("enrage", queen.LastMoveId);
        Assert.Equal(3, queen.PlannedMoveIndex);
        Assert.Equal(2, queen.PowerStates.Where(power =>
            power.PowerId == "proto.power.strength").Sum(power => power.Stacks));
    }

    [Fact]
    public void TestSubjectRevivesTwiceInPlaceThenDiesInThirdForm()
    {
        var engine = new PrototypeGameEngine();
        var state = Start(PrototypeNativeGloryBosses.TestSubjectEncounterId,
            "test-subject");
        var id = Assert.Single(state.World!.Combat!.Enemies).InstanceId;

        foreach (var phase in new[] { 1, 2 })
        {
            state = HitEnemy(state, id, 10000);
            var pending = Assert.Single(state.World!.Combat!.Enemies);
            Assert.Equal(1, pending.Hp);
            Assert.True(pending.BossPendingRevival);
            Assert.Equal(3, pending.PlannedMoveIndex);
            Assert.Equal(id, pending.InstanceId);
            var fork = state.Fork();
            state = EndTurn(engine, state);
            Assert.Equal(CanonicalJson.Sha256(state),
                CanonicalJson.Sha256(EndTurn(engine, fork)));
            var revived = Assert.Single(state.World!.Combat!.Enemies);
            Assert.Equal(phase, revived.BossPhase);
            Assert.Equal(phase == 1 ? 200 : 300, revived.MaxHp);
            Assert.Equal(revived.MaxHp, revived.Hp);
            Assert.False(revived.BossPendingRevival);
            Assert.Equal(phase == 1 ? "claw" : "lacerate",
                revived.AiStateId);
            Assert.Equal(id, revived.InstanceId);
        }

        Assert.Contains(Assert.Single(state.World!.Combat!.Enemies).PowerStates,
            p => p.PowerId == PrototypeNativeGloryBosses.NemesisId);
        // Nemesis alternates Intangible across enemy turns. Put the
        // final form at one HP so its per-hit cap remains respected.
        var finalCombat = state.World!.Combat!;
        state = state with
        {
            World = state.World with
            {
                Combat = finalCombat with
                {
                    Enemies = finalCombat.Enemies.Select(enemy =>
                        enemy.InstanceId == id
                            ? enemy with { Hp = 1 }
                            : enemy).ToArray()
                }
            }
        };
        state = HitEnemy(state, id, 10000);
        var final = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(0, final.Hp);
        Assert.False(final.BossPendingRevival);
    }

    [Fact]
    public void AeonglassGeneratesWitherEverySixPlayedCardsAndIntensifiesIt()
    {
        var engine = new PrototypeGameEngine();
        var state = Start(PrototypeNativeGloryBosses.AeonglassEncounterId,
            "aeonglass");
        var combat = state.World!.Combat!;
        var shivs = Enumerable.Range(1, 6).Select(i =>
            new CombatCardInstance(i, null, "proto.silent.shiv",
                0, true, PrototypeJson.EmptyObject())).ToArray();
        state = state with
        {
            World = state.World! with
            {
                Combat = combat with
                {
                    Hand = shivs.Select(card => card.InstanceId).ToArray(),
                    Cards = shivs,
                    NextCardInstanceId = 7
                }
            }
        };
        var enemyId = Assert.Single(combat.Enemies).InstanceId;
        for (var i = 0; i < shivs.Length; i++)
        {
            var action = GameAction.Create("play_card",
                new PlayCardPayload(shivs[i].InstanceId, enemyId));
            state = engine.Step(state, action).State;
        }
        combat = state.World!.Combat!;
        Assert.Single(combat.Cards.Where(card =>
            card.CardId == PrototypeNativeGloryBosses.WitherId));
        Assert.Equal(6, Assert.Single(combat.Enemies).PowerStates.Single(power =>
            power.PowerId == PrototypeNativeGloryBosses.WitheringPresenceId).Stacks);
        Assert.Contains(combat.Hand, id =>
            combat.Cards.Any(card => card.InstanceId == id
                && card.CardId == PrototypeNativeGloryBosses.WitherId));

        // Ebb then Eye Lasers then Increasing Intensity.
        for (var turn = 0; turn < 3; turn++)
            state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal("increasing_intensity", Assert.Single(combat.Enemies).LastMoveId);
        Assert.Equal(1, Assert.Single(combat.Enemies).WitherUpgradeCount);
        var withers = combat.Cards.Where(card =>
            card.CardId == PrototypeNativeGloryBosses.WitherId).ToArray();
        Assert.True(withers.Length >= 2);
        Assert.All(withers, card => Assert.Equal(1, card.WitherFakeUpgradeLevel));
        Assert.Equal(0, PrototypeContent.Card(
            PrototypeNativeGloryBosses.WitherId).MaxUpgradeLevel);
    }

    private static RunState Start(string encounterId, string seed)
    {
        var initial = new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(9000, 9000, 0, [], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 3, 1, 1,
                PrototypeRoomType.Boss, new MapState([]),
                new CombatState(1, 3, 0, [], [], [], [], [],
                    1, [], [], 1, Act: 3),
                null, null, null, null));
        var encounter = PrototypeContent.Encounter(encounterId);
        var method = typeof(PrototypeGameEngine).GetMethod(
            "StartCombat", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<RunState>(method!.Invoke(null,
        [
            initial, encounter.RoomType, encounter
        ]));
    }

    private static RunState HitEnemy(RunState state, int enemyId,
        int damage)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "DamageEnemyInternal", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var damageResult = method!.Invoke(null, [
            state.World!.Combat!, enemyId, damage, 0, true
        ])!;
        var combat = Assert.IsType<CombatState>(
            damageResult.GetType().GetProperty("Combat")!.GetValue(damageResult));
        return state with { World = state.World with { Combat = combat } };
    }

    private static RunState EndTurn(PrototypeGameEngine engine,
        RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;
}
