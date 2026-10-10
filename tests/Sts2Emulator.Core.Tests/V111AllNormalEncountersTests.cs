using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111AllNormalEncountersTests
{
    private static readonly Type EngineType = typeof(PrototypeGameEngine);

    [Fact]
    public void AllFortyOneNormalEncounterIdsHaveExecutableDefinitions()
    {
        var normals = V111Coverage.Create().Items.Where(item =>
            item.Kind == "encounters" && item.NativeId.EndsWith(
                "_NORMAL", StringComparison.Ordinal)).ToArray();
        Assert.Equal(41, normals.Length);
        Assert.All(normals, item =>
        {
            Assert.True(item.Registered, item.NativeId);
            var id = Assert.Single(item.EngineIds);
            Assert.Equal(PrototypeRoomType.Combat,
                PrototypeContent.Encounter(id).RoomType);
        });
        var added = PrototypeNativeRemainingNormals.Encounters;
        Assert.Equal(7, added.Length);
        Assert.All(added, encounter =>
        {
            Assert.Equal(0, encounter.Weight);
            Assert.Contains(encounter.MinAct, new[] { 2, 3 });
            Assert.Equal(encounter.MinAct, encounter.MaxAct);
        });
        Assert.DoesNotContain("TunnelerNormal",
            PrototypeNativeLaterActs.HiveEncounterNames);
    }

    [Theory]
    [InlineData(0, 148, 231, 150, 93, 106)]
    [InlineData(8, 158, 247, 155, 99, 111)]
    [InlineData(9, 158, 247, 155, 99, 111)]
    public void SourceBreakpointsForRemainingGloryNormals(
        int asc, int globe, int owl, int fabricator, int lost,
        int forgotten)
    {
        Assert.Equal(globe, PrototypeContent.Enemy(
            PrototypeNativeRemainingNormals.GlobeHeadId).HpAt(3,asc));
        Assert.Equal(owl, PrototypeContent.Enemy(
            PrototypeNativeRemainingNormals.OwlMagistrateId).HpAt(3,asc));
        Assert.Equal(fabricator, PrototypeContent.Enemy(
            PrototypeNativeRemainingNormals.FabricatorId).HpAt(3,asc));
        Assert.Equal(lost, PrototypeContent.Enemy(
            PrototypeNativeRemainingNormals.LostId).HpAt(3,asc));
        Assert.Equal(forgotten, PrototypeContent.Enemy(
            PrototypeNativeRemainingNormals.ForgottenId).HpAt(3,asc));

        var axe = PrototypeContent.Enemy(
            PrototypeNativeRemainingNormals.AxebotId);
        Assert.Equal(asc >= 8 ? (76,86) : (70,78),
            axe.HpRangeAt(3,asc));
        Assert.Equal(2, Assert.Single(axe.StartingPowers!).Stacks);
    }

    [Theory]
    [InlineData(PrototypeNativeRemainingNormals.MenagerieNormalId,3)]
    [InlineData(PrototypeNativeRemainingNormals.FabricatorNormalId,1)]
    [InlineData(PrototypeNativeRemainingNormals.LostNormalId,2)]
    [InlineData(PrototypeNativeRemainingNormals.TunnelerNormalId,2)]
    public void FormationsAndFirstIntentsAreForkDeterministic(
        string id, int expectedCount)
    {
        var state = Begin(id, "all-normals-" + id);
        var fight = state.World!.Combat!;
        Assert.Equal(expectedCount, fight.Enemies.Length);
        Assert.All(fight.Enemies, enemy =>
        {
            Assert.True(enemy.Hp > 0);
            Assert.NotNull(enemy.PlannedMoveIndex);
        });
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
        var engine = new PrototypeGameEngine();
        var replay = state.Fork();
        state = EndTurn(engine, state);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(EndTurn(engine, replay)));
    }

    [Fact]
    public void MenagerieUsesTwoShieldedCubexAndAnArtifactPunchConstruct()
    {
        var combat = Begin(PrototypeNativeRemainingNormals.MenagerieNormalId,
            "menagerie-shields").World!.Combat!;
        Assert.Equal(new[]
        {
            "proto.enemy.punch_construct",
            "proto.enemy.cubex_construct",
            "proto.enemy.cubex_construct"
        }, combat.Enemies.Select(enemy => enemy.EnemyId));
        Assert.Equal(new[] { 0,13,13 },
            combat.Enemies.Select(enemy => enemy.Block));
        Assert.All(combat.Enemies, e =>
            Assert.Equal(1, e.PowerStates.Single(p =>
                p.PowerId == "proto.power.artifact").Stacks));
    }

    [Fact]
    public void GlobeHeadGalvanizesPowerCardsBeforeTheFirstDrawAndShocksOnPlay()
    {
        var power = new CardInstance(1, "proto.silent.noxious_fumes", 0,
            PrototypeJson.EmptyObject());
        var state = Begin(PrototypeNativeRemainingNormals.GlobeHeadNormalId,
            "globe-galvanic-power", 9, [power]);
        var combat = state.World!.Combat!;
        var card = Assert.Single(combat.Cards);
        var affliction = Assert.IsType<PrototypeCardAffliction>(
            card.Affliction);
        Assert.Equal(PrototypeCardAfflictionKind.Galvanized, affliction.Kind);
        Assert.Equal(8, affliction.Amount);
        Assert.Equal(1, affliction.SourceEnemyInstanceId);
        Assert.Equal(8, Assert.Single(combat.Enemies[0].PowerStates).Stacks);

        var engine = new PrototypeGameEngine();
        var play = engine.GetLegalActions(state)
            .Single(a => a.Kind == "play_card");
        var after = engine.Step(state, play).State;
        Assert.Equal(8992, after.Player.Hp);
        Assert.Equal(9000, state.Player.Hp);
        Assert.Equal(PrototypeCardAfflictionKind.Galvanized,
            Assert.Single(state.World!.Combat!.Cards).Affliction?.Kind);
    }

    [Fact]
    public void OwlJudicialFlightProtectsFromPoweredAttacksOnly()
    {
        var engine = new PrototypeGameEngine();
        var state = Begin(PrototypeNativeRemainingNormals.OwlNormalId,
            "owl-flight");
        for (var i=0;i<3;i++)
            state = EndTurn(engine,state);
        var owl = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("judicial_flight",owl.LastMoveId);
        Assert.Contains(owl.PowerStates,p =>
            p.PowerId == PrototypeNativeRemainingNormals.SoarId);
        var direct = Damage(state.World.Combat, owl.InstanceId, 40, false);
        var powered = Damage(state.World.Combat, owl.InstanceId, 40, true);
        Assert.Equal(40, owl.Hp - direct.Enemies[0].Hp);
        Assert.Equal(20, owl.Hp - powered.Enemies[0].Hp);
        state = EndTurn(engine,state);
        Assert.DoesNotContain(state.World!.Combat!.Enemies[0].PowerStates,
            p=>p.PowerId == PrototypeNativeRemainingNormals.SoarId);
        Assert.Equal("verdict",state.World.Combat.Enemies[0].LastMoveId);
    }

    [Fact]
    public void LostAndForgottenStealDifferentStatsAndRefundAfterDeath()
    {
        var engine = new PrototypeGameEngine();
        var state = Begin(PrototypeNativeRemainingNormals.LostNormalId,
            "stat-thieves");
        state = EndTurn(engine,state);
        var combat = state.World!.Combat!;
        Assert.Equal(-2,combat.PlayerPowers.Where(p =>
            p.PowerId == "proto.power.strength").Sum(p=>p.Stacks));
        Assert.Equal(-2,combat.PlayerPowers.Where(p =>
            p.PowerId == "proto.power.dexterity").Sum(p=>p.Stacks));
        Assert.Equal(2,combat.Enemies[0].PowerStates.Where(p =>
            p.PowerId == "proto.power.strength").Sum(p=>p.Stacks));
        Assert.Equal(2,combat.Enemies[1].PowerStates.Where(p =>
            p.PowerId == "proto.power.dexterity").Sum(p=>p.Stacks));

        combat=Damage(combat,combat.Enemies[0].InstanceId,1000,true);
        Assert.Equal(0,combat.PlayerPowers.Where(p =>
            p.PowerId == "proto.power.strength").Sum(p=>p.Stacks));
        Assert.Equal(-2,combat.PlayerPowers.Where(p =>
            p.PowerId == "proto.power.dexterity").Sum(p=>p.Stacks));
        combat=Damage(combat,combat.Enemies[1].InstanceId,1000,true);
        Assert.Equal(0,combat.PlayerPowers.Where(p =>
            p.PowerId == "proto.power.dexterity").Sum(p=>p.Stacks));
    }

    [Fact]
    public void AxebotHasTwoStockedRespawnsWithOneAdditionalBootPerLife()
    {
        var state = Begin(PrototypeNativeRemainingNormals.AxebotsNormalId,
            "axebot-stocks");
        var combat = state.World!.Combat!;
        Assert.Equal("hammer_uppercut",
            PrototypeContent.Enemy(combat.Enemies[0].EnemyId)
                .Moves[combat.Enemies[0].PlannedMoveIndex!.Value].Id);
        foreach(var expectedStock in new[]{1,0})
        {
            var target = combat.Enemies.Last(enemy=>enemy.Hp>0);
            combat=Damage(combat,target.InstanceId,1000,true);
            var respawn = EngineType.GetMethod("ResolveEnemyDeathSummons",
                BindingFlags.NonPublic|BindingFlags.Static)!;
            combat = Assert.IsType<CombatState>(respawn.Invoke(null,
                [combat,state.Rng,1]));
            var current=combat.Enemies.Last(enemy=>enemy.Hp>0);
            Assert.InRange(current.MaxHp,
                70 +10*(2-expectedStock),
                86 +10*(2-expectedStock));
            Assert.Equal(expectedStock,current.PowerStates
                .Where(power=>power.PowerId==
                    PrototypeNativeRemainingNormals.StockId)
                .Sum(power=>power.Stacks));
            Assert.NotEqual(target.InstanceId,current.InstanceId);
        }
    }

    [Fact]
    public void FabricatorAddsMinionsWithinFourReservedBotSlots()
    {
        var engine=new PrototypeGameEngine();
        var sawDefender=false;
        var sawAttacker=false;
        for(var seed=0;seed<12;seed++)
        {
            var state=Begin(PrototypeNativeRemainingNormals.FabricatorNormalId,
                "fabricator-seed-"+seed);
            Assert.Equal("fabricator",state.World!.Combat!.Enemies[0].SlotName);
            Assert.Equal(2,state.World.Combat.Enemies[0].FormationPosition);
            state=EndTurn(engine,state);
            var combat=state.World!.Combat!;
            var bots=combat.Enemies.Where(e=>e.EnemyId!=
                PrototypeNativeRemainingNormals.FabricatorId).ToArray();
            Assert.InRange(bots.Length,1,2);
            Assert.All(bots,bot=>
            {
                Assert.True(PrototypeContent.Enemy(bot.EnemyId).IsMinion);
                Assert.Equal(1,bot.LeaderEnemyInstanceId);
                Assert.Contains(bot.SlotName,
                    new[]{"bot1","bot2","bot3","bot4"});
            });
            sawDefender |= bots.Any(bot=>bot.EnemyId is
                PrototypeNativeRemainingNormals.NoisebotId or
                PrototypeNativeRemainingNormals.GuardbotId);
            sawAttacker |= bots.Any(bot=>bot.EnemyId is
                PrototypeNativeRemainingNormals.ZapbotId or
                PrototypeNativeRemainingNormals.StabbotId);
            Assert.Equal(CanonicalJson.Sha256(state),
                CanonicalJson.Sha256(state.Fork()));
        }
        Assert.True(sawDefender);
        Assert.True(sawAttacker);
    }

    private static CombatState Damage(CombatState combat,int enemyId,
        int amount,bool powered)
    {
        var method=EngineType.GetMethod("DamageEnemyInternal",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var result=method.Invoke(null,[combat,enemyId,amount,0,powered])!;
        return Assert.IsType<CombatState>(
            result.GetType().GetProperty("Combat")!.GetValue(result));
    }

    private static RunState EndTurn(PrototypeGameEngine engine,
        RunState state)=>engine.Step(state,
            engine.GetLegalActions(state).Single(action=>
                action.Kind=="end_turn")).State;

    private static RunState Begin(string id,string seed,int asc=0,
        CardInstance[]? deck = null)
    {
        var state=new RunState("prototype-unbound","prototype-0.1",
            seed,seed,0,RunPhase.Combat,
            new PlayerState(9000,9000,0,deck ?? [],[],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,id ==
                    PrototypeNativeRemainingNormals.TunnelerNormalId
                    ? 2 : 3,1,1,PrototypeRoomType.Combat,
                new MapState([]),
                new CombatState(1,3,0,[],[],[],[],[],1,
                    [],[],1,Act:3,Ascension:asc),
                null,null,null,null),
            Ascension:asc);
        var start=EngineType.GetMethod("StartCombat",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        return Assert.IsType<RunState>(start.Invoke(null,
            [state,PrototypeRoomType.Combat,PrototypeContent.Encounter(id)]));
    }
}
