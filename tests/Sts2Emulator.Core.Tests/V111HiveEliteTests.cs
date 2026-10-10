using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveEliteTests
{
    private const string Front = PrototypeNativeHiveElites.FrontId;
    private const string Middle = PrototypeNativeHiveElites.MiddleId;
    private const string Back = PrototypeNativeHiveElites.BackId;
    private const string Ento = PrototypeNativeHiveElites.EntomancerId;
    private const string Prism = PrototypeNativeHiveElites.PrismId;

    [Theory]
    [InlineData(0, 40, 46, 5, 6, 8, 145, 18, 7, 161, 15, 20, 2)]
    [InlineData(8, 46, 52, 5, 6, 8, 165, 18, 7, 171, 15, 22, 2)]
    [InlineData(9, 46, 52, 6, 7, 9, 165, 20, 8, 171, 17, 22, 3)]
    public void ElitesRespectPinnedHpAndAscensionNumbers(
        int a, int min, int max, int writhe, int bulk, int constrict,
        int entoHp, int spear, int beesCount,
        int prismHp, int jab, int pulsateBlock, int spark)
    {
        foreach (var id in new[] { Front, Middle, Back })
        {
            var segment = PrototypeContent.Enemy(id);
            Assert.Equal((min, max), segment.HpRangeAt(2, a));
            Assert.True(segment.UniqueEvenInitialHp);
            Assert.True(segment.ReattachesWithLivingAlly);
            Assert.Equal(writhe, segment.Moves[0].Effects[0].AmountAt(2, a));
            Assert.Equal(2, segment.Moves[0].Effects[0].Repetitions);
            Assert.Equal(bulk, segment.Moves[1].Effects[0].AmountAt(2, a));
            Assert.Equal(2, segment.Moves[1].Effects[1].Amount);
            Assert.Equal(constrict,
                segment.Moves[2].Effects[0].AmountAt(2, a));
            Assert.Equal(25, Assert.Single(segment.StartingPowers!).Stacks);
            Assert.Equal(25, segment.Moves[4].Effects[0].Amount);
        }

        var ento = PrototypeContent.Enemy(Ento);
        Assert.Equal((entoHp, entoHp), ento.HpRangeAt(2, a));
        Assert.Equal(new[] { "bees", "spear", "pheromone_spit" },
            ento.Moves.Select(move => move.Id));
        Assert.Equal(beesCount, ento.Moves[0].Effects[0].RepetitionsAt(a));
        Assert.Equal(spear, ento.Moves[1].Effects[0].AmountAt(2, a));

        var prism = PrototypeContent.Enemy(Prism);
        Assert.Equal((prismHp, prismHp), prism.HpRangeAt(2, a));
        Assert.Equal(jab, prism.Moves[0].Effects[0].AmountAt(2, a));
        Assert.Equal(pulsateBlock,
            prism.Moves[3].Effects[1].AmountAt(2, a));
        Assert.Equal(spark, Assert.Single(prism.StartingPowers!).StacksAt(a));
        Assert.Equal(spark, prism.Moves[3].Effects[2].AmountAt(2, a));
        Assert.Equal(new[] { "jab", "radiate", "whirlwind", "pulsate" },
            prism.Moves.Select(move => move.Id));
    }

    [Fact]
    public void AllThreeElitesAreRegisteredButNotSelectedByNativeRoute()
    {
        var deci = PrototypeContent.Encounter(
            PrototypeNativeHiveElites.DecimillipedeEncounterId);
        Assert.Equal(new[] { Front, Middle, Back },
            deci.ResolveEnemySpecs(PrototypeRng.CreateBundle("deci"))
                .Select(spec => spec.EnemyId));
        Assert.Equal(new[] { "segment1", "segment2", "segment3" },
            deci.FixedEnemySpecs.Select(spec => spec.SlotName));
        Assert.Equal(new[] { "writhe", "bulk", "constrict" },
            deci.CyclicOpeningAiStateIds);
        foreach (var id in new[]
        {
            PrototypeNativeHiveElites.DecimillipedeEncounterId,
            PrototypeNativeHiveElites.EntomancerEncounterId,
            PrototypeNativeHiveElites.PrismsEncounterId
        })
        {
            var definition = PrototypeContent.Encounter(id);
            Assert.Equal((2, 2, 0, PrototypeRoomType.Elite),
                (definition.MinAct, definition.MaxAct, definition.Weight,
                    definition.RoomType));
            Assert.DoesNotContain(id,
                PrototypeContent.OvergrowthNormalEncounterPool);
        }
    }

    [Fact]
    public void DeadSegmentKeepsInstanceAndReattachesAfterDeadMove()
    {
        var engine = new PrototypeGameEngine();
        var state = Commit(ThreeSegments("deci-reattach"));
        var combat = state.World!.Combat!;
        var original = combat.Enemies[0];
        var killed = Damage(combat, original.InstanceId, 999);
        var dead = killed.Enemies[0];
        Assert.Equal(0, dead.Hp);
        Assert.Equal("dead", dead.AiStateId);
        Assert.Equal(3, dead.PlannedMoveIndex);
        state = ReplaceCombat(state, killed);

        state = EndTurn(engine, state);
        dead = state.World!.Combat!.Enemies[0];
        Assert.Equal("dead", dead.LastMoveId);
        Assert.Equal(0, dead.Hp);
        Assert.Equal(4, dead.PlannedMoveIndex);
        state = EndTurn(engine, state);
        var attached = state.World!.Combat!.Enemies[0];
        Assert.Equal(original.InstanceId, attached.InstanceId);
        Assert.Equal(Front, attached.EnemyId);
        Assert.Equal("reattach", attached.LastMoveId);
        Assert.Equal(25, attached.Hp);
        Assert.Contains(attached.PowerStates, power =>
            power.PowerId == PrototypeNativeHiveElites.ReattachId);
        Assert.Equal(3, state.World.Combat.Enemies.Length);
    }

    [Fact]
    public void LastSegmentDoesNotReattachAndCanTriggerFatal()
    {
        var combat = ThreeSegments("deci-last").World!.Combat!;
        var allButLast = combat.Enemies.Select((enemy, index) =>
            index < 2 ? enemy with { Hp = 0 } : enemy).ToArray();
        combat = combat with { Enemies = allButLast };
        var check = typeof(PrototypeGameEngine).GetMethod(
            "ShouldEnemyDeathTriggerFatal",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(check);
        Assert.True((bool)check!.Invoke(null, [combat, 3])!);
        Assert.False((bool)check.Invoke(null, [combat with
        {
            Enemies = combat.Enemies.Select((enemy, index) =>
                index == 0 ? enemy with { Hp = 40 } : enemy).ToArray()
        }, 3])!);
    }

    [Fact]
    public void PersonalHiveOnHitShufflesDazedAndSpitCapsAtThree()
    {
        var engine = new PrototypeGameEngine();
        var state = Commit(Single(Ento, "ento-pheromone"));
        state = AddCardToHand(state, "proto.silent.strike");
        var originalHash = CanonicalJson.Sha256(state);
        var play = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card");
        var attacked = engine.Step(state, play).State;
        var combat = attacked.World!.Combat!;
        Assert.Contains(combat.DrawPile.Select(id => combat.Cards.Single(c =>
            c.InstanceId == id).CardId), card => card == "proto.status.dazed");
        Assert.Equal(originalHash, CanonicalJson.Sha256(state));

        for (var i = 0; i < 6; i++)
            attacked = EndTurn(engine, attacked);
        var ento = Assert.Single(attacked.World!.Combat!.Enemies);
        Assert.Equal(3, Stack(ento, PrototypeNativeHiveElites.PersonalHiveId));
        Assert.Equal(2, Stack(ento, "proto.power.strength"));
        for (var i = 0; i < 3; i++)
            attacked = EndTurn(engine, attacked);
        ento = Assert.Single(attacked.World!.Combat!.Enemies);
        Assert.Equal(3, Stack(ento, PrototypeNativeHiveElites.PersonalHiveId));
        Assert.Equal(4, Stack(ento, "proto.power.strength"));
    }

    [Fact]
    public void PrismTaintsSkillThenExpiresAfterEnemySideTurn()
    {
        var engine = new PrototypeGameEngine();
        var state = Commit(AddCardToHand(
            Single(Prism, "prism-tainted"),
            "proto.silent.defend"));
        state = ReplaceCombat(state,
            RefreshSkillAura(state.World!.Combat!));
        var card = Assert.Single(state.World!.Combat!.Cards);
        Assert.NotNull(card.Affliction);
        Assert.Equal(PrototypeCardAfflictionKind.Tainted, card.Affliction.Kind);
        Assert.Equal(2, card.Affliction.Amount);
        Assert.Equal(1, card.Affliction.SourceEnemyInstanceId);

        var play = engine.GetLegalActions(state).Single(a =>
            a.Kind == "play_card");
        state = engine.Step(state, play).State;
        Assert.Equal(2, Assert.Single(state.World!.Combat!.PlayerPowers,
            power => power.PowerId == PrototypeNativeHiveElites.TaintedId)
            .Stacks);
        state = EndTurn(engine, state);
        Assert.DoesNotContain(state.World!.Combat!.PlayerPowers,
            power => power.PowerId == PrototypeNativeHiveElites.TaintedId);
        state = EndTurn(engine, state);
        state = EndTurn(engine, state);
        state = EndTurn(engine, state);
        var prism = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("pulsate", prism.LastMoveId);
        Assert.Equal(4,
            Stack(prism, PrototypeNativeHiveElites.VitalSparkId));
        Assert.Equal(4, state.World.Combat.Cards
            .Single(c => c.InstanceId == 1).Affliction!.Amount);
    }

    private static int Stack(EnemyCombatState enemy, string id) =>
        enemy.PowerStates.Where(power => power.PowerId == id)
            .Sum(power => power.Stacks);

    private static CombatState Damage(
        CombatState combat, int enemyId, int amount)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "DamageEnemy", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(null, [combat, enemyId, amount, 0]);
        Assert.NotNull(result);
        var property = result!.GetType().GetProperty("Combat");
        Assert.NotNull(property);
        return Assert.IsType<CombatState>(property!.GetValue(result));
    }

    private static CombatState RefreshSkillAura(CombatState combat)
    {
        var method = typeof(PrototypeGameEngine).GetMethod(
            "RefreshEnemySkillAfflictions",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<CombatState>(method!.Invoke(null, [combat]));
    }

    private static RunState AddCardToHand(RunState state, string cardId)
    {
        var card = new CardInstance(1, cardId, 0,
            PrototypeJson.EmptyObject());
        var combatCard = new CombatCardInstance(1, 1, cardId,
            0, false, card.PersistentState);
        return state with
        {
            Player = state.Player with { Deck = [card] },
            World = state.World! with
            {
                Combat = state.World.Combat! with
                {
                    Hand = [1], Cards = [combatCard],
                    NextCardInstanceId = 2
                }
            }
        };
    }

    private static RunState ThreeSegments(string seed)
    {
        var ids = new[] { Front, Middle, Back };
        var initial = new[] { "writhe", "bulk", "constrict" };
        var enemies = ids.Select((id, i) =>
            new EnemyCombatState(i + 1, id, 40 + 2 * i, 0, 0,
                new Dictionary<string, int>(StringComparer.Ordinal),
                Powers: [new PrototypePowerInstanceState(
                    PrototypeNativeHiveElites.ReattachId, 25, i + 1)],
                AiStateId: initial[i],
                FormationPosition: i,
                SlotName: "segment" + (i + 1))).ToArray();
        return Base(seed, enemies, 4);
    }

    private static RunState Single(string enemyId, string seed)
    {
        var definition = PrototypeContent.Enemy(enemyId);
        long order = 1;
        var powers = (definition.StartingPowers ??
            Array.Empty<PrototypeStartingPowerSpec>())
            .Select(power => new PrototypePowerInstanceState(
                power.PowerId, power.StacksAt(0), order++)).ToArray();
        var enemy = new EnemyCombatState(1, enemyId,
            definition.HpAt(2, 0), 0, 0,
            new Dictionary<string, int>(StringComparer.Ordinal),
            Powers: powers, AiStateId: definition.Ai?.InitialStateId);
        return Base(seed, [enemy], order);
    }

    private static RunState Base(string seed, EnemyCombatState[] enemies,
        long nextPowerOrder)
    {
        var combat = new CombatState(1, 3, 0, [], [], [], [],
            enemies, 1, [], [], nextPowerOrder, Act: 2);
        return new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(9000, 9000, 0, [], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 1,
                PrototypeRoomType.Elite, new MapState([]),
                combat, null, null, null, null));
    }

    private static RunState ReplaceCombat(
        RunState state, CombatState combat) =>
        state with
        {
            World = state.World! with { Combat = combat }
        };

    private static RunState Commit(RunState state) =>
        ReplaceCombat(state,
            PrototypeGameEngine.CommitEnemyIntents(
                state.World!.Combat!, state.Rng));

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;
}
