using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Pinned PhrogParasite.AfterAddedToRoom and
/// InfestedPower.AfterDeath / Wriggler.GenerateMoveStateMachine oracle.
/// Default prototype behavior has its separate original test suite.
/// </summary>
public sealed class NativePhrogParasiteDeathPhaseV111Tests
{
    private const string PhrogId = "proto.enemy.phrog_parasite";
    private const string WrigglerId = "proto.enemy.wriggler";
    private const string EncounterId = "proto.encounter.phrog_parasite_elite";
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState InNativePhrogCombat(string seed, int ascension)
    {
        var config = RunConfiguration.Create(
            firstAct: ActIdentity.Overgrowth, ascension: ascension) with
        {
            FidelityId = V111Build.NativeWeakCombatEntryFidelityId
        };
        var state = Engine.Step(
            V111RunFactory.Create(seed, config),
            GameAction.Empty("start_run")).State;
        for (var i = 0;
            i < 30 && state.Phase is RunPhase.Event or RunPhase.Reward; i++)
            state = Engine.Step(state, Engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        var world = state.World!;
        var nodes = world.Map.Nodes.ToDictionary(x => x.NodeId,
            StringComparer.Ordinal);
        var edge = world.Map.Nodes
            .SelectMany(parent => (parent.NextNodeIds ?? [])
                .Select(nextId => (Parent: parent, Child: nodes[nextId])))
            .First(x => x.Child.RoomType == PrototypeRoomType.Elite
                && x.Child.Floor is >= 3 and < 16);
        var enteredRoom = state with
        {
            World = world with
            {
                Floor = edge.Parent.Floor,
                ActiveRoom = null,
                ActOneEncounterPool = world.ActOneEncounterPool! with
                {
                    RemainingEliteEncounterIds = [EncounterId]
                },
                Map = world.Map with
                {
                    CurrentNodeId = edge.Parent.NodeId
                }
            }
        };
        var action = Engine.GetLegalActions(enteredRoom)
            .Where(x => x.Kind == "choose_map_node")
            .First(x => enteredRoom.World!.Map.Nodes.Any(node =>
                node.RoomType == PrototypeRoomType.Elite
                && node.NodeId == x.ReadPayload<ChooseMapNodePayload>()
                    .NodeId));
        var result = Engine.Step(enteredRoom, action).State;
        Assert.Equal(RunPhase.Combat, result.Phase);
        Assert.Equal(EncounterId, result.World!.EncounterIds.Last());
        NativeRunRngV111.Validate(result);
        return result;
    }

    private static NativeRngSnapshot Stream(CombatState combat, string id) =>
        NativeRngV111.ToSnapshot(
            combat.NativeRng!.RunStreams.Streams.Single(
                stream => stream.StreamId == id));

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(10)]
    public void PhrogDeathCreatesFourNativeStunnedWrigglersWithExactHpRng(
        int ascension)
    {
        for (var k = 0; k < 48; k++)
        {
            var state = InNativePhrogCombat(
                "native-phrog-death-" + ascension + "-" + k,
                ascension);
            var before = state.World!.Combat!;
            Assert.Equal("PhrogParasiteElite",
                before.NativeRng!.Formation.NativeEncounterEntry);
            Assert.Equal(4, Assert.Single(before.Enemies)
                .PowerStates.Single(power =>
                    power.PowerId == "proto.power.infested").Stacks);
            var snapshot = CanonicalJson.Sha256(state);
            var damaged = before with
            {
                Enemies = before.Enemies
                    .Select(enemy => enemy with { Hp = 0 }).ToArray()
            };
            var beforeNiche = Stream(damaged, "run.niche");
            var beforeAi = Stream(damaged, "run.monster_ai");
            var fork = damaged.NativeRng!.RunStreams.Fork();
            var spawned = PrototypeGameEngine.ResolveEnemyDeathSummons(
                damaged, state.Rng.Fork());
            Assert.Equal(snapshot, CanonicalJson.Sha256(state));
            Assert.Equal(5, spawned.Enemies.Length);
            Assert.Equal(new[] {1, 2, 3, 4, 5},
                spawned.Enemies.Select(enemy => enemy.InstanceId));
            Assert.Equal(new[] {"wriggler1", "wriggler2",
                "wriggler3", "wriggler4"},
                spawned.Enemies.Skip(1).Select(enemy => enemy.SlotName));
            Assert.All(spawned.Enemies.Skip(1), enemy =>
            {
                Assert.Equal(WrigglerId, enemy.EnemyId);
                Assert.Equal("spawned_move", enemy.AiStateId);
                Assert.Equal(2, enemy.PlannedMoveIndex);
                Assert.Equal("init", enemy.PlannedNextAiStateId);
                Assert.Equal(0, enemy.EnemyActionSkipsRemaining);
                Assert.Equal(enemy.Hp, enemy.MaxHp);
            });
            Assert.True(spawned.Enemies[0].DeathEffectsResolved);
            Assert.Equal(beforeNiche.Counter + 4,
                Stream(spawned, "run.niche").Counter);
            Assert.Equal(beforeAi, Stream(spawned, "run.monster_ai"));
            Assert.Equal(5, spawned.NativeRng!.Formation.Enemies.Length);
            Assert.Equal(5, spawned.NativeRng.Formation.MonsterContexts.Length);
            var occupied = new HashSet<int> { damaged.Enemies[0].MaxHp };
            var range = PrototypeContent.Enemy(WrigglerId)
                .HpRangeAt(1, ascension);
            foreach (var wriggler in spawned.Enemies.Skip(1))
            {
                var available = Enumerable.Range(range.Min,
                    range.Max - range.Min + 1)
                    .Where(hp => !occupied.Contains(hp)).ToArray();
                var full = Enumerable.Range(range.Min,
                    range.Max - range.Min + 1).ToArray();
                var options = available.Length == 0 ? full : available;
                var expectedHp = options[
                    NativeRngV111.NextInt(
                        fork, "run.niche", options.Length)];
                Assert.Equal(expectedHp, wriggler.MaxHp);
                occupied.Add(expectedHp);
            }
            Assert.Equal(NativeRngV111.ToSnapshot(
                fork.Streams.Single(x => x.StreamId == "run.niche")),
                Stream(spawned, "run.niche"));
            NativeCombatRngEntryV111.Validate(spawned);
            Assert.Equal(CanonicalJson.Sha256(spawned),
                CanonicalJson.Sha256(spawned.Fork()));
            var repeated = PrototypeGameEngine.ResolveEnemyDeathSummons(
                spawned, state.Rng.Fork());
            Assert.Equal(CanonicalJson.Sha256(spawned),
                CanonicalJson.Sha256(repeated));
            _ = PrototypeGameEngine.ResolveEnemyDeathSummons(
                damaged, state.Rng.Fork(), enemyActionSkips: 2);
        }
    }

