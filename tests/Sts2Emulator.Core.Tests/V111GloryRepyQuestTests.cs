using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryRepyQuestTests
{
    private static readonly PrototypeGameEngine Engine = new();
    private const string Key = PrototypeNativeHiveEvents.LanternKeyCardId;
    private const string Repy = PrototypeNativeHiveEvents.WarHistorianRepyId;

    [Fact]
    public void SourceGatedDefinitionsArePresentWithoutActThreeEventPooling()
    {
        var definition = PrototypeContent.Event(Repy);
        Assert.Equal((3, 3, 0),
            (definition.MinAct, definition.MaxAct, definition.Weight));
        Assert.Equal(new[] { "unlock_cage", "unlock_chest" },
            definition.Choices.Select(choice => choice.Id));
        Assert.Equal("History Course",
            PrototypeContent.Relic(
                PrototypeNativeHiveEvents.HistoryCourseId).Name);
    }

    [Theory]
    [InlineData(PrototypeRoomType.Unknown)]
    [InlineData(PrototypeRoomType.Event)]
    public void KeyForcesGloryEventAndDoesNotConsumeUnknownRoomRng(
        PrototypeRoomType room)
    {
        var initial = Fixture(1, room);
        var hash = CanonicalJson.Sha256(initial);
        var current = EnterRoom(initial);
        Assert.Equal(RunPhase.Event, current.Phase);
        Assert.Equal(Repy, current.World!.Event!.EventId);
        Assert.Equal(PrototypeRoomType.Event, current.World.ActiveRoom);
        Assert.Equal(Repy, current.World.EventIds.Last());
        Assert.Equal(new[] { "unlock_cage", "unlock_chest" },
            Choices(current));
        Assert.Equal(hash, CanonicalJson.Sha256(initial));
        Assert.Equal(CanonicalJson.Sha256(current),
            CanonicalJson.Sha256(EnterRoom(initial.Fork())));
        PrototypeStateInvariants.Validate(current);
    }

    [Fact]
    public void GloryUnknownWithoutKeyPreservesExplicitUnsupportedGate()
    {
        var noKey = Fixture(0, PrototypeRoomType.Unknown);
        Assert.Throws<InvalidOperationException>(() => EnterRoom(noKey));
    }

    [Fact]
    public void OneKeyCageConsumesExactCardAndPersistsFreedRepy()
    {
        var start = EnterRoom(Fixture(1));
        var keyId = start.Player.Deck.Single(x => x.CardId == Key).InstanceId;
        var result = Choose(start, "unlock_cage");
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.True(result.World!.FreedRepy);
        Assert.DoesNotContain(result.Player.Deck,
            x => x.InstanceId == keyId);
        Assert.Single(result.Player.Relics,
            x => x.RelicId == PrototypeNativeHiveEvents.HistoryCourseId);
        Assert.Null(result.World.Event);
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(Choose(start.Fork(), "unlock_cage")));
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void ChestPreRollsAllFourOptionalOffersAndTracksTwoPages()
    {
        var start = EnterRoom(Fixture(1));
        var initialHash = CanonicalJson.Sha256(start);
        var reward1 = Choose(start, "unlock_chest");
        Assert.True(reward1.Phase == RunPhase.Reward,
            $"phase={reward1.Phase}; event={reward1.World?.Event?.EventId}; " +
            $"page={reward1.World?.Event?.NativePageIndex}; " +
            $"potions={reward1.World?.Event?.RepyChestPotionIds?.Length}; " +
            $"relics={reward1.World?.Event?.RepyChestRelicIds?.Length}");
        Assert.Equal(2, reward1.World!.Event!.NativePageIndex);
        Assert.DoesNotContain(reward1.Player.Deck, x => x.CardId == Key);
        Assert.Equal(1, reward1.World.Event.RepyChestGroupsResolved);
        var storedPotions = reward1.World.Event.RepyChestPotionIds!;
        var storedRelics = reward1.World.Event.RepyChestRelicIds!;
        Assert.Equal(2, storedPotions.Length);
        Assert.Equal(2, storedRelics.Length);
        Assert.NotEqual(storedRelics[0], storedRelics[1]);
        Assert.Equal(storedPotions[0], reward1.World.Reward!.PotionOption);
        Assert.Equal(storedRelics[0], reward1.World.Reward.RelicOption);
        Assert.True(reward1.World.Reward.IndependentSelection);

        var firstActions = Engine.GetLegalActions(reward1);
        Assert.Contains(firstActions, x => x.Kind == "take_reward_potion");
        Assert.Contains(firstActions, x => x.Kind == "skip_reward_potion");
        Assert.Contains(firstActions, x => x.Kind == "take_reward_relic");
        Assert.Contains(firstActions, x => x.Kind == "leave_reward");
        PrototypeStateInvariants.Validate(reward1);

        // Leaving each offer is legal. Second offer is independent
        // of the choices made on the first reward screen.
        Assert.Contains(firstActions,
            action => action.Kind == "take_reward_extra_potion");
        Assert.Contains(firstActions,
            action => action.Kind == "take_reward_extra_relic");
        var end = Act(reward1, "leave_reward");
        Assert.Equal(RunPhase.MapChoice, end.Phase);
        Assert.False(end.World!.FreedRepy);
        Assert.DoesNotContain(end.Player.Relics,
            x => x.RelicId == PrototypeNativeHiveEvents.HistoryCourseId);
        Assert.Equal(start.Player.Relics.Length, end.Player.Relics.Length);
        Assert.DoesNotContain(end.Player.PotionSlots, x => x is not null);
        Assert.Equal(initialHash, CanonicalJson.Sha256(start));
        Assert.Equal(CanonicalJson.Sha256(end),
            CanonicalJson.Sha256(end.Fork()));
    }

    [Fact]
    public void ChestAllowsClaimingBothPotionsAndRelics()
    {
        var reward = Choose(EnterRoom(Fixture(1)), "unlock_chest");
        var offered = reward.World!.Reward!;
        var acquiredRelics = new[]
        {
            offered.RelicOption!, offered.ExtraRelicRewardIds![0]
        };
        reward = Act(reward, "take_reward_potion");
        reward = Act(reward, "take_reward_extra_potion");
        reward = Act(reward, "take_reward_relic");
        reward = ResolveRelicDeckChoice(reward);
        reward = Act(reward, "take_reward_relic");
        reward = ResolveRelicDeckChoice(reward);
        reward = Act(reward, "leave_reward");
        Assert.Equal(RunPhase.MapChoice, reward.Phase);
        Assert.Equal(2, reward.Player.PotionSlots.Count(x => x is not null));
        Assert.All(acquiredRelics, id => Assert.Contains(
            reward.Player.Relics, relic => relic.RelicId == id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TwoKeysOfferOppositeChoiceAfterFirstIsResolved(bool cageFirst)
    {
        var start = EnterRoom(Fixture(2));
        var first = cageFirst ? "unlock_cage" : "unlock_chest";
        var second = cageFirst ? "unlock_chest" : "unlock_cage";
        var state = Choose(start, first);
        if (!cageFirst)
        {
            Assert.Equal(RunPhase.Reward, state.Phase);
            state = Act(state, "leave_reward");
        }
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(1, state.World!.Event!.NativePageIndex);
        Assert.Equal(new[] { second }, Choices(state));
        Assert.Single(state.Player.Deck, x => x.CardId == Key);
        Assert.Throws<InvalidOperationException>(() =>
            Choose(state, first));

        state = Choose(state, second);
        if (cageFirst)
        {
            Assert.Equal(RunPhase.Reward, state.Phase);
            state = Act(state, "leave_reward");
        }
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.DoesNotContain(state.Player.Deck, x => x.CardId == Key);
        Assert.True(state.World!.FreedRepy);
        Assert.Single(state.Player.Relics,
            x => x.RelicId == PrototypeNativeHiveEvents.HistoryCourseId);
    }

    [Fact]
    public void SecondChoiceConsumesAllFurtherLanternKeys()
    {
        var start = EnterRoom(Fixture(3));
        var afterFirst = Choose(start, "unlock_cage");
        Assert.Equal(2, afterFirst.Player.Deck.Count(x => x.CardId == Key));
        var afterSecond = Choose(afterFirst, "unlock_chest");
        afterSecond = Act(afterSecond, "leave_reward");
        Assert.Equal(RunPhase.MapChoice, afterSecond.Phase);
        Assert.DoesNotContain(afterSecond.Player.Deck, x => x.CardId == Key);
    }

    [Fact]
    public void MalformedQuestContinuationIsRejected()
    {
        var result = Choose(EnterRoom(Fixture(1)), "unlock_chest");
        var evt = result.World!.Event!;
        var invalid = result with
        {
            World = result.World with
            {
                Event = evt with { RepyChestGroupsResolved = 3 }
            }
        };
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeStateInvariants.Validate(invalid));
        var other = EnterRoom(Fixture(2));
        var nonNative = other with
        {
            World = other.World! with
            {
                EventHistory = ["proto.event.fountain"],
                Event = new EventState("proto.event.fountain",
                    RepyFirstChoiceId: "unlock_cage")
            }
        };
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeStateInvariants.Validate(nonNative));
    }

    private static RunState Fixture(int keys,
        PrototypeRoomType room = PrototypeRoomType.Unknown)
    {
        var state = Engine.Step(
            PrototypeGameFactory.Create("glory-repy-v111"),
            GameAction.Empty("start_run")).State;
        var nextId = state.World!.NextCardInstanceId;
        var deck = state.Player.Deck.ToList();
        for (var i = 0; i < keys; i++)
            deck.Add(new CardInstance(
                nextId + i, Key, 0, PrototypeJson.EmptyObject()));
        return state with
        {
            Phase = RunPhase.MapChoice,
            Player = state.Player with { Deck = deck.ToArray() },
            World = state.World with
            {
                Act = 3, Floor = 0,
                NextCardInstanceId = nextId + keys,
                ActiveRoom = null,
                Map = new MapState(
                [
                    new("glory-event", 3, 1, room, ["next"]),
                    new("next", 3, 2, PrototypeRoomType.Rest, [])
                ], EntryNodeIds: ["glory-event"],
                   GenerationProfileId:
                       PrototypeNativeHiveEvents.RepyFixtureMapProfileId),
                Event = null, Combat = null, Reward = null
            }
        };
    }

    private static RunState EnterRoom(RunState state) =>
        Engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload("glory-event"))).State;

    private static string[] Choices(RunState state) =>
        Engine.GetLegalActions(state)
            .Where(a => a.Kind == "event_choice")
            .Select(a => a.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();

    private static RunState Choose(RunState state, string choiceId)
    {
        var action = GameAction.Create("event_choice",
            new EventChoicePayload(choiceId));
        return Engine.Step(state, action).State;
    }

    private static RunState Act(RunState state, string kind) =>
        Engine.Step(state,
            Engine.GetLegalActions(state).Single(a => a.Kind == kind)).State;

    private static RunState ResolveRelicDeckChoice(RunState state)
    {
        while (Engine.GetLegalActions(state).FirstOrDefault(action =>
            action.Kind == "choose_relic_deck_card") is { } choice)
        {
            state = Engine.Step(state, choice).State;
        }
        return state;
    }
}
