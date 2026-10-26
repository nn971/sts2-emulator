using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveLanternKeyTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void PinnedCatalogAndQuestCardHaveExplicitGatedDefinitions()
    {
        var evt = PrototypeContent.Event(
            PrototypeNativeHiveEvents.LanternKeyEventId);
        Assert.Equal((2, 2, 0), (evt.MinAct, evt.MaxAct, evt.Weight));
        Assert.Equal(new[] { "return_key", "keep_key" },
            evt.Choices.Select(choice => choice.Id));
        Assert.True(PrototypeNativeHiveEvents.IsNativeEvent(evt.Id));
        var quest = PrototypeContent.Card(
            PrototypeNativeHiveEvents.LanternKeyCardId);
        Assert.Equal(PrototypeCardRarity.Quest, quest.Rarity);
        Assert.Equal(PrototypeCardType.Quest, quest.Type);
        Assert.True(quest.Unplayable);
        Assert.False(quest.RewardEligible);
        Assert.Equal(0, quest.MaxUpgradeLevel);
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeHiveEvents.MysteriousKnightEncounterId);
        Assert.Equal(0, encounter.Weight);
        Assert.Equal(PrototypeRoomType.Combat, encounter.RoomType);
        Assert.Equal(new[] { PrototypeNativeHiveEvents.MysteriousKnightId },
            encounter.EnemyIds);
    }

    [Fact]
    public void ReturningKeyGainsExactlyOneHundredGoldAndClosesRoom()
    {
        var state = Fixture();
        var initial = CanonicalJson.Sha256(state);
        var result = Select(state, "return_key");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Null(result.World!.Event);
        Assert.Equal(state.Player.Gold + 100, result.Player.Gold);
        Assert.Equal(state.Player.Deck.Length, result.Player.Deck.Length);
        Assert.Equal(initial, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Select(state.Fork(), "return_key"))));
    }

    [Fact]
    public void KeepKeyMakesCombatMandatoryAndRetainsTheEvent()
    {
        var state = Fixture();
        var keep = Select(state, "keep_key");
        Assert.Equal(RunPhase.Event, keep.Phase);
        Assert.Equal(1, keep.World!.Event!.NativePageIndex);
        Assert.Equal(new[] { "fight" }, EventChoices(keep));
        Assert.Throws<InvalidOperationException>(() =>
            Select(keep, "return_key"));
        var fight = Select(keep, "fight");
        Assert.Equal(RunPhase.Combat, fight.Phase);
        Assert.Equal(2, fight.World!.Event!.NativePageIndex);
        Assert.Equal(PrototypeNativeHiveEvents.MysteriousKnightEncounterId,
            fight.World.EncounterIds.Last());
        var knight = Assert.Single(fight.World.Combat!.Enemies);
        Assert.Equal(PrototypeNativeHiveEvents.MysteriousKnightId,
            knight.EnemyId);
        Assert.Equal(6, knight.Block);
        Assert.Contains(knight.PowerStates, p =>
            p.PowerId == "proto.power.strength" && p.Stacks == 6);
        Assert.Contains(knight.PowerStates, p =>
            p.PowerId == PrototypeNativeHiveEvents.PlatingPowerId
            && p.Stacks == 6);
        Assert.Equal(CanonicalJson.Sha256(fight),
            CanonicalJson.Sha256(Select(keep.Fork(), "fight")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WinningFightOffersOptionalQuestCardThenCompletesEvent(bool claim)
    {
        var fight = Select(Select(Fixture(), "keep_key"), "fight");
        var combat = fight.World!.Combat!;
        var strike = combat.Cards.First(c =>
            c.CardId == "proto.silent.strike");
        var weakened = combat with
        {
            Hand = [strike.InstanceId],
            DrawPile = combat.Cards
                .Where(c => c.InstanceId != strike.InstanceId)
                .Select(c => c.InstanceId).ToArray(),
            DiscardPile = [],
            ExhaustPile = [],
            Enemies = combat.Enemies.Select(e => e with
            {
                Hp = 1,
                Block = 0
            }).ToArray()
        };
        fight = fight with
        {
            World = fight.World with { Combat = weakened }
        };
        var previous = CanonicalJson.Sha256(fight);
        var play = Engine.GetLegalActions(fight).Single(a =>
            a.Kind == "play_card"
            && a.ReadPayload<PlayCardPayload>().CardInstanceId
                == strike.InstanceId);
        var won = Engine.Step(fight, play).State;
        Assert.Equal(RunPhase.Reward, won.Phase);
        Assert.Null(won.World!.Combat);
        Assert.Equal(PrototypeNativeHiveEvents.LanternKeyEventId,
            won.World.Event!.EventId);
        Assert.Equal("Event:" + PrototypeNativeHiveEvents.LanternKeyEventId,
            won.World.Reward!.SourceRoom);
        var options = Engine.GetLegalActions(won);
        Assert.Contains(options, a => a.Kind == "take_reward_card");
        Assert.Contains(options, a => a.Kind == "skip_reward_card");

        var choice = options.Single(a => a.Kind ==
            (claim ? "take_reward_card" : "skip_reward_card"));
        var reward = Engine.Step(won, choice).State;
        Assert.Equal(RunPhase.Reward, reward.Phase);
        var finished = Engine.Step(reward,
            Engine.GetLegalActions(reward).Single(a =>
                a.Kind == "leave_reward")).State;
        Assert.Equal(RunPhase.MapChoice, finished.Phase);
        Assert.Null(finished.World!.Event);
        Assert.Null(finished.World.Reward);
        Assert.Equal(claim ? 1 : 0, finished.Player.Deck.Count(c =>
            c.CardId == PrototypeNativeHiveEvents.LanternKeyCardId));
        Assert.Equal(previous, CanonicalJson.Sha256(fight));
        Assert.Equal(CanonicalJson.Sha256(finished),
            CanonicalJson.Sha256(finished.Fork()));
    }

    [Fact]
    public void PlatingDecaysAfterFirstEnemyTurnAndIsForkSafe()
    {
        var fight = Select(Select(Fixture(), "keep_key"), "fight");
        var after = Engine.Step(fight,
            Engine.GetLegalActions(fight).Single(a =>
                a.Kind == "end_turn")).State;
        // The first enemy-side start occurs in round one; native
        // Plating starts decaying at the second enemy-side start.
        Assert.Equal(6, Assert.Single(after.World!.Combat!.Enemies)
            .PowerStates.Single(p =>
                p.PowerId == PrototypeNativeHiveEvents.PlatingPowerId).Stacks);
        after = Engine.Step(after, Engine.GetLegalActions(after)
            .Single(a => a.Kind == "end_turn")).State;
        Assert.Equal(RunPhase.Combat, after.Phase);
        var knight = Assert.Single(after.World!.Combat!.Enemies);
        Assert.Contains(knight.PowerStates, p =>
            p.PowerId == PrototypeNativeHiveEvents.PlatingPowerId
            && p.Stacks == 5);
        Assert.Equal(5, knight.Block);
        Assert.Equal(CanonicalJson.Sha256(after),
            CanonicalJson.Sha256(after.Fork()));
    }

    private static RunState Fixture()
    {
        var start = Engine.Step(
            PrototypeGameFactory.Create("v111-lantern-key-fixture"),
            GameAction.Empty("start_run")).State;
        return start with
        {
            Phase = RunPhase.Event,
            World = start.World! with
            {
                Act = 2,
                Floor = 1,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(
                    PrototypeNativeHiveEvents.LanternKeyEventId),
                Map = new MapState(
                [
                    new("event", 2, 1, PrototypeRoomType.Event, ["next"]),
                    new("next", 2, 2, PrototypeRoomType.Combat, [])
                ], "event", ["event"])
            }
        };
    }

    private static RunState Select(RunState state, string choiceId)
    {
        var action = Engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId == choiceId);
        return Engine.Step(state, action).State;
    }

    private static string[] EventChoices(RunState state) =>
        Engine.GetLegalActions(state).Where(a => a.Kind == "event_choice")
            .Select(a => a.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();
}
