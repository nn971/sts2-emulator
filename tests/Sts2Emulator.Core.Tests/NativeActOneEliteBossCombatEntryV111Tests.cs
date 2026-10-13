using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeActOneEliteBossCombatEntryV111Tests
{
    private static readonly PrototypeGameEngine Engine = new();

    // Independent expected catalog extracted from the pinned source
    // EncounterModel.GenerateMonsters() implementations.
    public static IEnumerable<object[]> Cases()
    {
        static object[] E(
            ActIdentity act, PrototypeRoomType room,
            string id, string native,
            string[] monsters, string?[] slots) =>
            [act, room, id, native, monsters, slots];

        yield return E(ActIdentity.Overgrowth, PrototypeRoomType.Elite,
            "proto.encounter.bygone_effigy_elite", "BygoneEffigyElite",
            ["proto.enemy.bygone_effigy"], [null]);
        yield return E(ActIdentity.Overgrowth, PrototypeRoomType.Elite,
            "proto.encounter.byrdonis_elite", "ByrdonisElite",
            ["proto.enemy.byrdonis"], [null]);
        yield return E(ActIdentity.Overgrowth, PrototypeRoomType.Elite,
            "proto.encounter.phrog_parasite_elite", "PhrogParasiteElite",
            ["proto.enemy.phrog_parasite"], ["phrog"]);
        yield return E(ActIdentity.Overgrowth, PrototypeRoomType.Boss,
            "proto.encounter.ceremonial_beast_boss", "CeremonialBeastBoss",
            ["proto.enemy.ceremonial_beast"], [null]);
        yield return E(ActIdentity.Overgrowth, PrototypeRoomType.Boss,
            "proto.encounter.the_kin_boss", "TheKinBoss",
            [
                "proto.enemy.kin_follower",
                "proto.enemy.kin_follower",
                "proto.enemy.kin_priest"
            ], ["slot1", "slot2", "leaderSlot"]);
        yield return E(ActIdentity.Overgrowth, PrototypeRoomType.Boss,
            "proto.encounter.vantom_boss", "VantomBoss",
            ["proto.enemy.vantom"], [null]);
        yield return E(ActIdentity.Underdocks, PrototypeRoomType.Elite,
            "proto.encounter.phantasmal_gardeners_elite",
            "PhantasmalGardenersElite",
            Enumerable.Repeat("proto.enemy.phantasmal_gardener", 4).ToArray(),
            ["first", "second", "third", "fourth"]);
        yield return E(ActIdentity.Underdocks, PrototypeRoomType.Elite,
            "proto.encounter.skulking_colony_elite", "SkulkingColonyElite",
            ["proto.enemy.skulking_colony"], [null]);
        yield return E(ActIdentity.Underdocks, PrototypeRoomType.Elite,
            "proto.encounter.terror_eel_elite", "TerrorEelElite",
            ["proto.enemy.terror_eel"], [null]);
        yield return E(ActIdentity.Underdocks, PrototypeRoomType.Boss,
            "proto.encounter.lagavulin_matriarch_boss",
            "LagavulinMatriarchBoss",
            ["proto.enemy.lagavulin_matriarch"], [null]);
        yield return E(ActIdentity.Underdocks, PrototypeRoomType.Boss,
            "proto.encounter.soul_fysh_boss", "SoulFyshBoss",
            ["proto.enemy.soul_fysh"], [null]);
        yield return E(ActIdentity.Underdocks, PrototypeRoomType.Boss,
            "proto.encounter.waterfall_giant_boss", "WaterfallGiantBoss",
            ["proto.enemy.waterfall_giant"], [null]);
    }

    private static RunState Initial(string seed, ActIdentity act)
    {
        var configuration = RunConfiguration.Create(firstAct: act) with
        {
            FidelityId = V111Build.NativeWeakCombatEntryFidelityId
        };
        var state = Engine.Step(
            V111RunFactory.Create(seed, configuration),
            GameAction.Empty("start_run")).State;
        for (var n = 0;
            n < 30 && state.Phase is RunPhase.Event or RunPhase.Reward; n++)
            state = Engine.Step(state, Engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        return state;
    }

    private static NativeEncounterFormationV111 PrepareDirect(
        RunState state, string id, PrototypeRoomType room)
    {
        var floor = room == PrototypeRoomType.Boss ? 16 : 5;
        var node = state.World!.Map.Nodes.First(x => x.Floor == floor);
        var world = state.World with
        {
            Floor = floor,
            ActiveRoom = room,
            Map = state.World.Map with { CurrentNodeId = node.NodeId }
        };
        return NativeActOneEliteBossCombatEntryV111.Prepare(
            state, world, PrototypeContent.Encounter(id));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AllTwelveFixedSourceFormationsHaveNativeOrderAndZeroDraws(
        ActIdentity act, PrototypeRoomType room, string id,
        string nativeName, string[] monsters, string?[] slots)
    {
        Assert.True(NativeActOneEliteBossCombatEntryV111.IsSupported(id));
        var state = Initial("elite-boss-source-entry-" + id, act);
        var before = CanonicalJson.Sha256(state);
        var formation = PrepareDirect(state, id, room);
        Assert.Equal(before, CanonicalJson.Sha256(state));
        Assert.Equal(nativeName, formation.NativeEncounterEntry);
        Assert.Equal(room == PrototypeRoomType.Boss ? 16 : 5,
            formation.TotalFloor);
        Assert.Equal(monsters, formation.Enemies.Select(x => x.EnemyId));
        Assert.Equal(slots, formation.Enemies.Select(x => x.SlotName));
        Assert.Equal(Enumerable.Range(0, monsters.Length),
            formation.Enemies.Select(x => x.FormationPosition));
        Assert.Equal(0, NativeRngV111.ToSnapshot(
            formation.EncounterRng).Counter);
        Assert.Null(formation.EncounterOpeningOffset);
        Assert.Equal(monsters.Length, formation.MonsterContexts.Length);
        for (var i = 0; i < monsters.Length; i++)
        {
            var local = formation.MonsterContexts[i];
            Assert.Equal(i + 1, local.CombatId);
            Assert.Equal(formation.Enemies[i], local.Enemy);
            Assert.Equal(0, NativeRngV111.ToSnapshot(local.Rng).Counter);
        }
        Assert.Equal(CanonicalJson.Sha256(formation),
            CanonicalJson.Sha256(PrepareDirect(state, id, room)));
        Assert.Equal(CanonicalJson.Sha256(formation),
            CanonicalJson.Sha256(formation.Fork()));
    }

    private static RunState SetupLive(
        RunState state, string id, PrototypeRoomType room)
    {
        var world = state.World!;
        var nodes = world.Map.Nodes.ToDictionary(x => x.NodeId,
            StringComparer.Ordinal);
        var edge = world.Map.Nodes
            .SelectMany(parent => (parent.NextNodeIds ?? [])
                .Select(nextId => (Parent: parent, Child: nodes[nextId])))
            .First(x => x.Child.RoomType == room
                && (room != PrototypeRoomType.Elite
                    || x.Child.Floor is >= 3 and < 16));
        var pool = world.ActOneEncounterPool!;
        return state with
        {
            Phase = RunPhase.MapChoice,
            World = world with
            {
                Floor = edge.Parent.Floor,
                ActiveRoom = null,
                ActOneEncounterPool = pool with
                {
                    BossEncounterId = room == PrototypeRoomType.Boss
                        ? id : pool.BossEncounterId,
                    RemainingEliteEncounterIds = room == PrototypeRoomType.Elite
                        ? [id] : pool.RemainingEliteEncounterIds
                },
                Map = world.Map with
                {
                    CurrentNodeId = edge.Parent.NodeId
                }
            }
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ActualMapEntryPreservesHpIntentAndNativeSnapshot(
        ActIdentity act, PrototypeRoomType room, string id,
        string nativeName, string[] expected, string?[] slots)
    {
        var seed = "elite-boss-live-entry-" + id;
        var state = SetupLive(Initial(seed, act), id, room);
        var initial = CanonicalJson.Sha256(state);
        var action = Engine.GetLegalActions(state)
            .Where(x => x.Kind == "choose_map_node")
            .First(x => state.World!.Map.Nodes.Any(node =>
                node.RoomType == room
                && node.NodeId == x.ReadPayload<ChooseMapNodePayload>()
                    .NodeId));
        var entered = Engine.Step(state, action).State;
        Assert.Equal(initial, CanonicalJson.Sha256(state));
        Assert.Equal(RunPhase.Combat, entered.Phase);
        Assert.Equal(id, entered.World!.EncounterIds.Last());
        var combat = entered.World.Combat!;
        Assert.NotNull(combat.NativeRng);
        Assert.Equal(nativeName,
            combat.NativeRng!.Formation.NativeEncounterEntry);
        Assert.Equal(expected, combat.Enemies.Select(x => x.EnemyId));
        Assert.Equal(slots, combat.Enemies.Select(x => x.SlotName));
        Assert.All(combat.Enemies, enemy =>
        {
            var range = PrototypeContent.Enemy(enemy.EnemyId)
                .HpRangeAt(1, entered.Ascension);
            Assert.InRange(enemy.MaxHp, range.Min, range.Max);
            Assert.Equal(enemy.MaxHp, enemy.Hp);
            Assert.NotNull(enemy.PlannedMoveIndex);
        });
        Assert.Equal(expected.Length, NativeRngV111.ToSnapshot(
            entered.NativeRunRng!.Streams.Single(x =>
                x.StreamId == "run.niche")).Counter);
        Assert.Equal(0, NativeRngV111.ToSnapshot(
            combat.NativeRng.Formation.EncounterRng).Counter);
        NativeRunRngV111.Validate(entered);
        PrototypeStateInvariants.Validate(entered);
        var restored = RunSnapshot.Load(RunSnapshot.Save(entered));
        Assert.Equal(CanonicalJson.Sha256(entered),
            CanonicalJson.Sha256(restored));
        Assert.Equal(CanonicalJson.Sha256(entered),
            CanonicalJson.Sha256(entered.Fork()));

        if (nativeName == "TheKinBoss")
        {
            Assert.Equal(new[] { 1, 2, 3 },
                combat.Enemies.Select(x => x.InstanceId));
            Assert.Equal(2, combat.Enemies[0].PlannedMoveIndex);
            Assert.Equal(0, combat.Enemies[1].PlannedMoveIndex);
            Assert.Equal(3, combat.Enemies[0].LeaderEnemyInstanceId);
            Assert.Equal(3, combat.Enemies[1].LeaderEnemyInstanceId);
            Assert.Null(combat.Enemies[2].LeaderEnemyInstanceId);
        }
    }

    [Fact]
    public void NativeEliteBossEntryRejectsWrongRegionRoomOrFloor()
    {
        var state = Initial("elite-boss-strict-region",
            ActIdentity.Overgrowth);
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.the_kin_boss");
        var bossNode = state.World!.Map.Nodes.First(x => x.Floor == 16);
        var valid = state.World with
        {
            Floor = 16,
            ActiveRoom = PrototypeRoomType.Boss,
            Map = state.World.Map with
            {
                CurrentNodeId = bossNode.NodeId
            }
        };
        Assert.NotEmpty(NativeActOneEliteBossCombatEntryV111.Prepare(
            state, valid, encounter).Enemies);
        Assert.Throws<NotSupportedException>(() =>
            NativeActOneEliteBossCombatEntryV111.Prepare(
                state, valid with { ActiveRoom = PrototypeRoomType.Elite },
                encounter));
        Assert.Throws<NotSupportedException>(() =>
            NativeActOneEliteBossCombatEntryV111.Prepare(
                state, valid with
                {
                    ActOneRegion = PrototypeActOneRegion.Underdocks
                }, encounter));
        Assert.Throws<NotSupportedException>(() =>
            NativeActOneEliteBossCombatEntryV111.Prepare(
                state, valid with { Floor = 15 }, encounter));
        Assert.False(NativeActOneEliteBossCombatEntryV111.IsSupported(
            "proto.encounter.cultists_normal"));
    }
}
