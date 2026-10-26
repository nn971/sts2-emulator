using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveInfestedAutomatonTests
{
    [Fact]
    public void HiveEventRosterMatchesPinnedSourceAndNewEventIsGated()
    {
        Assert.Equal(new[]
        {
            "Amalgamator", "Bugslayer", "ColorfulPhilosophers",
            "ColossalFlower", "FieldOfManSizedHoles", "InfestedAutomaton",
            "LostWisp", "SpiritGrafter", "TheLanternKey", "ZenWeaver"
        }, PrototypeNativeHiveEvents.NativeEventNames);

        var evt = PrototypeContent.Event(
            PrototypeNativeHiveEvents.InfestedAutomatonId);
        Assert.Equal(2, evt.MinAct);
        Assert.Equal(2, evt.MaxAct);
        Assert.Equal(0, evt.Weight);
        Assert.Equal(new[] { "study", "touch_core" },
            evt.Choices.Select(option => option.Id));
        Assert.Equal(0, PrototypeContent.Event(
            PrototypeNativeLaterActs.HiveBugslayerId).Weight);
    }

    [Theory]
    [InlineData("study")]
    [InlineData("touch_core")]
    public void BothChoicesGenerateOnlyEligibleSinglePlayerCards(string choice)
    {
        var engine = new PrototypeGameEngine();
        for (var seed = 0; seed < 12; seed++)
        {
            var state = EventRun($"hive-automaton-{choice}-{seed}");
            var originalHash = CanonicalJson.Sha256(state);
            var nextId = state.World!.NextCardInstanceId;
            var available = engine.GetLegalActions(state);
            Assert.Equal(new[] { "study", "touch_core" },
                available.Select(a =>
                    a.ReadPayload<EventChoicePayload>().ChoiceId));
            var action = available.Single(a =>
                a.ReadPayload<EventChoicePayload>().ChoiceId == choice);

            var result = engine.Step(state, action).State;
            result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
            Assert.Equal(RunPhase.MapChoice, result.Phase);
            Assert.Equal(state.Player.Deck.Length + 1,
                result.Player.Deck.Length);
            var added = result.Player.Deck.Last();
            Assert.Equal(nextId, added.InstanceId);
            Assert.Equal(nextId + 1,
                result.World!.NextCardInstanceId);
            Assert.Contains(added.CardId, PrototypeContent.RewardCardPool);

            var card = PrototypeContent.Card(added.CardId);
            if (choice == "study")
            {
                Assert.Equal(PrototypeCardType.Power, card.Type);
            }
            else
            {
                Assert.Equal(PrototypeCardCostKind.Fixed, card.Cost.Kind);
                Assert.Equal(0, card.Cost.AmountAt(0));
            }
            Assert.True(card.MechanicsImplemented);
            Assert.False(card.MultiplayerOnly);
            Assert.Equal(originalHash, CanonicalJson.Sha256(state));
            Assert.Equal(CanonicalJson.Sha256(result),
                CanonicalJson.Sha256(result.Fork()));

            var same = engine.Step(state.Fork(), action).State;
            same = PrototypeEventPageTestHelperV111.ContinueIfFinished(same);
            Assert.Equal(CanonicalJson.Sha256(result),
                CanonicalJson.Sha256(same));
        }
    }

    [Fact]
    public void InvalidEventChoiceIsRejectedWithoutMutatingRun()
    {
        var state = EventRun("hive-automaton-invalid");
        var hash = CanonicalJson.Sha256(state);
        var engine = new PrototypeGameEngine();
        Assert.Throws<InvalidOperationException>(() =>
            engine.Step(state, GameAction.Create(
                "event_choice", new EventChoicePayload("unknown"))));
        Assert.Equal(hash, CanonicalJson.Sha256(state));
    }

    private static RunState EventRun(string seed)
    {
        var initial = new PrototypeGameEngine().Step(
            PrototypeGameFactory.Create(seed),
            GameAction.Empty("start_run")).State;
        var world = initial.World!;
        return initial with
        {
            Phase = RunPhase.Event,
            World = world with
            {
                Act = 2,
                Floor = 1,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(
                    PrototypeNativeHiveEvents.InfestedAutomatonId),
                Map = new MapState(
                [
                    new("current", 2, 1, PrototypeRoomType.Event,
                        ["next"]),
                    new("next", 2, 2, PrototypeRoomType.Combat, [])
                ], "current", ["current"])
            }
        };
    }
}
