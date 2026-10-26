using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryBattlewornTests
{
    private static readonly PrototypeGameEngine Engine = new();
    private const string EventId = PrototypeNativeGloryEvents.BattlewornDummyId;

    [Fact]
    public void ThreeSettingsHaveSourceHpAndThreeTurnTimer()
    {
        var start = Start("battleworn-options");
        Assert.Equal((3, 3, 0),
            (PrototypeContent.Event(EventId).MinAct,
             PrototypeContent.Event(EventId).MaxAct,
             PrototypeContent.Event(EventId).Weight));
        Assert.Equal(new[] { "setting_1", "setting_2", "setting_3" },
            Choices(start));
        foreach (var (setting, hp, encounter) in new[]
        {
            ("setting_1", 75, PrototypeNativeGloryEvents.BattlewornV1EncounterId),
            ("setting_2", 150, PrototypeNativeGloryEvents.BattlewornV2EncounterId),
            ("setting_3", 300, PrototypeNativeGloryEvents.BattlewornV3EncounterId)
        })
        {
            var result = Choose(start, setting);
            Assert.Equal(RunPhase.Combat, result.Phase);
            Assert.Equal(encounter, result.World!.EncounterIds[^1]);
            var enemy = Assert.Single(result.World.Combat!.Enemies);
            Assert.Equal(hp, enemy.Hp);
            Assert.Equal(hp, enemy.MaxHp);
            Assert.Equal("nothing",
                PrototypeContent.Enemy(enemy.EnemyId).Moves[0].Id);
            Assert.Single(enemy.PowerStates, p =>
                p.PowerId == PrototypeNativeGloryEvents.BattlewornTimeLimitId
                && p.Stacks == 3);
            Assert.Equal(CanonicalJson.Sha256(result),
                CanonicalJson.Sha256(Choose(start.Fork(), setting)));
            PrototypeStateInvariants.Validate(result);
        }
    }

    [Theory]
    [InlineData("setting_1")]
    [InlineData("setting_2")]
    [InlineData("setting_3")]
    public void DummyTimesOutAfterThreeEnemyTurnsWithoutReward(string setting)
    {
        var start = Choose(Start($"timeout-{setting}"), setting);
        var first = Step(start, GameAction.Empty("end_turn"));
        Assert.Equal(RunPhase.Combat, first.Phase);
        Assert.Single(first.World!.Combat!.Enemies[0].PowerStates,
            p => p.PowerId == PrototypeNativeGloryEvents.BattlewornTimeLimitId
            && p.Stacks == 2);
        var second = Step(first, GameAction.Empty("end_turn"));
        Assert.Equal(RunPhase.Combat, second.Phase);
        Assert.Single(second.World!.Combat!.Enemies[0].PowerStates,
            p => p.PowerId == PrototypeNativeGloryEvents.BattlewornTimeLimitId
            && p.Stacks == 1);
        var completed = Step(second, GameAction.Empty("end_turn"));
        completed = PrototypeEventPageTestHelperV111.ContinueIfFinished(completed);
        Assert.Equal(RunPhase.MapChoice, completed.Phase);
        Assert.Null(completed.World!.Reward);
        Assert.Null(completed.World.Event);
        Assert.Single(completed.World.CompletedRooms);
        Assert.Equal(start.Player.Relics.Length, completed.Player.Relics.Length);
        Assert.Equal(CanonicalJson.Sha256(completed),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Step(second.Fork(), GameAction.Empty("end_turn")))));
        PrototypeStateInvariants.Validate(completed);
    }

    [Theory]
    [InlineData("setting_1")]
    [InlineData("setting_2")]
    [InlineData("setting_3")]
    public void VictoryUsesSettingSpecificRewardWithoutOrdinaryLoot(string setting)
    {
        var start = Choose(Start($"win-{setting}"), setting);
        var combat = start.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        // A harmless synthetic 1 HP target lets this regression focus
        // on the post-combat source event continuation.
        var vulnerable = combat with
        {
            Enemies = [enemy with { Hp = 1, Block = 0 }]
        };
        var id = vulnerable.NextCardInstanceId;
        var strike = new CombatCardInstance(id, null,
            "proto.silent.strike", 0, true, PrototypeJson.EmptyObject());
        start = start with
        {
            World = start.World with
            {
                Combat = vulnerable with
                {
                    Cards = vulnerable.Cards.Append(strike).ToArray(),
                    Hand = vulnerable.Hand.Append(id).ToArray(),
                    NextCardInstanceId = id + 1,
                    Energy = 3
                }
            }
        };
        var action = GameAction.Create("play_card",
            new PlayCardPayload(id, enemy.InstanceId));
        Assert.Contains(Engine.GetLegalActions(start), a =>
            a.Kind == "play_card"
            && a.ReadPayload<PlayCardPayload>().CardInstanceId == id);
        var won = Step(start, action);
        if (setting == "setting_2")
        {
            won = PrototypeEventPageTestHelperV111.ContinueIfFinished(won);
            Assert.Equal(RunPhase.MapChoice, won.Phase);
            Assert.Null(won.World!.Reward);
            Assert.Equal(start.Player.Deck.Length, won.Player.Deck.Length);
            Assert.True(won.Player.Deck.Count(c => c.UpgradeLevel > 0)
                >= start.Player.Deck.Count(c => c.UpgradeLevel > 0));
        }
        else
        {
            Assert.Equal(RunPhase.Reward, won.Phase);
            var reward = Assert.IsType<RewardState>(won.World!.Reward);
            Assert.StartsWith("Event:", reward.SourceRoom);
            Assert.Empty(reward.CardOptions);
            if (setting == "setting_1")
            {
                Assert.NotNull(reward.PotionOption);
                Assert.Null(reward.RelicOption);
                Assert.Contains(Engine.GetLegalActions(won), a =>
                    a.Kind == "leave_reward");
            }
            else
            {
                Assert.Null(reward.PotionOption);
                Assert.NotNull(reward.RelicOption);
                Assert.Contains(Engine.GetLegalActions(won), a =>
                    a.Kind == "take_reward_relic");
            }
            won = Step(won, GameAction.Empty("leave_reward"));
            Assert.Equal(RunPhase.MapChoice, won.Phase);
        }
        PrototypeStateInvariants.Validate(won);
    }

    [Fact]
    public void InvalidSettingLeavesEventUnchanged()
    {
        var start = Start("battleworn-invalid");
        var hash = CanonicalJson.Sha256(start);
        Assert.Throws<InvalidOperationException>(() =>
            Choose(start, "setting_4"));
        Assert.Equal(hash, CanonicalJson.Sha256(start));
    }

    private static RunState Start(string seed) =>
        PrototypeNativeLaterActFixture.CreateGloryEvent(seed, EventId);
    private static string[] Choices(RunState state) =>
        Engine.GetLegalActions(state)
            .Where(a => a.Kind == "event_choice")
            .Select(a => a.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();
    private static RunState Choose(RunState state, string choice) =>
        Step(state, GameAction.Create("event_choice",
            new EventChoicePayload(choice)));
    private static RunState Step(RunState state, GameAction action) =>
        Engine.Step(state, action).State;
}
