using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeTheHuntTests
{
    [Fact]
    public void NonFatalHuntDealsDamageWithoutRewardOrPower()
    {
        var state = CreateState(
            Card(1, "proto.silent.the_hunt"),
            [
                Enemy(1, 20)
            ]);
        var engine = new PrototypeGameEngine();

        state = PlayHunt(engine, state, targetEnemyId: 1);

        var combat = state.World!.Combat!;
        Assert.Equal(10, combat.Enemies[0].Hp);
        Assert.Equal(0, combat.ExtraCardRewardsEarned);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.the_hunt");
        Assert.Contains(1L, combat.ExhaustPile);
    }

    [Fact]
    public void FatalHuntAddsExtraRewardAndCounterPower()
    {
        var state = CreateState(
            Card(1, "proto.silent.the_hunt"),
            [
                Enemy(1, 10),
                Enemy(2, 20)
            ]);
        var engine = new PrototypeGameEngine();

        state = PlayHunt(engine, state, targetEnemyId: 1);

        var combat = state.World!.Combat!;
        Assert.Equal(0, combat.Enemies[0].Hp);
        Assert.Equal(1, combat.ExtraCardRewardsEarned);
        var huntPower = Assert.Single(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.the_hunt");
        Assert.Equal(1, huntPower.Stacks);
        Assert.Contains(1L, combat.ExhaustPile);
    }

    [Fact]
    public void UpgradedHuntDealsFifteenAndCanTriggerFatal()
    {
        var state = CreateState(
            Card(
                1,
                "proto.silent.the_hunt",
                upgrade: 1),
            [
                Enemy(1, 15),
                Enemy(2, 20)
            ]);
        var engine = new PrototypeGameEngine();

        state = PlayHunt(engine, state, targetEnemyId: 1);

        var combat = state.World!.Combat!;
        Assert.Equal(0, combat.Enemies[0].Hp);
        Assert.Equal(1, combat.ExtraCardRewardsEarned);
    }

    [Fact]
    public void MinionDeathDoesNotTriggerFatal()
    {
        var minion = Enemy(
            1,
            10,
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.minion",
                    1,
                    1)
            ]);
        var state = CreateState(
            Card(1, "proto.silent.the_hunt"),
            [
                minion,
                Enemy(2, 20)
            ]);
        var engine = new PrototypeGameEngine();

        state = PlayHunt(engine, state, targetEnemyId: 1);

        var combat = state.World!.Combat!;
        Assert.Equal(0, combat.Enemies[0].Hp);
        Assert.Equal(0, combat.ExtraCardRewardsEarned);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.the_hunt");
    }

    [Fact]
    public void FatalRewardIsSeparateThreeCardChoice()
    {
        var state = CreateState(
            Card(1, "proto.silent.the_hunt"),
            [
                Enemy(1, 10)
            ]);
        var engine = new PrototypeGameEngine();

        state = PlayHunt(engine, state, targetEnemyId: 1);

        Assert.Equal(RunPhase.Reward, state.Phase);
        var reward = state.World!.Reward!;
        Assert.Equal(3, reward.CardOptions.Length);
        var extra = Assert.Single(reward.ExtraCardOptions!);
        Assert.Equal(3, extra.Length);
        Assert.Equal(0, reward.ExtraCardRewardsResolved);

        var standardActions = engine.GetLegalActions(state);
        Assert.Equal(
            3,
            standardActions.Count(
                action => action.Kind == "take_reward_card"));
        state = engine.Step(
            state,
            standardActions.Single(
                action => action.Kind == "skip_reward_card")).State;

        var extraActions = engine.GetLegalActions(state);
        Assert.Equal(
            3,
            extraActions.Count(
                action => action.Kind == "take_reward_card"));
        Assert.Contains(
            extraActions,
            action => action.Kind == "skip_reward_card");

        var deckCount = state.Player.Deck.Length;
        state = engine.Step(
            state,
            extraActions.First(
                action => action.Kind == "take_reward_card")).State;

        Assert.Equal(deckCount + 1, state.Player.Deck.Length);
        Assert.Equal(
            1,
            state.World!.Reward!.ExtraCardRewardsResolved);
    }

    [Fact]
    public void HuntCannotBeGeneratedInCombat()
    {
        Assert.False(
            PrototypeContent.Card("proto.silent.the_hunt")
                .CanBeGeneratedInCombat);
    }

    private static RunState PlayHunt(
        PrototypeGameEngine engine,
        RunState state,
        int targetEnemyId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1
                && item.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == targetEnemyId);
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long id,
        string cardId,
        int upgrade = 0) =>
        new(
            id,
            1000 + id,
            cardId,
            upgrade,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(
        int id,
        int hp,
        PrototypePowerInstanceState[]? powers = null) =>
        new(
            id,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal),
            Powers: powers);

    private static RunState CreateState(
        CombatCardInstance hunt,
        EnemyCombatState[] enemies)
    {
        var persistent = new CardInstance(
            hunt.PersistentCardInstanceId!.Value,
            hunt.CardId,
            hunt.UpgradeLevel,
            PrototypeJson.EmptyObject());
        var player = new PlayerState(
            70,
            70,
            0,
            [persistent],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 1,
            PlayerBlock: 0,
            Hand: [hunt.InstanceId],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId: hunt.InstanceId + 1,
            Cards: [hunt],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "the-hunt-test",
            "the-hunt-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("the-hunt-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
