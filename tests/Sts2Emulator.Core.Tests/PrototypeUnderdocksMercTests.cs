using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksMercTests
{
    [Fact]
    public void PinnedMercStatsPowersAndDeathSummonsAreDeclared()
    {
        var merc = PrototypeContent.Enemy("proto.enemy.gremlin_merc");
        Assert.Equal((47, 49), merc.HpRangeAt(1, 0));
        Assert.Equal((51, 53), merc.HpRangeAt(1, 8));
        Assert.Equal(new[] { "gimme", "double_smash", "hehe" },
            merc.Moves.Select(move => move.Id));
        Assert.Equal(2, merc.Moves[0].Effects[0].Repetitions);
        Assert.Equal(7, merc.Moves[0].Effects[0].AmountAt(1, 0));
        Assert.Equal(8, merc.Moves[0].Effects[0].AmountAt(1, 8));
        Assert.Equal(6, merc.Moves[1].Effects[0].AmountAt(1, 0));
        Assert.Equal(7, merc.Moves[1].Effects[0].AmountAt(1, 8));
        Assert.Equal(new[] { "proto.power.surprise", "proto.power.thievery" },
            merc.StartingPowers!.Select(power => power.PowerId));
        Assert.Equal(20, merc.StartingPowers![1].StacksAt(0));
        Assert.Equal(new[] { "sneaky", "fat" },
            merc.DeathSummons!.Select(summon => summon.SlotName));
        Assert.True(merc.DeathSummons![1].TransferStolenGold);
        Assert.Equal(0, merc.DeathSummons![1].SkipEnemyActions);

        var fat = PrototypeContent.Enemy("proto.enemy.fat_gremlin");
        Assert.Equal((13, 17), fat.HpRangeAt(1, 0));
        Assert.Equal((14, 18), fat.HpRangeAt(1, 8));
        Assert.True(fat.RecoverCarriedGoldOnDeath);
        Assert.Equal(0f, fat.EscapedRewardProportionWithGold);
        Assert.Equal(0.5f, fat.EscapedRewardProportionWithoutGold);
        var sneaky = PrototypeContent.Enemy("proto.enemy.sneaky_gremlin");
        Assert.Equal((10, 14), sneaky.HpRangeAt(1, 0));
        Assert.Equal((11, 15), sneaky.HpRangeAt(1, 8));
    }

    [Fact]
    public void MercStealsGoldOncePerMultiAttackNotPerHit()
    {
        var engine = new PrototypeGameEngine();
        var state = FindMerc(engine);
        Assert.Equal(99, state.Player.Gold);
        Assert.Equal("merc", Assert.Single(state.World!.Combat!.Enemies).SlotName);

        state = EndTurn(engine, state);
        Assert.Equal(79, state.Player.Gold);
        Assert.Equal(56, state.Player.Hp);
        var merc = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("gimme", merc.LastMoveId);
        Assert.Equal(20, merc.StolenGold);

        state = EndTurn(engine, state);
        Assert.Equal(59, state.Player.Gold);
        Assert.Equal(44, state.Player.Hp);
        merc = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("double_smash", merc.LastMoveId);
        Assert.Equal(40, merc.StolenGold);
        Assert.Equal(2, state.World.Combat.PlayerPowers.Single(power =>
            power.PowerId == "proto.power.weak").Stacks);

        state = EndTurn(engine, state);
        Assert.Equal(39, state.Player.Gold);
        Assert.Equal(36, state.Player.Hp);
        merc = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("hehe", merc.LastMoveId);
        Assert.Equal(60, merc.StolenGold);
        Assert.Equal(2, merc.PowerStates.Single(power =>
            power.PowerId == "proto.power.strength").Stacks);
    }

    [Fact]
    public void MercDeathSummonsCarriersAndFatEscapeForfeitsStolenGold()
    {
        var engine = new PrototypeGameEngine();
        var state = FindMerc(engine);
        state = EndTurn(engine, state);
        Assert.Equal(79, state.Player.Gold);
        state = KillEnemyWithAttack(engine, state,
            "proto.enemy.gremlin_merc");
        Assert.Equal(RunPhase.Combat, state.Phase);
        var combat = state.World!.Combat!;
        Assert.Equal(3, combat.Enemies.Length);
        var fat = Assert.Single(combat.Enemies,
            enemy => enemy.EnemyId == "proto.enemy.fat_gremlin");
        var sneaky = Assert.Single(combat.Enemies,
            enemy => enemy.EnemyId == "proto.enemy.sneaky_gremlin");
        Assert.Equal(20, fat.StolenGold);
        Assert.Equal(0, sneaky.StolenGold);
        Assert.Equal(20, fat.PowerStates.Single(power =>
            power.PowerId == "proto.power.heist").Stacks);
        Assert.Equal("fat", fat.SlotName);
        Assert.Equal("sneaky", sneaky.SlotName);
        Assert.Equal(0, fat.EnemyActionSkipsRemaining);

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal("spawned", combat.Enemies.Single(enemy =>
            enemy.EnemyId == "proto.enemy.fat_gremlin").LastMoveId);
        state = EndTurn(engine, state);
        fat = state.World!.Combat!.Enemies.Single(enemy =>
            enemy.EnemyId == "proto.enemy.fat_gremlin");
        Assert.True(fat.Escaped);
        Assert.Equal(0, fat.Hp);
        Assert.Equal(20, fat.StolenGold);
        Assert.Equal(79, state.Player.Gold);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void FatDeathOffersStolenGoldRecoveryAsAReward()
    {
        var engine = new PrototypeGameEngine();
        var state = EndTurn(engine, FindMerc(engine));
        state = KillEnemyWithAttack(engine, state,
            "proto.enemy.gremlin_merc");
        // The smaller gremlin is already defeated. Killing the carrier
        // is the final blow, so recovered coins become an optional reward.
        var combat = state.World!.Combat!;
        var sneakyId = combat.Enemies.Single(enemy =>
            enemy.EnemyId == "proto.enemy.sneaky_gremlin").InstanceId;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Enemies = combat.Enemies.Select(enemy =>
                        enemy.InstanceId == sneakyId
                            ? enemy with { Hp = 0 }
                            : enemy).ToArray()
                }
            }
        };
        state = KillEnemyWithAttack(engine, state,
            "proto.enemy.fat_gremlin");
        Assert.Equal(RunPhase.Reward, state.Phase);
        var ordinaryGold = state.World!.Reward!.GoldOption!.Value;
        Assert.InRange(ordinaryGold, 10, 20);
        Assert.Equal(20, Assert.Single(state.World.Reward.ExtraGoldOptions!));
        Assert.False(state.World.Reward.GoldResolved);
        Assert.Equal(79, state.Player.Gold);
        var takeGold = engine.GetLegalActions(state)
            .Single(action => action.Kind == "take_reward_extra_gold");
        state = engine.Step(state, takeGold).State;
        Assert.Equal(99, state.Player.Gold);
        Assert.True(Assert.Single(state.World!.Reward!.ExtraGoldGroupsResolved!));
        Assert.False(state.World.Reward.GoldResolved);
        state = engine.Step(state, GameAction.Empty("take_reward_gold")).State;
        Assert.Equal(99 + ordinaryGold, state.Player.Gold);
        Assert.True(state.World!.Reward!.GoldResolved);
    }

    private static RunState KillEnemyWithAttack(
        PrototypeGameEngine engine, RunState state, string enemyId)
    {
        var combat = state.World!.Combat!;
        var enemy = combat.Enemies.Single(item =>
            item.EnemyId == enemyId && item.Hp > 0);
        var strike = combat.Cards.First(card =>
            PrototypeContent.Card(card.CardId).Type == PrototypeCardType.Attack
            && !PrototypeContent.Card(card.CardId).Unplayable);
        // Use a genuine legal attack action against a 1-HP target;
        // synthetic map traversal is already isolated in FindMerc().
        combat = combat with
        {
            Energy = 10,
            Hand = [strike.InstanceId],
            DrawPile = combat.DrawPile
                .Where(id => id != strike.InstanceId).ToArray(),
            DiscardPile = combat.DiscardPile
                .Where(id => id != strike.InstanceId).ToArray(),
            Enemies = combat.Enemies.Select(item =>
                item.InstanceId == enemy.InstanceId
                    ? item with { Hp = 1, Block = 0 }
                    : item).ToArray()
        };
        state = state with { World = state.World with { Combat = combat } };
        var action = engine.GetLegalActions(state)
            .First(action => action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId
                    == enemy.InstanceId);
        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(PrototypeGameEngine engine, RunState state)
    {
        return engine.Step(state,
            engine.GetLegalActions(state).Single(action =>
                action.Kind == "end_turn")).State;
    }

    private static RunState FindMerc(PrototypeGameEngine engine)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "merc-normal-regression");
        for (var floor = 1; floor <= 13; floor++)
        {
            var node = new MapNodeState(
                "merc-test:" + floor, 1, floor,
                PrototypeRoomType.Combat, []);
            state = state with
            {
                Phase = RunPhase.MapChoice,
                World = state.World! with
                {
                    Floor = floor - 1,
                    ActiveRoom = null,
                    Map = new MapState(
                        [node], CurrentNodeId: null,
                        EntryNodeIds: [node.NodeId],
                        GenerationProfileId:
                            PrototypeNativeUnderdocks.GenerationProfileId),
                    Combat = null,
                    Reward = null,
                    Shop = null,
                    Event = null
                }
            };
            state = engine.Step(state,
                Assert.Single(engine.GetLegalActions(state))).State;
            if (state.World!.EncounterIds[^1]
                == "proto.encounter.gremlin_merc_normal")
            {
                return state;
            }
        }
        throw new InvalidOperationException(
            "Merc not drawn within native normal bag.");
    }
}