    [Fact]
    public void NativeLivePhrogDeathTelegraphsSpawnedThenTransitionsBySlot()
    {
        var state = InNativePhrogCombat("native-phrog-live-spawned", 0);
        var combat = state.World!.Combat!;
        var cardId = combat.NextCardInstanceId;
        var strike = new CombatCardInstance(
            cardId, null, "proto.silent.strike", 0, true,
            PrototypeJson.EmptyObject());
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Enemies = combat.Enemies.Select(enemy =>
                        enemy with { Hp = 1, Block = 0 }).ToArray(),
                    Hand = combat.Hand.Append(cardId).ToArray(),
                    Cards = combat.Cards.Append(strike).ToArray(),
                    NextCardInstanceId = cardId + 1
                }
            }
        };
        NativeRunRngV111.Validate(state);
        var initialHash = CanonicalJson.Sha256(state);
        var action = Engine.GetLegalActions(state).Single(option =>
            option.Kind == "play_card"
            && option.ReadPayload<PlayCardPayload>().CardInstanceId == cardId
            && option.ReadPayload<PlayCardPayload>().TargetEnemyId == 1);
        var result = Engine.Step(state, action).State;
        var replay = Engine.Step(state.Fork(), action).State;
        Assert.Equal(initialHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(replay));
        Assert.Equal(RunPhase.Combat, result.Phase);
        var enemies = result.World!.Combat!.Enemies;
        Assert.Equal(5, enemies.Length);
        Assert.All(enemies.Skip(1), enemy =>
        {
            Assert.Equal(2, enemy.PlannedMoveIndex);
            Assert.Equal("init", enemy.PlannedNextAiStateId);
        });
        NativeRunRngV111.Validate(result);

        var end = Engine.GetLegalActions(result)
            .Single(option => option.Kind == "end_turn");
        result = Engine.Step(result, end).State;
        replay = Engine.Step(replay, end).State;
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(replay));
        Assert.All(result.World!.Combat!.Enemies.Skip(1), enemy =>
        {
            Assert.Equal("spawned", enemy.LastMoveId);
            Assert.Equal(0, enemy.EnemyActionSkipsRemaining);
        });
        NativeRunRngV111.Validate(result);

        end = Engine.GetLegalActions(result)
            .Single(option => option.Kind == "end_turn");
        result = Engine.Step(result, end).State;
        replay = Engine.Step(replay, end).State;
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(replay));
        Assert.Equal(2, result.World!.Combat!.Enemies
            .Count(enemy => enemy.LastMoveId == "nasty_bite"));
        Assert.Equal(2, result.World.Combat.Enemies
            .Count(enemy => enemy.LastMoveId == "wriggle"));
        NativeRunRngV111.Validate(result);
    }
}
