using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActCombatRoutingTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Theory]
    [InlineData(2, 4, 10)]
    [InlineData(3, 3, 9)]
    public void AllNativeRegionalEncountersAreRegisteredAndSeparated(
        int act, int weakCount, int normalCount)
    {
        var weak = PrototypeNativeLaterActCombatPool.Names(
            act, PrototypeRoomType.Combat, weak: true);
        var normal = PrototypeNativeLaterActCombatPool.Names(
            act, PrototypeRoomType.Combat);
        var elite = PrototypeNativeLaterActCombatPool.Names(
            act, PrototypeRoomType.Elite);
        var bosses = PrototypeNativeLaterActCombatPool.Names(
            act, PrototypeRoomType.Boss);
        Assert.Equal(weakCount, weak.Length);
        Assert.Equal(normalCount, normal.Length);
        Assert.Equal(3, elite.Length);
        Assert.Equal(3, bosses.Length);
        var all = weak.Concat(normal).Concat(elite).Concat(bosses).ToArray();
        Assert.Equal(weakCount + normalCount + 6, all.Length);
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());

        foreach (var id in all)
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal(act, encounter.MinAct);
            Assert.Equal(act, encounter.MaxAct);
            Assert.Equal(0, encounter.Weight);
            Assert.NotEmpty(encounter.ResolveEnemySpecs(
                PrototypeRng.CreateBundle($"source-{id}")));
            foreach (var enemy in encounter.EnemyIds)
                _ = PrototypeContent.Enemy(enemy);
        }

        Assert.All(weak.Concat(normal), id =>
            Assert.Equal(PrototypeRoomType.Combat,
                PrototypeContent.Encounter(id).RoomType));
        Assert.All(elite, id =>
            Assert.Equal(PrototypeRoomType.Elite,
                PrototypeContent.Encounter(id).RoomType));
        Assert.All(bosses, id =>
            Assert.Equal(PrototypeRoomType.Boss,
                PrototypeContent.Encounter(id).RoomType));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativeLaterActCombatPool.Names(1,
                PrototypeRoomType.Combat));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativeLaterActCombatPool.Names(3,
                PrototypeRoomType.Treasure));
    }

    [Fact]
    public void ConstructMenagerieMatchesPinnedThreeEnemyFormation()
    {
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeLaterActCombatPool.ConstructMenagerieId);
        Assert.Equal(new[]
        {
            "proto.enemy.punch_construct",
            "proto.enemy.cubex_construct",
            "proto.enemy.cubex_construct"
        }, encounter.ResolveEnemySpecs(
            PrototypeRng.CreateBundle("construct")).Select(x => x.EnemyId));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void TwoWeakFightsThenNonRepeatingNormalsAndIndependentElites(
        int act)
    {
        var rng = PrototypeRng.CreateBundle($"pool-sequence-{act}");
        var bag = PrototypeNativeLaterActCombatPool.Create(act, rng);
        Assert.Equal(0, bag.OrdinaryCombatsStarted);
        var boss = bag.BossEncounterId;

        for (var i = 0; i < 2; i++)
        {
            var selected = PrototypeNativeLaterActCombatPool.Pick(
                bag, PrototypeRoomType.Combat, 1 + i, rng);
            Assert.Contains(selected.Encounter.Id,
                PrototypeNativeLaterActCombatPool.Names(
                    act, PrototypeRoomType.Combat, weak: true));
            bag = selected.Pool;
            Assert.Equal(i + 1, bag.OrdinaryCombatsStarted);
        }

        var elite = PrototypeNativeLaterActCombatPool.Pick(
            bag, PrototypeRoomType.Elite, 7, rng);
        Assert.Contains(elite.Encounter.Id,
            PrototypeNativeLaterActCombatPool.Names(
                act, PrototypeRoomType.Elite));
        bag = elite.Pool;
        Assert.Equal(2, bag.OrdinaryCombatsStarted);
        Assert.DoesNotContain(elite.Encounter.Id,
            bag.RemainingEliteEncounterIds);

        var used = new HashSet<string>(StringComparer.Ordinal);
        var normalCount = PrototypeNativeLaterActCombatPool.Names(
            act, PrototypeRoomType.Combat).Length;
        for (var i = 0; i < normalCount; i++)
        {
            var result = PrototypeNativeLaterActCombatPool.Pick(
                bag, PrototypeRoomType.Combat, 8, rng);
            Assert.True(used.Add(result.Encounter.Id));
            bag = result.Pool;
        }
        Assert.Empty(bag.RemainingNormalEncounterIds);
        var refill = PrototypeNativeLaterActCombatPool.Pick(
            bag, PrototypeRoomType.Combat, 8, rng);
        Assert.Contains(refill.Encounter.Id, used);
        Assert.Equal(normalCount - 1,
            refill.Pool.RemainingNormalEncounterIds.Length);
        Assert.Equal(boss, bag.BossEncounterId);
        Assert.Equal(boss, PrototypeNativeLaterActCombatPool.Pick(
            bag, PrototypeRoomType.Boss, 15, rng).Encounter.Id);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NativeMapCombatEntryUpdatesOnlyActiveBagAndIsForkStable(int act)
    {
        var initial = PrototypeNativeLaterActFixture.Create(
            $"combat-map-{act}", act);
        var before = CanonicalJson.Sha256(initial);
        var first = initial.World!.Map.AvailableNodes()
            .First(n => n.RoomType == PrototypeRoomType.Combat);
        var action = GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(first.NodeId));
        var entered = Engine.Step(initial, action).State;
        Assert.Equal(RunPhase.Combat, entered.Phase);
        Assert.Single(entered.World!.EncounterIds);
        Assert.Equal(1, entered.World.LaterActEncounterPool!.OrdinaryCombatsStarted);
        Assert.Contains(entered.World.EncounterIds[^1],
            PrototypeNativeLaterActCombatPool.Names(
                act, PrototypeRoomType.Combat, weak: true));
        Assert.NotEmpty(entered.World.Combat!.Enemies);
        Assert.Equal(before, CanonicalJson.Sha256(initial));
        Assert.Equal(CanonicalJson.Sha256(entered),
            CanonicalJson.Sha256(Engine.Step(initial.Fork(), action).State));
        PrototypeStateInvariants.Validate(entered);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void FirstCombatVictoryProducesOptionalSourceShapedRewards(int act)
    {
        var initial = PrototypeNativeLaterActFixture.Create(
            $"combat-reward-{act}", act);
        var combatNode = initial.World!.Map.AvailableNodes()
            .First(n => n.RoomType == PrototypeRoomType.Combat);
        var entered = Engine.Step(initial,
            GameAction.Create("choose_map_node",
                new ChooseMapNodePayload(combatNode.NodeId))).State;
        var won = ForceWinningStrike(entered);
        Assert.Equal(RunPhase.Reward, won.Phase);
        var reward = Assert.IsType<RewardState>(won.World!.Reward);
        Assert.Equal("Combat", reward.SourceRoom);
        Assert.NotNull(reward.GoldOption);
        Assert.InRange(reward.GoldOption!.Value, 0, 20);
        Assert.True(reward.IndependentSelection);
        Assert.Equal(entered.Player.Gold, won.Player.Gold);
        Assert.NotEmpty(reward.CardOptions);
        Assert.Equal(CanonicalJson.Sha256(won),
            CanonicalJson.Sha256(ForceWinningStrike(entered.Fork())));
        var complete = Engine.Step(won,
            GameAction.Empty("leave_reward")).State;
        Assert.Equal(RunPhase.MapChoice, complete.Phase);
        Assert.Single(complete.World!.CompletedRooms);
        Assert.Null(complete.World.Combat);
        Assert.Null(complete.World.Reward);
        PrototypeStateInvariants.Validate(complete);
    }

    [Fact]
    public void NativeHiveToGloryActTransitionPreservesPersistentDeck()
    {
        var hive = PrototypeNativeLaterActFixture.Create("act2-to-3", 2);
        var original = hive.Player;
        var bossFloor = PrototypeNativeLaterActMap.HiveRoomRows + 1;
        var boss = hive.World!.Map.Nodes.Single(n => n.Floor == bossFloor);
        // Stage the transition at the last pre-boss row; native map
        // graph and preset boss choice remain unmodified.
        var predecessor = hive.World.Map.Nodes.First(n =>
            n.NextNodeIds?.Contains(boss.NodeId) == true);
        var staged = hive with
        {
            World = hive.World with
            {
                Floor = predecessor.Floor,
                Map = hive.World.Map with
                {
                    CurrentNodeId = predecessor.NodeId
                }
            }
        };
        var combat = Engine.Step(staged,
            GameAction.Create("choose_map_node",
                new ChooseMapNodePayload(boss.NodeId))).State;
        Assert.Equal(RunPhase.Combat, combat.Phase);
        Assert.Equal(hive.World.LaterActEncounterPool!.BossEncounterId,
            combat.World!.EncounterIds[^1]);
        var won = ForceWinningStrike(combat);
        Assert.Equal(RunPhase.Reward, won.Phase);
        var finished = Engine.Step(won,
            GameAction.Empty("leave_reward")).State;
        Assert.Equal(RunPhase.ActTransition, finished.Phase);
        var next = Engine.Step(finished,
            GameAction.Empty("continue_act")).State;
        Assert.Equal(RunPhase.MapChoice, next.Phase);
        Assert.Equal(3, next.World!.Act);
        Assert.Equal(0, next.World.Floor);
        Assert.Equal(PrototypeNativeLaterActMap.GloryProfileId,
            next.World.Map.GenerationProfileId);
        Assert.Equal(3, next.World.LaterActEncounterPool!.Act);
        Assert.Equal(0, next.World.LaterActEncounterPool.OrdinaryCombatsStarted);
        Assert.Equal(original.Deck.Select(c => c.InstanceId),
            next.Player.Deck.Take(original.Deck.Length)
                .Select(c => c.InstanceId));
        Assert.Contains(combat.World.EncounterIds[^1], next.World.EncounterIds);
        PrototypeStateInvariants.Validate(next);
    }

    [Fact]
    public void ForgedCrossActBagFailsValidation()
    {
        var initial = PrototypeNativeLaterActFixture.Create("wrong-bag", 3);
        var wrong = initial with
        {
            World = initial.World! with
            {
                LaterActEncounterPool = PrototypeNativeLaterActCombatPool
                    .Create(2, PrototypeRng.CreateBundle("wrong-act"))
            }
        };
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeStateInvariants.Validate(wrong));
    }

    private static RunState ForceWinningStrike(RunState state)
    {
        var combat = state.World!.Combat!;
        var victim = combat.Enemies[0];
        var id = combat.NextCardInstanceId;
        var strike = new CombatCardInstance(
            id, null, "proto.silent.strike", 0, true,
            PrototypeJson.EmptyObject());
        // Test combat resolution and rewards independently of all
        // native enemy HP/move mechanics. The real formations are
        // asserted before this synthetic decisive strike.
        var synthetic = combat with
        {
            Enemies = [victim with { Hp = 1, Block = 0 }],
            Cards = combat.Cards.Append(strike).ToArray(),
            Hand = combat.Hand.Append(id).ToArray(),
            NextCardInstanceId = id + 1,
            Energy = 3
        };
        var staged = state with
        {
            World = state.World with { Combat = synthetic }
        };
        return Engine.Step(staged, GameAction.Create(
            "play_card", new PlayCardPayload(id, victim.InstanceId))).State;
    }
}
